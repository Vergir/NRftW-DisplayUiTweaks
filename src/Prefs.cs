using MelonLoader;

namespace MoreAspectRatios;

/// <summary>All user-tunable values. Stored in UserData/MelonPreferences.cfg under [MoreAspectRatios].
/// The sizes, the custom aspect, the edge margins and the layout switch are also rows in Options > Display.</summary>
internal static class Prefs
{
    public const float HudScaleMin = 10f, HudScaleMax = 150f, HudScaleStep = 1f;
    public const float UiBoxAspectMin = 1.0f, UiBoxAspectMax = 4.0f, UiBoxAspectStep = 0.01f;
    public const float MarginMin = 0f, MarginMax = 25f, MarginStep = 0.5f;
    public const float GameDefaultUiBoxAspect = 16f / 9f;

    private static MelonPreferences_Category _cat = null!;

    public static MelonPreferences_Entry<bool> Enabled = null!;
    public static MelonPreferences_Entry<bool> UnlockResolutions = null!;
    public static MelonPreferences_Entry<bool> UnlockUiAspectModes = null!;
    public static MelonPreferences_Entry<bool> DisableLetterbox = null!;
    public static MelonPreferences_Entry<bool> FitMenusToBox = null!;
    public static MelonPreferences_Entry<bool> FitPanelsToBox = null!;
    public static MelonPreferences_Entry<bool> BoxUiToolkitScreens = null!;
    public static MelonPreferences_Entry<bool> StretchMismatchedRoots = null!;
    public static MelonPreferences_Entry<bool> CustomModeUses16x9Layouts = null!;
    public static MelonPreferences_Entry<float> HudScalePercent = null!;
    public static MelonPreferences_Entry<float> MenuScalePercent = null!;
    public static MelonPreferences_Entry<float> UiBoxAspect = null!;
    public static MelonPreferences_Entry<float> UiMarginXPercent = null!;
    public static MelonPreferences_Entry<float> UiMarginYPercent = null!;
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
            description: "In-game HUD, overlays and dialogue (canvases with fallback DPI 96): scale relative to the game's own (10-150). Also a slider in Options > Display.");
        MenuScalePercent = _cat.CreateEntry("MenuScalePercent", 100f,
            description: "Menus (inventory, stats, map, settings...; fallback DPI 221.5 and UI Toolkit panels): scale relative to the game's own, after the fit-to-box cap (10-150). Also a slider in Options > Display.");
        UiBoxAspect = _cat.CreateEntry("UiBoxAspect", GameDefaultUiBoxAspect,
            description: "Aspect ratio the whole UI is boxed to in the 'Custom (More Aspect Ratios)' UI aspect mode (1.00-4.00, 1.78 = 16:9). Also a slider in Options > Display.");
        UiMarginXPercent = _cat.CreateEntry("UiMarginXPercent", 0f,
            description: "Empty space kept at the left and right screen edges, in percent of the screen width per side (0-25). The UI box is fitted inside the remaining area. Works in every UI aspect mode. Also a slider in Options > Display.");
        UiMarginYPercent = _cat.CreateEntry("UiMarginYPercent", 0f,
            description: "Same for the top and bottom edges, in percent of the screen height per side (0-25). Also a slider in Options > Display.");
        CustomModeUses16x9Layouts = _cat.CreateEntry("CustomModeUses16x9Layouts", false,
            description: "In Custom UI aspect mode, let the inventory and community chest use their 16:9 layout (the one the game uses for its own 16:9 UI aspect mode) instead of picking a layout from the monitor's shape. Also a row in Options > Display.");
        FitMenusToBox = _cat.CreateEntry("FitMenusToBox", true,
            description: "Scale menu canvases down so their 1920x1080 reference size fits inside the UI box.");
        FitPanelsToBox = _cat.CreateEntry("FitPanelsToBox", true,
            description: "Same for UI Toolkit panels (map, fast travel, bounty and challenge boards).");
        BoxUiToolkitScreens = _cat.CreateEntry("BoxUiToolkitScreens", true,
            description: "Keep the bounty/challenge boards and the map's detail bar inside the UI box (the game's UI aspect option does not box UI Toolkit screens).");
        StretchMismatchedRoots = _cat.CreateEntry("StretchMismatchedRoots", true,
            description: "Screens with a fixed-size root (scribe table, inspect player) are stretched to that root instead of boxed, so their content is not cut off.");
        MenuDpiThreshold = _cat.CreateEntry("MenuDpiThreshold", 100f,
            description: "CanvasScaler.fallbackScreenDPI above this counts as a menu canvas (game: 96 = HUD, 221.5 = menus).");
        AddSettingsRows = _cat.CreateEntry("AddSettingsRows", true,
            description: "Add this mod's rows to Options > Display.");
    }

    public static void Save() => MelonPreferences.Save();

    public static float HudScale => Clamp(HudScalePercent.Value, HudScaleMin, HudScaleMax) / 100f;
    public static float MenuScale => Clamp(MenuScalePercent.Value, HudScaleMin, HudScaleMax) / 100f;
    public static float BoxAspect => Clamp(UiBoxAspect.Value, UiBoxAspectMin, UiBoxAspectMax);
    /// <summary>Margin per side as a fraction (0..0.25).</summary>
    public static float MarginX => Clamp(UiMarginXPercent.Value, MarginMin, MarginMax) / 100f;
    public static float MarginY => Clamp(UiMarginYPercent.Value, MarginMin, MarginMax) / 100f;
    public static bool HasMargins => MarginX > 0f || MarginY > 0f;

    private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
}
