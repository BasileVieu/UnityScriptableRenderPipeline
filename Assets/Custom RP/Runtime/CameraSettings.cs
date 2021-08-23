using UnityEngine;
using UnityEngine.Rendering;

[System.Serializable]
public class CameraSettings
{
    [System.Serializable]
    public struct FinalBlendMode
    {
        public BlendMode source;
        public BlendMode destination;
    }

    public enum RenderScaleMode
    {
        INHERIT,
        MULTIPLY,
        OVERRIDE
    }

    public bool copyColor = true;
    public bool copyDepth = true;

    [RenderingLayerMaskField] public int renderingLayerMask = -1;

    public bool maskLights;

    public RenderScaleMode renderScaleMode = RenderScaleMode.INHERIT;

    [Range(CameraRenderer.renderScaleMin, CameraRenderer.renderScaleMax)] public float renderScale = 1.0f;

    public bool overridePostFX;

    public PostFXSettings postFXSettings;

    public bool allowFXAA;

    public bool keepAlpha = false;

    public FinalBlendMode finalBlendMode = new FinalBlendMode
    {
            source = BlendMode.One,
            destination = BlendMode.Zero
    };

    public float GetRenderScale(float _scale) =>
            renderScaleMode == RenderScaleMode.INHERIT ? _scale :
            renderScaleMode == RenderScaleMode.OVERRIDE ? renderScale : _scale * renderScale;
}