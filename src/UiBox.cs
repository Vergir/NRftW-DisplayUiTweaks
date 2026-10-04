using System.Collections.Generic;
using Il2CppMoon.Forsaken;
using UnityEngine;
using UnityEngine.UI;

namespace DisplayUiTweaks;

/// <summary>The UI box: replacement for UIAspectConstraint.ApplyConstraint (see docs/internal.md).</summary>
internal static class UiBox
{
    private const float Aspect16x9 = 1.7777778f;
    private const float Aspect21x9 = 2.3333333f;
    private const float Aspect32x9 = 3.5555556f;

    // Fixed-size screens we shrank to fit the box, by instance id (to undo on unload / when no longer needed).
    private static readonly Dictionary<int, RectTransform> _fitted = new Dictionary<int, RectTransform>();

    /// <summary>Aspect of the box for a mode, or 0 for Native (no box).</summary>
    public static float TargetAspect(UIAspectMode mode)
    {
        switch (mode)
        {
            case UIAspectMode.Native:     return 0f;
            case UIAspectMode.Aspect16x9: return Aspect16x9;
            case UIAspectMode.Aspect21x9: return Aspect21x9;
            case UIAspectMode.Aspect32x9: return Aspect32x9;
            default:                      return Aspect16x9; // Custom: never offered by the game (1.0.0 used it)
        }
    }

    /// <summary>Aspect of the global UI box: the UI Area setting (it replaces the game's UI Aspect Mode).</summary>
    public static float GlobalAspect() => Prefs.UiAreaAspect;

    /// <summary>The largest box of the target aspect inside a parent of the given size (the parent itself when 0 = Native).</summary>
    public static Vector2 FitBox(Vector2 parent, float targetAspect)
    {
        if (targetAspect <= 0f) return parent;
        if (parent.x / parent.y > targetAspect) return new Vector2(parent.y * targetAspect, parent.y);
        return new Vector2(parent.x, parent.x / targetAspect);
    }

    /// <summary>Size in screen pixels of the global UI box (the whole screen when Native).</summary>
    public static Vector2 GlobalBoxSizePx()
    {
        var screen = new Vector2(Screen.width, Screen.height);
        return FitBox(screen, GlobalAspect());
    }

    /// <summary>True when the global UI does not cover the whole screen.</summary>
    public static bool GlobalBoxIsSmallerThanScreen()
    {
        var box = GlobalBoxSizePx();
        return box.x < Screen.width - 0.5f || box.y < Screen.height - 0.5f;
    }

    /// <summary>Full replacement for UIAspectConstraint.ApplyConstraint.</summary>
    public static void ApplyConstraint(UIAspectConstraint c)
    {
        Vector2 parent = c.GetParentSize();
        if (parent.x <= 0f || parent.y <= 0f) return;

        bool global = c.m_useGlobalMode && Application.isPlaying;
        RectTransform rt = c.m_rectTransform;
        float target = global ? GlobalAspect() : TargetAspect(c.m_mode);

        if (target <= 0f)
        {
            if (rt != null) { Unfit(rt); UIAspectConstraint.ApplyStretchToParent(rt); }
            c.ApplySafeZone(parent.x, parent.y);
            return;
        }

        Vector2 box;
        if (!Prefs.StretchMismatchedRoots.Value)
        {
            box = FitBox(parent, target);   // the game's rule: the aspect box inside the parent
            if (rt != null) Unfit(rt);
        }
        else
        {
            // The box is the overlap of the parent with the screen's UI box, measured in pixels. For a screen-sized
            // parent that is the game's rule; parents that are only partly screen-sized (full width, own height) or
            // fixed-size (1920x1080 roots) keep their own size wherever it is already inside the box.
            float cs = ExpectedCanvasScale(c, rt);
            Vector2 screen = new Vector2(Screen.width, Screen.height);
            Vector2 boxPx = FitBox(screen, target);
            Vector2 parentPx = parent * cs;
            // Fixed-size = the parent does not stretch with its own parent (anchors collapsed on both axes), is not a
            // canvas root, and is clearly smaller than the screen.
            var prt = rt != null ? rt.parent?.TryCast<RectTransform>() : null;
            bool fixedSize = prt != null && prt.anchorMin == prt.anchorMax
                && prt.GetComponent<Canvas>() == null
                && parentPx.x < screen.x * 0.98f && parentPx.y < screen.y * 0.98f;
            if (fixedSize)
            {
                // Its content is laid out for the parent's size: keep that size, shrink to fit if it is larger.
                if (rt != null) { UIAspectConstraint.ApplyStretchToParent(rt); FitFixedRoot(rt, parentPx, boxPx); }
                c.ApplySafeZone(parent.x, parent.y);
                return;
            }
            box = new Vector2(Mathf.Min(parent.x, boxPx.x / cs), Mathf.Min(parent.y, boxPx.y / cs));
            if (rt != null) Unfit(rt);
        }

        if (rt != null)
        {
            var half = new Vector2(0.5f, 0.5f);
            rt.anchorMin = half; rt.anchorMax = half; rt.pivot = half;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = box;
        }
        c.ApplySafeZone(box.x, box.y);
    }

    /// <summary>Scales a fixed-size screen down so it fits the UI box (never up).</summary>
    private static void FitFixedRoot(RectTransform rt, Vector2 parentPx, Vector2 boxPx)
    {
        float s = Mathf.Min(1f, boxPx.x / parentPx.x, boxPx.y / parentPx.y);
        if (s >= 0.999f) { Unfit(rt); return; }
        rt.pivot = new Vector2(0.5f, 0.5f);
        if (Mathf.Abs(rt.localScale.x - s) > 0.001f)
        {
            rt.localScale = new Vector3(s, s, 1f);
            DisplayUiTweaksMod.Log.Msg("Fitted fixed-size screen '" + ScreenName(rt) + "' to the UI box (scale " + s.ToString("0.000") + ")");
        }
        _fitted[rt.GetInstanceID()] = rt;
    }

    private static void Unfit(RectTransform rt)
    {
        if (_fitted.Remove(rt.GetInstanceID())) rt.localScale = Vector3.one;
    }

    /// <summary>Undo every fit (unload / hot reload).</summary>
    public static void RestoreFixedRoots()
    {
        foreach (var rt in _fitted.Values) if (rt != null) rt.localScale = Vector3.one;
        _fitted.Clear();
    }

    /// <summary>Screen pixels per canvas unit for this constraint, as the canvas will be scaled this frame
    /// (the scaler may not have applied a new value yet).</summary>
    private static float ExpectedCanvasScale(UIAspectConstraint c, RectTransform? rt)
    {
        var scaler = c.GetComponentInParent<CanvasScaler>();
        if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            return UiScaling.OverrideCanvasScale(scaler) ?? UiScaling.GameCanvasScale(scaler, new Vector2(Screen.width, Screen.height));
        var canvas = c.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.rootCanvas != null && canvas.rootCanvas.scaleFactor > 0f) return canvas.rootCanvas.scaleFactor;
        return 1f;
    }

    private static string ScreenName(Transform t)
    {
        var p = t.parent;
        return p != null ? p.name : t.name;
    }

    /// <summary>Re-apply every live constraint.</summary>
    public static void ReapplyAllConstraints()
    {
        UIAspectConstraint.SetGlobalMode(UIAspectConstraint.s_globalMode);
    }
}
