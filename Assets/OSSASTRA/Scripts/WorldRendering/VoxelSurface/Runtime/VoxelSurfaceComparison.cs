/* 功能：只对显式登记Renderer逐组切换原版/校准/轻分面作者材质。
 * 所有权：作者材质只读；仅需要Profile覆盖时缓存私有副本。只在切档/显式刷新时写sharedMaterials。
 * 归还：外部改写即逐binding让权；禁用/销毁只恢复仍完全属于自身的数组，不接管外部材质。
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ossastra.WorldRendering.VoxelSurface
{
    public enum VoxelSurfaceComparisonMode { Original, Calibrated, Faceted }

    [DisallowMultipleComponent]
    public sealed class VoxelSurfaceComparison : MonoBehaviour
    {
        [Serializable]
        public sealed class Binding
        {
            [Tooltip("明确选择的三维Renderer；不会扫描子物体或全场替换。")]
            public Renderer renderer;
            [Tooltip("原始完整材质槽；也是本组件唯一允许归还的作者快照。")]
            public Material[] original = Array.Empty<Material>();
            [Tooltip("校准PBR作者材质，槽数必须与原版相同。资源保持只读。")]
            public Material[] calibrated = Array.Empty<Material>();
            [Tooltip("轻分面作者材质，槽数必须与原版相同。资源保持只读。")]
            public Material[] shaped = Array.Empty<Material>();
        }

        [Tooltip("仅这些Renderer参加比较；每个Renderer最多登记一次。")]
        public Binding[] bindings = Array.Empty<Binding>();
        [Tooltip("B档参数覆盖；为空时直接使用校准作者材质，不创建副本。")]
        public VoxelSurfaceProfileSO calibratedProfile;
        [Tooltip("C档参数覆盖；为空时直接使用轻分面作者材质，不创建副本。")]
        public VoxelSurfaceProfileSO shapedProfile;
        [Tooltip("进入Play自动应用；编辑阶段仅保存选择，不自动更换场景材质。")]
        public VoxelSurfaceComparisonMode mode;

        public string Status { get; private set; } = "等待显式体素材质登记";

        sealed class BindingState
        {
            public Binding Binding;
            public Renderer Renderer;
            public Material[] Original;
            public Material[] Assigned;
            public bool Pinned;
        }

        readonly List<BindingState> states = new List<BindingState>();
        readonly HashSet<Renderer> observedRenderers = new HashSet<Renderer>();
        readonly Dictionary<(Material Source, VoxelSurfaceProfileSO Profile), Material> cache =
            new Dictionary<(Material, VoxelSurfaceProfileSO), Material>();
        int handedOffTotal;

        void OnEnable()
        {
            if (Application.isPlaying) ApplyMode(mode);
        }

        void OnDisable() { RestoreOriginal(); }
        void OnDestroy() { RestoreOriginal(); }

        public void ApplyMode(VoxelSurfaceComparisonMode value)
        {
            mode = Enum.IsDefined(typeof(VoxelSurfaceComparisonMode), value) ? value : VoxelSurfaceComparisonMode.Original;
            SynchronizeBindings();
            int applied = 0, pinned = 0, rejected = 0;
            foreach (var state in states)
                if (state.Renderer != null && !state.Pinned && !Same(state.Renderer.sharedMaterials, state.Assigned ?? state.Original)) state.Pinned = true;
            int handedOff = HandOffExternalClones();
            var refreshed = new HashSet<Material>();
            foreach (var state in states)
            {
                if (state.Renderer == null) continue;
                if (state.Pinned) { ++pinned; continue; }
                Material[] sources = mode == VoxelSurfaceComparisonMode.Original ? state.Original :
                    mode == VoxelSurfaceComparisonMode.Calibrated ? state.Binding.calibrated : state.Binding.shaped;
                var profile = mode == VoxelSurfaceComparisonMode.Calibrated ? calibratedProfile :
                    mode == VoxelSurfaceComparisonMode.Faceted ? shapedProfile : null;
                if (!CanUse(sources, state.Original, profile)) { ++rejected; continue; }
                var target = new Material[sources.Length];
                for (int slot = 0; slot < sources.Length; ++slot)
                    target[slot] = profile == null || sources[slot] == null ? sources[slot] : GetMaterial(sources[slot], profile, refreshed);
                if (!Same(state.Renderer.sharedMaterials, target)) state.Renderer.sharedMaterials = target;
                state.Assigned = target;
                ++applied;
            }
            handedOff += PruneCache();
            handedOffTotal += handedOff;
            Status = "体素" + ModeLabel(mode) + "；已应用 " + applied + "；外部改写让权 " + pinned + "；配置拒绝 " + rejected;
            if (handedOffTotal > 0) Status += "；ownership handed-off 累计 " + handedOffTotal + "（不再缓存）";
        }

        public void RefreshParameters()
        {
            ApplyMode(mode);
        }

        public void RestoreOriginal()
        {
            if (states.Count == 0 && cache.Count == 0) return;
            int restored = 0, pinned = 0, handedOff = 0;
            foreach (var state in states)
            {
                if (state.Renderer == null || state.Assigned == null) continue;
                if (state.Pinned) { ++pinned; continue; }
                if (Same(state.Renderer.sharedMaterials, state.Assigned))
                {
                    if (!Same(state.Assigned, state.Original)) state.Renderer.sharedMaterials = state.Original;
                    ++restored;
                }
                else ++pinned;
            }
            foreach (var material in cache.Values)
            {
                if (StillReferenced(material)) ++handedOff;
                else DestroyOwned(material);
            }
            cache.Clear(); states.Clear(); observedRenderers.Clear();
            handedOffTotal += handedOff;
            Status = "已归还原材质 " + restored + "；保留外部改写 " + pinned;
            if (handedOffTotal > 0) Status += "；ownership handed-off 累计 " + handedOffTotal + "（外部仍使用，已移交且不再缓存）";
        }

        void SynchronizeBindings()
        {
            var next = new List<BindingState>();
            var unique = new HashSet<Renderer>();
            if (bindings != null) foreach (var binding in bindings)
            {
                if (binding == null || binding.renderer == null || binding.original == null || binding.original.Length == 0 || !unique.Add(binding.renderer)) continue;
                observedRenderers.Add(binding.renderer);
                BindingState found = null;
                foreach (var state in states)
                    if (ReferenceEquals(state.Binding, binding) && state.Renderer == binding.renderer && Same(state.Original, binding.original)) { found = state; break; }
                if (found == null) found = new BindingState { Binding = binding, Renderer = binding.renderer, Original = (Material[])binding.original.Clone() };
                next.Add(found);
            }
            foreach (var state in states)
            {
                if (next.Contains(state) || state.Renderer == null || state.Assigned == null || state.Pinned) continue;
                if (Same(state.Renderer.sharedMaterials, state.Assigned)) state.Renderer.sharedMaterials = state.Original;
            }
            states.Clear(); states.AddRange(next);
        }

        Material GetMaterial(Material source, VoxelSurfaceProfileSO profile, HashSet<Material> refreshed)
        {
            var key = (source, profile);
            if (!cache.TryGetValue(key, out var material) || material == null)
            {
                material = new Material(source) { name = source.name + " [VoxelSurface private]", hideFlags = HideFlags.HideAndDontSave };
                cache[key] = material;
            }
            if (refreshed.Add(material))
            {
                material.shader = source.shader;
                material.CopyPropertiesFromMaterial(source);
                profile.settings.ApplyTo(material);
            }
            return material;
        }

        int PruneCache()
        {
            var remove = new List<(Material Source, VoxelSurfaceProfileSO Profile)>();
            int handedOff = 0;
            foreach (var item in cache)
            {
                bool retained = false;
                foreach (var state in states)
                {
                    if (item.Key.Profile == calibratedProfile && Contains(state.Binding.calibrated, item.Key.Source) ||
                        item.Key.Profile == shapedProfile && Contains(state.Binding.shaped, item.Key.Source)) { retained = true; break; }
                }
                if (retained) continue;
                if (StillReferenced(item.Value)) ++handedOff;
                else DestroyOwned(item.Value);
                remove.Add(item.Key);
            }
            foreach (var key in remove) cache.Remove(key);
            return handedOff;
        }

        int HandOffExternalClones()
        {
            var remove = new List<(Material Source, VoxelSurfaceProfileSO Profile)>();
            foreach (var item in cache)
                foreach (var renderer in observedRenderers)
                {
                    if (renderer == null) continue;
                    var current = renderer.sharedMaterials;
                    if (!Contains(current, item.Value)) continue;
                    bool stillOwned = false;
                    foreach (var state in states)
                        if (state.Renderer == renderer && !state.Pinned && state.Assigned != null && Same(current, state.Assigned))
                        { stillOwned = true; break; }
                    if (!stillOwned) { remove.Add(item.Key); break; }
                }
            // 外部仍使用的私有材质移交后不再刷新；其他正常binding按作者源重建自己的副本。
            foreach (var key in remove) cache.Remove(key);
            return remove.Count;
        }

        bool StillReferenced(Material material)
        {
            if (material == null) return false;
            foreach (var renderer in observedRenderers)
                if (renderer != null && Contains(renderer.sharedMaterials, material)) return true;
            return false;
        }

        static bool CanUse(Material[] materials, Material[] original, VoxelSurfaceProfileSO profile)
        {
            if (materials == null || materials.Length != original.Length || profile != null && profile.settings == null) return false;
            for (int slot = 0; slot < materials.Length; ++slot)
                if (original[slot] != null && materials[slot] == null) return false;
            if (profile != null) foreach (var material in materials)
                if (material != null && (!material.HasProperty("_VoxelMainShape") || !material.HasProperty("_VoxelIndirectDiffuse") ||
                    !material.HasProperty("_VoxelEnvironmentSpecular") || !material.HasProperty("_VoxelDirectSpecular") ||
                    !material.HasProperty("_VoxelHighlightStrength") || !material.HasProperty("_VoxelHighlightKnee") ||
                    !material.HasProperty("_VoxelHighlightCeiling") || !material.HasProperty("_VoxelDiagnostic") || !material.HasProperty("_VoxelVertexColors"))) return false;
            return true;
        }

        static bool Same(Material[] left, Material[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; ++i) if (left[i] != right[i]) return false;
            return true;
        }

        static bool Contains(Material[] materials, Material value)
        {
            if (materials != null) foreach (var material in materials) if (material == value) return true;
            return false;
        }

        static void DestroyOwned(Material material)
        {
            if (material == null) return;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }

        static string ModeLabel(VoxelSurfaceComparisonMode value)
        {
            return value == VoxelSurfaceComparisonMode.Calibrated ? "B 校准PBR" : value == VoxelSurfaceComparisonMode.Faceted ? "C 轻分面" : "A 原版";
        }
    }
}
