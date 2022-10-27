using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting
{
    private const string bufferName = "Lighting";
    private CommandBuffer buffer = new CommandBuffer() { name = bufferName };
    private const int maxDirLightCount = 4;

    private static int  dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
                        dirLightColorId = Shader.PropertyToID("_DirectionalLightColor"), 
                        dirLightDirectionId = Shader.PropertyToID("_DirectionalLightDirection");

    private static Vector4[]
        dirLightColors = new Vector4[maxDirLightCount],
        dirLightDirections = new Vector4[maxDirLightCount];

    private CullingResults cullingResults;//剔除摄像机影响不到的地方
    public void Setup(ScriptableRenderContext context,CullingResults cullingResults)
    {
        this.cullingResults = cullingResults;
        buffer.BeginSample(bufferName);
        //SetupDirectionalLight();
        SetupLights();
        buffer.EndSample(bufferName);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void SetupLights()
    {
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
    }

    void SetupDirectionalLight()
    {
        Light light = RenderSettings.sun;
        buffer.SetGlobalVector(dirLightColorId,light.color.linear * light.intensity);
        buffer.SetGlobalVector(dirLightDirectionId,-light.transform.forward);

    }
}