#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_CASCADE_COUNT 4

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);  //由于图集不是常规纹理，让我们通过 TEXTURE2D_SHADOW 宏来定义它，以使其清晰，即使它对我们支持的平台没有影响。
SAMPLER_CMP(sampler_DirectionalShadowAtlas);//采样器常规双线性过滤对深度数据没有意义

CBUFFER_START(_CustomShadows)
    int _CascadeCount;
    float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
    float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT*MAX_CASCADE_COUNT];
    float4 _CascadeData[MAX_CASCADE_COUNT];
    //float _ShadowDistance;
    float4 _ShadowDistanceFade;
CBUFFER_END

struct DirectionalShadowData
{
    float strength;
    int tileIndex;
    float3 normalBias;
};

struct ShadowData
{
    int cascadeIndex;
    float strength;
};
float FadedShadowStrength(float distance,float scale,float fade)
{
    return saturate((1.0-distance*scale)*fade);
}
ShadowData GetShadowData(Surface surfaceWS)
{
    ShadowData data;
    data.strength = FadedShadowStrength(
        surfaceWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y
    );
    int i;
    for (i = 0; i < _CascadeCount; i++) {
        float4 sphere = _CascadeCullingSpheres[i];
        float distanceSqr = DistanceSquared(surfaceWS.position, sphere.xyz);
        if (distanceSqr < sphere.w) {
            if (i == _CascadeCount - 1) {
                data.strength *= FadedShadowStrength(
                    distanceSqr, _CascadeData[0].x, _ShadowDistanceFade.z
                );
            }
            break;
        }
    }
    if(i==_CascadeCount)
    {
        data.strength = 0.f;
    }
    data.cascadeIndex = i;
    return data;
}

float SampleDirectionalShadowAtlas(float3 positionSTS)
{
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas,sampler_DirectionalShadowAtlas,positionSTS);
}

/**
 * \brief 
 * \param data 储存阴影数组的矩阵
 * \param surfaceWS 世界空间坐标
 * \return 
 */
float GetDirectionalShadowAttenuation (DirectionalShadowData directional,ShadowData global, Surface surfaceWS) {
    if (directional.strength <= 0.0)
    {
        return 1.0;
    }
    float3 normalBias = surfaceWS.normal * directional.normalBias*_CascadeData[global.cascadeIndex].y;
    float3 positionSTS = mul(
        _DirectionalShadowMatrices[directional.tileIndex],
        float4(surfaceWS.position+normalBias, 1.0)
    ).xyz;//计算阴影坐标,采样时候偏移了一个像素坐标
    float shadow = SampleDirectionalShadowAtlas(positionSTS);
    return lerp(1.0, shadow, directional.strength);//黑的地方是阴影也就是0
}

#endif