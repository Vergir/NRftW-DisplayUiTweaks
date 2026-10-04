using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppMoon.Forsaken;
using UnityEngine;

namespace DisplayUiTweaks.Patches;

/// <summary>Rebuilds the resolution list from every display mode and selects the saved resolution
/// (see docs/internal.md, "Resolution list").</summary>
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
            int savedW = 0, savedH = 0;
            var device = Core.SettingsData?.Device;
            if (device != null) { savedW = device.Resolution.Width; savedH = device.Resolution.Height; }

            var names = new Il2CppStringArray(list.Count);
            __instance.m_allowedResolutions.Clear();
            int savedIndex = -1, screenIndex = -1;
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                __instance.m_allowedResolutions.Add(r);
                string ratio = DisplaySettingsTab.GetAspectRatio(new Vector2(r.width, r.height), 0.015f);
                names[i] = string.Format(template, r.width, r.height, ratio);
                if (r.width == savedW && r.height == savedH) savedIndex = i;
                if (r.width == Screen.width && r.height == Screen.height) screenIndex = i;
            }
            __instance.m_resolutionNames = names;
            int newIndex = savedIndex >= 0 ? savedIndex : screenIndex >= 0 ? screenIndex : list.Count - 1;
            __instance.m_currentResolutionIndex = newIndex;

            bool windowDiffers = savedIndex >= 0 && (Screen.width != savedW || Screen.height != savedH);
            if (windowDiffers)
            {
                __instance.SetResolutionFromCurrentSettings(false);
                DisplayUiTweaksMod.Log.Msg("Window was " + Screen.width + "x" + Screen.height + ", applied the saved resolution " + savedW + "x" + savedH);
            }
            __instance.UpdateResolutionDropdownOptions();
            DisplayUiTweaksMod.Log.Msg("Resolutions: " + before + " listed by the game -> " + list.Count + " available, selected " + names[newIndex]
                + " (saved " + savedW + "x" + savedH + ", window " + Screen.width + "x" + Screen.height + ")");
        }
        catch (System.Exception e)
        {
            DisplayUiTweaksMod.Log.Error("Resolution unlock failed: " + e);
        }
    }
}

/// <summary>Defers the game's resolution apply while InitializeAvailableResolutions runs.</summary>
[HarmonyPatch(typeof(DisplaySettingsTab), nameof(DisplaySettingsTab.SetResolutionFromCurrentSettings))]
internal static class SetResolutionFromCurrentSettingsPatch
{
    static bool Prefix()
    {
        if (!InitializeAvailableResolutionsPatch.SuppressApply) return true;
        DisplayUiTweaksMod.Log.Msg("Deferred the game's resolution apply until the full resolution list is built");
        return false;
    }
}

/// <summary>
/// The game's UI Aspect Mode row: carries a 1.0.0 "Custom (Display &amp; UI Tweaks)" setup over to UI Area once, and notes
/// in the row's description that UI Area overrides it (see docs/internal.md, "UI Area").
/// </summary>
[HarmonyPatch(typeof(DisplaySettingsTab), nameof(DisplaySettingsTab.InitializeUIAspectModes))]
internal static class InitializeUIAspectModesPatch
{
    private static UIAspectMode _savedMode = UIAspectMode.Native;

    static void Prefix()
    {
        _savedMode = UIAspectMode.Native;
        try
        {
            var device = Core.SettingsData?.Device;
            if (device != null) _savedMode = device.UIAspectMode;
        }
        catch (System.Exception e) { DisplayUiTweaksMod.Log.Warning("Could not read the saved UI aspect mode: " + e.Message); }
    }

    static void Postfix()
    {
        if (!Prefs.Enabled.Value) return;
        try
        {
            // The game resets a saved Custom mode to Native here (it never offers Custom): keep the user's box as UI Area.
            Migrate(_savedMode);
            UiAspectNote.Apply();
        }
        catch (System.Exception e) { DisplayUiTweaksMod.Log.Error("UI aspect mode setup failed: " + e); }
    }

