/*
 * 可序列化的地图作者数据和程序编辑入口。
 * 占位格是实体权威，套件规则、图层及人工覆盖分别保存；实际网格与碰撞由场景适配生成。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ossastra.SideView.TerrainWorkshop
{
    public enum TerrainLayerKind { Far, Middle, Near, Body, Border, Decor, Foreground, Atmosphere }

    [Serializable]
    public sealed class TerrainLayer
    {
        public TerrainLayerKind kind;
        public string label;
        public bool visible = true, locked, solo;
        public int order;
        public float parallax, light = 1;
        public TerrainLibrary library;
        public TerrainArt art;
    }

    [Serializable]
    public sealed class TerrainOverride
    {
        public string key;
        public TerrainArt art;
        public bool locked;
        public TerrainRule rule;
    }

    [Serializable]
    public sealed class TerrainStamp
    {
        public TerrainArt art;
        public TerrainLayerKind layer;
        public Vector2 position, size;
        public string anchor;
        public bool locked;
    }

    public sealed class TerrainMap : ScriptableObject
    {
        [SerializeField] private int schemaVersion = 1;
        public float cellSize = 1, collisionDepth = 3;
        public List<TerrainTile> tiles = new List<TerrainTile>();
        public TerrainKit[] kits = Array.Empty<TerrainKit>();
        public List<TerrainOverride> overrides = new List<TerrainOverride>();
        public List<TerrainStamp> stamps = new List<TerrainStamp>();
        public TerrainLayer[] layers = CreateLayers();
        public TerrainLibrary library;

        public int SchemaVersion => schemaVersion;
        public TerrainKit Kit(string id) => kits.FirstOrDefault(kit => kit != null && kit.Id == id);
        public TerrainGrid Read() => new TerrainGrid(tiles);
        public TerrainLayer Layer(TerrainLayerKind kind) => layers.FirstOrDefault(layer => layer.kind == kind);
        public bool Visible(TerrainLayerKind kind)
        {
            var layer = Layer(kind);
            return layer != null && layer.visible && (layer.solo || !layers.Any(item => item.solo && item.visible));
        }

        /// <summary>程序和画笔共享批量改格事务，成功后才替换原作者数据。</summary>
        public bool Edit(Func<TerrainGrid, bool> apply)
        {
            var draft = Read();
            if (!apply(draft)) return false;
            var next = draft.Tiles.OrderBy(tile => tile.point.y).ThenBy(tile => tile.point.x).ToList();
            tiles = next;
            return true;
        }

        public static TerrainLayer[] CreateLayers() => new[]
        {
            NewLayer(TerrainLayerKind.Far, "远景底层", -62, .04f, .30f),
            NewLayer(TerrainLayerKind.Middle, "中景结构", -48, .02f, .45f),
            NewLayer(TerrainLayerKind.Near, "近背景墙", -35, 0, .55f),
            NewLayer(TerrainLayerKind.Body, "地形主体", -24, 0, 0),
            NewLayer(TerrainLayerKind.Border, "轮廓饰边", -22, 0, 1),
            NewLayer(TerrainLayerKind.Decor, "附着装饰", -20, 0, .85f),
            NewLayer(TerrainLayerKind.Foreground, "前景遮挡", 12, 0, .8f),
            NewLayer(TerrainLayerKind.Atmosphere, "气氛", 14, 0, 0)
        };

        private static TerrainLayer NewLayer(TerrainLayerKind kind, string title, int order, float parallax, float light) =>
            new TerrainLayer { kind = kind, label = title, order = order, parallax = parallax, light = light };
    }
}
