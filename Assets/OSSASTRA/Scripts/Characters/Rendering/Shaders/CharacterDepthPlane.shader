// 保留现有MapResearch/DepthPlane的颜色、PMA、透明裁剪和render state，仅替换本实例观察深度。
Shader "Ossastra/Characters/UprightDepthPlane"
{
 Properties
 {
  [MainTexture] _MainTex("Texture",2D)="white"{}
  [MainColor] _Color("Tint",Color)=(1,1,1,1)
  _Cutoff("Depth Alpha Cutoff",Range(0.001,1))=.06
  [Toggle] _PmaInput("Premultiplied Texture Input",Float)=0
  [HideInInspector] _VisualDepthFoot("Owner foot",Vector)=(0,0,0,1)
  [HideInInspector] _VisualDepthNormal("Horizontal view normal",Vector)=(0,0,1,0)
  [HideInInspector] _VisualDepthEnabled("Gameplay camera only",Float)=0
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest"}
  Pass
  {
   Tags{"LightMode"="SRPDefaultUnlit"}
   Cull Off ZWrite On ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;half4 _Color;half _Cutoff;half _PmaInput;
    float4 _VisualDepthFoot;float4 _VisualDepthNormal;float _VisualDepthEnabled;
   CBUFFER_END
   #include "Assets/OSSASTRA/Scripts/Characters/Rendering/Shaders/CharacterUprightDepthCommon.hlsl"
   struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
   struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;float eyeOffset:TEXCOORD1;};
   struct O{half4 color:SV_Target;float depth:SV_Depth;};
   V Vert(A input){V output;float3 world=TransformObjectToWorld(input.positionOS.xyz);output.positionCS=TransformWorldToHClip(world);output.uv=TRANSFORM_TEX(input.uv,_MainTex);output.color=input.color*_Color;output.eyeOffset=CharacterVisualEyeOffset(world);return output;}
   O Frag(V input){half4 texel=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv);if(_PmaInput>.5h)texel.rgb/=max(texel.a,.001h);half4 color=texel*input.color;clip(color.a-_Cutoff);O output;output.color=color;output.depth=CharacterVisualDeviceDepth(input.positionCS,input.eyeOffset);return output;}
   ENDHLSL
  }
 }
}
