#ifndef FRAGMENT_INCLUDED
#define FRAGMENT_INCLUDED

TEXTURE2D(_CameraColorTexture);
TEXTURE2D(_CameraDepthTexture);

float4 _CameraBufferSize;

struct Fragment
{
    float2 positionSS;
    float2 screenUV;
    float depth;
    float bufferDepth;
};

Fragment GetFragment(float4 _positionSS)
{
    Fragment f;
    f.positionSS = _positionSS.xy;
    f.screenUV = f.positionSS * _CameraBufferSize.xy;
    f.depth = IsOrthographicCamera() ? OrthographicDepthBufferToLinear(_positionSS.z) : _positionSS.w;
    f.bufferDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_point_clamp, f.screenUV);
    f.bufferDepth = IsOrthographicCamera() ? OrthographicDepthBufferToLinear(f.bufferDepth) : LinearEyeDepth(f.bufferDepth, _ZBufferParams);

    return f;
}

float4 GetBufferColor(Fragment _fragment, float2 _uvOffset = float2(0.0, 0.0))
{
    float2 uv = _fragment.screenUV + _uvOffset;

    return SAMPLE_TEXTURE2D(_CameraColorTexture, sampler_linear_clamp, uv);
}

#endif