using System;
using UnityEngine;
using UnityEngine.Rendering;
public partial class CameraRenderer
{
    private ScriptableRenderContext context;
    private Camera camera;
    private const string bufferName = "Render Camera";//buffer名称，方便在帧缓存中显示名称
    private CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    private bool useHDR;
    private CullingResults cullingResults;
    private static ShaderTagId unlitShadeerTagId = new ShaderTagId("SRPDefaultUnlit");

    private static ShaderTagId litShaderTagId = new ShaderTagId("CustomLit");//添加我们自己的tag

    private Lighting lighting = new Lighting();
    private PostFXStack postFXStack = new PostFXStack();
    //static int frameBufferId = Shader.PropertyToID("_CameraFrameBuffer");//后处理源纹理深度合并的一个缓冲区

    private static int
        colorAttachmentId = Shader.PropertyToID("_CameraColorAttachment"),
        depthAttachmentId = Shader.PropertyToID("_CameraDepthAttachment");//拆分位颜色盒深度信息

    private static int
        depthTextureId = Shader.PropertyToID("_CameraDepthTexture"),
        sourceTextureId = Shader.PropertyToID("_SourceTexture");
        
    private bool useDepthTexture;//是否启用深度纹理
    private bool useIntermediateBuffer;//
    static CameraSettings defaultCameraSettings = new CameraSettings();
    private Material material;

    public CameraRenderer(Shader shader)
    {
        material = CoreUtils.CreateEngineMaterial(shader);
    }
    public void Dispose () {
        CoreUtils.Destroy(material);
    }
    void Draw (RenderTargetIdentifier from, RenderTargetIdentifier to) {
        buffer.SetGlobalTexture(sourceTextureId, from);
        buffer.SetRenderTarget(
            to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
        );
        buffer.DrawProcedural(
            Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3
        );
    }
    public void Render(ScriptableRenderContext context, Camera camera,
        bool allowHDR, bool useDynamicBatching, bool useGPUInstancing,bool useLightsPerObject, ShadowSettings shadowSettings, PostFXSettings postFXSettings,int colorLUTResolution)
    {
        this.context = context;
        this.camera = camera;
        var crpCamera = camera.GetComponent<CustomRenderPipelineCamera>();
        CameraSettings cameraSettings =
            crpCamera ? crpCamera.Settings : defaultCameraSettings;
        useDepthTexture = true;
        
        if (cameraSettings.overridePostFX) {
            postFXSettings = cameraSettings.postFXSettings;
        }//每个相机覆盖后处理设置
        PrepareBuffer();
        PrepareForSceneWindow();
        if (!Cull(shadowSettings.maxDistance))
        {
            return;
        }

        useHDR = allowHDR && camera.allowHDR;
        /*Debug.Log(useHDR);*/
        //Setup();
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
        lighting.Setup(context,cullingResults,shadowSettings,useLightsPerObject,cameraSettings.maskLights ? cameraSettings.renderingLayerMask : -1);
        postFXStack.Setup(context,camera,postFXSettings,useHDR,colorLUTResolution,cameraSettings.finalBlendMode);
        buffer.EndSample(SampleName);
        Setup();
        DrawVisibleGeometry(useDynamicBatching,useGPUInstancing,useLightsPerObject,cameraSettings.renderingLayerMask);
        DrawUnsupportedShaders();
        //DrawGizmos();
        DrawGizmosBeforeFX();
        if (postFXStack.IsActive)
        {
            postFXStack.Render(colorAttachmentId);
        }
        else if (useIntermediateBuffer) {
            Draw(colorAttachmentId, BuiltinRenderTextureType.CameraTarget);
            ExecuteBuffer();
        }
        DrawGizmosAfterFX();
        Cleanup();
        //在提交之前清理生成的shadowmap
        Submit();
    }

