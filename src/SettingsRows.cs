using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppMoon.Forsaken;
using UnityEngine;

namespace MoreAspectRatios;

/// <summary>
/// Our rows in Options > Display, built with the game's own SettingsScreenControls.AddSliderItem / AddActualDropDownItem.
/// Sliders work on a normalized 0..1 value with a fixed increment; we map that to our ranges.
/// The IPlayerSettingAdapter argument of AddSliderItem is only stored by the row (never read in this build), so we pass null.
///
/// Lifetime facts that shape this class:
///  - The game builds its SettingsScreens once, during boot, and keeps them for the whole session (quitting to the main
///    menu does not rebuild them). DisplaySettingsTab.Initialize can run twice for the same screen during boot.
///  - SettingsScreenControls keeps a per-category dictionary keyed by the label's Id; adding the same Id twice throws
///    *after* the row prefab was instantiated, leaving an orphan "Slider" row.
/// So rows are found by their GameObject name (MAR_*), not by static state, which also survives a hot reload: the old
/// build removes its rows on unload, and the new build adds fresh ones to every live settings screen right away.
/// </summary>
internal static class SettingsRows
{
    private const string Prefix = "MAR_";
    private const string HudId = "MAR_HudScale", MenuId = "MAR_MenuScale", BoxId = "MAR_BoxAspect", BoxToolkitId = "MAR_BoxUiToolkit";
    private static readonly string[] AllIds = { HudId, MenuId, BoxId, BoxToolkitId };

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
        if (screens > 0) MoreAspectRatiosMod.Log.Msg("Checked " + screens + " live settings screen(s) for our rows");
    }

    public static void AddTo(DisplaySettingsTab tab)
    {
        var controls = tab.m_controls;
        if (controls == null) { MoreAspectRatiosMod.Log.Warning("DisplaySettingsTab.m_controls is null"); return; }
        var content = DisplayContent(controls);
        if (content == null) { MoreAspectRatiosMod.Log.Warning("Display tab has no content root yet"); return; }

        if (content.Find(HudId) != null)
        {
            MoreAspectRatiosMod.Log.Msg("Settings rows already present, skipping");
            return;
        }
        RemoveRegistryEntries(controls);

        AddSlider(controls, content, HudId,
            Msg(HudId, "HUD & Dialogue UI Size"),
            Msg(HudId + "_Desc", "Scale of the in-game HUD, overlays and dialogue (More Aspect Ratios)."),
            Prefs.HudScaleMin, Prefs.HudScaleMax, Prefs.HudScaleStep, Prefs.HudScalePercent.Value,
            v => Mathf.RoundToInt(v) + "%",
            v => { Prefs.HudScalePercent.Value = Mathf.Round(v); MoreAspectRatiosMod.OnLayoutPrefChanged(); });

        AddSlider(controls, content, MenuId,
            Msg(MenuId, "Menu UI Size"),
            Msg(MenuId + "_Desc", "Scale of menus (inventory, stats, map, settings) (More Aspect Ratios)."),
            Prefs.HudScaleMin, Prefs.HudScaleMax, Prefs.HudScaleStep, Prefs.MenuScalePercent.Value,
            v => Mathf.RoundToInt(v) + "%",
            v => { Prefs.MenuScalePercent.Value = Mathf.Round(v); MoreAspectRatiosMod.OnLayoutPrefChanged(); });

        AddSlider(controls, content, BoxId,
            Msg(BoxId, "Custom UI Aspect Ratio"),
            Msg(BoxId + "_Desc", "Width-to-height ratio the whole UI is kept in when UI Aspect is set to Custom (More Aspect Ratios), 1.78 = 16:9, 3.0 = 27:9."),
            Prefs.UiBoxAspectMin, Prefs.UiBoxAspectMax, Prefs.UiBoxAspectStep, Prefs.UiBoxAspect.Value,
            v => v.ToString("0.00"),
            v => { Prefs.UiBoxAspect.Value = Mathf.Round(v * 100f) / 100f; MoreAspectRatiosMod.OnLayoutPrefChanged(); });

        AddDropdown(controls, content, BoxToolkitId,
            Msg(BoxToolkitId, "Box Bounty Boards & Map Details"),
            Msg(BoxToolkitId + "_Desc", "Keep the bounty and challenge boards and the map's detail bar inside the UI box. The game's UI Aspect option only boxes parts of these screens (More Aspect Ratios mod)."),
            new[] { "Off", "On" },
            Prefs.BoxUiToolkitScreens.Value ? 1 : 0,
            i => { Prefs.BoxUiToolkitScreens.Value = i == 1; MoreAspectRatiosMod.OnLayoutPrefChanged(); });

        MoreAspectRatiosMod.Log.Msg("Added More Aspect Ratios rows to Options > Display");
    }

    /// <summary>Hot reload / unload: destroy our rows on every live settings screen and free their registry keys.</summary>
    public static void RemoveAll()
    {
        int removed = 0;
        foreach (var s in Resources.FindObjectsOfTypeAll<SettingsScreen>())
        {
            var controls = s != null && s.m_displayTab != null ? s.m_displayTab.m_controls : null;
            if (controls == null) continue;
            var content = DisplayContent(controls);
            if (content != null)
                for (int i = content.childCount - 1; i >= 0; i--)
                {
                    var child = content.GetChild(i);
                    if (child != null && child.name.StartsWith(Prefix)) { UnityEngine.Object.DestroyImmediate(child.gameObject); removed++; }
                }
            RemoveRegistryEntries(controls);
        }
        if (removed > 0) MoreAspectRatiosMod.Log.Msg("Removed " + removed + " settings rows");
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
            10,                                 // maxScrollMultiplier (held left/right speeds up to 10 steps)
            false,                              // invokeCallbackOnStart
            false,                              // canSelectForFader
            false);                             // showOffOnZero
        NameNewRow(content, before, id);
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
