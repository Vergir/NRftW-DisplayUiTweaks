using System.Collections.Generic;
using HarmonyLib;
using Il2CppMoon.Forsaken;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DisplayUiTweaks.Hud;

/// <summary>
/// Edit HUD Layout: the real HUD (or, in the main menu, a copy of the HUD prefab) on top of the game with sample
/// content; the mouse picks the element whose visible art is under it (smallest first, Tab cycles through the others
/// there, the picked one draws on top), drag = move (its centre stays on screen), wheel = resize, right click = reset
/// the element, R = reset all, Esc = done (saves). Gameplay input is blocked with the game's own
/// PlayerOverlay.BlockInputThisFrame; the menu it was opened from is hidden meanwhile. See docs/internal.md,
/// "HUD layout editor".
/// </summary>
internal static class HudEditor
{
    public static bool On { get; private set; }
    /// <summary>Editing the main-menu copy of the HUD (no live HUD).</summary>
    public static bool OnMenuCopy { get; private set; }

    private const string RootName = "DUT_HudEditor";
    private static GameObject? _root;
    private static TextMeshProUGUI? _cursorLabel;
    private static CanvasGroup? _panelGroup;
    private static readonly List<(Widget w, RectTransform box, Image fill)> _placeholders = new();
    private static readonly Dictionary<Widget, List<Graphic>> _graphics = new();
    private static readonly Dictionary<Widget, Rect> _rects = new();
    private static float _nextGraphicsScan;
    private static Widget? _drag, _hover, _front;
    private static Vector2 _grabMouse, _grabPos;
    private static readonly List<Widget> _under = new();
    private static int _cycle;
    private static readonly List<(Canvas c, bool was)> _hiddenCanvases = new();
    private static readonly List<(Canvas c, int order)> _raised = new();
    private static readonly List<(Canvas c, Camera? cam, float distance)> _overlays = new();
    private static int _enteredFrame, _exitFrame = -10;

    // Resize handle (chat window): an L-shaped bracket on the corner opposite the element's pivot.
    private static RectTransform? _handle;
    private static RectTransform? _handleBarX, _handleBarY;
    private static Widget? _resizing, _handleHover;
    private static Vector2 _resizeStartLocal, _resizeStartSize;

    /// <summary>Esc while editing (and in the frames right after) belongs to the editor, not to the settings screen.</summary>
    public static bool SuppressBack => On || Time.frameCount <= _exitFrame + 2;

    /// <summary>The live HUD is kept shown and fully visible while editing it.</summary>
    public static bool KeepLiveHudShown => On && !OnMenuCopy;
    private static bool _hudWasHidden, _exiting;

    public static void Enter()
    {
        if (On) return;
        SettingsPreview.StopNow();
        Showcase.Set(false);
        try
        {
            var live = PlayerUIService.Instance?.PlayerHud;
            if (live != null)
            {
                OnMenuCopy = false;
                if (HudLayout.LiveHud is null || HudLayout.LiveHud.Pointer != live.Pointer || HudLayout.Widgets.Count == 0) HudLayout.Bind(live);
            }
            else
            {
                var copy = MenuHud.Build();
                if (copy == null) { DisplayUiTweaksMod.Log.Warning("Edit HUD Layout: no HUD in memory"); return; }
                OnMenuCopy = true;
                HudLayout.BindRoot(copy);
                MenuHud.Prepare(copy, HudLayout.Widgets);
            }
            HideOtherUi(live);
            if (live != null)
            {
                // The settings screen deactivates the whole HUD (PlayerHUD.Hide) every frame; other menus only fade it.
                _hudWasHidden = live.IsHidden || !live.gameObject.activeInHierarchy;
                if (_hudWasHidden) live.Show();
                HudSamples.Begin(live);
                OverlaysToCamera();
            }
            Build();
            On = true;
            _enteredFrame = Time.frameCount;
            DisplayUiTweaksMod.Log.Msg($"HUD editor on ({(OnMenuCopy ? "main menu copy" : "live HUD")})");
        }
        catch (System.Exception e)
        {
            DisplayUiTweaksMod.Log.Error("Edit HUD Layout failed: " + e);
            Exit();
        }
    }

