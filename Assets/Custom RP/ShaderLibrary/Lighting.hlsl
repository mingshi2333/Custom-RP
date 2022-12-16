#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED


/**
 * \brief 
 * \param surface 
 * \param light
 *
 * 返回表面和光线的遮罩是否重叠。这是通过检查位掩码的位和是否为非零来实现的。
 * \return 
 */
bool RenderingLayersOverlap (Surface surface, Light light) {
    return (surface.renderingLayerMask & light.renderingLayerMask) != 0;
}

float3 IncomingLight (Surface surface, Light light) {
    return  saturate(dot(surface.normal, light.direction)*light.attenuation) * light.color;
}
float3 GetLighting (Surface surface,BRDF brdf, Light light) {
    return IncomingLight(surface, light) *DirectBRDF(surface,brdf,light);
}

Light GetDirectionalLight(int index)
{
    Light light;
    light.color = _DirectionalLightColors[index].rgb;
    light.direction = _DirectionalLightDirectionsAndMasks[index].xyz;
    return light;
}
float3 GetLighting (Surface surfaceWS,BRDF brdf,GI gi) {
    
    ShadowData shadowData = GetShadowData(surfaceWS);
    shadowData.shadowMask = gi.shadowMask;
    //float3 color = 0.0;
    //float3 color = gi.diffuse*brdf.diffuse;//初始颜色为lightmap采样的颜色
    float3 color = IndirectBRDF(surfaceWS, brdf, gi.diffuse, gi.specular);
    for (int i = 0; i < GetDirectionalLightCount(); i++) {
        Light light = GetDirectionalLight(i,surfaceWS,shadowData);
        if (RenderingLayersOverlap(surfaceWS, light)) {
            color += GetLighting(surfaceWS, brdf, light);
        }
    }
    #if defined(_LIGHTS_PER_OBJECT)
        for (int j = 0; j < min(unity_LightData.y,8); j++) {
            int lightIndex = unity_LightIndices[(uint)j / 4][(uint)j % 4];
            Light light = GetOtherLight(lightIndex, surfaceWS, shadowData);
            //color += GetLighting(surfaceWS, brdf, light);//物体灯光数组里找 限制
            if (RenderingLayersOverlap(surfaceWS, light)) {
                color += GetLighting(surfaceWS, brdf, light);
            }
        }
    #else
        for(int j = 0;j<GetOtherLightCount();j++)
        {
            Light light = GetOtherLight(j,surfaceWS,shadowData);
            //color+=GetLighting(surfaceWS,brdf,light);
            if (RenderingLayersOverlap(surfaceWS, light)) {
                color += GetLighting(surfaceWS, brdf, light);
            }
        }
    #endif
    
    return color;
    //return gi.shadowMask.shadows.xyz;
}



#endif