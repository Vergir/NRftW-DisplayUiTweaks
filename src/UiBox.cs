using Il2CppMoon.Forsaken;
using UnityEngine;

namespace MoreAspectRatios;

/// <summary>
/// The HUD box: UIAspectConstraint fits the HUD root into a box of a given aspect (game modes: Native = stretch,
/// 16:9 / 21:9 / 32:9 / Custom). We replace ApplyConstraint so the '16:9' mode uses the user's box aspect instead
/// of 1.7778, and so screens with a fixed-size root are stretched rather than boxed.
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
    private const float Aspect21x9 = 2.3333333f;
    private const float Aspect32x9 = 3.5555556f;

    /// <summary>Aspect of the box for a mode, or 0 for Native (no box).</summary>
    public static float TargetAspect(UIAspectMode mode, Vector2Int custom)
    {
        switch (mode)
        {
            case UIAspectMode.Native:     return 0f;
            case UIAspectMode.Aspect16x9: return Prefs.BoxAspect;   // game: 1.7777778
            case UIAspectMode.Aspect21x9: return Aspect21x9;
            case UIAspectMode.Aspect32x9: return Aspect32x9;
            case UIAspectMode.Custom:     return custom.y != 0 ? (float)custom.x / custom.y : Prefs.BoxAspect;
            default:                      return Prefs.BoxAspect;
        }
    }

    /// <summary>Aspect of the global HUD box (0 = Native / whole screen).</summary>
    public static float GlobalBoxAspect() => TargetAspect(UIAspectConstraint.s_globalMode, Vector2Int.zero);

    /// <summary>Width in screen pixels of the global HUD box (whole screen when Native or when the box is wider than the screen).</summary>
    public static float GlobalBoxWidthPx()
    {
        float aspect = GlobalBoxAspect();
        if (aspect <= 0f) return Screen.width;
        return Mathf.Min(Screen.width, Screen.height * aspect);
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
        float w, h;
        float target = TargetAspect(mode, c.m_customAspect);
        if (target <= 0f)
        {
            if (rt != null) UIAspectConstraint.ApplyStretchToParent(rt);
            w = parent.x; h = parent.y;
        }
        else
        {
            if (parent.x / parent.y > target) { w = parent.y * target; h = parent.y; }
            else { w = parent.x; h = parent.x / target; }
            if (rt != null)
            {
                var half = new Vector2(0.5f, 0.5f);
                rt.anchorMin = half; rt.anchorMax = half; rt.pivot = half;
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(w, h);
            }
        }
        c.ApplySafeZone(w, h);
    }

    /// <summary>Force every live constraint to re-apply (SetGlobalMode calls ApplyIfNeeded(force) on all instances).</summary>
    public static void ReapplyAllConstraints()
    {
        UIAspectConstraint.SetGlobalMode(UIAspectConstraint.s_globalMode);
    }
}
