// Upgrade NOTE: removed variant '__' where variant LOD_FADE_PERCENTAGE is used.

// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)

Shader "Critias/WindTree_Grass"
{
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _HueVariation ("Hue Variation", Color) = (1.0,0.5,0.0,0.1)
        _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
        _DetailTex ("Detail", 2D) = "black" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.333
        [MaterialEnum(Off,0,Front,1,Back,2)] _Cull ("Cull", Int) = 2
        [MaterialEnum(None,0,Fastest,1,Fast,2,Better,3,Best,4,Palm,5)] _WindQuality ("Wind Quality", Range(0,5)) = 0

		CRITIAS_MaxFoliageTypeDistance("Max Foliage Type Distance Default", Float) = 1000
    }

    // targeting SM3.0+
    SubShader
    {
        Tags
        {
            "Queue"="Geometry"
            "IgnoreProjector"="True"
            "RenderType"="Opaque"
            "DisableBatching"="LODFading"
        }
        LOD 400
        Cull [_Cull]

        CGPROGRAM
            #pragma surface surf Lambert vertex:SpeedTreeVert_Grass nodirlightmap nodynlightmap noshadowmask dithercrossfade
            #pragma target 3.0
            #pragma multi_compile_vertex  LOD_FADE_PERCENTAGE
            #pragma instancing_options assumeuniformscaling lodfade maxcount:50 procedural:Critias_FoliageSetup			
            #pragma shader_feature GEOM_TYPE_BRANCH GEOM_TYPE_BRANCH_DETAIL GEOM_TYPE_FROND GEOM_TYPE_LEAF GEOM_TYPE_MESH
            #pragma shader_feature EFFECT_BUMP
            #pragma shader_feature EFFECT_HUE_VARIATION
			#pragma shader_feature CRITIAS_DISTANCE_BEND
            #define ENABLE_WIND

			#define DISTANCE_SCALE_BIAS 3.0
			#define DISTANCE_SCALE_BIAS_SQR 9.0		

            #include "SpeedTreeCommon.cginc"
			
			float CRITIAS_MaxFoliageTypeDistance;
			float CRITIAS_MaxFoliageTypeDistanceSqr;

			#ifdef CRITIAS_DISTANCE_BEND
			float3 CRITIAS_Bend_Position;
			float CRITIAS_Bend_Distance;
			float CRITIAS_Bend_Scale;
			#endif

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


			void SpeedTreeVert_Grass(inout SpeedTreeVB IN, out Input OUT)
			{							
				float3 pos = float3(unity_ObjectToWorld[0].w, unity_ObjectToWorld[1].w, unity_ObjectToWorld[2].w);

				float dist = distance(pos, _WorldSpaceCameraPos);
				float maxDist = CRITIAS_MaxFoliageTypeDistance - DISTANCE_SCALE_BIAS;

				if(dist <= CRITIAS_MaxFoliageTypeDistance)
				{
					SpeedTreeVert(IN, OUT);

					if (dist > maxDist)
					{
						dist = 1.0 - clamp(dist - maxDist, 0.0, DISTANCE_SCALE_BIAS) / DISTANCE_SCALE_BIAS;
						IN.vertex.y *= dist;
					}

					#ifdef CRITIAS_DISTANCE_BEND
					// Bend the grass based on the provided distance and position
					float bendDist = distance(pos, CRITIAS_Bend_Position);

					if (bendDist < CRITIAS_Bend_Distance)
					{
						// We assume a maximum height of 3 meters
						float ratioY = IN.vertex.y * 0.3;

						// Get the displace direction
						float3 directionDisplacement = normalize(float3(pos.x - CRITIAS_Bend_Position.x, 0, pos.z - CRITIAS_Bend_Position.z));

						// Bend it
						float ratioDampened = 1.0 - bendDist / CRITIAS_Bend_Distance;
						IN.vertex.xyz += (directionDisplacement * ratioY * ratioDampened * CRITIAS_Bend_Scale);
					}
					#endif
				}
				else
				{
					UNITY_INITIALIZE_OUTPUT(Input, OUT);
					IN.vertex = half4(0, 0, 0, 0);
				}
			}
			
            void surf(Input IN, inout SurfaceOutput OUT)
            {
                SpeedTreeFragOut o;
                SpeedTreeFrag(IN, o);
                SPEEDTREE_COPY_FRAG(OUT, o)
            }
        ENDCG

        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }

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

				#include "../../CritiasUtils.cginc"
                #include "SpeedTreeCommon.cginc"

				float CRITIAS_MaxFoliageTypeDistance;
				float CRITIAS_MaxFoliageTypeDistanceSqr;

                struct v2f
                {
                    V2F_SHADOW_CASTER;
                    #ifdef SPEEDTREE_ALPHATEST
                        float2 uv : TEXCOORD1;
                    #endif

					#ifndef UNITY_PROCEDURAL_INSTANCING_ENABLED
					float2 screenPosOptimized : TEXCOORD2;
					half transparency : TEXCOORD3;
					#endif

                    UNITY_VERTEX_INPUT_INSTANCE_ID
                    UNITY_VERTEX_OUTPUT_STEREO
                };

                v2f vert(SpeedTreeVB v)
                {					
                    v2f o;
                    UNITY_SETUP_INSTANCE_ID(v);
                    UNITY_TRANSFER_INSTANCE_ID(v, o);
                    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                    #ifdef SPEEDTREE_ALPHATEST
                        o.uv = v.texcoord.xy;
                    #endif

					#ifndef UNITY_PROCEDURAL_INSTANCING_ENABLED
					// Calculate the transparency and lod offset
					float3 campos = _WorldSpaceCameraPos;
					float3 pos = float3(unity_ObjectToWorld[0].w, unity_ObjectToWorld[1].w, unity_ObjectToWorld[2].w);

					float dist = distance(pos, campos);

					if (dist > CRITIAS_MaxFoliageTypeDistance - DISTANCE_SCALE_BIAS)
						o.transparency = (CRITIAS_MaxFoliageTypeDistance - dist) / DISTANCE_SCALE_BIAS;
					else
						o.transparency = 1;

					// No need for this here
					/*
					float lodOffset = 0;

					if (dist > CRITIAS_FoliageMaxDistanceLOD - DISTANCE_SCALE_BIAS)
						lodOffset = 1.0f - ((CRITIAS_FoliageMaxDistanceLOD - dist) / DISTANCE_SCALE_BIAS);

					lodOffset = saturate(lodOffset);

					// Offset the shadow so that we don't have shadow popping
					OffsetSpeedTreeVertex(v, lodOffset);
					*/
					OffsetSpeedTreeVertex(v, unity_LODFade.x);
					#endif

                    TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)

					#ifndef UNITY_PROCEDURAL_INSTANCING_ENABLED
					o.screenPosOptimized = Critias_ScreenPosDitherFast(v.vertex);
					#endif

                    return o;
                }

                float4 frag(v2f i) : SV_Target
                {
                    UNITY_SETUP_INSTANCE_ID(i);
                    #ifdef SPEEDTREE_ALPHATEST
                        clip(tex2D(_MainTex, i.uv).a * _Color.a - _Cutoff);
                    #endif

					// UNITY_APPLY_DITHER_CROSSFADE(i.pos.xy);

					// Apply our fast dither
					Critias_ApplyDitherFast(i.screenPosOptimized, i.transparency);

                    SHADOW_CASTER_FRAGMENT(i)
                }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode" = "Vertex" }

            CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma target 3.0
                #pragma multi_compile_fog
                #pragma multi_compile_vertex  LOD_FADE_PERCENTAGE LOD_FADE_CROSSFADE
                #pragma multi_compile_fragment __ LOD_FADE_CROSSFADE
                #pragma multi_compile_instancing
                #pragma instancing_options assumeuniformscaling lodfade maxcount:50
                #pragma shader_feature GEOM_TYPE_BRANCH GEOM_TYPE_BRANCH_DETAIL GEOM_TYPE_FROND GEOM_TYPE_LEAF GEOM_TYPE_MESH
                #pragma shader_feature EFFECT_HUE_VARIATION
                #define ENABLE_WIND
                #include "SpeedTreeCommon.cginc"

                struct v2f
                {
#if UNITY_VERSION <= 563
				UNITY_POSITION(vertex);
#else
				float4 vertex	: SV_POSITION;
#endif                    
                    UNITY_FOG_COORDS(0)
                    Input data      : TEXCOORD1;
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                    UNITY_VERTEX_OUTPUT_STEREO
                };

                v2f vert(SpeedTreeVB v)
                {
                    v2f o;
                    UNITY_SETUP_INSTANCE_ID(v);
                    UNITY_TRANSFER_INSTANCE_ID(v, o);
                    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

					#ifndef UNITY_PROCEDURAL_INSTANCING_ENABLED
                    SpeedTreeVert(v, o.data);
                    o.data.color.rgb *= ShadeVertexLightsFull(v.vertex, v.normal, 4, true);
                    o.vertex = UnityObjectToClipPos(v.vertex);
					#endif

                    UNITY_TRANSFER_FOG(o,o.vertex);
                    return o;
                }

                fixed4 frag(v2f i) : SV_Target
                {
                    UNITY_SETUP_INSTANCE_ID(i);

					#ifndef UNITY_PROCEDURAL_INSTANCING_ENABLED
                    SpeedTreeFragOut o;
                    SpeedTreeFrag(i.data, o);
					#endif

                    UNITY_APPLY_DITHER_CROSSFADE(i.vertex.xy);
                    fixed4 c = fixed4(o.Albedo, o.Alpha);
                    UNITY_APPLY_FOG(i.fogCoord, c);
                    return c;
                }
            ENDCG
        }
    }

    // targeting SM2.0: Normal-mapping, Hue variation and Wind animation are turned off for less instructions
    SubShader
    {
        Tags
        {
            "Queue"="Geometry"
            "IgnoreProjector"="True"
            "RenderType"="Opaque"
            "DisableBatching"="LODFading"
        }
        LOD 400
        Cull [_Cull]

        CGPROGRAM
            #pragma surface surf Lambert vertex:SpeedTreeVert nodirlightmap nodynlightmap noshadowmask
            #pragma multi_compile_vertex  LOD_FADE_PERCENTAGE
            #pragma shader_feature GEOM_TYPE_BRANCH GEOM_TYPE_BRANCH_DETAIL GEOM_TYPE_FROND GEOM_TYPE_LEAF GEOM_TYPE_MESH
            #include "SpeedTreeCommon.cginc"

            void surf(Input IN, inout SurfaceOutput OUT)
            {
                SpeedTreeFragOut o;
                SpeedTreeFrag(IN, o);
                SPEEDTREE_COPY_FRAG(OUT, o)
            }
        ENDCG

        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }

            CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma multi_compile_vertex  LOD_FADE_PERCENTAGE
                #pragma shader_feature GEOM_TYPE_BRANCH GEOM_TYPE_BRANCH_DETAIL GEOM_TYPE_FROND GEOM_TYPE_LEAF GEOM_TYPE_MESH
                #pragma multi_compile_shadowcaster
                #include "SpeedTreeCommon.cginc"

                struct v2f
                {
                    V2F_SHADOW_CASTER;
                    #ifdef SPEEDTREE_ALPHATEST
                        float2 uv : TEXCOORD1;
                    #endif
                };

                v2f vert(SpeedTreeVB v)
                {
                    v2f o;
                    #ifdef SPEEDTREE_ALPHATEST
                        o.uv = v.texcoord.xy;
                    #endif
                    OffsetSpeedTreeVertex(v, unity_LODFade.x);
                    TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                    return o;
                }

                float4 frag(v2f i) : SV_Target
                {
                    #ifdef SPEEDTREE_ALPHATEST
                        clip(tex2D(_MainTex, i.uv).a * _Color.a - _Cutoff);
                    #endif
                    SHADOW_CASTER_FRAGMENT(i)
                }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode" = "Vertex" }

            CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma multi_compile_fog
                #pragma multi_compile_vertex  LOD_FADE_PERCENTAGE
                #pragma shader_feature GEOM_TYPE_BRANCH GEOM_TYPE_BRANCH_DETAIL GEOM_TYPE_FROND GEOM_TYPE_LEAF GEOM_TYPE_MESH
                #include "SpeedTreeCommon.cginc"

                struct v2f
                {
                    UNITY_POSITION(vertex);
                    UNITY_FOG_COORDS(0)
                    Input data      : TEXCOORD1;
                    UNITY_VERTEX_OUTPUT_STEREO
                };

                v2f vert(SpeedTreeVB v)
                {
                    v2f o;
                    UNITY_SETUP_INSTANCE_ID(v);
                    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                    SpeedTreeVert(v, o.data);
                    o.data.color.rgb *= ShadeVertexLightsFull(v.vertex, v.normal, 2, false);
                    o.vertex = UnityObjectToClipPos(v.vertex);
                    UNITY_TRANSFER_FOG(o,o.vertex);
                    return o;
                }

                fixed4 frag(v2f i) : SV_Target
                {
                    SpeedTreeFragOut o;
                    SpeedTreeFrag(i.data, o);
                    fixed4 c = fixed4(o.Albedo, o.Alpha);
                    UNITY_APPLY_FOG(i.fogCoord, c);
                    return c;
                }
            ENDCG
        }
    }

    FallBack "Transparent/Cutout/VertexLit"
    CustomEditor "SpeedTreeMaterialInspector"
}
