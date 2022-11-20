#ifndef CUSTOM_BRDF_INCLUDED
#define CUSTOM_BRDF_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"
#define MIN_REFLECTIVITY  0.04

TEXTURECUBE(unity_SpecCube0);
SAMPLER(samplerunity_SpecCube0);

struct BRDF {
    float3 diffuse;
    float3 specular;
    float occlusion;
    float roughness;
    float perceptualRoughness;
    float fresnel;
};
float OneMinusReflectivity(float metallic)
{
    float range = 1.0-MIN_REFLECTIVITY;
    return range-metallic*range;
}
BRDF GetBRDF(Surface surface,bool applyAlphaToDiffuse = false)
{
    BRDF brdf;
    float oneMinusReflectivity = OneMinusReflectivity(surface.metallic);
    brdf.diffuse = surface.color*oneMinusReflectivity;
    if(applyAlphaToDiffuse==true)
    {
        brdf.diffuse *= surface.alpha;
    }
    brdf.diffuse *= surface.alpha;
    brdf.specular = lerp(MIN_REFLECTIVITY, surface.color, surface.metallic);//插值，保证非金属返回白色，金属反射表面颜色
    brdf.perceptualRoughness = PerceptualSmoothnessToRoughness(surface.smoothness);
    brdf.roughness = PerceptualRoughnessToRoughness(brdf.perceptualRoughness);
    brdf.fresnel = saturate(surface.smoothness+1.0-oneMinusReflectivity);
    return brdf;
}
float SpecularStrength (Surface surface, BRDF brdf, Light light) {
    float3 h = SafeNormalize(light.direction + surface.viewDirection);
    float nh2 = Square(saturate(dot(surface.normal, h)));
    float lh2 = Square(saturate(dot(light.direction, h)));
    float r2 = Square(brdf.roughness);
    float d2 = Square(nh2 * (r2 - 1.0) + 1.00001);
    float normalization = brdf.roughness * 4.0 + 2.0;
    return r2 / (d2 * max(0.1, lh2) * normalization);
}

float3 DirectBRDF (Surface surface, BRDF brdf, Light light) {
    return SpecularStrength(surface, brdf, light) * brdf.specular + brdf.diffuse;
}

float3 SampleEnvironment(Surface surfaceWS,BRDF brdf)
{
    //float3 uvw = 0.0;
    float mip = PerceptualRoughnessToMipmapLevel(brdf.perceptualRoughness);
    float3 uvw = reflect(-surfaceWS.viewDirection,surfaceWS.normal);
    float4 environment = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0,samplerunity_SpecCube0,uvw,mip);
    return DecodeHDREnvironment(environment, unity_SpecCube0_HDR);
}

float3 IndirectBRDF(Surface surface,BRDF brdf,float3 diffuse,float3 specular)
{
    /*float fresnelStrength =surface.fresnelStrength * Pow4(1.0-saturate(dot(surface.viewDirection,surface.normal)));
    float3 reflection = specular * lerp(brdf.specular, brdf.fresnel, fresnelStrength);
    reflection /= brdf.roughness*brdf.roughness+1.0;
    
    //return diffuse*brdf.diffuse+reflection;
    return reflection*surface.occlusion;*/
    float fresnelStrength = surface.fresnelStrength *
    Pow4(1.0 - saturate(dot(surface.normal, surface.viewDirection)));
    float3 reflection =
        specular * lerp(brdf.specular, brdf.fresnel, fresnelStrength);
    reflection /= brdf.roughness * brdf.roughness + 1.0;
	
    return (diffuse * brdf.diffuse + reflection) * surface.occlusion;
}

#endif