using System.Collections.Generic;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DisplayUiTweaks.Hud;

/// <summary>
/// Live previews while a row is highlighted (mouse over it, or selected with keys / gamepad), and while a slider drag
/// that changed it is still held:
///  - UI Area: a thin outline of the UI area for the slider's current value;
///  - HUD &amp; Dialogue UI Size: a half-transparent copy of the HUD (MenuHud, with the user's layout and sample content)
///    over the settings screen.
/// Neither has scripts or raycasters, so the settings screen keeps the mouse and the gamepad (showing the live HUD
/// instead stole the pointer from the row, which then lost its highlight and the preview blinked). The HUD copy is built
/// once per visit to the settings screen and only shown / hidden after that (building it is the expensive part).
/// See docs/internal.md, "Settings preview".
/// </summary>
internal static class SettingsPreview
{
    private const float Opacity = 0.4f;
    private const int SortingOrder = 28000;
    private const float DropCopyAfter = 5f; // seconds without a visible preview row: settings closed, free the copy

    private enum Mode { None, Area, Hud }

    private static Mode _mode;
    private static bool _changedDuringHold;
    private static GameObject? _lastRow;
    private static float _lastRowSeen;
    private static Transform? _copy;
    private static readonly List<Widget> _widgets = new();
    private static GameObject? _overlay;
    private static RectTransform? _frame;
    private static TextMeshProUGUI? _label;

    /// <summary>A previewed setting changed: keep its preview while the mouse button stays down (a drag that slipped off
    /// the row).</summary>
    public static void Poke() => _changedDuringHold = Input.GetMouseButton(0);

    private static Mode Wanted()
    {
        if (HudEditor.On) return Mode.None;
        if (!Input.GetMouseButton(0)) _changedDuringHold = false;
        bool anyVisible = false;
        // The game highlights the row under the mouse / the selected row by activating its m_selectedImage
        // (SettingsItemGUIBase.HandleSelection); the EventSystem selection does not follow the mouse in this menu.
        foreach (var row in SettingsRows.PreviewRows)
        {
            if (row == null || !row.gameObject.activeInHierarchy) continue;
            anyVisible = true;
            var hl = row.m_selectedImage;
            if (hl != null && hl.gameObject.activeSelf) { _lastRow = row.gameObject; return ModeOf(row.gameObject); }
        }
        if (anyVisible) _lastRowSeen = Time.unscaledTime;
        if (_changedDuringHold && _lastRow != null && _lastRow.activeInHierarchy) return ModeOf(_lastRow);
        return Mode.None;
    }

    private static Mode ModeOf(GameObject row) => row.name == SettingsRows.UiAreaRowId ? Mode.Area : Mode.Hud;

    /// <summary>Every frame (late).</summary>
    public static void Tick()
    {
        var want = Wanted();
        if (want != _mode) Switch(want);
        if (_mode == Mode.Area) UpdateFrame();
        else if (_mode == Mode.Hud)
        {
            foreach (var w in _widgets) if (w.Alive) HudLayout.Apply(w);
            HudSamples.FitChatColumns();
        }
        // Settings screen closed (no row of ours visible for a while): free the copy.
        if (_mode == Mode.None && _copy != null && Time.unscaledTime - _lastRowSeen > DropCopyAfter) DropCopy();
    }

    private static void Switch(Mode to)
    {
        if (_mode == Mode.Area && _overlay != null) _overlay.SetActive(false);
        if (_mode == Mode.Hud && _copy != null) MenuHud.SetVisible(false);
        _mode = to;
        if (to == Mode.Area)
        {
            if (_overlay == null) BuildOverlay();
            _overlay!.SetActive(true);
            UpdateFrame();
        }
        else if (to == Mode.Hud)
        {
            if (_copy == null || !MenuHud.Exists) BuildCopy();
            if (_copy != null) MenuHud.SetVisible(true);
        }
    }

