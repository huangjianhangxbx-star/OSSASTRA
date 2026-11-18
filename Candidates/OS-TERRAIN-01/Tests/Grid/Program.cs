using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Ossastra.SideView.TerrainWorkshop;

internal static class Program
{
    private static TerrainPoint P(int x, int y) => new TerrainPoint(x, y);

    private static void Main(string[] args)
    {
        var cases = new Action[]
        {
            SolidLeftAndMaterialSeams, ClosedHoleAndBreach, DiagonalContactStaysSeparate,
            SplitAndRepairBridge, StableAbsoluteAnchor, SelectionIsAtomic,
            LineIsConnectedAndReversible, PhysicsAndFillContainTheSameSolids, ExhaustiveSmallGrid
        };
        foreach (var test in cases)
        {
            test();
            Console.WriteLine("PASS " + test.Method.Name);
        }
        if (args.Contains("--measure")) MeasureSample();
    }

    private static void SolidLeftAndMaterialSeams()
    {
        var grid = new TerrainGrid(new[] { new TerrainTile(P(0, 0), "brick"), new TerrainTile(P(1, 0), "rock") });
        var bands = grid.Bands().ToArray();
        Check(bands.Length == 6 && bands.Sum(band => band.Length) == 6, "a material seam splits adjacent surface art only");
        Check(bands.All(band => band.Start.x != 1 || band.End.x != 1), "the interior interface has no band");
        var top = bands.Single(band => band.Slot == TerrainSlot.Top && band.Kit == "brick");
        Check(top.Start.Equals(P(1, 1)) && top.End.Equals(P(0, 1)), "solid must be left of every directed band");
        Check(top.OutwardX == 0 && top.OutwardY == 1, "top faces upward");
        Check(grid.Corners().Count() == 4, "the material seam introduces no geometric corners");
    }

    private static void ClosedHoleAndBreach()
    {
        var grid = Full(-1, -1, 1, 1);
        grid.Paint(new[] { P(0, 0) }, null);
        var loops = grid.Loops().ToArray();
        Check(loops.Length == 2 && loops.Count(loop => loop.IsHole) == 1, "enclosed hole traces clockwise");
        Check(loops.Single(loop => loop.IsHole).Points.Count == 4, "one missing cell contributes four edges");
        var hole = grid.Bands().Where(band => band.Start.x >= 0 && band.Start.x <= 1 &&
            band.End.x >= 0 && band.End.x <= 1 && band.Start.y >= 0 && band.Start.y <= 1 &&
            band.End.y >= 0 && band.End.y <= 1).ToArray();
        Check(hole.Length == 4 && hole.Single(band => band.Start.y == 0 && band.End.y == 0).Slot == TerrainSlot.Top,
            "hole floor follows solid side and receives top art");
        Check(hole.Single(band => band.Start.y == 1 && band.End.y == 1).Slot == TerrainSlot.Ceiling,
            "hole ceiling receives underside art");
        Check(grid.Corners().Count(corner => corner.Slot == TerrainSlot.Concave) == 4, "hole corners are concave");
        grid.Paint(new[] { P(0, -1) }, "");
        Check(grid.Loops().Single().IsHole == false, "tunnel connected to exterior is no longer a hole");
    }

    private static void DiagonalContactStaysSeparate()
    {
        var grid = new TerrainGrid(new[] { new TerrainTile(P(0, 0), "brick"), new TerrainTile(P(1, 1), "brick") });
        var loops = grid.Loops().ToArray();
        Check(loops.Length == 2 && loops.All(loop => !loop.IsHole && loop.Points.Count == 4), "solid uses four-way connectivity");
        var sharedVertex = grid.Corners().Where(corner => corner.Point.Equals(P(1, 1))).ToArray();
        Check(sharedVertex.Length == 2 && sharedVertex.Select(corner => corner.Key).Distinct().Count() == 2, "diagonal corners need separate override identities");
        Check(sharedVertex.All(corner => corner.Slot == TerrainSlot.Convex), "each diagonal island turns around its own corner");
    }

    private static void SplitAndRepairBridge()
    {
        var grid = Full(0, 0, 2, 0);
        grid.Paint(new[] { P(1, 0) }, null);
        Check(grid.Loops().Count() == 2 && grid.CollisionBoxes().Count() == 2, "removing bridge splits render and physical islands");
        grid.Paint(new[] { P(1, 0) }, "brick");
        Check(grid.Loops().Count() == 1 && grid.CollisionBoxes().Single().Width == 3, "bridge restore rejoins render and physics");
    }

    private static void StableAbsoluteAnchor()
    {
        var grid = Full(0, 0, 3, 1);
        var far = grid.Bands().Single(band => band.Slot == TerrainSlot.Top);
        var point = P(2, 2);
        string key = far.KeyAt(point);
        grid.Paint(new[] { P(4, 1) }, "brick");
        var extended = grid.Bands().Single(band => band.Slot == TerrainSlot.Top);
        Check(extended.Start.Equals(P(5, 2)) && key == extended.KeyAt(point), "extending the run preserves unaffected spatial anchors");
    }

