#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

#if defined(_DIRECTIONAL_PCF3)
    #define DIRECTIONAL_FILTER_SAMPLES 4
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
    #define DIRECTIONAL_FILTER_SAMPLES 9
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
    #define DIRECTIONAL_FILTER_SAMPLES 16
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif


#if defined(_OTHER_PCF3)
    #define OTHER_FILTER_SAMPLES 4
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_OTHER_PCF5)
    #define OTHER_FILTER_SAMPLES 9
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_OTHER_PCF7)
    #define OTHER_FILTER_SAMPLES 16
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif


#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_SHADOWED_OTHER_LIGHT_COUNT 16
#define MAX_CASCADE_COUNT 4

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);  //由于图集不是常规纹理，让我们通过 TEXTURE2D_SHADOW 宏来定义它，以使其清晰，即使它对我们支持的平台没有影响。
TEXTURE2D_SHADOW(_OtherShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);//采样器常规双线性过滤对深度数据没有意义

CBUFFER_START(_CustomShadows)
    int _CascadeCount;
    float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
    float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT*MAX_CASCADE_COUNT];
    float4x4 _OtherShadowMatrices[MAX_SHADOWED_OTHER_LIGHT_COUNT];
    float4 _OtherShadowTiles[MAX_SHADOWED_OTHER_LIGHT_COUNT];
    float4 _CascadeData[MAX_CASCADE_COUNT];
    //float _ShadowDistance;
    float4 _ShadowDistanceFade;
    float4 _ShadowAtlasSize;
CBUFFER_END

/**
 * \brief distance表示是否启用，shadows指向是哪个阴影
 */
struct ShadowMask
{
    bool always;
    bool distance;
    float4 shadows;
    
};

struct DirectionalShadowData
{
    float strength;
    int tileIndex;
    float3 normalBias;
    int shadowMaskChannel;
};

struct ShadowData
{
    int cascadeIndex;
    float cascadeBlend;
    float strength;
    ShadowMask shadowMask;
};

struct OtherShadowData
{
    float strength;
    int tileIndex;
    bool isPoint;
    int shadowMaskChannel;
    float3 lightPositionWS;
    float3 lightDirectionWS;
    float3 spotDirectionWS;
};

float FadedShadowStrength(float distance,float scale,float fade)
{
    return saturate((1.0-distance*scale)*fade);
}
ShadowData GetShadowData(Surface surfaceWS)
{
    ShadowData data;
    //初始化默认不使用
    data.shadowMask.always = false;
    data.shadowMask.distance = false;
    data.shadowMask.shadows = 1.0;
    data.cascadeBlend = 1.0;
    data.strength = FadedShadowStrength(
        surfaceWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y
    );
    int i;
    for (i = 0; i < _CascadeCount; i++) {
        float4 sphere = _CascadeCullingSpheres[i];
        float distanceSqr = DistanceSquared(surfaceWS.position, sphere.xyz);
        if (distanceSqr < sphere.w) {
            float fade = FadedShadowStrength(
                distanceSqr, _CascadeData[i].x, _ShadowDistanceFade.z
            );
            if (i == _CascadeCount - 1) {
                data.strength *= fade;
            }
            else {
                data.cascadeBlend = fade;
            }
            break;
        }
    }
    if(i==_CascadeCount&&_CascadeCount>0)
    {
        data.strength = 0.f;//????
    }
    #if defined(_CASCADE_BLEND_DITHER)
    else if (data.cascadeBlend < surfaceWS.dither) {
        i += 1;
    }
    #endif
    #if !defined(_CASCADE_BLEND_SOFT)
        data.cascadeBlend =1.0;
    #endif
    
    data.cascadeIndex = i;
    return data;
}



float SampleDirectionalShadowAtlas(float3 positionSTS)
{
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas,SHADOW_SAMPLER,positionSTS);
}

float FilterDirectionalShadow (float3 positionSTS) {
    #if defined(DIRECTIONAL_FILTER_SETUP)
    float weights[DIRECTIONAL_FILTER_SAMPLES];
    float2 positions[DIRECTIONAL_FILTER_SAMPLES];
    float4 size = _ShadowAtlasSize.yyxx;
    DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions);
    float shadow = 0;
    for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++) {
        shadow += weights[i] * SampleDirectionalShadowAtlas(
            float3(positions[i].xy, positionSTS.z)
        );
    }
    return shadow;
    #else
    return SampleDirectionalShadowAtlas(positionSTS);
    #endif
}

float SampleOtherShadowAtlas (float3 positionSTS, float3 bounds) {
    positionSTS.xy = clamp(positionSTS.xy, bounds.xy, bounds.xy + bounds.z);
    return SAMPLE_TEXTURE2D_SHADOW(
        _OtherShadowAtlas, SHADOW_SAMPLER, positionSTS
    );
}

float FilterOtherShadow (float3 positionSTS,float3 bounds) {
    #if defined(OTHER_FILTER_SETUP)
    real weights[OTHER_FILTER_SAMPLES];
    real2 positions[OTHER_FILTER_SAMPLES];
    float4 size = _ShadowAtlasSize.wwzz;
    OTHER_FILTER_SETUP(size, positionSTS.xy, weights, positions);
    float shadow = 0;
    for (int i = 0; i < OTHER_FILTER_SAMPLES; i++) {
        shadow += weights[i] * SampleOtherShadowAtlas(
            float3(positions[i].xy, positionSTS.z),bounds
        );
    }
    return shadow;
    #else
    return SampleOtherShadowAtlas(positionSTS,bounds);
    #endif
}


