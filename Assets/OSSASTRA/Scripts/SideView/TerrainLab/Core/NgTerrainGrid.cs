using System;
using System.Collections.Generic;
using System.Linq;

namespace Ossastra.SideView.TerrainLab
{
    [Serializable]
    public struct TerrainCell : IEquatable<TerrainCell>
    {
        public int x, y;
        public TerrainCell(int x, int y) => (this.x, this.y) = (x, y);
        public bool Equals(TerrainCell other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is TerrainCell other && Equals(other);
        public override int GetHashCode() => unchecked(x * 397 ^ y);
        public override string ToString() => $"{x},{y}";
    }

    public readonly struct TerrainBox : IEquatable<TerrainBox>
    {
        public readonly int X, Y, Width, Height;
        public TerrainBox(int x, int y, int width, int height) => (X, Y, Width, Height) = (x, y, width, height);
        public bool Contains(TerrainCell cell) => cell.x >= X && cell.x < X + Width && cell.y >= Y && cell.y < Y + Height;
        public bool Equals(TerrainBox other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
        public override bool Equals(object obj) => obj is TerrainBox other && Equals(other);
        public override int GetHashCode() => unchecked(((X * 397 ^ Y) * 397 ^ Width) * 397 ^ Height);
        public override string ToString() => $"{X},{Y} {Width} × {Height}";
    }

    [Flags]
    public enum TerrainNeighbors : byte
    {
        None = 0, North = 1, East = 2, South = 4, West = 8,
        NorthEast = 16, SouthEast = 32, SouthWest = 64, NorthWest = 128, All = 255
    }

    public sealed class NgTerrainGrid
    {
        private readonly Dictionary<TerrainCell, int> cells = new Dictionary<TerrainCell, int>();
        private static readonly (int x, int y, TerrainNeighbors flag)[] neighborOffsets =
        {
            (0, 1, TerrainNeighbors.North), (1, 0, TerrainNeighbors.East), (0, -1, TerrainNeighbors.South),
            (-1, 0, TerrainNeighbors.West), (1, 1, TerrainNeighbors.NorthEast), (1, -1, TerrainNeighbors.SouthEast),
            (-1, -1, TerrainNeighbors.SouthWest), (-1, 1, TerrainNeighbors.NorthWest)
        };

        public const int Erase = -1;
        public IEnumerable<KeyValuePair<TerrainCell, int>> Cells => cells.OrderBy(pair => pair.Key.y).ThenBy(pair => pair.Key.x);

        public int Paint(IEnumerable<TerrainCell> points, int material)
        {
            if (material < Erase) throw new ArgumentOutOfRangeException(nameof(material));
            var changed = 0;
            foreach (var point in points)
            {
                if (material == Erase)
                {
                    if (cells.Remove(point)) changed++;
                }
                else if (!cells.TryGetValue(point, out var previous) || previous != material)
                {
                    cells[point] = material;
                    changed++;
                }
            }
            return changed;
        }

        public TerrainNeighbors Neighbors(TerrainCell cell)
        {
            var result = TerrainNeighbors.None;
            foreach (var (x, y, flag) in neighborOffsets)
                if (cells.ContainsKey(new TerrainCell(cell.x + x, cell.y + y))) result |= flag;
            return result;
        }

        public IEnumerable<TerrainBox> CollisionBoxes()
        {
            var available = new HashSet<TerrainCell>(cells.Keys);
            foreach (var origin in cells.Keys.OrderBy(cell => cell.y).ThenBy(cell => cell.x))
            {
                if (!available.Contains(origin)) continue;
                int width = 1, height = 1;
                while (available.Contains(new TerrainCell(origin.x + width, origin.y))) width++;
                while (RowAvailable(origin.x, origin.y + height, width, available)) height++;
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        available.Remove(new TerrainCell(origin.x + x, origin.y + y));
                yield return new TerrainBox(origin.x, origin.y, width, height);
            }
        }

        private static bool RowAvailable(int startX, int y, int width, HashSet<TerrainCell> available)
        {
            for (int x = 0; x < width; x++)
                if (!available.Contains(new TerrainCell(startX + x, y))) return false;
            return true;
        }

        public static int Variant(TerrainCell cell, int material, int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            uint hash = unchecked((uint)(cell.x * 73856093 ^ cell.y * 19349663 ^ material * 83492791));
            hash ^= hash >> 16;
            return (int)(hash % (uint)count);
        }

        public static IEnumerable<TerrainCell> Stroke(TerrainCell start, TerrainCell end)
        {
            int dx = Math.Abs(end.x - start.x), dy = -Math.Abs(end.y - start.y);
            int stepX = Math.Sign(end.x - start.x), stepY = Math.Sign(end.y - start.y), error = dx + dy;
            while (true)
            {
                yield return start;
                if (start.Equals(end)) yield break;
                int twice = error * 2;
                if (twice >= dy) { error += dy; start.x += stepX; }
                if (twice <= dx) { error += dx; start.y += stepY; }
            }
        }
    }
}
