using MelonLoader;

namespace DisplayUiTweaks;

/// <summary>All user-tunable values. Stored in UserData/MelonPreferences.cfg under [DisplayUiTweaks].
/// UI Area, the sizes, Hide HUD Outside Combat and the HUD layout buttons are also rows in Options.</summary>
internal static class Prefs
{
    public const float HudScaleMin = 10f, HudScaleMax = 150f, HudScaleStep = 1f;
    public const float UiAreaMin = 1.0f, UiAreaMax = 4.0f, UiAreaStep = 0.01f;
    public const float GameDefaultUiBoxAspect = 16f / 9f;
    public const float DefaultUiArea = 1.78f;

    private static MelonPreferences_Category _cat = null!;

    public static MelonPreferences_Entry<bool> Enabled = null!;
    public static MelonPreferences_Entry<bool> UnlockResolutions = null!;
    public static MelonPreferences_Entry<bool> AlwaysShowGameUiAspectOption = null!;
    public static MelonPreferences_Entry<bool> DisableLetterbox = null!;
    public static MelonPreferences_Entry<bool> FitMenusToBox = null!;
    public static MelonPreferences_Entry<bool> FitPanelsToBox = null!;
    public static MelonPreferences_Entry<bool> BoxUiToolkitScreens = null!;
    public static MelonPreferences_Entry<bool> BoxHudWidgets = null!;
    public static MelonPreferences_Entry<bool> StretchMismatchedRoots = null!;
    public static MelonPreferences_Entry<float> HudScalePercent = null!;
    public static MelonPreferences_Entry<float> MenuScalePercent = null!;
    public static MelonPreferences_Entry<float> UiArea = null!;
    public static MelonPreferences_Entry<float> UiBoxAspect = null!;
    public static MelonPreferences_Entry<bool> UiAreaMigrated = null!;
    public static MelonPreferences_Entry<int> HideHudOutsideCombat = null!;
    public static MelonPreferences_Entry<float> HideHudDelay = null!;
    public static MelonPreferences_Entry<string> HudLayout = null!;
    public static MelonPreferences_Entry<float> HudMaxDpi = null!;
    public static MelonPreferences_Entry<bool> AddSettingsRows = null!;

