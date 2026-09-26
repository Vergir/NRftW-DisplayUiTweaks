using Il2CppMoon.Forsaken;
using MelonLoader;
using MoreAspectRatios;
using UnityEngine;

[assembly: MelonInfo(typeof(MoreAspectRatiosMod), "MoreAspectRatios", "0.3.2", "vergir")]
[assembly: MelonGame("Moon Studios", "NoRestForTheWicked")]

namespace MoreAspectRatios;

/// <summary>
/// No Rest for the Wicked on any aspect ratio:
///  - every display resolution selectable in Options > Display,
///  - the 'UI aspect' option always available with all modes,
///  - no 16:9 letterbox on non-16:9 screens,
///  - menu canvases and UI Toolkit screens (bounty/challenge boards, map details) kept inside the UI box,
///  - HUD & Dialogue UI Size, Menu UI Size, Custom UI Aspect Ratio and 'Box Bounty Boards & Map Details' rows in Options > Display.
/// Successor of the GameAssembly.dll byte patches (see repo README).
/// </summary>
public class MoreAspectRatiosMod : MelonMod
{
    public static MoreAspectRatiosMod Instance { get; private set; } = null!;
    public static MelonLogger.Instance Log => Instance.LoggerInstance;

    private int _lastW, _lastH;
    private static string? _pendingReason;
    private static float _applyAt;
    private const float SceneApplyDelay = 0.5f;   // the world streams many additive scenes; apply once per burst

    public override void OnInitializeMelon()
    {
        Instance = this;
        Prefs.Init();
        if (!Prefs.Enabled.Value)
        {
            LoggerInstance.Msg("Disabled via preferences.");
            return;
        }

        if (Prefs.UnlockUiAspectModes.Value)
        {
            // The game's own debug switches: show the UI aspect setting regardless of monitor aspect, with all modes.
            DisplaySettingsTab.s_forceShowUIAspectSettingForTesting = true;
            DisplaySettingsTab.s_forceAllUIAspectModesForTesting = true;
        }

        HarmonyInstance.PatchAll(typeof(MoreAspectRatiosMod).Assembly);
        LoggerInstance.Msg("Patches applied.");

        // After a hot reload the game is already running: re-apply to what is on screen now.
        try { UiBox.ReapplyAllConstraints(); ApplyEverything("init"); }
        catch (System.Exception e) { LoggerInstance.Warning("Initial apply: " + e.Message); }
        // The game builds its settings screens once per session, so after a hot reload they have to be given our rows here.
        if (Prefs.AddSettingsRows.Value)
        {
            try { SettingsRows.AddToLiveScreens(); }
            catch (System.Exception e) { LoggerInstance.Warning("Adding rows to live settings screens: " + e.Message); }
        }
    }

    /// <summary>Hot reload / unload: give the game back what is not a Harmony patch. The pipeline flag and the debug
    /// switches are left as they are (harmless, and the next build sets them again). UIAspectConstraint layouts are
    /// re-applied by the new build (or by the game's own ApplyConstraint once our prefix is gone).</summary>
    public override void OnDeinitializeMelon()
    {
        try { UiScaling.RestorePanels(); }
        catch (System.Exception e) { LoggerInstance.Warning("RestorePanels: " + e.Message); }
        try { UiToolkitBoxing.RestoreAll(); }
        catch (System.Exception e) { LoggerInstance.Warning("UiToolkitBoxing.RestoreAll: " + e.Message); }
        try { SettingsRows.RemoveAll(); }
        catch (System.Exception e) { LoggerInstance.Warning("SettingsRows.RemoveAll: " + e.Message); }
        try { UiBox.ReapplyAllConstraints(); }
        catch (System.Exception e) { LoggerInstance.Warning("ReapplyAllConstraints: " + e.Message); }
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        if (!Prefs.Enabled.Value) return;
        _pendingReason = "scene " + sceneName;
        _applyAt = Time.unscaledTime + SceneApplyDelay;
    }

    public override void OnUpdate()
    {
        if (!Prefs.Enabled.Value) return;
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
    }

    /// <summary>Everything that is not a Harmony hook: pipeline flag and UI Toolkit panels. Cheap, safe to repeat.</summary>
    public static void ApplyEverything(string reason)
    {
        if (!reason.StartsWith("scene")) Log.Msg("Applying (" + reason + ")");
        RenderPipelineTweaks.Apply();
        UiScaling.ApplyPanels();
        UiToolkitBoxing.ApplyAll();
    }

    /// <summary>Called by the settings sliders after a value changed.</summary>
    public static void OnLayoutPrefChanged()
    {
        Prefs.Save();
        UiBox.ReapplyAllConstraints();   // SetGlobalMode postfix also refits panels and UI Toolkit boxes
        // CanvasScalers pick the new values up on their next Handle() (every frame).
    }
}
