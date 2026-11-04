/* 功能：将同一角色当前变形网格的有效alpha合成为完整自身轮廓；不画世界颜色。
 * 边界：ZTest Always仅用于私有alpha缓存，不读取场景深度或观察许可，不授予主画面显示权。
 * 生命周期：由CharacterCoverageCache的单个自有材质使用，逐draw数据通过独有全局命令写入。
 */
Shader "Hidden/Ossastra/CharacterLighting/Coverage"
{
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline"}
  Pass
  {
   Cull Off ZWrite Off ZTest Always Blend One OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_CharacterCoverageSourceTexture);SAMPLER(sampler_CharacterCoverageSourceTexture);
   float4x4 _CharacterCoverageVP;
   float4 _CharacterCoverageSourceST,_CharacterCoverageSourceFlip;
   float _CharacterCoverageSourceAlpha,_CharacterCoverageSourceCutoff;
   struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float alpha:TEXCOORD1;};
   V Vert(A input){V output;input.positionOS.xy*=_CharacterCoverageSourceFlip.xy;output.positionCS=mul(_CharacterCoverageVP,mul(unity_ObjectToWorld,input.positionOS));output.uv=input.uv*_CharacterCoverageSourceST.xy+_CharacterCoverageSourceST.zw;output.alpha=input.color.a*_CharacterCoverageSourceAlpha;return output;}
   float4 Frag(V input):SV_Target
   {
    float alpha=saturate(SAMPLE_TEXTURE2D(_CharacterCoverageSourceTexture,sampler_CharacterCoverageSourceTexture,input.uv).a*input.alpha);
    clip(alpha-max(_CharacterCoverageSourceCutoff,0.00001));
    return alpha.xxxx;
   }
   ENDHLSL
  }
 }
}
