using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace DisplayUiTweaks;

/// <summary>Canvas scale overrides (HUD / menu) and PanelSettings fitting.</summary>
internal static class UiScaling
{
    // ---- uGUI -------------------------------------------------------------------------------------------------

    /// <summary>Unity's own CanvasScaler.HandleScaleWithScreenSize result.</summary>
    public static float GameCanvasScale(CanvasScaler s, Vector2 screen)
    {
        Vector2 refRes = s.referenceResolution;
        if (refRes.x <= 0f || refRes.y <= 0f) return 1f;
        switch (s.screenMatchMode)
        {
            case CanvasScaler.ScreenMatchMode.MatchWidthOrHeight:
            {
                const float kLogBase = 2f;
                float logWidth = Mathf.Log(screen.x / refRes.x, kLogBase);
                float logHeight = Mathf.Log(screen.y / refRes.y, kLogBase);
                float logWeightedAverage = Mathf.Lerp(logWidth, logHeight, s.matchWidthOrHeight);
                return Mathf.Pow(kLogBase, logWeightedAverage);
            }
            case CanvasScaler.ScreenMatchMode.Expand:
                return Mathf.Min(screen.x / refRes.x, screen.y / refRes.y);
            case CanvasScaler.ScreenMatchMode.Shrink:
                return Mathf.Max(screen.x / refRes.x, screen.y / refRes.y);
            default:
                return 1f;
        }
    }

    /// <summary>Our scale for a ScaleWithScreenSize canvas, or null to let the game's code run unchanged.</summary>
    public static float? OverrideCanvasScale(CanvasScaler s)
    {
        if (!Prefs.Enabled.Value) return null;
        bool isMenu = s.fallbackScreenDPI > Prefs.MenuDpiThreshold.Value;
        var screen = new Vector2(Screen.width, Screen.height);
        float game = GameCanvasScale(s, screen);

        if (!isMenu)
        {
            float hud = Prefs.HudScale;
            return Mathf.Approximately(hud, 1f) ? null : game * hud;
        }

        float target = MenuTarget(game, s.referenceResolution);
        return Mathf.Approximately(target, game) ? null : target;
    }

    /// <summary>Menu scale: the game's value, capped to fit the UI box, times Menu UI Size.</summary>
    private static float MenuTarget(float game, Vector2 refRes)
    {
        float target = game;
        if (Prefs.FitMenusToBox.Value)
        {
            float fit = FitScale(refRes);
            if (fit < target) target = fit;
        }
        return target * Prefs.MenuScale;
    }

    /// <summary>Largest scale at which a reference-size layout still fits inside the UI box (width and height).</summary>
    public static float FitScale(Vector2 refRes)
    {
        Vector2 box = UiBox.GlobalBoxSizePx();
        float fit = float.MaxValue;
        if (refRes.x > 0f) fit = Mathf.Min(fit, box.x / refRes.x);
        if (refRes.y > 0f) fit = Mathf.Min(fit, box.y / refRes.y);
        return fit == float.MaxValue ? 1f : fit;
    }

    // ---- UI Toolkit -------------------------------------------------------------------------------------------

    private struct PanelOriginal { public PanelScaleMode Mode; public float Scale; }
    private static readonly Dictionary<int, PanelOriginal> _panelOriginals = new Dictionary<int, PanelOriginal>();
    // Known PanelSettings assets. Resources.FindObjectsOfTypeAll walks every loaded object, so it only runs on a rescan.
    private static readonly List<PanelSettings> _panels = new List<PanelSettings>();

    /// <summary>Unity's own ScaleWithScreenSize factor for a PanelSettings.</summary>
    public static float GamePanelScale(PanelSettings p, Vector2 screen)
    {
        Vector2 refRes = p.referenceResolution;
        if (refRes.x <= 0f || refRes.y <= 0f) return 1f;
        var ratio = new Vector2(screen.x / refRes.x, screen.y / refRes.y);
        switch (p.screenMatchMode)
        {
            case PanelScreenMatchMode.MatchWidthOrHeight:
            {
                float logW = Mathf.Log(ratio.x, 2f), logH = Mathf.Log(ratio.y, 2f);
                return Mathf.Pow(2f, Mathf.Lerp(logW, logH, p.match));
            }
            case PanelScreenMatchMode.Shrink: return Mathf.Min(ratio.x, ratio.y);
            case PanelScreenMatchMode.Expand: return Mathf.Max(ratio.x, ratio.y);
            default: return 1f;
        }
    }

    /// <summary>Re-evaluate every known PanelSettings asset; rescan = look for newly loaded ones first.</summary>
    public static void ApplyPanels(bool rescan = false)
    {
        if (rescan)
        {
            _panels.Clear();
            foreach (var p in Resources.FindObjectsOfTypeAll<PanelSettings>()) if (p != null) _panels.Add(p);
        }
        int found = 0, changed = 0, restored = 0;
        foreach (var p in _panels)
        {
            if (p == null) continue;
            found++;
            var r = ApplyPanel(p);
            if (r > 0) changed++; else if (r < 0) restored++;
        }
        if (changed + restored > 0)
            DisplayUiTweaksMod.Log.Msg("PanelSettings: " + found + " loaded, " + changed + " fitted to the UI box, " + restored + " restored");
    }

    /// <summary>Restore every panel we changed.</summary>
    public static void RestorePanels()
    {
        foreach (var p in Resources.FindObjectsOfTypeAll<PanelSettings>())
        {
            if (p == null || !_panelOriginals.TryGetValue(p.GetInstanceID(), out var orig)) continue;
            p.scaleMode = orig.Mode;
            p.scale = orig.Scale;
        }
        _panelOriginals.Clear();
    }

    /// <summary>Returns 1 when the panel was fitted, -1 when restored to the game's setting, 0 when untouched.</summary>
    public static int ApplyPanel(PanelSettings p)
    {
        if (p == null) return 0;
        if (!_panels.Exists(x => x != null && x.Pointer == p.Pointer)) _panels.Add(p);
        var screen = new Vector2(Screen.width, Screen.height);
        int id = p.GetInstanceID();
        bool known = _panelOriginals.TryGetValue(id, out var orig);
        if (!known)
        {
            if (p.scaleMode != PanelScaleMode.ScaleWithScreenSize) return 0;
            orig = new PanelOriginal { Mode = p.scaleMode, Scale = p.scale };
            _panelOriginals[id] = orig;
        }

        float? target = null;
        if (Prefs.Enabled.Value)
        {
            float game = GamePanelScale(p, screen);
            float t = game;
            if (Prefs.FitPanelsToBox.Value)
            {
                var rr = p.referenceResolution;
                float fit = FitScale(new Vector2(rr.x, rr.y));
                if (fit < t) t = fit;
            }
            t *= Prefs.MenuScale;
            if (!Mathf.Approximately(t, game)) target = t;
        }

        if (target.HasValue)
        {
            if (p.scaleMode == PanelScaleMode.ConstantPixelSize && Mathf.Approximately(p.scale, target.Value)) return 0;
            p.scaleMode = PanelScaleMode.ConstantPixelSize;
            p.scale = target.Value;
            DisplayUiTweaksMod.Log.Msg("PanelSettings '" + p.name + "': scale " + target.Value.ToString("0.000") + " (fit to UI box, Menu UI Size applied)");
            return 1;
        }
        if (p.scaleMode != orig.Mode || !Mathf.Approximately(p.scale, orig.Scale))
        {
            p.scaleMode = orig.Mode;
            p.scale = orig.Scale;
            return -1;
        }
        return 0;
    }
}
