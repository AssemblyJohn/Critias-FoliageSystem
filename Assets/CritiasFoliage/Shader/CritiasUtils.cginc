#ifndef _CRITIAS_UTILS_
#define _CRITIAS_UTILS_

#include "UnityCG.cginc"

/*
const float4x4 STD_DITHER_MTX =
	{
		1, 9,  3, 11,
		13, 5, 15, 7,
		4, 12, 2, 10,
		16, 8, 14, 6
	};
*/

// Util for quick calculation of dither values
inline float2 Critias_ScreenPosDitherFast(in float4 vertex)
{
	float4 screenPos = ComputeScreenPos(UnityObjectToClipPos(vertex));
	return ((screenPos.xy / screenPos.w) * _ScreenParams.xy);
}

/**
 * The dither is applied here with the position that is calculated per-vertex for speed
 * and that uses the formula:
 *
 * float4 screenPos = ComputeScreenPos(UnityObjectToClipPos(v.vertex));	// 1. Vertex shader
 * float2 pos = screenPos.xy / screenPos.w;								// 2. Vertex shader or fragment shader
 * pos *= _ScreenParams.xy;												// 3. Vertex shader or fragment shader
 *
 * NOTE: You can also calculate the second and third line in the fragment shader but with an extra computation cost.
 */
inline void Critias_ApplyDitherFast(float2 critiasPos, float fade)
{
	const float DITHER_MATRIX[16] =
	{
		0.058824, 0.529412, 0.176471, 0.647059,
		0.764706, 0.294118, 0.882353, 0.411765,
		0.235294, 0.705882, 0.117647, 0.588235,
		0.941176, 0.470588, 0.823529, 0.352941
	};

	int x = fmod(critiasPos.x, 4);
	int y = fmod(critiasPos.y, 4);

	clip(fade - DITHER_MATRIX[x + y * 4]);
}

/**
 * The dither is applied here with the position that is calculated per-vertex for speed
 * and that uses the formula:
 *
 * float4 screenPos = ComputeScreenPos(UnityObjectToClipPos(v.vertex)); // 1. Vertex shader
 * float2 pos = screenPos.xy / screenPos.w;								// 2. Vertex shader or fragment shader
 * pos *= _ScreenParams.xy;												// 3. Vertex shader or fragment shader
 *
 * NOTE: You can also calculate the second and third line in the fragment shader but with an extra computation cost.
 */
inline void Critias_ApplyDitherInvertedFast(float2 critiasPos, float fade)
{
	const float DITHER_MATRIX_INVERTED[16] =
	{
		0.058824, 0.764706, 0.235294, 0.941176,
		0.529412, 0.294118, 0.705882, 0.470588,
		0.176471, 0.882353, 0.117647, 0.823529,
		0.647059, 0.411765, 0.588235, 0.352941
	};

	int x = fmod(critiasPos.x, 4);
	int y = fmod(critiasPos.y, 4);

	clip(fade - DITHER_MATRIX_INVERTED[x + y * 4]);
}

#endif