    public static void Exit()
    {
        bool was = On;
        On = false;
        if (was) _exitFrame = Time.frameCount;
        _drag = null;
        _resizing = null;
        RestoreOrder();
        if (was) HudLayout.Save();
        var live = HudLayout.LiveHud;
        RestoreOverlays();
        if (!OnMenuCopy && live != null)
        {
            HudSamples.End(live);
            _exiting = true;
            try { if (_hudWasHidden) live.Hide(); }
            finally { _exiting = false; }
        }
        _hudWasHidden = false;
        foreach (var (c, en) in _hiddenCanvases) if (c != null) c.enabled = en;
        _hiddenCanvases.Clear();
        if (OnMenuCopy)
        {
            HudLayout.Unbind();
            MenuHud.Destroy();
            OnMenuCopy = false;
        }
        if (_root != null) Object.Destroy(_root);
        _root = null;
        _placeholders.Clear();
        _graphics.Clear();
        _rects.Clear();
        if (was) DisplayUiTweaksMod.Log.Msg("HUD editor off, layout saved: " + (Prefs.HudLayout.Value.Length > 0 ? Prefs.HudLayout.Value : "(game layout)"));
    }

    /// <summary>In game: the player menu's canvases (the menu stays open, so the game keeps ignoring gameplay input).
    /// Main menu: every other root canvas (title, buttons, the settings screen).</summary>
    private static void HideOtherUi(PlayerHUD? live)
    {
        _hiddenCanvases.Clear();
        if (live != null)
        {
            var menu = Object.FindObjectOfType<Il2Cpp.PlayerMenu>();
            if (menu != null && menu.IsOpen)
                foreach (var c in new[] { menu.ForegroundCanvas, menu.BackgroundCanvas })
                    if (c != null && c.enabled) { _hiddenCanvases.Add((c, true)); c.enabled = false; }
            return;
        }
        var copyRoot = HudLayout.Root;
        foreach (var c in Object.FindObjectsOfType<Canvas>())
        {
            if (c == null || !c.enabled || !c.isRootCanvas) continue;
            if (copyRoot != null && c.transform.IsChildOf(copyRoot)) continue;
            _hiddenCanvases.Add((c, true));
            c.enabled = false;
        }
    }

    /// <summary>OnUpdate and OnLateUpdate: the game's own "an overlay has the input" switch, per frame.</summary>
    public static void BlockGameInput()
    {
        if (!On || OnMenuCopy) return;
        var overlay = PlayerUIService.Instance?.PlayerOverlay;
        if (overlay != null) overlay.BlockInputThisFrame();
    }

    private static void Attach(GameObject go, Transform parent)
    {
        go.transform.parent = parent;
        go.transform.localScale = Vector3.one;
        go.transform.localPosition = Vector3.zero;
        go.layer = parent.gameObject.layer;
    }

    private static TMP_FontAsset? Font()
    {
        if (HudLayout.Root == null) return null;
        foreach (var t in HudLayout.Root.GetComponentsInChildren<TMP_Text>(true))
            if (t != null && t.font != null) return t.font;
        return null;
    }

    private static TextMeshProUGUI Text(Transform parent, string name, TMP_FontAsset font, float size)
    {
        var go = new GameObject(name);
        Attach(go, parent);
        go.AddComponent<RectTransform>();
        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.fontSize = size;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        t.outlineWidth = 0.25f;
        t.outlineColor = new Color32(0, 0, 0, 255);
        return t;
    }

