using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    private const string bufferName = "Render Camera";

    private static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
    private static ShaderTagId litShaderTagId = new ShaderTagId("CustomLit");

    private static int bufferSizeId = Shader.PropertyToID("_CameraBufferSize");
    private static int colorAttachmentId = Shader.PropertyToID("_CameraColorAttachment");
    private static int depthAttachmentId = Shader.PropertyToID("_CameraDepthAttachment");
    private static int colorTextureId = Shader.PropertyToID("_CameraColorTexture");
    private static int depthTextureId = Shader.PropertyToID("_CameraDepthTexture");
    private static int sourceTextureId = Shader.PropertyToID("_SourceTexture");
    private static int srcBlendId = Shader.PropertyToID("_CameraSrcBlend");
    private static int dstBlendId = Shader.PropertyToID("_CameraDstBlend");

    private CommandBuffer buffer = new CommandBuffer
    {
            name = bufferName
    };

    private ScriptableRenderContext context;

    private Camera camera;

    private CullingResults cullingResults;

    private Lighting lighting = new Lighting();

    private PostFXStack postFXStack = new PostFXStack();

    private static CameraSettings defaultCameraSettings = new CameraSettings();

    private Material material;

    private Texture2D missingTexture;

    private Vector2Int bufferSize;

    public const float renderScaleMin = 0.1f;
    public const float renderScaleMax = 2.0f;

    private bool useHDR;
    private bool useScaledRendering;
    private bool useColorTexture;
    private bool useDepthTexture;
    private bool useIntermediateBuffer;

    private static bool copyTextureSupported = SystemInfo.copyTextureSupport > CopyTextureSupport.None;

    public CameraRenderer(Shader _shader)
    {
        material = CoreUtils.CreateEngineMaterial(_shader);

        missingTexture = new Texture2D(1, 1)
        {
                hideFlags = HideFlags.HideAndDontSave,
                name = "Missing"
        };

        missingTexture.SetPixel(0, 0, Color.white * 0.5f);
        missingTexture.Apply(true, true);
    }

    void Setup()
    {
        context.SetupCameraProperties(camera);
        
        CameraClearFlags flags = camera.clearFlags;

        useIntermediateBuffer = useScaledRendering || useColorTexture || useDepthTexture || postFXStack.IsActive;

        if (useIntermediateBuffer)
        {
            if (flags > CameraClearFlags.Color)
            {
                flags = CameraClearFlags.Color;
            }
            
            buffer.GetTemporaryRT(colorAttachmentId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear,
                                  useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            
            buffer.GetTemporaryRT(depthAttachmentId, bufferSize.x, bufferSize.y, 32, FilterMode.Point,
                                  RenderTextureFormat.Depth);
            
            buffer.SetRenderTarget(colorAttachmentId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                                   depthAttachmentId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        }
        
        buffer.ClearRenderTarget(flags <= CameraClearFlags.Depth,
                                 flags == CameraClearFlags.Color,
                                 flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);
        
        buffer.BeginSample(SampleName);

        buffer.SetGlobalTexture(colorTextureId, missingTexture);

        buffer.SetGlobalTexture(depthTextureId, missingTexture);
        
        ExecuteBuffer();
    }

    public void Render(ScriptableRenderContext _context, Camera _camera,
                       CameraBufferSettings _bufferSettings, bool _useDynamicBatching, bool _useGPUInstancing,
                       bool _useLightsPerObject, ShadowSettings _shadowSettings,
                       PostFXSettings _postFXSettings, int _colorLUTResolution)
    {
        context = _context;
        camera = _camera;

        var crpCamera = camera.GetComponent<CustomRenderPipelineCamera>();
        
        CameraSettings cameraSettings = crpCamera ? crpCamera.Settings : defaultCameraSettings;

        if (camera.cameraType == CameraType.Reflection)
        {
            useColorTexture = _bufferSettings.copyColorReflections;
            useDepthTexture = _bufferSettings.copyDepthReflections;
        }
        else
        {
            useColorTexture = _bufferSettings.copyColor && cameraSettings.copyColor;
            useDepthTexture = _bufferSettings.copyDepth && cameraSettings.copyDepth;
        }

        if (cameraSettings.overridePostFX)
        {
            _postFXSettings = cameraSettings.postFXSettings;
        }

        float renderScale = cameraSettings.GetRenderScale(_bufferSettings.renderScale);

        useScaledRendering = renderScale < 0.99f || renderScale > 1.0f;

        PrepareBuffer();
        
        PrepareForSceneWindow();
        
        if (!Cull(_shadowSettings.maxDistance))
        {
            return;
        }

        useHDR = _bufferSettings.allowHDR && camera.allowHDR;

        if (useScaledRendering)
        {
            renderScale = Mathf.Clamp(renderScale, renderScaleMin, renderScaleMax);
            
            bufferSize.x = (int) (camera.pixelWidth * renderScale);
            bufferSize.y = (int) (camera.pixelHeight * renderScale);
        }
        else
        {
            bufferSize.x = camera.pixelWidth;
            bufferSize.y = camera.pixelHeight;
        }

        buffer.BeginSample(SampleName);

        buffer.SetGlobalVector(bufferSizeId, new Vector4(1.0f / bufferSize.x, 1.0f / bufferSize.y, bufferSize.x, bufferSize.y));
        
        ExecuteBuffer();
        
        lighting.Setup(_context, cullingResults, _shadowSettings, _useLightsPerObject, cameraSettings.maskLights ? cameraSettings.renderingLayerMask : -1);

        _bufferSettings.fxaa.enabled &= cameraSettings.allowFXAA;

        postFXStack.Setup(context, camera, bufferSize, _postFXSettings, cameraSettings.keepAlpha, useHDR, _colorLUTResolution, cameraSettings.finalBlendMode, _bufferSettings.bicubicRescaling, _bufferSettings.fxaa);
        
        buffer.EndSample(SampleName);
        
        Setup();

        DrawVisibleGeometry(_useDynamicBatching, _useGPUInstancing, _useLightsPerObject, cameraSettings.renderingLayerMask);
        
        DrawUnsupportedShaders();
        
        DrawGizmosBeforeFX();

        if (postFXStack.IsActive)
        {
            postFXStack.Render(colorAttachmentId);
        }
        else if (useIntermediateBuffer)
        {
            DrawFinal(cameraSettings.finalBlendMode);

            ExecuteBuffer();
        }

        DrawGizmosAfterFX();

        Cleanup();
        
        Submit();
    }

    void CopyAttachments()
    {
        if (useColorTexture)
        {
            buffer.GetTemporaryRT(colorTextureId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear,
                                  useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);

            if (copyTextureSupported)
            {
                buffer.CopyTexture(colorAttachmentId, colorTextureId);
            }
            else
            {
                Draw(colorAttachmentId, colorTextureId);
            }
        }

        if (useDepthTexture)
        {
            buffer.GetTemporaryRT(depthTextureId, bufferSize.x, bufferSize.y, 32, FilterMode.Point,
                                  RenderTextureFormat.Depth);

            if (copyTextureSupported)
            {
                buffer.CopyTexture(depthAttachmentId, depthTextureId);
            }
            else
            {
                Draw(depthAttachmentId, depthTextureId, true);
            }
        }

        if (!copyTextureSupported)
        {
            buffer.SetRenderTarget(colorAttachmentId, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                                   depthAttachmentId, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
        }

        ExecuteBuffer();
    }

    bool Cull(float _maxShadowDistance)
    {
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            p.shadowDistance = Mathf.Min(_maxShadowDistance, camera.farClipPlane);
            cullingResults = context.Cull(ref p);
            
            return true;
        }

        return false;
    }

    void Cleanup()
    {
        lighting.Cleanup();

        if (useIntermediateBuffer)
        {
            buffer.ReleaseTemporaryRT(colorAttachmentId);
            buffer.ReleaseTemporaryRT(depthAttachmentId);

            if (useColorTexture)
            {
                buffer.ReleaseTemporaryRT(colorTextureId);
            }

            if (useDepthTexture)
            {
                buffer.ReleaseTemporaryRT(depthTextureId);
            }
        }
    }

    public void Dispose()
    {
        CoreUtils.Destroy(material);
        CoreUtils.Destroy(missingTexture);
    }

    void Submit()
    {
        buffer.EndSample(SampleName);
        ExecuteBuffer();
        context.Submit();
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void DrawVisibleGeometry(bool _useDynamicBatching, bool _useGPUInstancing, bool _useLightsPerObject, int _renderingLayerMask)
    {
        PerObjectData lightsPerObjectFlags = _useLightsPerObject
                                                     ? PerObjectData.LightData | PerObjectData.LightIndices
                                                     : PerObjectData.None;
        
        var sortingSettings = new SortingSettings(camera)
        {
                criteria = SortingCriteria.CommonOpaque
        };
        
        var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings)
        {
                enableDynamicBatching = _useDynamicBatching,
                enableInstancing = _useGPUInstancing,
                perObjectData = PerObjectData.ReflectionProbes |
                                PerObjectData.Lightmaps | PerObjectData.ShadowMask |
                                PerObjectData.LightProbe | PerObjectData.OcclusionProbe |
                                PerObjectData.LightProbeProxyVolume |
                                PerObjectData.OcclusionProbeProxyVolume |
                                lightsPerObjectFlags
        };
        
        drawingSettings.SetShaderPassName(1, litShaderTagId);

        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque, renderingLayerMask: (uint) _renderingLayerMask);

        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

        context.DrawSkybox(camera);

        if (useColorTexture
            || useDepthTexture)
        {
            CopyAttachments();
        }

        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;

        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

    void Draw(RenderTargetIdentifier _from, RenderTargetIdentifier _to, bool _isDepth = false)
    {
        buffer.SetGlobalTexture(sourceTextureId, _from);

        buffer.SetRenderTarget(_to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);

        buffer.DrawProcedural(Matrix4x4.identity, material, _isDepth ? 1 : 0, MeshTopology.Triangles, 3);
    }

    void DrawFinal(CameraSettings.FinalBlendMode _finalBlendMode)
    {
        buffer.SetGlobalFloat(srcBlendId, (float) _finalBlendMode.source);
        buffer.SetGlobalFloat(dstBlendId, (float) _finalBlendMode.destination);
        buffer.SetGlobalTexture(sourceTextureId, colorAttachmentId);
        buffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget,
                               _finalBlendMode.destination == BlendMode.Zero
                                       ? RenderBufferLoadAction.DontCare
                                       : RenderBufferLoadAction.Load,
                               RenderBufferStoreAction.Store);
        buffer.SetViewport(camera.pixelRect);
        buffer.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
        buffer.SetGlobalFloat(srcBlendId, 1f);
        buffer.SetGlobalFloat(dstBlendId, 0f);
    }
}