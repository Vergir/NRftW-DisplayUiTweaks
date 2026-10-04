using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Il2CppMoon.Forsaken;
using UnityEngine;
using UnityEngine.UI;

namespace DisplayUiTweaks.Hud;

/// <summary>
/// The user's HUD layout: per element an offset in parts of its parent's size (the UI box) and a scale factor, saved in
/// Prefs.HudLayout as "Id=x,y,scale;...". Applied to the live HUD every frame (cheap: a compare per part), so it follows
/// UI box, HUD size and resolution changes, and wins over the few places the game moves elements itself.
/// </summary>
internal static class HudLayout
{
    public const float MinScale = 0.3f, MaxScale = 3f;

    private static readonly Dictionary<string, (Vector2 offset, float scale)> _saved = new();
    private static string _loadedFrom = "\0";

    /// <summary>The HUD the widgets belong to: PlayerHUD's transform, or the main-menu copy while editing there.</summary>
    public static Transform? Root { get; private set; }
    public static readonly List<Widget> Widgets = new();
    public static PlayerHUD? LiveHud { get; private set; }

    /// <summary>Every frame (late): bind to a new live HUD, apply the layout.</summary>
    public static void Tick()
    {
        if (!HudEditor.OnMenuCopy)
        {
            var hud = PlayerUIService.Instance?.PlayerHud;
            if (hud == null) { if (LiveHud is not null) Unbind(); }
            else if (LiveHud is null || LiveHud.Pointer != hud.Pointer || Widgets.Count == 0 || !Widgets[0].Alive) Bind(hud);
        }
        ApplyAll();
    }

    public static void Bind(PlayerHUD hud)
    {
        LiveHud = hud;
        BindRoot(hud.transform);
    }

    /// <summary>Box the edge-pinned elements of a HUD root, find its widgets, give them the saved layout.</summary>
    public static void BindRoot(Transform root)
    {
        Load();
        HudBoxing.Prune();
        HudBoxing.Apply(root);
        Root = root;
        Widgets.Clear();
        Widgets.AddRange(HudWidgets.Discover(root));
        foreach (var w in Widgets)
            if (_saved.TryGetValue(w.Id, out var s)) { w.Offset = s.offset; w.Scale = Mathf.Clamp(s.scale, MinScale, MaxScale); }
        DisplayUiTweaksMod.Log.Msg($"HUD layout: {Widgets.Count} elements on '{root.name}', {Widgets.FindAll(w => !w.IsDefault).Count} moved");
    }

    public static void Unbind()
    {
        foreach (var w in Widgets) Restore(w);
        Widgets.Clear();
        Root = null;
        LiveHud = null;
    }

    /// <summary>Unload / hot reload: the game's layout back, boxes removed.</summary>
    public static void RestoreAll()
    {
        Unbind();
        HudBoxing.RestoreAll();
    }

