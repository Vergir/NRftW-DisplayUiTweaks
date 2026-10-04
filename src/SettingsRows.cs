using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppMoon.Forsaken;
using UnityEngine;

namespace DisplayUiTweaks;

/// <summary>Our rows at the end of Options > Display (Mod Settings Tab moves them to its Mods tab); see docs/internal.md,
/// "Settings rows". Order: UI Area, HUD size, menu size, Edit HUD Layout, Reset HUD Layout, Hide HUD Outside Combat.</summary>
internal static class SettingsRows
{
    public const string Prefix = "DUT_";
    private const string SpacerId = "DUT_Spacer", AreaId = "DUT_UiArea", HudId = "DUT_HudScale", MenuId = "DUT_MenuScale",
        EditId = "DUT_EditHud", ResetId = "DUT_ResetHud", HideId = "DUT_HideHud";
    // 1.0.0 rows (Custom UI Aspect Ratio, Bounty Board & Map Fix): only freed from the registry.
    private static readonly string[] AllIds = { SpacerId, AreaId, HudId, MenuId, EditId, ResetId, HideId, "DUT_BoxAspect", "DUT_BoxUiToolkit" };
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

    private static readonly Dictionary<string, LocalizedMessage> _messages = new Dictionary<string, LocalizedMessage>();
    private static readonly Dictionary<string, Func<float>> _sliderValues = new();
    private static readonly Dictionary<string, Func<int>> _dropdownValues = new();

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
        var controls = tab.m_controls;
        if (controls == null) { DisplayUiTweaksMod.Log.Warning("DisplaySettingsTab.m_controls is null"); return; }
        var content = DisplayContent(controls);
        if (content == null) { DisplayUiTweaksMod.Log.Warning("Display tab has no content root yet"); return; }

        // Drop references to destroyed rows (left by an older build).
        ForgetRows(controls, oursToo: false);

        if (content.Find(HudId) != null)
        {
            DisplayUiTweaksMod.Log.Msg("Settings rows already present, skipping");
            return;
        }
        RemoveRegistryEntries(controls);

        AddSpacer(controls, content);

        AddSlider(controls, content, AreaId,
            Msg(AreaId, "UI Area"),
            Msg(AreaId + "_Desc", AreaDescription),
            Prefs.UiAreaMin, Prefs.UiAreaMax, Prefs.UiAreaStep, () => Prefs.UiAreaValue,
            v => v.ToString("0.00"),
            v => { Prefs.UiArea.Value = Mathf.Round(v * 100f) / 100f; DisplayUiTweaksMod.OnLayoutPrefChanged(); });

        AddSlider(controls, content, HudId,
            Msg(HudId, "HUD & Dialogue UI Size"),
            Msg(HudId + "_Desc", "Scale of the in-game HUD, overlays and dialogue."),
            Prefs.HudScaleMin, Prefs.HudScaleMax, Prefs.HudScaleStep, () => Prefs.HudScalePercent.Value,
            v => Mathf.RoundToInt(v) + "%",
            v => { Prefs.HudScalePercent.Value = Mathf.Round(v); DisplayUiTweaksMod.OnLayoutPrefChanged(); });

        AddSlider(controls, content, MenuId,
            Msg(MenuId, "Menu UI Size"),
            Msg(MenuId + "_Desc", "Scale of menus (inventory, stats, map, settings)."),
            Prefs.HudScaleMin, Prefs.HudScaleMax, Prefs.HudScaleStep, () => Prefs.MenuScalePercent.Value,
            v => Mathf.RoundToInt(v) + "%",
            v => { Prefs.MenuScalePercent.Value = Mathf.Round(v); DisplayUiTweaksMod.OnLayoutPrefChanged(); });

        AddButton(controls, content, EditId,
            Msg(EditId, "Edit HUD Layout"),
            Msg(EditId + "_Desc", "Move and resize HUD elements with the mouse, on top of the game, with sample content in empty elements."),
            () => Hud.HudEditor.Enter());

        AddButton(controls, content, ResetId,
            Msg(ResetId, "Reset HUD Layout"),
            Msg(ResetId + "_Desc", "Put every HUD element back where the game has it, at its normal size."),
            () => Hud.HudLayout.ResetAll());

