using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting
{
    private const string bufferName = "Lighting";
    private CommandBuffer buffer = new CommandBuffer() { name = bufferName };
    private const int maxDirLightCount = 4;

    private static int  dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
                        dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"), 
                        dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections");

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
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;//获取可见光源的数组
        int dirLightCount = 0;
        for (int i = 0; i < visibleLights.Length; i++)
        {
            VisibleLight visibleLight = visibleLights[i];
            SetupDirectionalLight(dirLightCount++,ref visibleLight);//直接引用，节省内存
            if(dirLightCount>maxDirLightCount)
                break;
        }//遍历设置灯光，如果大于就退出
        buffer.SetGlobalInt(dirLightCountId, visibleLights.Length);
        buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
        buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);
    }

    void SetupDirectionalLight(int index,ref VisibleLight visibleLight)
    {
        dirLightColors[index] = visibleLight.finalColor;
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);//获取光源的超前方向

    }
}