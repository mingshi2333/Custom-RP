using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{
    private const int maxShadowedDirectionalLightCount  = 4;
    private const int maxCascades = 4;
    private int ShadowedDirectionalLightCount;
    /// <summary>
    /// 方向性的阴影图集，从设置中获取图集大小的整数，然后在命令缓冲区上调用GetTemporaryRT，将纹理标识符作为参数，加上其宽度和高度的像素大小。
    /// </summary>
    private static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");//阴影绘制图集

    private static int dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices");//着色器阴影矩阵标志
    private static int cascadeCountId = Shader.PropertyToID("_CascadeCount");//联级计数

    private static int cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
        cascadeDataId = Shader.PropertyToID("_CascadeData"),
        shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize");
    private static int shadowDistanceId = Shader.PropertyToID("_ShadowDistance"),
        shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");

    private static string[] shadowMaskKeywords = { "_SHADOW_MASK_ALWAYS","_SHADOW_MASK_DISTANCE" };//shadowMask关键字
    private bool useShadowMask;
    private static Matrix4x4[] dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount*maxCascades];//静态阴影矩阵*联机阴影层数
    private static Vector4[] cascadeCullingSpheres = new Vector4[maxCascades];//
    private static Vector4[] cascadeData = new Vector4[maxCascades];

    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
        
    }
    /// <summary>
    /// pcf选择
    /// </summary>
    static string[] directionalFilterKeywords = {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7",
    };
    /// <summary>
    /// 阴影选择
    /// </summary>
    static string[] cascadeBlendKeywords = {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER"
    };

    private ShadowedDirectionalLight[] ShadowedDirectionalLights =
        new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];
    
    private const string bufferName = "Shadows";
    private CommandBuffer buffer = new CommandBuffer { name = bufferName };
    
    private ScriptableRenderContext context;
    private CullingResults cullingResults;
    private ShadowSettings settings;
