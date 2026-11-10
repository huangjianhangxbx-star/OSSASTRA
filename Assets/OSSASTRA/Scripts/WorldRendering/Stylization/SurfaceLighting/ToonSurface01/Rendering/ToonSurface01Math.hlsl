/* LOOKDEV-01 v1.3 isolated main-light model.
 * No frame/global state: GPU contracts include these exact production equations.
 * GI and punctual lights are composed separately by ToonSurface01Lighting.hlsl.
 */
#ifndef OSSASTRA_TOON_SURFACE01_MATH_INCLUDED
#define OSSASTRA_TOON_SURFACE01_MATH_INCLUDED
float ToonSurface01Finite(float x,float fallback)
{
    return (asuint(x)&0x7f800000u)==0x7f800000u?fallback:x;
}
float3 ToonSurface01Normalize(float3 v)
{
    return v*rsqrt(max(dot(v,v),1e-8));
}
float3 ToonSurface01BandWeights(float normalLight,float2 thresholds,float softness)
{
    float x=saturate(normalLight);
    float lower=clamp(ToonSurface01Finite(thresholds.x,.32),.001,.998);
    float upper=clamp(ToonSurface01Finite(thresholds.y,.70),lower+.001,.999);
    float width=max(ToonSurface01Finite(softness,.05),.0001);
    float first=smoothstep(lower-width,lower+width,x);
    float second=smoothstep(upper-width,upper+width,x);
    return float3(1-first,first*(1-second),first*second);
}
float3 ToonSurface01RampColor(float normalLight,float2 thresholds,float softness,
    float3 darkColor,float3 midColor,float3 lightColor)
{
    float3 weights=ToonSurface01BandWeights(normalLight,thresholds,softness);
    return max(darkColor,0)*weights.x+max(midColor,0)*weights.y+max(lightColor,0)*weights.z;
}
float3 ToonSurface01MainDiffuse(float3 baseDiffuse,float3 lightColor,float distanceAttenuation,
    float shadowAttenuation,float normalLight,float rampEnabled,float2 thresholds,float softness,
    float3 darkColor,float3 midColor,float3 litColor,float3 shadowTint)
{
    float3 response=saturate(normalLight).xxx;
    [branch] if(rampEnabled>.5)
    {
        response=ToonSurface01RampColor(normalLight,thresholds,softness,darkColor,midColor,litColor);
        // Independent penumbra chroma. Full occlusion still has zero solar energy;
        // existing GI/local lights supply the fully shadowed surface, not fake sun.
        response*=lerp(max(shadowTint,0),float3(1,1,1),saturate(shadowAttenuation));
    }
    return baseDiffuse*max(lightColor,0)*max(distanceAttenuation,0)*saturate(shadowAttenuation)*response;
}
float3 ToonSurface01MainSpecular(float3 normalWS,float3 lightDirection,float3 viewDirection,
    float3 lightColor,float distanceAttenuation,float shadowAttenuation,
    float enabled,float threshold,float softness,float strength,float3 color)
{
    [branch] if(enabled<.5||strength<=0)return 0;
    float3 normal=ToonSurface01Normalize(normalWS);
    float3 light=ToonSurface01Normalize(lightDirection);
    float3 view=ToonSurface01Normalize(viewDirection);
    float3 halfVector=ToonSurface01Normalize(light+view);
    float width=max(ToonSurface01Finite(softness,.03),.0001);
    float center=clamp(ToonSurface01Finite(threshold,.85),0,1);
    float shape=smoothstep(center-width,center+width,saturate(dot(normal,halfVector)));
    float received=saturate(dot(normal,light))*max(distanceAttenuation,0)*saturate(shadowAttenuation);
    return max(color,0)*max(strength,0)*shape*received*max(lightColor,0);
}
float3 ToonSurface01MainRim(float3 normalWS,float3 lightDirection,float3 viewDirection,
    float3 lightColor,float distanceAttenuation,float shadowAttenuation,
    float enabled,float exponent,float strength,float3 color)
{
    [branch] if(enabled<.5||strength<=0)return 0;
    float3 normal=ToonSurface01Normalize(normalWS);
    float3 light=ToonSurface01Normalize(lightDirection);
    float3 view=ToonSurface01Normalize(viewDirection);
    float shape=pow(1-saturate(dot(normal,view)),clamp(ToonSurface01Finite(exponent,4),1,16));
    float received=saturate(dot(normal,light))*max(distanceAttenuation,0)*saturate(shadowAttenuation);
    return max(color,0)*max(strength,0)*shape*received*max(lightColor,0);
}
#endif
