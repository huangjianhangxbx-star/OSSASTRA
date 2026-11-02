// 真实开口许可：只对当前帧真实场景深度之后的窗景写许可；开口前沿射线必须位于活动室内。
Shader "Hidden/Ossastra/Rooms/PortalMask"
{
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline"}
  Pass
  {
   Cull Off ZWrite Off ZTest LEqual Blend Off ColorMask RB
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "RoomMaskVolume.hlsl"
   float4 _RoomPortalOutward;
   struct A{float4 positionOS:POSITION;};
   struct V{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;};
   V Vert(A input){V output;output.world=TransformObjectToWorld(input.positionOS.xyz);output.positionCS=TransformWorldToHClip(output.world);return output;}
   half4 Frag(V input):SV_Target
   {
    float3 towardCamera=unity_OrthoParams.w>.5?UNITY_MATRIX_V[2].xyz:_WorldSpaceCameraPos-input.world;
    towardCamera=normalize(towardCamera);
    // 仅从室内侧向外看：相机本体可在屋外上方，但窗口前的射线段必须位于已揭顶房间。
    clip(-dot(towardCamera,_RoomPortalOutward.xyz)-0.0001);
    clip(RoomMaskContainsKnown(input.world+towardCamera*0.025)?1:-1);
    return 1;
   }
   ENDHLSL
  }
 }
}
