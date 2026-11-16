/* 脚本：SideViewTransitionRuntime
 * 功能：横版实体门过区的淡出、共同快照保存屏障、Single 装载与双角色安全落点。
 * 边界：持有的票据与角色位置仅用于在途和失败恢复；永久世界状态归 Commerce 快照。
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ossastra.Characters;
using Ossastra.Inventory;
using Ossastra.Commerce.Runtime;
using Ossastra.WorldProps;
using Ossastra.WorldTransition;

namespace Ossastra.SideViewWorld
{
    /// <summary>
    /// Tut_01/Tut_02 transition host. Scene-local Commerce hosts restore one shared snapshot.
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public sealed class SideViewTransitionRuntime : MonoBehaviour
    {
        private static SideViewTransitionRuntime instance;
        private readonly TransitTicketState tickets = new TransitTicketState();
        private TransitTicket activeTicket;
        private CharacterControlCoordinator capturedParty;
        private CommerceService sourceCommerce;
        private readonly Dictionary<string, Vector3> sourcePositions = new Dictionary<string, Vector3>(StringComparer.Ordinal);
        private string sourceScenePath;
        private string sourceActorId;
        private string sourceProfileId;
        private string sourceSaveDirectory;
        private bool arrivalConfirmed;
        private bool actorRestored;
        private bool recoveryPending;
        private bool busy;
        private float fadeAlpha;

        public static SideViewTransitionRuntime Instance => instance;
        public bool Busy => busy;
        public float FadeAlpha => fadeAlpha;
        public int SourceHandle { get; private set; }
        public int DestinationHandle { get; private set; }
        public string Phase { get; private set; } = "idle";
        public string Status { get; private set; } = "尚未换区";
        public string PendingTarget => tickets.Pending == null ? "" :
            tickets.Pending.DestinationScene + " @ " + tickets.Pending.DestinationGateId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticReference() => instance = null;

        public static SideViewTransitionRuntime Ensure()
        {
            if (instance != null) return instance;
            var host = new GameObject("Side View Scene Transition");
            return host.AddComponent<SideViewTransitionRuntime>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            ReleaseControls();
            if (instance == this) instance = null;
        }

        public bool TryBegin(SideViewTransitionGate gate)
        {
            if (!Application.isPlaying || gate == null || gate.party == null
                || gate.inventory == null || gate.party.Current == null
                || !gate.inventory.Ready || gate.commerce == null
                || gate.commerce.inventory != gate.inventory || !gate.commerce.Ready
                || gate.commerce.SaveStatus == null || !gate.commerce.SaveStatus.CanEdit)
            {
                Status = "入口、角色或共同存档未就绪";
                return false;
            }
            if (busy)
            {
                Status = "已有换区请求";
                return false;
            }
            var scene = SceneManager.GetActiveScene();
            if (gate.gameObject.scene != scene
                || gate.commerce.gameObject.scene != scene
                || !string.Equals(gate.commerce.mapId,
                    SideViewTransitionCatalog.MapIdForScene(scene.path), StringComparison.Ordinal)
                || !SideViewTransitionCatalog.IsRoute(scene.path, gate.gateId,
                    gate.destinationScenePath, gate.destinationGateId)
                || !SideViewTransitionCatalog.SceneAvailable(gate.destinationScenePath))
            {
                Status = "目标 Scene/Gate 配对无效";
                return false;
            }
            var begin = tickets.TryBegin(scene.name, gate.gateId,
                gate.destinationScenePath, gate.destinationGateId, out var ticket);
            if (begin != TransitBeginResult.Started)
            {
                Status = "入口票据被拒绝：" + begin;
                return false;
            }
            activeTicket = ticket;
            sourceCommerce = gate.commerce;
            sourceScenePath = scene.path;
            sourceActorId = gate.party.Current.ActorId;
            sourceProfileId = gate.commerce.profileId;
            sourceSaveDirectory = gate.commerce.saveDirectory;
            sourcePositions.Clear();
            foreach (var character in gate.party.Characters)
                if (character != null) sourcePositions[character.ActorId] = character.transform.position;
            SourceHandle = scene.handle;
            DestinationHandle = 0;
            arrivalConfirmed = false;
            actorRestored = false;
            recoveryPending = false;
            busy = true;
            StartCoroutine(Travel());
            return true;
        }

        private IEnumerator Travel()
        {
            Phase = "fade-out";
            Status = "已进入边界，正在淡出";
            CaptureControls();
            yield return FadeTo(1f);

            // 源场仍活着时等待原 Commerce 协调器确认最新修订已落盘。
            Phase = "save-flush";
            Status = "正在保存横版世界状态";
            Task<bool> flush = null;
            string saveError = null;
            try { flush = sourceCommerce.FlushBeforeSceneChangeAsync(); }
            catch (Exception ex) { saveError = ex.Message; }
            if (flush != null)
            {
                while (!flush.IsCompleted) yield return null;
                try { if (!flush.GetAwaiter().GetResult()) saveError = "共同快照未落盘"; }
                catch (Exception ex) { saveError = ex.Message; }
            }
            if (saveError != null || flush == null)
            {
                tickets.Abort(activeTicket.Id);
                Status = "源场保留；保存失败：" + (saveError ?? "保存任务未启动");
                yield return FinishFailure();
                yield break;
            }

            Phase = "scene-load";
            Status = "正在切换横版 Scene";
            AsyncOperation operation = null;
            string error = null;
            try { operation = LoadSingle(activeTicket.DestinationScene); }
            catch (Exception ex) { error = ex.Message; }
            if (operation == null)
            {
                tickets.Abort(activeTicket.Id);
                Status = "目标 Scene 未受理：" + (error ?? "未知原因");
                yield return FinishFailure();
                yield break;
            }
            while (!operation.isDone) yield return null;
            // sceneLoaded runs after Awake/OnEnable and before Start. Arrival.Start
            // consumes the ticket, then the side-view camera snaps in LateUpdate.
            yield return null;
            yield return null;
            var inventory = FindObjectOfType<InventorySession>(true);
            var commerce = FindObjectOfType<CommerceService>(true);
            var worldProps = FindObjectOfType<WorldPropsRoot>(true);
            var cameraFollow = FindObjectOfType<SideViewCameraFollow>(true);
            for (var frames = 0; frames < 120 && arrivalConfirmed
                    && (inventory == null || !inventory.Ready || commerce == null
                        || !commerce.Ready || worldProps == null || !worldProps.Ready); frames++)
            {
                yield return null;
                inventory = FindObjectOfType<InventorySession>(true);
                commerce = FindObjectOfType<CommerceService>(true);
                worldProps = FindObjectOfType<WorldPropsRoot>(true);
            }
            if (!arrivalConfirmed || !actorRestored || inventory == null || !inventory.Ready
                || commerce == null || !commerce.Ready || commerce.SaveStatus == null
                || !commerce.SaveStatus.CanEdit || worldProps == null || !worldProps.Ready
                || !string.Equals(commerce.mapId, SideViewTransitionCatalog.MapIdForScene(activeTicket.DestinationScene), StringComparison.Ordinal)
                || !string.Equals(commerce.profileId, sourceProfileId, StringComparison.Ordinal)
                || !string.Equals(commerce.saveDirectory, sourceSaveDirectory, StringComparison.Ordinal)
                || cameraFollow == null || !cameraFollow.isActiveAndEnabled)
            {
                tickets.Abort(activeTicket.Id);
                Phase = "source-recovery";
                Status = "目标落点或横版会话未就绪，尝试返回";
                recoveryPending = true;
                AsyncOperation recovery = null;
                try { recovery = LoadSingle(sourceScenePath); }
                catch (Exception ex) { error = ex.Message; }
                if (recovery != null)
                    while (!recovery.isDone) yield return null;
                Status = recovery == null ? "返回源场失败：" + (error ?? "未知原因")
                    : "已返回源场；目标横版会话未就绪";
                yield return FinishFailure();
                yield break;
            }

            DestinationHandle = SceneManager.GetActiveScene().handle;
            Phase = "fade-in";
            Status = "已到达目标入口";
            yield return FadeTo(0f);
            ReleaseControls();
            activeTicket = null;
            sourceCommerce = null;
            sourcePositions.Clear();
            busy = false;
            Phase = "idle";
        }

        private IEnumerator FinishFailure()
        {
            Phase = "failure-fade-in";
            yield return FadeTo(0f);
            ReleaseControls();
            activeTicket = null;
            sourceCommerce = null;
            sourcePositions.Clear();
            recoveryPending = false;
            busy = false;
            Phase = "idle";
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

        private IEnumerator FadeTo(float target)
        {
            while (Mathf.Abs(fadeAlpha - target) > .005f)
            {
                fadeAlpha = Mathf.MoveTowards(fadeAlpha, target,
                    Mathf.Max(Time.unscaledDeltaTime, .001f) / .28f);
                yield return null;
            }
            fadeAlpha = target;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!busy || mode != LoadSceneMode.Single) return;
            var party = FindObjectOfType<CharacterControlCoordinator>(true);
            actorRestored = party != null && party.TryRestoreControl(sourceActorId);
            if (!actorRestored) Status = "目标场未能恢复实控角色 " + sourceActorId;
            if (recoveryPending && scene.path == sourceScenePath && party != null)
                foreach (var character in party.Characters)
                    if (character != null && sourcePositions.TryGetValue(character.ActorId, out var position))
                        character.GetComponent<CharacterMovement>()?.Teleport(position);
            CaptureControls();
        }

        private void CaptureControls()
        {
            ReleaseControls();
            capturedParty = FindObjectOfType<CharacterControlCoordinator>(true);
            if (capturedParty == null) return;
            capturedParty.SetInputCapture(this, true);
            foreach (var character in capturedParty.Characters)
            {
                if (character == null) continue;
                character.CancelAction("scene-transition");
                character.SetReactionControl(this, true);
                var movement = character.GetComponent<CharacterMovement>();
                if (movement != null) movement.Teleport(character.transform.position);
            }
        }

        private void ReleaseControls()
        {
            if (capturedParty != null)
            {
                capturedParty.SetInputCapture(this, false);
                foreach (var character in capturedParty.Characters)
                    if (character != null) character.SetReactionControl(this, false);
            }
            capturedParty = null;
        }

        public bool TryConsumeArrival(string scenePath, string gateId)
            => busy && activeTicket != null
                && tickets.TryConsumeArrival(scenePath, gateId, out _);

        public void ConfirmArrival(string scenePath, string gateId)
        {
            if (activeTicket != null && tickets.Pending == null
                && string.Equals(scenePath, activeTicket.DestinationScene, StringComparison.Ordinal)
                && string.Equals(gateId, activeTicket.DestinationGateId, StringComparison.Ordinal))
                arrivalConfirmed = true;
        }

        private void OnGUI()
        {
            if (fadeAlpha <= 0f) return;
            var oldColor = GUI.color;
            var oldDepth = GUI.depth;
            GUI.depth = -10000;
            GUI.color = new Color(0f, 0f, 0f, fadeAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = oldColor;
            GUI.depth = oldDepth;
        }
    }
}