/// <summary>
/// 渲染所有阴影，不声称一个纹理将导只webgl2.0的问题，因为纹理和采样器绑定在一起，
/// 不声称一个纹理会导致得到一个默认纹理，这个纹理不会与阴影采样器兼容
/// 所以方法是生成一个1x1的假纹理。当然也可以引入shader关键字来避免
/// </summary>
    public void Render()
    {
        if (ShadowedDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else
        {
            //
            buffer.GetTemporaryRT(dirShadowAtlasId,1,1,32,FilterMode.Bilinear,RenderTextureFormat.Shadowmap);
        }
        
        buffer.BeginSample(bufferName);
        SetKeywords(shadowMaskKeywords,useShadowMask? QualitySettings.shadowmaskMode==ShadowmaskMode.Shadowmask?0:1:-1);//启用关键词
        //SetKeywords(shadowMaskKeywords,useShadowMask?0:-1);//启用关键词
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }
/// <summary>
/// 渲染直射光
/// </summary>
    void RenderDirectionalShadows()
    {
        int atlasSize = (int)settings.directional.atlasSize;
        // ReSharper disable once InvalidXmlDocComment
        ///默认是一张argb纹理，我们需要一个阴影贴图，所以我们需要额外的参数来指定他。首先是深度缓冲区的位数。我们希望这个位数越高越好，
        /// 所以我们使用32。第二是过滤模式，我们使用默认的双线性过滤。第三是渲染纹理类型，它必须是RenderTextureFormat.Shadowmap。
        /// 它通常是一个24或32比特的整数或浮点纹理。你也可以选择16比特，Unity的RP就是这样做的。
        /// 请求渲染纹理
        /// 
        buffer.GetTemporaryRT(dirShadowAtlasId,atlasSize,atlasSize,32,FilterMode.Bilinear,RenderTextureFormat.Shadowmap);
        //指示零时纹理如何渲染，我们不管他加载到什么位置，因为我们单纯要用，要他储存的阴影数据
        buffer.SetRenderTarget(
            dirShadowAtlasId,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
        );
        buffer.ClearRenderTarget(true,false,Color.clear);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();
        int tiles = ShadowedDirectionalLightCount * settings.directional.cascadeCount;//所有的tile数目
        int split = tiles <= 1 ? 1 :tiles<4?2:4;//灯的总数大于1就开始分
        int tileSize = atlasSize / split;
        
        for (int i = 0; i < ShadowedDirectionalLightCount; i++)
        {
            RenderDirectionalShadows(i,split,tileSize);
        }
        //buffer.SetGlobalFloat(shadowDistanceId, settings.maxDistance);//传递最大阴影采样距离
        float f = 1f - settings.directional.cascadeFade;//保证比例相同
        buffer.SetGlobalVector(shadowDistanceFadeId,new Vector4(1/settings.maxDistance,1f/settings.distanceFade,1f/(1f-f*f)));
        //SetKeywords();
        SetKeywords(directionalFilterKeywords,(int)settings.directional.filter-1);
        SetKeywords(cascadeBlendKeywords,(int)settings.directional.cascadeBlend-1);
        
        buffer.SetGlobalVector(
            shadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize));
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }
/// <summary>
/// 一个变体调用RenderDirectionalShadows
/// </summary>
/// <param name="index"></param>
/// <param name="tileSize"></param>
    void RenderDirectionalShadows(int index, int split,int tileSize)
{
    ShadowedDirectionalLight light = ShadowedDirectionalLights[index];
    var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
    
    int cascadeCount = settings.directional.cascadeCount;
    int tileOffset = index * cascadeCount;
    Vector3 ratios = settings.directional.CascadeRatios;
    
    float cullingFactor =
        Mathf.Max(0f, 0.8f - settings.directional.cascadeFade);
    
    for (int i = 0; i < cascadeCount; i++)
    {
        cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
            light.visibleLightIndex, i, cascadeCount, ratios, tileSize, light.nearPlaneOffset,
            out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
            out ShadowSplitData splitData
        ); //2 3 4 参数控制了阴影联机   输出参数 视图矩阵 投影矩阵 shadowsplitdata参数
        if (index == 0)
        {
            SetCascadeData(i, splitData.cullingSphere, tileSize);
        }

        splitData.shadowCascadeBlendCullingFactor = cullingFactor;//[这个参数是0-1] 0值为不剔除，大点剔除多，如果可以保证它们的结果总是被较小的级联覆盖，那么尝试从较大的级联中剔除一些阴影投射是有意义的。
        shadowSettings.splitData = splitData;
        int tileIndex = tileOffset + i;

        // SetTileViewport(index,split,tileSize);

        dirShadowMatrices[tileIndex] =
            ConvertToAtlasMatrix(projectionMatrix * viewMatrix, SetTileViewport(tileIndex, split, tileSize), split); //
        
        buffer.SetGlobalInt(cascadeCountId, settings.directional.cascadeCount);
        buffer.SetGlobalVectorArray(
            cascadeCullingSpheresId, cascadeCullingSpheres
        );
        buffer.SetGlobalVectorArray(cascadeDataId,cascadeData);
        
        buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
        buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
        //buffer.SetGlobalDepthBias(0f,3f);//bias 是一个非常小的数字的倍数， slopeBias是梯度
        buffer.SetGlobalDepthBias(0f,light.slopeScaleBias);
        ExecuteBuffer();
        context.DrawShadows(ref shadowSettings);
        buffer.SetGlobalDepthBias(0f, 0f);
    }
}
/// <summary>
/// 得到临时纹理后，我们需要保留他知道我们的摄像机完成渲染
/// 通过调用ReleaseTemporaryRT与缓冲区的纹理标识符来释放它
/// </summary>
    public void Cleanup()
    {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        ExecuteBuffer();
    }
    public void Setup(
        ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings settings
    )
    {
        this.context = context;
        this.cullingResults = cullingResults;
        this.settings = settings;
        ShadowedDirectionalLightCount = 0;//设置环节把计数设置为0
        
        //shadowmask
        useShadowMask = false;

    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
    /// <summary>
    /// 【该函数是为了储存灯光的阴影图，所以他的参数有灯光信息以及灯光的索引】
    /// </summary>
    /// <param name="light"></param>
    /// <param name="visibleLightIndex"></param>
    public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        //增加条件 如果灯光设置为无阴影 或者强度不大于0 他就该没有阴影  最后一个条件是判别该光是否影响剔除范围内的物体
        //修改:在没有realtimeshadow的时候也启动shadowmask，方便代替
        //if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount&&light.shadows!=LightShadows.None&&light.shadowStrength>0f&&cullingResults.GetShadowCasterBounds(visibleLightIndex,out Bounds b))
        if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount&&light.shadows!=LightShadows.None&&light.shadowStrength>0f)
        {
            float maskChannel = -1;
            LightBakingOutput lightBaking = light.bakingOutput;
            if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
                lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
            {
                useShadowMask = true;
                maskChannel = lightBaking.occlusionMaskChannel;
            }
            //shadowmask代替条件
            if (!cullingResults.GetShadowCasterBounds(
                    visibleLightIndex, out Bounds b
                )) {
                return new Vector4(-light.shadowStrength, 0f, 0f,maskChannel);//我们的shadow对阴影贴图采样是强度大于0，这里不让他工作直接设置为负值
            }
            
            ShadowedDirectionalLights[ShadowedDirectionalLightCount] = new ShadowedDirectionalLight
                { visibleLightIndex = visibleLightIndex ,slopeScaleBias = light.shadowBias,nearPlaneOffset = light.shadowNearPlane};
            return new Vector4(light.shadowStrength,settings.directional.cascadeCount*ShadowedDirectionalLightCount++,light.shadowNormalBias,maskChannel);
        }
        return new Vector4(0f,0f,0f,-1f);
    }
    public Vector4 ReserveOtherShadows (Light light, int visibleLightIndex) {
        if (light.shadows != LightShadows.None && light.shadowStrength > 0f) {
            LightBakingOutput lightBaking = light.bakingOutput;
            if (
                lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
                lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
            ) {
                useShadowMask = true;
                return new Vector4(
                    light.shadowStrength, 0f, 0f,
                    lightBaking.occlusionMaskChannel
                );
            }
        }
        return new Vector4(0f, 0f, 0f, -1f);
    }
    
    