    public static void Init()
    {
        _cat = MelonPreferences.CreateCategory("DisplayUiTweaks", "Display & UI Tweaks");

        Enabled = _cat.CreateEntry("Enabled", true, description: "Master switch.");
        UnlockResolutions = _cat.CreateEntry("UnlockResolutions", true,
            description: "List every resolution the display supports in Options > Display (the game hides anything narrower than 16:10 or wider than 32:9).");
        UiArea = _cat.CreateEntry("UiArea", DefaultUiArea,
            description: "UI Area: width / height of the area the whole UI is kept in (1.00-4.00, 1.78 = 16:9). Replaces the game's UI Aspect Mode. Also a slider in Options.");
        HudScalePercent = _cat.CreateEntry("HudScalePercent", 100f,
            description: "In-game HUD, overlays and dialogue (canvases with fallback DPI 96): scale relative to the game's own (10-150). Also a slider in Options.");
        MenuScalePercent = _cat.CreateEntry("MenuScalePercent", 100f,
            description: "Menus (main menu, inventory, stats, map, settings...; fallback DPI above 96 and UI Toolkit panels): scale relative to the game's own, after the fit-to-box cap (10-150). Also a slider in Options.");
        HideHudOutsideCombat = _cat.CreateEntry("HideHudOutsideCombat", 0,
            description: "0 = off, 1 = fade out health, equipment, money, durability, clock and location a few seconds after combat ends, 2 = the same but keep the health bar while not at full health. Also a row in Options.");
        HideHudDelay = _cat.CreateEntry("HideHudDelay", 5f,
            description: "Seconds after combat before the HUD fades out (Hide HUD Outside Combat).");
        HudLayout = _cat.CreateEntry("HudLayout", "",
            description: "HUD elements moved / resized with Edit HUD Layout: Element=x,y,scale; x and y are offsets in parts of the UI box. Empty = the game's layout.");
        DisableLetterbox = _cat.CreateEntry("DisableLetterbox", true,
            description: "Let the world fill the screen on non-16:9 displays (MoonRenderPipelineAsset.Enforce169Aspect = Never).");
        BoxHudWidgets = _cat.CreateEntry("BoxHudWidgets", true,
            description: "Keep the HUD elements the game pins to the screen edges (bounties, loot feed, hints, boss bar, plague meter, area banner) inside the UI box like the rest of the HUD.");
        BoxUiToolkitScreens = _cat.CreateEntry("BoxUiToolkitScreens", true,
            description: "Keep the bounty and challenge boards and the map's detail bar inside the UI box like the other menus (the game only boxes parts of these screens).");
        FitMenusToBox = _cat.CreateEntry("FitMenusToBox", true,
            description: "Scale menu canvases down so their 1920x1080 reference size fits inside the UI box.");
        FitPanelsToBox = _cat.CreateEntry("FitPanelsToBox", true,
            description: "Same for UI Toolkit panels (map, fast travel, bounty and challenge boards).");
        StretchMismatchedRoots = _cat.CreateEntry("StretchMismatchedRoots", true,
            description: "Box screens whose root is not screen-sized by their overlap with the UI box instead of the game's aspect-in-parent rule; fixed-size screens (scribe table, inspect player) keep their layout and shrink only to fit.");
        AlwaysShowGameUiAspectOption = _cat.CreateEntry("AlwaysShowGameUiAspectOption", false,
            description: "Debug: always show the game's own UI Aspect Mode option with all its modes (the game hides it on non-widescreen monitors). Not needed: UI Area replaces it.");
        HudMaxDpi = _cat.CreateEntry("HudMaxDpi", 96f,
            description: "Canvases with CanvasScaler.fallbackScreenDPI up to this are HUD, above it menus (game: 96 = HUD, 100 = main menu, 221.5 = in-game menus).");
        AddSettingsRows = _cat.CreateEntry("AddSettingsRows", true,
            description: "Add this mod's rows to Options (Display tab; Mod Settings Tab moves them to its Mods tab).");
        // Up to 1.0.0: the aspect of the "Custom (Display & UI Tweaks)" UI aspect mode. Read once to carry it over to UI Area.
        UiBoxAspect = _cat.CreateEntry("UiBoxAspect", GameDefaultUiBoxAspect, is_hidden: true);
        UiAreaMigrated = _cat.CreateEntry("UiAreaMigrated", false, is_hidden: true);
        CommitLayoutValues();
    }

    public static void Save() => MelonPreferences.Save();

    public static float HudScale => Clamp(HudScalePercent.Value, HudScaleMin, HudScaleMax) / 100f;
    // Menu UI Size and UI Area resize the settings screen itself. The layout uses these committed copies, which follow
    // the preferences only when the mouse button is up (see DisplayUiTweaksMod.OnUpdate); otherwise dragging those
    // sliders moves the slider under the cursor and the value bounces.
    public static float MenuScale { get; private set; } = 1f;
    /// <summary>Committed UI Area: width / height of the UI box.</summary>
    public static float UiAreaAspect { get; private set; } = DefaultUiArea;

    /// <summary>The preference, valid (a 1.1.0 test build saved 0 for "Off").</summary>
    public static float UiAreaValue => UiArea.Value < UiAreaMin - 0.001f ? DefaultUiArea : Clamp(UiArea.Value, UiAreaMin, UiAreaMax);

    /// <summary>Copy the preference values into the layout. Returns true when something changed.</summary>
    public static bool CommitLayoutValues()
    {
        float menu = Clamp(MenuScalePercent.Value, HudScaleMin, HudScaleMax) / 100f;
        float area = UiAreaValue;
        bool changed = menu != MenuScale || area != UiAreaAspect;
        MenuScale = menu;
        UiAreaAspect = area;
        return changed;
    }

    private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
}
