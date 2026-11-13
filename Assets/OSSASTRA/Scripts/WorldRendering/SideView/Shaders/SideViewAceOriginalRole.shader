Shader "Ossastra/SideView/AceOriginalRole"
{
    Properties
    {
        [NoScaleOffset] _MainTex("Original color page (Gamma values)", 2D) = "white" {}
        [NoScaleOffset] _Extra("Extra data page", 2D) = "black" {}
        [NoScaleOffset] _Extra2("Extra2 data page (original values)", 2D) = "black" {}
        _AmbientScale("Ambient response", Range(0, 1)) = 1
        _AlphaScale("Coverage multiplier", Range(0, 1)) = 1
        _AceHighlightThickness("Original texture scale", Float) = 1
        _AceOriginalFull("Use Bright, Highlight and Emission", Float) = 1
        _AceOriginalChannels("Diagnostic channels (Bright, Highlight, Emission)", Vector) = (1,1,1,0)
        _Emission("Extra G emission", Vector) = (0,0,0,0)
        _EmissionX("Extra2 R emission", Vector) = (0,0,0,0)
        _EmissionY("Extra2 G emission", Vector) = (0,0,0,0)
        _EmissionZ("Extra2 B emission", Vector) = (0,0,0,0)
        _EmissionA("Extra2 A emission", Vector) = (0,0,0,0)
        _EmissionIntensity("Original material emission intensity", Range(-1, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "OriginalRole"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RoleVert
            #pragma fragment RoleFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/OSSASTRA/Scripts/WorldRendering/SideView/Shaders/SideViewAceLightMaps.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_Extra); SAMPLER(sampler_Extra);
            TEXTURE2D(_Extra2); SAMPLER(sampler_Extra2);
            CBUFFER_START(UnityPerMaterial)
            float4 _Extra_TexelSize;
            float4 _AceOriginalChannels;
            float4 _Emission, _EmissionX, _EmissionY, _EmissionZ, _EmissionA;
            float _AmbientScale, _AlphaScale, _AceHighlightThickness, _AceOriginalFull, _EmissionIntensity;
            CBUFFER_END

            struct Attributes { float3 position : POSITION; float4 uv : TEXCOORD0; };
            struct Varyings { float4 position : SV_POSITION; float4 uv : TEXCOORD2; };

            Varyings RoleVert(Attributes input)
            {
                Varyings output;
                output.position = TransformObjectToHClip(input.position);
                output.uv = input.uv;
                return output;
            }

            float3 RoleEmission(float4 extra, float4 extra2)
            {
                float4 weights = float4(extra2.rgb, extra.g);
                weights *= step(.018, weights);
                return _Emission.rgb * (_Emission.a * weights.a) +
                    _EmissionX.rgb * (_EmissionX.a * weights.r) +
                    _EmissionY.rgb * (_EmissionY.a * weights.g) +
                    _EmissionZ.rgb * (_EmissionZ.a * weights.b) +
                    _EmissionA.rgb * (_EmissionA.a * extra2.a);
            }

            float4 RoleFrag(Varyings input) : SV_Target
            {
                float2 uv = input.uv.xy;
                float4 extra = SAMPLE_TEXTURE2D(_Extra, sampler_Extra, uv);
                float3 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
                SideViewAceMaps maps = SideViewAceSample(input.position, 1);
                color *= lerp(1, maps.ambient, _AmbientScale);

                if (_AceOriginalFull > .5)
                {
                    // PS152 interprets the legacy map vector in world XY and transforms it into sprite-local XY.
                    float2 direction = TransformWorldToObjectDir(float3(maps.direction, 0), false).xy;
                    float2 offset = direction * _Extra_TexelSize.xy * (12 * _AceHighlightThickness);
                    float offsetAlpha = SAMPLE_TEXTURE2D(_Extra, sampler_Extra, uv + offset).a;
                    float4 extra2 = SAMPLE_TEXTURE2D(_Extra2, sampler_Extra2, uv);
                    color += maps.bright * (extra.r * _AceOriginalChannels.x);
                    color += maps.highlight * ((1 - offsetAlpha) * _AceOriginalChannels.y);
                    color += RoleEmission(extra, extra2) * (saturate(maps.emission + _EmissionIntensity) * _AceOriginalChannels.z);
                }

                // The original shader samples and composes Gamma values. Local source pages preserve those
                // raw values in a linear sampler, including original bilinear interpolation before composition.
#if !defined(UNITY_COLORSPACE_GAMMA)
                color = SideViewAceDecode(color);
#endif
                return float4(color, extra.a * _AlphaScale);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
