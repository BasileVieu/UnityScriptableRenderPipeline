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

#if defined(_SHADOW_MASK_ALWAYS) || defined(_SHADOW_MASK_DISTANCE)
	#define SHADOWS_SHADOWMASK
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

SAMPLER(sampler_linear_clamp);
SAMPLER(sampler_point_clamp);

bool IsOrthographicCamera()
{
    return unity_OrthoParams.w;
}

float OrthographicDepthBufferToLinear(float _rawDepth)
{
    #if UNITY_REVERSED_Z
        _rawDepth = 1.0 - _rawDepth;
    #endif

    return (_ProjectionParams.z - _ProjectionParams.y) * _rawDepth + _ProjectionParams.y;
}

#include "Fragment.hlsl"

float Square(float _x)
{
    return _x * _x;
}

float DistanceSquared(float3 _pA, float3 _pB)
{
    return dot(_pA - _pB, _pA - _pB);
}

void ClipLOD(Fragment _fragment, float _fade)
{
    #if defined(LOF_FADE_CROSSFADE)
        float dither = InterleaveGradientNoise(_fragment.positionSS, 0);
        clip(_fade + FRONT_FACE_SEMANTIC < 0.0 ? dither : -dither))
    #endif
}

float3 DecodeNormal(float4 _sample, float _scale)
{
    #if defined(UNITY_NO_DXT5nm)
        return UnpackNormalRGB(_sample, _scale);
    #else
        return UnpackNormalmapRGorAG(_sample, _scale);
    #endif
}

float3 NormalTangentToWorld(float3 _normalTS, float3 _normalWS, float4 _tangentWS)
{
    float3x3 tangentToWorld = CreateTangentToWorld(_normalWS, _tangentWS.xyz, _tangentWS.w);

    return TransformTangentToWorld(_normalTS, tangentToWorld);
}

#endif
