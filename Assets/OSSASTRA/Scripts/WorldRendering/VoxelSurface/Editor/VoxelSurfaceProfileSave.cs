/* 功能：显式保存体素C会话候选，并单独固化当前绑定组的默认Profile。
 * 边界：只写明确候选资产；C0/B/无关既有资产拒绝覆盖，诊断不进入作者默认。
 * 生命周期：Play中设置默认仅排队；退出后按GlobalObjectId及绑定签名重找原作者组件，
 *           不保存运行材质，也不打开/重建场景，不静默保存其他未保存的场景修改。
 */
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ossastra.WorldRendering.VoxelSurface
{
    [InitializeOnLoad]
    public static class VoxelSurfaceProfileSave
    {
        const string PendingKey = "Ossastra.VoxelSurface.PendingBindingDefault.v1";
        [Serializable]
        sealed class PendingDefault
        {
            public string componentId;
            public string scenePath;
            public string profilePath;
            public string bindingSignature;
        }

        static VoxelSurfaceProfileSave()
        {
            VoxelSurfaceComparison.EditorSaveRequested -= OnRequest;
            VoxelSurfaceComparison.EditorSaveRequested += OnRequest;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.delayCall += () => ProcessPendingDefault();
        }

        static void OnRequest(VoxelSurfaceComparison comparison, bool setDefault)
        {
            if (setDefault) { SetBindingDefault(comparison); return; }
            var path = comparison.saveCandidateProfile == null ? "" : AssetDatabase.GetAssetPath(comparison.saveCandidateProfile);
            if (string.IsNullOrEmpty(path)) path = EditorUtility.SaveFilePanelInProject("保存独立体素候选", "FacetedTunedCandidate", "asset", "选择新的独立候选资产；不覆盖原C0、B或其他共享配置。");
            if (string.IsNullOrEmpty(path)) { comparison.ReportSaveResult("已取消候选保存；会话参数保留。", "", null); return; }
            SaveCandidate(comparison, path);
        }

        // 无弹窗入口供原面板和Native合同共用；路径必须由调用方明确给出。
        public static bool SaveCandidate(VoxelSurfaceComparison comparison, string verifiedPath)
        {
            if (comparison == null) return false;
            string path = (verifiedPath ?? "").Replace('\\', '/');
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/../") || path.Contains("/./") || !AssetDatabase.IsValidFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/')))
                return Failure(comparison, "拒绝保存：必须提供已存在Assets目录下的明确.asset路径。", path);
            var settings = comparison.ExportSessionSettings();
            if (settings == null) return Failure(comparison, "没有独立C会话可保存。", path);
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null && (existing == comparison.shapedBaselineProfile || existing == comparison.calibratedProfile || existing != comparison.saveCandidateProfile))
                return Failure(comparison, "拒绝覆盖C0、B或未登记的既有资产；请另存独立候选。", path);
            VoxelSurfaceProfileSO candidate = existing as VoxelSurfaceProfileSO;
            bool created = candidate == null;
            try
            {
                if (created)
                {
                    candidate = ScriptableObject.CreateInstance<VoxelSurfaceProfileSO>();
                    candidate.settings = settings;
                    AssetDatabase.CreateAsset(candidate, path);
                }
                else
                {
                    Undo.RecordObject(candidate, "保存体素C会话候选");
                    candidate.settings = settings;
                    EditorUtility.SetDirty(candidate);
                }
                AssetDatabase.SaveAssetIfDirty(candidate);
                // 只记保存目标，不修改shapedProfile默认引用或当前会话。
                comparison.saveCandidateProfile = candidate;
                comparison.ReportSaveResult("候选已保存：" + path + "；来源 " + SourcePath(comparison) +
                    "；本绑定组 " + comparison.BindingCount + " 对象；默认引用未改变。", path, candidate);
                return true;
            }
            catch (Exception error)
            {
                if (created && candidate != null && !EditorUtility.IsPersistent(candidate)) UnityEngine.Object.DestroyImmediate(candidate);
                return Failure(comparison, "候选保存失败：" + error.Message, path);
            }
        }

        public static bool SetBindingDefault(VoxelSurfaceComparison comparison)
        {
            if (comparison == null) return false;
            var candidate = comparison.LastSavedProfile != null ? comparison.LastSavedProfile : comparison.saveCandidateProfile;
            var path = candidate == null ? "" : AssetDatabase.GetAssetPath(candidate);
            if (candidate == null || candidate.settings == null || !EditorUtility.IsPersistent(candidate) ||
                candidate == comparison.shapedBaselineProfile || candidate == comparison.calibratedProfile ||
                candidate.settings.diagnostic != 0 || !SameSavedSettings(comparison, candidate))
                return Failure(comparison, "请先将当前会话保存到独立候选，再设为本绑定组默认。", path);
            var scene = comparison.gameObject.scene;
            var id = GlobalObjectId.GetGlobalObjectIdSlow(comparison);
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path) || id.identifierType != 2 || id.targetObjectId == 0)
                return Failure(comparison, "当前组件没有已保存场景中的稳定作者身份，不能固化默认。", path);
            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                return ApplyDefault(comparison, candidate, path);
            var pending = new PendingDefault { componentId = id.ToString(), scenePath = scene.path,
                profilePath = path, bindingSignature = BindingSignature(comparison) };
            string priorJson = SessionState.GetString(PendingKey, "");
            if (!string.IsNullOrEmpty(priorJson))
            {
                var prior = JsonUtility.FromJson<PendingDefault>(priorJson);
                if (prior != null && prior.componentId != pending.componentId)
                    return Failure(comparison, "已有另一绑定组待退出Play固化；请先完成该请求。", path);
            }
            SessionState.SetString(PendingKey, JsonUtility.ToJson(pending));
            comparison.ReportSaveResult("已登记默认请求：" + path + "；来源 " + SourcePath(comparison) +
                "；本绑定组 " + comparison.BindingCount + " 对象。待退出Play后固化，当前尚未保存场景。", path, candidate);
            return true;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += () => ProcessPendingDefault();
        }

        public static bool ProcessPendingDefault()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return false;
            string json = SessionState.GetString(PendingKey, "");
            if (string.IsNullOrEmpty(json)) return false;
            var pending = JsonUtility.FromJson<PendingDefault>(json);
            if (pending == null || !GlobalObjectId.TryParse(pending.componentId, out var id))
            { Debug.LogWarning("体素默认请求身份无效，未修改场景。"); SessionState.EraseString(PendingKey); return false; }
            var comparison = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as VoxelSurfaceComparison;
            var candidate = AssetDatabase.LoadAssetAtPath<VoxelSurfaceProfileSO>(pending.profilePath);
            if (comparison == null || comparison.gameObject.scene.path != pending.scenePath || BindingSignature(comparison) != pending.bindingSignature || candidate == null)
            { Debug.LogWarning("体素默认请求未找到完全相同的已加载作者绑定组，未修改或打开场景。"); SessionState.EraseString(PendingKey); return false; }
            bool saved = ApplyDefault(comparison, candidate, pending.profilePath);
            // 场景dirty时请求仍保留，用户先保存后可显式再次点设默认；不轮询或抢写。
            if (saved) SessionState.EraseString(PendingKey);
            return saved;
        }

        static bool ApplyDefault(VoxelSurfaceComparison comparison, VoxelSurfaceProfileSO candidate, string path)
        {
            var scene = comparison.gameObject.scene;
            if (candidate == comparison.shapedBaselineProfile || candidate == comparison.calibratedProfile || candidate.settings == null || candidate.settings.diagnostic != 0)
                return Failure(comparison, "候选已变化或不合法，默认未写入。", path);
            if (scene.isDirty)
                return Failure(comparison, "默认尚未固化：场景有未保存修改。请先保存场景，再点设为本绑定组默认。候选已保留：" + path, path);
            // 在Edit明确归还本组件私有预览后，只保存作者Profile引用，杜绝临时材质进入scene。
            comparison.RestoreOriginal();
            Undo.RecordObject(comparison, "设置本体素绑定组默认");
            comparison.shapedProfile = candidate;
            comparison.saveCandidateProfile = candidate;
            comparison.mode = VoxelSurfaceComparisonMode.Faceted;
            EditorUtility.SetDirty(comparison);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) return Failure(comparison, "默认引用已更新，但场景保存失败；请检查并手动保存。", path);
            comparison.ReportSaveResult("已固化本绑定组默认：" + path + "；场景 " + scene.path + "；" + comparison.BindingCount + " 对象；C0/A/B保持。", path, candidate);
            return true;
        }

        static bool SameSavedSettings(VoxelSurfaceComparison comparison, VoxelSurfaceProfileSO candidate)
        {
            var current = comparison.ExportSessionSettings();
            var saved = candidate.settings.Clone(); saved.Sanitize();
            return current != null && JsonUtility.ToJson(current) == JsonUtility.ToJson(saved);
        }

        static string BindingSignature(VoxelSurfaceComparison comparison)
        {
            var ids = new List<string>();
            if (comparison.bindings != null) foreach (var binding in comparison.bindings)
                ids.Add(binding == null || binding.renderer == null ? "missing" : GlobalObjectId.GetGlobalObjectIdSlow(binding.renderer).ToString());
            return string.Join("|", ids);
        }

        static string SourcePath(VoxelSurfaceComparison comparison)
        {
            var source = comparison.SessionSourceProfile;
            return source == null ? "未配置" : AssetDatabase.GetAssetPath(source);
        }

        static bool Failure(VoxelSurfaceComparison comparison, string message, string path)
        {
            comparison.ReportSaveResult(message + "；来源 " + SourcePath(comparison) + "；本绑定组 " + comparison.BindingCount + " 对象。", path, null);
            return false;
        }
    }
}
#endif
