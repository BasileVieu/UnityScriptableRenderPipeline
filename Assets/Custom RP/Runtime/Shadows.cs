using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{
    private const string bufferName = "Shadows";

    private const int maxShadowedDirLightCount = 4;
    private const int maxShadowedOtherLightCount = 16;
    private const int maxCascades = 4;

    private static string[] directionalFilterKeywords =
    {
            "_DIRECTIONAL_PCF3",
            "_DIRECTIONAL_PCF5",
            "_DIRECTIONAL_PCF7",
    };

    private static string[] cascadeBlendKeywords =
    {
            "_CASCADE_BLEND_SOFT",
            "_CASCADE_BLEND_DITHER"
    };

    private static string[] shadowMaskKeywords =
    {
            "_SHADOW_MASK_ALWAYS",
            "_SHADOW_MASK_DISTANCE"
    };

    private static string[] otherFilterKeywords =
    {
            "_OTHER_PCF3",
            "_OTHER_PCF5",
            "_OTHER_PCF7"
    };

    private static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");
    private static int dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices");
    private static int otherShadowAtlasId = Shader.PropertyToID("_OtherShadowAtlas");
    private static int otherShadowMatricesId = Shader.PropertyToID("_OtherShadowMatrices");
    private static int otherShadowTilesId = Shader.PropertyToID("_OtherShadowTiles");
    private static int cascadeCountId = Shader.PropertyToID("_CascadeCount");
    private static int cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres");
    private static int cascadeDataId = Shader.PropertyToID("_CascadeData");
    private static int shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize");
    private static int shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");
    private static int shadowPancakingId = Shader.PropertyToID("_ShadowPancaking");

    private static Vector4[] cascadeCullingSpheres = new Vector4[maxCascades];
    private static Vector4[] cascadeData = new Vector4[maxCascades];
    private static Vector4[] otherShadowTiles = new Vector4[maxShadowedOtherLightCount];

    private static Matrix4x4[] dirShadowMatrices = new Matrix4x4[maxShadowedDirLightCount * maxCascades];
    private static Matrix4x4[] otherShadowMatrices = new Matrix4x4[maxShadowedOtherLightCount];

    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }

    struct ShadowedOtherLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float normalBias;
        public bool isPoint;
    }

    private ShadowedDirectionalLight[] shadowedDirectionalLights = new ShadowedDirectionalLight[maxShadowedDirLightCount];
    private ShadowedOtherLight[] shadowedOtherLights = new ShadowedOtherLight[maxShadowedOtherLightCount];

    private int shadowedDirLightCount;
    private int shadowedOtherLightCount;

    private CommandBuffer buffer = new CommandBuffer
    {
            name = bufferName
    };

    private ScriptableRenderContext context;

    private CullingResults cullingResults;

    private ShadowSettings settings;

    private bool useShadowMask;

    private Vector4 atlasSizes;

    public void Setup(ScriptableRenderContext _context, CullingResults _cullingResults, ShadowSettings _settings)
    {
        context = _context;
        cullingResults = _cullingResults;
        settings = _settings;
        shadowedDirLightCount = shadowedOtherLightCount = 0;
        useShadowMask = false;
    }

    public void Cleanup()
    {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);

        if (shadowedOtherLightCount > 0)
        {
            buffer.SetGlobalTexture(otherShadowAtlasId, dirShadowAtlasId);
        }

        ExecuteBuffer();
    }

    public Vector4 ReserveDirectionalShadows(Light _light, int _visibleLightIndex)
    {
        if (shadowedDirLightCount < maxShadowedDirLightCount
            && _light.shadows != LightShadows.None
            && _light.shadowStrength > 0f)
        {
            float maskChannel = -1;

            LightBakingOutput lightBaking = _light.bakingOutput;

            if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed
                && lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
            {
                useShadowMask = true;
                maskChannel = lightBaking.occlusionMaskChannel;
            }

            if (!cullingResults.GetShadowCasterBounds(_visibleLightIndex, out Bounds b))
            {
                return new Vector4(-_light.shadowStrength, 0f, 0f, maskChannel);
            }

            shadowedDirectionalLights[shadowedDirLightCount] = new ShadowedDirectionalLight
            {
                    visibleLightIndex = _visibleLightIndex,
                    slopeScaleBias = _light.shadowBias,
                    nearPlaneOffset = _light.shadowNearPlane
            };

            return new Vector4(_light.shadowStrength,
                               settings.directional.cascadeCount * shadowedDirLightCount++,
                               _light.shadowNormalBias,
                               maskChannel);
        }

        return new Vector4(0f, 0f, 0f, -1f);
    }

    public Vector4 ReserveOtherShadows(Light _light, int _visibleLightIndex)
    {
        if (_light.shadows == LightShadows.None
            || _light.shadowStrength <= 0.0f)
        {
            return new Vector4(0.0f, 0.0f, 0.0f, -1.0f);
        }

        float maskChannel = -1.0f;
        
        LightBakingOutput lightBaking = _light.bakingOutput;

        if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed
            && lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
        {
            useShadowMask = true;
            maskChannel = lightBaking.occlusionMaskChannel;
        }

        bool isPoint = _light.type == LightType.Point;

        int newLightCount = shadowedOtherLightCount + (isPoint ? 6 : 1);

        if (newLightCount > maxShadowedOtherLightCount
            || !cullingResults.GetShadowCasterBounds(_visibleLightIndex, out Bounds b))
        {
            return new Vector4(-_light.shadowStrength, 0.0f, 0.0f, maskChannel);
        }

        shadowedOtherLights[shadowedOtherLightCount] = new ShadowedOtherLight
        {
                visibleLightIndex = _visibleLightIndex,
                slopeScaleBias = _light.shadowBias,
                normalBias = _light.shadowNormalBias,
                isPoint = isPoint
        };

        var data = new Vector4(_light.shadowStrength, shadowedOtherLightCount, isPoint ? 1.0f : 0.0f, maskChannel);

        shadowedOtherLightCount = newLightCount;

        return data;
    }

    public void Render()
    {
        if (shadowedDirLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else
        {
            buffer.GetTemporaryRT(dirShadowAtlasId, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        }

        if (shadowedOtherLightCount > 0)
        {
            RenderOtherShadows();
        }
        else
        {
            buffer.SetGlobalTexture(otherShadowAtlasId, dirShadowAtlasId);
        }

        buffer.BeginSample(bufferName);

        SetKeywords(shadowMaskKeywords,
                    useShadowMask ? QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1 : -1);

        buffer.SetGlobalInt(cascadeCountId, settings.directional.cascadeCount);
        
        float f = 1f - settings.directional.cascadeFade;
        buffer.SetGlobalVector(shadowDistanceFadeId, new Vector4(1f / settings.maxDistance, 1f / settings.distanceFade, 1f / (1f - f * f)));
        
        buffer.SetGlobalVector(shadowAtlasSizeId, atlasSizes);
        
        buffer.EndSample(bufferName);

        ExecuteBuffer();
    }

    void RenderDirectionalShadows()
    {
        var atlasSize = (int) settings.directional.atlasSize;
        atlasSizes.x = atlasSize;
        atlasSizes.y = 1.0f / atlasSize;
        
        buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear,
                              RenderTextureFormat.Shadowmap);
        buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.SetGlobalFloat(shadowPancakingId, 1.0f);

        buffer.BeginSample(bufferName);

        ExecuteBuffer();

        int tiles = shadowedDirLightCount * settings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        for (var i = 0; i < shadowedDirLightCount; i++)
        {
            RenderDirectionalShadows(i, split, tileSize);
        }

        buffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSpheres);
        buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
        buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
        SetKeywords(directionalFilterKeywords, (int) settings.directional.filter - 1);
        SetKeywords(cascadeBlendKeywords, (int) settings.directional.cascadeBlend - 1);

        buffer.EndSample(bufferName);

        ExecuteBuffer();
    }

    void RenderOtherShadows()
    {
        var atlasSize = (int) settings.other.atlasSize;
        atlasSizes.z = atlasSize;
        atlasSizes.w = 1.0f / atlasSize;
        
        buffer.GetTemporaryRT(otherShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear,
                              RenderTextureFormat.Shadowmap);
        buffer.SetRenderTarget(otherShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.SetGlobalFloat(shadowPancakingId, 0.0f);

        buffer.BeginSample(bufferName);

        ExecuteBuffer();

        int tiles = shadowedOtherLightCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        for (var i = 0; i < shadowedOtherLightCount;)
        {
            if (shadowedOtherLights[i].isPoint)
            {
                RenderPointShadows(i, split, tileSize);

                i += 6;
            }
            else
            {
                RenderSpotShadows(i, split, tileSize);

                i++;
            }
        }

        buffer.SetGlobalMatrixArray(otherShadowMatricesId, otherShadowMatrices);
        buffer.SetGlobalVectorArray(otherShadowTilesId, otherShadowTiles);
        SetKeywords(otherFilterKeywords, (int) settings.other.filter - 1);

        buffer.EndSample(bufferName);

        ExecuteBuffer();
    }

    void SetOtherTileData(int _index, Vector2 _offset, float _scale, float _bias)
    {
        float border = atlasSizes.w * 0.5f;

        Vector4 data;
        data.x = _offset.x * _scale + border;
        data.y = _offset.y * _scale + border;
        data.z = _scale - border - border;
        data.w = _bias;

        otherShadowTiles[_index] = data;
    }

    void SetKeywords(string[] _keywords, int _enabledIndex)
    {
        for (var i = 0; i < _keywords.Length; i++)
        {
            if (i == _enabledIndex)
            {
                buffer.EnableShaderKeyword(_keywords[i]);
            }
            else
            {
                buffer.DisableShaderKeyword(_keywords[i]);
            }
        }
    }

    void RenderDirectionalShadows(int _index, int _split, int _tileSize)
    {
        ShadowedDirectionalLight light = shadowedDirectionalLights[_index];

        var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex)
        {
                useRenderingLayerMaskTest = true
        };

        int cascadeCount = settings.directional.cascadeCount;
        int tileOffset = _index * cascadeCount;

        Vector3 ratios = settings.directional.CascadeRatios;

        float cullingFactor = Mathf.Max(0f, 0.8f - settings.directional.cascadeFade);

        float tileScale = 1.0f / _split;

        for (var i = 0; i < cascadeCount; i++)
        {
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(light.visibleLightIndex, i,
                                                                                    cascadeCount, ratios, _tileSize,
                                                                                    light.nearPlaneOffset,
                                                                                    out Matrix4x4 viewMatrix,
                                                                                    out Matrix4x4 projectionMatrix,
                                                                                    out ShadowSplitData splitData);
            splitData.shadowCascadeBlendCullingFactor = cullingFactor;
            shadowSettings.splitData = splitData;

            if (_index == 0)
            {
                SetCascadeData(i, splitData.cullingSphere, _tileSize);
            }

            int tileIndex = tileOffset + i;

            dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix, SetTileViewport(tileIndex, _split, _tileSize), tileScale);

            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);

            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);

            ExecuteBuffer();

            context.DrawShadows(ref shadowSettings);

            buffer.SetGlobalDepthBias(0f, 0f);
        }
    }

    void RenderSpotShadows(int _index, int _split, int _tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[_index];

        var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);

        cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(light.visibleLightIndex, out Matrix4x4 viewMatrix,
                                                                     out Matrix4x4 projectionMatrix,
                                                                     out ShadowSplitData splitData);

        shadowSettings.splitData = splitData;

        float texelSize = 2.0f / (_tileSize * projectionMatrix.m00);
        float filterSize = texelSize * ((float) settings.other.filter + 1.0f);
        float bias = light.normalBias * filterSize * 1.4142136f;

        Vector2 offset = SetTileViewport(_index, _split, _tileSize);

        float tileScale = 1.0f / _split;

        SetOtherTileData(_index, offset, tileScale, bias);

        otherShadowMatrices[_index] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix, offset, tileScale);

        buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
        buffer.SetGlobalDepthBias(0.0f, light.slopeScaleBias);

        ExecuteBuffer();

        context.DrawShadows(ref shadowSettings);

        buffer.SetGlobalDepthBias(0.0f, 0.0f);
    }

    void RenderPointShadows(int _index, int _split, int _tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[_index];

        var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);

        float texelSize = 2.0f / _tileSize;
        float filterSize = texelSize * ((float) settings.other.filter + 1.0f);
        float bias = light.normalBias * filterSize * 1.4142136f;
        float tileScale = 1.0f / _split;

        float fovBias = Mathf.Atan(1.0f + bias + filterSize) * Mathf.Rad2Deg * 2.0f - 90.0f;

        for (var i = 0; i < 6; i++)
        {
            cullingResults.ComputePointShadowMatricesAndCullingPrimitives(light.visibleLightIndex,
                                                                          (CubemapFace) i,
                                                                          fovBias,
                                                                          out Matrix4x4 viewMatrix,
                                                                          out Matrix4x4 projectionMatrix,
                                                                          out ShadowSplitData splitData);

            viewMatrix.m11 = -viewMatrix.m11;
            viewMatrix.m12 = -viewMatrix.m12;
            viewMatrix.m13 = -viewMatrix.m13;

            shadowSettings.splitData = splitData;

            int tileIndex = _index + i;

            Vector2 offset = SetTileViewport(tileIndex, _split, _tileSize);

            SetOtherTileData(tileIndex, offset, tileScale, bias);

            otherShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix, offset, tileScale);

            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            buffer.SetGlobalDepthBias(0.0f, light.slopeScaleBias);

            ExecuteBuffer();

            context.DrawShadows(ref shadowSettings);

            buffer.SetGlobalDepthBias(0.0f, 0.0f);
        }
    }

    void SetCascadeData(int _index, Vector4 _cullingSphere, float _tileSize)
    {
        float texelSize = 2f * _cullingSphere.w / _tileSize;

        float filterSize = texelSize * ((float) settings.directional.filter + 1f);

        _cullingSphere.w -= filterSize;
        _cullingSphere.w *= _cullingSphere.w;

        cascadeCullingSpheres[_index] = _cullingSphere;
        cascadeData[_index] = new Vector4(1f / _cullingSphere.w, filterSize * 1.4142136f);
    }

    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 _m, Vector2 _offset, float _scale)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            _m.m20 = -_m.m20;
            _m.m21 = -_m.m21;
            _m.m22 = -_m.m22;
            _m.m23 = -_m.m23;
        }

        _m.m00 = (0.5f * (_m.m00 + _m.m30) + _offset.x * _m.m30) * _scale;
        _m.m01 = (0.5f * (_m.m01 + _m.m31) + _offset.x * _m.m31) * _scale;
        _m.m02 = (0.5f * (_m.m02 + _m.m32) + _offset.x * _m.m32) * _scale;
        _m.m03 = (0.5f * (_m.m03 + _m.m33) + _offset.x * _m.m33) * _scale;
        _m.m10 = (0.5f * (_m.m10 + _m.m30) + _offset.y * _m.m30) * _scale;
        _m.m11 = (0.5f * (_m.m11 + _m.m31) + _offset.y * _m.m31) * _scale;
        _m.m12 = (0.5f * (_m.m12 + _m.m32) + _offset.y * _m.m32) * _scale;
        _m.m13 = (0.5f * (_m.m13 + _m.m33) + _offset.y * _m.m33) * _scale;
        _m.m20 = 0.5f * (_m.m20 + _m.m30);
        _m.m21 = 0.5f * (_m.m21 + _m.m31);
        _m.m22 = 0.5f * (_m.m22 + _m.m32);
        _m.m23 = 0.5f * (_m.m23 + _m.m33);

        return _m;
    }

    Vector2 SetTileViewport(int _index, int _split, float _tileSize)
    {
        var offset = new Vector2(_index % _split, _index / _split);

        buffer.SetViewport(new Rect(offset.x * _tileSize, offset.y * _tileSize, _tileSize, _tileSize));

        return offset;
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);

        buffer.Clear();
    }
}