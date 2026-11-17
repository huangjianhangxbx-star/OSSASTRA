using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.WorldRendering.SideView;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ossastra.SideView.TerrainLab
{
    public static class NgTerrainAuthoring
    {
        private const string Output = "Terrain Lab Output";

        public static int Paint(NgTerrainLayout layout, IEnumerable<TerrainCell> points, int material)
        {
            CheckEdit(layout);
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Terrain Lab Stroke");
            try
            {
                Undo.RecordObject(layout, "Terrain Lab Stroke");
                int count = layout.Paint(points, material);
                if (count != 0) Bake(layout);
                Undo.CollapseUndoOperations(undo);
                return count;
            }
            catch
            {
                Undo.RevertAllDownToGroup(undo);
                throw;
            }
        }

        public static int Bake(NgTerrainLayout layout)
        {
            CheckEdit(layout);
            var catalogs = layout.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SideViewComparisonCardCatalog>(false)).ToArray();
            if (catalogs.Length != 1)
                throw new InvalidOperationException("Terrain Lab needs one scene-local render card catalog.");
            var catalog = catalogs[0];
            var prior = layout.transform.Find(Output);
            var cards = catalog.Entries.Where(entry => entry.Renderer != null &&
                (prior == null || !entry.Renderer.transform.IsChildOf(prior)))
                .ToList();
            var map = layout.Read();
            var batches = new Dictionary<(Material material, string surface, int order, float depth), List<NgTerrainPatch.Quad>>();
            var missing = new HashSet<TerrainCell>();
            float size = layout.CellSize;
            foreach (var (cell, styleIndex) in map.Cells)
            {
                if (styleIndex < 0 || styleIndex >= layout.Styles.Count || layout.Styles[styleIndex] == null)
                {
                    missing.Add(cell);
                    continue;
                }
                var style = layout.Styles[styleIndex];
                var neighbors = map.Neighbors(cell);
                foreach (var layer in style.layers)
                {
                    if (layer == null) continue;
                    var sample = layer.Choose(cell, styleIndex, neighbors);
                    if (sample == null || !sample.TryGetUv(out var uv))
                    {
                        if (layer.required || sample != null) missing.Add(cell);
                        continue;
                    }
                    var key = (sample.material, layer.surfaceKind, layer.sortingOrder, layer.depth);
                    if (!batches.TryGetValue(key, out var quads)) batches.Add(key, quads = new List<NgTerrainPatch.Quad>());
                    var frame = sample.destinationCells;
                    var bounds = new Rect((cell.x + frame.x) * size, (cell.y + frame.y) * size,
                        frame.width * size, frame.height * size);
                    quads.Add(new NgTerrainPatch.Quad(bounds, uv));
                }
            }

            var output = AddChild(layout.transform, Output);
            try
            {
                foreach (var (key, quads) in batches)
                {
                    var child = AddChild(output, key.surface);
                    child.localPosition = Vector3.forward * key.depth;
                    var patch = child.gameObject.AddComponent<NgTerrainPatch>();
                    patch.SetQuads(quads.ToArray());
                    var renderer = child.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = key.material;
                    renderer.sortingOrder = key.order;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    cards.Add(new SideViewComparisonCardCatalog.Entry(renderer, key.material, true, false, false, true,
                        0f, 1f, .22f, Vector4.one, key.material.shader.name, key.surface));
                }
                var physics = AddChild(output, "Terrain Lab Collision");
                foreach (var box in map.CollisionBoxes())
                {
                    var block = AddChild(physics, $"Cells {box}");
                    var collider = block.gameObject.AddComponent<BoxCollider>();
                    collider.center = new Vector3((box.X + box.Width * .5f) * size, (box.Y + box.Height * .5f) * size, 0);
                    collider.size = new Vector3(box.Width * size, box.Height * size, layout.CollisionDepth);
                }
                if (missing.Count > 0)
                {
                    var warning = AddChild(output, "Unmapped Terrain Cells").gameObject.AddComponent<NgTerrainWarnings>();
                    warning.SetCells(missing.ToArray(), size);
                }
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(output.gameObject);
                throw;
            }
            Undo.RegisterCreatedObjectUndo(output.gameObject, "Rebuild Terrain Lab");
            if (prior != null) Undo.DestroyObjectImmediate(prior.gameObject);
            Undo.RecordObject(catalog, "Bind Terrain Lab Rendering");
            catalog.SetAuthoringEntries(cards.ToArray());
            EditorUtility.SetDirty(catalog);
            EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            return missing.Count;
        }

        private static Transform AddChild(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static void CheckEdit(NgTerrainLayout layout)
        {
            if (layout == null || EditorApplication.isPlayingOrWillChangePlaymode || !layout.gameObject.scene.IsValid() ||
                !layout.gameObject.scene.path.Contains("/PrivateValidation/NG-TERRAIN-01/"))
                throw new InvalidOperationException("Terrain authoring is limited to the private Terrain Lab in Edit mode.");
        }
    }
}
