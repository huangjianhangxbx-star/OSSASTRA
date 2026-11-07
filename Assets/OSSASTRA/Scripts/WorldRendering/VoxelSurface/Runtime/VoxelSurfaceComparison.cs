/* 功能：只对显式登记Renderer逐组切换原版/校准/轻分面作者材质。
 * 所有权：作者材质/Profile只读；C以独立会话副本调参，缓存私有材质，脏修改最多每帧合并刷新一次。
 * 保存：仅发出显式Editor请求；候选落盘与本绑定组默认分开，不在运行代码保存资产或业务。
 * 归还：外部改写即逐binding让权；禁用/销毁只恢复仍完全属于自身的数组，不接管外部材质。
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ossastra.WorldRendering.VoxelSurface
{
    public enum VoxelSurfaceComparisonMode { Original, Calibrated, Faceted }

    [DisallowMultipleComponent]
    public sealed partial class VoxelSurfaceComparison : MonoBehaviour
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
            [Tooltip("共用B/C当前受光会话，但保留自身表面配色、纹理和顶点色用途；不参与旧模型的候选换色。")]
            public bool lightingOnly;
        }

        [Tooltip("仅这些Renderer参加比较；每个Renderer最多登记一次。")]
        public Binding[] bindings = Array.Empty<Binding>();
        [Tooltip("B档参数覆盖；为空时直接使用校准作者材质，不创建副本。")]
        public VoxelSurfaceProfileSO calibratedProfile;
        [Tooltip("C档参数覆盖；为空时直接使用轻分面作者材质，不创建副本。")]
        public VoxelSurfaceProfileSO shapedProfile;
        [Tooltip("原C0独立作者基线。复位从此读取，不覆盖候选或退回A。")]
        public VoxelSurfaceProfileSO shapedBaselineProfile;
        [Tooltip("显式保存候选的独立目标；不能指向C0或B。为空时Editor可选择一个新资产。")]
        public VoxelSurfaceProfileSO saveCandidateProfile;
        [Tooltip("唯一接受体素灯光身份上下文的相机；不自动查找相机。")]
        public Camera lightContextCamera;
        [Tooltip("主方向光身份；GPU主光必须与此实际实例一致才允许独立背光调节。")]
        public Light keyLight;
        [Tooltip("明确允许压暗其漫反射的方向补光；其他方向光和局部灯不自动归类。")]
        public Light directionalFill;
        [Tooltip("进入Play自动应用；编辑阶段仅保存选择，不自动更换场景材质。")]
        public VoxelSurfaceComparisonMode mode;

        public string Status { get; private set; } = "等待显式体素材质登记";
        public string SaveStatus { get; private set; } = "尚未显式保存候选";
        public string LastSavePath { get; private set; } = "";
        public VoxelSurfaceProfileSO LastSavedProfile { get; private set; }
        public int BindingCount
        {
            get { var unique = new HashSet<Renderer>(); if (bindings != null) foreach (var binding in bindings) if (binding != null && binding.renderer != null) unique.Add(binding.renderer); return unique.Count; }
        }
        public VoxelSurfaceProfileSO SessionSourceProfile { get { EnsureSession(); return sessionSourceProfile; } }
        public int LightingOnlyBindingCount
        {
            get { var unique = new HashSet<Renderer>(); if (bindings != null) foreach (var b in bindings) if (b != null && b.renderer != null && b.lightingOnly) unique.Add(b.renderer); return unique.Count; }
        }
        // UI只能拿到快照；所有修改走受控入口，避免外部改了对象却没有刷新。
        public VoxelSurfaceSettings SessionSettings { get { EnsureSession(); return sessionSettings == null ? null : sessionSettings.Clone(); } }
        public bool HasSession => shapedProfile != null && shapedProfile.settings != null;
        public static event Action<VoxelSurfaceComparison, bool> EditorSaveRequested;

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
        readonly Dictionary<(Material Source, VoxelSurfaceProfileSO Profile, bool LightingOnly), Material> cache =
            new Dictionary<(Material, VoxelSurfaceProfileSO, bool), Material>();
        int handedOffTotal;
        VoxelSurfaceSettings sessionSettings;
        VoxelSurfaceProfileSO sessionSourceProfile;
        bool parametersDirty;
        int lastParameterFrame = -1;

        void OnEnable()
        {
            VoxelLightContext.Register(this);
            if (Application.isPlaying) ApplyMode(mode);
        }

        void OnDisable() { VoxelLightContext.Unregister(this); RestoreOriginal(); }
        void OnDestroy() { VoxelLightContext.Unregister(this); RestoreOriginal(); }
        void LateUpdate() { FlushPendingParameters(); }

        public void SetSessionAdjustments(float litBoost, float backDarkening)
        {
            EnsureSession();
            if (sessionSettings == null) return;
            var value = sessionSettings.Clone();
            value.litFaceBoost = litBoost; value.backFaceDarkening = backDarkening; value.Sanitize();
            if (value.litFaceBoost == sessionSettings.litFaceBoost && value.backFaceDarkening == sessionSettings.backFaceDarkening) return;
            sessionSettings = value; parametersDirty = true;
            PreviewState = activeLook == null ? "原C参数临时修改" : "正在预览候选";
        }

        public void ResetSessionToC0()
        {
            CaptureOriginalCLive(); ClearLookSession(); lookInitialized = true;
            var baseline = shapedBaselineProfile != null ? shapedBaselineProfile : shapedProfile;
            if (baseline == null || baseline.settings == null) return;
            sessionSourceProfile = shapedProfile;
            sessionSettings = baseline.settings.Clone();
            sessionSettings.litFaceBoost = 0; sessionSettings.backFaceDarkening = 0;
            sessionSettings.Sanitize(); parametersDirty = true; PreviewState = "技术C0预览";
        }

        public void LoadSessionFromProfile(VoxelSurfaceProfileSO profile)
        {
            if (profile == null || profile.settings == null) return;
            sessionSourceProfile = activeLook == null ? shapedProfile : activeLook.responseProfile;
            sessionSettings = profile.settings.Clone(); sessionSettings.Sanitize(); parametersDirty = true;
            PreviewState = activeLook == null ? "原C参数临时修改" : "正在预览候选";
        }

        public VoxelSurfaceSettings ExportSessionSettings()
        {
            EnsureSession();
            if (sessionSettings == null) return null;
            var exported = sessionSettings.Clone(); exported.Sanitize(); exported.diagnostic = 0;
            return exported;
        }

        public void FlushPendingParameters()
        {
            if (!parametersDirty || mode != VoxelSurfaceComparisonMode.Faceted || Application.isPlaying && lastParameterFrame == Time.frameCount) return;
            ApplyMode(mode);
        }

        public void RequestSaveCandidate() { RequestEditorSave(false); }
        public void RequestSetBindingDefault() { RequestEditorSave(true); }

        void RequestEditorSave(bool setDefault)
        {
            if (!Application.isEditor || EditorSaveRequested == null) { SaveStatus = "此入口仅Editor可保存作者资产；Player调参为当前会话。"; return; }
            EditorSaveRequested(this, setDefault);
        }

        public void ReportSaveResult(string status, string path, VoxelSurfaceProfileSO savedProfile)
        {
            SaveStatus = status ?? "";
            if (!string.IsNullOrEmpty(path)) LastSavePath = path;
            if (savedProfile != null) LastSavedProfile = savedProfile;
        }

        void EnsureSession()
        {
            var wanted = activeLook == null ? shapedProfile : activeLook.responseProfile;
            if (sessionSettings != null && sessionSourceProfile == wanted) return;
            sessionSourceProfile = wanted;
            sessionSettings = wanted == null || wanted.settings == null ? null : wanted.settings.Clone();
            if (sessionSettings != null) sessionSettings.Sanitize();
        }

        public void ApplyMode(VoxelSurfaceComparisonMode value)
        {
            // 默认Look只初始化C；A/B启动不被候选引用强制改档，显式恢复原C/C0也不会立即跳回默认。
            if (value == VoxelSurfaceComparisonMode.Faceted && !lookInitialized)
            {
                lookInitialized = true;
                if (defaultLook != null && ApplyLook(defaultLook)) return;
            }
            if (isActiveAndEnabled) VoxelLightContext.Register(this);
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
                    mode == VoxelSurfaceComparisonMode.Faceted ? (state.Binding.lightingOnly && activeLook != null ? activeLook.responseProfile : shapedProfile) : null;
                if (!CanUse(sources, state.Original, null)) { ++rejected; continue; }
                VoxelSurfaceSettings settings = null;
                if (profile != null)
                {
                    if (mode == VoxelSurfaceComparisonMode.Faceted) { EnsureSession(); settings = sessionSettings; }
                    else { settings = profile.settings.Clone(); settings.litFaceBoost = 0; settings.backFaceDarkening = 0; }
                }
                var target = new Material[sources.Length];
                var actualSources = new Material[sources.Length];
                var actualProfiles = new VoxelSurfaceProfileSO[sources.Length];
                var actualSettings = new VoxelSurfaceSettings[sources.Length];
                bool valid = true;
                // 先验证本binding的所有槽，再创建/应用；候选只接管映射槽，其他槽使用原C_live。
                for (int slot = 0; slot < sources.Length; ++slot)
                {
                    actualSources[slot] = sources[slot]; actualProfiles[slot] = profile; actualSettings[slot] = settings;
                    if (mode == VoxelSurfaceComparisonMode.Faceted && !state.Binding.lightingOnly)
                        ResolveCSlot(sources[slot], out actualSources[slot], out actualProfiles[slot], out actualSettings[slot]);
                    if (!SupportsProfile(actualSources[slot], actualProfiles[slot]) || actualProfiles[slot] != null && actualSettings[slot] == null) valid = false;
                }
                if (!valid) { ++rejected; continue; }
                for (int slot = 0; slot < sources.Length; ++slot)
                    target[slot] = actualProfiles[slot] == null || actualSources[slot] == null ? actualSources[slot] :
                        GetMaterial(actualSources[slot], actualProfiles[slot], actualSettings[slot], state.Binding.lightingOnly, refreshed);
                if (!Same(state.Renderer.sharedMaterials, target)) state.Renderer.sharedMaterials = target;
                state.Assigned = target;
                ++applied;
            }
            handedOff += PruneCache();
            handedOffTotal += handedOff;
            Status = "体素" + ModeLabel(mode) + "；已应用 " + applied + "；外部改写让权 " + pinned + "；配置拒绝 " + rejected;
            if (handedOffTotal > 0) Status += "；ownership handed-off 累计 " + handedOffTotal + "（不再缓存）";
            if (mode == VoxelSurfaceComparisonMode.Faceted) { parametersDirty = false; lastParameterFrame = Time.frameCount; }
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

        Material GetMaterial(Material source, VoxelSurfaceProfileSO profile, VoxelSurfaceSettings settings, bool lightingOnly, HashSet<Material> refreshed)
        {
            var key = (source, profile, lightingOnly);
            if (!cache.TryGetValue(key, out var material) || material == null)
            {
                material = new Material(source) { name = source.name + " [VoxelSurface private]", hideFlags = HideFlags.HideAndDontSave };
                cache[key] = material;
            }
            if (refreshed.Add(material))
            {
                material.shader = source.shader;
                material.CopyPropertiesFromMaterial(source);
                settings.ApplyTo(material);
                // 顶点色属于该模型的固有色数据：共用受光不能把草顶/土侧/洼底改白。
                if (lightingOnly && source.HasProperty("_VoxelVertexColors"))
                    material.SetFloat("_VoxelVertexColors", source.GetFloat("_VoxelVertexColors"));
            }
            return material;
        }

        int PruneCache()
        {
            var remove = new List<(Material Source, VoxelSurfaceProfileSO Profile, bool LightingOnly)>();
            int handedOff = 0;
            foreach (var item in cache)
            {
                bool retained = false;
                foreach (var state in states)
                {
                    if (state.Binding.lightingOnly == item.Key.LightingOnly &&
                        (item.Key.Profile == calibratedProfile && Contains(state.Binding.calibrated, item.Key.Source) ||
                        item.Key.Profile == shapedProfile && Contains(state.Binding.shaped, item.Key.Source) ||
                        item.Key.LightingOnly && activeLook != null && item.Key.Profile == activeLook.responseProfile && Contains(state.Binding.shaped, item.Key.Source))) { retained = true; break; }
                }
                if (retained || RetainLookKey(item.Key.Source, item.Key.Profile)) continue;
                if (StillReferenced(item.Value)) ++handedOff;
                else DestroyOwned(item.Value);
                remove.Add(item.Key);
            }
            foreach (var key in remove) cache.Remove(key);
            return handedOff;
        }

        int HandOffExternalClones()
        {
            var remove = new List<(Material Source, VoxelSurfaceProfileSO Profile, bool LightingOnly)>();
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
            if (profile != null) foreach (var material in materials) if (!SupportsProfile(material, profile)) return false;
            return true;
        }

        static bool SupportsProfile(Material material, VoxelSurfaceProfileSO profile)
        {
            if (profile == null || material == null) return true;
            if (profile.settings == null) return false;
            return !(!material.HasProperty("_VoxelMainShape") || !material.HasProperty("_VoxelIndirectDiffuse") ||
                    !material.HasProperty("_VoxelLitFaceBoost") || !material.HasProperty("_VoxelBackFaceDarkening") ||
                    !material.HasProperty("_VoxelEnvironmentSpecular") || !material.HasProperty("_VoxelDirectSpecular") ||
                    !material.HasProperty("_VoxelHighlightStrength") || !material.HasProperty("_VoxelHighlightKnee") ||
                    !material.HasProperty("_VoxelHighlightCeiling") || !material.HasProperty("_VoxelDiagnostic") || !material.HasProperty("_VoxelVertexColors"));
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
