#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED

#define MAX_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_OTHER_LIGHT_COUNT 64

CBUFFER_START(_CustomLight)
int _DirectionalLightCount;
float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
float4 _DirectionalLightDirectionsAndMasks[MAX_DIRECTIONAL_LIGHT_COUNT];
float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];

int _OtherLightCount;
float4 _OtherLightColors[MAX_OTHER_LIGHT_COUNT];
float4 _OtherLightPositions[MAX_OTHER_LIGHT_COUNT];
float4 _OtherLightDirectionsAndMasks[MAX_OTHER_LIGHT_COUNT];
float4 _OtherLightSpotAngles[MAX_OTHER_LIGHT_COUNT];
float4 _OtherLightShadowData[MAX_OTHER_LIGHT_COUNT];
CBUFFER_END

struct Light
{
    float3 color;
    float3 direction;
    float attenuation;
    uint renderingLayerMask;
};

int GetDirectionalLightCount()
{
    return _DirectionalLightCount;
}

int GetOtherLightCount()
{
    return _OtherLightCount;
}

DirectionalShadowData GetDirectionalShadowData(int _lightIndex, ShadowData _shadowData)
{
    DirectionalShadowData data;
    data.strength = _DirectionalLightShadowData[_lightIndex].x;
    data.tileIndex =
        _DirectionalLightShadowData[_lightIndex].y + _shadowData.cascadeIndex;
    data.normalBias = _DirectionalLightShadowData[_lightIndex].z;
    data.shadowMaskChannel = _DirectionalLightShadowData[_lightIndex].w;
    return data;
}

Light GetDirectionalLight(int _index, Surface _surfaceWS, ShadowData _shadowData)
{
    Light light;
    light.color = _DirectionalLightColors[_index].rgb;
    light.direction = _DirectionalLightDirectionsAndMasks[_index].xyz;
    light.renderingLayerMask = asuint(_DirectionalLightDirectionsAndMasks[_index].w);
    DirectionalShadowData dirShadowData =
        GetDirectionalShadowData(_index, _shadowData);
    light.attenuation =
        GetDirectionalShadowAttenuation(dirShadowData, _shadowData, _surfaceWS);
    return light;
}

OtherShadowData GetOtherShadowData(int _lightIndex)
{
    OtherShadowData data;
    data.strength = _OtherLightShadowData[_lightIndex].x;
    data.tileIndex = _OtherLightShadowData[_lightIndex].y;
    data.isPoint = _OtherLightShadowData[_lightIndex].z == 1.0;
    data.shadowMaskChannel = _OtherLightShadowData[_lightIndex].w;
    data.lightPositionWS = 0.0f;
    data.lightDirectionWS = 0.0f;
    data.spotDirectionWS = 0.0f;

    return data;
}

Light GetOtherLight(int _index, Surface _surfaceWS, ShadowData _shadowData)
{
    Light light;
    light.color = _OtherLightColors[_index].rgb;

    float3 position = _OtherLightPositions[_index].xyz;
    float3 ray = position - _surfaceWS.position;

    light.direction = normalize(ray);

    float distanceSqr = max(dot(ray, ray), 0.00001);
    float rangeAttenuation = Square(saturate(1.0 - Square(distanceSqr * _OtherLightPositions[_index].w)));
    float4 spotAngles = _OtherLightSpotAngles[_index];
    float3 spotDirection = _OtherLightDirectionsAndMasks[_index].xyz;

    light.renderingLayerMask = asuint(_OtherLightDirectionsAndMasks[_index].w);

    float spotAttenuation = Square(saturate(dot(spotDirection, light.direction) * spotAngles.x + spotAngles.y));

    OtherShadowData otherShadowData = GetOtherShadowData(_index);
    otherShadowData.lightPositionWS = position;
    otherShadowData.lightDirectionWS = light.direction;
    otherShadowData.spotDirectionWS = spotDirection;
    
    light.attenuation = GetOtherShadowAttenuation(otherShadowData, _shadowData, _surfaceWS) * spotAttenuation * rangeAttenuation / distanceSqr;

    return light;
}

#endif
