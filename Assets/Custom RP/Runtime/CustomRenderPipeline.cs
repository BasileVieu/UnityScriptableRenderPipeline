using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline
{
    private CameraRenderer renderer;

    private CameraBufferSettings cameraBufferSettings;

    private int colorLUTResolution;

    private bool useDynamicBatching;
    private bool useGPUInstancing;
    private bool useLightsPerObject;

    private ShadowSettings shadowSettings;

    private PostFXSettings postFXSettings;

    public CustomRenderPipeline(CameraBufferSettings _cameraBufferSettings, bool _useDynamicBatching,
                                bool _useGPUInstancing, bool _useSrpBatcher,
                                bool _useLightsPerObject, ShadowSettings _shadowSettings,
                                PostFXSettings _postFXSettings, int _colorLUTResolution,
                                Shader _cameraRendererShader)
    {
        colorLUTResolution = _colorLUTResolution;
        cameraBufferSettings = _cameraBufferSettings;
        useDynamicBatching = _useDynamicBatching;
        useGPUInstancing = _useGPUInstancing;
        useLightsPerObject = _useLightsPerObject;
        shadowSettings = _shadowSettings;
        postFXSettings = _postFXSettings;

        GraphicsSettings.useScriptableRenderPipelineBatching = _useSrpBatcher;
        GraphicsSettings.lightsUseLinearIntensity = true;

        InitializeForEditor();

        renderer = new CameraRenderer(_cameraRendererShader);
    }

    protected override void Render(ScriptableRenderContext _context, Camera[] _cameras)
    {
        foreach (Camera camera in _cameras)
        {
            renderer.Render(_context, camera, cameraBufferSettings, useDynamicBatching, useGPUInstancing,
                            useLightsPerObject, shadowSettings, postFXSettings, colorLUTResolution);
        }
    }

    protected override void Dispose(bool _disposing)
    {
        base.Dispose(_disposing);

        DisposeForEditor();

        renderer.Dispose();
    }
}