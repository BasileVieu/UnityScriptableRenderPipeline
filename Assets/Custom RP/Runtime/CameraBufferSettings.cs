using System;
using UnityEngine;

[Serializable]
public struct CameraBufferSettings
{
    public enum BicubicRescalingMode
    {
        OFF,
        UPONLY,
        UPANDDOWN
    }

    [Serializable]
    public struct FXAA
    {
        public enum Quality
        {
            Low,
            Medium,
            High
        }
        
        public bool enabled;

        [Range(0.0312f, 0.0833f)] public float fixedThreshold;

        [Range(0.063f, 0.333f)] public float relativeThreshold;

        [Range(0.0f, 1.0f)] public float subpixelBlending;

        public Quality quality;
    }
    
    public bool allowHDR;

    public bool copyColor;
    public bool copyColorReflections;

    public bool copyDepth;
    public bool copyDepthReflections;

    [Range(CameraRenderer.renderScaleMin, CameraRenderer.renderScaleMax)] public float renderScale;

    public BicubicRescalingMode bicubicRescaling;

    public FXAA fxaa;
}