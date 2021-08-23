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

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
TEXTURE2D_SHADOW(_OtherShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
int _CascadeCount;
float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
float4 _CascadeData[MAX_CASCADE_COUNT];
float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
float4x4 _OtherShadowMatrices[MAX_SHADOWED_OTHER_LIGHT_COUNT];
float4 _OtherShadowTiles[MAX_SHADOWED_OTHER_LIGHT_COUNT];
float4 _ShadowAtlasSize;
float4 _ShadowDistanceFade;
CBUFFER_END

struct ShadowMask
{
    bool always;
    bool distance;
    float4 shadows;
};

struct ShadowData
{
    int cascadeIndex;
    float cascadeBlend;
    float strength;
    ShadowMask shadowMask;
};

struct DirectionalShadowData
{
    float strength;
    int tileIndex;
    float normalBias;
    int shadowMaskChannel;
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

static const float3 pointShadowPlanes[6] =
{
    float3(-1.0, 0.0, 0.0),
    float3(1.0, 0.0, 0.0),
    float3(0.0, -1.0, 0.0),
    float3(0.0, 1.0, 0.0),
    float3(0.0, 0.0, -1.0),
    float3(0.0, 0.0, 1.0)
};

float GetBakedShadow(ShadowMask mask, int channel)
{
    float shadow = 1.0;
    if (mask.always || mask.distance)
    {
        if (channel >= 0)
        {
            shadow = mask.shadows[channel];
        }
    }
    
    return shadow;
}

float GetBakedShadow(ShadowMask mask, int channel, float strength)
{
    if (mask.always || mask.distance)
    {
        return lerp(1.0, GetBakedShadow(mask, channel), strength);
    }
    
    return 1.0;
}

float MixBakedAndRealtimeShadows(
    ShadowData global, float shadow, int shadowMaskChannel, float strength
)
{
    float baked = GetBakedShadow(global.shadowMask, shadowMaskChannel);
    if (global.shadowMask.always)
    {
        shadow = lerp(1.0, shadow, global.strength);
        shadow = min(baked, shadow);
        return lerp(1.0, shadow, strength);
    }
    if (global.shadowMask.distance)
    {
        shadow = lerp(baked, shadow, global.strength);
        return lerp(1.0, shadow, strength);
    }
    return lerp(1.0, shadow, strength * global.strength);
}

float FadedShadowStrength(float distance, float scale, float fade)
{
    return saturate((1.0 - distance * scale) * fade);
}

ShadowData GetShadowData(Surface surfaceWS)
{
    ShadowData data;
    data.shadowMask.always = false;
    data.shadowMask.distance = false;
    data.shadowMask.shadows = 1.0;
    data.cascadeBlend = 1.0;
    data.strength = FadedShadowStrength(
        surfaceWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y
    );
    int i;
    for (i = 0; i < _CascadeCount; i++)
    {
        float4 sphere = _CascadeCullingSpheres[i];
        float distanceSqr = DistanceSquared(surfaceWS.position, sphere.xyz);
        if (distanceSqr < sphere.w)
        {
            float fade = FadedShadowStrength(
                distanceSqr, _CascadeData[i].x, _ShadowDistanceFade.z
            );
            if (i == _CascadeCount - 1)
            {
                data.strength *= fade;
            }
            else
            {
                data.cascadeBlend = fade;
            }
            break;
        }
    }

    if (i == _CascadeCount
        && _CascadeCount > 0)
    {
        data.strength = 0.0;
    }
    
    #if defined(_CASCADE_BLEND_DITHER)
		else if (data.cascadeBlend < surfaceWS.dither) {
			i += 1;
		}
    #endif
    
    #if !defined(_CASCADE_BLEND_SOFT)
        data.cascadeBlend = 1.0;
    #endif
    
    data.cascadeIndex = i;
    return data;
}

float SampleDirectionalShadowAtlas(float3 _positionSTS)
{
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, _positionSTS);
}

float FilterDirectionalShadow(float3 _positionSTS)
{
    #if defined(DIRECTIONAL_FILTER_SETUP)
		float weights[DIRECTIONAL_FILTER_SAMPLES];
		float2 positions[DIRECTIONAL_FILTER_SAMPLES];
		float4 size = _ShadowAtlasSize.yyxx;
		DIRECTIONAL_FILTER_SETUP(size, _positionSTS.xy, weights, positions);
		float shadow = 0;
		for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++) {
			shadow += weights[i] * SampleDirectionalShadowAtlas(
				float3(positions[i].xy, _positionSTS.z)
			);
		}
		return shadow;
    #else
    return SampleDirectionalShadowAtlas(_positionSTS);
    #endif
}

