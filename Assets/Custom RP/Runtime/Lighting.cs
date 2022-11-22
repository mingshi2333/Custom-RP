using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class Lighting
{
    private const string bufferName = "Lighting";
    private CommandBuffer buffer = new CommandBuffer() { name = bufferName };
    private const int maxDirLightCount = 4;
    private const int maxOtherLightCount = 64;
/// <summary>
/// directional light parameters
/// </summary>
    private static int dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
        dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
        dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections"),
        dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");

private static Vector4[]
        dirLightColors = new Vector4[maxDirLightCount],
        dirLightDirections = new Vector4[maxDirLightCount],
        dirLightShadowData = new Vector4[maxDirLightCount];

private static string lightsPerObjectKeyword = "_LIGHTS_PER_OBJECT";//perobject关键词
    /// <summary>
    /// other light settings parameters
    /// </summary>
    private static int
        otherLightCountId = Shader.PropertyToID("_OtherLightCount"),
        otherLightColorsId = Shader.PropertyToID("_OtherLightColors"),
        otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions"),
        otherLightDirectionsId = Shader.PropertyToID("_OtherLightDirections"),
        otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles"),
        otherLightShadowDataId = Shader.PropertyToID("_OtherLightShadowData");



    private static Vector4[]
        otherLightColors = new Vector4[maxOtherLightCount],
        otherLightPositions = new Vector4[maxOtherLightCount],
        otherLightDirections = new Vector4[maxOtherLightCount],
        otherLightSpotAngles = new Vector4[maxOtherLightCount],
        otherLightShadowData = new Vector4[maxOtherLightCount];


    private CullingResults cullingResults;//剔除摄像机影响不到的地方
    private Shadows shadows = new Shadows();
    public void Setup(ScriptableRenderContext context,CullingResults cullingResults, ShadowSettings shadowSettings,bool useLightsPerObject)
    {
        this.cullingResults = cullingResults;
        buffer.BeginSample(bufferName);
        //【回顾计算机图形学我们可以知道实时渲染的shadow实际上就是从光线开始渲染的，所以我们需要light所看见的剔除等等】
        
        shadows.Setup(context,cullingResults,shadowSettings);//
        
        //SetupDirectionalLight();
        SetupLights(useLightsPerObject);//useLightsPerObject创建物体的受灯光影响数组
        shadows.Render();
        buffer.EndSample(bufferName);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
    public void Cleanup()
    {
        shadows.Cleanup();
    }
    void SetupLights(bool useLightsPerObject)
    {
        NativeArray<int> indexMap = useLightsPerObject ? cullingResults.GetLightIndexMap(Allocator.Temp) : default;//从剔除的结果中获取光的索引图。
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;//获取可见光源的数组
        int dirLightCount = 0;
        int otherLightCount = 0;
        int i;
        for (i = 0; i < visibleLights.Length; i++)
        {
            int newIndex = -1;//物体的光照索引
            VisibleLight visibleLight = visibleLights[i];
            // SetupDirectionalLight(dirLightCount++,ref visibleLight);//直接引用，节省内存
            // if(dirLightCount>maxDirLightCount)
            //     break;
            switch (visibleLight.lightType)
            {
                case LightType.Directional:
                    if (dirLightCount < maxDirLightCount)
                    {
                        SetupDirectionalLight(dirLightCount++,i,ref visibleLight);
                    }
                    break;
                case LightType.Point:
                    if (otherLightCount < maxOtherLightCount)
                    {
                        newIndex = otherLightCount;
                        SetupPointLight(otherLightCount++,i,ref visibleLight);
                    }
                    break;
                case LightType.Spot:
                    if (otherLightCount < maxOtherLightCount)
                    {
                        newIndex = otherLightCount;
                        SetupSpotLight(otherLightCount++,i,ref visibleLight);
                    }
                    break;
            }

            if (useLightsPerObject)
            {
                indexMap[i] = newIndex;
            }
        }//遍历设置灯光，如果大于就退出
        
        if (useLightsPerObject)
        {
            for (; i < indexMap.Length; i++)
            {
                indexMap[i] = -1;
            }
            cullingResults.SetLightIndexMap(indexMap);
            indexMap.Dispose();
            Shader.EnableKeyword(lightsPerObjectKeyword);
        }//消除所有不可见光的索引
        else
        {
            Shader.DisableKeyword(lightsPerObjectKeyword);
        }
        
        buffer.SetGlobalInt(dirLightCountId, dirLightCount);
        if (dirLightCount > 0)
        {
            buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
            buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);
            buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
        }
        buffer.SetGlobalInt(otherLightCountId,otherLightCount);
        if (otherLightCount > 0)
        {
            buffer.SetGlobalVectorArray(otherLightColorsId,otherLightColors);
            buffer.SetGlobalVectorArray(otherLightPositionsId,otherLightPositions);
            buffer.SetGlobalVectorArray(otherLightDirectionsId,otherLightDirections);//spot light 方向
            buffer.SetGlobalVectorArray(otherLightSpotAnglesId,otherLightSpotAngles);
            buffer.SetGlobalVectorArray(otherLightShadowDataId,otherLightShadowData);
        }
    }    

     
    void SetupDirectionalLight(int index,int visibleIndex, ref VisibleLight visibleLight)
         {
             dirLightColors[index] = visibleLight.finalColor;
             dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);//获取光源的超前方向
             dirLightShadowData[index] = 
                                     shadows.ReserveDirectionalShadows(visibleLight.light,visibleIndex);
     
         }
    void SetupPointLight(int index,int visibleIndex, ref VisibleLight visibleLight)
         {
             otherLightColors[index] = visibleLight.finalColor;
             Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
             position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
             otherLightPositions[index] = position;
             otherLightSpotAngles[index] = new Vector4(0f, 1f);
             
             Light light = visibleLight.light;
             otherLightShadowData[index] = shadows.ReserveOtherShadows(light, visibleIndex);
             
         }
    void SetupSpotLight (int index,int visibleIndex, ref VisibleLight visibleLight) 
        {
            otherLightColors[index] = visibleLight.finalColor;
            Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
            position.w =
                1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
            otherLightPositions[index] = position;
            otherLightDirections[index] =
                -visibleLight.localToWorldMatrix.GetColumn(2);
            Light light = visibleLight.light;
            float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
            float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
            float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
            otherLightSpotAngles[index] = new Vector4(
                angleRangeInv, -outerCos * angleRangeInv
            );
            
            otherLightShadowData[index] = shadows.ReserveOtherShadows(light, visibleIndex);
            
        }

    
}