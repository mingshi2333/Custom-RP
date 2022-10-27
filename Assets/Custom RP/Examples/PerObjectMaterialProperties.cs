using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour {
	
	//着色器属性的标识符，储存在静态变量中
    static int baseColorId = Shader.PropertyToID("_BaseColor");
    static int cutoffId = Shader.PropertyToID("_Cutoff");
	
    [SerializeField]
    Color baseColor = Color.white;
    [SerializeField,Range(0f,1f)]
    float cutoff = 0.5f;
    static MaterialPropertyBlock block;
	
    private void Awake()
    {
	    OnValidate();
    }

    void OnValidate () {
	    if (block == null) {
		    block = new MaterialPropertyBlock();
	    }
	    block.SetColor(baseColorId, baseColor);
	    block.SetFloat(cutoffId, cutoff);
	    GetComponent<Renderer>().SetPropertyBlock(block);
    }//OnValidate 组件load或者修改的时候自动调用
}