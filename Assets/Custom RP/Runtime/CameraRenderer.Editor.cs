﻿using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Profiling;
public partial class CameraRenderer
{
    partial void PrepareForSceneWindow ();
    partial void DrawGizmos ();
    partial void DrawGizmosBeforeFX ();

    partial void DrawGizmosAfterFX ();
    partial void DrawUnsupportedShaders ();
    partial void PrepareBuffer();



#if UNITY_EDITOR
    private string SampleName { get; set; }
    partial void PrepareForSceneWindow () {
        if (camera.cameraType == CameraType.SceneView) {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
        }
    }

    partial void PrepareBuffer()
    {
        Profiler.BeginSample("Editor Only");
        buffer.name = SampleName=camera.name;
        Profiler.EndSample();
    }
    partial void DrawGizmosBeforeFX () {
        if (Handles.ShouldRenderGizmos()) {
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            //context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
    }

    partial void DrawGizmosAfterFX () {
        if (Handles.ShouldRenderGizmos()) {
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
    }
    private static ShaderTagId[] legacyShaderTagIds =
    {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };
    //出现错误后的材质
    private static Material errorMaterial;
    partial void DrawUnsupportedShaders ()
    {
        if (errorMaterial == null)
        {
            errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
        }
        var drawingSettings = new DrawingSettings(legacyShaderTagIds[0], new SortingSettings(camera))
        {
            overrideMaterial =errorMaterial
        };
        var filteringSettings = FilteringSettings.defaultValue;
        for (int i = 1; i < legacyShaderTagIds.Length; i++)
        {
            drawingSettings.SetShaderPassName(i,legacyShaderTagIds[i]);
        }
        context.DrawRenderers(cullingResults,ref drawingSettings,ref filteringSettings);
    }
#else

    const string SampleName = bufferName;
    
#endif
}