    private static Image Box(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        Attach(go, parent);
        go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static void Build()
    {
        var old = GameObject.Find(RootName);
        if (old != null) Object.Destroy(old);
        _root = new GameObject(RootName);
        Object.DontDestroyOnLoad(_root);
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000; // no CanvasScaler: 1 unit = 1 px
        float px = Screen.height / 1080f;
        _placeholders.Clear();
        foreach (var w in HudLayout.Widgets)
        {
            var fill = Box(_root.transform, "placeholder_" + w.Id, Color.clear);
            var rt = fill.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            _placeholders.Add((w, rt, fill));
        }
        var font = Font();
        if (font != null)
        {
            // Instructions: a framed panel a bit above the middle of the screen (the top centre is the hint bar's).
            var panel = Box(_root.transform, "panel", new Color(0.05f, 0.05f, 0.07f, 0.82f));
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = new Vector2(0f, Screen.height * 0.17f);
            prt.sizeDelta = new Vector2(470f, 275f) * px;
            _panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            _panelGroup.blocksRaycasts = false;
            var frameColor = new Color(0.85f, 0.7f, 0.4f, 0.9f);
            foreach (var (amin, amax, size) in new[]
                     {
                         (new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 2)), (new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 2)),
                         (new Vector2(0, 0), new Vector2(0, 1), new Vector2(2, 0)), (new Vector2(1, 0), new Vector2(1, 1), new Vector2(2, 0)),
                     })
            {
                var edge = Box(panel.transform, "edge", frameColor).rectTransform;
                edge.anchorMin = amin; edge.anchorMax = amax; edge.sizeDelta = size * px; edge.anchoredPosition = Vector2.zero;
            }
            var text = Text(panel.transform, "text", font, 20f * px);
            var trt = text.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.sizeDelta = new Vector2(-32f, -24f) * px; trt.anchoredPosition = Vector2.zero;
            text.alignment = TextAlignmentOptions.Center;
            text.lineSpacing = 8f;
            text.text = "<size=125%><color=#E8C878>Edit HUD Layout</color></size>\n" +
                        "Drag an element to move it\n" +
                        "Mouse wheel: resize\n" +
                        "Chat: drag its corner bracket to change its size\n" +
                        "Right click: reset the element\n" +
                        "Tab: next element under the cursor\n" +
                        "R: reset everything      Esc: done";
            _cursorLabel = Text(_root.transform, "cursor", font, 18f * px);
            var crt = _cursorLabel.rectTransform;
            crt.anchorMin = crt.anchorMax = Vector2.zero;
            crt.pivot = new Vector2(0f, 1f);
            crt.sizeDelta = new Vector2(900f, 40f) * px;
        }
        var handleGo = new GameObject("resizeHandle");
        Attach(handleGo, _root.transform);
        _handle = handleGo.AddComponent<RectTransform>();
        _handle.anchorMin = _handle.anchorMax = Vector2.zero;
        _handle.sizeDelta = Vector2.zero;
        _handleBarX = Box(_handle, "barX", Color.clear).rectTransform;
        _handleBarY = Box(_handle, "barY", Color.clear).rectTransform;
        handleGo.SetActive(false);
        _graphics.Clear();
        _nextGraphicsScan = 0f;
    }

    /// <summary>Places the resize bracket on the chat's free corner; returns the widget when the cursor is on it.</summary>
    private static Widget? UpdateHandle(Vector2 mouse, float px)
    {
        if (_handle == null || _handleBarX == null || _handleBarY == null) return null;
        Widget? chat = null;
        foreach (var w in HudLayout.Widgets) if (w.Resizable && w.Alive && w.Parts[0].gameObject.activeInHierarchy) { chat = w; break; }
        _handle.gameObject.SetActive(chat != null);
        if (chat == null) return null;
        var part = chat.Parts[0];
        var r = HudWidgets.ScreenRect(part, HudWidgets.Cam(chat));
        // The corner opposite the pivot (the chat is anchored bottom-right: its top-left corner).
        bool left = part.pivot.x > 0.5f, top = part.pivot.y < 0.5f;
        var corner = new Vector2(left ? r.xMin : r.xMax, top ? r.yMax : r.yMin);
        float len = 34f * px, thick = 5f * px;
        _handle.anchoredPosition = corner;
        // Bars run from the corner along the two edges, into the element.
        _handleBarX.anchorMin = _handleBarX.anchorMax = Vector2.zero;
        _handleBarX.pivot = new Vector2(left ? 0f : 1f, top ? 1f : 0f);
        _handleBarX.sizeDelta = new Vector2(len, thick);
        _handleBarX.anchoredPosition = Vector2.zero;
        _handleBarY.anchorMin = _handleBarY.anchorMax = Vector2.zero;
        _handleBarY.pivot = _handleBarX.pivot;
        _handleBarY.sizeDelta = new Vector2(thick, len);
        _handleBarY.anchoredPosition = Vector2.zero;
        var hit = Rect.MinMaxRect(corner.x - (left ? 12f * px : len), corner.y - (top ? len : 12f * px),
                                  corner.x + (left ? len : 12f * px), corner.y + (top ? 12f * px : len));
        bool on = _resizing == chat || (_drag == null && _resizing == null && hit.Contains(mouse));
        var color = on ? new Color(1f, 0.85f, 0.35f, 1f) : new Color(0.9f, 0.75f, 0.4f, 0.85f);
        _handleBarX.GetComponent<Image>().color = color;
        _handleBarY.GetComponent<Image>().color = color;
        return hit.Contains(mouse) ? chat : null;
    }

    private static void StartResize(Widget w, Vector2 mouse)
    {
        var part = w.Parts[0];
        var parent = part.parent != null ? part.parent.TryCast<RectTransform>() : null;
        if (parent == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, mouse, HudWidgets.Cam(w), out _resizeStartLocal);
        _resizeStartSize = part.sizeDelta;
        _resizing = w;
    }

    private static void DoResize(Widget w, Vector2 mouse)
    {
        var part = w.Parts[0];
        var parent = part.parent != null ? part.parent.TryCast<RectTransform>() : null;
        if (parent == null || w.OrigSize[0].x <= 0f || w.OrigSize[0].y <= 0f) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, mouse, HudWidgets.Cam(w), out var local);
        var d = local - _resizeStartLocal;
        float sx = part.pivot.x > 0.5f ? -1f : 1f, sy = part.pivot.y > 0.5f ? -1f : 1f;
        float scale = Mathf.Max(0.01f, part.localScale.x);
        var size = _resizeStartSize + new Vector2(sx * d.x, sy * d.y) / scale;
        HudLayout.ResizeTo(w, new Vector2(size.x / w.OrigSize[0].x, size.y / w.OrigSize[0].y));
    }

    private static void ScanGraphics()
    {
        _graphics.Clear();
        foreach (var w in HudLayout.Widgets)
        {
            var list = new List<Graphic>();
            foreach (var p in w.Parts)
                if (p != null && p.gameObject.activeInHierarchy)
                    foreach (var g in p.GetComponentsInChildren<Graphic>(false)) if (g != null) list.Add(g);
            _graphics[w] = list;
        }
    }

    private static bool Visible(Graphic g) =>
        g.enabled && g.gameObject.activeInHierarchy && !g.canvasRenderer.cull && g.color.a * g.canvasRenderer.GetInheritedAlpha() >= 0.05f;

    /// <summary>Every frame (late) while editing.</summary>
    public static void Tick()
    {
        if (!On || _root == null) return;
        if (!OnMenuCopy && HudLayout.LiveHud != null) HudSamples.ForceVisible(HudLayout.LiveHud);
        HudSamples.FitChatColumns();
        BlockGameInput();
        if (Time.frameCount > _enteredFrame && Input.GetKeyDown(KeyCode.Escape)) { Exit(); return; }
        if (Input.GetKeyDown(KeyCode.R)) HudLayout.ResetAll();
        if (Time.unscaledTime >= _nextGraphicsScan) { ScanGraphics(); _nextGraphicsScan = Time.unscaledTime + 1f; }
        Vector2 mouse = Input.mousePosition;
        float screenArea = Screen.width * (float)Screen.height;

        // Per element: the screen rect of what it draws, and whether the cursor is on any of its visible art.
        var hits = new List<(Widget w, float area, bool placeholder)>();
        _rects.Clear();
        foreach (var (w, box, fill) in _placeholders)
        {
            if (!w.Alive) { box.gameObject.SetActive(false); continue; }
            var cam = HudWidgets.Cam(w);
            bool anyVisible = false;
            float hitArea = float.MaxValue;
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            if (_graphics.TryGetValue(w, out var list))
                foreach (var g in list)
                {
                    if (g == null || !Visible(g)) continue;
                    var r = HudWidgets.ScreenRect(g.rectTransform, cam);
                    if (r.width < 2f || r.height < 2f) continue;
                    float area = r.width * r.height;
                    if (area > screenArea * 0.4f) continue; // full-screen backdrops
                    anyVisible = true;
                    x0 = Mathf.Min(x0, r.xMin); y0 = Mathf.Min(y0, r.yMin); x1 = Mathf.Max(x1, r.xMax); y1 = Mathf.Max(y1, r.yMax);
                    if (r.Contains(mouse)) hitArea = Mathf.Min(hitArea, area);
                }
            // Nothing drawn (empty list, inactive element): a faint placeholder at its rect so it can still be grabbed.
            box.gameObject.SetActive(!anyVisible);
            Rect rect;
            if (anyVisible) rect = Rect.MinMaxRect(x0, y0, x1, y1);
            else
            {
                rect = HudWidgets.PartsRect(w);
                box.anchoredPosition = rect.position;
                box.sizeDelta = rect.size;
                if (rect.Contains(mouse)) hitArea = rect.width * rect.height;
            }
            _rects[w] = rect;
            if (hitArea < float.MaxValue) hits.Add((w, hitArea, !anyVisible));
        }
        // Visible art first (smallest first), placeholders of empty elements after it: a placeholder strip lying over
        // the chat window must not take the chat's clicks.
        hits.Sort((a, b) => a.placeholder != b.placeholder ? a.placeholder.CompareTo(b.placeholder) : a.area.CompareTo(b.area));

        // Tab cycles through everything under the cursor; the pick stays while the same elements are there.
        bool sameSet = hits.Count == _under.Count && hits.TrueForAll(h => _under.Contains(h.w));
        if (!sameSet) { _cycle = 0; _under.Clear(); foreach (var h in hits) _under.Add(h.w); }
        if (Input.GetKeyDown(KeyCode.Tab) && _under.Count > 1) _cycle = (_cycle + 1) % _under.Count;
        _hover = _under.Count > 0 ? _under[Mathf.Min(_cycle, _under.Count - 1)] : null;

        float px = Screen.height / 1080f;
        _handleHover = UpdateHandle(mouse, px);
        var target = _resizing ?? _drag ?? _handleHover ?? _hover;
        BringToFront(target);
        foreach (var (w, box, fill) in _placeholders)
            fill.color = w == target ? new Color(1f, 0.8f, 0.2f, 0.25f) : new Color(1f, 1f, 1f, 0.08f);
        if (_panelGroup != null) _panelGroup.alpha = _drag != null || _resizing != null ? 0.15f : 1f;
        if (_cursorLabel != null)
        {
            _cursorLabel.gameObject.SetActive(target != null);
            if (target != null)
            {
                _cursorLabel.text = (_resizing != null || (_drag == null && _handleHover != null)) ? $"{target.Name}: drag to resize"
                                  : _drag == null && _under.Count > 1 ? $"{target.Name}   <alpha=#99>({_cycle + 1}/{_under.Count}, Tab for next)" : target.Name;
                _cursorLabel.rectTransform.anchoredPosition = mouse + new Vector2(24f, -8f) * (Screen.height / 1080f);
            }
        }

        if (Input.GetMouseButtonDown(0) && _handleHover != null) StartResize(_handleHover, mouse);
        else if (Input.GetMouseButtonDown(0) && _hover != null && _rects.TryGetValue(_hover, out var start))
        {
            _drag = _hover;
            _grabMouse = mouse;
            _grabPos = start.position;
        }
        if (_drag != null && Input.GetMouseButton(0) && _rects.TryGetValue(_drag, out var cur))
        {
            // Where the element should be: its centre stays on screen, so it can always be grabbed again (art that
            // reaches past the screen edge, e.g. the health frame's glow, still fits into the corners).
            var want = _grabPos + (mouse - _grabMouse);
            want.x = Mathf.Clamp(want.x, -cur.width * 0.5f, Screen.width - cur.width * 0.5f);
            want.y = Mathf.Clamp(want.y, -cur.height * 0.5f, Screen.height - cur.height * 0.5f);
            var delta = want - cur.position;
            if (delta.sqrMagnitude > 0.01f) HudLayout.MoveScreen(_drag, mouse, mouse + delta);
        }
        if (_resizing != null && Input.GetMouseButton(0)) DoResize(_resizing, mouse);
        if (Input.GetMouseButtonUp(0)) { _drag = null; _resizing = null; }
        float wheel = Input.mouseScrollDelta.y;
        if (_hover != null && Mathf.Abs(wheel) > 0.01f) HudLayout.ScaleBy(_hover, Mathf.Pow(1.05f, wheel));
        if (_hover != null && Input.GetMouseButtonDown(1)) HudLayout.Reset(_hover);
    }

    /// <summary>
    /// The area banner's canvas is a screen overlay, and overlays draw above every camera canvas whatever their sorting
    /// order: while editing it draws through the HUD camera like the rest, so "bring to front" works on it too.
    /// </summary>
    private static void OverlaysToCamera()
    {
        _overlays.Clear();
        Canvas? reference = null;
        foreach (var w in HudLayout.Widgets)
            if (w.Root != null && w.Root.renderMode == RenderMode.ScreenSpaceCamera && w.Root.worldCamera != null) { reference = w.Root; break; }
        if (reference == null) return;
        foreach (var w in HudLayout.Widgets)
        {
            var c = w.Root;
            if (c == null || c.renderMode != RenderMode.ScreenSpaceOverlay || _overlays.Exists(o => o.c == c)) continue;
            _overlays.Add((c, c.worldCamera, c.planeDistance));
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = reference.worldCamera;
            c.planeDistance = reference.planeDistance;
        }
    }

    private static void RestoreOverlays()
    {
        foreach (var (c, cam, distance) in _overlays)
        {
            if (c == null) continue;
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.worldCamera = cam;
            c.planeDistance = distance;
        }
        _overlays.Clear();
    }

    /// <summary>The element under the cursor (or picked with Tab) draws above the others: its root canvas sorts higher.</summary>
    private static void BringToFront(Widget? w)
    {
        if (w == _front) return;
        RestoreOrder();
        _front = w;
        if (w?.Root == null) return;
        _raised.Add((w.Root, w.Root.sortingOrder));
        w.Root.sortingOrder += 100;
    }

    private static void RestoreOrder()
    {
        foreach (var (c, order) in _raised) if (c != null) c.sortingOrder = order;
        _raised.Clear();
        _front = null;
    }

    /// <summary>The settings screen keeps hiding the HUD (PlayerHUD.Hide deactivates its parts): not while editing.</summary>
    [HarmonyPatch(typeof(PlayerHUD), nameof(PlayerHUD.Hide))]
    private static class NoHidePatch
    {
        private static bool Prefix() => !KeepLiveHudShown || _exiting;
    }

    /// <summary>The boss bar fades itself in its own update (after ours): keep it up while editing the live HUD.</summary>
    [HarmonyPatch(typeof(BossStatsView), nameof(BossStatsView.UpdateHud))]
    private static class BossVisiblePatch
    {
        private static void Postfix(BossStatsView __instance)
        {
            if (KeepLiveHudShown && __instance.CanvasGroup != null) __instance.CanvasGroup.alpha = 1f;
        }
    }

    [HarmonyPatch(typeof(PlagueMeterHUD), "Update")]
    private static class PlagueVisiblePatch
    {
        private static void Postfix(PlagueMeterHUD __instance)
        {
            if (KeepLiveHudShown && __instance.CanvasGroup != null) __instance.CanvasGroup.alpha = 1f;
        }
    }

    /// <summary>Keep the HUD fully visible while editing: menus fade it out through these CanvasGroups.</summary>
    [HarmonyPatch(typeof(PlayerUIService), nameof(PlayerUIService.OnLateUpdate))]
    private static class ForceVisiblePatch
    {
        private static void Postfix()
        {
            if (!KeepLiveHudShown) return;
            var settings = PlayerUIService.PlayerHudSettings;
            if (settings != null)
                foreach (var s in settings)
                {
                    if (s?.canvasGroups == null) continue;
                    foreach (var g in s.canvasGroups) if (g != null) g.alpha = 1f;
                }
            if (HudLayout.LiveHud != null) HudSamples.ForceVisible(HudLayout.LiveHud);
        }
    }
}
