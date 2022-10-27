#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

int GetDirectionalLightCount () {
    return _DirectionalLightCount;
}
float3 IncomingLight (Surface surface, Light light) {
    return  saturate(dot(surface.normal, light.direction)) * light.color;
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
float3 GetLighting (Surface surface,BRDF brdf) {
    float3 color = 0.0;
    for (int i = 0; i < GetDirectionalLightCount(); i++) {
        color += GetLighting(surface,brdf,GetDirectionalLight(i));
    }
    return color;
}

#endif