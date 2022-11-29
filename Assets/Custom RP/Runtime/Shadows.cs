using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{
    private const int maxShadowedDirLightCount = 4, maxShadowedOtherLightCount = 16;
    private const int maxCascades = 4;
    private int ShadowedDirLightCount, shadowedOtherLightCount;
    /// <summary>
    /// 方向性的阴影图集，从设置中获取图集大小的整数，然后在命令缓冲区上调用GetTemporaryRT，将纹理标识符作为参数，加上其宽度和高度的像素大小。
    /// </summary>
    private static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");//阴影绘制图集

    private static int dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices");//着色器阴影矩阵标志
    private static int cascadeCountId = Shader.PropertyToID("_CascadeCount");//联级计数

    private static int cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
        cascadeDataId = Shader.PropertyToID("_CascadeData"),
        shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"),
        otherShadowAtlasId = Shader.PropertyToID("_OtherShadowAtlas"),
        otherShadowMatricesId = Shader.PropertyToID("_OtherShadowMatrices"),
        shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade"),
        shadowPancakingId  = Shader.PropertyToID("_ShadowPancaking");//阴影正交投影和透视切换项

    private static int otherShadowTilesId = Shader.PropertyToID("_OtherShadowTiles");

    private static string[] shadowMaskKeywords = { "_SHADOW_MASK_ALWAYS","_SHADOW_MASK_DISTANCE" };//shadowMask关键字
    private bool useShadowMask;
    private static Matrix4x4[] dirShadowMatrices = new Matrix4x4[maxShadowedDirLightCount *maxCascades];//静态阴影矩阵*联机阴影层数
    private Matrix4x4[] otherShadowMatrices = new Matrix4x4[maxShadowedOtherLightCount];//索引其他灯光阴影图集矩阵
    private static Vector4[] cascadeCullingSpheres = new Vector4[maxCascades];//
    private static Vector4[] cascadeData = new Vector4[maxCascades],
        otherShadowTiles = new Vector4[maxShadowedOtherLightCount];


    private Vector4 atlasSizes;//阴影图集大小，dir 和 other 分别用两个通道存

    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;

    }

    struct ShadowedOtherLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float normalBias;
        public bool isPoint;
    }

    private ShadowedOtherLight[] shadowedOtherLights = new ShadowedOtherLight[maxShadowedOtherLightCount];
    /// <summary>
    /// pcf选择
    /// </summary>
    static string[] directionalFilterKeywords = {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7"
    };

    private static string[] otherFilterKeywords =
    {
        "_OTHER_PCF3",
        "_OTHER_PCF5",
        "_OTHER_PCF7"
    };
    /// <summary>
    /// 阴影选择
    /// </summary>
    static string[] cascadeBlendKeywords = {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER"
    };

    private ShadowedDirectionalLight[] ShadowedDirectionalLights =
        new ShadowedDirectionalLight[maxShadowedDirLightCount ];
    
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
        if (ShadowedDirLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else
        {
            //
            buffer.GetTemporaryRT(dirShadowAtlasId,1,1,32,FilterMode.Bilinear,RenderTextureFormat.Shadowmap);
        }

        if (shadowedOtherLightCount > 0)
        {
            RenderOtherShadows();
        }
        else
        {
            buffer.SetGlobalTexture(otherShadowAtlasId,dirShadowAtlasId);//如果没有，我们使用一个假的纹理
        }
        
        buffer.BeginSample(bufferName);
        SetKeywords(shadowMaskKeywords,useShadowMask? QualitySettings.shadowmaskMode==ShadowmaskMode.Shadowmask?0:1:-1);//启用关键词
        //SetKeywords(shadowMaskKeywords,useShadowMask?0:-1);//启用关键词
        buffer.SetGlobalInt(
            cascadeCountId,
            ShadowedDirLightCount > 0 ? settings.directional.cascadeCount : 0
        );
        float f = 1f - settings.directional.cascadeFade;//保证比例相同
        buffer.SetGlobalVector(
            shadowDistanceFadeId,new Vector4(
                1/settings.maxDistance,1f/settings.distanceFade,
                1f/(1f-f*f)
            )
        );
        buffer.SetGlobalVector(shadowAtlasSizeId,atlasSizes);
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }
/// <summary>
/// 渲染直射光
/// </summary>
    void RenderDirectionalShadows()
    {
        int atlasSize = (int)settings.directional.atlasSize;
        atlasSizes.x = atlasSize;
        atlasSizes.y = 1f / atlasSize;
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
        buffer.SetGlobalFloat(shadowPancakingId,1f);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();
        int tiles = ShadowedDirLightCount * settings.directional.cascadeCount;//所有的tile数目
        int split = tiles <= 1 ? 1 :tiles<4?2:4;//灯的总数大于1就开始分
        int tileSize = atlasSize / split;
        
        for (int i = 0; i < ShadowedDirLightCount; i++)
        {
            RenderDirectionalShadows(i,split,tileSize);
        }
        //buffer.SetGlobalFloat(shadowDistanceId, settings.maxDistance);//传递最大阴影采样距离
        // float f = 1f - settings.directional.cascadeFade;//保证比例相同
        // buffer.SetGlobalVector(
        //     shadowDistanceFadeId,new Vector4(
        //         1/settings.maxDistance,1f/settings.distanceFade,
        //         1f/(1f-f*f)
        //         )
        //     );
        //SetKeywords();
        SetKeywords(directionalFilterKeywords,(int)settings.directional.filter-1);
        SetKeywords(cascadeBlendKeywords,(int)settings.directional.cascadeBlend-1);
        
        // buffer.SetGlobalVector(
        //     shadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize));
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

void RenderOtherShadows()
    {
        int atlasSize = (int)settings.other.atlasSize;
        atlasSizes.z = atlasSize;
        atlasSizes.w = 1f / atlasSize;
        // ReSharper disable once InvalidXmlDocComment
        ///默认是一张argb纹理，我们需要一个阴影贴图，所以我们需要额外的参数来指定他。首先是深度缓冲区的位数。我们希望这个位数越高越好，
        /// 所以我们使用32。第二是过滤模式，我们使用默认的双线性过滤。第三是渲染纹理类型，它必须是RenderTextureFormat.Shadowmap。
        /// 它通常是一个24或32比特的整数或浮点纹理。你也可以选择16比特，Unity的RP就是这样做的。
        /// 请求渲染纹理
        /// 
        buffer.GetTemporaryRT(otherShadowAtlasId,atlasSize,atlasSize,32,FilterMode.Bilinear,RenderTextureFormat.Shadowmap);
        //指示零时纹理如何渲染，我们不管他加载到什么位置，因为我们单纯要用，要他储存的阴影数据
        buffer.SetRenderTarget(
            otherShadowAtlasId,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
        );
        buffer.ClearRenderTarget(true,false,Color.clear);
        buffer.SetGlobalFloat(shadowPancakingId, 0f);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();
        int tiles = shadowedOtherLightCount;//所有的tile数目
        int split = tiles <= 1 ? 1 :tiles<4?2:4;//灯的总数大于1就开始分
        int tileSize = atlasSize / split;
        
        // for (int i = 0; i < shadowedOtherLightCount; i++)
        // {
        //     RenderSpotShadows(i, split, tileSize);
        // }
        for (int i = 0; i < shadowedOtherLightCount;)
        {
            if (shadowedOtherLights[i].isPoint)
            {
                RenderPointShadows(i,split,tileSize);
                i += 6;
            }
            else
            {
                RenderSpotShadows(i,split,tileSize);
                i += 1;
            }
        }

        buffer.SetGlobalMatrixArray(otherShadowMatricesId, otherShadowMatrices);
        buffer.SetGlobalVectorArray(otherShadowTilesId,otherShadowTiles);
        SetKeywords(
            otherFilterKeywords, (int)settings.other.filter - 1
        );
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
    float tileScale = 1f / split;
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
            ConvertToAtlasMatrix(projectionMatrix * viewMatrix, SetTileViewport(tileIndex, split, tileSize), tileScale); //
        
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
        if (shadowedOtherLightCount > 0)
        {
            buffer.ReleaseTemporaryRT(otherShadowAtlasId);//阴影的话就释放假阴影
        }
        ExecuteBuffer();
    }
    public void Setup(
        ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings settings
    )
    {
        this.context = context;
        this.cullingResults = cullingResults;
        this.settings = settings;
        ShadowedDirLightCount = shadowedOtherLightCount = 0;//设置环节把计数设置为0
        
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
        if (ShadowedDirLightCount < maxShadowedDirLightCount &&light.shadows!=LightShadows.None&&light.shadowStrength>0f)
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
            
            ShadowedDirectionalLights[ShadowedDirLightCount] = new ShadowedDirectionalLight
                { visibleLightIndex = visibleLightIndex ,slopeScaleBias = light.shadowBias,nearPlaneOffset = light.shadowNearPlane};
            return new Vector4(light.shadowStrength,settings.directional.cascadeCount*ShadowedDirLightCount++,light.shadowNormalBias,maskChannel);
        }
        return new Vector4(0f,0f,0f,-1f);
    }
    /// <summary>
    /// 重构后，不会为淘汰后的光保留阴影，单如果是烘培的，我们可以允许他们，返回阴影强度和通道
    /// </summary>
    /// <param name="light"></param>
    /// <param name="visibleLightIndex"></param>
    /// <returns></returns>
    public Vector4 ReserveOtherShadows (Light light, int visibleLightIndex) 
    {
       // if (light.shadows != LightShadows.None && light.shadowStrength > 0f) {
       if (light.shadows == LightShadows.None || light.shadowStrength <= 0)
       {
           return new Vector4(0f, 0f, 0f, -1f);
       }

       float maskChannel = -1f;
       LightBakingOutput lightBaking = light.bakingOutput;
       if (
           lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
           lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
       )
       {
           useShadowMask = true;
           maskChannel = lightBaking.occlusionMaskChannel;
       }
       //返回一个负的阴影强度和遮罩通道，以便在适当的时候使用烘培阴影
       bool isPoint = light.type == LightType.Point;
       
       int newLightCount = shadowedOtherLightCount + (isPoint ? 6 : 1);
       if (
           newLightCount > maxShadowedOtherLightCount ||
           !cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
       {
           return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
       }

       shadowedOtherLights[shadowedOtherLightCount] = new ShadowedOtherLight
       {
           visibleLightIndex = visibleLightIndex,
           slopeScaleBias = light.shadowBias,
           normalBias = light.shadowNormalBias,
           isPoint = isPoint
       };
       Vector4 data = new Vector4(light.shadowStrength, shadowedOtherLightCount, isPoint ? 1f : 0f, maskChannel);
       shadowedOtherLightCount = newLightCount;
       return data;

    }

    void RenderSpotShadows (int index, int split, int tileSize) {
        ShadowedOtherLight light = shadowedOtherLights[index];
        var shadowSettings =
            new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
        cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(
            light.visibleLightIndex, out Matrix4x4 viewMatrix,
            out Matrix4x4 projectionMatrix, out ShadowSplitData splitData
        );
        shadowSettings.splitData = splitData;
        
        float texelSize = 2f / (tileSize * projectionMatrix.m00);
        float filterSize = texelSize * ((float)settings.other.filter + 1f);
        float bias = light.normalBias * filterSize * 1.4142136f;
        float tileScale = 1f / split;
        Vector2 offset = SetTileViewport(index, split, tileSize);
        SetOtherTileData(index,offset,1/split,bias);
        otherShadowMatrices[index] = ConvertToAtlasMatrix(
            projectionMatrix * viewMatrix,
            offset, tileScale
        );
        buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
        buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
        ExecuteBuffer();
        context.DrawShadows(ref shadowSettings);
        buffer.SetGlobalDepthBias(0f, 0f);
    }
    
    void RenderPointShadows (int index, int split, int tileSize) {
        ShadowedOtherLight light = shadowedOtherLights[index];
        var shadowSettings =
            new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
        
        float texelSize = 2f / tileSize;
        float filterSize = texelSize * ((float)settings.other.filter + 1f);
        float bias = light.normalBias * filterSize * 1.4142136f;
        float tileScale = 1f / split;
        float fovBias =
            Mathf.Atan(1f + bias + filterSize) * Mathf.Rad2Deg * 2f - 90f;
        for (int i = 0; i < 6; i++) {
            cullingResults.ComputePointShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, (CubemapFace)i, fovBias,
                out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
                out ShadowSplitData splitData
            );
            viewMatrix.m11 = -viewMatrix.m11;
            viewMatrix.m12 = -viewMatrix.m12;
            viewMatrix.m13 = -viewMatrix.m13;
            shadowSettings.splitData = splitData;
            int tileIndex = index + i;
            // float texelSize = 2f / (tileSize * projectionMatrix.m00);
            // float filterSize = texelSize * ((float)settings.other.filter + 1f);
            // float bias = light.normalBias * filterSize * 1.4142136f;
            
            Vector2 offset = SetTileViewport(tileIndex, split, tileSize);
            
            // float tileScale = 1f / split;
            SetOtherTileData(tileIndex, offset, tileScale, bias);
            otherShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
                projectionMatrix * viewMatrix, offset, tileScale
            );

            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();
            context.DrawShadows(ref shadowSettings);
            buffer.SetGlobalDepthBias(0f, 0f);
        }
    }
    
    void SetOtherTileData (int index,Vector2 offset,float scale, float bias)
    {
        float border = atlasSizes.w * 0.5f;
        Vector4 data;
        data.x = offset.x * scale + border;
        data.y = offset.y * scale + border;
        data.z = scale - border - border;
        data.w = bias;
        otherShadowTiles[index] = data;
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

Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, float tileScale)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 *= -1;
            m.m21 *= -1;
            m.m22 *= -1;
            m.m23 *= -1;
            //float scale = 1f / split;
            m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * tileScale;
            m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * tileScale;
            m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * tileScale;
            m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * tileScale;
            m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * tileScale;
            m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * tileScale;
            m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * tileScale;
            m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * tileScale;
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