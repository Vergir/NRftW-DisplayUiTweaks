using HarmonyLib;
using Il2CppMoon.Forsaken;

namespace DisplayUiTweaks.Hud;

/// <summary>
/// Development only (UserData/DisplayUiTweaks/.dev): a screenshot mode for the Nexus images. F9 or the "showcase on|off"
/// command. While on: realistic chat lines in the chat window (kept visible), every item pickup gets a static copy that stays
/// (HudSamples.KeepPickups), the bounty / challenge panel is frozen as it is on screen (wait for it to show, then press
/// F9). Gameplay is untouched.
/// </summary>
internal static class Showcase
{
    public static bool On { get; private set; }

    public static void Toggle() => Set(!On);

    public static void Set(bool on)
    {
        var hud = HudLayout.LiveHud;
        if (on == On) return;
        if (on)
        {
            if (hud == null) { DisplayUiTweaksMod.Log.Msg("Showcase: no live HUD"); return; }
            if (HudEditor.On) { DisplayUiTweaksMod.Log.Msg("Showcase: close the editor first"); return; }
            HudSamples.BeginShowcase(hud);
            On = true;
        }
        else
        {
            On = false;
            HudSamples.End(hud);
            if (hud != null && hud.ActivitiesHUD != null) hud.ActivitiesHUD.HideActivitiesLog();
        }
        DisplayUiTweaksMod.Log.Msg("Showcase " + (On ? "on" : "off"));
    }

    /// <summary>The bounty / challenge panel hides itself a few seconds after it was shown: not while the showcase is on.</summary>
    [HarmonyPatch(typeof(PlayerActivitiesHUD), nameof(PlayerActivitiesHUD.HideActivitiesLog))]
    private static class KeepActivitiesPatch
    {
        private static bool Prefix() => !On;
    }

    [HarmonyPatch(typeof(PlayerActivitiesHUD), nameof(PlayerActivitiesHUD.Update))]
    private static class FreezeActivitiesPatch
    {
        private static bool Prefix() => !On;
    }

    /// <summary>After the game's own fades (the chat fades out when idle).</summary>
    [HarmonyPatch(typeof(PlayerUIService), nameof(PlayerUIService.OnLateUpdate))]
    private static class KeepVisiblePatch
    {
        private static void Postfix()
        {
            if (On && HudLayout.LiveHud != null) HudSamples.ShowcaseVisible(HudLayout.LiveHud);
        }
    }
}