/// <summary>
/// 我们可以调整渲染视口到单个tile，计算偏移量，然后计算视口
/// </summary>
/// <param name="index"></param>
/// <param name="split"></param>
/// <param name="tileSize"></param>
    Vector2 SetTileViewport(int index, int split,float tileSize)
    {
        //    ____
        //   |2|3|
        //   |0|1|
        //   
        Vector2 offset = new Vector2(index % split, index / split);//fun code 
        buffer.SetViewport(new Rect(offset.x * tileSize,offset.y*tileSize,tileSize,tileSize));
        return offset;
    }

    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 *= -1;
            m.m21 *= -1;
            m.m22 *= -1;
            m.m23 *= -1;
            float scale = 1f / split;
            m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
            m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
            m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
            m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
            m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
            m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
            m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
            m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
            m.m20 = 0.5f * (m.m20 + m.m30);
            m.m21 = 0.5f * (m.m21 + m.m31);
            m.m22 = 0.5f * (m.m22 + m.m32);
            m.m23 = 0.5f * (m.m23 + m.m33);
        }
        return m;
    }
/// <summary>
/// 在第一次遍历的时候就设置好的参数
/// </summary>
/// <param name="index"></param>
/// <param name="cullingSphere"></param>
/// <param name="tileSize"></param>
    void SetCascadeData(int index, Vector4 cullingSphere, float tileSize)
    {
        float texelSize = 2f * cullingSphere.w / tileSize;//单个纹理大小的偏移量，不过最坏的情况是偏移一个正方形像素的对角线√2.
        float filterSize = texelSize * ((float)settings.directional.filter + 1f);
        cullingSphere.w -= filterSize;
        cullingSphere.w *= cullingSphere.w;
        cascadeCullingSpheres[index] = cullingSphere;
        cascadeData[index] = new Vector4(
            1f / cullingSphere.w,
            filterSize* 1.4142136f
        );
    }
/// <summary>
/// 为buffer设置keyword
/// </summary>
/// <param name="keywords"></param>
/// <param name="enabledIndex"></param>
    void SetKeywords (string[] keywords,int enabledIndex) {
        //int enabledIndex = (int)settings.directional.filter - 1;//枚举序号，序号从-1开始
        //Debug.Log(enabledIndex); 从-1是默认值
        for (int i = 0; i < keywords.Length; i++) {
            if (i == enabledIndex) {
                buffer.EnableShaderKeyword(keywords[i]);
            }
            else {
                buffer.DisableShaderKeyword(keywords[i]);
            }
        }
    }


}