#ifndef CUSTOM_POST_FX_PASSES_INCLUDED
#define CUSTOM_POST_FX_PASSES_INCLUDED

TEXTURE2D(_SourceTexture);

struct Varyings
{
    float4 positionCS_SS : SV_POSITION;
    float2 screenUV : VAR_SCREEN_UV;
};

Varyings DefaultPassVertex(uint _vertexID : SV_VERTEXID)
{
    Varyings output;
    output.positionCS_SS = float4(_vertexID <= 1 ? -1.0 : 3.0, _vertexID == 1 ? 3.0 : -1.0, 0.0, 1.0);
    output.screenUV = float2(_vertexID <= 1 ? 0.0 : 2.0, _vertexID == 1 ? 2.0 : 0.0);

    if (_ProjectionParams.x < 0.0)
    {
        output.screenUV.y = 1.0 - output.screenUV.y;
    }

    return output;
}

float4 CopyPassFragment(Varyings _input) : SV_TARGET
{
    return SAMPLE_TEXTURE2D(_SourceTexture, sampler_linear_clamp, _input.screenUV);
}

float CopyDepthPassFragment(Varyings _input) : SV_DEPTH
{
    return SAMPLE_DEPTH_TEXTURE(_SourceTexture, sampler_point_clamp, _input.screenUV);
}

#endif