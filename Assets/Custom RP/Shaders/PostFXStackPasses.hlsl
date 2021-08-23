#ifndef CUSTOM_POST_FX_PASSES_INCLUDED
#define CUSTOM_POST_FX_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

TEXTURE2D(_PostFXSource);
TEXTURE2D(_PostFXSource2);
TEXTURE2D(_ColorGradingLUT);

float4 _PostFXSource_TexelSize;
float4 _BloomThreshold;
float4 _ColorAdjustments;
float4 _ColorFilter;
float4 _WhiteBalance;
float4 _SplitToningShadows;
float4 _SplitToningHighlights;
float4 _ChannelMixerRed;
float4 _ChannelMixerGreen;
float4 _ChannelMixerBlue;
float4 _SMHShadows;
float4 _SMHMidtones;
float4 _SMHHighlights;
float4 _SMHRange;
float4 _ColorGradingLUTParameters;

float _BloomIntensity;

bool _BloomBicubicUpSampling;
bool _ColorGradingLUTInLogC;
bool _CopyBicubic;

struct Varyings
{
    float4 positionCS_SS : SV_POSITION;
    float2 screenUV : VAR_SCREEN_UV;
};

float4 GetSourceTexelSize()
{
    return _PostFXSource_TexelSize;
}

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

float4 GetSource(float2 _screenUV)
{
    return SAMPLE_TEXTURE2D(_PostFXSource, sampler_linear_clamp, _screenUV);
}

float4 GetSource2(float2 _screenUV)
{
    return SAMPLE_TEXTURE2D(_PostFXSource2, sampler_linear_clamp, _screenUV);
}

float4 GetSourceBicubic(float2 _screenUV)
{
    return SampleTexture2DBicubic(TEXTURE2D_ARGS(_PostFXSource, sampler_linear_clamp), _screenUV, _PostFXSource_TexelSize.zwxy, 1.0, 0.0);
}

float4 CopyPassFragment(Varyings _input) : SV_TARGET
{
    return GetSource(_input.screenUV);
}

float3 ApplyBloomThreshold (float3 _color)
{
    float brightness = Max3(_color.r, _color.g, _color.b);
    
    float soft = brightness + _BloomThreshold.y;
    soft = clamp(soft, 0.0, _BloomThreshold.z);
    soft = soft * soft * _BloomThreshold.w;
    
    float contribution = max(soft, brightness - _BloomThreshold.x);
    contribution /= max(brightness, 0.00001);
    
    return _color * contribution;
}

float3 ApplyColorGradingLUT(float3 _color)
{
    return ApplyLut2D(TEXTURE2D_ARGS(_ColorGradingLUT, sampler_linear_clamp), saturate(_ColorGradingLUTInLogC ? LinearToLogC(_color) : _color), _ColorGradingLUTParameters.xyz);
}

float Luminance(float3 _color, bool _useACES)
{
    return _useACES ? AcesLuminance(_color) : Luminance(_color);
}

float3 ColorGradePostExposure(float3 _color)
{
    return _color * _ColorAdjustments.x;
}

float3 ColorGradeWhiteBalance(float3 _color)
{
    _color = LinearToLMS(_color);
    _color *= _WhiteBalance.rgb;

    return LMSToLinear(_color);
}

float3 ColorGradingContrast(float3 _color, bool _useACES)
{
    _color = _useACES ? ACES_to_ACEScc(unity_to_ACES(_color)) : LinearToLogC(_color);
    _color = (_color - ACEScc_MIDGRAY) * _ColorAdjustments.y + ACEScc_MIDGRAY;

    return _useACES ? ACES_to_ACEScg(ACEScc_to_ACES(_color)) : LogCToLinear(_color);
}

float3 ColorGradeColorFilter(float3 _color)
{
    return _color * _ColorFilter.rgb;
}

float3 ColorGradingHueShift(float3 _color)
{
    _color = RgbToHsv(_color);

    float hue = _color.x + _ColorAdjustments.z;
    _color.x = RotateHue(hue, 0.0, 1.0);

    return HsvToRgb(_color);
}

