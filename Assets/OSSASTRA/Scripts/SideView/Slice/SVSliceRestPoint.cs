/*
 * 保存椅复用统一交互输入。显示保存结果之前等待 Commerce 的真实写盘屏障。
 */
using Ossastra.Characters;
using SliceActor = Ossastra.Characters.CharacterRoot;
using Ossastra.Commerce.Runtime;
using Ossastra.Combat;
using Ossastra.SideViewWorld;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ossastra.SideView.Slice
{
    [DisallowMultipleComponent]
    public sealed class SVSliceRestPoint : SVSliceWorldInteractable
    {
        public SideViewSliceProgress progress;
        public CommerceService commerce;
        public CharacterControlCoordinator party;
        public Transform seat;
        public string anchorId;
        public GameObject panel;
        public Button saveButton;
        public Button leaveButton;
        public Text statusLabel;
        private bool open;
        private bool saving;

        public override string Prompt => "休息";

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
            if (saveButton != null) saveButton.onClick.AddListener(Save);
            if (leaveButton != null) leaveButton.onClick.AddListener(Leave);
        }

        protected override bool CanUse(SliceActor actor)
        {
            return !open && actor == party?.Current && progress != null && progress.Ready &&
                commerce != null && commerce.Ready && panel != null &&
                (SideViewTransitionRuntime.Instance == null || !SideViewTransitionRuntime.Instance.Busy);
        }

        public override void Use(SliceActor actor)
        {
            if (!IsAccessible(actor)) return;
            open = true;
            party.SetInputCapture(this, true);
            if (seat != null && actor.TryGetComponent<CharacterMovement>(out var mover))
                mover.Teleport(seat.position);
            foreach (var member in party.Characters)
                if (member != null && member.TryGetComponent<CombatVitals>(out var vitals) && vitals.IsAlive)
                    vitals.Health.Heal(vitals.Health.Maximum);
            panel.SetActive(true);
            SetStatus("选择保存或出发");
            if (saveButton != null) EventSystem.current?.SetSelectedGameObject(saveButton.gameObject);
        }

        private void Save()
        {
            if (open && !saving) StartCoroutine(Flush());
        }

        private IEnumerator Flush()
        {
            saving = true;
            if (saveButton != null) saveButton.interactable = false;
            if (leaveButton != null) leaveButton.interactable = false;
            SetStatus("正在保存…");
            if (commerce.SaveStatus?.HasFailure == true)
            {
                var retry = commerce.RetrySaveAsync();
                while (!retry.IsCompleted) yield return null;
                if (!Succeeded(retry))
                {
                    FinishSave(false);
                    yield break;
                }
            }
            var save = progress.SaveCheckpointAsync(anchorId);
            while (!save.IsCompleted) yield return null;
            FinishSave(Succeeded(save));
        }

        private static bool Succeeded(Task<bool> task)
            => task.Status == TaskStatus.RanToCompletion && task.Result;

        private void FinishSave(bool succeeded)
        {
            SetStatus(succeeded ? "已保存" : "保存失败，可以重试");
            if (saveButton != null) saveButton.interactable = true;
            if (leaveButton != null) leaveButton.interactable = true;
            saving = false;
        }

        private void Leave()
        {
            if (!saving) ClosePanel();
        }

        private void SetStatus(string value)
        {
            if (statusLabel != null) statusLabel.text = value;
        }

        private void ClosePanel()
        {
            if (!open) return;
            open = false;
            if (panel != null) panel.SetActive(false);
            if (party != null) party.SetInputCapture(this, false);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            saving = false;
            ClosePanel();
        }

        private void OnDestroy()
        {
            if (saveButton != null) saveButton.onClick.RemoveListener(Save);
            if (leaveButton != null) leaveButton.onClick.RemoveListener(Leave);
        }
    }
}