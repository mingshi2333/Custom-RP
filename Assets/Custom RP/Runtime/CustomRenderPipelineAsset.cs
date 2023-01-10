using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public partial class CustomRenderPipelineAsset:RenderPipelineAsset
{
    [SerializeField] private bool allowHDR = true;
    [SerializeField]
    bool useDynamicBatching = true, useGPUInstancing = true, useSRPBatcher = true,useLightsPerObject = true;
    public enum ColorLUTResolution { _16 = 16, _32 = 32, _64 = 64 }

    [SerializeField]
    ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;

    [SerializeField] private PostFXSettings postFXSettings = default;
    [SerializeField]
    Shader cameraRendererShader = default;
    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(allowHDR, useDynamicBatching,useGPUInstancing,useSRPBatcher,useLightsPerObject,shadows,postFXSettings,(int)colorLUTResolution,cameraRendererShader);
    }
    [SerializeField]
    private ShadowSettings shadows = default;
}