/* 功能：保留Sprites/Default与Upright变体的原SpriteVert、PMA、Flip、PixelSnap及外部alpha。
 * 边界：从最终深度重建许可点后裁剪；不将透明Sprite改为alpha-test，不改变源色彩。
 * 生命周期：只供正向适配材质，源材质和表现根由原组件持有。
 */
Shader "Hidden/Ossastra/Rooms/SightSprite"
{
 Properties
 {
  [PerRendererData] _MainTex("Sprite Texture",2D)="white"{}
  _Color("Tint",Color)=(1,1,1,1)
  [MaterialToggle] PixelSnap("Pixel snap",Float)=0
  [HideInInspector] _RendererColor("RendererColor",Color)=(1,1,1,1)
  [HideInInspector] _Flip("Flip",Vector)=(1,1,1,1)
  [PerRendererData] _AlphaTex("External Alpha",2D)="white"{}
  [PerRendererData] _EnableExternalAlpha("Enable External Alpha",Float)=0
  [HideInInspector] _VisualDepthFoot("Owner foot",Vector)=(0,0,0,1)
  [HideInInspector] _VisualDepthNormal("Horizontal normal",Vector)=(0,0,1,0)
  [HideInInspector] _VisualDepthEnabled("Gameplay camera only",Float)=0
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True"}
  Cull Off Lighting Off ZWrite Off ZTest LEqual Blend One OneMinusSrcAlpha
  Pass
  {
   CGPROGRAM
   #pragma target 4.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #pragma multi_compile _ PIXELSNAP_ON
   #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
   #include "UnitySprites.cginc"
   #include "RoomSightClip.hlsl"
   float4 _VisualDepthFoot,_VisualDepthNormal;float _VisualDepthEnabled;
   struct V{float4 vertex:SV_POSITION;fixed4 color:COLOR;float2 texcoord:TEXCOORD0;float3 rayWorld:TEXCOORD1;float eyeOffset:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
   struct O{fixed4 color:SV_Target;float depth:SV_Depth;};
   V Vert(appdata_t input)
   {
    UNITY_SETUP_INSTANCE_ID(input);
    v2f original=SpriteVert(input);V output;UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.vertex=original.vertex;output.color=original.color;output.texcoord=original.texcoord;
    float3 world=mul(unity_ObjectToWorld,UnityFlipSprite(input.vertex.xyz,_Flip)).xyz;
    output.eyeOffset=-UnityWorldToViewPos(world).z+UnityWorldToViewPos(_VisualDepthFoot.xyz).z;
    #ifdef PIXELSNAP_ON
     // SpriteVert已做原生PixelSnap。保持eye不变，校正ray插值点，使其仍对应真正被snap后的像素射线。
     float4 rawClip=UnityObjectToClipPos(UnityFlipSprite(input.vertex.xyz,_Flip));float2 delta=original.vertex.xy-rawClip.xy;
     float a=UNITY_MATRIX_P[0][0],b=UNITY_MATRIX_P[0][1],c=UNITY_MATRIX_P[1][0],d=UNITY_MATRIX_P[1][1];float determinant=a*d-b*c;
     if(abs(determinant)>1e-8){float2 viewDelta=float2(d*delta.x-b*delta.y,-c*delta.x+a*delta.y)/determinant;world+=UNITY_MATRIX_V[0].xyz*viewDelta.x+UNITY_MATRIX_V[1].xyz*viewDelta.y;}
    #endif
    output.rayWorld=world;return output;
   }
   float DeviceDepth(V input)
   {
    if(_VisualDepthEnabled<.5)return input.vertex.z;
    float3 forward=-UNITY_MATRIX_V[2].xyz;
    float3 origin=unity_OrthoParams.w>.5?input.rayWorld:_WorldSpaceCameraPos;
    float3 direction=unity_OrthoParams.w>.5?forward:input.rayWorld-_WorldSpaceCameraPos;
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
    float deviceDepth=DeviceDepth(input);
    float3 logicalWorld=RoomSightWorldFromDeviceDepth(input.vertex.xy,deviceDepth);
    clip(RoomSightPartialContains(logicalWorld)?1:-1);
    // 与UnitySprites.cginc的SpriteFrag逐式相同，不反预乘、不改alpha、不加颜色裁剪。
    fixed4 color=SampleSpriteTexture(input.texcoord)*input.color;color.rgb*=color.a;
    O output;output.color=RoomSightFogColor(color,logicalWorld,1);output.depth=deviceDepth;return output;
   }
   ENDCG
  }
  // 原Sprite家族的硬件逐采样深度；调用方按真实源Shader选pass1。
  Pass
  {
   Name "SourceHardwareDepth"
   CGPROGRAM
   #pragma target 4.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #pragma multi_compile _ PIXELSNAP_ON
   #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
   #include "UnitySprites.cginc"
   #include "RoomSightClip.hlsl"
   float4 _VisualDepthFoot,_VisualDepthNormal;float _VisualDepthEnabled;
   struct V{float4 vertex:SV_POSITION;fixed4 color:COLOR;float2 texcoord:TEXCOORD0;float3 rayWorld:TEXCOORD1;float eyeOffset:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};

   V Vert(appdata_t input)
   {
    UNITY_SETUP_INSTANCE_ID(input);
    v2f original=SpriteVert(input);V output;UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.vertex=original.vertex;output.color=original.color;output.texcoord=original.texcoord;
    float3 world=mul(unity_ObjectToWorld,UnityFlipSprite(input.vertex.xyz,_Flip)).xyz;
    output.eyeOffset=-UnityWorldToViewPos(world).z+UnityWorldToViewPos(_VisualDepthFoot.xyz).z;
    #ifdef PIXELSNAP_ON
     // SpriteVert已做原生PixelSnap。保持eye不变，校正ray插值点，使其仍对应真正被snap后的像素射线。
     float4 rawClip=UnityObjectToClipPos(UnityFlipSprite(input.vertex.xyz,_Flip));float2 delta=original.vertex.xy-rawClip.xy;
     float a=UNITY_MATRIX_P[0][0],b=UNITY_MATRIX_P[0][1],c=UNITY_MATRIX_P[1][0],d=UNITY_MATRIX_P[1][1];float determinant=a*d-b*c;
     if(abs(determinant)>1e-8){float2 viewDelta=float2(d*delta.x-b*delta.y,-c*delta.x+a*delta.y)/determinant;world+=UNITY_MATRIX_V[0].xyz*viewDelta.x+UNITY_MATRIX_V[1].xyz*viewDelta.y;}
    #endif
    output.rayWorld=world;return output;
   }
   float DeviceDepth(V input)
   {
    if(_VisualDepthEnabled<.5)return input.vertex.z;
    float3 forward=-UNITY_MATRIX_V[2].xyz;
    float3 origin=unity_OrthoParams.w>.5?input.rayWorld:_WorldSpaceCameraPos;
    float3 direction=unity_OrthoParams.w>.5?forward:input.rayWorld-_WorldSpaceCameraPos;
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
   fixed4 Frag(V input):SV_Target
   {
    float deviceDepth=input.vertex.z;
    float3 logicalWorld=RoomSightWorldFromDeviceDepth(input.vertex.xy,deviceDepth);
    clip(RoomSightPartialContains(logicalWorld)?1:-1);
    // 与UnitySprites.cginc的SpriteFrag逐式相同，不反预乘、不改alpha、不加颜色裁剪。
    fixed4 color=SampleSpriteTexture(input.texcoord)*input.color;color.rgb*=color.a;
    return RoomSightFogColor(color,logicalWorld,1);
   }
   ENDCG
  }
 }
}
