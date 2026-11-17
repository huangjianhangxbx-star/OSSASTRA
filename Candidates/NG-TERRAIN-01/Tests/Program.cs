using Ossastra.SideView.TerrainLab;

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"expected {expected}, got {actual}");
}

var grid = new NgTerrainGrid();
var floor = Enumerable.Range(0, 7).Select(x => new TerrainCell(x, 0));
grid.Paint(floor, 0);
Equal(new TerrainBox(0, 0, 7, 1), grid.CollisionBoxes().Single());
Equal(TerrainNeighbors.East, grid.Neighbors(new TerrainCell(0, 0)));
grid.Paint(new[] { new TerrainCell(7, 0) }, 1);
Equal(new TerrainBox(0, 0, 8, 1), grid.CollisionBoxes().Single());
Equal(TerrainNeighbors.East | TerrainNeighbors.West, grid.Neighbors(new TerrainCell(6, 0)));
Equal(TerrainNeighbors.West, grid.Neighbors(new TerrainCell(7, 0)));

var fill = Enumerable.Range(0, 5).SelectMany(y => Enumerable.Range(0, 5).Select(x => new TerrainCell(x, y)));
grid = new NgTerrainGrid();
grid.Paint(fill, 0);
Equal(new TerrainBox(0, 0, 5, 5), grid.CollisionBoxes().Single());
var hole = new TerrainCell(2, 2);
var firstVariant = NgTerrainGrid.Variant(new TerrainCell(0, 0), 0, 1024);
Equal(1, grid.Paint(new[] { hole, hole }, NgTerrainGrid.Erase));
Equal(0, grid.Paint(new[] { hole }, NgTerrainGrid.Erase));
Equal(false, grid.CollisionBoxes().Any(box => box.Contains(hole)));
Equal(24, grid.CollisionBoxes().Sum(box => box.Width * box.Height));
Equal(TerrainNeighbors.All ^ TerrainNeighbors.NorthEast, grid.Neighbors(new TerrainCell(1, 1)));
grid.Paint(new[] { hole }, 0);
Equal(new TerrainBox(0, 0, 5, 5), grid.CollisionBoxes().Single());
Equal(TerrainNeighbors.All, grid.Neighbors(new TerrainCell(1, 1)));
Equal(firstVariant, NgTerrainGrid.Variant(new TerrainCell(0, 0), 0, 1024));
Equal(new TerrainCell(-3, 2), NgTerrainGrid.Stroke(new TerrainCell(1, 1), new TerrainCell(-3, 2)).Last());
Equal(5, NgTerrainGrid.Stroke(new TerrainCell(1, 1), new TerrainCell(-3, 2)).Count());
Console.WriteLine("PASS platform width, connected surfaces, closed hole, stable variants, continuous reverse stroke");
