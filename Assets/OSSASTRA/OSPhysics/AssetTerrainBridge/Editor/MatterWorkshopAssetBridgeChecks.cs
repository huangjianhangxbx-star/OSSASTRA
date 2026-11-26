/* 功能：验证实验画笔消费当前地图的工坊片段，并保留图件的实际透明结构。
 * 在现有编辑器内创建临时网格，读取源地图和图片，无场景或资产写入。 */
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.SideView.TerrainWorkshop;
using UnityEngine;

namespace Ossastra.OSPhysics
{
    public static class MatterWorkshopAssetBridgeChecks
    {
        /// <summary>用同一形状分别走作者地图和实验画笔，比较可交互图层的顶点、UV、顺序及源图。</summary>
        public static string Verify(TerrainMap source, string kitId)
        {
            Require(source != null && source.Kit(kitId) != null, "缺少可比较的源地图和套件。");
            using var bridge = MatterWorkshopAssetBridge.Open();
            Require(bridge.Name == source.Kit(kitId).name, "画笔未选用当前作者地图套件。");
            Require(Mathf.Abs(bridge.CellSize - TerrainScale.Cell(source)) < .00001f, "画笔修改了作者格尺度。");

            // 同一网格包含外角、凹角、镂空及短平台，覆盖编辑会遇到的边界。
            var shape = new List<Vector2Int>();
            for (int y = 1; y <= 5; y++)
                for (int x = 1; x <= 10; x++)
                    if (!(y >= 4 && x >= 7) && !(x >= 3 && x <= 5 && y >= 2 && y <= 3))
                        shape.Add(new Vector2Int(x, y));
            shape.Add(new Vector2Int(12, 7));
            shape.Add(new Vector2Int(13, 7));
            var grid = new TerrainGrid(shape.Select(point => new TerrainTile(new TerrainPoint(point.x, point.y), kitId)));
            var expected = TerrainAssembly.Solve(source, grid).Parts
                .Where(part => part.Layer == TerrainLayerKind.Body || part.Layer == TerrainLayerKind.Border ||
                    part.Layer == TerrainLayerKind.Decor).ToArray();
            var quads = new List<MatterAssetQuad>();
            bridge.Solve(shape, quads);
            Require(quads.Count == expected.Length && quads.Count > 10, "漏画或重复画了同源地形片段。");
            Require(quads.Any(quad => quad.Slot == "Fill") && quads.Any(quad => quad.Slot == "Top") &&
                quads.Any(quad => quad.Slot == "Ceiling") && quads.Any(quad => quad.Image != null),
                "黑体、外露石边或洞顶缺失。");
            for (int i = 0; i < expected.Length; i++)
            {
                var actual = quads[i];
                var part = expected[i];
                Require(actual.Image == part.Art?.Image && actual.UV == part.UV &&
                    actual.A == part.A && actual.B == part.B && actual.C == part.C && actual.D == part.D &&
                    actual.Tint == part.Tint && actual.Order == part.SortOrder(source) && actual.FadeVertices == part.FadeVertices &&
                    actual.FlipX == part.FlipX && actual.FlipY == part.FlipY,
                    "地形片段改变了正常工坊产物，片段号 " + i);
            }
            Require(bridge.Grass.Count >= 2 && bridge.Grass.All(stamp => stamp.Image != null && stamp.AttachToGround),
                "完整附着草图少于两种。");
            Require(bridge.Wood.Count > 0 && bridge.Wood[0].Image != null && !bridge.Wood[0].AttachToGround,
                "木图件未作为独立构件接入。");
            var root = bridge.Wood[0];
            Require(root.ContainsFuel(new Vector2(.7f, .8f)) && !root.ContainsFuel(new Vector2(.4f, .6f)),
                "木图件的枝条未映射为燃料，或枝间透空被填实。");
            return $"{quads.Count} 片同源图件 / {bridge.Grass.Count} 草 / {bridge.Wood.Count} 木 · {bridge.CellSize:F7} U / 作者格";
        }

        private static void Require(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException(reason);
        }
    }
}
