/*
 * 生成工坊随附的原创图件、两套规则和独立地图数据。
 * 图件以正交轮廓的接点和局部透明为设计约束，可在工坊继续裁切和重配。
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ossastra.SideView.TerrainWorkshop
{
    internal static class TerrainWorkshopExample
    {
        private const string Folder = TerrainWorkshopScenes.ExampleFolder;
        private static readonly TerrainSlot[] Slots = (TerrainSlot[])Enum.GetValues(typeof(TerrainSlot));

        public static TerrainMap Create()
        {
            string mapPath = Folder + "/高台遗址.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TerrainMap>(mapPath);
            if (existing != null) return existing;

            const string copper = "铜脉遗迹";
            const string slate = "月白灰岩";
            var main = CreateKit(copper, new Color(.50f, .40f, .31f), new Color(.075f, .066f, .091f), 21);
            var alternate = CreateKit(slate, new Color(.49f, .64f, .69f), new Color(.056f, .082f, .115f), 107);
            var backdrop = Art("山隙远景", copper, "远景", 800, 224, DrawHorizon);
            var pillars = Art("风蚀断碑", copper, "中景结构", 192, 256, DrawMonument);
            var library = Save<TerrainLibrary>(Folder + "/星骸素材库.asset");
            library.items = main.rules.Concat(alternate.rules).Select(rule => rule.art)
                .Where(art => art != null).Concat(new[] { backdrop, pillars }).ToArray();
            Persist(library);

            var map = Save<TerrainMap>(mapPath);
            map.kits = new[] { main, alternate };
            map.library = library;
            var far = map.Layer(TerrainLayerKind.Far);
            far.art = backdrop;
            far.library = library;
            map.Layer(TerrainLayerKind.Middle).library = library;
            map.Layer(TerrainLayerKind.Foreground).library = library;
            map.stamps.Add(new TerrainStamp { art = pillars, layer = TerrainLayerKind.Middle, position = new Vector2(5, 2.0f), size = new Vector2(8, 11) });
            map.stamps.Add(new TerrainStamp { art = pillars, layer = TerrainLayerKind.Near, position = new Vector2(20, 3.3f), size = new Vector2(6, 8) });
            map.stamps.Add(new TerrainStamp { art = pillars, layer = TerrainLayerKind.Middle, position = new Vector2(38, 3.5f), size = new Vector2(9, 12) });
            map.Edit(grid =>
            {
                Paint(grid, -14, -5, 28, -1, main.Id);
                Paint(grid, 29, -5, 49, -1, alternate.Id);
                Paint(grid, 15, 0, 19, 0, main.Id);
                Paint(grid, 21, 0, 23, 1, main.Id);
                Paint(grid, -6, 3, 0, 3, main.Id);
                Paint(grid, 9, 5, 22, 7, main.Id);
                Paint(grid, 9, 4, 10, 5, main.Id);
                Paint(grid, 19, 4, 22, 5, main.Id);
                Paint(grid, 12, 6, 15, 6, null);
                Paint(grid, 30, 0, 33, 0, alternate.Id);
                Paint(grid, 37, 3, 45, 4, alternate.Id);
                Paint(grid, 44, 0, 48, 0, alternate.Id);
                return true;
            });
            Persist(map);

            var blank = Save<TerrainMap>(Folder + "/空白试拼.asset");
            blank.kits = map.kits;
            blank.library = library;
            Persist(blank);
            return map;
        }

        private static void Paint(TerrainGrid grid, int x1, int y1, int x2, int y2, string kit) =>
            grid.Paint(TerrainGrid.Rectangle(new TerrainPoint(x1, y1), new TerrainPoint(x2, y2)), kit);

        private static TerrainKit CreateKit(string name, Color stone, Color fill, int seed)
        {
            string path = Folder + "/" + name + ".asset";
            var kit = Save<TerrainKit>(path);
            kit.SetAuthoringId(AssetDatabase.AssetPathToGUID(path));
            kit.fill = fill;
            kit.rules = Slots.Select(slot => new TerrainRule
            {
                slot = slot,
                art = slot == TerrainSlot.Fill ? null : CreateEdge(name, slot, stone, seed),
                placement = slot == TerrainSlot.Convex || slot == TerrainSlot.Concave || slot == TerrainSlot.Cap
                    ? TerrainPlacement.Whole : slot == TerrainSlot.Attached ? TerrainPlacement.Scatter : TerrainPlacement.Repeat,
                span = slot == TerrainSlot.Attached ? 3.5f : slot == TerrainSlot.Top || slot == TerrainSlot.Ceiling ? 2 : 1,
                minLength = .25f,
                anchor = .64f,
                rotate = slot == TerrainSlot.Left || slot == TerrainSlot.Right,
                size = slot == TerrainSlot.Convex || slot == TerrainSlot.Concave ? new Vector2(.65f, .65f) : Vector2.zero,
                overlap = .075f
            }).ToArray();
            kit.state = 1;
            Persist(kit);
            return kit;
        }

        private static TerrainArt CreateEdge(string theme, TerrainSlot slot, Color stone, int seed)
        {
            bool joint = slot == TerrainSlot.Convex || slot == TerrainSlot.Concave || slot == TerrainSlot.Cap;
            int width = joint ? 72 : 160, height = joint ? 72 : slot == TerrainSlot.Attached ? 104 : 40;
            return Art(theme + " " + Label(slot), theme, Label(slot), width, height, (u, v) =>
            {
                if (slot == TerrainSlot.Attached)
                {
                    float heightField = .7f - Mathf.Abs(u - .5f);
                    float crystal = Mathf.Abs(u - .5f - v * .10f);
                    if (v > heightField || crystal > .19f * (1 - v)) return Color.clear;
                    return Color.Lerp(new Color(.20f, .32f, .34f), new Color(.71f, .85f, .60f), Mathf.Clamp01(v * 1.5f + (u < .53f ? .15f : -.10f)));
                }
                float field = Mathf.Sin(u * Mathf.PI * 6 + seed) * .035f + Mathf.Sin(u * Mathf.PI * 14) * .024f;
                float upper = .86f + field;
                float lower = .12f + .15f * Mathf.Abs(Mathf.Sin(u * Mathf.PI * (slot == TerrainSlot.Ceiling ? 4 : 3) + seed));
                if (joint)
                {
                    float radius = Vector2.Distance(new Vector2(u, v), new Vector2(.48f, .45f));
                    if (radius > .46f + field || (slot == TerrainSlot.Concave && u > .52f && v > .52f && u + v > 1.31f)) return Color.clear;
                    upper = 1;
                    lower = 0;
                }
                else if (v < lower || v > upper) return Color.clear;
                float stagger = (slot == TerrainSlot.Left || slot == TerrainSlot.Right ? .37f : .12f) * v;
                float seam = Mathf.Repeat(u * (slot == TerrainSlot.Top ? 5 : 4) + stagger, 1);
                float chip = Hash(Mathf.FloorToInt(u * 70), Mathf.FloorToInt(v * 39), seed);
                float broad = Hash(Mathf.FloorToInt(u * 4 + stagger), 1, seed);
                float rim = Mathf.SmoothStep(0, 1, (v - lower) / Mathf.Max(.1f, upper - lower));
                var result = stone * (.55f + .47f * rim + .27f * broad + .14f * chip);
                if (upper - v < .13f || seam > .96f) result *= 1.26f;
                if (seam < .034f || v - lower < .06f) result *= .51f;
                if (slot == TerrainSlot.Top && rim > .62f && chip > .94f)
                    result = Color.Lerp(result, new Color(.80f, .67f, .45f), .56f);
                result.a = 1;
                return result;
            });
        }

        private static Color DrawHorizon(float u, float v)
        {
            var c = Color.Lerp(new Color(.19f, .28f, .36f), new Color(.065f, .13f, .24f), v);
            float haze = Mathf.Exp(-27 * ((u - .54f) * (u - .54f) + (v - .49f) * (v - .49f)));
            c = Color.Lerp(c, new Color(.42f, .44f, .40f), haze * .40f);
            for (int n = 0; n < 3; n++)
            {
                float ridge = .10f + n * .115f + .10f * Mathf.Abs(Mathf.Sin(u * (12 + 6 * n) + 2.4f * n)) +
                    .07f * Mathf.Abs(Mathf.Sin(u * (29 + 4 * n)));
                if (v < ridge) c = Color.Lerp(c, new Color(.08f + .038f * n, .15f + .029f * n, .22f + .03f * n), .68f);
            }
            return c;
        }

        private static Color DrawMonument(float u, float v)
        {
            float slope = .04f * Mathf.Sin(v * 10f);
            if (u < .18f + slope || u > .82f - slope || v > .94f - (u - .18f) * .29f) return Color.clear;
            float horizontal = Mathf.Repeat(v * 8, 1);
            bool groove = (u > .38f && u < .43f) || (u > .52f && u < .56f) || horizontal < .075f;
            return Color.Lerp(new Color(.13f, .19f, .26f), new Color(.25f, .36f, .38f),
                groove ? .07f : .44f + .24f * Mathf.Sin(7 * u) + .24f * v);
        }

        private static TerrainArt Art(string name, string theme, string purpose, int width, int height, Func<float, float, Color> draw)
        {
            string media = Folder + "/原创图件";
            Directory.CreateDirectory(media);
            string file = $"{media}/{name}.png";
            string reference = $"{media}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TerrainArt>(reference);
            if (existing != null) return existing;
            if (!File.Exists(file))
            {
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                var pixels = new Color32[width * height];
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = draw((x + .5f) / width, (y + .5f) / height);
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(file, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(file, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(file);
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var art = Save<TerrainArt>(reference);
            art.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(file);
            art.theme = theme;
            art.purpose = purpose;
            art.origin = "星骸回廊原创工坊示例 · " + name;
            Persist(art);
            return art;
        }

        private static T Save<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var result = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(result, path);
            return result;
        }

        private static void Persist(UnityEngine.Object target)
        {
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
        }

        private static float Hash(int x, int y, int seed) => Mathf.Repeat(Mathf.Sin(x * 127.1f + y * 81.9f + seed) * 4375.15f, 1);
        private static string Label(TerrainSlot slot) => TerrainWorkshopShelf.Label(slot).Replace("/", "-");
    }
}
