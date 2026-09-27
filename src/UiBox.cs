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
            case UIAspectMode.Custom:     return Prefs.BoxAspect;
            default:                      return Aspect16x9;
        }
    }

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
        return FitBox(screen, TargetAspect(UIAspectConstraint.s_globalMode));
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

        UIAspectMode mode = (c.m_useGlobalMode && Application.isPlaying) ? UIAspectConstraint.s_globalMode : c.m_mode;
        RectTransform rt = c.m_rectTransform;

        // Parent not screen-shaped = fixed-size root (recipe screens, inspect player): stretch to it instead of
        // boxing (boxing cuts its content off), and shrink it if it comes out larger than the box.
        bool fixedRoot = false;
        if (Prefs.StretchMismatchedRoots.Value)
        {
            float screenAspect = (float)Screen.width / Screen.height;
            float parentAspect = parent.x / parent.y;
            fixedRoot = Mathf.Abs(parentAspect - screenAspect) > 0.02f;
        }
        if (fixedRoot)
        {
            if (rt != null)
            {
                UIAspectConstraint.ApplyStretchToParent(rt);
                FitFixedRoot(c, rt, parent);
            }
            c.ApplySafeZone(parent.x, parent.y);
            return;
        }
        if (rt != null) Unfit(rt);

        float target = TargetAspect(mode);
        Vector2 box;
        if (target <= 0f)
        {
            if (rt != null) UIAspectConstraint.ApplyStretchToParent(rt);
            box = parent;
        }
        else
        {
            box = FitBox(parent, target);
            if (rt != null)
            {
                var half = new Vector2(0.5f, 0.5f);
                rt.anchorMin = half; rt.anchorMax = half; rt.pivot = half;
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = box;
            }
        }
        c.ApplySafeZone(box.x, box.y);
    }

    /// <summary>Scales a fixed-size screen down so it fits the UI box (never up).</summary>
    private static void FitFixedRoot(UIAspectConstraint c, RectTransform rt, Vector2 parent)
    {
        float canvasScale = ExpectedCanvasScale(c, rt);
        Vector2 box = GlobalBoxSizePx();
        float s = Mathf.Min(1f, box.x / (parent.x * canvasScale), box.y / (parent.y * canvasScale));
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

    /// <summary>The canvas scale this screen gets this frame (the scaler may not have applied a new value yet).</summary>
    private static float ExpectedCanvasScale(UIAspectConstraint c, RectTransform rt)
    {
        var scaler = c.GetComponentInParent<CanvasScaler>();
        if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            return UiScaling.OverrideCanvasScale(scaler) ?? UiScaling.GameCanvasScale(scaler, new Vector2(Screen.width, Screen.height));
        var p = rt.parent;
        return p != null && p.lossyScale.x > 0f ? p.lossyScale.x : 1f;
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
