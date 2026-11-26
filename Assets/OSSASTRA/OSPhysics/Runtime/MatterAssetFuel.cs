// 职责：将稳定草木实例一次性登记到物质世界，并记录与实例身份绑定的局部耗损。
using System.Collections.Generic;
using UnityEngine;

namespace Ossastra.OSPhysics
{
    internal sealed class MatterAssetFuel
    {
        internal sealed class Placement
        {
            internal readonly ushort Id;
            internal readonly Matter Kind;
            internal readonly MatterAssetStamp Art;
            internal readonly Rect Bounds;
            internal readonly Rect Fuel;
            internal readonly bool Flipped;
            internal readonly int[] Cells;
            internal readonly Vector2Int? Support;
            internal readonly int Slot;
            internal readonly int[] DamageSource;
            internal bool Dirty = true;

            internal Placement(ushort id, Matter kind, MatterAssetStamp art, Rect bounds, Rect fuel, bool flipped,
                int[] cells, Vector2Int? support, int slot, int[] damageSource)
            {
                Id = id;
                Kind = kind;
                Art = art;
                Bounds = bounds;
                Fuel = fuel;
                Flipped = flipped;
                Cells = cells;
                Support = support;
                Slot = slot;
                DamageSource = damageSource;
            }
        }

        internal const byte Initial = 224;
        internal const int TilePixels = 24, AtlasColumns = 16, AtlasSize = TilePixels * AtlasColumns;
        private readonly MatterWorld world;
        private readonly Vector2 origin;
        private readonly float size;
        private readonly ushort[] owners;
        private readonly List<Placement> instances = new List<Placement>();
        private ushort serial;

        private readonly Color32[] damage;
        private readonly bool[] occupiedSlots = new bool[AtlasColumns * AtlasColumns];
        internal readonly Color32[] Atlas = new Color32[AtlasSize * AtlasSize];
        internal IReadOnlyList<Placement> Instances => instances;
        internal bool Changed { get; private set; } = true;

        internal MatterAssetFuel(MatterWorld world, Vector2 origin, float size)
        {
            this.world = world;
            this.origin = origin;
            this.size = size;
            owners = new ushort[world.Width * world.Height];
            damage = new Color32[owners.Length];
        }

        internal bool Owns(int x, int y) => owners[y * world.Width + x] != 0;
        internal bool IsOwner(int index, ushort id) => owners[index] == id;
        internal int GeometryRevision { get; private set; }

        internal bool Inspect(MatterAssetStamp art, Matter kind, Rect bounds, Rect fuel, bool flip,
            out RectInt cells, out int count, out string reason)
        {
            cells = ToCells(fuel);
            count = 0;
            Rect worldBounds = new Rect(origin, new Vector2(world.Width, world.Height) * size);
            if (art.Image == null || bounds.xMin < worldBounds.xMin || bounds.yMin < worldBounds.yMin ||
                bounds.xMax > worldBounds.xMax || bounds.yMax > worldBounds.yMax || cells.width < 1 || cells.height < 1 ||
                serial == ushort.MaxValue || instances.Count >= occupiedSlots.Length ||
                (kind != Matter.Grass && kind != Matter.Wood))
            {
                reason = "图件或燃料区域超出样板范围。";
                return false;
            }
            for (int y = cells.yMin; y < cells.yMax; y++)
            for (int x = cells.xMin; x < cells.xMax; x++)
            {
                if (!InSource(art, bounds, flip, x, y)) continue;
                count++;
                if (!world.TrySnapshot(x, y, out var cell) || cell.Blocked || Owns(x, y) || cell.Amount != 0)
                {
                    reason = "燃料区域已有实体、物质或其他图件。";
                    return false;
                }
            }
            reason = count > 0 ? null : "当前姿态下，可燃轮廓小于一个物质格。";
            return count > 0;
        }

        private bool InSource(MatterAssetStamp art, Rect bounds, bool flip, int x, int y)
        {
            if (art.Fuel.width <= 0 || art.Fuel.height <= 0) return true;
            Vector2 point = (origin + new Vector2(x + .5f, y + .5f) * size - bounds.min) / bounds.size;
            if (flip) point.x = 1 - point.x;
            return art.ContainsFuel(point);
        }

        internal bool TryAdd(MatterAssetStamp art, Matter kind, Rect bounds, Rect fuel, bool flip,
            Vector2Int? support, out string reason)
        {
            if (!Inspect(art, kind, bounds, fuel, flip, out var cells, out int count, out reason)) return false;
            int[] owned = new int[count];
            int slot = System.Array.IndexOf(occupiedSlots, false);
            occupiedSlots[slot] = true;
            ushort id = ++serial;
            int n = 0;
            // 主线程一次提交。预检逐格确认空格、容量、边界和属主，整段中间无求解或回调。
            for (int y = cells.yMin; y < cells.yMax; y++)
            for (int x = cells.xMin; x < cells.xMax; x++)
            {
                if (!InSource(art, bounds, flip, x, y)) continue;
                int i = y * world.Width + x;
                int admitted = world.Add(x, y, kind, Initial);
                if (admitted != Initial) throw new System.InvalidOperationException("燃料登记与提交预检不一致。");
                owned[n++] = i;
                owners[i] = id;
                damage[i] = new Color32(255, 0, 0, 255);
            }
            instances.Add(new Placement(id, kind, art, bounds, fuel, flip, owned, support, slot, BuildDamageLookup(bounds, owned)));
            GeometryRevision++;
            Changed = true;
            return true;
        }

