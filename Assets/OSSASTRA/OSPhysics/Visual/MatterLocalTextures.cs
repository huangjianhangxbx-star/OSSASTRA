// 职责：在编辑器中读取本机白名单映射，用临时 Texture2D 替换选定表现槽，退出释放。
using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using System.IO;
#endif
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ossastra.OSPhysics
{
    internal sealed class MatterLocalTextures : IDisposable
    {
        [Serializable]
        private sealed class Paths
        {
            public string background, structure, foreground, stone, water, waterEdge, oil, plant, wood, flame, smoke, sand;
        }

        [Serializable]
        private sealed class Manifest { public Paths ruins, moss; }

        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        public int Count => textures.Count;
        public int Missing { get; private set; }
        public string Status { get; private set; } = "原创配置";

        public Texture2D Get(string key, Texture2D original) =>
            textures.TryGetValue(key, out var texture) && texture != null ? texture : original;

        public void Load(MatterPaintStyle style, bool useLocal)
        {
            Dispose();
            Missing = 0;
            Status = "原创配置";
            if (!useLocal) return;
#if UNITY_EDITOR
            // 映射以 JSON 存在 Reports，跟踪场景和配置永远不引用这些外部图片。
            try
            {
                var project = Directory.GetParent(Application.dataPath).FullName;
                var filename = Path.Combine(project, "Reports", "OSPHYSICS-02", "本机素材映射.json");
                if (!File.Exists(filename))
                {
                    Status = "找不到本机映射，使用原创配置";
                    return;
                }

                var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(filename));
                var slots = style == MatterPaintStyle.Moss ? manifest?.moss : manifest?.ruins;
                if (slots == null)
                {
                    Status = "此风格没有本机映射";
                    return;
                }

                Read("background", slots.background, false);
                Read("structure", slots.structure, false);
                Read("foreground", slots.foreground, false);
                Read("stone", slots.stone, true);
                Read("water", slots.water, true);
                Read("waterEdge", slots.waterEdge, false);
                Read("oil", slots.oil, true);
                Read("plant", slots.plant, true);
                Read("wood", slots.wood, true);
                Read("flame", slots.flame, false);
                Read("smoke", slots.smoke, true);
                Read("sand", slots.sand, true);
                Status = Count > 0 ? $"本机映射 {Count} 槽 · 缺失 {Missing}" : "本机映射无有效图片";
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException)
            {
                Status = "本机映射读取失败，使用原创配置";
                Dispose();
            }
#else
            Status = "本机研究映射仅在编辑器可用";
#endif
        }

#if UNITY_EDITOR
        private void Read(string key, string source, bool repeat)
        {
            if (string.IsNullOrWhiteSpace(source)) return;
            Texture2D candidate = null;
            try
            {
                var file = new FileInfo(source);
                if (!file.Exists || file.Length > 12 * 1024 * 1024 ||
                    !string.Equals(file.Extension, ".png", StringComparison.OrdinalIgnoreCase))
                {
                    Missing++;
                    return;
                }

                candidate = new Texture2D(2, 2, TextureFormat.RGBA32, true, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave,
                    name = "OSPhysics local " + key
                };
                if (!ImageConversion.LoadImage(candidate, File.ReadAllBytes(file.FullName), true))
                {
                    Missing++;
                    return;
                }

                textures.Add(key, candidate);
                candidate = null;
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException)
            {
                Missing++;
            }
            finally
            {
                MatterPaintedStage.DestroyOwned(candidate);
            }
        }
#endif

        public void Dispose()
        {
            foreach (Texture2D texture in textures.Values) MatterPaintedStage.DestroyOwned(texture);
            textures.Clear();
        }
    }
}
