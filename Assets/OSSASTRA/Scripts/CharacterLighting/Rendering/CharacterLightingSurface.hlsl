/* 功能：从单个角色完整alpha缓存求有限的内侧窄带与屏幕外向方向，不生成颜色或发光。
 * 边界：只消费自身形状，拒绝相邻角色tile、内部半透明叠层接缝和近零梯度；最终显示权仍在原角色片元。
 * 坐标：rasterXY与缓存ScreenRect均为最终target左上角坐标；返回xy外向单位向量、z内带权重。
 */
#ifndef OSSASTRA_CHARACTER_LIGHTING_SURFACE_INCLUDED
#define OSSASTRA_CHARACTER_LIGHTING_SURFACE_INCLUDED
TEXTURE2D(_CharacterCoverageTexture);SAMPLER(sampler_CharacterCoverageTexture);
float _CharacterCoverageReady;
float4 _CharacterCoverageRect,_CharacterCoverageAtlasRect,_CharacterCoverageScreenSize,_CharacterCoverageTexelSize;

float CharacterLightingCoverageAlphaAt(float2 localUV,float4 atlasRect,float2 texelSize,Texture2D coverage,SamplerState coverageSampler)
{
 if(any(localUV<0)||any(localUV>1))return 0;
 float2 minimum=atlasRect.xy+texelSize*.5;
 float2 maximum=atlasRect.xy+atlasRect.zw-texelSize*.5;
 float2 atlasUV=clamp(atlasRect.xy+localUV*atlasRect.zw,minimum,maximum);
 return saturate(SAMPLE_TEXTURE2D(coverage,coverageSampler,atlasUV).a);
}
float CharacterLightingCoverageAlpha(float2 localUV)
{
 return CharacterLightingCoverageAlphaAt(localUV,_CharacterCoverageAtlasRect,_CharacterCoverageTexelSize.xy,_CharacterCoverageTexture,sampler_CharacterCoverageTexture);
}
float3 CharacterLightingRimDirectionAndBandAt(float2 rasterXY,float width,float softness,float4 screenRect,float4 screenSize,
 float4 atlasRect,float2 texelSize,Texture2D coverage,SamplerState coverageSampler)
{
 if(width<=0||any(screenRect.zw<=0)||any(screenSize.xy<=0))return 0;
 float2 localUV=(rasterXY*screenSize.zw-screenRect.xy)/screenRect.zw;
 if(any(localUV<0)||any(localUV>1))return 0;
 #define COVERAGE_AT(uv) CharacterLightingCoverageAlphaAt((uv),atlasRect,texelSize,coverage,coverageSampler)
 float center=COVERAGE_AT(localUV);if(center<.00001)return 0;
 float2 radius=width*screenSize.zw/screenRect.zw;
 float left=COVERAGE_AT(localUV-float2(radius.x,0));
 float right=COVERAGE_AT(localUV+float2(radius.x,0));
 float top=COVERAGE_AT(localUV-float2(0,radius.y));
 float bottom=COVERAGE_AT(localUV+float2(0,radius.y));
 float topLeft=COVERAGE_AT(localUV-radius);
 float topRight=COVERAGE_AT(localUV+float2(radius.x,-radius.y));
 float bottomLeft=COVERAGE_AT(localUV+float2(-radius.x,radius.y));
 float bottomRight=COVERAGE_AT(localUV+radius);
 #undef COVERAGE_AT
 float background=min(min(min(left,right),min(top,bottom)),min(min(topLeft,topRight),min(bottomLeft,bottomRight)));
 // 相邻采样必须真的接近背景；.5与.75的slot叠层差异不是人物自身轮廓。
 float relativeBackground=background/max(center,.00001);
 float backgroundGate=1-smoothstep(.015,.08,relativeBackground);
 if(backgroundGate<=0)return 0;
 float2 outward=float2(left-right,top-bottom)+.5*float2(topLeft+bottomLeft-topRight-bottomRight,topLeft+topRight-bottomLeft-bottomRight);
 float lengthSquared=dot(outward,outward);if(lengthSquared<.00000001)return 0;
 float band=pow(saturate((center-background)/max(center,.00001)),clamp(softness,.05,2));
 band*=backgroundGate*saturate(center/.02);
 return float3(outward*rsqrt(lengthSquared),band);
}
float3 CharacterLightingRimDirectionAndBand(float2 rasterXY,float width,float softness)
{
 if(_CharacterCoverageReady<.5)return 0;
 return CharacterLightingRimDirectionAndBandAt(rasterXY,min(width,4),softness,_CharacterCoverageRect,_CharacterCoverageScreenSize,
  _CharacterCoverageAtlasRect,_CharacterCoverageTexelSize.xy,_CharacterCoverageTexture,sampler_CharacterCoverageTexture);
}
#endif