    /// <summary>Once: a 1.0.0 "Custom (Display &amp; UI Tweaks)" UI aspect becomes the same UI Area. Also called at start-up
    /// (hot reload / settings already loaded) with the live mode.</summary>
    public static void Migrate(UIAspectMode mode)
    {
        if (Prefs.UiAreaMigrated.Value) return;
        Prefs.UiAreaMigrated.Value = true;
        if (mode == UIAspectMode.Custom)
        {
            Prefs.UiArea.Value = Mathf.Round(Mathf.Clamp(Prefs.UiBoxAspect.Value, Prefs.UiAreaMin, Prefs.UiAreaMax) * 100f) / 100f;
            Prefs.CommitLayoutValues();
            UiBox.ReapplyAllConstraints();
            DisplayUiTweaksMod.Log.Msg("Carried the Custom UI aspect " + Prefs.UiArea.Value.ToString("0.00") + " over to UI Area");
        }
        Prefs.Save();
    }
}

/// <summary>Appends "overridden by UI Area" to the game's UI Aspect Mode description (one static LocalizedMessage).</summary>
internal static class UiAspectNote
{
    public const string Note = "\n\nDisplay & UI Tweaks replaces this setting with its UI Area setting.";
    private static LocalizedMessage? _message;
    private static string?[]? _original;

    public static void Apply()
    {
        var m = DisplaySettingsTab.s_uiAspectModeDescription;
        if (m == null || (m.English != null && m.English.EndsWith(Note))) return;
        _message = m;
        _original = new string[] { m.English, m.French, m.Italian, m.German, m.Spanish, m.BrazilianPortuguese, m.TraditionalChinese,
                            m.SimplifiedChinese, m.Korean, m.Russian, m.Japanese, m.Polish };
        m.English += Note; m.French += Note; m.Italian += Note; m.German += Note; m.Spanish += Note; m.BrazilianPortuguese += Note;
        m.TraditionalChinese += Note; m.SimplifiedChinese += Note; m.Korean += Note; m.Russian += Note; m.Japanese += Note; m.Polish += Note;
    }

    /// <summary>Unload / hot reload.</summary>
    public static void Restore()
    {
        var m = _message;
        var o = _original;
        if (m == null || o == null) return;
        m.English = o[0]; m.French = o[1]; m.Italian = o[2]; m.German = o[3]; m.Spanish = o[4]; m.BrazilianPortuguese = o[5];
        m.TraditionalChinese = o[6]; m.SimplifiedChinese = o[7]; m.Korean = o[8]; m.Russian = o[9]; m.Japanese = o[10]; m.Polish = o[11];
        _message = null;
        _original = null;
    }
}

/// <summary>Adds our rows to the Display tab.</summary>
[HarmonyPatch(typeof(DisplaySettingsTab), nameof(DisplaySettingsTab.Initialize))]
internal static class DisplaySettingsTabInitializePatch
{
    static void Postfix(DisplaySettingsTab __instance)
    {
        DisplayUiTweaksMod.Log.Msg("Display settings tab initialized by the game");
        if (!Prefs.Enabled.Value) return;
        try { UiAspectNote.Apply(); }
        catch (System.Exception e) { DisplayUiTweaksMod.Log.Warning("UI aspect note: " + e.Message); }
        if (!Prefs.AddSettingsRows.Value) return;
        try { SettingsRows.AddTo(__instance); }
        catch (System.Exception e) { DisplayUiTweaksMod.Log.Error("Adding settings rows failed: " + e); }
    }
}

/// <summary>Our rows show the current values whenever a settings screen opens (main menu and game have separate screens).</summary>
[HarmonyPatch(typeof(SettingsScreen), nameof(SettingsScreen.Show))]
internal static class SettingsScreenShowPatch
{
    static void Postfix(SettingsScreen __instance)
    {
        if (!Prefs.Enabled.Value) return;
        try { SettingsRows.RefreshValues(__instance); }
        catch (System.Exception e) { DisplayUiTweaksMod.Log.Warning("Refreshing settings rows: " + e.Message); }
    }
}

/// <summary>Esc while editing the HUD layout belongs to the editor: the settings screen it was opened from stays open.</summary>
[HarmonyPatch(typeof(MainMenuSettingsScreen), "get_IgnoreBack")]
internal static class MainMenuSettingsIgnoreBackPatch
{
    static void Postfix(ref bool __result) { if (Hud.HudEditor.SuppressBack) __result = true; }
}

[HarmonyPatch(typeof(SettingsScreenPlayerMenu), "get_IgnoreBack")]
internal static class PlayerMenuSettingsIgnoreBackPatch
{
    static void Postfix(ref bool __result) { if (Hud.HudEditor.SuppressBack) __result = true; }
}
