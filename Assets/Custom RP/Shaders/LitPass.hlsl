#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED

#define MAX_DIRECTIONAL_LIGHT_COUNT 4


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
    float4 tangentOS : TANGENT;
    GI_ATTRIBUTE_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct Varyings
{
    float4 positionCS:SV_POSITION;
    float3 positionWS:VAR_POSITION;
    float3 normalWS : VAR_NORMAL;
    float2 baseUV : VAR_BASE_UV;//任何不使用的标识符
    float2 detailUV :VAR_DETAIL_UV;
    #if defined(_NORMAL_MAP)
        float4 tangentWS : VAR_TANGENT;
    #endif
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
    
    #if defined(_NORMAL_MAP)
        output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
    #endif
    
    //float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial,_BaseMap_ST);
    output.baseUV = TransformBaseUV(input.baseUV);
    #if defined(_DETAIL_MAP)
        output.detailUV = TransformDetailUV(input.baseUV);
    #endif
    
    return output;
    
}
float4 LitPassFragment  (Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);

    // #if defined(LOD_FADE_CROSSFADE)
    //     return -unity_LODFade.x;
    // #endif
    ClipLOD(input.positionCS.xy,unity_LODFade.x);
    
    InputConfig config = GetInputConfig(input.baseUV);
    #if defined(_MASK_MAP)
        config.useMask = true;
    #endif
    
    #if defined(_DETAIL_MAP)
        config.detailUV = input.detailUV;
        config.useDetail = true;
    #endif
    
    float4 base = GetBase(config);
    Surface surface;
    surface.position = input.positionWS;
    surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
    surface.interpolatedNormal  = input.normalWS;
    #if defined(_NORMAL_MAP)
    
        surface.normal = NormalTangentToWorld(
            GetNormalTS(config), input.normalWS, input.tangentWS
        );
    #else
        surface.normal = normalize(input.normalWS);
    #endif
    
    
    surface.color = base.rgb;
    surface.alpha  = base.a;
    surface.occlusion = GetOcclusion(config);
    surface.metallic  = GetMetallic(config);
    surface.smoothness = GetSmoothness(config);
    surface.dither = InterleavedGradientNoise(input.positionCS.xy,0);
    surface.depth = -TransformWorldToView(input.positionWS).z;
    surface.fresnelStrength = GetFresnel(config);
    

    #if defined(_PREMULTIPLY_ALPHA)
        BRDF brdf = GetBRDF(surface, true);
    #else
        BRDF brdf = GetBRDF(surface);
    #endif

    #if defined(_SHADOWS_CLIP)
    clip(base.a- GetCutoff(config));
    #endif
    
    GI gi =GetGI(GI_FRAGMENT_DATA(input),surface,brdf);//采样lightmap

    
    float3 color = GetLighting(surface,brdf,gi);
    color += GetEmission(config);
    return float4(color, GetFinalAlpha(surface.alpha));

}



#endif