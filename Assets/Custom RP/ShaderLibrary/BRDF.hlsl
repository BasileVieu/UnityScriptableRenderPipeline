#ifndef CUSTOM_BRDF_INCLUDED
#define CUSTOM_BRDF_INCLUDED

struct BRDF
{
    float3 diffuse;
    float3 specular;
    float roughness;
    float perceptualRoughness;
    float fresnel;
};

#define MIN_REFLECTIVITY 0.04

float OneMinusReflectivity(float _metallic)
{
    float range = 1.0 - MIN_REFLECTIVITY;
    return range - _metallic * range;
}

BRDF GetBRDF(Surface _surface, bool _applyAlphaToDiffuse = false)
{
    BRDF brdf;
    float oneMinusReflectivity = OneMinusReflectivity(_surface.metallic);

    brdf.diffuse = _surface.color * oneMinusReflectivity;
    if (_applyAlphaToDiffuse)
    {
        brdf.diffuse *= _surface.alpha;
    }
    brdf.specular = lerp(MIN_REFLECTIVITY, _surface.color, _surface.metallic);

    brdf.perceptualRoughness =
        PerceptualSmoothnessToPerceptualRoughness(_surface.smoothness);
    brdf.roughness = PerceptualRoughnessToRoughness(brdf.perceptualRoughness);
    brdf.fresnel = saturate(_surface.smoothness + 1.0 - oneMinusReflectivity);
    
    return brdf;
}

float SpecularStrength(Surface _surface, BRDF _brdf, Light _light)
{
    float3 h = SafeNormalize(_light.direction + _surface.viewDirection);
    float nh2 = Square(saturate(dot(_surface.normal, h)));
    float lh2 = Square(saturate(dot(_light.direction, h)));
    float r2 = Square(_brdf.roughness);
    float d2 = Square(nh2 * (r2 - 1.0) + 1.00001);
    float normalization = _brdf.roughness * 4.0 + 2.0;
    return r2 / (d2 * max(0.1, lh2) * normalization);
}

float3 DirectBRDF(Surface _surface, BRDF _brdf, Light _light)
{
    return SpecularStrength(_surface, _brdf, _light) * _brdf.specular + _brdf.diffuse;
}

float3 IndirectBRDF(Surface _surface, BRDF _brdf, float3 _diffuse, float3 _specular)
{
    float fresnelStrength = _surface.fresnelStrength * Pow4(1.0 - saturate(dot(_surface.normal, _surface.viewDirection)));

    float3 reflection = _specular * lerp(_brdf.specular, _brdf.fresnel, fresnelStrength);
    reflection /= _brdf.roughness * _brdf.roughness + 1.0;

    return (_diffuse * _brdf.diffuse + reflection) * _surface.occlusion;
}

#endif
