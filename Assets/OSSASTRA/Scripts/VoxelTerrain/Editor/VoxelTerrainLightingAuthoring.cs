/* 功能：将明确体素安装根的三维表面一次性登记到既有Comparison统一受光。
 * 边界：仅Editor同场景作者接线；不扫描全场、不建材质/管理器、不保存资产或改变当前Look/参数。
 * 原引用：完整保留旧Binding对象及顺序，新组只跟受光；A/B/C源均保存当前完整持久材质槽。
 * 失败：先验证全部目标与已有接线，再一次性追加；私有材质、跨场景或冲突策略不静默接管。
 */
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Ossastra.WorldRendering.VoxelSurface;

namespace Ossastra.VoxelTerrain.Editor
{
    public static class VoxelTerrainLightingAuthoring
    {
        public static int Register(VoxelTerrainInstallation installation, VoxelSurfaceComparison comparison)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play after preserving the approved live parameters before author registration");
            if (installation == null || comparison == null) throw new ArgumentNullException(installation == null ? nameof(installation) : nameof(comparison));
            var scene = installation.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded || scene != comparison.gameObject.scene || EditorUtility.IsPersistent(installation) || EditorUtility.IsPersistent(comparison))
                throw new InvalidOperationException("Terrain installation and original Comparison must belong to the same loaded author scene");
            var targets = installation.GetComponentsInChildren<MeshRenderer>(true);
            if (targets.Length == 0) throw new InvalidOperationException("Explicit terrain installation contains no mesh surfaces");
            var original = comparison.bindings ?? Array.Empty<VoxelSurfaceComparison.Binding>();
            var existing = new Dictionary<Renderer, VoxelSurfaceComparison.Binding>();
            foreach (var binding in original)
            {
                if (binding == null || binding.renderer == null || existing.ContainsKey(binding.renderer))
                    throw new InvalidOperationException("Existing Comparison contains missing or duplicate Renderer bindings; author must resolve it first");
                existing.Add(binding.renderer, binding);
            }
            var additions = new List<VoxelSurfaceComparison.Binding>();
            foreach (var renderer in targets)
            {
                if (renderer.gameObject.scene != scene || !renderer.transform.IsChildOf(installation.transform))
                    throw new InvalidOperationException("Renderer escaped the explicit terrain installation root");
                var filter = renderer.GetComponent<MeshFilter>();
                var materials = renderer.sharedMaterials;
                if (filter == null || filter.sharedMesh == null || materials == null || materials.Length == 0 || materials.Any(m => m == null || !EditorUtility.IsPersistent(m) || m.shader == null || m.shader.name != "Ossastra/WorldRendering/VoxelLit"))
                    throw new InvalidOperationException("Every terrain slot must be a persistent VoxelLit author material: " + renderer.name);
                if (existing.TryGetValue(renderer, out var binding))
                {
                    if (!binding.lightingOnly || !Same(binding.original, materials) || !Same(binding.calibrated, materials) || !Same(binding.shaped, materials))
                        throw new InvalidOperationException("Existing terrain binding conflicts with lighting-only ownership or complete original slots: " + renderer.name);
                    continue;
                }
                additions.Add(new VoxelSurfaceComparison.Binding {
                    renderer = renderer, lightingOnly = true,
                    original = (Material[])materials.Clone(), calibrated = (Material[])materials.Clone(), shaped = (Material[])materials.Clone()
                });
            }
            if (additions.Count == 0) return 0;
            Undo.RecordObject(comparison, "Register explicit voxel terrain lighting surfaces");
            comparison.bindings = original.Concat(additions).ToArray();
            if (PrefabUtility.IsPartOfPrefabInstance(comparison)) PrefabUtility.RecordPrefabInstancePropertyModifications(comparison);
            EditorUtility.SetDirty(comparison); EditorSceneManager.MarkSceneDirty(scene);
            return additions.Count;
        }
        static bool Same(Material[] left, Material[] right) { return left != null && right != null && left.SequenceEqual(right); }
    }
}
#endif