    private static void BuildCopy()
    {
        _widgets.Clear();
        _copy = MenuHud.Build();
        if (_copy == null) return;
        _widgets.AddRange(HudLayout.DiscoverWithLayout(_copy));
        MenuHud.Prepare(_copy, _widgets);
        foreach (var cv in _copy.GetComponentsInChildren<Canvas>(true))
        {
            if (!cv.isRootCanvas) continue;
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = SortingOrder;
            var group = cv.GetComponent<CanvasGroup>() ?? cv.gameObject.AddComponent<CanvasGroup>();
            group.alpha *= Opacity;
            group.blocksRaycasts = false;
            group.interactable = false;
        }
        foreach (var w in _widgets) if (w.Alive) HudLayout.Apply(w);
    }

    private static void DropCopy()
    {
        _widgets.Clear();
        _copy = null;
        MenuHud.Destroy();
        HudBoxing.Prune();
    }

    private static void UpdateFrame()
    {
        if (_frame == null) return;
        float area = Prefs.UiAreaValue;
        _frame.sizeDelta = UiBox.FitBox(new Vector2(Screen.width, Screen.height), area);
        if (_label != null) _label.text = "UI Area " + area.ToString("0.00");
    }

    /// <summary>Editor opening / unload / hot reload: everything off and freed.</summary>
    public static void StopNow()
    {
        if (_mode != Mode.None) Switch(Mode.None);
        if (_copy != null) DropCopy();
        if (_overlay != null) Object.Destroy(_overlay);
        _overlay = null;
        _frame = null;
        _label = null;
    }

    private static void BuildOverlay()
    {
        float px = Screen.height / 1080f;
        _overlay = new GameObject("DUT_SettingsPreview");
        Object.DontDestroyOnLoad(_overlay);
        var canvas = _overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder + 1; // no CanvasScaler: 1 unit = 1 px; no GraphicRaycaster: no input
        var frameGo = new GameObject("frame");
        _frame = frameGo.AddComponent<RectTransform>();
        _frame.parent = _overlay.transform;
        _frame.localScale = Vector3.one;
        _frame.anchorMin = _frame.anchorMax = _frame.pivot = new Vector2(0.5f, 0.5f);
        _frame.anchoredPosition = Vector2.zero;
        var color = new Color(0.9f, 0.75f, 0.4f, 0.9f);
        foreach (var (amin, amax, size) in new[]
                 {
                     (new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 2)), (new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 2)),
                     (new Vector2(0, 0), new Vector2(0, 1), new Vector2(2, 0)), (new Vector2(1, 0), new Vector2(1, 1), new Vector2(2, 0)),
                 })
        {
            var edgeGo = new GameObject("edge");
            var edge = edgeGo.AddComponent<RectTransform>();
            edge.parent = _frame;
            edge.localScale = Vector3.one;
            edge.anchorMin = amin; edge.anchorMax = amax; edge.sizeDelta = size * Mathf.Max(1f, px); edge.anchoredPosition = Vector2.zero;
            var img = edgeGo.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }
        // The label uses the settings row's own font.
        TMP_FontAsset? font = null;
        var rowText = _lastRow != null ? _lastRow.GetComponentInChildren<TMP_Text>(true) : null;
        if (rowText != null) font = rowText.font;
        if (font == null) return;
        var labelGo = new GameObject("label");
        var lrt = labelGo.AddComponent<RectTransform>();
        lrt.parent = _frame;
        lrt.localScale = Vector3.one;
        lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0f, 1f);
        lrt.anchoredPosition = new Vector2(10f, -6f) * px;
        lrt.sizeDelta = new Vector2(400f, 40f) * px;
        _label = labelGo.AddComponent<TextMeshProUGUI>();
        _label.font = font;
        _label.fontSize = 20f * px;
        _label.color = color;
        _label.raycastTarget = false;
        _label.enableWordWrapping = false;
    }
}
