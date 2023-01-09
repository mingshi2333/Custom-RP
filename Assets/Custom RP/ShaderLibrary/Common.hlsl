#ifndef CUSTOM_COMMON_INCLUDED
#define CUSTOM_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "UnityInput.hlsl"

#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_MATRIX_P glstate_matrix_projection
#define UNITY_PREV_MATRIX_M PrevObjectToWorldMatrix
#define UNITY_MATRIX_I_V unity_MatrixInvV
#define UNITY_PREV_MATRIX_I_M unity_PreWorldToObject

//unity_ProbesOcclusion此参数只有在UnityInstancing定义了SHADOWS_SHADOWMASK时才会自动得到实例化，所以我们必须在头文件之前定义她
#if defined(_SHADOW_MASK_DISTANCE)||defined(_SHADOW_MASK_ALWAYS)
    #define SHADOWS_SHADOWMASK
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
SAMPLER(sampler_linear_clamp);
SAMPLER(sampler_point_clamp);

/**
 * \brief 
 * \return 如果是正交相机，他的分量是1，否则是0
 */
bool IsOrthographicCamera()
{
    return unity_OrthoParams.w;
}

/**
 * \brief 
 * \param rawDepth 
 * \return 近距离和远距离被存储在_ProjectionParams的Y和Z部分。如果使用反转的深度缓冲器，我们还需要反转原始深度。
 */
float OrthographicDepthBufferToLinear (float rawDepth) {
    #if UNITY_REVERSED_Z
    rawDepth = 1.0 - rawDepth;
    #endif
    return (_ProjectionParams.z - _ProjectionParams.y) * rawDepth + _ProjectionParams.y;
}

#include "Fragment.hlsl"

/*float3 TransformObjectToWorld (float3 positionOS) {
    return mul(unity_ObjectToWorld,float4(positionOS,1.0)).xyz;
}

float4 TransformWorldToHClip (float3 positionWS) {
    return mul(unity_MatrixVP, float4(positionWS, 1.0));
}*/
float Square(float v)
{
    return v*v;
}
float DistanceSquared(float3 pA,float3 pB)
{
    return dot(pA-pB,pA-pB);
}
float3 DecodeNormal (float4 sample, float scale) {
    #if defined(UNITY_NO_DXT5nm)
    return UnpackNormalRGB(sample, scale);
    #else
    return UnpackNormalmapRGorAG(sample, scale);
    #endif
}

float3 NormalTangentToWorld (float3 normalTS, float3 normalWS, float4 tangentWS) {
    float3x3 tangentToWorld =
        CreateTangentToWorld(normalWS, tangentWS.xyz, tangentWS.w);
    return TransformTangentToWorld(normalTS, tangentToWorld);
}

//计算lod中间切换
void ClipLOD(Fragment fragment,float fade)
{
    #if defined(LOD_FADE_CROSSFADE)
    float dither=InterleavedGradientNoise(fragment.positionSS, 0);
    clip(fade + (fade < 0.0 ? dither : -dither));
    #endif
}


#endif