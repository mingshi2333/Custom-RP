﻿Shader "Custom RP/Particles/Unlit" {
	
	Properties {
		_BaseMap("Texture", 2D) = "white" {}
		_BaseColor("Color",Color)=(1,1,1,1)
		[Toggle(_VERTEX_COLORS)] _VertexColors ("Vertex Colors", Float) = 0
		[Toggle(_FLIPBOOK_BLENDING)] _FlipbookBlending ("Flipbook Blending", Float) = 0
		_Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
		[HDR] _BaseColor("Color", Color) = (1.0, 1.0, 1.0, 1.0)
		[Toggle(_CLIPPING)] _Clipping("Alpha Clipping",Float) = 0
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
		[Enum(off,0,on,1)] _ZWrite ("Z Write",Float) = 1
 		}
	
	SubShader {
		HLSLINCLUDE
		#include "../ShaderLibrary/Common.hlsl"
		#include "UnlitInput.hlsl"
		ENDHLSL
		
		Pass {
			Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
			ZWrite [_ZWrite]
			HLSLPROGRAM
			#pragma target 3.5
			#pragma shader_feature _CLIPPING
			#pragma shader_feature _VERTEX_COLORS
			#pragma shader_feature _FLIPBOOK_BLENDING
			#pragma multi_compile_instancing
			#pragma vertex UnlitPassVertex
			#pragma fragment UnlitPassFragment
			
			
			#include "UnlitPass.hlsl"
			ENDHLSL
			
		}
	}
	CustomEditor "CustomShaderGUI"
}