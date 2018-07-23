Shader "Critias/NullShader" {
	Properties {
		
	}
	SubShader {
		Tags 
		{ 
			"RenderType" = "Transparent" 
			"Queue" = "Transparent+1"
		}
		LOD 200
		
		CGPROGRAM		
		#pragma surface surf Standard noshadow
		
		#pragma target 3.0

		struct Input {
			float2 uv_MainTex;
		};		

		void surf (Input IN, inout SurfaceOutputStandard o) {
			clip(-1);
		}
		ENDCG
	}
	FallBack "Diffuse"
}
