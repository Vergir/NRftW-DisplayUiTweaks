using Il2Cpp;
using UnityEngine.Rendering;

namespace DisplayUiTweaks;

/// <summary>Turns off the 16:9 letterbox.</summary>
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
            if (!_warned) DisplayUiTweaksMod.Log.Warning("MoonRenderPipelineAsset not found (currentRenderPipeline is null or another type).");
            _warned = true;
            return;
        }
        if (asset.Enforce169Aspect != MoonRenderPipelineAsset.Enforce169Mode.Never)
        {
            asset.Enforce169Aspect = MoonRenderPipelineAsset.Enforce169Mode.Never;
            DisplayUiTweaksMod.Log.Msg("Enforce169Aspect -> Never");
        }
    }
}
