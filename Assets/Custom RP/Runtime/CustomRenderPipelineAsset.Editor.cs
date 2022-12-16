partial class CustomRenderPipelineAsset {

#if UNITY_EDITOR

    static string[] renderingLayerNames;

    static CustomRenderPipelineAsset () {
        renderingLayerNames = new string[31];//最高位为32，灯光的渲染层遮罩在内部被存储为无符号整数，即uint，但是SerializedProperty只支持获取和设置有符号的整数值。every的-1和超出范围的32最终对吼变成0
        for (int i = 0; i < renderingLayerNames.Length; i++) {
            renderingLayerNames[i] = "Layer " + (i + 1);
        }
    }

    public override string[] renderingLayerMaskNames => renderingLayerNames;

#endif
}