/* 场景交战资格：仅观察真实Accepted及存活威胁；不拥有Current、HP、动作或角色目标。
 * Update只推进平静脱战窗口。双向根直接发布事实，无全局事件总线。参数为SAMPLE。
 */
using System;
using System.Linq;
using Ossastra.CharacterCore;
using Ossastra.Characters;
using Ossastra.Enemies;
using UnityEngine;
namespace Ossastra.Combat
{
    [DefaultExecutionOrder(-140), DisallowMultipleComponent]
    public sealed class CombatEngagementSession : MonoBehaviour
    {
        [SerializeField] private CharacterControlCoordinator coordinator;
        [SerializeField] private Ossastra.Enemies.EnemyRoot[] enemies = Array.Empty<Ossastra.Enemies.EnemyRoot>();
        [SerializeField, Min(.1f)] private float quietDuration = 3;
        private float quietTime;
        public bool Engaged { get; private set; }
        public CharacterControlCoordinator Coordinator => coordinator;
        public Ossastra.Enemies.EnemyRoot[] Enemies => (Ossastra.Enemies.EnemyRoot[])enemies.Clone();
        public bool IsAvailable => isActiveAndEnabled && coordinator != null && coordinator.isActiveAndEnabled;
        public bool CanSample => IsAvailable && coordinator.IsIntentSamplingAllowed &&
            !coordinator.Characters.Any(c => c != null && c.IsSimulationSuspended);
        public float QuietTime => quietTime;
        private void OnEnable()
        {
            if (coordinator == null) return;
            // 同一控制会话只能有一个资格观察者；重复配置显式拒绝，不争夺Current。
            if (FindObjectsOfType<CombatEngagementSession>().Any(other => other != this && other.isActiveAndEnabled && other.coordinator == coordinator))
            { enabled=false; return; }
            foreach (var c in coordinator.Characters) if(c != null) c.ActionObserved += OnFact;
            foreach (var e in enemies) if(e != null) e.ActionObserved += OnFact;
        }
        private void OnFact(ActionFact fact)
        {
            if (!IsAvailable || fact.Kind != ActionFactKind.Accepted) return;
            Engaged = true; quietTime = 0;
        }
        private void Update()
        {
            if (!CanSample || !Engaged) return;
            var threat = enemies.Any(e => e != null && e.isActiveAndEnabled && e.Vitals != null &&
                e.Vitals.IsAvailable && (e.ActionActive || e.HasCombatTarget));
            if (threat) quietTime = 0;
            else { quietTime += Time.deltaTime; if(quietTime >= quietDuration) Engaged = false; }
        }
        private void OnDisable()
        {
            if (coordinator != null) foreach(var c in coordinator.Characters) if(c != null) c.ActionObserved -= OnFact;
            foreach(var e in enemies) if(e != null) e.ActionObserved -= OnFact;
            Engaged = false; quietTime = 0;
        }
    }
}
