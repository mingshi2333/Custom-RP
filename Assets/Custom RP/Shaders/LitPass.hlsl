#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED

#define MAX_DIRECTIONAL_LIGHT_COUNT 4

CBUFFER_START(_CustomLight)
    int _DirectionalLightCount;
    float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
    float4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHT_COUNT];
    float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];
CBUFFER_END
#include "../ShaderLibrary/Surface.hlsl"
#include  "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include  "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"


/*TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    //float4 _BaseColor;
    UNITY_DEFINE_INSTANCED_PROP(float4,_BaseMap_ST)
    UNITY_DEFINE_INSTANCED_PROP(float4,_BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
    UNITY_DEFINE_INSTANCED_PROP(float,_Metallic)
    UNITY_DEFINE_INSTANCED_PROP(float,_Smoothness)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)*/

struct Attributes {
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 baseUV : TEXCOORD0;
    GI_ATTRIBUTE_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct Varyings
{
    float4 positionCS:SV_POSITION;
    float3 positionWS:VAR_POSITION;
    float3 normalWS : VAR_NORMAL;
    float2 baseUV : VAR_BASE_UV;//任何不使用的标识符
    GI_VARYINGS_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
// float4 UnlitPassVertex(Attributes input) : SV_POSITION
// {
//     UNITY_SETUP_INSTANCE_ID(input);
//     float3 positionWS = TransformObjectToWorld(input.positionOS);
//     return TransformWorldToHClip(positionWS);
//     
// }

Varyings LitPassVertex (Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input,output);
    TRANSFER_GI_DATA(input, output);
    output.positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    //float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial,_BaseMap_ST);
    output.baseUV = TransformBaseUV(input.baseUV);
    return output;
    
}
float4 LitPassFragment  (Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    float3 normal = normalize(input.normalWS);

    float4 base  = GetBase(input.baseUV);
    Surface surface;
    surface.position = input.positionWS;
    surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
    surface.normal = normalize(input.normalWS);
    surface.color = base.rgb;
    surface.alpha  = base.a;
    surface.metallic  = GetMetallic(input.baseUV);
    surface.smoothness = GetSmoothness(input.baseUV);
    surface.dither = InterleavedGradientNoise(input.positionCS.xy,0);
    surface.depth = -TransformWorldToView(input.positionWS).z;
    GI gi =GetGI(GI_FRAGMENT_DATA(input),surface);//采样lightmap

    #if defined(_PREMULTIPLY_ALPHA)
    BRDF brdf = GetBRDF(surface, true);
    #else
    BRDF brdf = GetBRDF(surface);
    #endif
    
    
    #if defined(_SHADOWS_CLIP)
    clip(base.a- GetCutoff(input.baseUV));
    #endif
    float3 color = GetLighting(surface,brdf,gi);
    color +=GetEmission(input.baseUV);
    return float4(color,surface.alpha);

}



#endif