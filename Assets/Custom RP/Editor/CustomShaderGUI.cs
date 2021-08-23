using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomShaderGUI : ShaderGUI
{
    MaterialEditor editor;
    Object[] materials;
    MaterialProperty[] properties;

    bool showPresets;

    bool Clipping
    {
        set => SetProperty("_Clipping", "_CLIPPING", value);
    }

    bool HasPremultiplyAlpha => HasProperty("_PremulAlpha");

    bool PremultiplyAlpha
    {
        set => SetProperty("_PremulAlpha", "_PREMULTIPLY_ALPHA", value);
    }

    BlendMode SrcBlend
    {
        set => SetProperty("_SrcBlend", (float) value);
    }

    BlendMode DstBlend
    {
        set => SetProperty("_DstBlend", (float) value);
    }

    bool ZWrite
    {
        set => SetProperty("_ZWrite", value ? 1f : 0f);
    }

    enum ShadowMode
    {
        ON,
        CLIP,
        DITHER,
        OFF
    }

    ShadowMode Shadows
    {
        set
        {
            if (SetProperty("_Shadows", (float) value))
            {
                SetKeyword("_SHADOWS_CLIP", value == ShadowMode.CLIP);
                SetKeyword("_SHADOWS_DITHER", value == ShadowMode.DITHER);
            }
        }
    }

    RenderQueue RenderQueue
    {
        set
        {
            foreach (Material material in materials)
            {
                material.renderQueue = (int) value;
            }
        }
    }

    public override void OnGUI(MaterialEditor _materialEditor, MaterialProperty[] _properties)
    {
        EditorGUI.BeginChangeCheck();
        
        base.OnGUI(_materialEditor, _properties);
        
        editor = _materialEditor;
        materials = _materialEditor.targets;
        properties = _properties;

        BakedEmission();

        EditorGUILayout.Space();
        
        showPresets = EditorGUILayout.Foldout(showPresets, "Presets", true);
        
        if (showPresets)
        {
            OpaquePreset();
            ClipPreset();
            FadePreset();
            TransparentPreset();
        }

        if (EditorGUI.EndChangeCheck())
        {
            SetShadowCasterPass();
            CopyLightMappingProperties();
        }
    }

    void CopyLightMappingProperties()
    {
        MaterialProperty mainTex = FindProperty("_MainTex", properties, false);
        MaterialProperty baseMap = FindProperty("_BaseMap", properties, false);
        
        if (mainTex != null
            && baseMap != null)
        {
            mainTex.textureValue = baseMap.textureValue;
            mainTex.textureScaleAndOffset = baseMap.textureScaleAndOffset;
        }

        MaterialProperty color = FindProperty("_Color", properties, false);
        MaterialProperty baseColor = FindProperty("_BaseColor", properties, false);
        
        if (color != null
            && baseColor != null)
        {
            color.colorValue = baseColor.colorValue;
        }
    }

    void BakedEmission()
    {
        EditorGUI.BeginChangeCheck();
        
        editor.LightmapEmissionProperty();
        
        if (EditorGUI.EndChangeCheck())
        {
            foreach (Material material in editor.targets)
            {
                material.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }
    }

    void OpaquePreset()
    {
        if (PresetButton("Opaque"))
        {
            Clipping = false;
            Shadows = ShadowMode.ON;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.Geometry;
        }
    }

    void ClipPreset()
    {
        if (PresetButton("Clip"))
        {
            Clipping = true;
            Shadows = ShadowMode.CLIP;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.AlphaTest;
        }
    }

    void FadePreset()
    {
        if (PresetButton("Fade"))
        {
            Clipping = false;
            Shadows = ShadowMode.DITHER;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.SrcAlpha;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
        }
    }

    void TransparentPreset()
    {
        if (HasPremultiplyAlpha
            && PresetButton("Transparent"))
        {
            Clipping = false;
            Shadows = ShadowMode.DITHER;
            PremultiplyAlpha = true;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
        }
    }

    bool PresetButton(string _name)
    {
        if (GUILayout.Button(_name))
        {
            editor.RegisterPropertyChangeUndo(_name);
            
            return true;
        }

        return false;
    }

    bool HasProperty(string _name) => FindProperty(_name, properties, false) != null;

    void SetProperty(string _name, string _keyword, bool _value)
    {
        if (SetProperty(_name, _value ? 1f : 0f))
        {
            SetKeyword(_keyword, _value);
        }
    }

    bool SetProperty(string _name, float _value)
    {
        MaterialProperty property = FindProperty(_name, properties, false);
        
        if (property != null)
        {
            property.floatValue = _value;
            
            return true;
        }

        return false;
    }

    void SetKeyword(string _keyword, bool _enabled)
    {
        if (_enabled)
        {
            foreach (Material material in materials)
            {
                material.EnableKeyword(_keyword);
            }
        }
        else
        {
            foreach (Material material in materials)
            {
                material.DisableKeyword(_keyword);
            }
        }
    }

    void SetShadowCasterPass()
    {
        MaterialProperty shadows = FindProperty("_Shadows", properties, false);
        
        if (shadows == null
            || shadows.hasMixedValue)
        {
            return;
        }

        bool enabled = shadows.floatValue < (float) ShadowMode.OFF;
        
        foreach (Material m in materials)
        {
            m.SetShaderPassEnabled("ShadowCaster", enabled);
        }
    }
}