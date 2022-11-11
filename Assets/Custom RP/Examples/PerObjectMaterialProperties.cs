using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour {
	
	//着色器属性的标识符，储存在静态变量中
    static int baseColorId = Shader.PropertyToID("_BaseColor");
    static int cutoffId = Shader.PropertyToID("_Cutoff");
    static int metallicId = Shader.PropertyToID("_Metallic");
    static int smoothnessId = Shader.PropertyToID("_Smoothness");
    static int emissionColorId = Shader.PropertyToID("_EmissionColor");
    [SerializeField]
    Color baseColor = Color.white;
    [SerializeField,Range(0f,1f)]
    float cutoff = 0.5f,metallic = 0f, smoothness = 0.5f;
    [SerializeField, ColorUsage(false, true)]
    Color emissionColor = Color.black;
    static MaterialPropertyBlock block;
	
    private void Awake()
    {
	    OnValidate();
    }

    void OnValidate () {
	    if (block == null) {
		    block = new MaterialPropertyBlock();
	    }
	    block.SetFloat(metallicId, metallic);
	    block.SetFloat(smoothnessId, smoothness);
	    block.SetColor(baseColorId, baseColor);
	    block.SetFloat(cutoffId, cutoff);
	    block.SetColor(emissionColorId,emissionColor);
	    GetComponent<Renderer>().SetPropertyBlock(block);
    }//OnValidate 组件load或者修改的时候自动调用
}