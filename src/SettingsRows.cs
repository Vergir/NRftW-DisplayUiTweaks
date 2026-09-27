using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppMoon.Forsaken;
using UnityEngine;

namespace DisplayUiTweaks;

/// <summary>Our rows in Options > Display (see docs/internal.md, "Settings rows").</summary>
internal static class SettingsRows
{
    private const string Prefix = "DUT_";
    private const string SpacerId = "DUT_Spacer", HudId = "DUT_HudScale", MenuId = "DUT_MenuScale", BoxId = "DUT_BoxAspect", BoxToolkitId = "DUT_BoxUiToolkit";
    private static readonly string[] AllIds = { SpacerId, HudId, MenuId, BoxId, BoxToolkitId };

    private const float HoldRepeatSeconds = 0.3f;

    private static readonly Dictionary<string, LocalizedMessage> _messages = new Dictionary<string, LocalizedMessage>();

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

        AddSlider(controls, content, HudId,
            Msg(HudId, "HUD & Dialogue UI Size"),
            Msg(HudId + "_Desc", "Scale of the in-game HUD, overlays and dialogue (Display & UI Tweaks)."),
            Prefs.HudScaleMin, Prefs.HudScaleMax, Prefs.HudScaleStep, Prefs.HudScalePercent.Value,
            v => Mathf.RoundToInt(v) + "%",
            v => { Prefs.HudScalePercent.Value = Mathf.Round(v); DisplayUiTweaksMod.OnLayoutPrefChanged(); });

        AddSlider(controls, content, MenuId,
            Msg(MenuId, "Menu UI Size"),
            Msg(MenuId + "_Desc", "Scale of menus (inventory, stats, map, settings) (Display & UI Tweaks)."),
            Prefs.HudScaleMin, Prefs.HudScaleMax, Prefs.HudScaleStep, Prefs.MenuScalePercent.Value,
            v => Mathf.RoundToInt(v) + "%",
            v => { Prefs.MenuScalePercent.Value = Mathf.Round(v); DisplayUiTweaksMod.OnLayoutPrefChanged(); });

        AddSlider(controls, content, BoxId,
            Msg(BoxId, "Custom UI Aspect Ratio"),
            Msg(BoxId + "_Desc", "Width-to-height ratio the whole UI is kept in when UI Aspect is set to Custom (Display & UI Tweaks), 1.78 = 16:9, 3.0 = 27:9."),
            Prefs.UiBoxAspectMin, Prefs.UiBoxAspectMax, Prefs.UiBoxAspectStep, Prefs.UiBoxAspect.Value,
            v => v.ToString("0.00"),
            v => { Prefs.UiBoxAspect.Value = Mathf.Round(v * 100f) / 100f; DisplayUiTweaksMod.OnLayoutPrefChanged(); });

        AddDropdown(controls, content, BoxToolkitId,
            Msg(BoxToolkitId, "Bounty Board & Map Fix"),
            Msg(BoxToolkitId + "_Desc", "Keeps the bounty and challenge boards and the map's detail bar inside the UI box like the other menus. The game only boxes parts of these screens. Turn off to see them as the game draws them (Display & UI Tweaks)."),
            new[] { "Off", "On" },
            Prefs.BoxUiToolkitScreens.Value ? 1 : 0,
            i => { Prefs.BoxUiToolkitScreens.Value = i == 1; DisplayUiTweaksMod.OnLayoutPrefChanged(); });

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
            var content = DisplayContent(controls);
            if (content != null)
                for (int i = content.childCount - 1; i >= 0; i--)
                {
                    var child = content.GetChild(i);
                    if (child != null && child.name.StartsWith(Prefix)) { UnityEngine.Object.DestroyImmediate(child.gameObject); removed++; }
                }
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
        float min, float max, float step, float current, Func<float, string> display, Action<float> onChanged)
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
            ToNormalized(current),
            changedNormalized,
            increment,
            displayNormalized,
            desc,
            4,                                  // maxScrollMultiplier: a held key speeds up to 4 steps per repeat
            false,                              // invokeCallbackOnStart
            false,                              // canSelectForFader
            false);                             // showOffOnZero
        NameNewRow(content, before, id);
        // Held-key repeat interval: long enough that a normal key tap is exactly one step.
        if (content.childCount > before)
        {
            var slider = content.GetChild(content.childCount - 1).GetComponent<SliderSettingsItemGUI>();
            if (slider != null && slider.m_scrollHoldThreshold < HoldRepeatSeconds) slider.m_scrollHoldThreshold = HoldRepeatSeconds;
        }
    }

    private static void AddDropdown(SettingsScreenControls controls, RectTransform content, string id, LocalizedMessage name, LocalizedMessage desc,
        string[] options, int current, Action<int> onChanged)
    {
        var arr = new Il2CppStringArray(options.Length);
        for (int i = 0; i < options.Length; i++) arr[i] = options[i];
        int before = content.childCount;
        controls.AddActualDropDownItem(PlayerSettingCategory.Display, name, arr, current, onChanged, desc, true, false);
        NameNewRow(content, before, id);
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