float3 ColorGradingSaturation(float3 _color, bool _useACES)
{
    float luminance = Luminance(_color, _useACES);

    return (_color - luminance) * _ColorAdjustments.w + luminance;
}

float3 ColorGradeSplitToning(float3 _color, bool _useACES)
{
    _color = PositivePow(_color, 1.0 / 2.0);

    float t = saturate(Luminance(saturate(_color), _useACES) + _SplitToningShadows.w);

    float3 shadows = lerp(0.5, _SplitToningShadows.rgb, 1.0 - t);
    float3 highlights = lerp(0.5, _SplitToningHighlights.rgb, t);

    _color = SoftLight(_color, shadows);
    _color = SoftLight(_color, highlights);

    return PositivePow(_color, 2.2);
}

float3 ColorGradingChannelMixer(float3 _color)
{
    return mul(float3x3(_ChannelMixerRed.rgb, _ChannelMixerGreen.rgb, _ChannelMixerBlue.rgb), _color);
}

float3 ColorGradingShadowsMidtonesHighlights(float3 _color, bool _useACES)
{
    float luminance = Luminance(_color, _useACES);
    float shadowsWeight = 1.0 - smoothstep(_SMHRange.x, _SMHRange.y, luminance);
    float highlightsWeight = smoothstep(_SMHRange.z, _SMHRange.w, luminance);
    float midtonesWeight = 1.0 - shadowsWeight - highlightsWeight;

    return _color * _SMHShadows.rgb * shadowsWeight + _color * _SMHMidtones.rgb * midtonesWeight + _color * _SMHHighlights.rgb * highlightsWeight;
}

float3 ColorGrade(float3 _color, bool _useACES = false)
{
    _color = ColorGradePostExposure(_color);
    _color = ColorGradeWhiteBalance(_color);
    _color = ColorGradingContrast(_color, _useACES);
    _color = ColorGradeColorFilter(_color);
    _color = max(_color, 0.0);
    _color = ColorGradeSplitToning(_color, _useACES);
    _color = ColorGradingChannelMixer(_color);
    _color = max(_color, 0.0);
    _color = ColorGradingShadowsMidtonesHighlights(_color, _useACES);
    _color = ColorGradingHueShift(_color);
    _color = ColorGradingSaturation(_color, _useACES);

    return max(_useACES ? ACEScg_to_ACES(_color) : _color, 0.0);
}

float4 BloomAddPassFragment(Varyings _input) : SV_TARGET
{
    float3 lowRes;
    
    if (_BloomBicubicUpSampling)
    {
        lowRes = GetSourceBicubic(_input.screenUV).rgb;
    }
    else
    {
        lowRes = GetSource(_input.screenUV).rgb;
    }
    
    float4 highRes = GetSource2(_input.screenUV);

    return float4(lowRes * _BloomIntensity + highRes.rgb, highRes.a);
}

float4 BloomHorizontalPassFragment(Varyings _input) : SV_TARGET
{
    float3 color = 0.0;
    
    float offsets[] = {-4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0};
    float weights[] = {0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703, 0.19459459, 0.12162162, 0.05405405, 0.01621622};

    for (int i = 0; i < 9; i++)
    {
        float offset = offsets[i] * 2.0 * GetSourceTexelSize().x;
        color += GetSource(_input.screenUV + float2(offset, 0.0)).rgb * weights[i];
    }

    return float4(color, 1.0);
}

float4 BloomVerticalPassFragment(Varyings _input) : SV_TARGET
{
    float3 color = 0.0;
    
    float offsets[] = {-3.23076923, -1.38461538, 0.0, 1.38461538, 3.23076923};
    float weights[] = {0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027};

    for (int i = 0; i < 5; i++)
    {
        float offset = offsets[i] * 2.0 * GetSourceTexelSize().y;
        color += GetSource(_input.screenUV + float2(0.0, offset)).rgb * weights[i];
    }

    return float4(color, 1.0);
}

