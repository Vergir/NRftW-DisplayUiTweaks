using MelonLoader;

namespace MoreAspectRatios;

/// <summary>All user-tunable values. Stored in UserData/MelonPreferences.cfg under [MoreAspectRatios].
/// HudScalePercent and UiBoxAspect are also exposed as sliders in the game's Display settings tab.</summary>
internal static class Prefs
{
    public const float HudScaleMin = 10f, HudScaleMax = 150f, HudScaleStep = 1f;
    public const float UiBoxAspectMin = 1.0f, UiBoxAspectMax = 4.0f, UiBoxAspectStep = 0.01f;
    public const float GameDefaultUiBoxAspect = 16f / 9f;

    private static MelonPreferences_Category _cat = null!;

    public static MelonPreferences_Entry<bool> Enabled = null!;
    public static MelonPreferences_Entry<bool> UnlockResolutions = null!;
    public static MelonPreferences_Entry<bool> UnlockUiAspectModes = null!;
    public static MelonPreferences_Entry<bool> DisableLetterbox = null!;
    public static MelonPreferences_Entry<bool> FitMenusToBox = null!;
    public static MelonPreferences_Entry<bool> FitPanelsToBox = null!;
    public static MelonPreferences_Entry<bool> StretchMismatchedRoots = null!;
    public static MelonPreferences_Entry<float> HudScalePercent = null!;
    public static MelonPreferences_Entry<float> UiBoxAspect = null!;
    public static MelonPreferences_Entry<float> MenuDpiThreshold = null!;
    public static MelonPreferences_Entry<bool> AddSettingsRows = null!;

    public static void Init()
    {
        _cat = MelonPreferences.CreateCategory("MoreAspectRatios", "More Aspect Ratios");

        Enabled = _cat.CreateEntry("Enabled", true, description: "Master switch.");
        UnlockResolutions = _cat.CreateEntry("UnlockResolutions", true,
            description: "List every resolution the display supports in Options > Display (the game hides anything narrower than 16:10 or wider than 32:9).");
        UnlockUiAspectModes = _cat.CreateEntry("UnlockUiAspectModes", true,
            description: "Always show the 'UI aspect' option with all modes plus 'Custom (More Aspect Ratios)' (the game hides the option on non-widescreen monitors).");
        DisableLetterbox = _cat.CreateEntry("DisableLetterbox", true,
            description: "Let the world fill the screen on non-16:9 displays (MoonRenderPipelineAsset.Enforce169Aspect = Never).");
        HudScalePercent = _cat.CreateEntry("HudScalePercent", 100f,
            description: "HUD/overlay canvas scale relative to the game's own scale (10-150). Also a slider in Options > Display.");
        UiBoxAspect = _cat.CreateEntry("UiBoxAspect", GameDefaultUiBoxAspect,
            description: "Aspect ratio of the HUD box for the 'Custom (More Aspect Ratios)' UI aspect mode (1.00-4.00). Also a slider in Options > Display.");
        FitMenusToBox = _cat.CreateEntry("FitMenusToBox", true,
            description: "Scale menu canvases down so their 1920px reference width fits inside the HUD box.");
        FitPanelsToBox = _cat.CreateEntry("FitPanelsToBox", true,
            description: "Same for UI Toolkit panels (map, fast travel, activity screens).");
        StretchMismatchedRoots = _cat.CreateEntry("StretchMismatchedRoots", true,
            description: "Screens with a fixed-size root (scribe table, inspect player) are stretched to that root instead of boxed, so their content is not cut off.");
        MenuDpiThreshold = _cat.CreateEntry("MenuDpiThreshold", 100f,
            description: "CanvasScaler.fallbackScreenDPI above this counts as a menu canvas (game: 96 = HUD, 221.5 = menus).");
        AddSettingsRows = _cat.CreateEntry("AddSettingsRows", true,
            description: "Add the HUD size and HUD box aspect sliders to Options > Display.");
    }

    public static void Save() => MelonPreferences.Save();

    public static float HudScale => Clamp(HudScalePercent.Value, HudScaleMin, HudScaleMax) / 100f;
    public static float BoxAspect => Clamp(UiBoxAspect.Value, UiBoxAspectMin, UiBoxAspectMax);

    private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
}
