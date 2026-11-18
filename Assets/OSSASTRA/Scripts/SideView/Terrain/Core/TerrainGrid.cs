/* 地形占位、连续轮廓与合并碰撞的纯 C# 核心。作者笔刷和程序编辑均使用这份网格。 */
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ossastra.SideView.TerrainWorkshop
{
    [Serializable]
    public struct TerrainPoint : IEquatable<TerrainPoint>
    {
        public int x, y;

        public TerrainPoint(int x, int y) => (this.x, this.y) = (x, y);
        public bool Equals(TerrainPoint other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is TerrainPoint other && Equals(other);
        public override int GetHashCode() => unchecked(x * 397 ^ y);
        public override string ToString() => FormattableString.Invariant($"{x},{y}");

        internal TerrainPoint Offset(int dx, int dy) => new TerrainPoint(checked(x + dx), checked(y + dy));
    }

    [Serializable]
    public struct TerrainTile
    {
        public TerrainPoint point;
        public string kit;

        public TerrainTile(TerrainPoint point, string kit) => (this.point, this.kit) = (point, kit);
    }

    public enum TerrainSlot { Fill, Top, Left, Right, Ceiling, Convex, Concave, Cap, Attached }

    public readonly struct TerrainBand
    {
        public TerrainPoint Start { get; }
        public TerrainPoint End { get; }
        public TerrainSlot Slot { get; }
        public string Kit { get; }
        public int Length => checked((int)(Math.Abs((long)End.x - Start.x) + Math.Abs((long)End.y - Start.y)));
        public int OutwardX => End.y.CompareTo(Start.y);
        public int OutwardY => Start.x.CompareTo(End.x);
        public string Key => KeyAt(Start);

        internal TerrainBand(TerrainPoint start, TerrainPoint end, TerrainSlot slot, string kit)
            => (Start, End, Slot, Kit) = (start, end, slot, kit);

        // Anchors are in world-grid coordinates so extending a run does not change existing placements.
        public string KeyAt(TerrainPoint absoluteAnchor)
            => FormattableString.Invariant($"edge:{(int)Slot}:{absoluteAnchor.x}:{absoluteAnchor.y}");
    }

    public readonly struct TerrainCorner
    {
        public TerrainPoint Point { get; }
        public TerrainSlot Slot { get; }
        public string Kit { get; }
        public int OutwardX { get; }
        public int OutwardY { get; }
        public TerrainSlot IncomingSlot { get; }
        public TerrainSlot OutgoingSlot { get; }
        public string Key => FormattableString.Invariant($"corner:{(int)Slot}:{Point.x}:{Point.y}:{OutwardX}:{OutwardY}");

        internal TerrainCorner(TerrainPoint point, TerrainSlot slot, string kit, int outwardX, int outwardY,
            TerrainSlot incomingSlot, TerrainSlot outgoingSlot)
            => (Point, Slot, Kit, OutwardX, OutwardY, IncomingSlot, OutgoingSlot)
                = (point, slot, kit, outwardX, outwardY, incomingSlot, outgoingSlot);
    }

    public readonly struct TerrainBox
    {
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }

        internal TerrainBox(int x, int y, int width, int height) => (X, Y, Width, Height) = (x, y, width, height);
    }

    public sealed class TerrainLoop
    {
        // Each vertex is the start of one unit edge. The last edge closes back to Points[0].
        public IReadOnlyList<TerrainPoint> Points { get; }
        public bool IsHole { get; }

        internal TerrainLoop(TerrainPoint[] points, bool isHole) =>
            (Points, IsHole) = (Array.AsReadOnly(points), isHole);
    }

    public sealed class TerrainGrid
    {
        private readonly Dictionary<TerrainPoint, string> cells = new Dictionary<TerrainPoint, string>();
        private BoundaryData boundary;

        public TerrainGrid(IEnumerable<TerrainTile> tiles)
        {
            if (tiles == null) throw new ArgumentNullException(nameof(tiles));
            foreach (var tile in tiles)
            {
                ValidatePoint(tile.point);
                if (string.IsNullOrEmpty(tile.kit))
                    throw new ArgumentException($"Occupied cell {tile.point} needs a kit identity.", nameof(tiles));
                cells.Add(tile.point, tile.kit);
            }
        }

        public int Count => cells.Count;
        public IEnumerable<TerrainTile> Tiles => cells.OrderBy(pair => pair.Key.y).ThenBy(pair => pair.Key.x)
            .Select(pair => new TerrainTile(pair.Key, pair.Value));
        public bool TryGetKit(TerrainPoint point, out string kit) => cells.TryGetValue(point, out kit);

        public int Paint(IEnumerable<TerrainPoint> points, string kit, bool existingOnly = false)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            var edit = points.Distinct().ToArray();
            foreach (var point in edit) ValidatePoint(point);
            int changed = 0;
            foreach (var point in edit)
            {
                if (string.IsNullOrEmpty(kit))
                {
                    if (cells.Remove(point)) changed++;
                    continue;
                }
                bool occupied = cells.TryGetValue(point, out string previous);
                if ((occupied && previous != kit) || (!occupied && !existingOnly))
                {
                    cells[point] = kit;
                    changed++;
                }
            }
            if (changed != 0) boundary = null;
            return changed;
        }

        public bool TransformSelection(IEnumerable<TerrainPoint> points, TerrainPoint offset, bool copy, bool overwrite = false)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (offset.Equals(default(TerrainPoint))) return false;
            var selected = new HashSet<TerrainPoint>(points.Where(cells.ContainsKey));
            if (selected.Count == 0) return false;
            var transformed = new List<TerrainTile>(selected.Count);
            foreach (var point in selected)
            {
                var target = point.Offset(offset.x, offset.y);
                ValidatePoint(target);
                if (!overwrite && cells.ContainsKey(target) && (copy || !selected.Contains(target))) return false;
                transformed.Add(new TerrainTile(target, cells[point]));
            }
            if (!copy)
                foreach (var point in selected) cells.Remove(point);
            foreach (var tile in transformed) cells[tile.point] = tile.kit;
            boundary = null;
            return true;
        }

        public static IEnumerable<TerrainPoint> Line(TerrainPoint start, TerrainPoint end)
        {
            ValidatePoint(start);
            ValidatePoint(end);
            long width = Math.Abs((long)end.x - start.x), height = Math.Abs((long)end.y - start.y);
            int stepX = end.x.CompareTo(start.x), stepY = end.y.CompareTo(start.y);
            long x = 0, y = 0;
            yield return start;
            while (x < width || y < height)
            {
                // Compare the next boundary crossings of a segment between the two cell centers.
                // At a perfect corner use an X step first in one direction and a Y step first in reverse.
                decimal crossingX = (2m * x + 1) * height;
                decimal crossingY = (2m * y + 1) * width;
                if (x < width && (y == height || crossingX < crossingY || (crossingX == crossingY && stepX > 0)))
                {
                    start.x += stepX;
                    x++;
                }
                else
                {
                    start.y += stepY;
                    y++;
                }
                yield return start;
            }
        }

        public static IEnumerable<TerrainPoint> Rectangle(TerrainPoint first, TerrainPoint last)
        {
            ValidatePoint(first);
            ValidatePoint(last);
            int maxX = Math.Max(first.x, last.x), maxY = Math.Max(first.y, last.y);
            for (int y = Math.Min(first.y, last.y); y <= maxY; y++)
                for (int x = Math.Min(first.x, last.x); x <= maxX; x++)
                    yield return new TerrainPoint(x, y);
        }

        public IEnumerable<TerrainBox> CollisionBoxes(string kit = null)
        {
            // The collision lattice does not depend on material. A filtered fill uses the same partitioner.
            var origins = Tiles.Where(tile => kit == null || tile.kit == kit).Select(tile => tile.point).ToArray();
            var available = new HashSet<TerrainPoint>(origins);
            foreach (var origin in origins)
            {
                if (!available.Contains(origin)) continue;
                int width = 1, height = 1;
                while (available.Contains(origin.Offset(width, 0))) width++;
                while (RowAvailable(origin.Offset(0, height), width, available)) height++;
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        available.Remove(origin.Offset(x, y));
                yield return new TerrainBox(origin.x, origin.y, width, height);
            }
        }

        public IEnumerable<TerrainBand> Bands() => GetBoundary().Bands;
        public IEnumerable<TerrainCorner> Corners() => GetBoundary().Corners;
        public IEnumerable<TerrainLoop> Loops() => GetBoundary().Loops;

        private BoundaryData GetBoundary()
        {
            if (boundary != null) return boundary;
            var result = new BoundaryData();
            var edges = OpenEdges().ToArray();
            var outgoing = new Dictionary<TerrainPoint, List<int>>();
            for (int i = 0; i < edges.Length; i++)
            {
                if (!outgoing.TryGetValue(edges[i].Start, out var candidates))
                    outgoing.Add(edges[i].Start, candidates = new List<int>(2));
                candidates.Add(i);
            }
            var visited = new bool[edges.Length];
            for (int i = 0; i < edges.Length; i++)
            {
                if (visited[i]) continue;
                var loop = new List<Edge>();
                int current = i;
                do
                {
                    if (visited[current]) throw new InvalidOperationException("An exposed terrain edge did not close.");
                    visited[current] = true;
                    Edge edge = edges[current];
                    loop.Add(edge);
                    var next = outgoing[edge.End];
                    current = next[0];
                    // When two solids touch diagonally, turning left follows the same solid around the vertex.
                    if (next.Count == 2 && Turn(edge, edges[current]) != 1) current = next[1];
                } while (current != i);
                DescribeLoop(loop, result);
            }
            boundary = result;
            return boundary;
        }

        private IEnumerable<Edge> OpenEdges()
        {
            foreach (var tile in Tiles)
            {
                var point = tile.point;
                // For each exposed side, traversal keeps solid on the left.
                if (!cells.ContainsKey(point.Offset(0, -1)))
                    yield return new Edge(point, 1, 0, TerrainSlot.Ceiling, tile.kit);
                if (!cells.ContainsKey(point.Offset(1, 0)))
                    yield return new Edge(point.Offset(1, 0), 0, 1, TerrainSlot.Right, tile.kit);
                if (!cells.ContainsKey(point.Offset(0, 1)))
                    yield return new Edge(point.Offset(1, 1), -1, 0, TerrainSlot.Top, tile.kit);
                if (!cells.ContainsKey(point.Offset(-1, 0)))
                    yield return new Edge(point.Offset(0, 1), 0, -1, TerrainSlot.Left, tile.kit);
            }
        }

        private static void DescribeLoop(List<Edge> loop, BoundaryData result)
        {
            int start = 0;
            long turning = 0;
            for (int i = 0; i < loop.Count; i++)
            {
                Edge incoming = loop[(i + loop.Count - 1) % loop.Count], outgoing = loop[i];
                int turn = Turn(incoming, outgoing);
                if (turn == 0) continue;
                start = i;
                turning += turn;
                var corner = turn > 0 ? TerrainSlot.Convex : TerrainSlot.Concave;
                // At a kit transition the outgoing side owns the corner. No corner is added on a flat material seam.
                result.Corners.Add(new TerrainCorner(outgoing.Start, corner, outgoing.Kit,
                    incoming.Dy + outgoing.Dy, -incoming.Dx - outgoing.Dx, incoming.Slot, outgoing.Slot));
            }
            result.Loops.Add(new TerrainLoop(loop.Select(edge => edge.Start).ToArray(), turning < 0));

            Edge run = loop[start], previous = run;
            for (int i = 1; i < loop.Count; i++)
            {
                Edge edge = loop[(start + i) % loop.Count];
                if (edge.Slot != run.Slot || edge.Kit != run.Kit)
                {
                    result.Bands.Add(new TerrainBand(run.Start, previous.End, run.Slot, run.Kit));
                    run = edge;
                }
                previous = edge;
            }
            result.Bands.Add(new TerrainBand(run.Start, previous.End, run.Slot, run.Kit));
        }

        private static int Turn(Edge a, Edge b) => a.Dx * b.Dy - a.Dy * b.Dx;

        private static bool RowAvailable(TerrainPoint origin, int width, HashSet<TerrainPoint> available)
        {
            for (int x = 0; x < width; x++)
                if (!available.Contains(origin.Offset(x, 0))) return false;
            return true;
        }

        private static void ValidatePoint(TerrainPoint point)
        {
            // Each occupied coordinate needs room for neighbor and corner coordinates in the same type.
            if (point.x == int.MinValue || point.x == int.MaxValue || point.y == int.MinValue || point.y == int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(point), "Terrain cells need a one-cell coordinate margin.");
        }

        private struct Edge
        {
            public readonly TerrainPoint Start;
            public readonly int Dx, Dy;
            public readonly TerrainSlot Slot;
            public readonly string Kit;
            public TerrainPoint End => Start.Offset(Dx, Dy);

            public Edge(TerrainPoint start, int dx, int dy, TerrainSlot slot, string kit)
                => (Start, Dx, Dy, Slot, Kit) = (start, dx, dy, slot, kit);
        }

        private sealed class BoundaryData
        {
            public readonly List<TerrainBand> Bands = new List<TerrainBand>();
            public readonly List<TerrainCorner> Corners = new List<TerrainCorner>();
            public readonly List<TerrainLoop> Loops = new List<TerrainLoop>();
        }
    }
}
