// LOOKDEV-01: finite opaque 3D inverted hull; no source shader, alpha or mesh normals are changed.
Shader "Hidden/Ossastra/Stylization/FiniteOutline"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "FiniteOutline"
            Cull Front ZWrite Off ZTest LEqual Blend One Zero ColorMask RGB
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/OSSASTRA/Scripts/SceneVisibility/Rooms/Rendering/RoomSightClip.hlsl"
            float4 _LookDevOutlineColor;
            float4 _LookDevOutlineOutputSize;
            float _LookDevOutlinePixels, _LookDevOutlineRoomMode, _LookDevOutlineCutRoof, _LookDevOutlineSmooth;
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 smoothNormalOS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 normalOS = input.normalOS;
                if (_LookDevOutlineSmooth > .5 && dot(input.smoothNormalOS,input.smoothNormalOS) > .01)
                    normalOS = input.smoothNormalOS;
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
                float3 normalVS = TransformWorldToViewDir(normalWS);
                float2 projected = mul(UNITY_MATRIX_P, float4(normalVS,0)).xy * _LookDevOutlineOutputSize.xy;
                float lengthSquared = dot(projected,projected);
                float2 direction = lengthSquared > 1e-8 ? projected * rsqrt(lengthSquared) : float2(0,0);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionCS.xy += direction * (2 * _LookDevOutlinePixels * _LookDevOutlineOutputSize.zw) * output.positionCS.w;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (_LookDevOutlineRoomMode > 1.5)
                    RoomSightPreviewRoofClip(input.positionCS.xy,_LookDevOutlineCutRoof);
                else if (_LookDevOutlineRoomMode > .5)
                {
                    // Test the expanded raster point, not the original vertex; the hull must not extend portal permission.
                    float3 world = RoomSightWorldFromDeviceDepth(input.positionCS.xy,input.positionCS.z);
                    clip(RoomSightPartialContains(world) ? 1 : -1);
                }
                return half4(_LookDevOutlineColor.rgb,1);
            }
            ENDHLSL
        }
    }
}
