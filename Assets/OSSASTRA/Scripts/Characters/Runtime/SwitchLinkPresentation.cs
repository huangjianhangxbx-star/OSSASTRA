/* 脚本：SwitchLinkPresentation
 * 功能：消费一次已提交交接，提供真实时间包络和相机推近请求。
 * 边界：相机由 SideViewCameraFollow 写入。切换音由现有 CombatSfxFactAdapter 观察主控变更后提交。
 * 关键时序：完整交接后开始，失去控制或世界资格时释放，只恢复本次请求仍持有的时间值。
 */
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ossastra.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterControlCoordinator))]
    [DefaultExecutionOrder(80)]
    public sealed class SwitchLinkPresentation : MonoBehaviour
    {
        [SerializeField] private SideViewCameraFollow cameraFollow;
        [SerializeField, Range(1f, 1.4f)] private float zoomMultiplier = 1.07f;
        [SerializeField, Range(.1f, 1f)] private float timeMultiplier = .65f;
        [SerializeField, Min(0f)] private float enterSeconds = .06f;
        [SerializeField, Min(0f)] private float holdSeconds = .15f;
        [SerializeField, Min(0f)] private float leaveSeconds = .24f;

        private static SwitchLinkPresentation activeOwner;
        private readonly SwitchLinkPresentationClock clock = new SwitchLinkPresentationClock();
        private CharacterControlCoordinator session;
        private CharacterRoot outgoing, incoming;
        private CharacterInputHandler input;
        private int incomingRevision;

        public Guid ExchangeId => clock.Active ? clock.ExchangeId : Guid.Empty;
        public float CurrentZoom { get; private set; } = 1f;

        private void Awake() => session = GetComponent<CharacterControlCoordinator>();

        private void OnEnable() => SceneManager.activeSceneChanged += OnSceneChanged;

        /// <summary>仅在两名角色都已完成交接提交之后调用。</summary>
        public bool PlayCommitted(Guid exchangeId, CharacterRoot previous, CharacterRoot selected)
        {
            if (!isActiveAndEnabled || session == null || activeOwner != null || !CanPresent(previous, selected)) return false;
            var controller = selected.GetComponent<CharacterInputHandler>();
            if (controller == null || !controller.isActiveAndEnabled || controller.InventoryCaptured ||
                !clock.TryBegin(exchangeId, Time.unscaledTimeAsDouble, enterSeconds, holdSeconds, leaveSeconds, Time.timeScale))
                return false;

            activeOwner = this;
            outgoing = previous;
            incoming = selected;
            input = controller;
            incomingRevision = selected.ActionControlRevision;
            outgoing.Died += OnActorDied;
            incoming.Died += OnActorDied;
            return true;
        }

        private bool CanPresent(CharacterRoot previous, CharacterRoot selected) =>
            cameraFollow != null && cameraFollow.isActiveAndEnabled && session.isActiveAndEnabled &&
            previous != null && selected != null && selected != previous && session.Current == selected &&
            previous.isActiveAndEnabled && selected.isActiveAndEnabled && selected.IsAlive && previous.IsAlive &&
            !selected.IsSimulationSuspended && !previous.IsSimulationSuspended;

        private void Update()
        {
            if (!clock.Active) return;
            if (!CanPresent(outgoing, incoming) || input == null || !input.isActiveAndEnabled || input.InventoryCaptured ||
                incoming.ActionControlRevision != incomingRevision || Time.timeScale <= 0f ||
                !clock.TrySample(Time.unscaledTimeAsDouble, out float weight))
            {
                Cancel();
                return;
            }

            CurrentZoom = Mathf.Lerp(1f, zoomMultiplier, weight);
            cameraFollow.SetSwitchZoom(CurrentZoom);
            float next = clock.ApplyTime(Time.timeScale, timeMultiplier, weight);
            if (next != Time.timeScale) Time.timeScale = next;
        }

        /// <summary>释放本次交接的投影与时间，不改变主控和动作。</summary>
        public void Cancel()
        {
            if (!clock.Active) return;
            float observed = Time.timeScale;
            float restored = clock.Release(observed);
            if (activeOwner == this)
            {
                if (restored != observed) Time.timeScale = restored;
                activeOwner = null;
            }

            if (cameraFollow != null) cameraFollow.ClearSwitchZoom();
            if (outgoing != null) outgoing.Died -= OnActorDied;
            if (incoming != null) incoming.Died -= OnActorDied;
            outgoing = incoming = null;
            input = null;
            CurrentZoom = 1f;
        }

        private void OnActorDied(CharacterRoot _) => Cancel();
        private void OnSceneChanged(Scene previous, Scene selected) => Cancel();
        private void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
        private void OnApplicationPause(bool paused) { if (paused) Cancel(); }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            Cancel();
        }
    }
}
