using UnityEngine;
using UnityEngine.Rendering;

public class CustomRenderPipeline : RenderPipeline
{
    //实例，在创建的时候自动设置pipelinebatching 
    bool useDynamicBatching, useGPUInstancing;
    public CustomRenderPipeline(bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher)
    {
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        GraphicsSettings.lightsUseLinearIntensity = true;//使用颜色线性空间
    }
    private CameraRenderer renderer = new CameraRenderer();
    protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
        foreach (var camera in cameras)
        {
            renderer.Render(context,camera,useDynamicBatching,useGPUInstancing);
            

        }

    }
    

}
