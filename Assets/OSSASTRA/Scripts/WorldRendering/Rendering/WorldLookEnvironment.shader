// 功能：把既有环境色调、距离空气和轻量景深/Bloom施加到明确合法的环境像素。
// 边界：中心与邻域都必须有本帧许可和环境alpha；人物、信息、黑幕及混合边缘保持原色。
Shader "Hidden/Ossastra/WorldRendering/EnvironmentLook"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            Name "QualifiedEnvironmentLook"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            TEXTURE2D_X(_WorldLookPermission);
            TEXTURE2D_X_FLOAT(_WorldLookDepth);
            CBUFFER_START(UnityPerMaterial)
                float4 _WorldLookTexelSize, _WorldLookUvTransform;
                float4 _WorldLookFeatures, _WorldLookGrade, _WorldLookFilter, _WorldLookFocus, _WorldLookBloom;
                float4 _WorldLookAirNear, _WorldLookAirMid, _WorldLookAirFar, _WorldLookAirDistances, _WorldLookAirStyle;
                float4x4 _WorldLookInverseViewProjection;
                float _WorldLookVignette;
            CBUFFER_END

            bool EnvironmentAllows(float2 uv)
            {
                float4 permission = SAMPLE_TEXTURE2D_X(_WorldLookPermission, sampler_PointClamp, uv);
                // 保守排除MSAA混合边缘，不把屋顶/人物轮廓的部分覆盖扩成环境许可。
                return min(permission.r, min(permission.g, permission.a)) > .998;
            }
            float RawDepth(float2 uv) { return SAMPLE_TEXTURE2D_X(_WorldLookDepth, sampler_PointClamp, uv).r; }
            float EyeDepth(float raw)
            {
                if (unity_OrthoParams.w > .5)
                {
                    #if UNITY_REVERSED_Z
                        return lerp(_ProjectionParams.z, _ProjectionParams.y, raw);
                    #else
                        return lerp(_ProjectionParams.y, _ProjectionParams.z, raw);
                    #endif
                }
                return LinearEyeDepth(raw, _ZBufferParams);
            }
            float WorldLookLuminance(float3 color) { return dot(color, float3(.2126, .7152, .0722)); }

            void Gather(float2 uv, float2 direction, float radius, float centerDepth, float tolerance,
                inout float3 focusSum, inout float focusWeight, inout float3 bloomSum, inout float bloomWeight)
            {
                float2 sampleUv = clamp(uv + direction * radius * _WorldLookTexelSize.xy, _WorldLookTexelSize.xy * .5, 1 - _WorldLookTexelSize.xy * .5);
                if (!EnvironmentAllows(sampleUv)) return;
                // Point取色与Point许可对齐，不能在线性插值时偷取相邻角色或黑幕的颜色。
                float3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, sampleUv).rgb;
                float difference = abs(EyeDepth(RawDepth(sampleUv)) - centerDepth);
                float weight = 1 - smoothstep(tolerance * .5, tolerance, difference);
                focusSum += color * weight; focusWeight += weight;
                float brightness = max(color.r, max(color.g, color.b));
                bloomSum += color * saturate((brightness - _WorldLookBloom.y) / max(.0001, brightness));
                bloomWeight += 1;
            }
            float3 ApplyAir(float3 color, float2 uv, float raw)
            {
                #if UNITY_REVERSED_Z
                    if (raw <= .000001) return color;
                    float deviceDepth = raw;
                #else
                    if (raw >= .999999) return color;
                    float deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, raw);
                #endif
                float3 world = ComputeWorldSpacePosition(uv, deviceDepth, _WorldLookInverseViewProjection);
                float distanceToCamera = length(world - _WorldSpaceCameraPos);
                float nearToMid = smoothstep(_WorldLookAirDistances.x, _WorldLookAirDistances.y, distanceToCamera);
                float midToFar = smoothstep(_WorldLookAirDistances.y, _WorldLookAirDistances.z, distanceToCamera);
                float3 tint = lerp(lerp(_WorldLookAirNear.rgb, _WorldLookAirMid.rgb, nearToMid), _WorldLookAirFar.rgb, midToFar);
                float amount = 1 - exp(-max(0, distanceToCamera - _WorldLookAirDistances.x) * max(0, _WorldLookAirDistances.w));
                color = lerp(color, WorldLookLuminance(color).xxx, saturate(_WorldLookAirStyle.x) * amount);
                color = lerp(color, max(0, tint), saturate(_WorldLookAirStyle.y) * amount * .3);
                return lerp(color, max(0, tint), saturate(amount + (1 - nearToMid) * _WorldLookAirStyle.z));
            }
            float3 ApplyGrade(float3 color)
            {
                color *= exp2(clamp(_WorldLookGrade.x, -10, 10));
                float temperature = clamp(_WorldLookGrade.w, -100, 100) * .001;
                color *= float3(1 + temperature, 1, 1 - temperature) * max(0, _WorldLookFilter.rgb);
                color = max(0, (color - .18) * max(0, 1 + _WorldLookGrade.y * .01) + .18);
                return max(0, lerp(WorldLookLuminance(color).xxx, color, max(0, 1 + _WorldLookGrade.z * .01)));
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord * _WorldLookUvTransform.xy + _WorldLookUvTransform.zw;
                half4 center = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (!EnvironmentAllows(uv)) return center;
                float raw = RawDepth(uv), depth = EyeDepth(raw);
                float coc = _WorldLookFeatures.y > .5 ? smoothstep(0, max(.001, _WorldLookFocus.z), max(0, abs(depth - _WorldLookFocus.x) - _WorldLookFocus.y)) : 0;
                float3 color = center.rgb;
                if ((_WorldLookFeatures.y > .5 && coc > .0001 && _WorldLookFocus.w > .0001) || (_WorldLookFeatures.w > .5 && _WorldLookBloom.x > .0001))
                {
                    float radius = max(_WorldLookFeatures.y > .5 ? min(3, max(0, _WorldLookFocus.w)) * coc : 0,
                        _WorldLookFeatures.w > .5 ? lerp(1, 3, saturate(_WorldLookBloom.z)) : 0);
                    float tolerance = max(.03, min(.25, _WorldLookFocus.y * .1));
                    float3 focusSum = color * 4, bloomSum = 0; float focusWeight = 4, bloomWeight = 0;
                    Gather(uv, float2(1, 0), radius, depth, tolerance, focusSum, focusWeight, bloomSum, bloomWeight);
                    Gather(uv, float2(-1, 0), radius, depth, tolerance, focusSum, focusWeight, bloomSum, bloomWeight);
                    Gather(uv, float2(0, 1), radius, depth, tolerance, focusSum, focusWeight, bloomSum, bloomWeight);
                    Gather(uv, float2(0, -1), radius, depth, tolerance, focusSum, focusWeight, bloomSum, bloomWeight);
                    Gather(uv, float2(.7071, .7071), radius, depth, tolerance, focusSum, focusWeight, bloomSum, bloomWeight);
                    Gather(uv, float2(-.7071, .7071), radius, depth, tolerance, focusSum, focusWeight, bloomSum, bloomWeight);
                    Gather(uv, float2(.7071, -.7071), radius, depth, tolerance, focusSum, focusWeight, bloomSum, bloomWeight);
                    Gather(uv, float2(-.7071, -.7071), radius, depth, tolerance, focusSum, focusWeight, bloomSum, bloomWeight);
                    if (_WorldLookFeatures.y > .5) color = lerp(color, focusSum / max(1, focusWeight), coc);
                    if (_WorldLookFeatures.w > .5) color += bloomSum / max(1, bloomWeight) * max(0, _WorldLookBloom.x);
                }
                if (_WorldLookFeatures.z > .5) color = ApplyAir(color, uv, raw);
                if (_WorldLookFeatures.x > .5) color = ApplyGrade(color);
                if (_WorldLookVignette > .0001)
                {
                    float2 fromCenter = (uv - .5) * 2;
                    color *= 1 - saturate(dot(fromCenter, fromCenter) * _WorldLookVignette * .5);
                }
                // 不向原黑幕加性补光，防止许可柔边附近被环境色调重新抬亮。
                if (max(center.r, max(center.g, center.b)) <= .000001) return center;
                return half4(max(0, color), center.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