    void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing,bool useLightsPerObject,int renderingLayerMask)
    {
        PerObjectData lightsPerObjectFlags =
            useLightsPerObject ? PerObjectData.LightData | PerObjectData.LightIndices : PerObjectData.None;
        //不透明物体
        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };//基于距离的排序
        var drawingSettings = new DrawingSettings(unlitShadeerTagId,sortingSettings)
        {
            enableDynamicBatching = useDynamicBatching,enableInstancing = useGPUInstancing,
            perObjectData = PerObjectData.ReflectionProbes | PerObjectData.Lightmaps|PerObjectData.LightProbe|PerObjectData.LightProbeProxyVolume| PerObjectData.ShadowMask| PerObjectData.OcclusionProbe
            | lightsPerObjectFlags
        };//按照着色器以及距离排序    //后续参数可以配置   //光照贴图设置
        drawingSettings.SetShaderPassName(1,litShaderTagId);//插入支持的passtag，srpdefaultunlit是默认的，C

        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque, renderingLayerMask: (uint)renderingLayerMask);
        context.DrawRenderers(
            cullingResults,ref drawingSettings,ref filteringSettings);
        //天空box
        context.DrawSkybox(camera);
        //复制深度图
        CopyAttachments();
        
        
        
        //透明物体
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(
            cullingResults,ref drawingSettings,ref filteringSettings);
    }

    void CopyAttachments()
    {
        if (useDepthTexture)
        {
            buffer.GetTemporaryRT(depthTextureId,camera.pixelWidth,camera.pixelHeight,
                32,FilterMode.Point,RenderTextureFormat.Depth);
            buffer.CopyTexture(depthAttachmentId,depthTextureId);
            ExecuteBuffer();
        }
    }


    void Cleanup () {
        lighting.Cleanup();
        if (useIntermediateBuffer)
        {
            buffer.ReleaseTemporaryRT(colorAttachmentId);
            buffer.ReleaseTemporaryRT(depthAttachmentId);

            if (useDepthTexture)
            {
                buffer.ReleaseTemporaryRT(depthTextureId);
            }
        }
    }
    
    void Setup()
    {
        context.SetupCameraProperties(camera);//传入相机的mvp矩阵等参数
        CameraClearFlags flags;
        flags = camera.clearFlags;
        useIntermediateBuffer = useDepthTexture || postFXStack.IsActive;
        if (useIntermediateBuffer) {
            if (flags > CameraClearFlags.Color) {
                flags = CameraClearFlags.Color;
            }
            buffer.GetTemporaryRT(
                colorAttachmentId, camera.pixelWidth, camera.pixelHeight,
                0, FilterMode.Bilinear, useHDR? RenderTextureFormat.DefaultHDR: RenderTextureFormat.Default
            );//支持defaulthdr 每个颜色16位，在任何平台总是支持
            buffer.GetTemporaryRT(
                depthAttachmentId,camera.pixelWidth,camera.pixelHeight,32,FilterMode.Point,RenderTextureFormat.Depth);
            /*buffer.SetRenderTarget(
                frameBufferId,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );*/
            buffer.SetRenderTarget(
                colorAttachmentId,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,depthAttachmentId,RenderBufferLoadAction.DontCare,RenderBufferStoreAction.Store
            );
        }

        buffer.ClearRenderTarget(flags<=CameraClearFlags.Depth,flags==CameraClearFlags.Color,flags==CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);//清屏     有两种清理方式，在没有设置摄像机之前调用是使用一个专门的着色器渲染一个四边形，在之后调用话则是直接清理颜色和深度
        //Clear (color+Z+stencil)
        //
        
        //《buffer标识符开始》
        buffer.BeginSample(SampleName);
     //   buffer.ClearRenderTarget(true,true,Color.clear);//摆脱旧的渲染内容，他会自动调用sample name生成一个sample，放在创建之前后面的会和他合并。frame debuger层级会自动合并。
        ExecuteBuffer();
    }

    void Submit()
    {
        buffer.EndSample(SampleName);
        ExecuteBuffer();
        context.Submit();
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
    

    bool Cull(float maxShadowDistance)
    {
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            //p.shadowDistance = maxShadowDistance;//阴影距离剔除
            p.shadowDistance = Math.Min(maxShadowDistance, camera.farClipPlane);//超出渲染距离的不要，阴影超过距离的也不要，所以取最小值
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }

}
