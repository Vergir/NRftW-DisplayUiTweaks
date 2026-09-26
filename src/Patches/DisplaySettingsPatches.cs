using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppMoon.Forsaken;
using UnityEngine;

namespace MoreAspectRatios.Patches;

/// <summary>
/// The game only lists resolutions with an aspect between 16:10 and 32:9 (the check is inlined, so it cannot be hooked
/// on its own). Worse, when the saved resolution is not in that list the original applies AND saves a fallback from it
/// through SetResolutionFromCurrentSettings, which is how a 9:8 player ends up at 1680x1050 after every launch.
/// So: while the original runs, SetResolutionFromCurrentSettings is suppressed; afterwards the list is rebuilt from
/// every mode the display reports, the index is recomputed, and the (now harmless) apply is done with the full list.
/// </summary>
[HarmonyPatch(typeof(DisplaySettingsTab), nameof(DisplaySettingsTab.InitializeAvailableResolutions))]
internal static class InitializeAvailableResolutionsPatch
{
    internal static bool SuppressApply;

    static void Prefix()
    {
        SuppressApply = Prefs.Enabled.Value && Prefs.UnlockResolutions.Value;
    }

    static void Postfix(DisplaySettingsTab __instance)
    {
        SuppressApply = false;
        if (!Prefs.Enabled.Value || !Prefs.UnlockResolutions.Value) return;
        try
        {
            var all = Screen.resolutions;
            if (all == null || all.Length == 0) return;

            // Distinct width x height, in the order the display reports them (ascending).
            var seen = new HashSet<long>();
            var list = new List<Resolution>();
            foreach (var r in all)
            {
                if (r.width <= 0 || r.height <= 0) continue;
                long key = ((long)r.width << 32) | (uint)r.height;
                if (seen.Add(key)) list.Add(r);
            }

            // Name format: reuse the game's own naming for an entry it accepted, e.g. "1920x1080 (16:9)".
            string template = "{0}x{1} ({2})";
            var oldRes = __instance.m_allowedResolutions;
            var oldNames = __instance.m_resolutionNames;
            if (oldRes != null && oldNames != null && oldRes.Count > 0 && oldNames.Length >= oldRes.Count)
            {
                var r0 = oldRes[0];
                string n0 = oldNames[0];
                string ratio0 = DisplaySettingsTab.GetAspectRatio(new Vector2(r0.width, r0.height), 0.015f);
                if (!string.IsNullOrEmpty(n0) && n0.Contains(r0.width.ToString()) && n0.Contains(r0.height.ToString()))
                {
                    template = n0.Replace(r0.width.ToString(), "{0}").Replace(r0.height.ToString(), "{1}");
                    if (!string.IsNullOrEmpty(ratio0) && template.Contains(ratio0)) template = template.Replace(ratio0, "{2}");
                }
            }

            int before = oldRes != null ? oldRes.Count : 0;
            var names = new Il2CppStringArray(list.Count);
            __instance.m_allowedResolutions.Clear();
            int newIndex = -1;
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                __instance.m_allowedResolutions.Add(r);
                string ratio = DisplaySettingsTab.GetAspectRatio(new Vector2(r.width, r.height), 0.015f);
                names[i] = string.Format(template, r.width, r.height, ratio);
                if (r.width == Screen.width && r.height == Screen.height) newIndex = i;
            }
            __instance.m_resolutionNames = names;
            if (newIndex < 0) newIndex = list.Count - 1;
            __instance.m_currentResolutionIndex = newIndex;

            // Same two calls the original ends with, now against the full list (the selected entry is the running one).
            __instance.SetResolutionFromCurrentSettings(true);
            __instance.UpdateResolutionDropdownOptions();
            MoreAspectRatiosMod.Log.Msg("Resolutions: " + before + " listed by the game -> " + list.Count + " available, current " + names[newIndex]);
        }
        catch (System.Exception e)
        {
            MoreAspectRatiosMod.Log.Error("Resolution unlock failed: " + e);
        }
    }
}

