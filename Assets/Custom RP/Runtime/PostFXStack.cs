using UnityEngine;
using UnityEngine.Rendering;
using static PostFXSettings;

partial class PostFXStack
{
    enum Pass
    {
        BLOOMADD,
        BLOOMHORIZONTAL,
        BLOOMPREFILTER,
        BLOOMPREFILTERFIREFLIES,
        BLOOMSCATTER,
        BLOOMSCATTERFINAL,
        BLOOMVERTICAL,
        COPY,
        COLORGRADINGNONE,
        COLORGRADINGACES,
        COLORGRADINGNEUTRAL,
        COLORGRADINGREINHARD,
        APPLYCOLORGRADING,
        APPLYCOLORGRADINGWITHLUMA,
        FINALRESCALE,
        FXAA,
        FXAAWITHLUMA
    }
    
    private const string bufferName = "Post FX";
    private const string fxaaQualityLowKeyword = "FXAA_QUALITY_LOW";
    private const string fxaaQualityMediumKeyword = "FXAA_QUALITY_MEDIUM";

    private CommandBuffer buffer = new CommandBuffer
    {
            name = bufferName
    };

    private ScriptableRenderContext context;

    private Camera camera;

    private PostFXSettings settings;

    private CameraBufferSettings.FXAA fxaa;

    public bool IsActive => settings != null;

    private int fxSourceId = Shader.PropertyToID("_PostFXSource");
    private int fxSource2Id = Shader.PropertyToID("_PostFXSource2");
    private int bloomBicubicUpSamplingId = Shader.PropertyToID("_BloomBicubicUpSampling");
    private int bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter");
    private int bloomResultId = Shader.PropertyToID("_BloomResult");
    private int bloomThresholdId = Shader.PropertyToID("_BloomThreshold");
    private int bloomIntensityId = Shader.PropertyToID("_BloomIntensity");
    private int colorAdjustmentsId = Shader.PropertyToID("_ColorAdjustments");
    private int colorFilterId = Shader.PropertyToID("_ColorFilter");
    private int whiteBalanceId = Shader.PropertyToID("_WhiteBalance");
    private int splitToningShadowsId = Shader.PropertyToID("_SplitToningShadows");
    private int splitToningHighlightsId = Shader.PropertyToID("_SplitToningHighlights");
    private int channelMixerRedId = Shader.PropertyToID("_ChannelMixerRed");
    private int channelMixerGreenId = Shader.PropertyToID("_ChannelMixerGreen");
    private int channelMixerBlueId = Shader.PropertyToID("_ChannelMixerBlue");
    private int smhShadowsId = Shader.PropertyToID("_SMHShadows");
    private int smhMidtonesId = Shader.PropertyToID("_SMHMidtones");
    private int smhHighlightsId = Shader.PropertyToID("_SMHHighlights");
    private int smhRangeId = Shader.PropertyToID("_SMHRange");
    private int colorGradingLUTId = Shader.PropertyToID("_ColorGradingLUT");
    private int colorGradingLUTParametersId = Shader.PropertyToID("_ColorGradingLUTParameters");
    private int colorGradingLUTInLogCId = Shader.PropertyToID("_ColorGradingLUTInLogC");
    private int copyBicubicId = Shader.PropertyToID("_CopyBicubic");
    private int colorGradingResultId = Shader.PropertyToID("_ColorGradingResult");
    private int finalResultId = Shader.PropertyToID("_FinalResult");
    private int finalSrcBlendId = Shader.PropertyToID("_FinalSrcBlend");
    private int finalDstBlendId = Shader.PropertyToID("_FinalDstBlend");
    private int fxaaConfigId = Shader.PropertyToID("_FXAAConfig");
    
    private int bloomPyramidId;
    private int colorLUTResolution;

    private const int maxBloomPyramidLevels = 16;

    private bool keepAlpha;
    private bool useHDR;
    
    private CameraBufferSettings.BicubicRescalingMode bicubicRescaling;

    private CameraSettings.FinalBlendMode finalBlendMode;

    private Vector2Int bufferSize;

    public PostFXStack()
    {
        bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");

        for (var i = 1; i < maxBloomPyramidLevels * 2; i++)
        {
            Shader.PropertyToID("_BloomPyramid" + i);
        }
    }