float SampleOtherShadowAtlas(float3 _positionSTS, float3 _bounds)
{
    _positionSTS.xy = clamp(_positionSTS.xy, _bounds.xy, _bounds.xy + _bounds.z);

    return SAMPLE_TEXTURE2D_SHADOW(_OtherShadowAtlas, SHADOW_SAMPLER, _positionSTS);
}

float FilterOtherShadow(float3 _positionSTS, float3 _bounds)
{
    #if defined(OTHER_FILTER_SETUP)
    float weights[OTHER_FILTER_SAMPLES];
    float2 positions[OTHER_FILTER_SAMPLES];
    float4 size = _ShadowAtlasSize.wwzz;
    OTHER_FILTER_SETUP(size, _positionSTS.xy, weights, positions);
    float shadow = 0;
    for (int i = 0; i < OTHER_FILTER_SAMPLES; i++) {
        shadow += weights[i] * SampleOtherShadowAtlas(float3(positions[i].xy, _positionSTS.z), _bounds);
    }
    return shadow;
    #else
    return SampleOtherShadowAtlas(_positionSTS, _bounds);
    #endif
}

float GetCascadedShadow(DirectionalShadowData _directional, ShadowData _global, Surface _surfaceWS)
{
    float3 normalBias = _surfaceWS.interpolatedNormal * (_directional.normalBias * _CascadeData[_global.cascadeIndex].y);
    float3 positionSTS = mul(_DirectionalShadowMatrices[_directional.tileIndex], float4(_surfaceWS.position + normalBias, 1.0)).xyz;
    
    float shadow = FilterDirectionalShadow(positionSTS);
    
    if (_global.cascadeBlend < 1.0)
    {
        normalBias = _surfaceWS.interpolatedNormal * (_directional.normalBias * _CascadeData[_global.cascadeIndex + 1].y);
        positionSTS = mul(_DirectionalShadowMatrices[_directional.tileIndex + 1], float4(_surfaceWS.position + normalBias, 1.0)).xyz;
        shadow = lerp(FilterDirectionalShadow(positionSTS), shadow, _global.cascadeBlend);
    }
    
    return shadow;
}

float GetDirectionalShadowAttenuation(DirectionalShadowData _directional, ShadowData _global, Surface _surfaceWS)
{
    #if !defined(_RECEIVE_SHADOWS)
    return 1.0;
    #endif

    float shadow;
    if (_directional.strength * _global.strength <= 0.0)
    {
        shadow = GetBakedShadow(
            _global.shadowMask, _directional.shadowMaskChannel,
            abs(_directional.strength)
        );
    }
    else
    {
        shadow = GetCascadedShadow(_directional, _global, _surfaceWS);
        shadow = MixBakedAndRealtimeShadows(
            _global, shadow, _directional.shadowMaskChannel, _directional.strength
        );
    }
    return shadow;
}

float GetOtherShadow(OtherShadowData _other, ShadowData _global, Surface _surfaceWS)
{
    float tileIndex = _other.tileIndex;

    float3 lightPlane = _other.spotDirectionWS;

    if (_other.isPoint)
    {
        float faceOffset = CubeMapFaceID(-_other.lightDirectionWS);

        tileIndex += faceOffset;

        lightPlane = pointShadowPlanes[faceOffset];
    }

    float4 tileData = _OtherShadowTiles[tileIndex];

    float3 surfaceToLight = _other.lightPositionWS - _surfaceWS.position;

    float distanceToLightPlane = dot(surfaceToLight, lightPlane);

    float3 normalBias = _surfaceWS.interpolatedNormal * (distanceToLightPlane * tileData.w);

    float4 positionSTS = mul(_OtherShadowMatrices[tileIndex], float4(_surfaceWS.position + normalBias, 1.0));

    return FilterOtherShadow(positionSTS.xyz / positionSTS.w, tileData.xyz);
}

float GetOtherShadowAttenuation(OtherShadowData _other, ShadowData _global, Surface _surfaceWS)
{
    #if !defined(_RECEIVE_SHADOWS)
        return 1.0;
    #endif

    float shadow;

    if (_other.strength * _global.strength <= 0.0)
    {
        shadow = GetBakedShadow(_global.shadowMask, _other.shadowMaskChannel, abs(_other.strength));
    }
    else
    {
        shadow = GetOtherShadow(_other, _global, _surfaceWS);
        shadow = MixBakedAndRealtimeShadows(_global, shadow, _other.shadowMaskChannel, _other.strength);
    }

    return shadow;
}

#endif
