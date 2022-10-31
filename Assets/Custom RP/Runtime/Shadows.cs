using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{
    private const int maxShadowedDirectionalLightCount  = 1;
    private int ShadowedDirectionalLightCount;
    /// <summary>
    /// 方向性的阴影图集，从设置中获取图集大小的整数，然后在命令缓冲区上调用GetTemporaryRT，将纹理标识符作为参数，加上其宽度和高度的像素大小。
    /// </summary>
    private static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");

    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
    }

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
        for (int i = 0; i < ShadowedDirectionalLightCount; i++)
        {
            RenderDirectionalShadows(i,atlasSize);
        }
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }
/// <summary>
/// 一个变体调用RenderDirectionalShadows
/// </summary>
/// <param name="index"></param>
/// <param name="tileSize"></param>
    void RenderDirectionalShadows(int index, int tileSize)
{
    ShadowedDirectionalLight light = ShadowedDirectionalLights[index];
    var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
    cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
        light.visibleLightIndex, 0, 1, Vector3.zero, tileSize, 0f,
        out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
        out ShadowSplitData splitData
    );//2 3 4 参数控制了阴影联机   输出参数 视图矩阵 投影矩阵 shadowsplitdata参数
    shadowSettings.splitData = splitData;
    buffer.SetViewProjectionMatrices(viewMatrix,projectionMatrix);
    ExecuteBuffer();
    context.DrawShadows(ref shadowSettings);
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
    public void ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        //增加条件 如果灯光设置为无阴影 或者强度不大于0 他就该没有阴影  最后一个条件是判别该光是否影响剔除范围内的物体
        if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount&&light.shadows!=LightShadows.None&&light.shadowStrength>0f&&cullingResults.GetShadowCasterBounds(visibleLightIndex,out Bounds b))
        {
            ShadowedDirectionalLights[ShadowedDirectionalLightCount++] = new ShadowedDirectionalLight
                { visibleLightIndex = visibleLightIndex };
        }
    }

}