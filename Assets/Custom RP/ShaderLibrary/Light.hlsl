#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED

struct Light {
    float3 color;
    float3 direction;
    float attenuation;
};

DirectionalShadowData GetDirectionalShadowData (int lightIndex) {
    DirectionalShadowData data;
    data.strength = _DirectionalLightShadowData[lightIndex].x;
    data.tileIndex = _DirectionalLightShadowData[lightIndex].y;
    return data;
}

Light GetDirectionalLight () {
    Light light;
    light.color = 1.0;
    light.direction = float3(0.0, 1.0, 0.0);
    return light;
}

Light GetDirectionalLight (int index, Surface surfaceWS) {
    Light light;
    light.color = _DirectionalLightColors[index].rgb;
    light.direction = _DirectionalLightDirections[index].xyz;
    DirectionalShadowData shadowData = GetDirectionalShadowData(index);
    light.attenuation = GetDirectionalShadowAttenuation(shadowData, surfaceWS);
    return light;
}



#endif