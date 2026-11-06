/* 功能：真实DX11线性浮点GPU合同，先观察URP基线缺少独立响应控制的RED，再检查共享Voxel HLSL。
 * 边界：Root独占Native执行；不写场景/资产/全局灯，所有临时Shader/材质/RT精确销毁。
 * 红阶段无生产include，实际调用本机URP；不把编译失败或洋红色当作RED。
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Funplay.Editor.Tools.Scripting;

public sealed class VoxelGpuContract : IFunplayCommand
{
    const string IncludePath = "Assets/OSSASTRA/Scripts/WorldRendering/VoxelSurface/Rendering/VoxelSurfaceLighting.hlsl";
    readonly List<string> failures = new List<string>();
    int assertions;
    Material material;
    RenderTexture output;
    Texture2D read;
    CommandBuffer command;

    public void Execute(ExecutionContext context)
    {
        if (Application.dataPath != "E:/WORLDCREATOR/new/Project_ANKALUTE/Assets") throw new InvalidOperationException("wrong project");
        bool production = File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath), IncludePath));
        bool oldAsync = ShaderUtil.allowAsyncCompilation;
        RenderTexture previous = RenderTexture.active;
        Shader shader = null;
        try
        {
            ShaderUtil.allowAsyncCompilation = false;
            var factory = typeof(ShaderUtil).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "CreateShaderAsset" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
            if (factory == null) throw new InvalidOperationException("transient shader factory not available");
            string source = ShaderSource.Replace("// VOXEL_INCLUDE", production ? "#define CONTRACT_VOXEL 1\n#include \"" + IncludePath + "\"" : "");
            shader = (Shader)factory.Invoke(null, new object[] { source });
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            output = new RenderTexture(2, 2, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear) { hideFlags = HideFlags.HideAndDontSave };
            read = new Texture2D(2, 2, TextureFormat.RGBAFloat, false, true) { hideFlags = HideFlags.HideAndDontSave };
            command = new CommandBuffer { name = "VOXEL-LIGHT-01 isolated linear contribution contract" };
            output.Create();
            Reset();
            Check(Render(9, 0), new Color(.25f, .5f, .75f, 1), "harness executes real fragment", .0001f);
            string[] compileErrors = ShaderUtil.GetShaderMessages(shader).Where(m => m.severity.ToString() == "Error").Select(m => m.message).ToArray();
            if (compileErrors.Length > 0 || failures.Count > 0) throw new InvalidOperationException("GPU harness invalid: " + string.Join(" | ", compileErrors.Concat(failures)));

            // Catches changed neutral BRDF/coat energy, double AO, spec-off and metallic handling.
            foreach (float metallic in new[] { 0f, 1f }) foreach (float smoothness in new[] { .12f, .72f }) foreach (float coat in new[] { 0f, .65f })
            {
                Reset(); material.SetFloat("_TestMetallic", metallic); material.SetFloat("_TestSmoothness", smoothness); material.SetFloat("_TestCoat", coat);
                Check(Render(1, 0), Render(4, 0), "neutral direct metal=" + metallic + " smooth=" + smoothness + " coat=" + coat);
                Check(Render(2, 0), Render(5, 0), "neutral GI metal=" + metallic + " smooth=" + smoothness + " coat=" + coat);
                material.SetFloat("_TestSpecOff", 1);
                Check(Render(1, 0), Render(4, 0), "spec-off direct metal=" + metallic + " coat=" + coat);
            }

            // Expected multiples come from the measured native baseline, not the implementation under test.
            Reset(); Color diffuse = Render(1, 1), specular = Render(1, 2);
            material.SetFloat("_VoxelDirectSpecular", .2f);
            Check(Render(1, 1), diffuse, "direct spec control leaves diffuse");
            Check(Render(1, 2), Scale(specular, .2f), "direct spec control scales only specular");
            material.SetFloat("_VoxelDirectSpecular", 0);
            Check(Render(1, 2), Color.black, "direct spec zero removes specular");
            Reset(); Color giDiffuse = Render(2, 1), giSpecular = Render(2, 2);
            material.SetFloat("_VoxelIndirectDiffuse", .25f);
            Check(Render(2, 1), Scale(giDiffuse, .25f), "GI diffuse response affects diffuse");
            Check(Render(2, 2), giSpecular, "GI diffuse response preserves reflection");
            material.SetFloat("_VoxelIndirectDiffuse", 0);
            Check(Render(2, 1), Color.black, "GI diffuse zero removes only diffuse");
            material.SetFloat("_VoxelEnvironmentSpecular", .3f);
            Check(Render(2, 2), Scale(giSpecular, .3f), "environment spec response independent");
            material.SetFloat("_VoxelEnvironmentSpecular", 0);
            Check(Render(2, 0), Color.black, "GI response zeros remove both");
            Reset(); Color aoOne = Render(2, 0); material.SetFloat("_TestAO", .5f);
            Check(Render(2, 0), Scale(aoOne, .5f), "AO applied exactly once");

            // Art curve is present but defaults to zero in author profiles; specular uses native NdotL.
            Reset(); material.SetFloat("_TestNdotL", .25f);
            Check(Render(0, 0), new Color(.25f, .25f, .25f, 1), "zero shape identity");
            Color nativeSpec = Render(1, 2); material.SetFloat("_VoxelMainShape", 1);
            Check(Render(0, 0), new Color(.15625f, .15625f, .15625f, 1), "quarter angle smooth directional response");
            Check(Render(1, 2), nativeSpec, "directional shape preserves specular");
            float previousValue = -1;
            for (int i = 0; i <= 32; i++)
            {
                material.SetFloat("_TestNdotL", i / 32f); float value = Render(0, 0).r;
                Assert(!float.IsNaN(value) && !float.IsInfinity(value) && value >= previousValue - .00001f && value >= -.00001f && value <= 1.00001f, "curve monotone bounded at " + i);
                previousValue = value;
            }
            foreach (string factor in new[] { "_TestNdotL", "_TestShadow", "_TestDistance" })
            { Reset(); material.SetFloat("_VoxelMainShape", 1); material.SetFloat(factor, 0); Check(Render(1, 0), Color.black, "zero direct factor " + factor); }
            Reset(); Color shadowOne = Render(1, 0); material.SetFloat("_TestShadow", .37f);
            Check(Render(1, 0), Scale(shadowOne, .37f), "shadow remains continuous multiplier");
            Reset(); Color distanceOne = Render(1, 0); material.SetFloat("_TestDistance", .19f);
            Check(Render(1, 0), Scale(distanceOne, .19f), "distance remains continuous multiplier");
            Reset(); Color fixedDiffuse = Render(1, 1); material.SetVector("_TestView", new Vector4(.6f, .2f, .77f, 0));
            Check(Render(1, 1), fixedDiffuse, "diffuse independent of view direction");

            // Catches RGB channel clamps, unintended low-light compression and missing shoulder.
            Reset(); material.SetVector("_TestGain", new Vector4(12, 6, 3, 0));
            Check(Render(3, 0), new Color(12, 6, 3, 1), "zero protection exact identity");
            material.SetFloat("_VoxelHighlightStrength", 1); Color protectedGain = Render(3, 0);
            Assert(protectedGain.r < 12 && protectedGain.r > 4, "strong gain receives soft shoulder");
            Assert(Mathf.Abs(protectedGain.r / protectedGain.g - 2) < .001f && Mathf.Abs(protectedGain.g / protectedGain.b - 2) < .001f, "shoulder preserves RGB ratio");
            material.SetVector("_TestGain", new Vector4(2, 1, .5f, 0));
            Check(Render(3, 0), new Color(2, 1, .5f, 1), "gain below knee unchanged");
            context.ReturnValue = new { pass = failures.Count == 0, stage = failures.Count == 0 ? "GREEN" : "RED", productionIncluded = production,
                assertions, failures = failures.ToArray(), shaderSupported = shader.isSupported, graphics = SystemInfo.graphicsDeviceType.ToString(),
                shaderMessages = ShaderUtil.GetShaderMessages(shader).Select(m => m.severity + ": " + m.message).ToArray(),
                scope = "pure shared lighting GPU math; not full scene mixed/layers/cluster integration" };
        }
        finally
        {
            RenderTexture.active = previous;
            if (command != null) command.Release();
            if (output != null) { output.Release(); UnityEngine.Object.DestroyImmediate(output); }
            if (read != null) UnityEngine.Object.DestroyImmediate(read);
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
            if (shader != null) UnityEngine.Object.DestroyImmediate(shader);
            ShaderUtil.allowAsyncCompilation = oldAsync;
        }
    }

    void Reset()
    {
        material.SetFloat("_VoxelMainShape", 0); material.SetFloat("_VoxelIndirectDiffuse", 1); material.SetFloat("_VoxelEnvironmentSpecular", 1);
        material.SetFloat("_VoxelDirectSpecular", 1); material.SetFloat("_VoxelHighlightStrength", 0); material.SetFloat("_VoxelHighlightKnee", 4); material.SetFloat("_VoxelHighlightCeiling", 8);
        material.SetFloat("_VoxelDiagnostic", 0); material.SetFloat("_VoxelVertexColors", 0);
        material.SetFloat("_TestNdotL", .6f); material.SetFloat("_TestShadow", 1); material.SetFloat("_TestDistance", 1); material.SetFloat("_TestAO", 1);
        material.SetFloat("_TestMetallic", 0); material.SetFloat("_TestSmoothness", .3f); material.SetFloat("_TestCoat", 0); material.SetFloat("_TestSpecOff", 0);
        material.SetVector("_TestView", new Vector4(0, 0, 1, 0)); material.SetVector("_TestGain", new Vector4(1, 1, 1, 0));
    }
    Color Render(int operation, int part)
    {
        material.SetFloat("_TestOperation", operation); material.SetFloat("_TestPart", part);
        command.Clear(); command.SetRenderTarget(output); command.SetViewport(new Rect(0, 0, 2, 2)); command.ClearRenderTarget(false, true, Color.magenta);
        command.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
        Graphics.ExecuteCommandBuffer(command);
        RenderTexture.active = output; read.ReadPixels(new Rect(0, 0, 2, 2), 0, 0, false); read.Apply(false, false);
        return read.GetPixel(0, 0);
    }
    static Color Scale(Color c, float scale) { return new Color(c.r * scale, c.g * scale, c.b * scale, 1); }
    void Assert(bool valid, string label) { assertions++; if (!valid) failures.Add(label); }
    void Check(Color actual, Color expected, string label, float epsilon = .001f)
    {
        float error = Mathf.Max(Mathf.Abs(actual.r - expected.r), Mathf.Abs(actual.g - expected.g), Mathf.Abs(actual.b - expected.b));
        Assert(!float.IsNaN(error) && !float.IsInfinity(error) && error <= epsilon * Mathf.Max(1, Mathf.Abs(expected.r), Mathf.Abs(expected.g), Mathf.Abs(expected.b)), label + " actual=" + actual + " expected=" + expected);
    }

    const string ShaderSource = @"Shader ""Hidden/VOXEL-LIGHT-01/MathContract"" { SubShader { Pass {
        Cull Off ZWrite Off ZTest Always
        HLSLPROGRAM
        #pragma target 4.5
        #pragma vertex Vert
        #pragma fragment Frag
        #define _CLEARCOAT 1
        #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
        #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl""
        float _VoxelMainShape, _VoxelIndirectDiffuse, _VoxelEnvironmentSpecular, _VoxelDirectSpecular;
        float _VoxelHighlightStrength, _VoxelHighlightKnee, _VoxelHighlightCeiling, _VoxelDiagnostic, _VoxelVertexColors;
        #define ENVIRONMENT_NATIVE_LIT 1
        #include ""Assets/OSSASTRA/Scripts/EnvironmentSimulation/Rendering/EnvironmentLighting.hlsl""
        // VOXEL_INCLUDE
        float _TestOperation, _TestPart, _TestNdotL, _TestShadow, _TestDistance, _TestAO, _TestMetallic, _TestSmoothness, _TestCoat, _TestSpecOff;
        float4 _TestView, _TestGain;
        float4 Vert(uint id:SV_VertexID):SV_POSITION { float2 uv=float2((id<<1)&2,id&2);return float4(uv*2-1,0,1); }
        float3 NativeGI(BRDFData brdf, BRDFData coat, float3 baked, float3 reflection, float3 coatReflection)
        {
            float3 result=EnvironmentBRDF(brdf,baked,reflection,.16);
            float coatFresnel=kDielectricSpec.x+kDielectricSpec.a*.16;
            return (result*(1-coatFresnel*_TestCoat)+EnvironmentBRDFClearCoat(coat,_TestCoat,coatReflection,.16))*_TestAO;
        }
        float4 Frag():SV_Target
        {
            if (_TestOperation>8.5) return float4(.25,.5,.75,1);
            SurfaceData surface=(SurfaceData)0; surface.albedo=float3(.4,.2,.1);surface.metallic=_TestMetallic;surface.specular=.04;
            surface.smoothness=_TestSmoothness;surface.alpha=1;surface.clearCoatMask=_TestCoat;surface.clearCoatSmoothness=.5;
            BRDFData brdf; InitializeBRDFData(surface,brdf);BRDFData coat=CreateClearCoatBRDFData(surface,brdf);
            float3 N=float3(0,0,1),V=normalize(_TestView.xyz);
            Light light=(Light)0;light.direction=float3(sqrt(max(1-_TestNdotL*_TestNdotL,0)),0,_TestNdotL);light.color=float3(1,.8,.6);
            light.distanceAttenuation=_TestDistance;light.shadowAttenuation=_TestShadow;
            float3 baked=float3(.7,.5,.3),reflection=float3(.9,.8,.7),coatReflection=float3(.8,.7,.6);
            float3 result=0;
            if (_TestOperation<.5)
            {
                #if defined(CONTRACT_VOXEL)
                result=VoxelShapeNdotL(_TestNdotL,_VoxelMainShape).xxx;
                #else
                result=_TestNdotL.xxx;
                #endif
            }
            else if (_TestOperation<1.5)
            {
                #if defined(CONTRACT_VOXEL)
                VoxelLightingContributions terms=VoxelEvaluateDirectLighting(brdf,coat,light,N,V,_TestCoat,_TestSpecOff>.5,_VoxelMainShape);
                result=_TestPart<.5?terms.diffuse+terms.specular:(_TestPart<1.5?terms.diffuse:terms.specular);
                #else
                float3 total=LightingPhysicallyBased(brdf,coat,light,N,V,_TestCoat,_TestSpecOff>.5);
                float3 diffuse=LightingPhysicallyBased(brdf,coat,light,N,V,_TestCoat,true);
                float coatF=kDielectricSpec.x+kDielectricSpec.a*Pow4(1-saturate(dot(N,V)));
                if (_TestSpecOff<.5) diffuse*=1-_TestCoat*coatF;
                result=_TestPart<.5?total:(_TestPart<1.5?diffuse:total-diffuse);
                #endif
            }
            else if (_TestOperation<2.5)
            {
                #if defined(CONTRACT_VOXEL)
                VoxelLightingContributions terms=VoxelEvaluateIndirectContributions(brdf,coat,_TestCoat,baked,reflection,coatReflection,.16,_TestAO);
                result=_TestPart<.5?terms.diffuse+terms.specular:(_TestPart<1.5?terms.diffuse:terms.specular);
                #else
                float3 diffuse=NativeGI(brdf,coat,baked,0,0),specular=NativeGI(brdf,coat,0,reflection,coatReflection);
                result=_TestPart<.5?diffuse+specular:(_TestPart<1.5?diffuse:specular);
                #endif
            }
            else if (_TestOperation<3.5)
            {
                #if defined(CONTRACT_VOXEL)
                result=VoxelProtectLight(_TestGain.rgb);
                #else
                result=_TestGain.rgb;
                #endif
            }
            else if (_TestOperation<4.5) result=LightingPhysicallyBased(brdf,coat,light,N,V,_TestCoat,_TestSpecOff>.5);
            else result=NativeGI(brdf,coat,baked,reflection,coatReflection);
            return float4(result,1);
        }
        ENDHLSL
    } } }";
}
