/** Copyright (c) Lazu Ioan-Bogdan */

Shader "Critias/DetailMetallicDithered" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_MetallicRough("Metallic (RGB) Rough (A)", 2D) = "white" {}
		_RoughnessMult ("Roughness Multiplier", Range(0, 1)) = 1
		_BumpMap("Normal Map", 2D) = "bump" {}		

		CRITIAS_MaxFoliageTypeDistance ("Dont touch!", Float) = 1000
	}

	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200
		
		Cull Back

		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard  vertex:DetailVert nolightmap addshadow			
		#pragma instancing_options procedural:Critias_FoliageSetup

		#pragma target 3.0

		#include "../CritiasUtils.cginc"

		struct Input {
			float2 uv_MainTex;

			half transparency;
			float2 screenPosOptimized;
		};

		struct appdata_t
		{
			float4 vertex		: POSITION;
			float4 tangent		: TANGENT;
			float3 normal		: NORMAL;
			float4 texcoord		: TEXCOORD0;
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};
		
		#define LOD_TRANZITION_THRESHOLD 2.0
		float CRITIAS_MaxFoliageTypeDistance;
		
		#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
		StructuredBuffer<float4x4> CRITIAS_InstancePositionBuffer;
		#endif
		void Critias_FoliageSetup()
		{
		#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
			float4x4 mtx = CRITIAS_InstancePositionBuffer[unity_InstanceID];

			// Set the mtx
			unity_ObjectToWorld = mtx;

			// Invert the mtx as per the example
			unity_WorldToObject = unity_ObjectToWorld;
			unity_WorldToObject._14_24_34 *= -1;
			unity_WorldToObject._11_22_33 = 1.0f / unity_WorldToObject._11_22_33;
		#endif
		}

		void DetailVert(inout appdata_t IN, out Input OUT)
		{
			float3 pos = float3(unity_ObjectToWorld[0].w, unity_ObjectToWorld[1].w, unity_ObjectToWorld[2].w);
			float dist = distance(pos, _WorldSpaceCameraPos);

			if (dist <= CRITIAS_MaxFoliageTypeDistance)
			{
				UNITY_INITIALIZE_OUTPUT(Input, OUT);

				if (dist > CRITIAS_MaxFoliageTypeDistance - LOD_TRANZITION_THRESHOLD)
					OUT.transparency = (CRITIAS_MaxFoliageTypeDistance - dist) / (LOD_TRANZITION_THRESHOLD);
				else
					OUT.transparency = 1.0;

				OUT.screenPosOptimized = Critias_ScreenPosDitherFast(IN.vertex);
			}
			else
			{				
				IN.vertex = half4(0, 0, 0, 0);
			}
		}

		sampler2D _MainTex;
		sampler2D _BumpMap;
		sampler2D _MetallicRough;

		fixed4 _Color;
		fixed _RoughnessMult;

		void surf (Input IN, inout SurfaceOutputStandard o) 
		{
			Critias_ApplyDitherInvertedFast(IN.screenPosOptimized, IN.transparency);

			fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
			o.Albedo = c.rgb;

			fixed4 metallicRough = tex2D(_MetallicRough, IN.uv_MainTex);

			o.Metallic = metallicRough.r;
			o.Smoothness = metallicRough.a * _RoughnessMult;

			o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
		}
		ENDCG			
	}
	FallBack "Diffuse"
}
