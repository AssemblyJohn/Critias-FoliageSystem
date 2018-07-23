// Upgrade NOTE: removed variant '__' where variant LOD_FADE_PERCENTAGE is used.

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Critias/WindTree_Billboard"
{
	Properties {		
		_Color("Main Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_BumpMap("Bump (RGB)", 2D) = "white" {}
		_HueVariation("Hue Variation", Color) = (1.0,0.5,0.0,0.1)
		_Cutoff("Cutoff", Range(0, 1)) = 0.33
		_Size("Billboard Sizes", Vector) = (0, 0, 0, 0)
	}
	SubShader {
		Tags
		{ 
			"IgnoreProjector" = "True" 
			// "Queue" = "Geometry" 
			"Queue" = "AlphaTest"
			"RenderType" = "TransparentCutout" 

			"DisableBatching" = "True"
		}
		
		LOD 200
		Cull Off

		CGPROGRAM						
		
		// If you don't like the self-shadowing on the billboards you can safely uncomment this. However the shadow will rotate with the billboard
		#pragma surface surf Standard vertex:BatchedTreeVertex dithercrossfade addshadow

		// NOTE: This approach is more performance intensive!!!
		// This is the default SpeedTree mode. If you are OK with the billboards self shadows uncomment this. The shadows will not rotate with the billboard. 
		// #pragma surface surf Standard vertex:BatchedTreeVertex dithercrossfade

		#pragma target 3.0

		#include "../../CritiasUtils.cginc"

		sampler2D _MainTex;
		sampler2D _BumpMap;

		struct Input {
			float2 computedUv;

			// Our own optimized screen pos
			float2 screenPosOptimized;

			half transparency;
			half HueVariationAmount;
		};

		struct appdata_t
		{
			float4 vertex		: POSITION;
			float4 tangent		: TANGENT;
			float3 normal		: NORMAL;
			float4 texcoord		: TEXCOORD0;
			float4 texcoord1	: TEXCOORD1;
			float4 texcoord2	: TEXCOORD2;
			half4 color			: COLOR;

			UNITY_VERTEX_INPUT_INSTANCE_ID
		};

		// Make sure this matches the value in the 'FoliageRenderer'
		#define LOD_TRANZITION_THRESHOLD 3.0
		float CRITIAS_MaxFoliageTypeDistance;

		fixed4 _Color;

		fixed _Cutoff;

		fixed4 _HueVariation;

		float4 _UVVert_U[8];
		float4 _UVVert_V[8];

		float4 _UVHorz_U;
		float4 _UVHorz_V;

		float3 _Size;

		void BatchedTreeVertex(inout appdata_t IN, out Input OUT)
		{
			UNITY_INITIALIZE_OUTPUT(Input, OUT);			

			float3 campos = _WorldSpaceCameraPos;

			float3 posUnoffseted = IN.texcoord1.xyz;
			float3 pos = posUnoffseted + float3(0, _Size.z, 0);
						
			float dist = distance(pos, campos);

			if (dist >= CRITIAS_MaxFoliageTypeDistance - LOD_TRANZITION_THRESHOLD)
			{	
				if (dist > CRITIAS_MaxFoliageTypeDistance - LOD_TRANZITION_THRESHOLD)
					OUT.transparency = 1.0 - (CRITIAS_MaxFoliageTypeDistance - dist) / (LOD_TRANZITION_THRESHOLD);
				else
					OUT.transparency = 1.0;

				float x = IN.vertex.x;
				float y = IN.vertex.y;

				int idx;
				if (x < 0 && y < 0) idx = 0;
				else if (x < 0 && y > 0) idx = 3;
				else if (x > 0 && y < 0) idx = 1;
				else idx = 2;

				OUT.computedUv = float2(_UVVert_U[0][idx], _UVVert_V[0][idx]);

				// Instance rotation
				float rot = IN.texcoord1.w;

				float3 v2 = float3(0, 0, 1);
				float3 v1 = campos - pos;

				int angleIdx;

				v1 = normalize(v1);

				float dotA, detA;

				dotA = v1.x * v2.x + v1.z * v2.z;
				detA = v1.x * v2.z - v1.z * v2.x;

				// Map to 0 - 360
				// float angle = (atan2(det, dot)) + 180.0f + instance rotation
				float angle = (atan2(detA, dotA)) + UNITY_PI;

				v2 = float3(0, 1, 0);
				float F = dot(v1.yz, v2.yz);

				// TODO: see if we should have here an 'F' value snap or something so that we 
				// don't tranzition at once into a vertical billboard
				if (F > .9)
				{
					// Make it a horizontal billboard						
					IN.vertex.xzy = IN.vertex.xyz;
					OUT.computedUv = float2(_UVHorz_U[idx], _UVHorz_V[idx]);
				}
				else
				{
					v2 = v1;
					v1 = float3(-1, 0, 0);

					dotA = v1.x * v2.x + v1.z * v2.z;
					detA = v1.x * v2.z - v1.z * v2.x;

					// Add tree's instance rotation too
					float angle360 = (atan2(detA, dotA) + 3.141592632) + rot;
					if (angle360 < 0) angle360 = 6.283185264 + angle360;

					// 1.27 is inverse of 45' in rad
					angleIdx = fmod(floor((angle360) * 1.273239553 + 0.5), 8);

					OUT.computedUv = float2(_UVVert_U[angleIdx][idx], _UVVert_V[angleIdx][idx]);
				}				

				// OUT.transparency = 1.0 - (clamp(DISTANCE - dist, 0.0, THRES) / THRES);

				float2 vert;

				// Rotate vert, tangent and normal
				float cosO = cos(angle);
				float sinO = sin(angle);

				vert.x = cosO * IN.vertex.x + sinO * IN.vertex.z;
				vert.y = -sinO * IN.vertex.x + cosO * IN.vertex.z;

				IN.vertex.x = vert.x;
				IN.vertex.z = vert.y;

				vert.x = cosO * IN.normal.x + sinO * IN.normal.z;
				vert.y = -sinO * IN.normal.x + cosO * IN.normal.z;

				IN.normal.x = vert.x;
				IN.normal.z = vert.y;

				vert.x = cosO * IN.tangent.x + sinO * IN.tangent.z;
				vert.y = -sinO * IN.tangent.x + cosO * IN.tangent.z;

				IN.tangent.x = vert.x;
				IN.tangent.z = vert.y;

				// Apply the scale, and move into world space
				IN.vertex += float4(0.0, 0.5, 0, 0);
				float4 scale = float4(IN.texcoord2.xyx, 1.0);
				IN.tangent *= scale;
				IN.vertex *= scale;
				IN.vertex += float4(pos.xyz, 0);

				// Set the hue variation ammount
				{
					float hueVariationAmount = frac(posUnoffseted.x + posUnoffseted.y + posUnoffseted.z);
					OUT.HueVariationAmount = saturate(hueVariationAmount * _HueVariation.a);
				}

				// Calc the screen pos
				OUT.screenPosOptimized = Critias_ScreenPosDitherFast(IN.vertex);
			}
		}

		void surf (Input IN, inout SurfaceOutputStandard o)
		{				
			// Apply our dither
			Critias_ApplyDitherInvertedFast(IN.screenPosOptimized, IN.transparency);

			half4 diffuseColor = tex2D(_MainTex, IN.computedUv);
			
			clip(diffuseColor.a - _Cutoff);

			// Hue variation
			{
				half3 shiftedColor = lerp(diffuseColor.rgb, _HueVariation.rgb, IN.HueVariationAmount);
				half maxBase = max(diffuseColor.r, max(diffuseColor.g, diffuseColor.b));
				half newMaxBase = max(shiftedColor.r, max(shiftedColor.g, shiftedColor.b));
				maxBase /= newMaxBase;
				maxBase = maxBase * 0.5f + 0.5f;

				// Preserve vibrance
				shiftedColor.rgb *= maxBase;
				diffuseColor.rgb = saturate(shiftedColor);
			}

			o.Albedo = diffuseColor.rgb * _Color.rgb;
			o.Alpha = diffuseColor.a;

			o.Normal = UnpackNormal(tex2D(_BumpMap, IN.computedUv));

			// Default to 2
			o.Smoothness = 0;
			o.Metallic = 0;
		}
		ENDCG

		Pass
		{
			Tags{ "LightMode" = "ShadowCaster" }

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#pragma multi_compile_vertex  LOD_FADE_PERCENTAGE LOD_FADE_CROSSFADE
			#pragma multi_compile_fragment __ LOD_FADE_CROSSFADE
			#pragma multi_compile_instancing
			#pragma instancing_options assumeuniformscaling lodfade maxcount:50
			#pragma shader_feature GEOM_TYPE_BRANCH GEOM_TYPE_BRANCH_DETAIL GEOM_TYPE_FROND GEOM_TYPE_LEAF GEOM_TYPE_MESH
			#pragma multi_compile_shadowcaster
			#define ENABLE_WIND

			#define DISTANCE_SCALE_BIAS 3.0
			#define DISTANCE_SCALE_BIAS_SQR 9.0		

			#include "SpeedTreeCommon.cginc"

			float CRITIAS_MaxFoliageTypeDistance;
			float CRITIAS_MaxFoliageTypeDistanceSqr;

			fixed _Cutoff;

			float4 _UVVert_U[8];
			float4 _UVVert_V[8];

			float4 _UVHorz_U;
			float4 _UVHorz_V;

			float3 _Size;

			struct v2f
			{
				V2F_SHADOW_CASTER;				
				float2 uv : TEXCOORD1;				
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			// v2f vert(SpeedTreeVB v)
			v2f vert(appdata_full v)			
			{
				v2f o;

				// Set the shadow billboard to it's designated position
				{
					float3 campos = _WorldSpaceCameraPos;
					// float3 lightpos = _WorldSpaceLightPos0;

					float3 posUnoffseted = v.texcoord1.xyz;
					float3 pos = posUnoffseted + float3(0, _Size.z, 0);

					float dist = distance(pos, campos);

					// if (dist >= CRITIAS_MaxFoliageTypeDistance - LOD_TRANZITION_THRESHOLD)
					if (dist >= CRITIAS_MaxFoliageTypeDistance)
					{						
						float x = v.vertex.x;
						float y = v.vertex.y;

						int idx;
						if (x < 0 && y < 0) idx = 0;
						else if (x < 0 && y > 0) idx = 3;
						else if (x > 0 && y < 0) idx = 1;
						else idx = 2;

						o.uv = float2(_UVVert_U[0][idx], _UVVert_V[0][idx]);
					
						// Instance rotation
						float rot = v.texcoord1.w;

						float3 v2 = float3(0, 0, 1);
						float3 v1 = _WorldSpaceLightPos0.xyz;// campos - pos;

						int angleIdx;

						v1 = normalize(v1);

						float dotA, detA;

						dotA = v1.x * v2.x + v1.z * v2.z;
						detA = v1.x * v2.z - v1.z * v2.x;

						// Map to 0 - 360
						// float angle = (atan2(det, dot)) + 180.0f + instance rotation
						float angle = (atan2(detA, dotA)) + UNITY_PI;
						// float angle = (atan2(detA, dotA));
						// float angle = UNITY_PI + rot;						

						/*
						v2 = float3(0, 1, 0);
						float F = dot(v1.yz, v2.yz);												
						{
							v2 = v1;
							v1 = float3(-1, 0, 0);

							dotA = v1.x * v2.x + v1.z * v2.z;
							detA = v1.x * v2.z - v1.z * v2.x;

							// Add tree's instance rotation too
							// float angle360 = (atan2(detA, dotA) + 3.141592632) + rot;
							float angle360 = (atan2(detA, dotA) + 3.141592632);
							// float angle360 = rot;

							if (angle360 < 0) angle360 = 6.283185264 + angle360;

							// 1.27 is inverse of 45' in rad
							angleIdx = fmod(floor((angle360) * 1.273239553 + 0.5), 8);

							o.uv = float2(_UVVert_U[angleIdx][idx], _UVVert_V[angleIdx][idx]);
						}
						*/
						
						float2 vert;

						// Rotate vert, tangent and normal
						float cosO = cos(angle);
						float sinO = sin(angle);

						vert.x = cosO * v.vertex.x + sinO * v.vertex.z;
						vert.y = -sinO * v.vertex.x + cosO * v.vertex.z;

						v.vertex.x = vert.x;
						v.vertex.z = vert.y;

						vert.x = cosO * v.normal.x + sinO * v.normal.z;
						vert.y = -sinO * v.normal.x + cosO * v.normal.z;

						v.normal.x = vert.x;
						v.normal.z = vert.y;

						vert.x = cosO * v.tangent.x + sinO * v.tangent.z;
						vert.y = -sinO * v.tangent.x + cosO * v.tangent.z;

						v.tangent.x = vert.x;
						v.tangent.z = vert.y;

						// Apply the scale, and move into world space
						v.vertex += float4(0.0, 0.5, 0, 0);
						float4 scale = float4(v.texcoord2.xyx, 1.0);
						v.tangent *= scale;
						v.vertex *= scale;
						v.vertex += float4(pos.xyz, 0);						
					}
				}

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				
				// o.uv = v.texcoord.xy;

				TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)

				return o;
			}

			float4 frag(v2f i) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);				
				clip(tex2D(_MainTex, i.uv).a - _Cutoff);				
				UNITY_APPLY_DITHER_CROSSFADE(i.pos.xy);
				SHADOW_CASTER_FRAGMENT(i)
			}

			ENDCG
		}
	}
	FallBack "Diffuse"
}
