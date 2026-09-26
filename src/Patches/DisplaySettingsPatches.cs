using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppMoon.Forsaken;
using UnityEngine;

namespace MoreAspectRatios.Patches;

/// <summary>
/// The game only lists resolutions with an aspect between 16:10 and 32:9 (the check is inlined, so it cannot be hooked
/// on its own). After the original has built its list we rebuild it from every mode the display reports.
/// </summary>
[HarmonyPatch(typeof(DisplaySettingsTab), nameof(DisplaySettingsTab.InitializeAvailableResolutions))]
internal static class InitializeAvailableResolutionsPatch
{
    static void Postfix(DisplaySettingsTab __instance)
    {
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
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                __instance.m_allowedResolutions.Add(r);
                string ratio = DisplaySettingsTab.GetAspectRatio(new Vector2(r.width, r.height), 0.015f);
                names[i] = string.Format(template, r.width, r.height, ratio);
            }
            __instance.m_resolutionNames = names;

            // Same two calls the original ends with: pick the current resolution's index and refresh the dropdown.
            __instance.SetResolutionFromCurrentSettings(true);
            __instance.UpdateResolutionDropdownOptions();
            MoreAspectRatiosMod.Log.Msg("Resolutions: " + before + " listed by the game -> " + list.Count + " available");
        }
        catch (System.Exception e)
        {
            MoreAspectRatiosMod.Log.Error("Resolution unlock failed: " + e);
        }
    }
}

/// <summary>Adds our sliders to the Display tab once the game has built it.</summary>
[HarmonyPatch(typeof(DisplaySettingsTab), nameof(DisplaySettingsTab.Initialize))]
internal static class DisplaySettingsTabInitializePatch
{
    static void Postfix(DisplaySettingsTab __instance)
    {
        if (!Prefs.Enabled.Value || !Prefs.AddSettingsRows.Value) return;
        try { SettingsRows.AddTo(__instance); }
        catch (System.Exception e) { MoreAspectRatiosMod.Log.Error("Adding settings rows failed: " + e); }
    }
}
