#ifndef CUSTOM_FXAA_PASS_INCLUDED
#define CUSTOM_FXAA_PASS_INCLUDED

#if defined(FXAA_QUALITY_LOW)
    #define EXTRA_EDGE_STEPS 3
    #define EDGE_STEP_SIZES 1.5, 2.0, 2.0
    #define LAST_EDGE_STEP_GUESS 8.0
#elif defined(FXAA_QUALITY_MEDIUM)
    #define EXTRA_EDGE_STEPS 8
    #define EDGE_STEP_SIZES 1.5, 2.0, 2.0, 2.0, 2.0, 2.0, 2.0, 4.0
    #define LAST_EDGE_STEP_GUESS 8.0
#else
    #define EXTRA_EDGE_STEPS 10
    #define EDGE_STEP_SIZES 1.0, 1.0, 1.0, 1.0, 1.5, 2.0, 2.0, 2.0, 2.0, 4.0
    #define LAST_EDGE_STEP_GUESS 8.0
#endif

static const float edgeStepSizes[EXTRA_EDGE_STEPS] = { EDGE_STEP_SIZES };

float4 _FXAAConfig;

struct LumaNeighborhood
{
    float m;
    float n;
    float e;
    float s;
    float w;
    float ne;
    float se;
    float sw;
    float nw;
    float highest;
    float lowest;
    float range;
};

struct FXAAEdge
{
    bool isHorizontal;
    float pixelStep;
    float lumaGradient;
    float otherLuma;
};

float GetLuma(float2 _uv, float _uOffset = 0.0f, float _vOffset = 0.0f)
{
    _uv += float2(_uOffset, _vOffset) * GetSourceTexelSize().xy;

    #if defined(FXAA_ALPHA_CONTAINS_LUMA)
        return GetSource(_uv).a;
    #else
        return GetSource(_uv).g;
    #endif
}

LumaNeighborhood GetLumaNeighborhood(float2 _uv)
{
    LumaNeighborhood luma;
    luma.m = GetLuma(_uv);
    luma.n = GetLuma(_uv, 0.0f, 1.0f);
    luma.e = GetLuma(_uv, 1.0f, 0.0f);
    luma.s = GetLuma(_uv, 0.0f, -1.0f);
    luma.w = GetLuma(_uv, -1.0f, 0.0f);
    luma.ne = GetLuma(_uv, 1.0f, 1.0f);
    luma.se = GetLuma(_uv, 1.0f, -1.0f);
    luma.sw = GetLuma(_uv, -1.0f, -1.0f);
    luma.nw = GetLuma(_uv, -1.0f, 1.0f);
    luma.highest = max(max(max(max(luma.m, luma.n), luma.e), luma.s), luma.w);
    luma.lowest = min(min(min(min(luma.m, luma.n), luma.e), luma.s), luma.w);
    luma.range = luma.highest - luma.lowest;

    return luma;
}

bool IsHorizontalEdge(LumaNeighborhood _luma)
{
    float horizontal = 2.0f * abs(_luma.n + _luma.s - 2.0f * _luma.m) +
        abs(_luma.ne + _luma.se - 2.0f * _luma.e) +
        abs(_luma.nw + _luma.sw - 2.0f * _luma.w);
    float vertical = 2.0f * abs(_luma.e + _luma.w - 2.0f * _luma.m) +
        abs(_luma.ne + _luma.nw - 2.0f * _luma.n) +
        abs(_luma.se + _luma.sw - 2.0f * _luma.s);

    return horizontal >= vertical;
}

FXAAEdge GetFXAAEdge(LumaNeighborhood _luma)
{
    FXAAEdge edge;
    edge.isHorizontal = IsHorizontalEdge(_luma);

    float lumaP;
    float lumaN;

    if (edge.isHorizontal)
    {
        edge.pixelStep = GetSourceTexelSize().y;
        lumaP = _luma.n;
        lumaN = _luma.s;
    }
    else
    {
        edge.pixelStep = GetSourceTexelSize().x;
        lumaP = _luma.e;
        lumaN = _luma.w;
    }

    float gradientP = abs(lumaP - _luma.m);
    float gradientN = abs(lumaN - _luma.m);

    if (gradientP < gradientN)
    {
        edge.pixelStep = -edge.pixelStep;
        edge.lumaGradient = gradientN;
        edge.otherLuma = lumaN;
    }
    else
    {
        edge.lumaGradient = gradientP;
        edge.otherLuma = lumaP;
    }

    return edge;
}

