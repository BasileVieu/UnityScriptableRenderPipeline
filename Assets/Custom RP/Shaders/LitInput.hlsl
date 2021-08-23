#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED

#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)

TEXTURE2D(_BaseMap);
TEXTURE2D(_NormalMap);
TEXTURE2D(_MaskMap);
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_DetailMap);
TEXTURE2D(_DetailNormalMap);
SAMPLER(sampler_DetailMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
UNITY_DEFINE_INSTANCED_PROP(float4, _DetailMap_ST)
UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite)
UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
UNITY_DEFINE_INSTANCED_PROP(float, _Occlusion)
UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
UNITY_DEFINE_INSTANCED_PROP(float, _Fresnel)
UNITY_DEFINE_INSTANCED_PROP(float, _DetailAlbedo)
UNITY_DEFINE_INSTANCED_PROP(float, _DetailSmoothness)
UNITY_DEFINE_INSTANCED_PROP(float, _NormalScale)
UNITY_DEFINE_INSTANCED_PROP(float, _DetailNormalScale)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

struct InputConfig
{
    Fragment fragment;
    float2 baseUV;
    float2 detailUV;
    bool useMask;
    bool useDetail;
};

InputConfig GetInputConfig(float4 _positionSS, float2 _baseUV, float2 _detailUV = 0.0)
{
    InputConfig c;
    c.fragment = GetFragment(_positionSS);
    c.baseUV = _baseUV;
    c.detailUV = _detailUV;
    c.useMask = false;
    c.useDetail = false;

    return c;
}

float2 TransformBaseUV(float2 _baseUV)
{
    float4 baseST = INPUT_PROP(_BaseMap_ST);
    
    return _baseUV * baseST.xy + baseST.zw;
}

float2 TransformDetailUV(float2 _detailUV)
{
    float4 detailST = INPUT_PROP(_DetailMap_ST);
    
    return _detailUV * detailST.xy + detailST.zw;
}

float GetFinalAlpha(float _alpha)
{
    return INPUT_PROP(_ZWrite) ? 1.0 : _alpha;
}

float4 GetDetail(InputConfig _c)
{
    if (_c.useDetail)
    {
        float4 map = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, _c.detailUV);

        return map * 2.0 - 1.0;
    }

    return 0.0;
}

float4 GetMask(InputConfig _c)
{
    if (_c.useMask)
    {
        return SAMPLE_TEXTURE2D(_MaskMap, sampler_BaseMap, _c.baseUV);
    }

    return 1.0;
}

float4 GetBase(InputConfig _c)
{
    float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, _c.baseUV);
    float4 color = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);

    if (_c.useDetail)
    {
        float detail = GetDetail(_c).r * INPUT_PROP(_DetailAlbedo);

        float mask = GetMask(_c).b;

        map.rgb = lerp(sqrt(map.rgb), detail < 0.0 ? 0.0 : 1.0, abs(detail) * mask);
        map.rgb *= map.rgb;
    }
    
    return map * color;
}

float3 GetEmission(InputConfig _c)
{
    float4 map = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, _c.baseUV);
    float4 color = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EmissionColor);
    
    return map.rgb * color.rgb;
}

float GetCutoff(InputConfig _c)
{
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff);
}

float GetMetallic(InputConfig _c)
{
    float metallic = INPUT_PROP(_Metallic);
    metallic *= GetMask(_c).r;

    return metallic;
}

float GetSmoothness(InputConfig _c)
{
    float smoothness = INPUT_PROP(_Smoothness);
    smoothness *= GetMask(_c).a;

    if (_c.useDetail)
    {
        float detail = GetDetail(_c).b * INPUT_PROP(_DetailSmoothness);

        float mask = GetMask(_c).b;

        smoothness = lerp(smoothness, detail < 0.0 ? 0.0 : 1.0, abs(detail) * mask);
    }

    return smoothness;
}

float GetFresnel(InputConfig _c)
{
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Fresnel);
}

float GetOcclusion(InputConfig _c)
{
    float strength = INPUT_PROP(_Occlusion);
    
    float occlusion = GetMask(_c).g;
    occlusion = lerp(occlusion, 1.0, strength);

    return occlusion;
}

float3 GetNormalTS(InputConfig _c)
{
    float4 map = SAMPLE_TEXTURE2D(_NormalMap, sampler_BaseMap, _c.baseUV);

    float scale = INPUT_PROP(_NormalScale);

    float3 normal = DecodeNormal(map, scale);

    if (_c.useDetail)
    {
        map = SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailMap, _c.detailUV);
    
        scale = INPUT_PROP(_DetailNormalScale) * GetMask(_c).b;

        float3 detail = DecodeNormal(map, scale);

        normal = BlendNormalRNM(normal, detail);
    }

    return normal;
}

#endif
