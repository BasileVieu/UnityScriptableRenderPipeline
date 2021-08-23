using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSettings : ScriptableObject
{
    [System.Serializable]
    public struct BloomSettings
    {
        public enum Mode
        {
            ADDITIVE,
            SCATTERING
        }

        public bool ignoreRenderScale;
        
        [Range(0.0f, 16.0f)] public int maxIterations;

        [Min(1.0f)] public int downscaleLimit;

        public bool bicubicUpSampling;

        [Min(0.0f)] public float threshold;

        [Range(0.0f, 1.0f)] public float thresholdKnee;

        [Min(0.0f)] public float intensity;

        public bool fadeFireflies;

        public Mode mode;

        [Range(0.05f, 0.95f)] public float scatter;
    }

    [System.Serializable]
    public struct ColorAdjustmentsSettings
    {
        public float postExposure;

        [Range(-100.0f, 100.0f)] public float contrast;

        [ColorUsage(true, false)] public Color colorFilter;

        [Range(-180.0f, 180.0f)] public float hueShift;

        [Range(-100.0f, 100.0f)] public float saturation;
    }

    [System.Serializable]
    public struct WhiteBalanceSettings
    {
        [Range(-100.0f, 100.0f)] public float temperature;
        
        [Range(-100.0f, 100.0f)] public float tint;
    }

    [System.Serializable]
    public struct SplitToningSettings
    {
        [ColorUsage(false)] public Color shadows;

        [ColorUsage(false)] public Color highlights;

        [Range(-100.0f, 100.0f)] public float balance;
    }

    [System.Serializable]
    public struct ChannelMixerSettings
    {
        public Vector3 red;
        
        public Vector3 green;
        
        public Vector3 blue;
    }

    [System.Serializable]
    public struct ShadowsMidtonesHighlightsSettings
    {
        [ColorUsage(false, true)] public Color shadows;
        
        [ColorUsage(false, true)] public Color midtones;
        
        [ColorUsage(false, true)] public Color highlights;

        [Range(0.0f, 20.0f)] public float shadowsStart;

        [Range(0.0f, 20.0f)] public float shadowsEnd;

        [Range(0.0f, 20.0f)] public float highlightsStart;

        [Range(0.0f, 20.0f)] public float highlightsEnd;
    }

    [System.Serializable]
    public struct ToneMappingSettings
    {
        public enum Mode
        {
            NONE,
            ACES,
            NEUTRAL,
            REINHARD
        }

        public Mode mode;
    }
    
    [SerializeField] private Shader shader;

    [System.NonSerialized] private Material material;

    [SerializeField] private BloomSettings bloom = new BloomSettings
    {
            scatter = 0.7f
    };

    [SerializeField] private ColorAdjustmentsSettings colorAdjustments = new ColorAdjustmentsSettings
    {
            colorFilter = Color.white
    };

    [SerializeField] private WhiteBalanceSettings whiteBalance;

    [SerializeField] private SplitToningSettings splitToning = new SplitToningSettings
    {
            shadows = Color.gray,
            highlights = Color.gray
    };

    [SerializeField] private ChannelMixerSettings channelMixer = new ChannelMixerSettings
    {
            red = Vector3.right,
            green = Vector3.up,
            blue = Vector3.forward
    };

    public ShadowsMidtonesHighlightsSettings shadowsMidtonesHighlights = new ShadowsMidtonesHighlightsSettings
    {
            shadows = Color.white,
            midtones = Color.white,
            highlights = Color.white,
            shadowsEnd = 0.3f,
            highlightsStart = 0.55f,
            highlightsEnd = 1.0f
    };

    [SerializeField] private ToneMappingSettings toneMapping;

    public BloomSettings Bloom => bloom;

    public ColorAdjustmentsSettings ColorAdjustments => colorAdjustments;

    public WhiteBalanceSettings WhiteBalance => whiteBalance;

    public SplitToningSettings SplitToning => splitToning;

    public ChannelMixerSettings ChannelMixer => channelMixer;

    public ShadowsMidtonesHighlightsSettings ShadowsMidtonesHighlights => shadowsMidtonesHighlights;

    public ToneMappingSettings ToneMapping => toneMapping;

    public Material Material
    {
        get
        {
            if (material == null
                && shader != null)
            {
                material = new Material(shader)
                {
                        hideFlags = HideFlags.HideAndDontSave
                };
            }

            return material;
        }
    }
}