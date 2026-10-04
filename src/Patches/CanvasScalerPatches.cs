using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DisplayUiTweaks.Patches;

/// <summary>Applies our canvas scale, or lets the game's code run when there is no override.</summary>
[HarmonyPatch(typeof(CanvasScaler), "HandleScaleWithScreenSize")]
internal static class HandleScaleWithScreenSizePatch
{
    // Last scale factor seen per scaler (by pointer), and its canvas.
    private static readonly Dictionary<IntPtr, (Canvas canvas, float scale)> _last = new();

    static bool Prefix(CanvasScaler __instance)
    {
        float? scale = UiScaling.OverrideCanvasScale(__instance);
        if (!scale.HasValue) return true;
        __instance.SetScaleFactor(scale.Value);
        __instance.SetReferencePixelsPerUnit(__instance.referencePixelsPerUnit);
        return false;
    }

    /// <summary>
    /// Nested canvases (the chat window's, the item pickups') keep drawing with their root's previous scale after the root
    /// canvas's scale factor changes at run time (HUD &amp; Dialogue UI Size): the chat then draws bigger and lower than
    /// its own rect. Switching each nested canvas off and on makes it pick up the new scale.
    /// </summary>
    static void Postfix(CanvasScaler __instance)
    {
        var key = __instance.Pointer;
        if (!_last.TryGetValue(key, out var last) || last.canvas == null)
        {
            var c = __instance.GetComponent<Canvas>();
            if (c == null) return;
            _last[key] = (c, c.scaleFactor);
            return;
        }
        float now = last.canvas.scaleFactor;
        if (Mathf.Abs(now - last.scale) < 0.0001f) return;
        _last[key] = (last.canvas, now);
        foreach (var nested in last.canvas.GetComponentsInChildren<Canvas>(true))
        {
            if (nested == null || nested.Pointer == last.canvas.Pointer || !nested.enabled) continue;
            nested.enabled = false;
            nested.enabled = true;
        }
    }
}