float4 BloomScatterPassFragment(Varyings _input) : SV_TARGET
{
    float3 lowRes;
    
    if (_BloomBicubicUpSampling)
    {
        lowRes = GetSourceBicubic(_input.screenUV).rgb;
    }
    else
    {
        lowRes = GetSource(_input.screenUV).rgb;
    }
    
    float3 highRes = GetSource2(_input.screenUV).rgb;

    return float4(lerp(highRes, lowRes, _BloomIntensity), 1.0);
}

float4 BloomScatterFinalPassFragment(Varyings _input) : SV_TARGET
{
    float3 lowRes;
    
    if (_BloomBicubicUpSampling)
    {
        lowRes = GetSourceBicubic(_input.screenUV).rgb;
    }
    else
    {
        lowRes = GetSource(_input.screenUV).rgb;
    }
    
    float4 highRes = GetSource2(_input.screenUV);

    lowRes += highRes.rgb - ApplyBloomThreshold(highRes.rgb);

    return float4(lerp(highRes.rgb, lowRes, _BloomIntensity), highRes.a);
}

float4 BloomPrefilterPassFragment (Varyings _input) : SV_TARGET
{
    float3 color = ApplyBloomThreshold(GetSource(_input.screenUV).rgb);
    
    return float4(color, 1.0);
}

float4 BloomPrefilterFirefliesPassFragment (Varyings _input) : SV_TARGET
{
    float3 color = 0.0;

    float weightSum = 0.0;
    
    float2 offsets[] = {float2(0.0, 0.0), float2(-1.0, -1.0), float2(-1.0, 1.0), float2(1.0, -1.0), float2(1.0, 1.0)};

    for (int i = 0; i < 5; i++)
    {
        float3 c = GetSource(_input.screenUV + offsets[i] * GetSourceTexelSize().xy * 2.0).rgb;
        c = ApplyBloomThreshold(c);

        float w = 1.0 / (Luminance(c) + 1.0);
        
        color += c * w;

        weightSum += w;
    }
     
    color /= weightSum;
    
    return float4(color, 1.0);
}

float3 GetColorGradedLUT(float2 _uv, bool _useACES = false)
{
    float3 color = GetLutStripValue(_uv, _ColorGradingLUTParameters);

    return ColorGrade(_ColorGradingLUTInLogC ? LogCToLinear(color) : color, _useACES);
}

float3 ColorGradingNonePassFragment (Varyings _input) : SV_TARGET
{
    float3 color = GetColorGradedLUT(_input.screenUV);
    
    return float4(color, 1.0);
}

float3 ColorGradingACESPassFragment (Varyings _input) : SV_TARGET
{
    float3 color = GetColorGradedLUT(_input.screenUV, true);
    color = AcesTonemap(color);
    
    return float4(color, 1.0);
}

float3 ColorGradingNeutralPassFragment (Varyings _input) : SV_TARGET
{
    float3 color = GetColorGradedLUT(_input.screenUV);
    color = NeutralTonemap(color);
    
    return float4(color, 1.0);
}

float3 ColorGradingReinhardPassFragment (Varyings _input) : SV_TARGET
{
    float3 color = GetColorGradedLUT(_input.screenUV);
    color /= color + 1.0;
    
    return float4(color, 1.0);
}

float4 ApplyColorGradingPassFragment(Varyings _input) : SV_TARGET
{
    float4 color = GetSource(_input.screenUV);
    color.rgb = ApplyColorGradingLUT(color.rgb);

    return color;
}

float4 ApplyColorGradingWithLumaPassFragment(Varyings _input) : SV_TARGET
{
    float4 color = GetSource(_input.screenUV);
    color.rgb = ApplyColorGradingLUT(color.rgb);
    color.a = sqrt(Luminance(color.rgb));

    return color;
}

float4 FinalPassFragmentRescale(Varyings _input) : SV_TARGET
{
    if (_CopyBicubic)
    {
        return GetSourceBicubic(_input.screenUV);
    }
    else
    {
        return GetSource(_input.screenUV);
    }
}

#endif