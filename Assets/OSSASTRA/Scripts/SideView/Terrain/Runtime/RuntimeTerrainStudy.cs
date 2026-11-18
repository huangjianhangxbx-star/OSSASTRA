/* 编辑器试玩用地形实验。以原工坊的占位和解算生成临时画片与 3D 碰撞，作者地图始终只读。 */
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.SideView.TerrainLab;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ossastra.SideView.TerrainWorkshop
{
    [DisallowMultipleComponent]
    public sealed class RuntimeTerrainStudy : MonoBehaviour
    {
        private const string BakedOutputName = "地形工坊 · 派生输出";
        public TerrainWorkshopRoot workshop;
        public Vector2Int gateMin = new Vector2Int(12, -1);
        public Vector2Int gateMax = new Vector2Int(14, 0);
        public bool showControls = true;

        private TerrainGrid grid;
        private TerrainTile[] initialTiles;
        private TerrainTile[] gate;
        private readonly List<(MeshRenderer renderer, bool wasHidden)> bakedCards =
            new List<(MeshRenderer, bool)>();
        private readonly List<(BoxCollider collider, bool wasEnabled)> bakedColliders =
            new List<(BoxCollider, bool)>();
        private GameObject output;
        private readonly List<Material> generatedMaterials = new List<Material>();

        public TerrainSolution CurrentSolution { get; private set; }
        public Transform CurrentOutput => output == null ? null : output.transform;
        public string Status { get; private set; } = "等待进入 Play";
        public int CellCount => grid?.Count ?? 0;

        private void Start()
        {
            if (Prepare() && bakedColliders.Count == 0) Commit(grid);
        }

        public bool Occupied(TerrainPoint point) => grid != null && grid.TryGetKit(point, out _);

        public bool Dig()
        {
            if (!Prepare()) return false;
            var draft = new TerrainGrid(grid.Tiles);
            if (draft.Paint(gate.Select(tile => tile.point), null) == 0) return false;
            return Commit(draft);
        }

        public bool Refill()
        {
            if (!Prepare()) return false;
            var missing = gate.Where(tile => !grid.TryGetKit(tile.point, out _)).ToArray();
            if (missing.Length == 0) return false;
            if (OverlapsCharacter(missing))
            {
                Status = "缺口中有角色，请先离开再回填。";
                return false;
            }
            var draft = new TerrainGrid(grid.Tiles);
            foreach (var group in missing.GroupBy(tile => tile.kit))
                draft.Paint(group.Select(tile => tile.point), group.Key);
            return Commit(draft);
        }

        public bool ResetTerrain()
        {
            if (!Prepare() || output == null) return false;
            var missing = gate.Where(tile => !grid.TryGetKit(tile.point, out _)).ToArray();
            if (OverlapsCharacter(missing))
            {
                Status = "缺口中有角色，请先离开再恢复。";
                return false;
            }
            var original = new TerrainGrid(initialTiles);
            if (bakedColliders.Count == 0) return Commit(original);

            var solution = TerrainAssembly.Solve(workshop.map, original);
            ReleaseOutput();
            SetBakedEnabled(true);
            grid = original;
            CurrentSolution = solution;
            Physics.SyncTransforms();
            Report();
            return true;
        }

        private bool Prepare()
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return false;
            if (grid != null) return true;
            if (workshop == null) workshop = GetComponent<TerrainWorkshopRoot>();
            if (workshop == null || workshop.map == null)
            {
                Status = "缺少工坊地图。";
                return false;
            }

            initialTiles = workshop.map.Read().Tiles.ToArray();
            gate = initialTiles.Where(tile =>
                tile.point.x >= gateMin.x && tile.point.x <= gateMax.x &&
                tile.point.y >= gateMin.y && tile.point.y <= gateMax.y).ToArray();
            if (gate.Length == 0)
            {
                Status = "试验缺口内没有原始实体格。";
                return false;
            }
            grid = new TerrainGrid(initialTiles);
            CurrentSolution = TerrainAssembly.Solve(workshop.map, grid);

            var baked = workshop.transform.Find(BakedOutputName);
            if (baked != null)
            {
                foreach (Transform child in baked)
                {
                    if (child.name.StartsWith("碰撞 ", StringComparison.Ordinal))
                        bakedColliders.AddRange(child.GetComponentsInChildren<BoxCollider>(true)
                            .Select(collider => (collider, collider.enabled)));
                    else if (IsTerrainCard(child.name) && child.TryGetComponent(out MeshRenderer renderer))
                        bakedCards.Add((renderer, renderer.forceRenderingOff));
                }
            }
            Report();
            return true;
        }

        private bool Commit(TerrainGrid draft)
        {
            var map = workshop.map;
            var materials = new List<Material>();
            var staged = new GameObject("运行时地形");
            staged.SetActive(false);
            staged.transform.SetParent(workshop.transform, false);
            try
            {
                var solution = TerrainAssembly.Solve(map, draft);
                var parts = solution.Parts.Where(part => part.Layer == TerrainLayerKind.Body ||
                    part.Layer == TerrainLayerKind.Border || part.Layer == TerrainLayerKind.Decor);
                foreach (var group in parts.GroupBy(part => (layer: part.Layer, order: SortOrder(map, part),
                    texture: part.Art == null ? null : part.Art.Image, tint: part.Tint)))
                {
                    var child = AddChild(staged.transform, group.Key.layer.ToString());
                    var patch = child.AddComponent<NgTerrainPatch>();
                    patch.SetQuads(group.Select(part =>
                        new NgTerrainPatch.Quad(part.A, part.B, part.C, part.D, part.FlipX, part.FlipY) { uv = part.UV }).ToArray());
                    var renderer = child.GetComponent<MeshRenderer>();
                    var material = CreateSurface(group.Key.texture, group.Key.tint);
                    materials.Add(material);
                    renderer.sharedMaterial = material;
                    renderer.sortingOrder = group.Key.order;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                var collision = AddChild(staged.transform, "3D 碰撞");
                float cell = Mathf.Max(.05f, map.cellSize);
                float depth = Mathf.Max(.2f, map.collisionDepth);
                foreach (var box in draft.CollisionBoxes())
                {
                    var collider = collision.AddComponent<BoxCollider>();
                    collider.center = new Vector3((box.X + box.Width * .5f) * cell,
                        (box.Y + box.Height * .5f) * cell, 0);
                    collider.size = new Vector3(box.Width * cell, box.Height * cell, depth);
                }
                staged.SetActive(true);
                if (staged.GetComponentsInChildren<MeshFilter>().Any(filter => filter.sharedMesh == null))
                    throw new InvalidOperationException("地形画片网格未生成。");

                SetBakedEnabled(false);
                ReleaseOutput();
                output = staged;
                generatedMaterials.AddRange(materials);
                grid = draft;
                CurrentSolution = solution;
                Physics.SyncTransforms();
                Report();
                return true;
            }
            catch (Exception error)
            {
                staged.SetActive(false);
                Destroy(staged);
                foreach (var material in materials) Destroy(material);
                Status = "本次编辑未提交。 " + error.Message;
                return false;
            }
        }

        private bool OverlapsCharacter(TerrainTile[] cells)
        {
            var root = workshop.transform;
            float cell = Mathf.Max(.05f, workshop.map.cellSize);
            float depth = Mathf.Max(.2f, workshop.map.collisionDepth);
            var scale = root.lossyScale;
            var half = Vector3.Scale(new Vector3(cell * .47f, cell * .47f, depth * .49f),
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            Physics.SyncTransforms();
            return cells.Any(tile => Physics.OverlapBox(root.TransformPoint((tile.point.x + .5f) * cell,
                (tile.point.y + .5f) * cell, 0), half, root.rotation, Physics.AllLayers, QueryTriggerInteraction.Ignore)
                .Any(hit => hit is CharacterController));
        }

        private Material CreateSurface(Texture2D texture, Color tint)
        {
            var template = workshop.cardMaterial;
            var shader = template != null ? template.shader : Shader.Find("Sprites/Default");
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException("没有可用的地形画片材质。");
            var material = template != null ? new Material(template) : new Material(shader);
            if (!material.HasProperty("_MainTex") || !material.HasProperty("_Color"))
            {
                Destroy(material);
                throw new InvalidOperationException("画片材质缺少 MainTex 或 Color。");
            }
            material.hideFlags = HideFlags.DontSave;
            material.SetTexture("_MainTex", texture == null ? Texture2D.whiteTexture : texture);
            material.SetColor("_Color", tint);
            if (material.HasProperty("_RendererColor")) material.SetColor("_RendererColor", Color.white);
            return material;
        }

        private GameObject AddChild(Transform parent, string label)
        {
            var child = new GameObject(label) { layer = workshop.gameObject.layer };
            child.transform.SetParent(parent, false);
            return child;
        }

        private static bool IsTerrainCard(string label) =>
            label.StartsWith("画片 Body ", StringComparison.Ordinal) ||
            label.StartsWith("画片 Border ", StringComparison.Ordinal) ||
            label.StartsWith("画片 Decor ", StringComparison.Ordinal);

        private static int SortOrder(TerrainMap map, TerrainPart part)
        {
            int order = map.Layer(part.Layer)?.order ?? 0;
            return part.Slot == TerrainSlot.Convex || part.Slot == TerrainSlot.Concave ? order + 1 : order;
        }

        private void SetBakedEnabled(bool value)
        {
            foreach (var entry in bakedCards)
                if (entry.renderer != null) entry.renderer.forceRenderingOff = !value || entry.wasHidden;
            foreach (var entry in bakedColliders)
                if (entry.collider != null) entry.collider.enabled = value && entry.wasEnabled;
        }

        private void ReleaseOutput()
        {
            if (output != null)
            {
                output.SetActive(false);
                Destroy(output);
                output = null;
            }
            foreach (var material in generatedMaterials) Destroy(material);
            generatedMaterials.Clear();
        }

        private void Report()
        {
            int missing = gate.Count(tile => !Occupied(tile.point));
            Status = $"实体 {CellCount} 格，缺口挖除 {missing}/{gate.Length} 格";
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !showControls) return;
            GUILayout.BeginArea(new Rect(Mathf.Max(8, (Screen.width - 360) * .5f),
                Mathf.Max(8, Screen.height - 151), 360, 63), GUI.skin.box);
            GUILayout.Label(Status);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("挖开缺口")) Dig();
            if (GUILayout.Button("回填缺口")) Refill();
            if (GUILayout.Button("恢复初始")) ResetTerrain();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void OnDisable()
        {
            if (grid == null) return;
            ReleaseOutput();
            SetBakedEnabled(true);
            bakedCards.Clear();
            bakedColliders.Clear();
            grid = null;
            CurrentSolution = null;
        }
    }
}
