/* 功能：独立GPU深度附件写入已知值，再经真实新深度解析Shader验证MSAA1/2/4/8。
 * 边界：不改变生产相机/管线；不把原世界纹理作为期望，临时Shader/RT/材质全部销毁。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Funplay.Editor.Tools.Scripting;

public sealed class RenderingDepthContract : IFunplayCommand
{
    public void Execute(ExecutionContext context)
    {
        if (Application.dataPath != "E:/WORLDCREATOR/new/Project_ANKALUTE/Assets") throw new InvalidOperationException("wrong project");
        Shader copyShader = Shader.Find("Hidden/Ossastra/WorldRendering/LegalDepthCopy");
        if (copyShader == null || !copyShader.isSupported) throw new InvalidOperationException("missing supported legal depth shader");
        string stampSource = @"Shader ""Hidden/Isolated/LegalDepthStamp"" { SubShader { Pass {
            Cull Off ZWrite On ZTest Always ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            float _KnownRawDepth;
            float4 Vert(uint id:SV_VertexID):SV_POSITION { float2 uv=float2((id<<1)&2,id&2);return float4(uv*2-1,0,1); }
            float Frag():SV_Depth {return _KnownRawDepth;}
            ENDHLSL
        } } }";
        var factory = typeof(ShaderUtil).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "CreateShaderAsset" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
        if (factory == null) throw new InvalidOperationException("transient shader factory not available");
        var stampShader = (Shader)factory.Invoke(null, new object[] { stampSource });
        var stamp = new Material(stampShader); var copy = new Material(copyShader);
        var oldDepth = Shader.GetGlobalTexture("_WorldLookDepthSource");
        Vector4 oldSize = Shader.GetGlobalVector("_WorldLookDepthSourceSize");
        RenderTexture previous = RenderTexture.active;
        var results = new List<object>(); bool pass = true; int tested = 0;
        try
        {
            foreach (int samples in new[] { 1, 2, 4, 8 })
            {
                var descriptor = new RenderTextureDescriptor(16, 16) { graphicsFormat = GraphicsFormat.None,
                    depthStencilFormat = GraphicsFormat.D24_UNorm_S8_UInt, msaaSamples = samples, bindMS = samples > 1,
                    useMipMap = false, autoGenerateMips = false };
                if (SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor) < samples)
                { results.Add(new { samples, supported = false }); continue; }
                var source = new RenderTexture(descriptor);
                var output = new RenderTexture(16, 16, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
                var read = new Texture2D(16, 16, TextureFormat.RFloat, false, true);
                var command = new CommandBuffer { name = "Isolated fresh legal depth resolve contract" };
                try
                {
                    source.Create(); output.Create();
                    foreach (float expected in new[] { .375f, .625f })
                    {
                        command.Clear(); stamp.SetFloat("_KnownRawDepth", expected);
                        command.SetRenderTarget(source); command.SetViewport(new Rect(0, 0, 16, 16));
                        command.ClearRenderTarget(true, false, Color.clear, 1);
                        command.DrawProcedural(Matrix4x4.identity, stamp, 0, MeshTopology.Triangles, 3);
                        Graphics.ExecuteCommandBuffer(command); command.Clear();
                        CoreUtils.SetKeyword(copy, "_WORLD_LOOK_DEPTH_MSAA_2", samples == 2);
                        CoreUtils.SetKeyword(copy, "_WORLD_LOOK_DEPTH_MSAA_4", samples == 4);
                        CoreUtils.SetKeyword(copy, "_WORLD_LOOK_DEPTH_MSAA_8", samples == 8);
                        copy.SetVector("_BlitScaleBias", new Vector4(1, 1, 0, 0));
                        command.SetGlobalTexture("_WorldLookDepthSource", source);
                        command.SetGlobalVector("_WorldLookDepthSourceSize", new Vector4(16, 16, 1f / 16, 1f / 16));
                        command.SetRenderTarget(output); command.SetViewport(new Rect(0, 0, 16, 16));
                        command.DrawProcedural(Matrix4x4.identity, copy, 0, MeshTopology.Triangles, 3);
                        Graphics.ExecuteCommandBuffer(command);
                        RenderTexture.active = output; read.ReadPixels(new Rect(0, 0, 16, 16), 0, 0); read.Apply();
                        float actual = read.GetPixel(8, 8).r;
                        bool valid = !float.IsNaN(actual) && Mathf.Abs(actual - expected) < .001;
                        pass &= valid; ++tested; results.Add(new { samples, expected, actual, pass = valid });
                    }
                }
                finally
                {
                    command.Release(); source.Release(); output.Release();
                    UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(output); UnityEngine.Object.DestroyImmediate(read);
                }
            }
            context.ReturnValue = new { pass = pass && tested > 0, tested, results = results.ToArray(), reversedZ = SystemInfo.usesReversedZBuffer };
        }
        finally
        {
            RenderTexture.active = previous; Shader.SetGlobalTexture("_WorldLookDepthSource", oldDepth); Shader.SetGlobalVector("_WorldLookDepthSourceSize", oldSize);
            UnityEngine.Object.DestroyImmediate(copy); UnityEngine.Object.DestroyImmediate(stamp); UnityEngine.Object.DestroyImmediate(stampShader);
        }
    }
}
