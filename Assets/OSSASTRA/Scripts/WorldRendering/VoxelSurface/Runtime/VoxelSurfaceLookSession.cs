/* 功能：既有比较组件的完整Look会话；材质映射与受光副本一起切换、导出和恢复原C_live。
 * 边界：不是新管理器；仍由VoxelSurfaceComparison唯一缓存/写Renderer，不修改作者材质和Profile。
 * 归属：只映射明确的binding.shaped源，未映射槽使用原C_live，A/B保持旧路径。
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ossastra.WorldRendering.VoxelSurface
{
    public sealed partial class VoxelSurfaceComparison
    {
        [Header("完整材质与响应候选；原C不等同技术C0")]
        public VoxelSurfaceProfileSO originalCLiveProfile;
        public VoxelSurfaceLookSO[] lookPresets = Array.Empty<VoxelSurfaceLookSO>();
        public VoxelSurfaceLookSO defaultLook;
        public VoxelSurfaceLookSO saveLookTarget;

        public VoxelSurfaceLookSO ActiveLook => activeLook;
        public VoxelSurfaceLookSO LastSavedLook { get; private set; }
        public string ActiveLookLabel => activeLook == null ? "原C" : sessionLookLabel;
        public string PreviewState { get; private set; } = "原C预览";
        public int MappedRendererCount
        {
            get
            {
                if (activeLook == null || bindings == null) return 0;
                var seen = new HashSet<Renderer>();
                foreach (var binding in bindings)
                {
                    if (binding == null || binding.renderer == null || binding.shaped == null) continue;
                    foreach (var source in binding.shaped)
                        if (TryResolveLookMaterial(source, out _)) { seen.Add(binding.renderer); break; }
                }
                return seen.Count;
            }
        }

        VoxelSurfaceLookSO activeLook;
        VoxelSurfaceLookSO.MaterialOverride[] sessionLookMaterials = Array.Empty<VoxelSurfaceLookSO.MaterialOverride>();
        string sessionLookLabel;
        bool sessionLookDevelopmentOnly;
        VoxelSurfaceSettings originalCLiveSettings;
        bool lookInitialized;

        public bool ApplyLook(VoxelSurfaceLookSO look)
        {
            if (look == null) { RestoreOriginalCLive(); return true; }
            if (!look.Validate(out var reason)) { Status = "完整候选拒绝：" + reason; return false; }
            bool mapped = false;
            if (bindings != null) foreach (var binding in bindings)
                if (binding != null && binding.renderer != null && binding.shaped != null)
                    foreach (var source in binding.shaped) foreach (var item in look.materials)
                        if (source == item.source) mapped = true;
            if (!mapped) { Status = "完整候选拒绝：当前明确绑定中没有匹配的源材质"; return false; }
            CaptureOriginalCLive();
            activeLook = look; sessionLookLabel = look.label; sessionLookDevelopmentOnly = look.developmentOnly;
            sessionLookMaterials = CloneMappings(look.materials);
            sessionSourceProfile = look.responseProfile;
            sessionSettings = look.responseProfile.settings.Clone(); sessionSettings.Sanitize();
            parametersDirty = true; lookInitialized = true; PreviewState = "正在预览候选";
            ApplyMode(VoxelSurfaceComparisonMode.Faceted);
            return true;
        }

        public void RestoreOriginalCLive()
        {
            CaptureOriginalCLive();
            ClearLookSession(); lookInitialized = true;
            sessionSourceProfile = shapedProfile;
            sessionSettings = originalCLiveSettings == null ? null : originalCLiveSettings.Clone();
            parametersDirty = true; PreviewState = "原C预览";
            ApplyMode(VoxelSurfaceComparisonMode.Faceted);
        }

        public VoxelSurfaceLookSnapshot ExportLookSession()
        {
            if (activeLook == null) return null;
            return new VoxelSurfaceLookSnapshot { label = sessionLookLabel, developmentOnly = sessionLookDevelopmentOnly, settings = ExportSessionSettings(), materials = CloneMappings(sessionLookMaterials) };
        }

        public void ReportLookSaveResult(string status, string path, VoxelSurfaceLookSO saved, bool isDefault)
        {
            if (saved != null) LastSavedLook = saved;
            PreviewState = isDefault ? "已设本对象集默认" : saved == null ? "正在预览候选" : "已保存候选";
            ReportSaveResult(status, path, saved == null ? null : saved.responseProfile);
        }

        void CaptureOriginalCLive()
        {
            if (originalCLiveSettings != null) return;
            var source = originalCLiveProfile != null ? originalCLiveProfile : shapedProfile;
            if (originalCLiveProfile == null && sessionSettings != null && activeLook == null)
                originalCLiveSettings = sessionSettings.Clone();
            else originalCLiveSettings = source == null || source.settings == null ? null : source.settings.Clone();
            if (originalCLiveSettings != null) originalCLiveSettings.Sanitize();
        }

        void ClearLookSession()
        { activeLook = null; sessionLookMaterials = Array.Empty<VoxelSurfaceLookSO.MaterialOverride>(); sessionLookLabel = null; }

        bool TryResolveLookMaterial(Material source, out Material replacement)
        {
            if (activeLook != null && sessionLookMaterials != null)
                foreach (var item in sessionLookMaterials)
                    if (item != null && item.source == source && item.replacement != null) { replacement = item.replacement; return true; }
            replacement = source; return false;
        }

        void ResolveCSlot(Material source, out Material actualSource, out VoxelSurfaceProfileSO profile, out VoxelSurfaceSettings settings)
        {
            if (activeLook != null)
            {
                if (TryResolveLookMaterial(source, out actualSource))
                { profile = activeLook.responseProfile; settings = sessionSettings; return; }
                CaptureOriginalCLive(); actualSource = source;
                profile = originalCLiveProfile != null ? originalCLiveProfile : shapedProfile;
                settings = originalCLiveSettings; return;
            }
            actualSource = source; profile = shapedProfile; settings = sessionSettings;
        }

        bool RetainLookKey(Material source, VoxelSurfaceProfileSO profile)
        {
            if (source == null || profile == null) return false;
            var baseline = originalCLiveProfile != null ? originalCLiveProfile : shapedProfile;
            if (profile == baseline && bindings != null)
                foreach (var binding in bindings) if (binding != null && Contains(binding.shaped, source)) return true;
            if (activeLook != null && profile == activeLook.responseProfile)
                foreach (var item in sessionLookMaterials) if (item != null && item.replacement == source) return true;
            if (RetainConfiguredLook(lookPresets, source, profile)) return true;
            return RetainConfiguredLook(new[] { defaultLook, saveLookTarget }, source, profile);
        }

        bool RetainConfiguredLook(VoxelSurfaceLookSO[] looks, Material source, VoxelSurfaceProfileSO profile)
        {
            if (looks == null || bindings == null) return false;
            foreach (var look in looks)
            {
                if (look == null || look.responseProfile != profile || look.materials == null) continue;
                foreach (var item in look.materials)
                    if (item != null && item.replacement == source)
                        foreach (var binding in bindings)
                            if (binding != null && Contains(binding.shaped, item.source)) return true;
            }
            return false;
        }

        static VoxelSurfaceLookSO.MaterialOverride[] CloneMappings(VoxelSurfaceLookSO.MaterialOverride[] source)
        {
            if (source == null) return Array.Empty<VoxelSurfaceLookSO.MaterialOverride>();
            var copy = new VoxelSurfaceLookSO.MaterialOverride[source.Length];
            for (int i = 0; i < copy.Length; ++i) copy[i] = source[i] == null ? null : source[i].Clone();
            return copy;
        }
    }
}