    public void Setup(ScriptableRenderContext _context, Camera _camera, Vector2Int _bufferSize, PostFXSettings _settings, bool _keepAlpha, bool _useHDR,
                      int _colorLUTResolution, CameraSettings.FinalBlendMode _finalBlendMode, CameraBufferSettings.BicubicRescalingMode _bicubicRescaling,
                      CameraBufferSettings.FXAA _fxaa)
    {
        fxaa = _fxaa;
        context = _context;
        camera = _camera;
        bufferSize = _bufferSize;
        settings = camera.cameraType <= CameraType.SceneView ? _settings : null;
        keepAlpha = _keepAlpha;
        useHDR = _useHDR;
        colorLUTResolution = _colorLUTResolution;
        finalBlendMode = _finalBlendMode;
        bicubicRescaling = _bicubicRescaling;
        
        ApplySceneViewState();
    }

    public void Render(int _sourceId)
    {
        if (DoBloom(_sourceId))
        {
            DoFinal(bloomResultId);

            buffer.ReleaseTemporaryRT(bloomResultId);
        }
        else
        {
            DoFinal(_sourceId);
        }

        context.ExecuteCommandBuffer(buffer);

        buffer.Clear();
    }

    void Draw(RenderTargetIdentifier _from, RenderTargetIdentifier _to, Pass _pass)
    {
        buffer.SetGlobalTexture(fxSourceId, _from);
        buffer.SetRenderTarget(_to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int) _pass, MeshTopology.Triangles, 3);
    }

    void DrawFinal(RenderTargetIdentifier _from, Pass _pass)
    {
        buffer.SetGlobalFloat(finalSrcBlendId, (float) finalBlendMode.source);
        buffer.SetGlobalFloat(finalDstBlendId, (float) finalBlendMode.destination);
        buffer.SetGlobalTexture(fxSourceId, _from);
        buffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget,
                               finalBlendMode.destination == BlendMode.Zero ? RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load,
                               RenderBufferStoreAction.Store);
        buffer.SetViewport(camera.pixelRect);
        buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int) _pass, MeshTopology.Triangles, 3);
    }

    void ConfigureColorAdjustments()
    {
        ColorAdjustmentsSettings colorAdjustments = settings.ColorAdjustments;

        buffer.SetGlobalVector(colorAdjustmentsId,
                               new Vector4(Mathf.Pow(2.0f, colorAdjustments.postExposure),
                                           colorAdjustments.contrast * 0.01f + 1.0f,
                                           colorAdjustments.hueShift * (1.0f / 360.0f),
                                           colorAdjustments.saturation * 0.01f + 1.0f));

        buffer.SetGlobalColor(colorFilterId, colorAdjustments.colorFilter.linear);
    }

    void ConfigureWhiteBalance()
    {
        WhiteBalanceSettings whiteBalance = settings.WhiteBalance;

        buffer.SetGlobalVector(whiteBalanceId, ColorUtils.ColorBalanceToLMSCoeffs(whiteBalance.temperature, whiteBalance.tint));
    }

    void ConfigureSplitToning()
    {
        SplitToningSettings splitToning = settings.SplitToning;

        Color splitColor = splitToning.shadows;
        splitColor.a = splitToning.balance * 0.01f;

        buffer.SetGlobalColor(splitToningShadowsId, splitColor);
        buffer.SetGlobalColor(splitToningHighlightsId, splitToning.highlights);
    }

    void ConfigureChannelMixer()
    {
        ChannelMixerSettings channelMixer = settings.ChannelMixer;

        buffer.SetGlobalVector(channelMixerRedId, channelMixer.red);
        buffer.SetGlobalVector(channelMixerGreenId, channelMixer.green);
        buffer.SetGlobalVector(channelMixerBlueId, channelMixer.blue);
    }

    void ConfigureShadowsMidtonesHighlights()
    {
        ShadowsMidtonesHighlightsSettings smh = settings.shadowsMidtonesHighlights;

        buffer.SetGlobalColor(smhShadowsId, smh.shadows.linear);
        buffer.SetGlobalColor(smhMidtonesId, smh.midtones.linear);
        buffer.SetGlobalColor(smhHighlightsId, smh.highlights.linear);
        
        buffer.SetGlobalVector(smhRangeId, new Vector4(smh.shadowsStart, smh.shadowsEnd, smh.highlightsStart, smh.highlightsEnd));
    }

    void ConfigureFXAA()
    {
        if (fxaa.quality == CameraBufferSettings.FXAA.Quality.Low)
        {
            buffer.EnableShaderKeyword(fxaaQualityLowKeyword);
            buffer.DisableShaderKeyword(fxaaQualityMediumKeyword);
        }
        else if (fxaa.quality == CameraBufferSettings.FXAA.Quality.Medium)
        {
            buffer.DisableShaderKeyword(fxaaQualityLowKeyword);
            buffer.EnableShaderKeyword(fxaaQualityMediumKeyword);
        }
        else
        {
            buffer.DisableShaderKeyword(fxaaQualityLowKeyword);
            buffer.DisableShaderKeyword(fxaaQualityMediumKeyword);
        }
        
        buffer.SetGlobalVector(fxaaConfigId, new Vector4(fxaa.fixedThreshold, fxaa.relativeThreshold, fxaa.subpixelBlending));
    }

    void DoFinal(int _sourceId)
    {
        ConfigureColorAdjustments();
        ConfigureWhiteBalance();
        ConfigureSplitToning();
        ConfigureChannelMixer();
        ConfigureShadowsMidtonesHighlights();

        int lutHeight = colorLUTResolution;
        int lutWidth = lutHeight * lutHeight;

        buffer.GetTemporaryRT(colorGradingLUTId, lutWidth, lutHeight, 0, FilterMode.Bilinear,
                              RenderTextureFormat.DefaultHDR);

        buffer.SetGlobalVector(colorGradingLUTParametersId,
                               new Vector4(lutHeight, 0.5f / lutWidth, 0.5f / lutHeight,
                                           lutHeight / (lutHeight - 1.0f)));
        
        ToneMappingSettings.Mode mode = settings.ToneMapping.mode;

        Pass pass = Pass.COLORGRADINGNONE + (int)mode;

        buffer.SetGlobalFloat(colorGradingLUTInLogCId, useHDR && pass != Pass.COLORGRADINGNONE ? 1.0f : 0.0f);

        Draw(_sourceId, colorGradingLUTId, pass);

        buffer.SetGlobalVector(colorGradingLUTParametersId, new Vector4(1.0f / lutWidth, 1.0f / lutHeight, lutHeight - 1.0f));

        buffer.SetGlobalFloat(finalSrcBlendId, 1.0f);
        buffer.SetGlobalFloat(finalDstBlendId, 0.0f);
        
        if (fxaa.enabled)
        {
            ConfigureFXAA();
            
            buffer.GetTemporaryRT(colorGradingResultId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
            
            Draw(_sourceId, colorGradingResultId, keepAlpha ? Pass.APPLYCOLORGRADING : Pass.APPLYCOLORGRADINGWITHLUMA);
        }

        if (bufferSize.x == camera.pixelWidth)
        {
            if (fxaa.enabled)
            {
                DrawFinal(colorGradingResultId, keepAlpha ? Pass.FXAA : Pass.FXAAWITHLUMA);

                buffer.ReleaseTemporaryRT(colorGradingResultId);
            }
            else
            {
                DrawFinal(_sourceId, Pass.APPLYCOLORGRADING);
            }
        }
        else
        {
            buffer.GetTemporaryRT(finalResultId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear,
                                  RenderTextureFormat.Default);

            if (fxaa.enabled)
            {
                Draw(colorGradingResultId, finalResultId, keepAlpha ? Pass.FXAA : Pass.FXAAWITHLUMA);

                buffer.ReleaseTemporaryRT(colorGradingResultId);
            }
            else
            {
                Draw(_sourceId, finalResultId, Pass.APPLYCOLORGRADING);
            }

            bool bicubicSampling = bicubicRescaling == CameraBufferSettings.BicubicRescalingMode.UPANDDOWN ||
                                   bicubicRescaling == CameraBufferSettings.BicubicRescalingMode.UPONLY &&
                                   bufferSize.x < camera.pixelWidth;

            buffer.SetGlobalFloat(copyBicubicId, bicubicSampling ? 1.0f : 0.0f);

            DrawFinal(finalResultId, Pass.FINALRESCALE);

            buffer.ReleaseTemporaryRT(finalResultId);
        }

        buffer.ReleaseTemporaryRT(colorGradingLUTId);
    }

    bool DoBloom(int _sourceId)
    {
        BloomSettings bloom = settings.Bloom;

        int width;
        int height;

        if (bloom.ignoreRenderScale)
        {
            width = camera.pixelWidth / 2;
            height = camera.pixelHeight / 2;
        }
        else
        {
            width = bufferSize.x / 2;
            height = bufferSize.y / 2;
        }

        if (bloom.maxIterations == 0
            || bloom.intensity <= 0.0f
            || height < bloom.downscaleLimit * 2
            || width < bloom.downscaleLimit * 2)
        {
            return false;
        }

        buffer.BeginSample("Bloom");
        
        Vector4 threshold;
        threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
        threshold.y = threshold.x * bloom.thresholdKnee;
        threshold.z = 2f * threshold.y;
        threshold.w = 0.25f / (threshold.y + 0.00001f);
        threshold.y -= threshold.x;
        
        buffer.SetGlobalVector(bloomThresholdId, threshold);

        RenderTextureFormat format = useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;

        buffer.GetTemporaryRT(bloomPrefilterId, width, height, 0, FilterMode.Bilinear, format);

        Draw(_sourceId, bloomPrefilterId, bloom.fadeFireflies ? Pass.BLOOMPREFILTERFIREFLIES : Pass.BLOOMPREFILTER);

        width /= 2;
        height /= 2;

        int fromId = bloomPrefilterId;
        int toId = bloomPyramidId + 1;

        int i;

        for (i = 0; i < bloom.maxIterations; i++)
        {
            if (height < bloom.downscaleLimit
                || width < bloom.downscaleLimit)
            {
                break;
            }

            int midId = toId - 1;

            buffer.GetTemporaryRT(midId, width, height, 0, FilterMode.Bilinear, format);

            buffer.GetTemporaryRT(toId, width, height, 0, FilterMode.Bilinear, format);

            Draw(fromId, midId, Pass.BLOOMHORIZONTAL);
            Draw(midId, toId, Pass.BLOOMVERTICAL);

            fromId = toId;

            toId += 2;

            width /= 2;
            height /= 2;
        }

        buffer.ReleaseTemporaryRT(bloomPrefilterId);

        buffer.SetGlobalFloat(bloomBicubicUpSamplingId, bloom.bicubicUpSampling ? 1.0f : 0.0f);

        Pass combinePass;
        Pass finalPass;

        float finalIntensity;

        if (bloom.mode == BloomSettings.Mode.ADDITIVE)
        {
            combinePass = finalPass = Pass.BLOOMADD;
            
            buffer.SetGlobalFloat(bloomIntensityId, 1.0f);

            finalIntensity = bloom.intensity;
        }
        else
        {
            combinePass = Pass.BLOOMSCATTER;
            finalPass = Pass.BLOOMSCATTERFINAL;
            
            buffer.SetGlobalFloat(bloomIntensityId, bloom.scatter);

            finalIntensity = Mathf.Min(bloom.intensity, 1.0f);
        }

        if (i > 1)
        {
            buffer.ReleaseTemporaryRT(fromId - 1);
            
            toId -= 5;

            for (i -= 1; i > 0; i--)
            {
                buffer.SetGlobalTexture(fxSource2Id, toId + 1);

                Draw(fromId, toId, combinePass);

                buffer.ReleaseTemporaryRT(fromId);
                buffer.ReleaseTemporaryRT(toId + 1);

                fromId = toId;
                toId -= 2;
            }
        }
        else
        {
            buffer.ReleaseTemporaryRT(bloomPyramidId);
        }

        buffer.SetGlobalFloat(bloomIntensityId, finalIntensity);

        buffer.SetGlobalTexture(fxSource2Id, _sourceId);

        buffer.GetTemporaryRT(bloomResultId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, format);

        Draw(fromId, bloomResultId, finalPass);

        buffer.ReleaseTemporaryRT(fromId);

        buffer.EndSample("Bloom");

        return true;
    }
}