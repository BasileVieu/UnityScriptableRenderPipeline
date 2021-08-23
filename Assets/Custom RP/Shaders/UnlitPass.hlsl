#ifndef CUSTOM_UNLIT_PASS_INCLUDED
#define CUSTOM_UNLIT_PASS_INCLUDED

struct Attributes
{
    float3 positionOS : POSITION;
    float4 color : COLOR;

    #if defined(_FLIPBOOK_BLENDING)
        float4 baseUV : TEXCOORD0;
        float flipbookBlend : TEXCOORD1;
    #else
        float2 baseUV : TEXCOORD0;
    #endif    
    
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS_SS : SV_POSITION;

    #if defined(_VERTEX_COLORS)
        float4 color : VAR_COLOR;
    #endif
    
    float2 baseUV : VAR_BASE_UV;

    #if defined(_FLIPBOOK_BLENDING)
        float3 flipbookUVB : VAR_FLIPBOOK;
    #endif
    
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings UnlitPassVertex(Attributes _input)
{
    Varyings output;
    
    UNITY_SETUP_INSTANCE_ID(_input);
    UNITY_TRANSFER_INSTANCE_ID(_input, output);
    
    float3 positionWS = TransformObjectToWorld(_input.positionOS);
    
    output.positionCS_SS = TransformWorldToHClip(positionWS);

    #if defined(_VERTEX_COLORS)
        output.color = _input.color;
    #endif
    
    output.baseUV.xy = TransformBaseUV(_input.baseUV.xy);

    #if defined(_FLIPBOOK_BLENDING)
        output.flipbookUVB.xy = TransformBaseUV(_input.baseUV.zw);
        output.flipbookUVB.z = _input.flipbookBlend;
    #endif

    return output;
}

float4 UnlitPassFragment(Varyings _input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(_input);

    InputConfig config = GetInputConfig(_input.positionCS_SS, _input.baseUV);

    #if defined(_VERTEX_COLORS)
        config.color = _input.color;
    #endif

    #if defined(_FLIPBOOK_BLENDING)
        config.flipbookUVB = _input.flipbookUVB;
        config.flipbookBlending = true;
    #endif

    #if defined(_NEAR_FADE)
        config.nearFade = true;
    #endif

    #if defined(_SOFT_PARTICLES)
        config.softParticles = true;
    #endif

    float4 base = GetBase(config);
    
    #if defined(_CLIPPING)
		clip(base.a - GetCutoff(config));
    #endif

    #if defined(_DISTORTION)
        float2 distortion = GetDistortion(config) * base.a;
        base.rgb = lerp(GetBufferColor(config.fragment, distortion).rgb, base.rgb, saturate(base.a - GetDistortionBlend(config)));
    #endif
    
    return float4(base.rgb, GteFinalAlpha(base.a));
}

#endif
