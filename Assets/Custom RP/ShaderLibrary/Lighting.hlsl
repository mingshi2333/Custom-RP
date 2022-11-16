#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

int GetDirectionalLightCount () {
    return _DirectionalLightCount;
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
    light.direction = _DirectionalLightDirections[index].xyz;
    return light;
}
float3 GetLighting (Surface surfaceWS,BRDF brdf,GI gi) {
    
    ShadowData shadowData = GetShadowData(surfaceWS);
    shadowData.shadowMask = gi.shadowMask;
    //float3 color = 0.0;
    float3 color = gi.diffuse*brdf.diffuse;//初始颜色为lightmap采样的颜色
    for (int i = 0; i < GetDirectionalLightCount(); i++) {
        Light light = GetDirectionalLight(i,surfaceWS,shadowData);
        color += GetLighting(surfaceWS,brdf,light);
    }
    return color;
    //return gi.shadowMask.shadows.xyz;
}

#endif