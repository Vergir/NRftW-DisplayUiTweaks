using HarmonyLib;
using UnityEngine.UIElements;

namespace MoreAspectRatios.Patches;

/// <summary>UI Toolkit screens (map, fast travel, bounty/challenge boards) load their PanelSettings asset with the
/// screen, after our scene-load pass, and rebuild their root element on enable. Fit the panel and box the root then.</summary>
[HarmonyPatch(typeof(UIDocument), "OnEnable")]
internal static class UIDocumentOnEnablePatch
{
    static void Postfix(UIDocument __instance)
    {
        if (!Prefs.Enabled.Value) return;
        try
        {
            var ps = __instance.panelSettings;
            if (ps != null) UiScaling.ApplyPanel(ps);
            UiToolkitBoxing.OnDocumentEnabled(__instance);
        }
        catch (System.Exception e) { MoreAspectRatiosMod.Log.Warning("UIDocument.OnEnable postfix: " + e.Message); }
    }
}
