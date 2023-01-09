#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED
//汇入函数库
/*#include "../ShaderLibrary/Common.hlsl"

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)//放入材质缓冲区
UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)//放入材质缓冲区结束*/

bool _ShadowPancking;
//定义汇入模型数据
struct Attributes
{
    float3 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID//实例化

};
//汇入顶点数据
struct Varyings
{
    float4 positionCS_SS : SV_POSITION;//实例化
    float2 baseUV : VAR_BASE_UV;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

//顶点着色器
Varyings ShadowCasterPassVertex(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);//实例化
    UNITY_TRANSFER_INSTANCE_ID(input, output);//传递到片元着色器里面
    float3 positionWS = TransformObjectToWorld(input.positionOS);//从模型空间到世界空间
    output.positionCS_SS = TransformWorldToHClip(positionWS);//从世界空间到裁剪空间

    //float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);//汇入ST
   //output.baseUV = input.baseUV * baseST.xy + baseST.zw;//修改UV
    ///把clip限制近平面
    ///UNITY_REVERSED_Z是判断有没有z反转的，dx为1，gl为0，UNITY_NEAR_CLIP_VALUE在dx中是1
    if(_ShadowPancking)
    {
        #if UNITY_REVERSED_Z
        output.positionCS_SS.z =
            min(output.positionCS_SS.z, output.positionCS_SS.w * UNITY_NEAR_CLIP_VALUE);
        #else
        output.positionCS.z =
            max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);//把clip限制近平面
        #endif
    }
    output.baseUV = TransformBaseUV(input.baseUV);
    return output;
}
//片元着色器
void ShadowCasterPassFragment(Varyings input)
{
    UNITY_SETUP_INSTANCE_ID(input);//实例化
    
    InputConfig config = GetInputConfig(input.positionCS_SS,input.baseUV);
    ClipLOD(config.fragment,unity_LODFade.x);
    //float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);//采样贴图
    //float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);//汇入基础颜色
    float4 base = GetBase(config);
    #if defined(_SHADOWS_CLIP)
        clip(base.a - GetCutoff(config));//裁剪
    #elif defined(_SHADOWS_DITHER)
        float dither = InterleavedGradientNoise(input.positionCS.xy, 0);
        clip(base.a - dither);
    #endif
    
    
}
#endif