// 功能：仅对已知Lit／ThinTop／DecorationCutout捕获最近device-depth数据；原可见颜色仍由原材质绘制。
// 边界：保真alpha裁剪和薄面统一深度，不支持未知形变或任意透明材质，不改角色shader。
Shader "Hidden/Ossastra/SceneVisibility/OwnedGroupDeviceDepth"
{
 Properties
 {
  _VisDataMode("0 Lit / 1 ThinTop / 3 DecorationCutout",Float)=0 _Cull("Cull",Float)=2
  _BaseMap("Lit alpha",2D)="white"{} _BaseColor("Lit color",Color)=(1,1,1,1)
  _VisAlphaClip("Lit alpha test",Float)=0 _Cutoff("Alpha cutoff",Float)=.5
  _MainTex("Thin mask",2D)="white"{} _Color("Thin vertex color",Color)=(1,1,1,1)
  _GroundBounds("Thin bounds",Vector)=(-100000,-100000,100000,100000) _BaseCoverage("Thin base",Float)=0
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline"}
  Pass
  {
   Cull[_Cull] ZWrite On ZTest LEqual Blend Off Offset 0,0
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST,_BaseColor,_Color,_GroundBounds;float _VisDataMode,_VisAlphaClip,_Cutoff,_BaseCoverage;
   CBUFFER_END
   struct A{float4 p:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;nointerpolation float planeY:TEXCOORD2;float4 color:COLOR;};
   struct O{float data:SV_Target;float depth:SV_Depth;};
   V Vert(A a){V o;o.world=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.world);o.planeY=o.world.y;o.uv=a.uv;o.color=a.color*_Color;return o;}
   float ThinDepth(float2 rasterPixel,float planeY)
   {
    float2 uv=rasterPixel/_ScaledScreenParams.xy;
    float3 nearWS=ComputeWorldSpacePosition(uv,UNITY_NEAR_CLIP_VALUE,UNITY_MATRIX_I_VP);
    float4 farH=mul(UNITY_MATRIX_I_VP,ComputeClipSpacePosition(uv,UNITY_RAW_FAR_CLIP_VALUE));
    float3 direction=farH.xyz-nearWS*farH.w;clip(abs(direction.y)-1e-8);
    float t=(planeY-nearWS.y)/direction.y;float3 onPlane=nearWS+direction*t;onPlane.y=planeY;
    float3 view=TransformWorldToView(onPlane);float eye=-view.z;clip(eye-_ProjectionParams.y);clip(_ProjectionParams.z-eye);
    float4 projected=TransformWViewToHClip(view);float d=projected.z/projected.w;
    #if UNITY_REVERSED_Z
     return saturate(d);
    #else
     return saturate((d-UNITY_NEAR_CLIP_VALUE)/(1.0-UNITY_NEAR_CLIP_VALUE));
    #endif
   }
   O Frag(V i)
   {
    float d=i.p.z;
    if(_VisDataMode>2.5)
    {
     // Match the audited source shader's half4 intermediate and exact texture*vertex*_Color alpha.
     half4 color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*i.color;clip(color.a-_Cutoff);
    }
    else if(_VisDataMode>.5)
    {
     clip(i.world.x-_GroundBounds.x);clip(i.world.z-_GroundBounds.y);clip(_GroundBounds.z-i.world.x);clip(_GroundBounds.w-i.world.z);
     float mask=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a;mask=lerp(mask,1,saturate(_BaseCoverage));clip(mask*i.color.a-_Cutoff);
     d=ThinDepth(i.p.xy,i.planeY);
    }
    else if(_VisAlphaClip>.5)clip(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv*_BaseMap_ST.xy+_BaseMap_ST.zw).a*_BaseColor.a-_Cutoff);
    O o;o.data=d;o.depth=d;return o;
   }
   ENDHLSL
  }
 }
}
