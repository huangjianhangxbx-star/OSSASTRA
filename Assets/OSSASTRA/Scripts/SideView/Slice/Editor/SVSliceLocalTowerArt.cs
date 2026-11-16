/*
 * Imports a small explicit local set from EM-MAP-EX01. The project publishes
 * authoring code separately from the source-derived texture and mesh assets.
 */
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Ossastra.SideView.Slice
{
    public static partial class SVSliceSceneAuthoring
    {
        private const string LocalArtOutput = SliceFolder + "/LocalArt/";
        private const string DefaultLocalArtInput = "E:/WORLDCREATOR/XingHaiHuiLang/work/SV-SLICE-01/maps/LocalArt";

        private sealed class LocalTowerArt
        {
            public Texture2D Ground, Masonry, Iron, Switch, Rest, Elevator;
            public Mesh RestMesh, ElevatorMesh;
        }

        [Serializable]
        private sealed class SourceMesh
        {
            public float[] positions;
            public float[] uv;
            public int[] triangles;
        }

        private static LocalTowerArt LoadLocalTowerArt()
        {
            var input = Environment.GetEnvironmentVariable("OSSASTRA_SVSLICE_TOWER_ART");
            if (string.IsNullOrEmpty(input)) input = DefaultLocalArtInput;
            if (!File.Exists(Path.Combine(input, "source-index.json")))
            {
                Debug.LogWarning("[SV-SLICE-01] 未安装锁之塔原作选件，使用自有的地景、铁笼与椅子几何。");
                return null;
            }
            EnsureFolder(SliceFolder, "LocalArt");
            return new LocalTowerArt
            {
                Ground = LoadTexture(input, "ground.png", true),
                Masonry = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/XingHaiHuiLang/Art/PBRDungeonCartoon_v1/Textures/Painted_Wall.png"),
                Iron = LoadTexture(input, "iron.png", true),
                Switch = LoadTexture(input, "switch.png", false),
                Rest = LoadPropTexture(input, "rest"),
                RestMesh = LoadSourceMesh(input, "rest"),
                Elevator = LoadPropTexture(input, "elevator"),
                ElevatorMesh = LoadSourceMesh(input, "elevator"),
            };
        }

        private static Texture2D LoadTexture(string input, string name, bool repeat)
        {
            var path = LocalArtOutput + name;
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            File.Copy(Path.Combine(input, name), path, true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                throw new InvalidOperationException("不能导入本机素材：" + path);
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D LoadPropTexture(string input, string name)
        {
            var path = LocalArtOutput + name + "_alpha.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            var color = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var mask = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!color.LoadImage(File.ReadAllBytes(Path.Combine(input, name + ".png"))) ||
                    !mask.LoadImage(File.ReadAllBytes(Path.Combine(input, name + "-mask.png"))) ||
                    color.width != mask.width || color.height != mask.height)
                    throw new InvalidOperationException("素材与透明遮罩尺寸不匹配：" + name);
                var pixels = color.GetPixels32();
                var opacity = mask.GetPixels32();
                if (Array.TrueForAll(pixels, pixel => pixel.a == 255))
                    for (var i = 0; i < pixels.Length; i++) pixels[i].a = opacity[i].a < 255 ? opacity[i].a : opacity[i].r;
                color.SetPixels32(pixels);
                color.wrapMode = TextureWrapMode.Clamp;
                color.Apply();
                AssetDatabase.CreateAsset(color, path);
                return color;
            }
            finally
            {
                Object.DestroyImmediate(mask);
                if (!EditorUtility.IsPersistent(color)) Object.DestroyImmediate(color);
            }
        }

        private static Mesh LoadSourceMesh(string input, string name)
        {
            var path = LocalArtOutput + name + "_source_mesh.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                if (existing.colors32.Length != existing.vertexCount)
                {
                    existing.colors32 = WhiteVertices(existing.vertexCount);
                    EditorUtility.SetDirty(existing);
                }
                return existing;
            }
            var source = JsonUtility.FromJson<SourceMesh>(File.ReadAllText(Path.Combine(input, name + ".mesh.json")));
            var vertices = new Vector3[source.positions.Length / 3];
            if (source.uv.Length != vertices.Length * 2)
                throw new InvalidOperationException("源网格 UV 数量不匹配：" + name);
            for (var i = 0; i < vertices.Length; i++)
                vertices[i] = new Vector3(source.positions[i * 3], source.positions[i * 3 + 1], -source.positions[i * 3 + 2]);
            var uv = new Vector2[vertices.Length];
            for (var i = 0; i < uv.Length; i++) uv[i] = new Vector2(source.uv[i * 2], 1f - source.uv[i * 2 + 1]);
            var mesh = new Mesh { name = name, indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = vertices;
            mesh.colors32 = WhiteVertices(vertices.Length);
            mesh.uv = uv;
            mesh.triangles = source.triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}