float GetCascadeShadow(DirectionalShadowData directional,ShadowData global,Surface surfaceWS)
{
    float3 normalBias = surfaceWS.interpolatedNormal  * directional.normalBias*_CascadeData[global.cascadeIndex].y;
    float3 positionSTS = mul(
        _DirectionalShadowMatrices[directional.tileIndex],
        float4(surfaceWS.position+normalBias, 1.0)
    ).xyz;//计算阴影坐标,采样时候偏移了一个像素坐标
    //float shadow = SampleDirectionalShadowAtlas(positionSTS);
    float shadow = FilterDirectionalShadow(positionSTS);

    if (global.cascadeBlend < 1.0) {
        normalBias = surfaceWS.interpolatedNormal  *
            (directional.normalBias * _CascadeData[global.cascadeIndex + 1].y);
        positionSTS = mul(
            _DirectionalShadowMatrices[directional.tileIndex + 1],
            float4(surfaceWS.position + normalBias, 1.0)
        ).xyz;
        shadow = lerp(
            FilterDirectionalShadow(positionSTS), shadow, global.cascadeBlend
        );
    }//阴影之间过度
    return shadow;
}
float GetBakedShadow(ShadowMask mask,int channel)
{
    float shadow = 1.0;
    if(mask.distance||mask.always)
    {
        if(channel>=0)
        {
            shadow = mask.shadows[channel];//选择通道
        }
    }
    return shadow;
}

float GetBakedShadow (ShadowMask mask,int channel, float strength) {
    if (mask.always || mask.distance) {
        return lerp(1.0, GetBakedShadow(mask,channel), abs(strength));
    }
    return 1.0;
}
float MixBakedAndRealtimeShadows(ShadowData global,float shadow,int shadowMaskChannel, float strength)
{
    float baked = GetBakedShadow(global.shadowMask,shadowMaskChannel);
    if(global.shadowMask.always)
    {
        shadow = lerp(1.0,shadow,global.strength);
        shadow = min(baked,shadow);
        return lerp(1.0,shadow,strength);
    }
    
    if(global.shadowMask.distance)
    {
        shadow = lerp(baked,shadow,global.strength);
        return lerp(1.0,shadow,strength);
    }
    return lerp(1.0,shadow,strength*global.strength);
}
/**
 * \brief 
 * \param data 储存阴影数组的矩阵
 * \param surfaceWS 世界空间坐标
 * \return 
 */
float GetDirectionalShadowAttenuation (DirectionalShadowData directional,ShadowData global, Surface surfaceWS) {

    float shadow;
    ///设置是否接受阴影
    #if !defined(_RECEIVE_SHADOWS)
        return 1.0;
    #endif
    if (directional.strength *global.strength <= 0.0)
    {
        shadow = GetBakedShadow(global.shadowMask,directional.shadowMaskChannel, directional.strength);
    }
    if(directional.strength<=0.0)
    {
        shadow = 1.0;
    }
    else
    {
        shadow = GetCascadeShadow(directional,global,surfaceWS);
        shadow = MixBakedAndRealtimeShadows(global,shadow,directional.shadowMaskChannel, directional.strength);
        shadow = lerp(1.0, shadow, directional.strength);//黑的地方是阴影也就是0
        
    }
    return shadow;
}
static const float3 pointShadowPlanes[6] = {
    float3(-1.0, 0.0, 0.0),
    float3(1.0, 0.0, 0.0),
    float3(0.0, -1.0, 0.0),
    float3(0.0, 1.0, 0.0),
    float3(0.0, 0.0, -1.0),
    float3(0.0, 0.0, 1.0)
};
float GetOtherShadow(OtherShadowData other,ShadowData global,Surface surfaceWS)
{
    float tileIndex = other.tileIndex;
    float3 lightPlane   = other.spotDirectionWS;
    if (other.isPoint) {
        float faceOffset = CubeMapFaceID(-other.lightDirectionWS);
        tileIndex += faceOffset;
        lightPlane = pointShadowPlanes[faceOffset];
    }
    float4 tileData = _OtherShadowTiles[tileIndex];
    float3 surfaceToLight = other.lightPositionWS-surfaceWS.position;
    float distanceToLightPlance = dot(surfaceToLight,lightPlane);
    float3 normalBias = surfaceWS.interpolatedNormal * (tileData.w*distanceToLightPlance);
    float4 positionSTS = mul(
        _OtherShadowMatrices[tileIndex],
        float4(surfaceWS.position + normalBias, 1.0)
    );
    return FilterOtherShadow(positionSTS.xyz / positionSTS.w, tileData.xyz);
}

float GetOtherShadowAttenuation(OtherShadowData other,ShadowData global,Surface surfaceWS)
{
    #if !defined(_RECEIVE_SHADOWS)
    return 1.0;
    #endif
    float shadow;
    if(other.strength>0.0*global.strength<=0.0)
    {
        shadow = GetBakedShadow(global.shadowMask,other.shadowMaskChannel,abs(other.strength));
    }
    else
    {
        shadow = GetOtherShadow(other,global,surfaceWS);
        shadow = MixBakedAndRealtimeShadows(global,shadow,other.shadowMaskChannel,other.strength);
    }
    return shadow;
}


#endif