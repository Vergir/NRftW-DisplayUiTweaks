using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace MoreAspectRatios;

/// <summary>
/// Canvas and panel scaling.
///  - HUD canvases (fallback DPI 96): the game's own ScaleWithScreenSize result times HudScalePercent.
///  - Menu canvases (fallback DPI 221.5): the game's result, but never larger than what fits the HUD box
///    (box width / 1920 reference width), so a 1920-wide menu is never cropped by a narrow box.
///  - UI Toolkit panels (map, fast travel, activities): same fit rule; they are not inside the box, but they were
///    designed for its width.
/// </summary>
internal static class UiScaling
{
    // ---- uGUI -------------------------------------------------------------------------------------------------

    /// <summary>Unity's CanvasScaler.HandleScaleWithScreenSize formula (UnityEngine.UI source), before any of our changes.</summary>
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

        if (!Prefs.FitMenusToBox.Value) return null;
        float refW = s.referenceResolution.x;
        if (refW <= 0f) return null;
        float fit = UiBox.GlobalBoxWidthPx() / refW;
        return fit < game ? fit : null;
    }

    // ---- UI Toolkit -------------------------------------------------------------------------------------------

    private struct PanelOriginal { public PanelScaleMode Mode; public float Scale; }
    private static readonly Dictionary<int, PanelOriginal> _panelOriginals = new Dictionary<int, PanelOriginal>();

    /// <summary>Unity's PanelSettings.ResolveScale denominator for ScaleWithScreenSize (= the uGUI-style scale factor).</summary>
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

    /// <summary>Re-evaluate every loaded PanelSettings asset. Assets that load later (activity / map screens) are caught
    /// by the UIDocument.OnEnable postfix, which calls ApplyPanel for the document's settings.</summary>
    public static void ApplyPanels()
    {
        int found = 0, changed = 0, restored = 0;
        foreach (var p in Resources.FindObjectsOfTypeAll<PanelSettings>())
        {
            found++;
            var r = ApplyPanel(p);
            if (r > 0) changed++; else if (r < 0) restored++;
        }
        MoreAspectRatiosMod.Log.Msg("PanelSettings: " + found + " loaded, " + changed + " fitted to the HUD box, " + restored + " restored");
    }

    /// <summary>Returns 1 when the panel was fitted, -1 when restored to the game's setting, 0 when untouched.</summary>
    public static int ApplyPanel(PanelSettings p)
    {
        if (p == null) return 0;
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
        if (Prefs.Enabled.Value && Prefs.FitPanelsToBox.Value)
        {
            float refW = p.referenceResolution.x;
            if (refW > 0f)
            {
                float game = GamePanelScale(p, screen);
                float fit = UiBox.GlobalBoxWidthPx() / refW;
                if (fit < game) target = fit;
            }
        }

        if (target.HasValue)
        {
            if (p.scaleMode == PanelScaleMode.ConstantPixelSize && Mathf.Approximately(p.scale, target.Value)) return 0;
            // ConstantPixelSize: ResolveScale returns 1/scale, i.e. 'scale' behaves like a uGUI scale factor.
            p.scaleMode = PanelScaleMode.ConstantPixelSize;
            p.scale = target.Value;
            MoreAspectRatiosMod.Log.Msg("PanelSettings '" + p.name + "': scale " + target.Value.ToString("0.000") + " (fit to HUD box)");
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
