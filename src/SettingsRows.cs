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
/// SettingsScreen.Initialize (and with it DisplaySettingsTab.Initialize) runs every time a settings screen is built
/// (main menu, in game), and the controls keep a per-category dictionary keyed by the label's Id. Adding the same Id
/// twice throws *after* the row prefab was instantiated, which leaves an orphan "Slider" row. So we track our rows and
/// only add when they are not already alive under the current Display content root. On unload (hot reload) the rows
/// are destroyed and their registry entries removed, so the new build can add fresh ones.
/// </summary>
internal static class SettingsRows
{
    private const string HudId = "MAR_HudScale", MenuId = "MAR_MenuScale", BoxId = "MAR_BoxAspect",
        MarginXId = "MAR_MarginX", MarginYId = "MAR_MarginY", LayoutId = "MAR_CustomLayout";
    private static readonly string[] AllIds = { HudId, MenuId, BoxId, MarginXId, MarginYId, LayoutId };

    private static readonly Dictionary<string, LocalizedMessage> _messages = new Dictionary<string, LocalizedMessage>();
    private static readonly List<Transform> _rows = new List<Transform>();
    private static SettingsScreenControls? _controls;

    public static void AddTo(DisplaySettingsTab tab)
    {
        var controls = tab.m_controls;
        if (controls == null) { MoreAspectRatiosMod.Log.Warning("DisplaySettingsTab.m_controls is null"); return; }

        RectTransform? content = null;
        if (controls.m_nameToContentRoot != null && controls.m_nameToContentRoot.ContainsKey(PlayerSettingCategory.Display))
            content = controls.m_nameToContentRoot[PlayerSettingCategory.Display];

        // Already there (same screen re-initialized)? Then nothing to do.
        _rows.RemoveAll(t => t == null);
        if (content != null && _rows.Count > 0 && _rows.TrueForAll(t => t.parent == content))
        {
            MoreAspectRatiosMod.Log.Msg("Settings rows already present, skipping");
            return;
        }
        _rows.Clear();
        _controls = controls;
        RemoveRegistryEntries(controls);

        AddSlider(controls, content,
            Msg(HudId, "HUD & Dialogue UI Size"),
            Msg(HudId + "_Desc", "Scale of the in-game HUD, overlays and dialogue relative to the game's default (More Aspect Ratios mod)."),
            Prefs.HudScaleMin, Prefs.HudScaleMax, Prefs.HudScaleStep, Prefs.HudScalePercent.Value,
            v => Mathf.RoundToInt(v) + "%",
            v => { Prefs.HudScalePercent.Value = Mathf.Round(v); MoreAspectRatiosMod.OnLayoutPrefChanged(); });

        AddSlider(controls, content,
            Msg(MenuId, "Menu UI Size"),
            Msg(MenuId + "_Desc", "Scale of menus (inventory, stats, map, settings) relative to the game's default, after they were shrunk to fit the UI box (More Aspect Ratios mod)."),
            Prefs.HudScaleMin, Prefs.HudScaleMax, Prefs.HudScaleStep, Prefs.MenuScalePercent.Value,
            v => Mathf.RoundToInt(v) + "%",
            v => { Prefs.MenuScalePercent.Value = Mathf.Round(v); MoreAspectRatiosMod.OnLayoutPrefChanged(); });

        AddSlider(controls, content,
            Msg(BoxId, "Custom UI Aspect Ratio"),
            Msg(BoxId + "_Desc", "Width-to-height ratio the whole UI is kept in when UI Aspect is set to Custom (More Aspect Ratios). 1.00 = square, 1.78 = 16:9."),
            Prefs.UiBoxAspectMin, Prefs.UiBoxAspectMax, Prefs.UiBoxAspectStep, Prefs.UiBoxAspect.Value,
            v => v.ToString("0.00"),
            v => { Prefs.UiBoxAspect.Value = Mathf.Round(v * 100f) / 100f; MoreAspectRatiosMod.OnLayoutPrefChanged(); });

        AddSlider(controls, content,
            Msg(MarginXId, "UI Edge Margin (Left/Right)"),
            Msg(MarginXId + "_Desc", "Empty space kept at the left and right screen edges, per side. The UI is fitted into the remaining area, in every UI Aspect mode (More Aspect Ratios mod)."),
            Prefs.MarginMin, Prefs.MarginMax, Prefs.MarginStep, Prefs.UiMarginXPercent.Value,
            v => v.ToString("0.0") + "%",
            v => { Prefs.UiMarginXPercent.Value = Mathf.Round(v * 2f) / 2f; MoreAspectRatiosMod.OnLayoutPrefChanged(); });

        AddSlider(controls, content,
            Msg(MarginYId, "UI Edge Margin (Top/Bottom)"),
            Msg(MarginYId + "_Desc", "Empty space kept at the top and bottom screen edges, per side. The UI is fitted into the remaining area, in every UI Aspect mode (More Aspect Ratios mod)."),
            Prefs.MarginMin, Prefs.MarginMax, Prefs.MarginStep, Prefs.UiMarginYPercent.Value,
            v => v.ToString("0.0") + "%",
            v => { Prefs.UiMarginYPercent.Value = Mathf.Round(v * 2f) / 2f; MoreAspectRatiosMod.OnLayoutPrefChanged(); });

        AddDropdown(controls, content,
            Msg(LayoutId, "Custom Mode Menu Layouts"),
            Msg(LayoutId + "_Desc", "Which layout the inventory and community chest use in Custom UI Aspect mode. 'Monitor' = picked from the monitor's shape (game behaviour). '16:9' = the layout the game uses for its own 16:9 UI Aspect mode (More Aspect Ratios mod)."),
            new[] { "Monitor", "16:9" },
            Prefs.CustomModeUses16x9Layouts.Value ? 1 : 0,
            i => { Prefs.CustomModeUses16x9Layouts.Value = i == 1; Prefs.Save(); });

        MoreAspectRatiosMod.Log.Msg("Added " + _rows.Count + " More Aspect Ratios rows to Options > Display");
    }

