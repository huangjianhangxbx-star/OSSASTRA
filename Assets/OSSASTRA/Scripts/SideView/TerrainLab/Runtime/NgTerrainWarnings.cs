using System;
using UnityEngine;

namespace Ossastra.SideView.TerrainLab
{
    public sealed class NgTerrainWarnings : MonoBehaviour
    {
        [SerializeField] private TerrainCell[] cells = Array.Empty<TerrainCell>();
        [SerializeField] private float cellSize;

        public void SetCells(TerrainCell[] value, float size) => (cells, cellSize) = (value, size);

        private void OnDrawGizmos()
        {
            var oldMatrix = Gizmos.matrix;
            var oldColor = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.red;
            foreach (var cell in cells)
                Gizmos.DrawWireCube(new Vector3((cell.x + .5f) * cellSize, (cell.y + .5f) * cellSize),
                    new Vector3(cellSize * .92f, cellSize * .92f, .01f));
            Gizmos.matrix = oldMatrix;
            Gizmos.color = oldColor;
        }
    }
}
