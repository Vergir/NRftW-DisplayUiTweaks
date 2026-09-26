using Il2CppMoon.Forsaken;
using UnityEngine;

namespace MoreAspectRatios;

/// <summary>
/// The UI box: UIAspectConstraint fits the UI root into a box of a given aspect. Game modes: Native = stretch to the
/// screen, 16:9 / 21:9 / 32:9 = fixed boxes, Custom = per-instance aspect the game never exposes in the UI.
/// We give "Custom" a meaning (the Custom UI Aspect Ratio slider) and stretch constraints whose parent is not
/// screen-shaped (fixed-size roots) instead of boxing them.
///
/// C# transcription of the original ApplyConstraint (build 29466, RVA 0x8C22FC0):
///   parent = GetParentSize(); if (parent.x <= 0 || parent.y <= 0) return;
///   mode = (m_useGlobalMode && Application.isPlaying) ? s_globalMode : m_mode;
///   if (mode == Native) { ApplyStretchToParent(rt); w = parent.x; h = parent.y; }
///   else { target = aspect(mode);
///          if (parent.x/parent.y > target) { w = parent.y*target; h = parent.y; } else { w = parent.x; h = parent.x/target; }
///          rt.anchorMin = rt.anchorMax = rt.pivot = (0.5,0.5); rt.anchoredPosition = 0; rt.sizeDelta = (w,h); }
///   ApplySafeZone(w, h);
/// </summary>
internal static class UiBox
{
    private const float Aspect16x9 = 1.7777778f;
    private const float Aspect21x9 = 2.3333333f;
    private const float Aspect32x9 = 3.5555556f;

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

        // A constraint whose parent does not have the screen's aspect sits under a fixed-size root (1920x1080 in this game:
        // scribe table, inspect player). Boxing those crops their content, so stretch to the root instead.
        if (Prefs.StretchMismatchedRoots.Value && mode != UIAspectMode.Native)
        {
            float screenAspect = (float)Screen.width / Screen.height;
            float parentAspect = parent.x / parent.y;
            if (Mathf.Abs(parentAspect - screenAspect) > 0.02f) mode = UIAspectMode.Native;
        }

        RectTransform rt = c.m_rectTransform;
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

    /// <summary>Force every live constraint to re-apply (SetGlobalMode calls ApplyIfNeeded(force) on all instances).</summary>
    public static void ReapplyAllConstraints()
    {
        UIAspectConstraint.SetGlobalMode(UIAspectConstraint.s_globalMode);
    }
}