    private static void Load()
    {
        string text = Prefs.HudLayout.Value ?? "";
        if (text == _loadedFrom) return;
        _loadedFrom = text;
        _saved.Clear();
        foreach (var entry in text.Split(';'))
        {
            int eq = entry.IndexOf('=');
            if (eq <= 0) continue;
            var v = entry[(eq + 1)..].Split(',');
            if (v.Length < 3) continue;
            if (float.TryParse(v[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                && float.TryParse(v[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
                && float.TryParse(v[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float s))
                _saved[entry[..eq].Trim()] = (new Vector2(x, y), s);
        }
    }

    /// <summary>Store the bound widgets' layout (entries of elements not bound right now are kept).</summary>
    public static void Save()
    {
        Load();
        foreach (var w in Widgets)
        {
            if (w.IsDefault) _saved.Remove(w.Id);
            else _saved[w.Id] = (w.Offset, w.Scale);
        }
        var sb = new StringBuilder();
        foreach (var kv in _saved)
        {
            if (sb.Length > 0) sb.Append(';');
            sb.Append(kv.Key).Append('=')
              .Append(kv.Value.offset.x.ToString("0.#####", CultureInfo.InvariantCulture)).Append(',')
              .Append(kv.Value.offset.y.ToString("0.#####", CultureInfo.InvariantCulture)).Append(',')
              .Append(kv.Value.scale.ToString("0.###", CultureInfo.InvariantCulture));
        }
        Prefs.HudLayout.Value = sb.ToString();
        _loadedFrom = Prefs.HudLayout.Value;
        Prefs.Save();
    }

    public static void ApplyAll()
    {
        foreach (var w in Widgets) Apply(w);
        if (LiveHud != null && !HudEditor.OnMenuCopy) RememberEquipmentLayout(LiveHud);
    }

    private static float _nextLayoutCheck;

    /// <summary>For the main-menu copy: which equipment layout (gamepad / keyboard) the player's HUD shows.</summary>
    private static void RememberEquipmentLayout(PlayerHUD hud)
    {
        if (Time.unscaledTime < _nextLayoutCheck) return;
        _nextLayoutCheck = Time.unscaledTime + 2f;
        var eq = hud.playerEquipmentHUD;
        var pad = eq != null ? eq.m_controllerCanvasGroup : null;
        var kbm = eq != null ? eq.m_kbmCanvasGroup : null;
        if (pad == null || kbm == null) return;
        bool padShown = pad.gameObject.activeInHierarchy && pad.alpha > 0.5f;
        bool kbmShown = kbm.gameObject.activeInHierarchy && kbm.alpha > 0.5f;
        if (padShown != kbmShown) MenuHud.LastControllerLayout = padShown;
    }

    /// <summary>Game value + user offset (and the box inset for layout-driven elements), only writing what differs.</summary>
    public static void Apply(Widget w)
    {
        if (w.Group != null && w.OrigPadding != null)
        {
            var parent = w.Group.transform.TryCast<RectTransform>();
            if (parent != null)
            {
                var size = parent.rect.size;
                var inset = parent.GetComponent<Canvas>() != null ? HudBoxing.InsetFor(parent) : Vector2.zero;
                int dx = Mathf.RoundToInt(w.Offset.x * size.x), dy = Mathf.RoundToInt(w.Offset.y * size.y);
                int ix = Mathf.RoundToInt(inset.x), iy = Mathf.RoundToInt(inset.y);
                var o = w.OrigPadding;
                int l = o.left + ix + dx, r = o.right + ix - dx, t = o.top + iy - dy, b = o.bottom + iy + dy;
                var p = w.Group.padding;
                if (p.left != l || p.right != r || p.top != t || p.bottom != b)
                {
                    w.Group.padding = new RectOffset(l, r, t, b);
                    LayoutRebuilder.MarkLayoutForRebuild(parent);
                }
            }
        }
        for (int i = 0; i < w.Parts.Count; i++)
        {
            var part = w.Parts[i];
            if (part == null) continue;
            if (w.Group == null)
            {
                var cur = part.anchoredPosition;
                if (w.LastPos[i] is Vector2 last && (cur - last).sqrMagnitude > 0.0001f) w.OrigPos[i] = cur; // the game moved it
                var parent = part.parent != null ? part.parent.TryCast<RectTransform>() : null;
                var size = parent != null ? parent.rect.size : Vector2.zero;
                var want = w.OrigPos[i] + Vector2.Scale(w.Offset, size);
                if ((cur - want).sqrMagnitude > 0.0001f) part.anchoredPosition = want;
                w.LastPos[i] = want;
            }
            var curScale = part.localScale;
            if (w.LastScale[i] is Vector3 lastScale && (curScale - lastScale).sqrMagnitude > 1e-8f) w.OrigScale[i] = curScale; // the game rescaled it
            var ws = w.OrigScale[i] * w.Scale;
            if ((curScale - ws).sqrMagnitude > 1e-8f) part.localScale = ws;
            w.LastScale[i] = ws;
        }
    }

    /// <summary>The game's values back (unbind / unload).</summary>
    private static void Restore(Widget w)
    {
        if (w.Group != null && w.OrigPadding != null)
        {
            var o = w.OrigPadding;
            w.Group.padding = new RectOffset(o.left, o.right, o.top, o.bottom);
            var rt = w.Group.transform.TryCast<RectTransform>();
            if (rt != null) LayoutRebuilder.MarkLayoutForRebuild(rt);
        }
        for (int i = 0; i < w.Parts.Count; i++)
        {
            var part = w.Parts[i];
            if (part == null) continue;
            if (w.Group == null) part.anchoredPosition = w.OrigPos[i];
            part.localScale = w.OrigScale[i];
        }
    }

    /// <summary>Move by a screen-pixel delta (converted in the first part's parent space).</summary>
    public static void MoveScreen(Widget w, Vector2 from, Vector2 to)
    {
        RectTransform? parent = null;
        foreach (var p in w.Parts)
            if (p != null && p.parent != null) { parent = p.parent.TryCast<RectTransform>(); if (parent != null) break; }
        if (parent == null) return;
        var cam = HudWidgets.Cam(w);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, from, cam, out var a);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, to, cam, out var b);
        var size = parent.rect.size;
        if (size.x <= 0f || size.y <= 0f) return;
        w.Offset += new Vector2((b.x - a.x) / size.x, (b.y - a.y) / size.y);
        Apply(w);
    }

    public static void ScaleBy(Widget w, float factor)
    {
        w.Scale = Mathf.Clamp(w.Scale * factor, MinScale, MaxScale);
        Apply(w);
    }

    public static void Reset(Widget w)
    {
        w.Offset = Vector2.zero;
        w.Scale = 1f;
        Apply(w);
    }

    /// <summary>Settings row / R in the editor: everything back to the game's layout, saved.</summary>
    public static void ResetAll()
    {
        foreach (var w in Widgets) Reset(w);
        _saved.Clear();
        Prefs.HudLayout.Value = "";
        _loadedFrom = "";
        Prefs.Save();
        DisplayUiTweaksMod.Log.Msg("HUD layout reset");
    }
}
