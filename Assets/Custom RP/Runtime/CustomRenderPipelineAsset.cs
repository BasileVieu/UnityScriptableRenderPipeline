using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public partial class CustomRenderPipelineAsset : RenderPipelineAsset
{
    public enum ColorLUTResolution
    {
        _16 = 16,
        _32 = 32,
        _64 = 64
    }

    [SerializeField] private CameraBufferSettings cameraBuffer = new CameraBufferSettings
    {
            allowHDR = true,
            renderScale = 1.0f,
            fxaa = new CameraBufferSettings.FXAA
            {
                fixedThreshold = 0.0833f,
                relativeThreshold = 0.166f,
                subpixelBlending = 0.75f
            }
    };
    [SerializeField] private bool useDynamicBatching = true;
    [SerializeField] private bool useGPUInstancing = true;
    [SerializeField] private bool useSrpBatcher = true;
    [SerializeField] private bool useLightsPerObject = true;

    [SerializeField] ShadowSettings shadows;

    [SerializeField] private PostFXSettings postFXSettings;

    [SerializeField] private ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;

    [SerializeField] private Shader cameraRendererShader;

    protected override RenderPipeline CreatePipeline() =>
            new CustomRenderPipeline(cameraBuffer, useDynamicBatching, useGPUInstancing, useSrpBatcher, useLightsPerObject,
                                     shadows, postFXSettings, (int) colorLUTResolution, cameraRendererShader);
}