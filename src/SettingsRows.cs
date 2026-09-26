using System;
using Il2CppInterop.Runtime;
using Il2CppMoon.Forsaken;
using UnityEngine;

namespace MoreAspectRatios;

/// <summary>
/// Two slider rows in Options > Display, built with the game's own SettingsScreenControls.AddSliderItem.
/// The row works on a normalized 0..1 value with a fixed increment; we map that to our ranges.
/// The IPlayerSettingAdapter argument is only stored by the row (never read in this build), so we pass null.
/// </summary>
internal static class SettingsRows
{
    private static LocalizedMessage? _hudName, _hudDesc, _boxName, _boxDesc;

    public static void AddTo(DisplaySettingsTab tab)
    {
        var controls = tab.m_controls;
        if (controls == null) { MoreAspectRatiosMod.Log.Warning("DisplaySettingsTab.m_controls is null"); return; }

        _hudName ??= Message("MAR_HudScale", "HUD Size");
        _hudDesc ??= Message("MAR_HudScale_Desc", "Scale of the in-game HUD relative to the game's default (More Aspect Ratios mod).");
        _boxName ??= Message("MAR_BoxAspect", "HUD Box Aspect");
        _boxDesc ??= Message("MAR_BoxAspect_Desc", "Width-to-height ratio of the box the HUD is kept in when UI Aspect is not Native. 1.00 = square, 1.78 = 16:9 (More Aspect Ratios mod).");

        AddSlider(controls, _hudName, _hudDesc,
            Prefs.HudScaleMin, Prefs.HudScaleMax, Prefs.HudScaleStep, Prefs.HudScalePercent.Value,
            v => Mathf.RoundToInt(v) + "%",
            v => { Prefs.HudScalePercent.Value = Mathf.Round(v); MoreAspectRatiosMod.OnLayoutPrefChanged(); });

        AddSlider(controls, _boxName, _boxDesc,
            Prefs.UiBoxAspectMin, Prefs.UiBoxAspectMax, Prefs.UiBoxAspectStep, Prefs.UiBoxAspect.Value,
            v => v.ToString("0.00"),
            v => { Prefs.UiBoxAspect.Value = Mathf.Round(v * 100f) / 100f; MoreAspectRatiosMod.OnLayoutPrefChanged(); });

        MoreAspectRatiosMod.Log.Msg("Added HUD Size and HUD Box Aspect sliders to Options > Display");
    }

    private static void AddSlider(SettingsScreenControls controls, LocalizedMessage name, LocalizedMessage desc,
        float min, float max, float step, float current, Func<float, string> display, Action<float> onChanged)
    {
        int steps = Mathf.Max(1, Mathf.RoundToInt((max - min) / step));
        float increment = 1f / steps;
        float ToValue(float normalized) => min + Mathf.Clamp01(normalized) * (max - min);
        float ToNormalized(float value) => Mathf.Clamp01((value - min) / (max - min));

        Func<float, string> displayNormalized = n => display(ToValue(n));
        Action<float> changedNormalized = n => onChanged(ToValue(n));

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
