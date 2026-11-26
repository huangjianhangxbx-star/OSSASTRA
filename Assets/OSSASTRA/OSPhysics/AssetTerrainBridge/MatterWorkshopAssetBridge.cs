/* 功能：用横版地形工坊的真实源图和求解器给物质实验室供图。
 * 负责：读取本机绑定、沿用作者格和接点，返回透明片及黑体填充。
 * 边界：采用内存地图和借用的贴图。正式地图、套件、物理及材质由各自系统管理。 */
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.SideView.TerrainWorkshop;
using UnityEngine;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

namespace Ossastra.OSPhysics
{
    public sealed class MatterWorkshopAssetBridge : IMatterAssetTerrainKit
    {
        private const string OriginalArtFolder = "OSPhysicsR1/";
        private const string OriginalKitId = "osphysics-r1-original-copper";
        private const string LocalManifest = "Reports/OSPHYSICS-02/R1/资产绑定.json";
        private readonly List<ScriptableObject> owned = new List<ScriptableObject>();
        private readonly TerrainMap draft;
        private readonly TerrainKit kit;
        private readonly string sourceStatus;
        private int issueCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => MatterAssetTerrain.Load = Open;

        private MatterWorkshopAssetBridge(TerrainKit kit, TerrainMap source, string status,
            MatterAssetStamp[] grass, MatterAssetStamp[] wood)
        {
            this.kit = kit;
            draft = ScriptableObject.CreateInstance<TerrainMap>();
            draft.name = "OSPhysics 资产求解";
            draft.hideFlags = HideFlags.HideAndDontSave;
            if (source == null)
                TerrainScale.ApplyNewMap(draft);
            else
            {
                draft.cellSize = TerrainScale.Cell(source);
                draft.referencePixelsPerCell = TerrainScale.PixelsPerCell(source);
                // 源图和规则留在原作者资产。临时地图只携带与地形笔刷有关的图层。
                draft.layers = source.layers
                    .Where(layer => layer != null && IsInteractive(layer.kind))
                    .Select(layer => layer.Copy()).ToArray();
                foreach (var layer in draft.layers)
                {
                    layer.visible = true;
                    layer.solo = false;
                }
            }
            draft.kits = new[] { kit };
            sourceStatus = status;
            Grass = grass;
            Wood = wood;
            owned.Add(draft);
        }

        public string Name => kit.name;
        public string Status => issueCount == 0 ? sourceStatus : $"{sourceStatus} · 接边提示 {issueCount}";
        public float CellSize => TerrainScale.Cell(draft);
        public IReadOnlyList<MatterAssetStamp> Grass { get; }
        public IReadOnlyList<MatterAssetStamp> Wood { get; }

        public static IMatterAssetTerrainKit Open()
        {
#if UNITY_EDITOR
            var source = TryOpenLocal(out string status);
            if (source != null) return source;
            return OpenOriginal(status);
#else
            return OpenOriginal("本机参考图件仅供编辑器内使用");
#endif
        }

        /// <summary>直接消费工坊求解的四顶点、裁图、变换和渐退。实体形状由实验室按同一批作者格转换。</summary>
        public void Solve(IReadOnlyCollection<Vector2Int> blocks, List<MatterAssetQuad> output)
        {
            output.Clear();
            var grid = new TerrainGrid(blocks.Select(point =>
                new TerrainTile(new TerrainPoint(point.x, point.y), kit.Id)));
            var solved = TerrainAssembly.Solve(draft, grid);
            issueCount = solved.Issues.Count;
            foreach (var part in solved.Parts)
            {
                if (!IsInteractive(part.Layer)) continue;
                output.Add(new MatterAssetQuad(part.Art?.Image, part.A, part.B, part.C, part.D,
                    part.UV, part.Tint, part.SortOrder(draft), part.FadeVertices,
                    part.Art != null ? part.Art.name : kit.name, part.Slot.ToString(), part.FlipX, part.FlipY));
            }
        }

