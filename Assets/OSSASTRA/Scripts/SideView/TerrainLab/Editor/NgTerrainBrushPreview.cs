using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ossastra.SideView.TerrainLab
{
    internal static class NgTerrainBrushPreview
    {
        private const float CellPixels = 24;

        public static void Draw(SceneView scene, NgTerrainLayout layout, HashSet<TerrainCell> stroke, TerrainCell anchor, int material)
        {
            if (stroke.Count == 0 || material >= 0 && (material >= layout.Styles.Count || layout.Styles[material] == null)) return;
            var grid = layout.Read();
            grid.Paint(stroke, material);
            var region = PreviewRegion(stroke, anchor);
            var visible = new List<(NgTerrainStyle.Sample sample, Rect uv, Rect bounds, int order)>();
            var missing = new HashSet<TerrainCell>();
            foreach (var (cell, style) in grid.Cells)
            {
                if (!region.Contains(new Vector2Int(cell.x, cell.y))) continue;
                if (style < 0 || style >= layout.Styles.Count || layout.Styles[style] == null)
                {
                    missing.Add(cell);
                    continue;
                }
                var neighbors = grid.Neighbors(cell);
                foreach (var layer in layout.Styles[style].layers)
                {
                    if (layer == null) continue;
                    var sample = layer.Choose(cell, style, neighbors);
                    if (sample != null && sample.TryGetUv(out var uv))
                    {
                        var frame = sample.destinationCells;
                        var rect = CellRect(region, cell.x + frame.x, cell.y + frame.y, frame.width, frame.height);
                        visible.Add((sample, uv, rect, layer.sortingOrder));
                    }
                    else if (layer.required || sample != null) missing.Add(cell);
                }
            }

            Handles.BeginGUI();
            try
            {
                float height = region.height * CellPixels;
                var panel = new Rect(14, Mathf.Max(60, scene.position.height - height - 64), region.width * CellPixels, height);
                EditorGUI.DrawRect(new Rect(panel.x, panel.y - 23, panel.width, 23), new Color(.11f, .14f, .16f));
                GUI.Label(new Rect(panel.x + 5, panel.y - 22, Mathf.Max(130, panel.width), 22), "笔画预览 · 素材色");
                GUI.BeginGroup(panel);
                try
                {
                    EditorGUI.DrawRect(new Rect(0, 0, panel.width, panel.height), new Color(.18f, .20f, .22f));
                    var original = GUI.color;
                    foreach (var tile in visible.OrderBy(tile => tile.order))
                    {
                        GUI.color = tile.sample.material.HasProperty("_Color") ? tile.sample.material.color : Color.white;
                        GUI.DrawTextureWithTexCoords(tile.bounds, tile.sample.material.mainTexture, tile.uv, true);
                    }
                    GUI.color = original;
                    foreach (var cell in stroke)
                    {
                        if (region.Contains(new Vector2Int(cell.x, cell.y)))
                            Outline(CellRect(region, cell.x, cell.y), material == NgTerrainGrid.Erase ? new Color(1, .4f, .3f) : new Color(.50f, .91f, .69f));
                    }
                    foreach (var cell in missing) Outline(CellRect(region, cell.x, cell.y), Color.red);
                }
                finally { GUI.EndGroup(); }
            }
            finally { Handles.EndGUI(); }
        }

        private static RectInt PreviewRegion(HashSet<TerrainCell> cells, TerrainCell anchor)
        {
            int left = cells.Min(cell => cell.x) - 2, right = cells.Max(cell => cell.x) + 2;
            int bottom = cells.Min(cell => cell.y) - 2, top = cells.Max(cell => cell.y) + 2;
            int width = Mathf.Min(18, right - left + 1), height = Mathf.Min(10, top - bottom + 1);
            return new RectInt(Mathf.Clamp(anchor.x - width / 2, left, right - width + 1),
                Mathf.Clamp(anchor.y - height / 2, bottom, top - height + 1), width, height);
        }

        private static Rect CellRect(RectInt region, float x, float y, float width = 1, float height = 1) =>
            new Rect((x - region.xMin) * CellPixels, (region.yMax - y - height) * CellPixels, width * CellPixels, height * CellPixels);

        private static void Outline(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), color);
        }
    }
}
