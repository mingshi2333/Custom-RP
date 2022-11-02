#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);  //由于图集不是常规纹理，让我们通过 TEXTURE2D_SHADOW 宏来定义它，以使其清晰，即使它对我们支持的平台没有影响。
SAMPLER_CMP(sampler_DirectionalShadowAtlas);//采样器常规双线性过滤对深度数据没有意义

CBUFFER_START(_CustomShadows)
    float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT];
CBUFFER_END

struct DirectionalShadowData
{
    float strength;
    int tileIndex;
};

float SampleDirectionalShadowAtlas(float3 positionSTS)
{
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas,sampler_DirectionalShadowAtlas,positionSTS);
}
float GetDirectionalShadowAttenuation (DirectionalShadowData data, Surface surfaceWS) {
    if (data.strength <= 0.0)
    {
        return 1.0;
    }
    float3 positionSTS = mul(
        _DirectionalShadowMatrices[data.tileIndex],
        float4(surfaceWS.position, 1.0)
    ).xyz;
    float shadow = SampleDirectionalShadowAtlas(positionSTS);
    return lerp(1.0, shadow, data.strength);
}

#endif