        public void Dispose()
        {
            // 只释放内存地图和临时图件描述，Resources 和 AssetDatabase 的源图由 Unity 持有。
            for (int index = owned.Count - 1; index >= 0; index--)
            {
                var item = owned[index];
                if (item == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(item);
                else UnityEngine.Object.DestroyImmediate(item);
            }
            owned.Clear();
        }

        private static bool IsInteractive(TerrainLayerKind kind) =>
            kind == TerrainLayerKind.Body || kind == TerrainLayerKind.Border || kind == TerrainLayerKind.Decor;

#if UNITY_EDITOR
        [Serializable]
        private sealed class LocalFiles
        {
            public string terrainMap = "", calibrationMap = "", kitId = "";
            public string[] grass = Array.Empty<string>(), wood = Array.Empty<string>();
        }

        private static IMatterAssetTerrainKit TryOpenLocal(out string status)
        {
            status = "本机绑定文件缺失";
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), LocalManifest);
            if (!File.Exists(path)) return null;

            LocalFiles files;
            try { files = JsonUtility.FromJson<LocalFiles>(File.ReadAllText(path)); }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException)
            {
                status = "本机绑定文件无法读取";
                return null;
            }

            var source = LoadAsset<TerrainMap>(files?.terrainMap);
            var calibration = LoadAsset<TerrainMap>(files?.calibrationMap);
            var kit = source == null ? null : source.Kit(files.kitId);
            if (kit == null)
            {
                status = "本机绑定的地图或套件缺失";
                return null;
            }

            var missing = new List<string>();
            foreach (var slot in new[] { TerrainSlot.Top, TerrainSlot.Left, TerrainSlot.Right, TerrainSlot.Ceiling })
                if (kit.Rule(slot)?.art?.Image == null) missing.Add(slot.ToString());

            var grass = ReadStamps(files.grass, true, source, calibration, missing, "草");
            var wood = ReadStamps(files.wood, false, source, calibration, missing, "木");
            status = missing.Count == 0 ? "当前地图图件 · 本机" :
                $"当前地图图件 · 缺 {string.Join("、", missing)}";
            return new MatterWorkshopAssetBridge(kit, source, status, grass, wood);
        }

        private static MatterAssetStamp[] ReadStamps(string[] paths, bool attach, TerrainMap source,
            TerrainMap calibration, List<string> missing, string category)
        {
            if (paths == null || paths.Length == 0)
            {
                missing.Add(category);
                return Array.Empty<MatterAssetStamp>();
            }

            var stamps = new List<MatterAssetStamp>(paths.Length);
            foreach (string path in paths)
            {
                var art = LoadAsset<TerrainArt>(path);
                if (art == null || art.Image == null || art.Pixels.width <= 0 || art.Pixels.height <= 0)
                {
                    missing.Add(string.IsNullOrEmpty(path) ? category : Path.GetFileNameWithoutExtension(path));
                    continue;
                }

                var definition = calibration?.decorGroups?.Where(group => group != null)
                    .SelectMany(group => group.ArtCandidates).FirstOrDefault(candidate =>
                        candidate != null && candidate.art == art && candidate.size.x > 0 && candidate.size.y > 0);
                if (definition == null)
                {
                    missing.Add(art.name + "标定");
                    continue;
                }

                // 工坊原标定中的支撑宽度对应图件内部的可燃核心。显示仍使用完整裁片和真实 Alpha。
                float fraction = Mathf.Clamp(definition.support / definition.size.x, .22f, .78f);
                float left = Mathf.Clamp(definition.pivot.x - fraction * .5f, 0, 1 - fraction);
                float bottom = attach ? Mathf.Clamp01(definition.pivot.y) : .12f;
                float height = Mathf.Min(attach ? .5f : .75f, 1 - bottom);
                var fuel = attach ? new Rect(left, bottom, fraction, height) : new Rect(.045f, .055f, .91f, .88f);
                var coverage = ReadCoverage(art.Image, art.UV);
                if (coverage == null)
                {
                    missing.Add(art.name + "轮廓");
                    continue;
                }
                stamps.Add(new MatterAssetStamp(art.name, art.Image, art.UV, art.Pixels,
                    definition.size * TerrainScale.Cell(source), definition.pivot, definition.mirror, attach, fuel, coverage));
            }
            return stamps.ToArray();
        }

        private static T LoadAsset<T>(string path) where T : UnityEngine.Object =>
            string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", StringComparison.Ordinal)
                ? null : AssetDatabase.LoadAssetAtPath<T>(path);
