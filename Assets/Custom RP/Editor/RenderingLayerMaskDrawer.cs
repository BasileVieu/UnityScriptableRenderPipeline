using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[CustomPropertyDrawer(typeof(RenderingLayerMaskFieldAttribute))]
public class RenderingLayerMaskDrawer : PropertyDrawer
{
    public override void OnGUI(Rect _position, SerializedProperty _property, GUIContent _label)
    {
        Draw(_position, _property, _label);
    }

    public static void Draw(SerializedProperty _property, GUIContent _label)
    {
        Draw(EditorGUILayout.GetControlRect(), _property, _label);
    }
    
    public static void Draw(Rect _position, SerializedProperty _property, GUIContent _label)
    {
        EditorGUI.showMixedValue = _property.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();

        int mask = _property.intValue;

        bool isUint = _property.type == "uint";

        if (isUint
            && mask == int.MaxValue)
        {
            mask = -1;
        }

        mask = EditorGUI.MaskField(_position, _label, mask,
                                   GraphicsSettings.currentRenderPipeline.renderingLayerMaskNames);

        if (EditorGUI.EndChangeCheck())
        {
            _property.intValue = isUint && mask == -1 ? int.MaxValue : mask;
        }

        EditorGUI.showMixedValue = false;
    }
}