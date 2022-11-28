using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public class CustomRenderPipelineAsset:RenderPipelineAsset
{
    [SerializeField] private bool allowHDR = true;
    [SerializeField]
    bool useDynamicBatching = true, useGPUInstancing = true, useSRPBatcher = true,useLightsPerObject = true;

    [SerializeField] private PostFXSettings postFXSettings = default;
    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(allowHDR, useDynamicBatching,useGPUInstancing,useSRPBatcher,useLightsPerObject,shadows,postFXSettings);
    }
    [SerializeField]
    private ShadowSettings shadows = default;
}