        private int[] BuildDamageLookup(Rect bounds, int[] cells)
        {
            // 投影按实际位置映射到最近的有效燃料；轮廓本身仍由完整原图的 Alpha 决定。
            var lookup = new int[TilePixels * TilePixels];
            var centers = new Vector2[cells.Length];
            for (int i = 0; i < cells.Length; i++)
                centers[i] = origin + new Vector2(cells[i] % world.Width + .5f, cells[i] / world.Width + .5f) * size;
            for (int y = 0; y < TilePixels; y++)
            for (int x = 0; x < TilePixels; x++)
            {
                Vector2 p = bounds.min + new Vector2((x + .5f) / TilePixels, (y + .5f) / TilePixels) * bounds.size;
                float best = float.PositiveInfinity;
                int nearest = cells[0];
                for (int i = 0; i < centers.Length; i++)
                {
                    float distance = (p - centers[i]).sqrMagnitude;
                    if (distance >= best) continue;
                    best = distance;
                    nearest = cells[i];
                }
                lookup[y * TilePixels + x] = nearest;
            }
            return lookup;
        }

        internal RectInt ToCells(Rect rect)
        {
            var lo = Vector2Int.CeilToInt((rect.min - origin) / size - Vector2.one * .5f);
            var hi = Vector2Int.CeilToInt((rect.max - origin) / size - Vector2.one * .5f);
            return new RectInt(lo, hi - lo);
        }

        internal void UpdateDamage()
        {
            for (int i = instances.Count - 1; i >= 0; i--)
            {
                var instance = instances[i];
                bool remains = false;
                foreach (int index in instance.Cells)
                {
                    if (owners[index] != instance.Id) continue;
                    world.TrySnapshot(index % world.Width, index / world.Width, out var sample);
                    Color32 before = damage[index];
                    if (sample.Kind != instance.Kind || sample.Amount == 0)
                    {
                        owners[index] = 0;
                        damage[index] = default;
                        instance.Dirty = Changed = true;
                        continue;
                    }

                    remains = true;
                    byte retention = (byte)Mathf.Min(before.r, sample.Amount * 255 / Initial);
                    byte charred = (byte)Mathf.Max(before.g, Mathf.Max(sample.Burning ? 155 : 0, (255 - retention) * 3 / 4));
                    if (retention == before.r && charred == before.g) continue;
                    damage[index] = new Color32(retention, charred, 0, 255);
                    instance.Dirty = Changed = true;
                }
                if (!remains) Delete(i);
            }
        }

        internal void RefreshAtlas()
        {
            foreach (var instance in instances)
            {
                if (!instance.Dirty) continue;
                var origin = AtlasOrigin(instance.Slot);
                for (int y = 0; y < TilePixels; y++)
                for (int x = 0; x < TilePixels; x++)
                {
                    int source = instance.DamageSource[y * TilePixels + x];
                    Atlas[(origin.y + y) * AtlasSize + origin.x + x] =
                        owners[source] == instance.Id ? damage[source] : default;
                }
                instance.Dirty = false;
            }
            Changed = false;
        }

        internal static Vector2Int AtlasOrigin(int slot) =>
            new Vector2Int(slot % AtlasColumns * TilePixels, slot / AtlasColumns * TilePixels);

        internal bool DeleteAt(Vector2 point)
        {
            for (int i = instances.Count - 1; i >= 0; i--)
            {
                if (!instances[i].Bounds.Contains(point)) continue;
                Delete(i);
                return true;
            }
            return false;
        }

        internal void DeleteUnsupported(Vector2Int tile)
        {
            for (int i = instances.Count - 1; i >= 0; i--)
                if (instances[i].Support == tile) Delete(i);
        }

        private void Delete(int i)
        {
            var instance = instances[i];
            foreach (int index in instance.Cells)
            {
                if (owners[index] != instance.Id) continue;
                int x = index % world.Width;
                int y = index / world.Width;
                world.TrySnapshot(x, y, out var sample);
                if (sample.Kind == instance.Kind) world.Remove(x, y);
                owners[index] = 0;
                damage[index] = default;
            }
            occupiedSlots[instance.Slot] = false;
            instances.RemoveAt(i);
            GeometryRevision++;
            Changed = true;
        }

        internal void Clear()
        {
            instances.Clear();
            GeometryRevision++;
            System.Array.Clear(owners, 0, owners.Length);
            System.Array.Clear(damage, 0, damage.Length);
            System.Array.Clear(occupiedSlots, 0, occupiedSlots.Length);
            System.Array.Clear(Atlas, 0, Atlas.Length);
            serial = 0;
            Changed = true;
        }
    }
}
