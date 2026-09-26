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

/// <summary>The UI Aspect dropdown (and our own re-apply) go through SetGlobalMode. The uGUI boxes follow by
/// themselves; UI Toolkit panels and boxes are ours to update.</summary>
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