    /// <summary>Hot reload / unload: destroy our rows and free their registry keys.</summary>
    public static void RemoveAll()
    {
        foreach (var t in _rows)
            if (t != null) UnityEngine.Object.Destroy(t.gameObject);
        _rows.Clear();
        if (_controls != null) RemoveRegistryEntries(_controls);
        _controls = null;
    }

    private static void RemoveRegistryEntries(SettingsScreenControls controls)
    {
        if (controls.m_categoryToContentToItem == null || !controls.m_categoryToContentToItem.ContainsKey(PlayerSettingCategory.Display)) return;
        var items = controls.m_categoryToContentToItem[PlayerSettingCategory.Display];
        if (items == null) return;
        foreach (var id in AllIds) items.Remove(id);
    }

    private static void AddSlider(SettingsScreenControls controls, RectTransform? content, LocalizedMessage name, LocalizedMessage desc,
        float min, float max, float step, float current, Func<float, string> display, Action<float> onChanged)
    {
        int steps = Mathf.Max(1, Mathf.RoundToInt((max - min) / step));
        float increment = 1f / steps;
        float ToValue(float normalized) => min + Mathf.Clamp01(normalized) * (max - min);
        float ToNormalized(float value) => Mathf.Clamp01((value - min) / (max - min));

        Func<float, string> displayNormalized = n => display(ToValue(n));
        Action<float> changedNormalized = n => onChanged(ToValue(n));

        int before = content != null ? content.childCount : -1;
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
        TrackNewRow(content, before);
    }

    private static void AddDropdown(SettingsScreenControls controls, RectTransform? content, LocalizedMessage name, LocalizedMessage desc,
        string[] options, int current, Action<int> onChanged)
    {
        var arr = new Il2CppStringArray(options.Length);
        for (int i = 0; i < options.Length; i++) arr[i] = options[i];
        int before = content != null ? content.childCount : -1;
        controls.AddActualDropDownItem(PlayerSettingCategory.Display, name, arr, current, onChanged, desc, true, false);
        TrackNewRow(content, before);
    }

    private static void TrackNewRow(RectTransform? content, int before)
    {
        if (content != null && content.childCount > before)
            _rows.Add(content.GetChild(content.childCount - 1));
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
