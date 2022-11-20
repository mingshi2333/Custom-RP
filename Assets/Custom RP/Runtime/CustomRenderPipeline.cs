using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline
{
    //实例，在创建的时候自动设置pipelinebatching 
    bool useDynamicBatching, useGPUInstancing,useLightsPerObject;
    ShadowSettings shadowSettings;
    public CustomRenderPipeline(bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher,bool useLightsPerObject, ShadowSettings shadowSettings
    )
    {
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        this.shadowSettings = shadowSettings;
        this.useLightsPerObject = useLightsPerObject;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        GraphicsSettings.lightsUseLinearIntensity = true;//使用颜色线性空间
        InitializeForEditor();
    }
    private CameraRenderer renderer = new CameraRenderer();
    protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
        foreach (var camera in cameras)
        {
            renderer.Render(context,camera,useDynamicBatching,useGPUInstancing,useLightsPerObject,shadowSettings);
            

        }

    }
    

}
