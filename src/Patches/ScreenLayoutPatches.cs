using HarmonyLib;
using Il2Cpp;
using Il2CppMoon.Forsaken;

namespace MoreAspectRatios.Patches;

/// <summary>
/// Three screens choose their layout (mainly where the item-comparison panel goes) with an IsResolutionSupported check:
///   if UI Aspect setting == 16:9 -> supported;
///   else supported only if the monitor is 16:9 or 16:10 (+-0.02) or a few exact sizes.
/// So the game already ties these layouts to its own 16:9 UI box. With the option on, our Custom box counts too: menus
/// are scaled so their 1920x1080 reference fits the box, so the 16:9 layout has the room it was designed for.
/// </summary>
internal static class CustomModeLayout
{
    public static bool ShouldForceSupported =>
        Prefs.Enabled.Value && Prefs.CustomModeUses16x9Layouts.Value && UIAspectConstraint.s_globalMode == UIAspectMode.Custom;
}

[HarmonyPatch(typeof(InventoryScreenMain), "IsResolutionSupported")]
internal static class InventoryScreenMainLayoutPatch
{
    static void Postfix(ref bool __result) { if (CustomModeLayout.ShouldForceSupported) __result = true; }
}

[HarmonyPatch(typeof(InventoryItemsElement), "IsResolutionSupported")]
internal static class InventoryItemsElementLayoutPatch
{
    static void Postfix(ref bool __result) { if (CustomModeLayout.ShouldForceSupported) __result = true; }
}

[HarmonyPatch(typeof(CommunityChestScreenV2), "IsResolutionSupported")]
internal static class CommunityChestLayoutPatch
{
    static void Postfix(ref bool __result) { if (CustomModeLayout.ShouldForceSupported) __result = true; }
}
