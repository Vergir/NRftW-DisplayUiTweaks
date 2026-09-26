using Il2CppMoon.Forsaken;
using MelonLoader;
using MoreAspectRatios;
using UnityEngine;

[assembly: MelonInfo(typeof(MoreAspectRatiosMod), "MoreAspectRatios", "0.2.1", "vergir")]
[assembly: MelonGame("Moon Studios", "NoRestForTheWicked")]

namespace MoreAspectRatios;

/// <summary>
/// No Rest for the Wicked on any aspect ratio:
///  - every display resolution selectable in Options > Display,
///  - the 'UI aspect' option always available with all modes,
///  - no 16:9 letterbox on non-16:9 screens,
///  - menu canvases and UI Toolkit panels kept inside the HUD box,
///  - HUD size and HUD box aspect sliders in Options > Display.
/// Successor of the GameAssembly.dll byte patches (see repo README).
/// </summary>
public class MoreAspectRatiosMod : MelonMod
{
    public static MoreAspectRatiosMod Instance { get; private set; } = null!;
    public static MelonLogger.Instance Log => Instance.LoggerInstance;

    private int _lastW, _lastH;

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
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        if (!Prefs.Enabled.Value) return;
        ApplyEverything("scene " + sceneName);
    }

    public override void OnUpdate()
    {
        if (!Prefs.Enabled.Value) return;
        if (Screen.width != _lastW || Screen.height != _lastH)
        {
            _lastW = Screen.width; _lastH = Screen.height;
            ApplyEverything("resolution " + _lastW + "x" + _lastH);
        }
    }

    /// <summary>Everything that is not a Harmony hook: pipeline flag and UI Toolkit panels. Cheap, safe to repeat.</summary>
    public static void ApplyEverything(string reason)
    {
        Log.Msg("Applying (" + reason + ")");
        RenderPipelineTweaks.Apply();
        UiScaling.ApplyPanels();
    }

    /// <summary>Called by the settings sliders after a value changed.</summary>
    public static void OnLayoutPrefChanged()
    {
        Prefs.Save();
        UiBox.ReapplyAllConstraints();
        UiScaling.ApplyPanels();
        // CanvasScalers pick the new values up on their next Handle() (every frame).
    }
}
