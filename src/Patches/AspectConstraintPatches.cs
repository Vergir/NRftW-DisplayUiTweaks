using HarmonyLib;
using Il2CppMoon.Forsaken;

namespace MoreAspectRatios.Patches;

[HarmonyPatch(typeof(UIAspectConstraint), nameof(UIAspectConstraint.ApplyConstraint))]
internal static class ApplyConstraintPatch
{
    // GetTargetAspect is inlined into ApplyConstraint in the shipped build, so the whole method is replaced.
    static bool Prefix(UIAspectConstraint __instance)
    {
        if (!Prefs.Enabled.Value) return true;
        UiBox.ApplyConstraint(__instance);
        return false;
    }
}
