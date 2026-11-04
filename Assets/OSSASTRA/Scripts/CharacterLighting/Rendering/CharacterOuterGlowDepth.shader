/* 功能：从当前合法世界附件解析最近深度；与环境远样本copy用途不同，保守拒绝MSAA前景边缘上的柔光。
 * 边界：仅私有R32数据，不画世界颜色；不读取旧CameraDepthTexture，不写硬件深度。
 */
Shader "Hidden/Ossastra/CharacterLighting/OuterGlowDepth"
{
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline"}
  Cull Off ZWrite Off ZTest LEqual
  Pass
  {
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_local _ _CHARACTER_OUTER_MSAA_2 _CHARACTER_OUTER_MSAA_4 _CHARACTER_OUTER_MSAA_8
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #if defined(_CHARACTER_OUTER_MSAA_2)
    #define OUTER_SAMPLES 2
   #elif defined(_CHARACTER_OUTER_MSAA_4)
    #define OUTER_SAMPLES 4
   #elif defined(_CHARACTER_OUTER_MSAA_8)
    #define OUTER_SAMPLES 8
   #else
    #define OUTER_SAMPLES 1
   #endif
   #if OUTER_SAMPLES==1
    TEXTURE2D(_CharacterOuterDepthSource);
   #else
    Texture2DMS<float,OUTER_SAMPLES> _CharacterOuterDepthSource;
   #endif
   float4 _CharacterCoverageScreenSize;
   struct V{float4 positionCS:SV_POSITION;};
   V Vert(uint id:SV_VertexID){V o;o.positionCS=GetFullScreenTriangleVertexPosition(id);return o;}
   float Frag(V i):SV_Target
   {
    int2 pixel=clamp(int2(i.positionCS.xy),0,(int2)_CharacterCoverageScreenSize.xy-1);
    #if OUTER_SAMPLES==1
     return LOAD_TEXTURE2D(_CharacterOuterDepthSource,pixel).r;
    #else
     #if UNITY_REVERSED_Z
      float depth=0;
     #else
      float depth=1;
     #endif
     [unroll]for(int s=0;s<OUTER_SAMPLES;s++){
      float value=_CharacterOuterDepthSource.Load(pixel,s);
      #if UNITY_REVERSED_Z
       depth=max(depth,value);
      #else
       depth=min(depth,value);
      #endif
     }return depth;
    #endif
   }
   ENDHLSL
  }
 }
 Fallback Off
}
