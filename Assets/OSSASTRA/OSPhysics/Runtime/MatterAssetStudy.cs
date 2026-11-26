// 职责：持有实验样板的作者占格和燃料实例，向同一世界登记阻挡，并用地编套件求解画片。
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ossastra.OSPhysics
{
    public enum MatterAssetBrush { Matter, Block, Grass, Wood, Delete }

    internal sealed class MatterAssetStudy : IDisposable
    {
        private readonly IMatterAssetTerrainKit kit;
        private readonly MatterWorld world;
        private readonly Vector2 origin;
        private readonly float cell;
        private readonly Transform root;
        private readonly BoxCollider2D gate;
        private readonly PolygonCollider2D solid;
        private readonly HashSet<Vector2Int> tiles = new HashSet<Vector2Int>();
        private readonly List<MatterAssetQuad> parts = new List<MatterAssetQuad>();
        private readonly MatterAssetDrawing drawing;
        private readonly RectInt gateCells;
        private readonly Rect gateWorld;
        private readonly int gateColumn;
        private bool layoutDirty = true;

        internal MatterAssetFuel Fuel { get; }
        internal string Status => kit.Status;
        internal float TileSize => kit.CellSize;
        internal int Columns { get; }
        internal int Rows { get; }
        internal int Count => tiles.Count;
        internal IEnumerable<Vector2Int> Blocks => tiles;
        internal int DrawCount => drawing.VisibleBatches;
        internal int PieceCount => parts.Count;
        internal float BuildTime { get; private set; }
        internal IReadOnlyList<MatterAssetStamp> Stamps(MatterAssetBrush tool) =>
            tool == MatterAssetBrush.Grass ? kit.Grass : kit.Wood;

        internal MatterAssetStudy(IMatterAssetTerrainKit kit, MatterWorld world, Transform parent, Vector2 origin, float cell)
        {
            this.kit = kit;
            this.world = world;
            this.origin = origin;
            this.cell = cell;
            Fuel = new MatterAssetFuel(world, origin, cell);
            Columns = Mathf.FloorToInt(world.Width * cell / TileSize);
            Rows = Mathf.FloorToInt(world.Height * cell / TileSize);
            gateColumn = Columns * 2 / 5;
            gateWorld = new Rect(origin + new Vector2(gateColumn, 1) * TileSize, new Vector2(1, 3) * TileSize);
            gateCells = Fuel.ToCells(gateWorld);
            drawing = new MatterAssetDrawing(parent, origin);
            root = new GameObject("资产样板碰撞").transform;
            root.SetParent(parent, false);
            solid = root.gameObject.AddComponent<PolygonCollider2D>();
            solid.enabled = false;
            var go = new GameObject("资产样板闸门");
            go.transform.SetParent(root, false);
            go.transform.position = gateWorld.center;
            gate = go.AddComponent<BoxCollider2D>();
            gate.size = gateWorld.size;
            gate.enabled = false;
        }

        internal Rect TileRect(Vector2Int tile) => new Rect(origin + (Vector2)tile * TileSize, Vector2.one * TileSize);

        internal Vector2Int TileAt(Vector2 position) => Vector2Int.FloorToInt((position - origin) / TileSize);

        internal RectInt TileArea(Vector2 a, Vector2 b)
        {
            var lo = TileAt(Vector2.Min(a, b));
            var hi = TileAt(Vector2.Max(a, b));
            return new RectInt(lo, hi - lo + Vector2Int.one);
        }

        internal bool Edit(RectInt area, bool add, out string reason)
        {
            for (int y = area.yMin; y < area.yMax; y++)
            for (int x = area.xMin; x < area.xMax; x++)
            {
                var tile = new Vector2Int(x, y);
                if (x < 0 || x >= Columns || y < 0 || y >= Rows)
                {
                    reason = "地编格超出实验边界。";
                    return false;
                }
                if (!add || tiles.Contains(tile)) continue;
                if (x == gateColumn && y >= 1 && y < 4)
                {
                    reason = "这里是可开闸口。";
                    return false;
                }
                RectInt cells = Fuel.ToCells(TileRect(tile));
                for (int cy = cells.yMin; cy < cells.yMax; cy++)
                for (int cx = cells.xMin; cx < cells.xMax; cx++)
                {
                    world.TrySnapshot(cx, cy, out var sample);
                    if (!sample.Blocked && sample.Amount == 0) continue;
                    reason = "范围已有物质或其他阻挡，保留现有内容。";
                    return false;
                }
            }

            for (int y = area.yMin; y < area.yMax; y++)
            for (int x = area.xMin; x < area.xMax; x++)
            {
                var tile = new Vector2Int(x, y);
                if (!(add ? tiles.Add(tile) : tiles.Remove(tile))) continue;
                var cells = Fuel.ToCells(TileRect(tile));
                for (int cy = cells.yMin; cy < cells.yMax; cy++)
                for (int cx = cells.xMin; cx < cells.xMax; cx++)
                    if (!world.SetFixed(cx, cy, add)) throw new InvalidOperationException("地形实体预检失配。");
                if (!add) Fuel.DeleteUnsupported(tile);
                layoutDirty = true;
            }
            reason = null;
            return true;
        }

        internal bool SetGate(bool open)
        {
            if (!open)
                for (int y = gateCells.yMin; y < gateCells.yMax; y++)
                for (int x = gateCells.xMin; x < gateCells.xMax; x++)
                {
                    world.TrySnapshot(x, y, out var sample);
                    if (sample.Fixed || sample.Amount > 0) return false;
                }
            for (int y = gateCells.yMin; y < gateCells.yMax; y++)
            for (int x = gateCells.xMin; x < gateCells.xMax; x++)
                world.SetBarrier(x, y, !open);
            if (gate != null) gate.enabled = !open;
            Physics2D.SyncTransforms();
            return true;
        }

        internal bool IsGate(int x, int y) => gateCells.Contains(new Vector2Int(x, y));

        internal bool Preview(Vector2 point, MatterAssetBrush brush, int variant, bool flip,
            out MatterAssetStamp art, out Rect bounds, out Rect fuel, out Vector2 rootPoint, out Vector2Int? support,
            out string reason)
        {
            art = default;
            bounds = fuel = default;
            rootPoint = point;
            support = null;
            var list = Stamps(brush);
            if (list.Count == 0)
            {
                reason = "当前套件缺少这种完整图件。";
                return false;
            }
            art = list[Mathf.Clamp(variant, 0, list.Count - 1)];
            bool foundSupport = !art.AttachToGround;
            if (art.AttachToGround)
            {
                Vector2Int tile = TileAt(point + Vector2.up * (TileSize * .18f));
                for (int d = 0; d <= 2; d++)
                {
                    var cellBelow = tile - Vector2Int.up * d;
                    if (!tiles.Contains(cellBelow) || tiles.Contains(cellBelow + Vector2Int.up)) continue;
                    rootPoint.y = TileRect(cellBelow).yMax;
                    support = cellBelow;
                    foundSupport = true;
                    break;
                }
            }

            Vector2 pivot = art.Pivot;
            Rect area = art.Fuel.width > 0 && art.Fuel.height > 0 ? art.Fuel : new Rect(.16f, .12f, .68f, .53f);
            if (flip && art.Flip)
            {
                area.x = 1 - area.xMax;
                pivot.x = 1 - pivot.x;
            }
            bounds = new Rect(rootPoint - art.Size * pivot, art.Size);
            fuel = new Rect(bounds.min + area.min * art.Size, area.size * art.Size);
            if (support.HasValue)
            {
                float top = fuel.yMax;
                fuel.yMin = Mathf.Max(fuel.yMin, rootPoint.y + .014f);
                fuel.yMax = top;
            }
            if (!foundSupport)
            {
                reason = "草簇根部需要贴到石台顶面。";
                return false;
            }
            return Fuel.Inspect(art, brush == MatterAssetBrush.Grass ? Matter.Grass : Matter.Wood,
                bounds, fuel, flip && art.Flip, out _, out _, out reason);
        }

        internal bool Place(Vector2 point, MatterAssetBrush brush, int variant, bool flip, out string reason)
        {
            if (!Preview(point, brush, variant, flip, out var art, out var bounds, out var fuel, out _, out var support, out reason))
                return false;
            if (!Fuel.TryAdd(art, brush == MatterAssetBrush.Grass ? Matter.Grass : Matter.Wood,
                bounds, fuel, flip && art.Flip, support, out reason)) return false;
            drawing.Invalidate();
            return true;
        }

        internal bool DeleteAt(Vector2 point)
        {
            if (!Fuel.DeleteAt(point)) return false;
            drawing.Invalidate();
            return true;
        }

        internal void ClearFuel()
        {
            Fuel.Clear();
            drawing.Invalidate();
        }

        internal void FlushTerrain()
        {
            if (!layoutDirty) return;
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            layoutDirty = false;
            parts.Clear();
            kit.Solve(tiles, parts);
            var paths = new List<Vector2[]>();
            for (int y = 0; y < Rows; y++)
            {
                int x = 0;
                while (x < Columns)
                {
                    if (!tiles.Contains(new Vector2Int(x, y))) { x++; continue; }
                    var left = TileRect(new Vector2Int(x, y));
                    do { x++; } while (x < Columns && tiles.Contains(new Vector2Int(x, y)));
                    float end = origin.x + x * TileSize;
                    paths.Add(new[]
                    {
                        left.min, new Vector2(end, left.yMin), new Vector2(end, left.yMax), new Vector2(left.xMin, left.yMax)
                    });
                }
            }
            solid.pathCount = paths.Count;
            for (int i = 0; i < paths.Count; i++) solid.SetPath(i, paths[i]);
            solid.enabled = paths.Count > 0;
            Physics2D.SyncTransforms();
            drawing.Invalidate();
            BuildTime = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 /
                System.Diagnostics.Stopwatch.Frequency);
        }

        internal void Present(bool show, bool body, bool trim, bool plants, float mood, bool lit)
        {
            FlushTerrain();
            drawing.Present(parts, Fuel, show, body, trim, plants, mood, lit);
        }

        public void Dispose()
        {
            foreach (var tile in tiles)
            {
                var cells = Fuel.ToCells(TileRect(tile));
                for (int y = cells.yMin; y < cells.yMax; y++)
                for (int x = cells.xMin; x < cells.xMax; x++) world.SetFixed(x, y, false);
            }
            SetGate(true);
            if (root != null) root.gameObject.SetActive(false);
            drawing.Dispose();
            kit.Dispose();
            if (root == null) return;
            if (Application.isPlaying) Object.Destroy(root.gameObject);
            else Object.DestroyImmediate(root.gameObject);
        }
    }
}
