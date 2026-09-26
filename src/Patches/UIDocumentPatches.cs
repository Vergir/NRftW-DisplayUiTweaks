using HarmonyLib;
using UnityEngine.UIElements;

namespace MoreAspectRatios.Patches;

/// <summary>UI Toolkit screens (map, fast travel, activities/bounties) load their PanelSettings asset with the screen,
/// after our scene-load pass. Fit the panel the moment a document comes up.</summary>
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
        }
        catch (System.Exception e) { MoreAspectRatiosMod.Log.Warning("UIDocument.OnEnable postfix: " + e.Message); }
    }
}
