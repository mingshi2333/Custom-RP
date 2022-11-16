#ifndef CUSTOM_UNITY_INPUT_INCLUDED
#define CUSTOM_UNITY_INPUT_INCLUDED

CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld;
	float4x4 unity_WorldToObject;
	float4 unity_LODFade;
	float3 _WorldSpaceCameraPos;//相机位置
	float4  unity_WorldTransformParams;

	float4 unity_ProbesOcclusion;

///光照贴图uv，由unity自动生成，定义了一个纹理展开，有自己的空间专门的缩放平移
	float4 unity_LightmapST;
	float4 unity_DynamicLightmapST;//为了srp批处理兼容

///  probe 间接光球谐函数参数
	float4 unity_SHAr;
	float4 unity_SHAg;
	float4 unity_SHAb;
	float4 unity_SHBr;
	float4 unity_SHBg;
	float4 unity_SHBb;
	float4 unity_SHC;

/// lppv 体间接
	float4 unity_ProbeVolumeParams;
	float4x4 unity_ProbeVolumeWorldToObject;
	float4 unity_ProbeVolumeSizeInv;
	float4 unity_ProbeVolumeMin;
CBUFFER_END

float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 glstate_matrix_projection;
float4x4 PrevObjectToWorldMatrix;
float4x4 unity_MatrixInvV;
float4x4 unity_PreWorldToObject;

#endif