#endif

        private static byte[] ReadCoverage(Texture2D image, Rect uv)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return null;
            int size = MatterAssetStamp.FuelCoverageSize;
            var previous = RenderTexture.active;
            var temp = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
            var readable = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            try
            {
                // 只在启用套件时读取小型 Alpha 掩码；不更改源贴图的可读取选项。
                Graphics.Blit(image, temp, uv.size, uv.position);
                RenderTexture.active = temp;
                readable.ReadPixels(new Rect(0, 0, size, size), 0, 0, false);
                var colors = readable.GetPixels32();
                var coverage = new byte[colors.Length];
                for (int index = 0; index < colors.Length; index++) coverage[index] = colors[index].a;
                return coverage;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temp);
                if (Application.isPlaying) UnityEngine.Object.Destroy(readable);
                else UnityEngine.Object.DestroyImmediate(readable);
            }
        }

        private static IMatterAssetTerrainKit OpenOriginal(string status)
        {
            // 公开检出沿用工坊自有铜脉图与原规则的有效跨度、接点及朝向。根部绑定留给本机套件。
            var fallback = ScriptableObject.CreateInstance<TerrainKit>();
            fallback.hideFlags = HideFlags.HideAndDontSave;
            fallback.name = "铜脉遗迹 · 原创对照";
            JsonUtility.FromJsonOverwrite("{\"stableId\":\"" + OriginalKitId + "\"}", fallback);
            fallback.fill = new Color(.075f, .066f, .091f, 1);
            var temporary = new List<ScriptableObject> { fallback };
            TerrainRule Rule(TerrainSlot slot, string image, float span, float anchor = .64f,
                bool rotate = false, float square = 0)
            {
                var texture = Resources.Load<Texture2D>(OriginalArtFolder + image);
                if (texture == null) return null;
                var art = ScriptableObject.CreateInstance<TerrainArt>();
                art.hideFlags = HideFlags.HideAndDontSave;
                art.name = texture.name;
                art.texture = texture;
                temporary.Add(art);
                return new TerrainRule { slot = slot, art = art, anchor = anchor, span = span,
                    overlap = .075f, rotate = rotate, size = Vector2.one * square,
                    placement = slot == TerrainSlot.Convex || slot == TerrainSlot.Concave || slot == TerrainSlot.Cap
                        ? TerrainPlacement.Whole : TerrainPlacement.Repeat };
            }

            fallback.rules = new[]
            {
                Rule(TerrainSlot.Top, "CopperTop", 2, .800716f),
                Rule(TerrainSlot.Left, "CopperLeft", 1, rotate: true),
                Rule(TerrainSlot.Right, "CopperRight", 1, rotate: true),
                Rule(TerrainSlot.Ceiling, "CopperCeiling", 2),
                Rule(TerrainSlot.Convex, "CopperOuter", 1, square: .65f),
                Rule(TerrainSlot.Concave, "CopperInner", 1, square: .65f),
                Rule(TerrainSlot.Cap, "CopperEnd", 1)
            }.Where(rule => rule != null).ToArray();

            var grass = new List<MatterAssetStamp>();
            foreach (var sample in new[] { ("OriginalGrass", "原创草丛", .65f, .0625f),
                       ("OriginalFlower", "原创花丛", .58f, .0625f) })
            {
                var texture = Resources.Load<Texture2D>(OriginalArtFolder + sample.Item1);
                if (texture == null) continue;
                var size = new Vector2(sample.Item3 * texture.width / texture.height, sample.Item3);
                grass.Add(new MatterAssetStamp(sample.Item2, texture, new Rect(0, 0, 1, 1),
                    new Rect(0, 0, texture.width, texture.height), size, new Vector2(.5f, sample.Item4), true, true,
                    new Rect(.26f, .12f, .48f, .52f), ReadCoverage(texture, new Rect(0, 0, 1, 1))));
            }
            var bridge = new MatterWorkshopAssetBridge(fallback, null,
                $"{status} · 原创对照 · 缺木图件", grass.ToArray(), Array.Empty<MatterAssetStamp>());
            bridge.owned.AddRange(temporary);
            return bridge;
        }
    }
}
