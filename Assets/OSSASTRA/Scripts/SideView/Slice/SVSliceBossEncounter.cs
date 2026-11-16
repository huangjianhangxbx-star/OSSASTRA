using Ossastra.CharacterCore;
using Ossastra.Characters;
using SliceEnemyRoot = Ossastra.Enemies.EnemyRoot;
using Ossastra.SideViewWorld;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ossastra.SideView.Slice
{
    public sealed class SVSliceBossEncounter : MonoBehaviour
    {
        public string bossId;
        public SliceEnemyRoot enemy;
        public SideViewSliceProgress progress;
        public CharacterControlCoordinator party;
        public SideViewTransitionGate exit;
        public GameObject barrier;
        private bool defeatedHere;
        private float finishAfter;
        private bool returning;

        private void Start()
        {
            if (enemy == null || progress == null || party == null || enemy.Vitals == null)
            {
                enabled = false;
                return;
            }
            if (progress.IsBossDefeated)
            {
                enemy.gameObject.SetActive(false);
                SetArenaClosed(false);
            }
            else enemy.Vitals.Died += OnKilled;
        }

        private void Update()
        {
            if (defeatedHere)
            {
                var flow = SideViewSliceSessionFlow.Instance;
                if (flow == null) return;
                if (!returning && Time.unscaledTime >= finishAfter)
                    returning = progress.IsBossDefeated
                        ? progress.FinishBossAndReturn() : progress.FinishBossAndReturn(bossId);
                if (returning && !flow.Busy && Keyboard.current?.enterKey.wasPressedThisFrame == true)
                    flow.RetryReturnToTitle();
                return;
            }
            var aliveParty = false;
            foreach (var character in party.Characters)
                aliveParty |= character != null && character.Vitals != null && character.Vitals.IsAlive;
            SetArenaClosed(aliveParty && enemy.Vitals.IsAlive && (barrier != null && barrier.activeSelf || enemy.HasCombatTarget));
        }

        private void OnKilled(HitOutcome _)
        {
            if (defeatedHere) return;
            defeatedHere = true;
            finishAfter = Time.unscaledTime + 3.7f;
            SetArenaClosed(false);
            progress.MarkBossDefeated(bossId);
        }

        private void SetArenaClosed(bool value)
        {
            if (barrier != null && barrier.activeSelf != value) barrier.SetActive(value);
            if (exit != null) exit.enabled = !value;
        }

        private void OnGUI()
        {
            var flow = SideViewSliceSessionFlow.Instance;
            if (!returning || flow == null || flow.Busy || flow.AtTitle) return;
            var box = new Rect(Screen.width * .25f, Screen.height * .78f, Screen.width * .5f, 70);
            GUI.Box(box, flow.Status);
            if (GUI.Button(new Rect(box.center.x - 87, box.y + 34, 174, 29), "重试保存并返回标题"))
                flow.RetryReturnToTitle();
        }

        private void OnDestroy()
        {
            if (enemy != null && enemy.Vitals != null) enemy.Vitals.Died -= OnKilled;
        }
    }
}
