using HarmonyLib;
using UnityEngine.UI;

namespace MoreAspectRatios.Patches;

/// <summary>CanvasScaler.Handle() calls this every frame for ScaleWithScreenSize canvases. When we have an override we
/// do what the original does (SetScaleFactor + SetReferencePixelsPerUnit) with our value and skip it; otherwise the
/// game's code runs untouched.</summary>
[HarmonyPatch(typeof(CanvasScaler), "HandleScaleWithScreenSize")]
internal static class HandleScaleWithScreenSizePatch
{
    static bool Prefix(CanvasScaler __instance)
    {
        float? scale = UiScaling.OverrideCanvasScale(__instance);
        if (!scale.HasValue) return true;
        __instance.SetScaleFactor(scale.Value);
        __instance.SetReferencePixelsPerUnit(__instance.referencePixelsPerUnit);
        return false;
    }
}
