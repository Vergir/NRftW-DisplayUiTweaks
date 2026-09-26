using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppMoon.Forsaken;
using UnityEngine;

namespace MoreAspectRatios;

/// <summary>
/// Two slider rows in Options > Display, built with the game's own SettingsScreenControls.AddSliderItem.
/// The row works on a normalized 0..1 value with a fixed increment; we map that to our ranges.
/// The IPlayerSettingAdapter argument is only stored by the row (never read in this build), so we pass null.
///
/// SettingsScreen.Initialize (and with it DisplaySettingsTab.Initialize) runs every time a settings screen is built
/// (main menu, in game), and the controls keep a per-category dictionary keyed by the label's Id. Adding the same Id
/// twice throws *after* the row prefab was instantiated, which leaves an orphan "Slider" row. So we track our rows and
/// only add when they are not already alive under the current Display content root.
/// </summary>
internal static class SettingsRows
{
    private const string HudId = "MAR_HudScale", BoxId = "MAR_BoxAspect";

    private static LocalizedMessage? _hudName, _hudDesc, _boxName, _boxDesc;
    private static readonly List<Transform> _rows = new List<Transform>();

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

        // Stale registry entries from a previous screen would make AddSliderItem throw; drop them.
        if (controls.m_categoryToContentToItem != null && controls.m_categoryToContentToItem.ContainsKey(PlayerSettingCategory.Display))
        {
            var items = controls.m_categoryToContentToItem[PlayerSettingCategory.Display];
            if (items != null) { items.Remove(HudId); items.Remove(BoxId); }
        }

        _hudName ??= Message(HudId, "HUD Size");
        _hudDesc ??= Message(HudId + "_Desc", "Scale of the in-game HUD relative to the game's default (More Aspect Ratios mod).");
        _boxName ??= Message(BoxId, "HUD Box Aspect");
        _boxDesc ??= Message(BoxId + "_Desc", "Width-to-height ratio of the HUD box when UI Aspect is set to Custom (More Aspect Ratios). 1.00 = square, 1.78 = 16:9.");

        AddSlider(controls, content, _hudName, _hudDesc,
            Prefs.HudScaleMin, Prefs.HudScaleMax, Prefs.HudScaleStep, Prefs.HudScalePercent.Value,
            v => Mathf.RoundToInt(v) + "%",
            v => { Prefs.HudScalePercent.Value = Mathf.Round(v); MoreAspectRatiosMod.OnLayoutPrefChanged(); });

        AddSlider(controls, content, _boxName, _boxDesc,
            Prefs.UiBoxAspectMin, Prefs.UiBoxAspectMax, Prefs.UiBoxAspectStep, Prefs.UiBoxAspect.Value,
            v => v.ToString("0.00"),
            v => { Prefs.UiBoxAspect.Value = Mathf.Round(v * 100f) / 100f; MoreAspectRatiosMod.OnLayoutPrefChanged(); });

        MoreAspectRatiosMod.Log.Msg("Added HUD Size and HUD Box Aspect sliders to Options > Display");
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
        if (content != null && content.childCount > before)
            _rows.Add(content.GetChild(content.childCount - 1));
    }

    /// <summary>A LocalizedMessage is a ScriptableObject holding one string per language; fill every language with the same text.</summary>
    private static LocalizedMessage Message(string id, string text)
    {
        var so = ScriptableObject.CreateInstance(Il2CppType.Of<LocalizedMessage>());
        var m = so.Cast<LocalizedMessage>();
        m.name = id;
        m.Id = id;
        m.English = text; m.French = text; m.Italian = text; m.German = text; m.Spanish = text;
        m.BrazilianPortuguese = text; m.TraditionalChinese = text; m.SimplifiedChinese = text;
        m.Korean = text; m.Russian = text; m.Japanese = text; m.Polish = text;
        m.hideFlags = HideFlags.HideAndDontSave;
        return m;
    }
}
