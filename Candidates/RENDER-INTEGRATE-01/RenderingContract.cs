/* 功能：用独立线性色块与许可纹理验证真实GPU环境后处理，不触碰现有场景或相机。
 * 边界：测试不授权生产可见权；所有纹理/材质均在finally销毁，源图与原生对象不变。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Funplay.Editor.Tools.Scripting;

public sealed class RenderingContract : IFunplayCommand
{
    public void Execute(ExecutionContext context)
    {
        if (Application.dataPath != "E:/WORLDCREATOR/new/Project_ANKALUTE/Assets") throw new InvalidOperationException("wrong project");
        Shader shader = Shader.Find("Hidden/Ossastra/WorldRendering/EnvironmentLook");
        if (shader == null) { context.ReturnValue = new { pass = false, stage = "RED", reason = "missing environment-only legal world shader" }; return; }
        const int width = 32, height = 8;
        var source = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true) { filterMode = FilterMode.Point };
        var permission = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true) { filterMode = FilterMode.Point };
        var depth = new Texture2D(width, height, TextureFormat.RFloat, false, true) { filterMode = FilterMode.Point };
        var material = new Material(shader);
        var output = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var read = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);
        RenderTexture previous = RenderTexture.active;
        var failures = new List<string>();
        try
        {
            for (int y = 0; y < height; ++y) for (int x = 0; x < width; ++x)
            {
                source.SetPixel(x, y, x < 8 ? Color.black : new Color(.2f, .2f, .2f, 1));
                permission.SetPixel(x, y, x < 8 ? Color.clear : new Color(1, 1, 0, x < 16 ? 0 : x < 24 ? 1 : .25f));
                depth.SetPixel(x, y, new Color(.5f, 0, 0, 0));
            }
            source.Apply(); permission.Apply(); depth.Apply();
            material.SetTexture("_WorldLookPermission", permission); material.SetTexture("_WorldLookDepth", depth);
            material.SetVector("_WorldLookFeatures", new Vector4(1, 0, 0, 0));
            material.SetVector("_WorldLookGrade", new Vector4(1, 0, 0, 0));
            material.SetColor("_WorldLookFilter", Color.white);
            material.SetFloat("_WorldLookVignette", 0);
            material.SetVector("_WorldLookTexelSize", new Vector4(1f / width, 1f / height, width, height));
            material.SetVector("_WorldLookUvTransform", new Vector4(1, 1, 0, 0));
            output.Create(); Render(source, output, material, read);
            Check(read.GetPixel(3, 4), Color.black, "unknown black", failures);
            Check(read.GetPixel(11, 4), new Color(.2f, .2f, .2f, 1), "protected actor", failures);
            Check(read.GetPixel(19, 4), new Color(.4f, .4f, .4f, 1), "legal environment exposure", failures);
            Check(read.GetPixel(27, 4), new Color(.2f, .2f, .2f, 1), "mixed alpha boundary", failures);

            // 被保护高亮像素紧邻合法环境，Bloom不能借它污染邻居；黑幕也不能借邻居变亮。
            for (int y = 0; y < height; ++y) for (int x = 0; x < width; ++x)
            {
                source.SetPixel(x, y, x == 15 ? new Color(30, 0, 0, 1) : x < 8 ? Color.black : new Color(.2f, .2f, .2f, 1));
                permission.SetPixel(x, y, x < 8 ? Color.clear : new Color(1, 1, 0, x == 15 ? 0 : 1));
            }
            source.Apply(); permission.Apply();
            material.SetVector("_WorldLookFeatures", new Vector4(0, 0, 0, 1));
            material.SetVector("_WorldLookBloom", new Vector4(1, 1, 1, 0));
            Render(source, output, material, read);
            Check(read.GetPixel(16, 4), new Color(.2f, .2f, .2f, 1), "bloom rejects actor neighbor", failures);
            Check(read.GetPixel(15, 4), new Color(30, 0, 0, 1), "protected bright actor unchanged", failures);
            Check(read.GetPixel(7, 4), Color.black, "bloom black border unchanged", failures);
            context.ReturnValue = new { pass = failures.Count == 0, stage = failures.Count == 0 ? "GREEN" : "FAIL", assertions = 7, failures = failures.ToArray(), shaderSupported=shader.isSupported,
                shaderMessages=ShaderUtil.GetShaderMessages(shader).Select(m=>m.severity+": "+m.message).ToArray() };
        }
        finally
        {
            RenderTexture.active = previous;
            output.Release(); UnityEngine.Object.DestroyImmediate(output); UnityEngine.Object.DestroyImmediate(read);
            UnityEngine.Object.DestroyImmediate(material); UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(permission); UnityEngine.Object.DestroyImmediate(depth);
        }
    }
    static void Render(Texture source, RenderTexture output, Material material, Texture2D read)
    {
        var block = new MaterialPropertyBlock();
        block.SetTexture("_BlitTexture", source);
        block.SetVector("_BlitScaleBias", new Vector4(1, 1, 0, 0));
        var command = new CommandBuffer { name = "Isolated world look GPU contract" };
        try
        {
            command.SetRenderTarget(output); command.SetViewport(new Rect(0, 0, output.width, output.height));
            command.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1, block);
            Graphics.ExecuteCommandBuffer(command);
        }
        finally { command.Release(); }
        RenderTexture.active = output; read.ReadPixels(new Rect(0, 0, output.width, output.height), 0, 0); read.Apply();
    }
    static void Check(Color actual, Color expected, string label, List<string> failures)
    {
        float error = Mathf.Max(Mathf.Abs(actual.r - expected.r), Mathf.Abs(actual.g - expected.g), Mathf.Abs(actual.b - expected.b));
        if (error > .004f) failures.Add(label + " actual=" + actual + " expected=" + expected);
    }
}
