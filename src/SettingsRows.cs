using System;
using System.Collections.Generic;
using Il2CppMoon.Forsaken;
using NrftwShared;
using UnityEngine;

namespace DisplayUiTweaks;

/// <summary>Our rows at the end of Options > Display under a "Display &amp; UI Tweaks" heading (Mod Settings Tab moves them
/// to its Mods tab); see docs/internal.md, "Settings rows". Built with the shared kit (src/Shared/SettingsRowsKit.cs),
/// which also removes them. Order: UI Area, HUD size, menu size, Edit HUD Layout, Reset HUD Layout, Hide HUD Outside Combat.</summary>
internal static class SettingsRows
{
    public const string Prefix = "DUT_";
    public const string UiAreaRowId = "DUT_UiArea";
    private const PlayerSettingCategory Category = PlayerSettingCategory.Display;
    private const string SpacerId = "DUT_Spacer", HeadingId = "DUT_Heading", AreaId = "DUT_UiArea", HudId = "DUT_HudScale", MenuId = "DUT_MenuScale",
        EditId = "DUT_EditHud", ResetId = "DUT_ResetHud", HideId = "DUT_HideHud";
    private static readonly string[] HideNames = { "Off", "On", "On, but show health while hurt" };

    private const string AreaDescription =
        "Shape of the area the whole UI is kept in (HUD, menus and dialogue): its width divided by its height. " +
        "Replaces the game's own UI Aspect Mode setting.\n\n" +
        "Examples\n" +
        "1.00 = 1:1 (square)\n" +
        "1.33 = 4:3\n" +
        "1.60 = 16:10\n" +
        "1.78 = 16:9\n" +
        "2.33 = 21:9\n" +
        "3.00 = 27:9\n" +
        "3.56 = 32:9";

    private const float HoldRepeatSeconds = 0.3f;

    // 1.0.0 rows (Custom UI Aspect Ratio, Bounty Board & Map Fix): only freed from the registry.
    private static readonly SettingsRowsKit Kit = new SettingsRowsKit(Prefix,
        new[] { SpacerId, HeadingId, AreaId, HudId, MenuId, EditId, ResetId, HideId, "DUT_BoxAspect", "DUT_BoxUiToolkit" },
        () => DisplayUiTweaksMod.Log, SettingsRowsKit.DisplayTabControls) { Verbose = true };

    private static readonly Dictionary<string, Func<float>> _sliderValues = new();
    private static readonly Dictionary<string, Func<int>> _dropdownValues = new();

    /// <summary>The UI Area and HUD size rows of every settings screen (the settings preview watches their highlight).</summary>
    public static readonly List<SettingsItemGUIBase> PreviewRows = new();

    /// <summary>
    /// The main menu and the game each have their own settings screen with their own copy of our rows; a row shows the
    /// value it was built with. Called when a settings screen opens: every row of ours shows the current value again
    /// (without calling back).
    /// </summary>
    public static void RefreshValues(SettingsScreen screen)
    {
        foreach (var slider in screen.GetComponentsInChildren<SliderSettingsItemGUI>(true))
            if (slider != null && _sliderValues.TryGetValue(slider.gameObject.name, out var get)) slider.SetValue(get(), false);
        foreach (var dd in screen.GetComponentsInChildren<ActualDropDownSettingsItemGUI>(true))
            if (dd != null && _dropdownValues.TryGetValue(dd.gameObject.name, out var get)) dd.SetIndex(get(), false);
    }

    /// <summary>Add the rows to every settings screen that already exists (after a hot reload).</summary>
    public static void AddToLiveScreens()
    {
        int screens = 0;
        foreach (var s in Resources.FindObjectsOfTypeAll<SettingsScreen>())
        {
            if (s == null || s.m_displayTab == null) continue;
            screens++;
            AddTo(s.m_displayTab);
        }
        if (screens > 0) DisplayUiTweaksMod.Log.Msg("Checked " + screens + " live settings screen(s) for our rows");
    }

