using Il2CppMoon.Forsaken;
using MelonLoader;
using DisplayUiTweaks;
using UnityEngine;

[assembly: MelonInfo(typeof(DisplayUiTweaksMod), "Display & UI Tweaks", "1.1.0", "vergir")]
[assembly: MelonGame("Moon Studios", "NoRestForTheWicked")]
// Patches are applied in OnInitializeMelon, only when enabled (MelonLoader would otherwise apply them all by itself).
[assembly: HarmonyDontPatchAll]

namespace DisplayUiTweaks;

/// <summary>Entry point. See docs/internal.md.</summary>
public class DisplayUiTweaksMod : MelonMod
{
    public static DisplayUiTweaksMod Instance { get; private set; } = null!;
    public static MelonLogger.Instance Log => Instance.LoggerInstance;

    private int _lastW, _lastH;
    private static string? _pendingReason;
    private static float _applyAt;
    private const float SceneApplyDelay = 0.5f;   // one re-apply per burst of additive scene loads
    private const float SettingApplyDelay = 0.25f; // settings rows: apply once the value stops changing
    private static float _settingApplyAt = -1f;

    public override void OnInitializeMelon()
    {
        Instance = this;
        Prefs.Init();
        if (!Prefs.Enabled.Value)
        {
            LoggerInstance.Msg("Disabled via preferences.");
            return;
        }

        // The game's own debug switches: always show the UI aspect option, with all modes (debug only; UI Area replaces it).
        DisplaySettingsTab.s_forceShowUIAspectSettingForTesting = Prefs.AlwaysShowGameUiAspectOption.Value;
        DisplaySettingsTab.s_forceAllUIAspectModesForTesting = Prefs.AlwaysShowGameUiAspectOption.Value;

        HarmonyInstance.PatchAll(typeof(DisplayUiTweaksMod).Assembly);
        try { if (UIAspectConstraint.s_globalMode == UIAspectMode.Custom) Patches.InitializeUIAspectModesPatch.Migrate(UIAspectMode.Custom); }
        catch (System.Exception e) { LoggerInstance.Warning("UI Area migration: " + e.Message); }
        LoggerInstance.Msg("Patches applied.");

        // After a hot reload: apply to what is on screen, and give the existing settings screens our rows.
        try { UiBox.ReapplyAllConstraints(); ApplyEverything("init"); }
        catch (System.Exception e) { LoggerInstance.Warning("Initial apply: " + e.Message); }
        if (Prefs.AddSettingsRows.Value)
        {
            try { SettingsRows.AddToLiveScreens(); }
            catch (System.Exception e) { LoggerInstance.Warning("Adding rows to live settings screens: " + e.Message); }
        }
    }

    /// <summary>Unload / hot reload: undo what is not a Harmony patch.</summary>
    public override void OnDeinitializeMelon()
    {
        try { Hud.HudEditor.Exit(); Hud.HudLayout.RestoreAll(); Hud.MenuHud.Destroy(); }
        catch (System.Exception e) { LoggerInstance.Warning("HUD layout restore: " + e.Message); }
        try { UiScaling.RestorePanels(); }
        catch (System.Exception e) { LoggerInstance.Warning("RestorePanels: " + e.Message); }
        try { UiToolkitBoxing.RestoreAll(); }
        catch (System.Exception e) { LoggerInstance.Warning("UiToolkitBoxing.RestoreAll: " + e.Message); }
        try { SettingsRows.RemoveAll(); }
        catch (System.Exception e) { LoggerInstance.Warning("SettingsRows.RemoveAll: " + e.Message); }
        try { Patches.UiAspectNote.Restore(); }
        catch (System.Exception e) { LoggerInstance.Warning("UiAspectNote.Restore: " + e.Message); }
        try { UiBox.RestoreFixedRoots(); UiBox.ReapplyAllConstraints(); }
        catch (System.Exception e) { LoggerInstance.Warning("ReapplyAllConstraints: " + e.Message); }
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        if (!Prefs.Enabled.Value) return;
        _pendingReason = "scene " + sceneName;
        _applyAt = Time.unscaledTime + SceneApplyDelay;
    }

    private int _hudErrors;
    private float _hudPausedUntil;

    public override void OnUpdate()
    {
        if (!Prefs.Enabled.Value) return;
        Hud.HudEditor.BlockGameInput();
        try { Hud.HideOutsideCombat.Update(); }
        catch (System.Exception e) { if (_hudErrors++ < 5) LoggerInstance.Warning("Hide HUD Outside Combat: " + e.Message); }
        if (Screen.width != _lastW || Screen.height != _lastH)
        {
            _lastW = Screen.width; _lastH = Screen.height;
            _pendingReason = null;
            ApplyEverything("resolution " + _lastW + "x" + _lastH);
        }
        else if (_pendingReason != null && Time.unscaledTime >= _applyAt)
        {
            string reason = _pendingReason;
            _pendingReason = null;
            ApplyEverything(reason);
        }
        // A changed setting is applied once it has stopped changing and the mouse button is up (a slider drag resizing
        // the settings screen under the cursor would otherwise feed back into the drag).
        if (_settingApplyAt >= 0f && Time.unscaledTime >= _settingApplyAt && !Input.GetMouseButton(0))
        {
            _settingApplyAt = -1f;
            Prefs.Save();
            Prefs.CommitLayoutValues();
            UiBox.ReapplyAllConstraints();   // SetGlobalMode postfix refits the known panels and UI Toolkit screens
        }
    }

    /// <summary>HUD layout and its editor, after the game's own updates.</summary>
    public override void OnLateUpdate()
    {
        if (!Prefs.Enabled.Value || Time.unscaledTime < _hudPausedUntil) return;
        try
        {
            Hud.HudLayout.Tick();
            Hud.HudEditor.Tick();
            DevCommands.Tick();
        }
        catch (System.Exception e)
        {
            // HUDs come and go with loading screens and sessions: pause and retry.
            if (_hudErrors++ < 10) LoggerInstance.Warning("HUD layout: " + e);
            _hudPausedUntil = Time.unscaledTime + 2f;
        }
    }

    /// <summary>Re-applies the non-Harmony changes, looking for newly loaded screens first. Safe to repeat.</summary>
    public static void ApplyEverything(string reason)
    {
        if (!reason.StartsWith("scene")) Log.Msg("Applying (" + reason + ")");
        RenderPipelineTweaks.Apply();
        UiScaling.ApplyPanels(rescan: true);
        UiToolkitBoxing.ApplyAll(rescan: true);
    }

    /// <summary>Called by the settings rows after a value changed. HUD size follows every frame by itself; menu size,
    /// the box and panels are applied once the value has stopped changing and the mouse button is released.</summary>
    public static void OnLayoutPrefChanged()
    {
        _settingApplyAt = Time.unscaledTime + SettingApplyDelay;
    }
}
