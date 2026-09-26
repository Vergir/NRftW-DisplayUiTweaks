using Il2Cpp;
using UnityEngine.Rendering;

namespace MoreAspectRatios;

/// <summary>The render pipeline letterboxes the world to 16:9 when MoonRenderPipelineAsset.Enforce169Aspect says so
/// (InBuilds is the shipped value). Setting the asset field to Never removes the black bars.</summary>
internal static class RenderPipelineTweaks
{
    private static bool _warned;

    public static void Apply()
    {
        if (!Prefs.DisableLetterbox.Value) return;
        var rp = GraphicsSettings.currentRenderPipeline;
        var asset = rp == null ? null : rp.TryCast<MoonRenderPipelineAsset>();
        if (asset == null)
        {
            if (!_warned) MoreAspectRatiosMod.Log.Warning("MoonRenderPipelineAsset not found (currentRenderPipeline is null or another type).");
            _warned = true;
            return;
        }
        if (asset.Enforce169Aspect != MoonRenderPipelineAsset.Enforce169Mode.Never)
        {
            asset.Enforce169Aspect = MoonRenderPipelineAsset.Enforce169Mode.Never;
            MoreAspectRatiosMod.Log.Msg("Enforce169Aspect -> Never");
        }
    }
}