    public static void AddTo(DisplaySettingsTab tab)
    {
        var rows = Kit.Begin(tab.m_controls, Category, HudId);
        if (rows == null) return;

        rows.Spacer(SpacerId);
        // A heading row as the game uses between its own groups (Controls tab), so the rows read as this mod's.
        rows.Heading(HeadingId, "Display & UI Tweaks");

        AddSlider(rows, AreaId, "UI Area", AreaDescription,
            Prefs.UiAreaMin, Prefs.UiAreaMax, Prefs.UiAreaStep, () => Prefs.UiAreaValue,
            v => v.ToString("0.00"),
            v => { Prefs.UiArea.Value = Mathf.Round(v * 100f) / 100f; DisplayUiTweaksMod.OnLayoutPrefChanged(); Hud.SettingsPreview.Poke(); });

        AddSlider(rows, HudId, "HUD & Dialogue UI Size", "Scale of the in-game HUD, overlays and dialogue.",
            Prefs.HudScaleMin, Prefs.HudScaleMax, Prefs.HudScaleStep, () => Prefs.HudScalePercent.Value,
            v => Mathf.RoundToInt(v) + "%",
            v => { Prefs.HudScalePercent.Value = Mathf.Round(v); DisplayUiTweaksMod.OnLayoutPrefChanged(); Hud.SettingsPreview.Poke(); });

        AddSlider(rows, MenuId, "Menu UI Size", "Scale of menus (inventory, stats, map, settings).",
            Prefs.HudScaleMin, Prefs.HudScaleMax, Prefs.HudScaleStep, () => Prefs.MenuScalePercent.Value,
            v => Mathf.RoundToInt(v) + "%",
            v => { Prefs.MenuScalePercent.Value = Mathf.Round(v); DisplayUiTweaksMod.OnLayoutPrefChanged(); });

        rows.Button(EditId, "Edit HUD Layout",
            "Move and resize HUD elements with the mouse, on top of the game, with sample content in empty elements.",
            () => Hud.HudEditor.Enter());

        rows.Button(ResetId, "Reset HUD Layout",
            "Put every HUD element back where the game has it, at its normal size.",
            () => Hud.HudLayout.ResetAll());

        Func<int> hideCurrent = () => Mathf.Clamp(Prefs.HideHudOutsideCombat.Value, 0, HideNames.Length - 1);
        rows.Dropdown(HideId, "Hide HUD Outside Combat",
            "Fades out health, equipment, money, durability, clock and location a few seconds after combat ends; " +
            "they come back when combat starts. The last option keeps the health bar while you are not at full health. " +
            "Item pickups, hints and chat stay.",
            HideNames, hideCurrent(),
            i => { Prefs.HideHudOutsideCombat.Value = i; Prefs.Save(); });
        _dropdownValues[HideId] = hideCurrent;

        DisplayUiTweaksMod.Log.Msg("Added Display & UI Tweaks rows to Options > Display");
    }

    /// <summary>Hot reload / unload: destroy our rows on every live settings screen and free their registry keys.</summary>
    public static void RemoveAll() => Kit.RemoveAll();

    /// <summary>A slider row (no snapping: the change handlers round), remembered for RefreshValues; the UI Area and HUD
    /// size rows also go to PreviewRows.</summary>
    private static void AddSlider(SettingsRowsKit.Builder rows, string id, string name, string desc,
        float min, float max, float step, Func<float> current, Func<float, string> display, Action<float> onChanged)
    {
        var row = rows.Slider(id, name, desc, min, max, step, current(), display, onChanged, HoldRepeatSeconds, snapToStep: false);
        _sliderValues[id] = () => SettingsRowsKit.Normalize(current(), min, max);
        if ((id == AreaId || id == HudId) && row != null)
        {
            var item = row.GetComponent<SettingsItemGUIBase>();
            if (item != null) { PreviewRows.RemoveAll(r => r == null); PreviewRows.Add(item); }
        }
    }
}
