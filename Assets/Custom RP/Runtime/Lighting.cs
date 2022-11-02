using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class Lighting
{
    private const string bufferName = "Lighting";
    private CommandBuffer buffer = new CommandBuffer() { name = bufferName };
    private const int maxDirLightCount = 4;

    private static int dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
        dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
        dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections"),
        dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");

    private static Vector4[]
        dirLightColors = new Vector4[maxDirLightCount],
        dirLightDirections = new Vector4[maxDirLightCount],
        dirLightShadowData = new Vector4[maxDirLightCount];

    private CullingResults cullingResults;//剔除摄像机影响不到的地方
    private Shadows shadows = new Shadows();
    public void Setup(ScriptableRenderContext context,CullingResults cullingResults,ShadowSettings shadowSettings)
    {
        this.cullingResults = cullingResults;
        buffer.BeginSample(bufferName);
        //【回顾计算机图形学我们可以知道实时渲染的shadow实际上就是从光线开始渲染的，所以我们需要light所看见的剔除等等】
        
        shadows.Setup(context,cullingResults,shadowSettings);//
        
        //SetupDirectionalLight();
        SetupLights();
        shadows.Render();
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
        
        buffer.SetGlobalVectorArray(dirLightShadowDataId,dirLightShadowData);
    }

    void SetupDirectionalLight(int index,ref VisibleLight visibleLight)
    {
        dirLightColors[index] = visibleLight.finalColor;
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);//获取光源的超前方向
        dirLightShadowData[index] = 
                                shadows.ReserveDirectionalShadows(visibleLight.light,index);

    }
    public void Cleanup()
    {
        shadows.Cleanup();
    }
}