        AddDropdown(controls, content, HideId,
            Msg(HideId, "Hide HUD Outside Combat"),
            Msg(HideId + "_Desc", "Fades out health, equipment, money, durability, clock and location a few seconds after combat ends; " +
                                  "they come back when combat starts. The last option keeps the health bar while you are not at full health. " +
                                  "Item pickups, hints and chat stay."),
            HideNames,
            () => Mathf.Clamp(Prefs.HideHudOutsideCombat.Value, 0, HideNames.Length - 1),
            i => { Prefs.HideHudOutsideCombat.Value = i; Prefs.Save(); });

        DisplayUiTweaksMod.Log.Msg("Added Display & UI Tweaks rows to Options > Display");
    }

    /// <summary>Hot reload / unload: destroy our rows on every live settings screen and free their registry keys.</summary>
    public static void RemoveAll()
    {
        int removed = 0;
        foreach (var s in Resources.FindObjectsOfTypeAll<SettingsScreen>())
        {
            var controls = s != null && s.m_displayTab != null ? s.m_displayTab.m_controls : null;
            if (controls == null) continue;
            // Unregister before destroying, or Back throws on the destroyed dropdown.
            ForgetRows(controls, oursToo: true);
            // Anywhere in the screen: Mod Settings Tab moves the rows to its own tab.
            foreach (var row in s!.GetComponentsInChildren<SettingsItemGUIBase>(true))
                if (row != null && row.gameObject.name.StartsWith(Prefix)) { UnityEngine.Object.DestroyImmediate(row.gameObject); removed++; }
            RemoveRegistryEntries(controls);
        }
        if (removed > 0) DisplayUiTweaksMod.Log.Msg("Removed " + removed + " settings rows");
    }

    /// <summary>Drop references the controls hold to destroyed rows (and, with oursToo, to our live rows):
    /// the dropdown instance lists and the cached selected / modal-previous element.</summary>
    private static void ForgetRows(SettingsScreenControls controls, bool oursToo)
    {
        int dropped = 0;
        var actual = controls.m_actualDropDownInstances;
        if (actual != null)
            for (int i = actual.Count - 1; i >= 0; i--)
            {
                var d = actual[i];
                if (d == null || (oursToo && d.gameObject.name.StartsWith(Prefix))) { actual.RemoveAt(i); dropped++; }
            }
        var bound = controls.m_boundDropDownInstances;
        if (bound != null)
            for (int i = bound.Count - 1; i >= 0; i--)
                if (bound[i] == null) { bound.RemoveAt(i); dropped++; }
        if (IsDeadOrOurs(controls.m_cachedSelectedItemGUI, oursToo)) { controls.m_cachedSelectedItemGUI = null; dropped++; }
        if (IsDeadOrOurs(controls.m_modalPreviousElement, oursToo)) { controls.m_modalPreviousElement = null; dropped++; }
        if (dropped > 0) DisplayUiTweaksMod.Log.Msg("Dropped " + dropped + " settings-screen reference(s) to " + (oursToo ? "our rows" : "destroyed rows"));
    }

    private static bool IsDeadOrOurs(SettingsItemGUIBase? item, bool oursToo)
    {
        if (item is null) return false;                       // no reference at all
        if (item == null) return true;                        // Unity-destroyed object
        return oursToo && item.gameObject.name.StartsWith(Prefix);
    }

    private static RectTransform? DisplayContent(SettingsScreenControls controls)
    {
        var roots = controls.m_nameToContentRoot;
        if (roots == null || !roots.ContainsKey(PlayerSettingCategory.Display)) return null;
        return roots[PlayerSettingCategory.Display];
    }

    private static void RemoveRegistryEntries(SettingsScreenControls controls)
    {
        if (controls.m_categoryToContentToItem == null || !controls.m_categoryToContentToItem.ContainsKey(PlayerSettingCategory.Display)) return;
        var items = controls.m_categoryToContentToItem[PlayerSettingCategory.Display];
        if (items == null) return;
        foreach (var id in AllIds) items.Remove(id);
    }

    private static void AddSlider(SettingsScreenControls controls, RectTransform content, string id, LocalizedMessage name, LocalizedMessage desc,
        float min, float max, float step, Func<float> current, Func<float, string> display, Action<float> onChanged)
    {
        int steps = Mathf.Max(1, Mathf.RoundToInt((max - min) / step));
        float increment = 1f / steps;
        float ToValue(float normalized) => min + Mathf.Clamp01(normalized) * (max - min);
        float ToNormalized(float value) => Mathf.Clamp01((value - min) / (max - min));

        Func<float, string> displayNormalized = n => display(ToValue(n));
        Action<float> changedNormalized = n => onChanged(ToValue(n));

        int before = content.childCount;
        controls.AddSliderItem(
            PlayerSettingCategory.Display,
            null!,                              // IPlayerSettingAdapter<float>: stored, never read (see class remarks)
            name,
            ToNormalized(current()),
            changedNormalized,
            increment,
            displayNormalized,
            desc,
            10,                                 // maxScrollMultiplier: a held key speeds up to 10 steps per repeat
            false,                              // invokeCallbackOnStart
            false,                              // canSelectForFader
            false);                             // showOffOnZero
        NameNewRow(content, before, id);
        _sliderValues[id] = () => ToNormalized(current());
        // Held-key repeat interval: long enough that a normal key tap is exactly one step.
        if (content.childCount > before)
        {
            var slider = content.GetChild(content.childCount - 1).GetComponent<SliderSettingsItemGUI>();
            if (slider != null && slider.m_scrollHoldThreshold < HoldRepeatSeconds) slider.m_scrollHoldThreshold = HoldRepeatSeconds;
        }
    }

    /// <summary>A button row, like the game's Reset Tutorials.</summary>
    private static void AddButton(SettingsScreenControls controls, RectTransform content, string id, LocalizedMessage name, LocalizedMessage desc, Action onClick)
    {
        int before = content.childCount;
        controls.AddButtonItem(id, PlayerSettingCategory.Display, name, onClick, desc);
        NameNewRow(content, before, id);
    }

    private static void AddDropdown(SettingsScreenControls controls, RectTransform content, string id, LocalizedMessage name, LocalizedMessage desc,
        string[] options, Func<int> current, Action<int> onChanged)
    {
        var arr = new Il2CppStringArray(options.Length);
        for (int i = 0; i < options.Length; i++) arr[i] = options[i];
        int before = content.childCount;
        controls.AddActualDropDownItem(PlayerSettingCategory.Display, name, arr, current(), onChanged, desc, true, false);
        NameNewRow(content, before, id);
        _dropdownValues[id] = current;
    }

    /// <summary>The same empty divider row the game uses between its own groups.</summary>
    private static void AddSpacer(SettingsScreenControls controls, RectTransform content)
    {
        int before = content.childCount;
        controls.AddDividerItem(PlayerSettingCategory.Display, SpacerId);
        NameNewRow(content, before, SpacerId);
        if (content.childCount > before)
        {
            var row = content.GetChild(content.childCount - 1).GetComponent<SettingsItemGUIBase>();
            if (row != null && row.SettingLabel != null) row.SettingLabel.text = "";
        }
    }

    private static void NameNewRow(RectTransform content, int before, string id)
    {
        if (content.childCount > before) content.GetChild(content.childCount - 1).name = id;
    }

    /// <summary>A LocalizedMessage is a ScriptableObject holding one string per language; fill every language with the same text.</summary>
    private static LocalizedMessage Msg(string id, string text)
    {
        if (_messages.TryGetValue(id, out var cached) && cached != null) return cached;
        var so = ScriptableObject.CreateInstance(Il2CppType.Of<LocalizedMessage>());
        var m = so.Cast<LocalizedMessage>();
        m.name = id;
        m.Id = id;
        m.English = text; m.French = text; m.Italian = text; m.German = text; m.Spanish = text;
        m.BrazilianPortuguese = text; m.TraditionalChinese = text; m.SimplifiedChinese = text;
        m.Korean = text; m.Russian = text; m.Japanese = text; m.Polish = text;
        m.hideFlags = HideFlags.HideAndDontSave;
        _messages[id] = m;
        return m;
    }
}
