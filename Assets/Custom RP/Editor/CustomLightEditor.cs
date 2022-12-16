using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

[CanEditMultipleObjects]//选中多个物体操作
[CustomEditorForRenderPipeline(typeof(Light), typeof(CustomRenderPipelineAsset))]
public class CustomLightEditor : LightEditor
{
    static GUIContent renderingLayerMaskLabel =
        new GUIContent("Rendering Layer Mask", "Functional version of above property.");
    public override void OnInspectorGUI() {
        base.OnInspectorGUI();
        RenderingLayerMaskDrawer.Draw(
            settings.renderingLayerMask, renderingLayerMaskLabel
        );
        if (
            !settings.lightType.hasMultipleDifferentValues &&
            (LightType)settings.lightType.enumValueIndex == LightType.Spot
        )
        {
            settings.DrawInnerAndOuterSpotAngle();
            //settings.ApplyModifiedProperties();
        }
        settings.ApplyModifiedProperties();//始终应用到灯光
        var light = target as Light;
        if (light.cullingMask != -1) {
            EditorGUILayout.HelpBox(
                light.type == LightType.Directional ?
                    "Culling Mask only affects shadows." :
                    "Culling Mask only affects shadow unless Use Lights Per Objects is on.",
                MessageType.Warning
            );//-1代表所有图层，在OnInspectorGUI的最后调用EditorGUILayout.HelpBox，用一个字符串表示遮罩只影响阴影，用MessageType.Warning来显示一个警告图标。
        }
    }
    /*void DrawRenderingLayerMask () {
        SerializedProperty property = settings.renderingLayerMask;
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        int mask = property.intValue;
        if (mask == int.MaxValue)
        {
            mask = -1;
        }
        mask = EditorGUILayout.MaskField(
            renderingLayerMaskLabel, mask,
            GraphicsSettings.currentRenderPipeline.renderingLayerMaskNames
        );//显示下拉菜单通过EditorGUILayout.MaskField
        if (EditorGUI.EndChangeCheck()) {
            property.intValue = mask == -1?int.MaxValue: mask ;
        }
        EditorGUI.showMixedValue = false;
    }*/
    
}