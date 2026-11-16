/*
 * 脚本：SideViewSliceSessionFlow
 * 功能：标题选择槽位、配置每个地图场景的 Commerce 启动、等待退出保存后返回标题。
 * 边界：门只处理在途落点；本应用流不持有第二份存档或修改领域事实。
 */
using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ossastra.Characters;
using Ossastra.Inventory;
using Ossastra.Commerce.Persistence;
using Ossastra.Commerce.Runtime;
using Ossastra.SideViewWorld;

namespace Ossastra.SideView.Slice
{
    [Serializable]
    public sealed class SliceSceneEntry
    {
        public string mapId;
        public string scenePath;
        public string[] anchorIds = Array.Empty<string>();
    }

    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class SideViewSliceSessionFlow : MonoBehaviour
    {
        private static SideViewSliceSessionFlow instance;
        public static SideViewSliceSessionFlow Instance => instance;

        public string titleScenePath;
        public string firstMapId;
        [Tooltip("留空使用 persistentDataPath/SV-SLICE-01；与旧验证和正式 Commerce 档分离。")]
        public string saveDirectory;
        public SliceSceneEntry[] scenes = Array.Empty<SliceSceneEntry>();

        private SliceSlotCatalog catalog;
        private string activeProfileId, expectedResumeMapId, sceneConfigurationError;
        private CommerceStartupIntent launchIntent = CommerceStartupIntent.Any;
        private bool pendingResume, resumeApplied, gameActive, busy;
        private CharacterControlCoordinator capturedParty;

        public bool Busy => busy;
        public bool GameActive => gameActive;
        public bool AtTitle => SceneManager.GetActiveScene().path == titleScenePath;
        public bool CanRequestReturnToTitle => gameActive && !busy
            && (SideViewTransitionRuntime.Instance == null || !SideViewTransitionRuntime.Instance.Busy);
        public string ActiveProfileId => activeProfileId;
        public string Status { get; private set; } = "等待标题";
        public string SaveDirectory => string.IsNullOrWhiteSpace(saveDirectory)
            ? System.IO.Path.Combine(Application.persistentDataPath, "SV-SLICE-01") : saveDirectory;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticReference() => instance = null;

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            if (string.IsNullOrWhiteSpace(titleScenePath) || string.IsNullOrWhiteSpace(firstMapId)
                || scenes == null || scenes.Length == 0 || scenes.Any(s => s == null
                    || string.IsNullOrWhiteSpace(s.mapId) || string.IsNullOrWhiteSpace(s.scenePath)
                    || s.anchorIds == null || s.anchorIds.Length == 0)
                || scenes.Select(s => s.mapId).Distinct(StringComparer.Ordinal).Count() != scenes.Length
                || !scenes.Any(s => s.mapId == firstMapId))
            {
                Status = "标题/地图目录配置无效";
                Debug.LogError("[SV-SLICE-01] " + Status, this);
                enabled = false;
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            catalog = new SliceSlotCatalog(new CommerceFileSaveStore(SaveDirectory), IsKnownAnchor);
            SceneManager.sceneLoaded += OnSceneLoaded;
            Status = "标题就绪";
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            ReleaseGameplay();
            if (instance == this) instance = null;
        }

        public SliceSlotPreview[] InspectSlots() => catalog?.InspectAll() ?? Array.Empty<SliceSlotPreview>();

        /// <summary>Title-only test seam: choose a fresh external directory before selecting a slot.</summary>
        public bool ConfigureValidationDirectory(string directory)
        {
            if (!CanChooseSlot() || string.IsNullOrWhiteSpace(directory)) return false;
            try
            {
                var absolute = System.IO.Path.GetFullPath(directory);
                saveDirectory = absolute;
                catalog = new SliceSlotCatalog(new CommerceFileSaveStore(absolute), IsKnownAnchor);
                Status = "验证档目录已隔离";
                return true;
            }
            catch (Exception ex) { Status = "验证档目录无效：" + ex.Message; return false; }
        }

        public bool StartNew(string profileId)
        {
            if (!CanChooseSlot() || !SliceSlotCatalog.IsSlotId(profileId)
                || catalog.Inspect(profileId).State != SliceSlotState.Empty)
            { Status = "新游戏需要一个真正空槽"; return false; }
            return BeginLaunch(profileId, firstMapId, CommerceStartupIntent.New, false);
        }

        public bool Continue(string profileId)
        {
            if (!CanChooseSlot() || !SliceSlotCatalog.IsSlotId(profileId)) return false;
            var preview = catalog.Inspect(profileId);
            if (preview.State != SliceSlotState.Loadable)
            { Status = "槽位不可读取：" + (preview.Error ?? "缺档"); return false; }
            return BeginLaunch(profileId, preview.CheckpointMapId,
                CommerceStartupIntent.Continue, true);
        }

        public bool CopySlot(string sourceProfileId, string destinationProfileId)
        {
            if (!CanChooseSlot()) return false;
            bool copied = catalog.Copy(sourceProfileId, destinationProfileId);
            Status = copied ? "槽位已复制" : "复制失败：" + catalog.LastError;
            return copied;
        }

        /// <summary>由标题 UI 的二次确认后调用。</summary>
        public bool DeleteSlot(string profileId)
        {
            if (!CanChooseSlot()) return false;
            bool deleted = catalog.Delete(profileId);
            Status = deleted ? "槽位已删除" : "删除失败：" + (catalog.LastError ?? "槽位为空");
            return deleted;
        }

        private bool CanChooseSlot() => isActiveAndEnabled && catalog != null && AtTitle && !busy && !gameActive;

        private bool BeginLaunch(string profileId, string mapId, CommerceStartupIntent intent, bool resume)
        {
            var entry = EntryForMap(mapId);
            if (entry == null || !SceneAvailable(entry.scenePath))
            { Status = "目标地图未列入可加载场景：" + mapId; return false; }
            activeProfileId = profileId;
            launchIntent = intent;
            pendingResume = resume;
            resumeApplied = false;
            expectedResumeMapId = resume ? mapId : null;
            sceneConfigurationError = null;
            busy = true;
            StartCoroutine(Launch(entry));
            return true;
        }

        private IEnumerator Launch(SliceSceneEntry entry)
        {
            Status = pendingResume ? "正在读取完整会话" : "正在建立新会话";
            AsyncOperation operation = null;
            try { operation = LoadSingle(entry.scenePath); }
            catch (Exception ex) { sceneConfigurationError = ex.Message; }
            if (operation != null) while (!operation.isDone) yield return null;
            if (operation != null)
            {
                for (int frame = 0; frame < 180; ++frame)
                {
                    if (sceneConfigurationError != null) break;
                    var commerce = FindObjectOfType<CommerceService>(true);
                    var progress = FindObjectOfType<SideViewSliceProgress>(true);
                    if (commerce != null && progress != null && commerce.Ready && progress.Ready)
                    {
                        if (!pendingResume || resumeApplied || progress.TryApplyCheckpoint())
                        {
                            gameActive = true;
                            pendingResume = false;
                            launchIntent = CommerceStartupIntent.Any;
                            busy = false;
                            Status = "游戏会话就绪";
                            yield break;
                        }
                    }
                    yield return null;
                }
            }
            Status = "启动失败：" + (sceneConfigurationError ?? "存档或检查点未能恢复");
            activeProfileId = null;
            pendingResume = false;
            launchIntent = CommerceStartupIntent.Any;
            expectedResumeMapId = null;
            AsyncOperation back = null;
            try { back = LoadSingle(titleScenePath); }
            catch (Exception ex) { Status += "；标题加载失败：" + ex.Message; }
            if (back != null) while (!back.isDone) yield return null;
            busy = false;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (string.IsNullOrEmpty(activeProfileId) || mode != LoadSceneMode.Single) return;
            var entry = EntryForScene(scene.path);
            if (entry == null) return;
            var commerce = FindObjectsOfType<CommerceService>(true)
                .FirstOrDefault(candidate => candidate.gameObject.scene == scene);
            if (commerce == null)
            { FailSceneConfiguration(scene, "地图缺少场景局部 CommerceService"); return; }
            try { commerce.ConfigureStartup(activeProfileId, SaveDirectory, launchIntent); }
            catch (Exception ex) { FailSceneConfiguration(scene, ex.Message); }
        }

        private void FailSceneConfiguration(Scene scene, string reason)
        {
            sceneConfigurationError = reason;
            // Prevent an unconfigured InventorySession from seeding a scene-authored old profile.
            foreach (var inventory in FindObjectsOfType<InventorySession>(true))
                if (inventory.gameObject.scene == scene) inventory.enabled = false;
        }

        public bool HasPendingResumeFor(string mapId)
            => pendingResume && activeProfileId != null
                && string.Equals(mapId, expectedResumeMapId, StringComparison.Ordinal);

        public void ConfirmResumeApplied(SideViewSliceProgress progress)
        {
            if (progress != null && HasPendingResumeFor(progress.mapId)
                && progress.CheckpointMapId == progress.mapId)
                resumeApplied = true;
        }

        public bool RequestReturnToTitle(string reason = "menu")
        {
            if (!CanRequestReturnToTitle) { Status = "过门中或无活动会话，不能返回标题"; return false; }
            busy = true;
            StartCoroutine(ReturnToTitle(reason, false));
            return true;
        }

        public bool RetryReturnToTitle()
        {
            if (!CanRequestReturnToTitle) return false;
            busy = true;
            StartCoroutine(ReturnToTitle("retry", true));
            return true;
        }

        private IEnumerator ReturnToTitle(string reason, bool retry)
        {
            var commerce = FindObjectOfType<CommerceService>(true);
            capturedParty = FindObjectOfType<CharacterControlCoordinator>(true);
            if (capturedParty != null) capturedParty.SetInputCapture(this, true);
            var previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            if (commerce == null || !commerce.Ready || commerce.profileId != activeProfileId)
            {
                Status = "返回标题失败：活动存档根缺失";
                Time.timeScale = previousTimeScale;
                ReleaseGameplay(); busy = false; yield break;
            }
            if (retry && commerce.SaveStatus != null && !commerce.SaveStatus.IsClosed)
            {
                var retryTask = commerce.RetrySaveAsync();
                while (!retryTask.IsCompleted) yield return null;
                if (!Succeeded(retryTask))
                {
                    Status = "重试保存失败：" + commerce.Diagnostic;
                    Time.timeScale = previousTimeScale;
                    ReleaseGameplay(); busy = false; yield break;
                }
            }
            if (commerce.SaveStatus == null)
            {
                Status = "返回标题失败：保存状态缺失";
                Time.timeScale = previousTimeScale;
                ReleaseGameplay(); busy = false; yield break;
            }
            if (!commerce.SaveStatus.IsClosed)
            {
                Status = "正在保存并关闭会话";
                var closeTask = commerce.CloseForTitleAsync();
                while (!closeTask.IsCompleted) yield return null;
                if (!Succeeded(closeTask))
                {
                    Status = "保存失败，仍在游戏内；可重试：" + commerce.Diagnostic;
                    Time.timeScale = previousTimeScale;
                    ReleaseGameplay(); busy = false; yield break;
                }
            }
            else if (!commerce.SaveStatus.IsSaved)
            {
                Status = "返回标题失败：会话已关闭但快照未保存";
                Time.timeScale = previousTimeScale;
                ReleaseGameplay(); busy = false; yield break;
            }
            var transition = SideViewTransitionRuntime.Instance;
            if (transition != null)
            {
                Destroy(transition.gameObject);
                yield return null;
            }
            Time.timeScale = 1f;
            Status = reason == "boss-complete" ? "通关状态已保存，正在返回标题" : "会话已保存，正在返回标题";
            AsyncOperation operation = null;
            try { operation = LoadSingle(titleScenePath); }
            catch (Exception ex) { Status = "标题场景未受理：" + ex.Message; }
            if (operation == null)
            {
                // Writer is already closed; keep the scene and report a recoverable presentation failure.
                Time.timeScale = previousTimeScale;
                ReleaseGameplay(); busy = false; yield break;
            }
            while (!operation.isDone) yield return null;
            ReleaseGameplay();
            activeProfileId = null;
            expectedResumeMapId = null;
            pendingResume = false;
            resumeApplied = false;
            gameActive = false;
            launchIntent = CommerceStartupIntent.Any;
            busy = false;
            Status = "标题就绪";
        }

        private void ReleaseGameplay()
        {
            if (capturedParty != null) capturedParty.SetInputCapture(this, false);
            capturedParty = null;
        }

        private static bool Succeeded(Task<bool> task)
        {
            try { return task.GetAwaiter().GetResult(); }
            catch { return false; }
        }

        private bool IsKnownAnchor(string mapId, string anchorId)
        {
            var entry = EntryForMap(mapId);
            return entry != null && entry.anchorIds != null
                && entry.anchorIds.Contains(anchorId, StringComparer.Ordinal);
        }

        private SliceSceneEntry EntryForMap(string mapId)
            => scenes?.FirstOrDefault(s => s != null && s.mapId == mapId);

        private SliceSceneEntry EntryForScene(string path)
            => scenes?.FirstOrDefault(s => s != null && s.scenePath == path);

        private static bool SceneAvailable(string path)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(path) != null;
#else
            return SceneUtility.GetBuildIndexByScenePath(path) >= 0;
#endif
        }

        private static AsyncOperation LoadSingle(string path)
        {
#if UNITY_EDITOR
            return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            return SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
#endif
        }
    }
}