bool CanSkipFXAA(LumaNeighborhood _luma)
{
    return _luma.range < max(_FXAAConfig.x, _FXAAConfig.y * _luma.highest);
}

float GetSubpixelBlendFactor(LumaNeighborhood _luma)
{
    float filter = 2.0f * (_luma.n + _luma.e + _luma.s + _luma.w);
    filter += _luma.ne + _luma.nw + _luma.se + _luma.sw;
    filter *= 1.0f / 12.0f;
    filter = abs(filter - _luma.m);
    filter = saturate(filter / _luma.range);
    filter = smoothstep(0, 1, filter);

    return filter * filter * _FXAAConfig.z;
}

float GetEdgeBlendFactor(LumaNeighborhood _luma, FXAAEdge _edge, float2 _uv)
{
    float2 edgeUV = _uv;
    float2 uvStep = 0.0f;

    if (_edge.isHorizontal)
    {
        edgeUV.y += 0.5f * _edge.pixelStep;
        uvStep.x = GetSourceTexelSize().x;
    }
    else
    {
        edgeUV.x += 0.5f * _edge.pixelStep;
        uvStep.y = GetSourceTexelSize().y;
    }

    float edgeLuma = 0.5f * (_luma.m + _edge.otherLuma);
    float gradientThreshold = 0.25f * _edge.lumaGradient;

    float2 uvP = edgeUV + uvStep;
    
    float lumaDeltaP = GetLuma(uvP) - edgeLuma;
    
    bool atEndP = abs(lumaDeltaP) >= gradientThreshold;

    UNITY_UNROLL
    for (int i = 0; i < EXTRA_EDGE_STEPS && !atEndP; i++)
    {
        uvP += uvStep * edgeStepSizes[i];
        lumaDeltaP = GetLuma(uvP) - edgeLuma;
        atEndP = abs(lumaDeltaP) >= gradientThreshold;
    }

    if (!atEndP)
    {
        uvP += uvStep * LAST_EDGE_STEP_GUESS;
    }

    float2 uvN = edgeUV - uvStep;
    
    float lumaDeltaN = GetLuma(uvN) - edgeLuma;
    
    bool atEndN = abs(lumaDeltaN) >= gradientThreshold;

    UNITY_UNROLL
    for (int i = 0; i < EXTRA_EDGE_STEPS && !atEndN; i++)
    {
        uvN -= uvStep * edgeStepSizes[i];
        lumaDeltaN = GetLuma(uvN) - edgeLuma;
        atEndN = abs(lumaDeltaN) >= gradientThreshold;
    }

    if (!atEndN)
    {
        uvN -= uvStep * LAST_EDGE_STEP_GUESS;
    }

    float distanceToEndP;
    float distanceToEndN;

    if (_edge.isHorizontal)
    {
        distanceToEndP = uvP.x - _uv.x;
        distanceToEndN = _uv.x - uvN.x;
    }
    else
    {
        distanceToEndP = uvP.y - _uv.y;
        distanceToEndN = _uv.y - uvN.y;
    }

    float distanceToNearestEnd;

    bool deltaSign;

    if (distanceToEndP <= distanceToEndN)
    {
        distanceToNearestEnd = distanceToEndP;
        deltaSign = lumaDeltaP >= 0;
    }
    else
    {
        distanceToNearestEnd = distanceToEndN;
        deltaSign = lumaDeltaN >= 0;
    }

    if (deltaSign == _luma.m - edgeLuma >= 0)
    {
        return 0.0f;
    }
    else
    {
        return 0.5f - distanceToNearestEnd / (distanceToEndP + distanceToEndN);
    }
}

float4 FXAAPassFragment(Varyings _input) : SV_TARGET
{
    LumaNeighborhood luma = GetLumaNeighborhood(_input.screenUV);

    if (CanSkipFXAA(luma))
    {
        return GetSource(_input.screenUV);
    }

    FXAAEdge edge = GetFXAAEdge(luma);

    float blendFactor = max(GetSubpixelBlendFactor(luma), GetEdgeBlendFactor(luma, edge, _input.screenUV));

    float2 blendUV = _input.screenUV;

    if (edge.isHorizontal)
    {
        blendUV.y += blendFactor * edge.pixelStep;
    }
    else
    {
        blendUV.x += blendFactor * edge.pixelStep;
    }

    return GetSource(blendUV);
}

#endif