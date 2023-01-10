using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline
{
    //实例，在创建的时候自动设置pipelinebatching 
    private bool allowHDR;
    bool useDynamicBatching, useGPUInstancing,useLightsPerObject;

    ShadowSettings shadowSettings;
    private PostFXSettings postFXSettings;
    int colorLUTResolution;
    private CameraRenderer renderer;
    public CustomRenderPipeline(bool allowHDR, bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher,bool useLightsPerObject, ShadowSettings shadowSettings
    ,PostFXSettings postFXSettings, int colorLUTResolution,Shader cameraRendererShader)
    {
        this.allowHDR = allowHDR;
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        this.shadowSettings = shadowSettings;
        this.useLightsPerObject = useLightsPerObject;
        this.postFXSettings = postFXSettings;
        this.colorLUTResolution = colorLUTResolution;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        GraphicsSettings.lightsUseLinearIntensity = true;//使用颜色线性空间
        InitializeForEditor();
        renderer = new CameraRenderer(cameraRendererShader);
    }
    protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
        foreach (var camera in cameras)
        {
            renderer.Render(context,camera,allowHDR, useDynamicBatching,useGPUInstancing,useLightsPerObject,shadowSettings,postFXSettings,colorLUTResolution);
            

        }

    }
    

}
