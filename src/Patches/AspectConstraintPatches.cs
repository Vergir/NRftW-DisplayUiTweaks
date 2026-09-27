using HarmonyLib;
using Il2CppMoon.Forsaken;

namespace MoreAspectRatios.Patches;

[HarmonyPatch(typeof(UIAspectConstraint), nameof(UIAspectConstraint.ApplyConstraint))]
internal static class ApplyConstraintPatch
{
    static bool Prefix(UIAspectConstraint __instance)
    {
        if (!Prefs.Enabled.Value) return true;
        UiBox.ApplyConstraint(__instance);
        return false;
    }
}

/// <summary>Refits UI Toolkit panels and boxes when the UI aspect mode changes.</summary>
[HarmonyPatch(typeof(UIAspectConstraint), nameof(UIAspectConstraint.SetGlobalMode))]
internal static class SetGlobalModePatch
{
    static void Postfix()
    {
        if (!Prefs.Enabled.Value) return;
        try
        {
            UiScaling.ApplyPanels();
            UiToolkitBoxing.ApplyAll();
        }
        catch (System.Exception e) { MoreAspectRatiosMod.Log.Warning("SetGlobalMode postfix: " + e.Message); }
    }
}
