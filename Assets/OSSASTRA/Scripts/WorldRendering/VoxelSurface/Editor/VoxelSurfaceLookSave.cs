/* 功能：保存完整体素候选（材质映射与独立响应），并复用原唯一退出Play默认队列。
 * 边界：只写显式saveLookTarget及其独立Profile，不覆盖原C_live/C0/B或普通候选；不保存私有材质。
 * 验证：完整指纹包含映射材质身份/依赖和真实参数，不能只保存两滑条后丢失配色。
 */
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ossastra.WorldRendering.VoxelSurface
{
    public static class VoxelSurfaceLookSave
    {
        public static bool SaveFullLook(VoxelSurfaceComparison comparison, string verifiedPath)
        {
            if (comparison == null) return false;
            var target = comparison.saveLookTarget;
            var snapshot = comparison.ExportLookSession();
            string path = (verifiedPath ?? "").Replace('\\', '/');
            if (target == null || !EditorUtility.IsPersistent(target) || AssetDatabase.GetAssetPath(target) != path ||
                !path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                return Failure(comparison, "请先登记明确的独立saveLookTarget；仅允许写该资产。", path);
            if (snapshot == null || snapshot.settings == null || snapshot.materials == null || snapshot.materials.Length == 0)
                return Failure(comparison, "当前没有可保存的完整候选会话。", path);
            var profile = target.responseProfile;
            if (profile == null || !EditorUtility.IsPersistent(profile) || IsProtectedProfile(comparison, profile))
                return Failure(comparison, "完整候选目标必须引用独立的持久化响应Profile，不能写原C_live/C0/B。", path);
            var currentDefault = comparison.defaultLook;
            if (currentDefault != null && (target == currentDefault || profile == currentDefault.responseProfile))
                return Failure(comparison, "保存目标正在作为本对象集默认，或共享默认响应；请另选独立保存目标，现行默认未改变。", path);
            foreach (var look in comparison.lookPresets ?? Array.Empty<VoxelSurfaceLookSO>())
                if (look != null && (look == target || look.responseProfile == profile))
                    return Failure(comparison, "保存目标响应被其他作者候选共享；请登记独立目标。", path);
            if (!PersistentMappings(snapshot.materials, out var reason)) return Failure(comparison, reason, path);
            var settings = snapshot.settings.Clone(); settings.Sanitize(); settings.diagnostic = 0;
            Undo.RecordObjects(new UnityEngine.Object[] { target, profile }, "保存完整体素外观候选");
            target.label = snapshot.label;
            target.developmentOnly = snapshot.developmentOnly;
            target.materials = CopyMappings(snapshot.materials);
            profile.settings = settings;
            EditorUtility.SetDirty(profile); EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(profile); AssetDatabase.SaveAssetIfDirty(target);
            comparison.ReportLookSaveResult("完整候选已保存：" + path + "；响应 " + AssetDatabase.GetAssetPath(profile) +
                "；来源 " + comparison.ActiveLookLabel + "；明确映射 " + comparison.MappedRendererCount + " 对象；默认未改变。", path, target, false);
            return true;
        }

        public static bool SetLookDefault(VoxelSurfaceComparison comparison)
        {
            if (comparison == null) return false;
            var candidate = comparison.LastSavedLook != null ? comparison.LastSavedLook : comparison.saveLookTarget;
            string path = candidate == null ? "" : AssetDatabase.GetAssetPath(candidate);
            if (comparison.ActiveLook == null || candidate == null || candidate.developmentOnly || !EditorUtility.IsPersistent(candidate) || !candidate.Validate(out var reason))
                return Failure(comparison, "请先保存完整候选，再显式设为本对象集默认。", path);
            if (!TryFingerprint(comparison.ExportLookSession(), out var current, out reason) ||
                !TryFingerprint(Snapshot(candidate), out var saved, out reason) || current != saved || candidate.responseProfile.settings.diagnostic != 0)
                return Failure(comparison, "当前完整配色/响应与已存候选不一致；请先保存。" + reason, path);
            return VoxelSurfaceProfileSave.QueueLookDefault(comparison, candidate, saved);
        }

        public static bool ProcessPendingDefault() => VoxelSurfaceProfileSave.ProcessPendingDefault();

        internal static bool PendingMatches(VoxelSurfaceLookSO look, string expected)
        { return look != null && TryFingerprint(Snapshot(look), out var actual, out _) && actual == expected; }

        internal static bool ApplyDefault(VoxelSurfaceComparison comparison, VoxelSurfaceLookSO candidate, string path)
        {
            if (comparison == null || candidate == null || candidate.developmentOnly || !candidate.Validate(out var reason) || candidate.responseProfile.settings.diagnostic != 0)
                return comparison == null ? false : Failure(comparison, "完整候选不合法或包含诊断，默认未写入。", path);
            var scene = comparison.gameObject.scene;
            if (scene.isDirty) return Failure(comparison, "完整默认尚未固化：请先保存场景，再点设默认；候选已保留。", path);
            comparison.RestoreOriginal();
            foreach (var binding in comparison.bindings)
                if (binding != null && binding.renderer != null)
                    foreach (var material in binding.renderer.sharedMaterials)
                        if (material != null && !EditorUtility.IsPersistent(material))
                            return Failure(comparison, "仍有外部私有材质，拒绝将其写入作者场景。", path);
            Undo.RecordObject(comparison, "设置完整体素候选默认");
            comparison.defaultLook = candidate;
            comparison.mode = VoxelSurfaceComparisonMode.Faceted;
            EditorUtility.SetDirty(comparison); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) return Failure(comparison, "完整默认引用已改，但场景保存失败，请手动检查。", path);
            comparison.ReportLookSaveResult("已设本对象集完整默认：" + path + "；场景 " + scene.path +
                "；原C_live/C0/A/B与原槽位快照保留。", path, candidate, true);
            return true;
        }

        internal static bool TryFingerprint(VoxelSurfaceLookSnapshot value, out string fingerprint, out string reason)
        {
            fingerprint = "";
            if (value == null || value.settings == null || !PersistentMappings(value.materials, out reason))
            { reason = "缺少完整持久化候选"; return false; }
            var settings = value.settings.Clone(); settings.Sanitize();
            var rows = new List<string>();
            foreach (var item in value.materials)
                rows.Add(Identity(item.source) + ">" + Identity(item.replacement));
            rows.Sort(StringComparer.Ordinal);
            // Profile身份允许另存为独立目标；实际参数和材质依赖才决定是否为同一外观。
            fingerprint = (value.developmentOnly ? "dev|" : "normal|") + JsonUtility.ToJson(settings) + "|" + string.Join("|", rows);
            reason = ""; return true;
        }

        static VoxelSurfaceLookSnapshot Snapshot(VoxelSurfaceLookSO look)
        { return look == null || look.responseProfile == null ? null : new VoxelSurfaceLookSnapshot { label = look.label, developmentOnly = look.developmentOnly, settings = look.responseProfile.settings, materials = look.materials }; }

        static string Identity(Material material)
        {
            string path = AssetDatabase.GetAssetPath(material);
            return AssetDatabase.AssetPathToGUID(path) + ":" + AssetDatabase.GetAssetDependencyHash(path);
        }

        static bool PersistentMappings(VoxelSurfaceLookSO.MaterialOverride[] materials, out string reason)
        {
            if (materials == null || materials.Length == 0) { reason = "完整候选没有材质映射。"; return false; }
            var seen = new HashSet<Material>();
            foreach (var item in materials)
            {
                if (item == null || item.source == null || item.replacement == null || !seen.Add(item.source) ||
                    !EditorUtility.IsPersistent(item.source) || !EditorUtility.IsPersistent(item.replacement))
                { reason = "材质映射缺失、重复或仍为运行时副本，拒绝保存。"; return false; }
                if (item.source.shader == null || item.replacement.shader == null || item.source.shader.name != "Ossastra/WorldRendering/VoxelLit" ||
                    item.replacement.shader.name != "Ossastra/WorldRendering/VoxelLit")
                { reason = "完整候选包含未授权材质家族。"; return false; }
            }
            reason = ""; return true;
        }

        static bool IsProtectedProfile(VoxelSurfaceComparison c, VoxelSurfaceProfileSO p)
        { return p == c.originalCLiveProfile || p == c.shapedBaselineProfile || p == c.shapedProfile || p == c.calibratedProfile; }

        static VoxelSurfaceLookSO.MaterialOverride[] CopyMappings(VoxelSurfaceLookSO.MaterialOverride[] source)
        {
            var result = new VoxelSurfaceLookSO.MaterialOverride[source.Length];
            for (int i = 0; i < source.Length; ++i) result[i] = source[i].Clone();
            return result;
        }

        static bool Failure(VoxelSurfaceComparison comparison, string message, string path)
        { comparison.ReportLookSaveResult(message + "；当前 " + comparison.ActiveLookLabel + "；明确映射 " + comparison.MappedRendererCount + " 对象。", path, null, false); return false; }
    }
}
#endif
