#ifndef CUSTOM_UNLIT_PASS_INCLUDED
#define CUSTOM_UNLIT_PASS_INCLUDED
#include "../ShaderLibrary/Common.hlsl"

// CBUFFER_START(UnityPerMaterial)
//     float4 _BaseColor;
// CBUFFER_END

/*TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    //float4 _BaseColor;
    UNITY_DEFINE_INSTANCED_PROP(float4,_BaseMap_ST)
    UNITY_DEFINE_INSTANCED_PROP(float4,_BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)*/

struct Attributes {
    float3 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct Varyings
{
    float4 positionCS:SV_POSITION;
    float2 baseUV : VAR_BASE_UV;//任何不使用的标识符
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
// float4 UnlitPassVertex(Attributes input) : SV_POSITION
// {
//     UNITY_SETUP_INSTANCE_ID(input);
//     float3 positionWS = TransformObjectToWorld(input.positionOS);
//     return TransformWorldToHClip(positionWS);
//     
// }

Varyings UnlitPassVertex(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input,output);
    float3 positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(positionWS);
    output.baseUV = TransformBaseUV(input.baseUV);
    return output;
    
}
float4 UnlitPassFragment (Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    float4 base  = GetBase(input.baseUV);
    
    #if defined(_CLIPPING)
        clip(base.a- GetCutoff(input.baseUV));
    #endif
    
    return base;

}



#endif