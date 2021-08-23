#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED

#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)

TEXTURE2D(_BaseMap);
TEXTURE2D(_DistortionMap);
SAMPLER(sampler_BaseMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float, _NearFadeDistance)
    UNITY_DEFINE_INSTANCED_PROP(float, _NearFadeRange)
    UNITY_DEFINE_INSTANCED_PROP(float, _SoftParticlesDistance)
    UNITY_DEFINE_INSTANCED_PROP(float, _SoftParticlesRange)
    UNITY_DEFINE_INSTANCED_PROP(float, _DistortionStrength)
    UNITY_DEFINE_INSTANCED_PROP(float, _DistortionBlend)
    UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
    UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

struct InputConfig
{
    Fragment fragment;
    float4 color;
    float2 baseUV;
    float3 flipbookUVB;
    bool flipbookBlending;
    bool nearFade;
    bool softParticles;
};

InputConfig GetInputConfig(float4 _positionSS, float2 _baseUV)
{
    InputConfig c;
    c.fragment = GetFragment(_positionSS);
    c.color = 1.0;
    c.baseUV = _baseUV;
    c.flipbookUVB = 0.0;
    c.flipbookBlending = false;
    c.nearFade = false;
    c.softParticles = false;

    return c;
}

float2 TransformBaseUV(float2 _baseUV)
{
    float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
    return _baseUV * baseST.xy + baseST.zw;
}

float GteFinalAlpha(float _alpha)
{
    return INPUT_PROP(_ZWrite) ? 1.0 : _alpha;
}

float4 GetBase(InputConfig _c)
{
    float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, _c.baseUV);

    if (_c.flipbookBlending)
    {
        baseMap = lerp(baseMap, SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, _c.flipbookUVB.xy), _c.flipbookUVB.z);
    }

    if (_c.nearFade)
    {
        float nearAttenuation = (_c.fragment.depth - INPUT_PROP(_NearFadeDistance)) / INPUT_PROP(_NearFadeRange);

        baseMap.a *= saturate(nearAttenuation);
    }

    if (_c.softParticles)
    {
        float depthDelta = _c.fragment.bufferDepth - _c.fragment.depth;
        float nearAttenuation = (depthDelta - INPUT_PROP(_SoftParticlesDistance)) / INPUT_PROP(_SoftParticlesRange);

        baseMap.a *= saturate(nearAttenuation);
    }

    float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
    return baseMap * baseColor * _c.color;
}

float2 GetDistortion(InputConfig _c)
{
    float4 rawMap = SAMPLE_TEXTURE2D(_DistortionMap, sampler_BaseMap, _c.baseUV);

    if (_c.flipbookBlending)
    {
        rawMap = lerp(rawMap, SAMPLE_TEXTURE2D(_DistortionMap, sampler_BaseMap, _c.flipbookUVB.xy), _c.flipbookUVB.z);
    }

    return DecodeNormal(rawMap, INPUT_PROP(_DistortionStrength)).xy;
}

float GetDistortionBlend(InputConfig _c)
{
    return INPUT_PROP(_DistortionBlend);
}

float3 GetEmission(InputConfig _c)
{
    return GetBase(_c).rgb;
}

float GetCutoff(InputConfig _c)
{
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff);
}

float GetMetallic(InputConfig _c)
{
    return 0.0;
}

float GetSmoothness(InputConfig _c)
{
    return 0.0;
}

float GetFresnel(InputConfig _c)
{
    return 0.0;
}

#endif
