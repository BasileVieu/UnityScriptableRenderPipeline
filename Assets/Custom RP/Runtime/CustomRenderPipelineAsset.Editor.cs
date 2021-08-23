using UnityEngine;

partial class CustomRenderPipelineAsset
{
    #if UNITY_EDITOR
    private static string[] renderingLayerNames;

    public override string[] renderingLayerMaskNames => renderingLayerNames;

    static CustomRenderPipelineAsset()
    {
        renderingLayerNames = new string[31];

        for (var i = 0; i < renderingLayerNames.Length; i++)
        {
            renderingLayerNames[i] = "Layer " + (i + 1);
        }
    }
    #endif
}