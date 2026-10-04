using Il2CppMoon.Forsaken;
using UnityEngine;

namespace DisplayUiTweaks.Hud;

/// <summary>
/// Hide HUD Outside Combat, through the game's own fade system: PlayerUIService.FadeOutHudSpecificElement(flags) ORs
/// flags into this frame's fade flags, and the service's late update fades the matching CanvasGroups out (and back in
/// once the flags stop coming) at its own speed. Called from OnUpdate, before the game's late update reads them.
/// "In combat" = PlayerControllerView.LocalPlayerInCombat. Item pickups, hints, notifications and chat are left alone.
/// </summary>
internal static class HideOutsideCombat
{
    private const PlayerUiFadeFlags Flags = PlayerUiFadeFlags.PlayerEquipment | PlayerUiFadeFlags.PlayerMoney |
                                            PlayerUiFadeFlags.PlayerHealth | PlayerUiFadeFlags.TimeOfDay |
                                            PlayerUiFadeFlags.PlayerLocation | PlayerUiFadeFlags.PlayerJournal |
                                            PlayerUiFadeFlags.PlayerDurability;

    private static float _lastCombat = -999f;

    public static void Update()
    {
        int mode = Prefs.HideHudOutsideCombat.Value;
        if (mode <= 0 || HudEditor.On) return;
        var hud = PlayerUIService.Instance?.PlayerHud;
        var controller = hud != null ? hud.m_context?.ControllerView : null;
        if (controller == null) return;
        if (controller.LocalPlayerInCombat) _lastCombat = Time.unscaledTime;
        if (Time.unscaledTime - _lastCombat <= Mathf.Max(0f, Prefs.HideHudDelay.Value)) return;
        var flags = Flags;
        if (mode == 2 && Hurt(hud!)) flags &= ~PlayerUiFadeFlags.PlayerHealth; // keep the health bar while not at full health
        PlayerUIService.FadeOutHudSpecificElement(flags);
    }

    private static bool Hurt(PlayerHUD hud)
    {
        var hp = hud.PlayerHealthUI;
        return hp != null && hp.m_cachedMaxHealth > 0 && hp.m_cachedCurrentHealth < hp.m_cachedMaxHealth;
    }
}