/// <summary>Skips the apply+save while the original InitializeAvailableResolutions runs with its narrow list.</summary>
[HarmonyPatch(typeof(DisplaySettingsTab), nameof(DisplaySettingsTab.SetResolutionFromCurrentSettings))]
internal static class SetResolutionFromCurrentSettingsPatch
{
    static bool Prefix()
    {
        if (!InitializeAvailableResolutionsPatch.SuppressApply) return true;
        MoreAspectRatiosMod.Log.Msg("Suppressed the game's resolution fallback (saved resolution not in its list)");
        return false;
    }
}

/// <summary>
/// Adds the game's own (otherwise unreachable) "Custom" UI aspect mode to the dropdown, labelled for the mod. Our
/// ApplyConstraint replacement maps that mode to the UI aspect ratio slider. Runs before Initialize builds the dropdown
/// from m_allowedUIAspectModes / m_uiAspectModeNames.
/// The original resets the saved mode to Native when it is not in its list (Custom never is), so the saved value is
/// captured in a prefix and restored afterwards.
/// </summary>
[HarmonyPatch(typeof(DisplaySettingsTab), nameof(DisplaySettingsTab.InitializeUIAspectModes))]
internal static class InitializeUIAspectModesPatch
{
    public const string CustomModeLabel = "Custom (More Aspect Ratios)";
    private static UIAspectMode _savedMode = UIAspectMode.Native;

    static void Prefix()
    {
        _savedMode = UIAspectMode.Native;
        if (!Prefs.Enabled.Value || !Prefs.UnlockUiAspectModes.Value) return;
        try
        {
            var device = Core.SettingsData?.Device;
            if (device != null) _savedMode = device.UIAspectMode;
        }
        catch (System.Exception e) { MoreAspectRatiosMod.Log.Warning("Could not read the saved UI aspect mode: " + e.Message); }
    }

    static void Postfix(DisplaySettingsTab __instance)
    {
        if (!Prefs.Enabled.Value || !Prefs.UnlockUiAspectModes.Value) return;
        try
        {
            var modes = __instance.m_allowedUIAspectModes;
            var names = __instance.m_uiAspectModeNames;
            if (modes == null || names == null) return;

            if (!modes.Contains(UIAspectMode.Custom))
            {
                modes.Add(UIAspectMode.Custom);
                var newNames = new Il2CppStringArray(names.Length + 1);
                for (int i = 0; i < names.Length; i++) newNames[i] = names[i];
                newNames[names.Length] = CustomModeLabel;
                __instance.m_uiAspectModeNames = newNames;
            }
            else
            {
                int idx = modes.IndexOf(UIAspectMode.Custom);
                if (idx >= 0 && idx < names.Length) names[idx] = CustomModeLabel;
            }

            if (_savedMode == UIAspectMode.Custom)
            {
                var device = Core.SettingsData?.Device;
                if (device != null && device.UIAspectMode != UIAspectMode.Custom)
                {
                    device.UIAspectMode = UIAspectMode.Custom;
                    MoreAspectRatiosMod.Log.Msg("Restored the saved UI aspect mode (Custom) that the game reset to Native");
                }
                __instance.m_currentUIAspectModeIndex = modes.IndexOf(UIAspectMode.Custom);
                if (UIAspectConstraint.s_globalMode != UIAspectMode.Custom) UIAspectConstraint.SetGlobalMode(UIAspectMode.Custom);
            }

            MoreAspectRatiosMod.Log.Msg("UI aspect modes: " + modes.Count + " (current index " + __instance.m_currentUIAspectModeIndex + ")");
        }
        catch (System.Exception e)
        {
            MoreAspectRatiosMod.Log.Error("UI aspect mode unlock failed: " + e);
        }
    }
}

/// <summary>Adds our sliders to the Display tab once the game has built it.</summary>
[HarmonyPatch(typeof(DisplaySettingsTab), nameof(DisplaySettingsTab.Initialize))]
internal static class DisplaySettingsTabInitializePatch
{
    static void Postfix(DisplaySettingsTab __instance)
    {
        MoreAspectRatiosMod.Log.Msg("Display settings tab initialized by the game");
        if (!Prefs.Enabled.Value || !Prefs.AddSettingsRows.Value) return;
        try { SettingsRows.AddTo(__instance); }
        catch (System.Exception e) { MoreAspectRatiosMod.Log.Error("Adding settings rows failed: " + e); }
    }
}
