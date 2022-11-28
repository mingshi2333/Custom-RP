using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline
{
    //实例，在创建的时候自动设置pipelinebatching 
    private bool allowHDR;
    bool useDynamicBatching, useGPUInstancing,useLightsPerObject;
    ShadowSettings shadowSettings;
    private PostFXSettings postFXSettings;
    public CustomRenderPipeline(bool allowHDR, bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher,bool useLightsPerObject, ShadowSettings shadowSettings
    ,PostFXSettings postFXSettings)
    {
        this.allowHDR = allowHDR;
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        this.shadowSettings = shadowSettings;
        this.useLightsPerObject = useLightsPerObject;
        this.postFXSettings = postFXSettings;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        GraphicsSettings.lightsUseLinearIntensity = true;//使用颜色线性空间
        InitializeForEditor();
    }
    private CameraRenderer renderer = new CameraRenderer();
    protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
        foreach (var camera in cameras)
        {
            renderer.Render(context,camera,allowHDR, useDynamicBatching,useGPUInstancing,useLightsPerObject,shadowSettings,postFXSettings);
            

        }

    }
    

}
