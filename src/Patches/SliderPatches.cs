using HarmonyLib;
using Il2CppMoon.Forsaken;

namespace DisplayUiTweaks.Patches;

/// <summary>The game's slider keeps its held-key repeat timer across key presses (only the speed-up counter is reset
/// on release), so a few short taps add up to an extra repeat step. Our rows start each press with a fresh timer.</summary>
[HarmonyPatch(typeof(SliderSettingsItemGUI), nameof(SliderSettingsItemGUI.OnLeftButtonPressed))]
internal static class SliderLeftPressPatch
{
    static void Prefix(SliderSettingsItemGUI __instance) => SliderTap.Reset(__instance);
}

[HarmonyPatch(typeof(SliderSettingsItemGUI), nameof(SliderSettingsItemGUI.OnRightButtonPressed))]
internal static class SliderRightPressPatch
{
    static void Prefix(SliderSettingsItemGUI __instance) => SliderTap.Reset(__instance);
}

internal static class SliderTap
{
    public static void Reset(SliderSettingsItemGUI slider)
    {
        if (slider != null && slider.gameObject.name.StartsWith(SettingsRows.Prefix)) slider.m_scrollHoldTimer = 0f;
    }
}
