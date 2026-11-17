using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ossastra.SideView.TerrainLab
{
    [DisallowMultipleComponent]
    public sealed class NgTerrainLayout : MonoBehaviour
    {
        [Serializable]
        private struct PlacedCell
        {
            public TerrainCell position;
            public int material;
        }

        [SerializeField] private List<PlacedCell> cells = new List<PlacedCell>();
        [SerializeField] private NgTerrainStyle[] styles = Array.Empty<NgTerrainStyle>();
        [SerializeField, Min(.125f)] private float cellSize = 1;
        [SerializeField, Min(.2f)] private float collisionDepth = 3;

        public IReadOnlyList<NgTerrainStyle> Styles => styles;
        public float CellSize => cellSize;
        public float CollisionDepth => collisionDepth;

        public NgTerrainGrid Read()
        {
            var map = new NgTerrainGrid();
            foreach (var group in cells.GroupBy(cell => cell.material))
                map.Paint(group.Select(cell => cell.position), group.Key);
            return map;
        }

        public int Paint(IEnumerable<TerrainCell> points, int material)
        {
            if (material < NgTerrainGrid.Erase || material >= styles.Length || material >= 0 && styles[material] == null)
                throw new ArgumentOutOfRangeException(nameof(material));
            var map = Read();
            int changed = map.Paint(points, material);
            if (changed != 0)
                cells = map.Cells.Select(pair => new PlacedCell { position = pair.Key, material = pair.Value }).ToList();
            return changed;
        }

        public TerrainCell CellAt(Vector3 worldPoint)
        {
            var local = transform.InverseTransformPoint(worldPoint) / cellSize;
            return new TerrainCell(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));
        }
    }
}
