using HarmonyLib;
using Il2CppMoon.Forsaken;

namespace DisplayUiTweaks.Hud;

/// <summary>
/// Development only (UserData/DisplayUiTweaks/.dev): a screenshot mode for the Nexus images. F9 or the "showcase on|off"
/// command. While on: realistic chat lines in the chat window (kept visible), the item pickups on screen stay there
/// (their timers are paused), the bounty / challenge panel stays up while it has real rows. Gameplay is untouched.
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
        }
        DisplayUiTweaksMod.Log.Msg("Showcase " + (On ? "on" : "off"));
    }

    /// <summary>The pickups' fade-in, hold and fade-out run in the view's OnUpdate: paused while the showcase is on.</summary>
    [HarmonyPatch(typeof(PlayerNewItemsView), nameof(PlayerNewItemsView.OnUpdate))]
    private static class FreezePickupsPatch
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
