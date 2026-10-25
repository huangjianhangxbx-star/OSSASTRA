/* 场景交战资格：仅在场景存在有效敌对成员时观察Accepted，存活威胁维持既有交战。
 * 不拥有Current、HP、动作、角色目标或音乐；动作受理不等于场上仍有敌人。
 * Update只推进原平静脱战窗口。双向根直接发布事实，无全局事件总线。参数为SAMPLE。
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
            // 清场后仍能空挥/练习/施法，但这些Accepted不能凭空重启交战或反复重置calm。
            if (!IsAvailable || fact.Kind != ActionFactKind.Accepted || !enemies.Any(IsActiveOpponent)) return;
            Engaged = true; quietTime = 0;
        }
        private bool IsActiveOpponent(Ossastra.Enemies.EnemyRoot enemy)
        {
            if (enemy == null || !enemy.isActiveAndEnabled || enemy.Vitals == null ||
                !enemy.Vitals.IsAvailable || enemy.Vitals.Faction == CombatFaction.Neutral) return false;
            var opponent = enemy.Vitals;
            // 显式敌方表中的旧引用/友方也不算威胁；复用命中的身份与阵营资格，不复制HP。
            return coordinator.Characters.Any(character => character != null && character.isActiveAndEnabled &&
                character.Vitals != null && character.Vitals.IsAvailable &&
                CombatTargetPolicy.CanHit(character.ActorId, character.Vitals.Faction, true,
                    opponent.Health.Id, opponent.Faction, true));
        }
        private void Update()
        {
            if (!CanSample || !Engaged) return;
            var threat = enemies.Any(e => IsActiveOpponent(e) && (e.ActionActive || e.HasCombatTarget));
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
