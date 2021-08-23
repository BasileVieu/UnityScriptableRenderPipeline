#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

bool RenderingLayersOverlap(Surface _surface, Light _light)
{
    return (_surface.renderingLayerMask & _light.renderingLayerMask) != 0;
}

float3 IncomingLight(Surface _surface, Light _light)
{
    return saturate(dot(_surface.normal, _light.direction) * _light.attenuation) * _light.color;
}

float3 GetLighting(Surface _surface, BRDF _brdf, Light _light)
{
    return IncomingLight(_surface, _light) * DirectBRDF(_surface, _brdf, _light);
}

float3 GetLighting(Surface _surfaceWS, BRDF _brdf, GI _gi)
{
    ShadowData shadowData = GetShadowData(_surfaceWS);
    shadowData.shadowMask = _gi.shadowMask;

    float3 color = IndirectBRDF(_surfaceWS, _brdf, _gi.diffuse, _gi.specular);
    
    for (int i = 0; i < GetDirectionalLightCount(); i++)
    {
        Light light = GetDirectionalLight(i, _surfaceWS, shadowData);

        if (RenderingLayersOverlap(_surfaceWS, light))
        {
            color += GetLighting(_surfaceWS, _brdf, light);
        }
    }

    #if defined(_LIGHTS_PER_OBJECTS)
        for (int j = 0; j < min(unity_LightData.y, 8); j++)
        {
            int lightIndex = unity_LightIndices[j / 4][j % 4];
            Light light = GetOtherLight(lightIndex, _surfaceWS, shadowData);

            if (RenderingLayersOverlap(_surfaceWS, light))
            {
                color += GetLighting(_surfaceWS, _brdf, light);
            }
        }
    #else
        for (int j = 0; j < GetOtherLightCount(); j++)
        {
            Light light = GetOtherLight(j, _surfaceWS, shadowData);

            if (RenderingLayersOverlap(_surfaceWS, light))
            {
                color += GetLighting(_surfaceWS, _brdf, light);
            }
        }
    #endif
    
    return color;
}

#endif
