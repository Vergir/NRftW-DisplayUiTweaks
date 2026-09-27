using HarmonyLib;
using UnityEngine.UIElements;

namespace MoreAspectRatios.Patches;

/// <summary>Fits and boxes UI Toolkit screens when they enable.</summary>
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