    private static void SelectionIsAtomic()
    {
        var grid = new TerrainGrid(new[] { new TerrainTile(P(0, 0), "brick"), new TerrainTile(P(1, 0), "rock"),
            new TerrainTile(P(3, 0), "destination") });
        Check(!grid.TransformSelection(new[] { P(0, 0), P(1, 0) }, P(2, 0), copy: false), "move rejected over unrelated terrain");
        Check(grid.TryGetKit(P(0, 0), out var before) && before == "brick" && !grid.TryGetKit(P(2, 0), out _), "rejection is atomic");
        Check(grid.TransformSelection(new[] { P(0, 0), P(1, 0) }, P(1, 0), copy: false), "overlapping selection move");
        Check(!grid.TryGetKit(P(0, 0), out _) && grid.TryGetKit(P(2, 0), out var moved) && moved == "rock", "moves preserve material");
        Check(grid.TransformSelection(new[] { P(1, 0) }, P(0, 1), copy: true), "copy");
        Check(grid.TryGetKit(P(1, 0), out _) && grid.TryGetKit(P(1, 1), out var copied) && copied == "brick", "copy keeps source intact");
        Check(grid.Paint(new[] { P(1, 1), P(4, 4) }, "rock", existingOnly: true) == 1 && !grid.TryGetKit(P(4, 4), out _),
            "replace material only touches existing cells");
    }

    private static void LineIsConnectedAndReversible()
    {
        var a = P(-3, 3);
        var b = P(5, -5);
        var forward = TerrainGrid.Line(a, b).ToArray();
        var backward = TerrainGrid.Line(b, a).Reverse().ToArray();
        Check(forward.SequenceEqual(backward), "drag order should not change painted cells");
        Check(forward[0].Equals(a) && forward.Last().Equals(b), "both endpoints included");
        for (int i = 1; i < forward.Length; i++)
            Check(Math.Abs(forward[i].x - forward[i - 1].x) + Math.Abs(forward[i].y - forward[i - 1].y) == 1,
                "one fast drag does not leave diagonal-only cell links");
    }

    private static void PhysicsAndFillContainTheSameSolids()
    {
        var grid = Full(-2, -1, 2, 2);
        grid.Paint(new[] { P(0, 0), P(0, 1) }, null);
        grid.Paint(new[] { P(1, 0), P(1, 1) }, "rock");
        Check(Covered(grid.CollisionBoxes()).SetEquals(grid.Tiles.Select(tile => tile.point)), "colliders match the occupied grid including the hole");
        Check(Covered(grid.CollisionBoxes("rock")).SetEquals(new[] { P(1, 0), P(1, 1) }), "kit-filtered fill covers its own cells only");
    }

    private static void ExhaustiveSmallGrid()
    {
        var all = TerrainGrid.Rectangle(P(0, 0), P(2, 2)).ToArray();
        var offsets = new[] { P(-1, 0), P(1, 0), P(0, -1), P(0, 1) };
        for (int mask = 0; mask < (1 << all.Length); mask++)
        {
            var filled = new HashSet<TerrainPoint>(all.Where((_, bit) => (mask & (1 << bit)) != 0));
            var grid = new TerrainGrid(filled.Select(point => new TerrainTile(point, (point.x + point.y) % 2 == 0 ? "a" : "b")));
            int expected = filled.Sum(point => offsets.Count(offset => !filled.Contains(P(point.x + offset.x, point.y + offset.y))));
            Check(grid.Bands().Sum(band => band.Length) == expected, "bands expose each open side exactly once for mask " + mask);
            Check(grid.Loops().Sum(loop => loop.Points.Count) == expected, "closed loops visit each open side exactly once for mask " + mask);
            Check(Covered(grid.CollisionBoxes()).SetEquals(filled), "no empty or overlapping collider cells for mask " + mask);
        }
    }

    private static HashSet<TerrainPoint> Covered(IEnumerable<TerrainBox> boxes)
    {
        var result = new HashSet<TerrainPoint>();
        foreach (var box in boxes)
            foreach (var point in TerrainGrid.Rectangle(P(box.X, box.Y), P(box.X + box.Width - 1, box.Y + box.Height - 1)))
                Check(result.Add(point), "merged boxes may not overlap");
        return result;
    }

    private static void MeasureSample()
    {
        var grid = Full(0, 0, 127, 63);
        var horizontal = TerrainGrid.Line(P(40, 16), P(66, 16)).ToArray();
        var editWall = TerrainGrid.Line(P(66, 16), P(66, 25)).ToArray();
        var points = horizontal.Union(editWall).ToArray();
        var timings = new List<double>();
        for (int stroke = 0; stroke < 36; stroke++)
        {
            var watch = Stopwatch.StartNew();
            int changes = grid.Paint(points, stroke % 2 == 0 ? null : "brick");
            int length = grid.Bands().Sum(band => band.Length);
            int corners = grid.Corners().Count();
            int volume = grid.CollisionBoxes().Sum(box => box.Width * box.Height);
            watch.Stop();
            Check(changes == points.Length && length > 0 && corners > 0 && volume == grid.Count,
                "each measured stroke must solve real visual and physics geometry");
            if (stroke >= 6) timings.Add(watch.Elapsed.TotalMilliseconds);
        }
        timings.Sort();
        int middle = timings.Count / 2;
        double median = (timings[middle - 1] + timings[middle]) / 2;
        double p95 = timings[(int)Math.Ceiling(timings.Count * .95) - 1];
        Console.WriteLine(FormattableString.Invariant(
            $"128 × 64 grid, {points.Length} changed cells, core only, {timings.Count} strokes: median {median:F3} ms, p95 {p95:F3} ms."));
    }

    private static TerrainGrid Full(int minX, int minY, int maxX, int maxY) =>
        new TerrainGrid(TerrainGrid.Rectangle(P(minX, minY), P(maxX, maxY)).Select(point => new TerrainTile(point, "brick")));

    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
}
