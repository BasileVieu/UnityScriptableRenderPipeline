using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting
{
    private const string bufferName = "Lighting";

    private const int maxDirLightCount = 4;
    private const int maxOtherLightCount = 64;

    private static int dirLightCountId = Shader.PropertyToID("_DirectionalLightCount");
    private static int dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors");
    private static int dirLightDirectionsAndMasksId = Shader.PropertyToID("_DirectionalLightDirectionsAndMasks");
    private static int dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");

    private static int otherLightCountId = Shader.PropertyToID("_OtherLightCount");
    private static int otherLightColorsId = Shader.PropertyToID("_OtherLightColors");
    private static int otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions");
    private static int otherLightDirectionsAndMasksId = Shader.PropertyToID("_OtherLightDirectionsAndMasks");
    private static int otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles");
    private static int otherLightShadowDataId = Shader.PropertyToID("_OtherLightShadowData");

    private static Vector4[] dirLightColors = new Vector4[maxDirLightCount];
    private static Vector4[] dirLightDirectionsAndMasks = new Vector4[maxDirLightCount];
    private static Vector4[] dirLightShadowData = new Vector4[maxDirLightCount];

    private static Vector4[] otherLightColors = new Vector4[maxOtherLightCount];
    private static Vector4[] otherLightPositions = new Vector4[maxOtherLightCount];
    private static Vector4[] otherLightDirectionsAndMasks = new Vector4[maxOtherLightCount];
    private static Vector4[] otherLightSpotAngles = new Vector4[maxOtherLightCount];
    private static Vector4[] otherLightShadowData = new Vector4[maxOtherLightCount];

    private static string lightsPerObjectKeyword = "_LIGHTS_PER_OBJECT";

    private CommandBuffer buffer = new CommandBuffer
    {
            name = bufferName
    };

    private CullingResults cullingResults;

    private Shadows shadows = new Shadows();

    public void Setup(ScriptableRenderContext _context, CullingResults _cullingResults, ShadowSettings _shadowSettings, bool _useLightsPerObject, int _renderingLayerMask)
    {
        cullingResults = _cullingResults;
        
        buffer.BeginSample(bufferName);
        
        shadows.Setup(_context, _cullingResults, _shadowSettings);
        
        SetupLights(_useLightsPerObject, _renderingLayerMask);
        
        shadows.Render();
        
        buffer.EndSample(bufferName);
        
        _context.ExecuteCommandBuffer(buffer);
        
        buffer.Clear();
    }

    public void Cleanup()
    {
        shadows.Cleanup();
    }

    void SetupLights(bool _useLightsPerObject, int _renderingLayerMask)
    {
        NativeArray<int> indexMap = _useLightsPerObject ? cullingResults.GetLightIndexMap(Allocator.Temp) : default;
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
        var dirLightCount = 0;
        var otherLightCount = 0;

        int i;

        for (i = 0; i < visibleLights.Length; i++)
        {
            int newIndex = -1;
            
            VisibleLight visibleLight = visibleLights[i];

            Light light = visibleLight.light;

            if ((light.renderingLayerMask & _renderingLayerMask) != 0)
            {
                switch (visibleLight.lightType)
                {
                    case LightType.Directional:
                    {
                        if (dirLightCount < maxDirLightCount)
                        {
                            SetupDirectionalLight(dirLightCount++, i, ref visibleLight, light);
                        }
                    }

                        break;

                    case LightType.Point:
                    {
                        if (otherLightCount < maxOtherLightCount)
                        {
                            newIndex = otherLightCount;

                            SetupPointLight(otherLightCount++, i, ref visibleLight, light);
                        }

                        break;
                    }

                    case LightType.Spot:
                    {
                        if (otherLightCount < maxOtherLightCount)
                        {
                            newIndex = otherLightCount;

                            SetupSpotLight(otherLightCount++, i, ref visibleLight, light);
                        }

                        break;
                    }
                }
            }

            if (_useLightsPerObject)
            {
                indexMap[i] = newIndex;
            }
        }

        if (_useLightsPerObject)
        {
            for (; i < indexMap.Length; i++)
            {
                indexMap[i] = -1;
            }

            cullingResults.SetLightIndexMap(indexMap);

            indexMap.Dispose();

            Shader.EnableKeyword(lightsPerObjectKeyword);
        }
        else
        {
            Shader.DisableKeyword(lightsPerObjectKeyword);
        }

        buffer.SetGlobalInt(dirLightCountId, dirLightCount);

        if (dirLightCount > 0)
        {
            buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
            buffer.SetGlobalVectorArray(dirLightDirectionsAndMasksId, dirLightDirectionsAndMasks);
            buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
        }

        buffer.SetGlobalInt(otherLightCountId, otherLightCount);

        if (otherLightCount > 0)
        {
            buffer.SetGlobalVectorArray(otherLightColorsId, otherLightColors);
            buffer.SetGlobalVectorArray(otherLightPositionsId, otherLightPositions);
            buffer.SetGlobalVectorArray(otherLightDirectionsAndMasksId, otherLightDirectionsAndMasks);
            buffer.SetGlobalVectorArray(otherLightSpotAnglesId, otherLightSpotAngles);
            buffer.SetGlobalVectorArray(otherLightShadowDataId, otherLightShadowData);
        }
    }

    void SetupDirectionalLight(int _index, int _visibleIndex, ref VisibleLight _visibleLight, Light _light)
    {
        dirLightColors[_index] = _visibleLight.finalColor;
        
        Vector4 dirAndMask = -_visibleLight.localToWorldMatrix.GetColumn(2);
        dirAndMask.w = _light.renderingLayerMask.ReinterpretAsFloat();

        dirLightDirectionsAndMasks[_index] = dirAndMask;
        dirLightShadowData[_index] = shadows.ReserveDirectionalShadows(_light, _visibleIndex);
    }

    void SetupPointLight(int _index, int _visibleIndex, ref VisibleLight _visibleLight, Light _light)
    {
        otherLightColors[_index] = _visibleLight.finalColor;

        Vector4 position = _visibleLight.localToWorldMatrix.GetColumn(3);
        position.w = 1.0f / Mathf.Max(_visibleLight.range * _visibleLight.range, 0.00001f);
        
        otherLightPositions[_index] = position;
        otherLightSpotAngles[_index] = new Vector4(0.0f, 1.0f);

        Vector4 dirAndMask = Vector4.zero;
        dirAndMask.w = _light.renderingLayerMask.ReinterpretAsFloat();

        otherLightDirectionsAndMasks[_index] = dirAndMask;
        
        otherLightShadowData[_index] = shadows.ReserveOtherShadows(_light, _visibleIndex);
    }

    void SetupSpotLight(int _index, int _visibleIndex, ref VisibleLight _visibleLight, Light _light)
    {
        otherLightColors[_index] = _visibleLight.finalColor;

        Vector4 position = _visibleLight.localToWorldMatrix.GetColumn(3);
        position.w = 1.0f / Mathf.Max(_visibleLight.range * _visibleLight.range, 0.00001f);
        
        otherLightPositions[_index] = position;
        
        Vector4 dirAndMask = -_visibleLight.localToWorldMatrix.GetColumn(2);
        dirAndMask.w = _light.renderingLayerMask.ReinterpretAsFloat();

        otherLightDirectionsAndMasks[_index] = dirAndMask;
        
        float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * _light.innerSpotAngle);
        float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * _visibleLight.spotAngle);
        float angleRangeInv = 1.0f / Mathf.Max(innerCos - outerCos, 0.001f);

        otherLightSpotAngles[_index] = new Vector4(angleRangeInv, -outerCos * angleRangeInv);
        otherLightShadowData[_index] = shadows.ReserveOtherShadows(_light, _visibleIndex);
    }
}