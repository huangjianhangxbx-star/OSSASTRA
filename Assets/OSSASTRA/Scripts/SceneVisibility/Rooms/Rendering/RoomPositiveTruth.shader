// 功能：仅绘制合法表面的纯色真值，先裁逻辑视域再写颜色与深度。
// 边界：保留原图集alpha/薄顶面/竖直角色深度；不是实际材质光照验收。
Shader "Hidden/Ossastra/Rooms/PositiveTruth"
{
 Properties
 {
  _RoomPositiveDepthWrite("真值写深度/标签只比较",Float)=1
  _RoomPositivePartial("有限视域片元",Float)=0
  _RoomPositiveCutRoof("精确屋顶预览候选",Float)=0
  _RoomPositiveColor("真值颜色",Color)=(0,1,1,1)
  _RoomSurfaceMode("0 Lit / 1 ThinTop / 2 Decoration / 3 Body / 4 PMA",Float)=0
  _RoomClipToVolumes("跨区域表面裁到活动体积",Float)=0
  _RoomClipUseKnown("采用已到达体积",Float)=0
  _RoomMaskChannels("写R或RG",Float)=12
  _RoomSourceAlphaClip("源材质alpha test",Float)=0
  _Cull("源材质背面裁剪",Float)=2
  _BaseMap("Lit纹理",2D)="white"{} _BaseColor("Lit颜色",Color)=(1,1,1,1)
  [PerRendererData] _MainTex("源图集",2D)="white"{} _Color("源颜色",Color)=(1,1,1,1)
  _Cutoff("源alpha阈值",Float)=.06
  _GroundBounds("地表界限",Vector)=(-100000,-100000,100000,100000) _BaseCoverage("地表基层",Float)=0
  [HideInInspector] _VisualDepthFoot("角色脚点",Vector)=(0,0,0,1)
  [HideInInspector] _VisualDepthNormal("角色环境深度平面",Vector)=(0,0,1,0)
  [HideInInspector] _VisualDepthEnabled("角色自定义深度",Float)=0
  [HideInInspector] _StencilRef("原Stencil",Float)=1
  [HideInInspector] _StencilComp("原Stencil比较",Float)=8
  [HideInInspector] _RendererColor("RendererColor",Color)=(1,1,1,1)
  [HideInInspector] _Flip("Flip",Vector)=(1,1,1,1)
  [PerRendererData] _AlphaTex("External Alpha",2D)="white"{}
  [PerRendererData] _EnableExternalAlpha("Enable External Alpha",Float)=0
  [MaterialToggle] PixelSnap("Pixel snap",Float)=0
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline"}
  Cull[_Cull] ZWrite[_RoomPositiveDepthWrite] ZTest LEqual Blend Off ColorMask RGBA
  Stencil{Ref[_StencilRef] Comp[_StencilComp] Pass Keep}
  Pass
  {
   Name "PositiveCustomDepthTruth"
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_local _ _ROOM_STYLE_COVERAGE _ROOM_STYLE_WINDOW_ONLY
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST,_MainTex_ST,_BaseColor,_Color,_GroundBounds;
    float4 _VisualDepthFoot,_VisualDepthNormal;
    float _RoomSurfaceMode,_RoomClipToVolumes,_RoomClipUseKnown,_RoomSourceAlphaClip,_Cutoff,_BaseCoverage,_VisualDepthEnabled;
   CBUFFER_END
   #include "RoomSightClip.hlsl"
   #include "RoomStyleCoverage.hlsl"
   float _RoomPositivePartial,_RoomPositiveCutRoof;float4 _RoomPositiveColor;
   #include "Assets/OSSASTRA/Scripts/Characters/Rendering/Shaders/CharacterUprightDepthCommon.hlsl"
   struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float4 color:COLOR;float eyeOffset:TEXCOORD2;nointerpolation float planeY:TEXCOORD3;};
   struct O{half4 color:SV_Target0;float depth:SV_Depth;
   #if defined(_ROOM_STYLE_COVERAGE)
    float4 style:SV_Target1;
   #endif
   };
   V Vert(A input)
   {
    V output;output.world=TransformObjectToWorld(input.positionOS.xyz);output.positionCS=TransformWorldToHClip(output.world);
    output.uv=input.uv;output.color=input.color;output.eyeOffset=CharacterVisualEyeOffset(output.world);output.planeY=output.world.y;return output;
   }
   float ThinDepth(float2 rasterPixel,float planeY)
   {
    float2 uv=rasterPixel/_ScaledScreenParams.xy;
    float3 nearWS=ComputeWorldSpacePosition(uv,UNITY_NEAR_CLIP_VALUE,UNITY_MATRIX_I_VP);
    float4 farH=mul(UNITY_MATRIX_I_VP,ComputeClipSpacePosition(uv,UNITY_RAW_FAR_CLIP_VALUE));
    float3 direction=farH.xyz-nearWS*farH.w;clip(abs(direction.y)-1e-8);
    float t=(planeY-nearWS.y)/direction.y;float3 onPlane=nearWS+direction*t;onPlane.y=planeY;
    float3 view=TransformWorldToView(onPlane);float eye=-view.z;clip(eye-_ProjectionParams.y);clip(_ProjectionParams.z-eye);
    float4 projected=TransformWViewToHClip(view);float ndc=projected.z/projected.w;
    #if UNITY_REVERSED_Z
     return saturate(ndc);
    #else
     return saturate((ndc-UNITY_NEAR_CLIP_VALUE)/(1.0-UNITY_NEAR_CLIP_VALUE));
    #endif
   }
   O Frag(V input)
   {
    float depth=input.positionCS.z;
    if(_RoomSurfaceMode<.5)
    {
     if(_RoomSourceAlphaClip>.5)clip(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv*_BaseMap_ST.xy+_BaseMap_ST.zw).a*_BaseColor.a-_Cutoff);
    }
    else if(_RoomSurfaceMode<1.5)
    {
     clip(input.world.x-_GroundBounds.x);clip(input.world.z-_GroundBounds.y);clip(_GroundBounds.z-input.world.x);clip(_GroundBounds.w-input.world.z);
     half alpha=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv).a;
     alpha=lerp(alpha,1,saturate(_BaseCoverage));clip(alpha*(input.color*_Color).a-_Cutoff);depth=ThinDepth(input.positionCS.xy,input.planeY);
    }
    else if(_RoomSurfaceMode<2.5)
    {
     half4 color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv)*input.color*_Color;clip(color.a-_Cutoff);
    }
    else if(_RoomSurfaceMode<3.5)
    {
     half4 color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv*_MainTex_ST.xy+_MainTex_ST.zw)*input.color*_Color;
     clip(color.a-_Cutoff);depth=CharacterVisualDeviceDepth(input.positionCS,input.eyeOffset);
    }
    else
    {
     float alpha=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv).a*input.color.a;clip(alpha-0.000001);
     depth=CharacterVisualDeviceDepth(input.positionCS,input.eyeOffset);
    }
    float3 logicalWorld=RoomSightWorldFromDeviceDepth(input.positionCS.xy,depth);
    if(_RoomPositivePartial>.5)clip(RoomSightPartialContains(logicalWorld)?1:-1);
    RoomSightPreviewRoofClip(input.positionCS.xy,_RoomPositiveCutRoof);
    O output;output.color=RoomSightTruthColor(logicalWorld,_RoomPositiveColor,_RoomPositivePartial);output.depth=depth;
    #if defined(_ROOM_STYLE_WINDOW_ONLY)
     output.color=RoomStyleCoverage(logicalWorld);
    #endif
    #if defined(_ROOM_STYLE_COVERAGE)
     output.style=RoomStyleCoverage(logicalWorld);
    #endif
    return output;
   }
   ENDHLSL
  }
  Pass
  {
   Name "PositiveSpriteTruth"
   Cull Off
   CGPROGRAM
   #pragma target 4.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_local _ _ROOM_STYLE_COVERAGE _ROOM_STYLE_WINDOW_ONLY
   #pragma multi_compile_instancing
   #pragma multi_compile _ PIXELSNAP_ON
   #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
   #include "UnitySprites.cginc"
   #include "RoomSightClip.hlsl"
   #include "RoomStyleCoverage.hlsl"
   float _RoomPositivePartial,_RoomPositiveCutRoof;float4 _RoomPositiveColor;
   float4 _VisualDepthFoot,_VisualDepthNormal;float _VisualDepthEnabled,_RoomClipToVolumes,_RoomClipUseKnown;
   struct V{float4 vertex:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float eyeOffset:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
   struct O{float4 color:SV_Target0;float depth:SV_Depth;
   #if defined(_ROOM_STYLE_COVERAGE)
    float4 style:SV_Target1;
   #endif
   };
   V Vert(appdata_t input)
   {
    UNITY_SETUP_INSTANCE_ID(input);v2f original=SpriteVert(input);V output;UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.vertex=original.vertex;output.color=original.color;output.uv=original.texcoord;
    float3 world=mul(unity_ObjectToWorld,UnityFlipSprite(input.vertex.xyz,_Flip)).xyz;
    output.eyeOffset=-UnityWorldToViewPos(world).z+UnityWorldToViewPos(_VisualDepthFoot.xyz).z;
    #ifdef PIXELSNAP_ON
     float4 rawClip=UnityObjectToClipPos(UnityFlipSprite(input.vertex.xyz,_Flip));float2 delta=original.vertex.xy-rawClip.xy;
     float a=UNITY_MATRIX_P[0][0],b=UNITY_MATRIX_P[0][1],c=UNITY_MATRIX_P[1][0],d=UNITY_MATRIX_P[1][1];float determinant=a*d-b*c;
     if(abs(determinant)>1e-8){float2 viewDelta=float2(d*delta.x-b*delta.y,-c*delta.x+a*delta.y)/determinant;world+=UNITY_MATRIX_V[0].xyz*viewDelta.x+UNITY_MATRIX_V[1].xyz*viewDelta.y;}
    #endif
    output.world=world;return output;
   }
   float SpriteDepth(V input)
   {
    if(_VisualDepthEnabled<.5)return input.vertex.z;
    float3 forward=-UNITY_MATRIX_V[2].xyz;
    float3 origin=unity_OrthoParams.w>.5?input.world:_WorldSpaceCameraPos;
    float3 direction=unity_OrthoParams.w>.5?forward:input.world-_WorldSpaceCameraPos;
    float denominator=dot(direction,_VisualDepthNormal.xyz);clip(abs(denominator)-1e-8);
    float t=dot(_VisualDepthFoot.xyz-origin,_VisualDepthNormal.xyz)/denominator;
    float3 view=UnityWorldToViewPos(origin+direction*t);view.z-=input.eyeOffset;
    float eye=-view.z;clip(eye-_ProjectionParams.y);clip(_ProjectionParams.z-eye);
    float4 projected=mul(UNITY_MATRIX_P,float4(view,1));float ndc=projected.z/projected.w;
    #if UNITY_REVERSED_Z
     return saturate(ndc);
    #else
     return saturate((ndc-UNITY_NEAR_CLIP_VALUE)/(1.0-UNITY_NEAR_CLIP_VALUE));
    #endif
   }
   O Frag(V input)
   {
    fixed4 color=SampleSpriteTexture(input.uv)*input.color;clip(color.a-0.000001);
    float depth=SpriteDepth(input);float3 logicalWorld=RoomSightWorldFromDeviceDepth(input.vertex.xy,depth);
    if(_RoomPositivePartial>.5)clip(RoomSightPartialContains(logicalWorld)?1:-1);
    RoomSightPreviewRoofClip(input.vertex.xy,_RoomPositiveCutRoof);
    O output;output.color=RoomSightTruthColor(logicalWorld,_RoomPositiveColor,_RoomPositivePartial);output.depth=depth;
    #if defined(_ROOM_STYLE_WINDOW_ONLY)
     output.color=RoomStyleCoverage(logicalWorld);
    #endif
    #if defined(_ROOM_STYLE_COVERAGE)
     output.style=RoomStyleCoverage(logicalWorld);
    #endif
    return output;
   }
   ENDCG
  }
  Pass
  {
   Name "UnexploredInteriorVolume"
   Cull Off ColorMask G
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A{float4 positionOS:POSITION;};
   struct V{float4 positionCS:SV_POSITION;};
   struct O{half4 color:SV_Target;float depth:SV_Depth;};
   float _RoomCameraInsideUnknownVolume;
   V Vert(A input){V output;output.positionCS=TransformObjectToHClip(input.positionOS.xyz);return output;}
   O Frag(V input)
   {
    O output;output.color=0;output.depth=input.positionCS.z;
    if(_RoomCameraInsideUnknownVolume>.5)
    {
     #if UNITY_REVERSED_Z
      output.depth=1;
     #else
      output.depth=0;
     #endif
    }
    return output;
   }
   ENDHLSL
  }
  Pass
  {
   Name "PositiveHardwareTruth"
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_local _ _ROOM_STYLE_COVERAGE _ROOM_STYLE_WINDOW_ONLY
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "RoomSightClip.hlsl"
   #include "RoomStyleCoverage.hlsl"
   float _RoomPositivePartial,_RoomPositiveCutRoof;float4 _RoomPositiveColor;
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST,_MainTex_ST,_BaseColor,_Color,_GroundBounds;
    float _RoomSurfaceMode,_RoomClipToVolumes,_RoomClipUseKnown,_RoomSourceAlphaClip,_Cutoff,_BaseCoverage;
   CBUFFER_END
   struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float4 color:COLOR;};
   V Vert(A input)
   {
    V output;output.world=TransformObjectToWorld(input.positionOS.xyz);output.positionCS=TransformWorldToHClip(output.world);
    output.uv=input.uv;output.color=input.color*_Color;return output;
   }
   // 不输出SV_Depth，保留原普通表面在MSAA各采样位置的硬件深度插值。
   RoomStyleOutput Frag(V input)
   {
    float3 logicalWorld=input.world;
    if(_RoomPositivePartial>.5)clip(RoomSightPartialContains(logicalWorld)?1:-1);
    RoomSightPreviewRoofClip(input.positionCS.xy,_RoomPositiveCutRoof);
    if(_RoomSurfaceMode<.5)
    {
     if(_RoomSourceAlphaClip>.5)clip(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv*_BaseMap_ST.xy+_BaseMap_ST.zw).a*_BaseColor.a-_Cutoff);
    }
    else if(_RoomSurfaceMode>5.5)
    {
     clip(input.world.x-_GroundBounds.x);clip(input.world.z-_GroundBounds.y);clip(_GroundBounds.z-input.world.x);clip(_GroundBounds.w-input.world.z);
     half alpha=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv).a;
     clip(lerp(alpha,1,saturate(_BaseCoverage))*input.color.a-_Cutoff);
    }
    else
    {
     float2 uv=_RoomSurfaceMode>2.5?input.uv*_MainTex_ST.xy+_MainTex_ST.zw:input.uv;
     half4 color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv)*input.color;clip(color.a-_Cutoff);
    }
    return RoomStylePack(RoomSightTruthColor(logicalWorld,_RoomPositiveColor,_RoomPositivePartial),logicalWorld);
   }
   ENDHLSL
  }
 }
}
