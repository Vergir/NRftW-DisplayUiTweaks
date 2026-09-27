using HarmonyLib;
using UnityEngine.UI;

namespace MoreAspectRatios.Patches;

/// <summary>Applies our canvas scale, or lets the game's code run when there is no override.</summary>
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
