/*
 * 脚本：CombatPosture
 * 功能：把本体合法普通命中或盾挡接触交给唯一PostureState；报告破势与解除。
 * 边界：不扣HP、不查世界目标、不找玩家根，不自带Update；无组件时旧场不变。
 * 协作：CharacterRoot/EnemyRoot初始化、Tick、监听并管理自己的权限owner。
 */
using System;
using Ossastra.CharacterCore;
using Ossastra.CombatCore;
using UnityEngine;

namespace Ossastra.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatPosture : MonoBehaviour, ICombatPostureReceiver
    {
        [SerializeField] private float maximum = 100, impactMultiplier = 1;
        [SerializeField] private float staggerDuration = .6f, recoveryFraction = .5f;
        [SerializeField] private float recoveryDelay = 1.5f, recoveryRate = .25f;
        private CombatVitals vitals;
        private Behaviour driver;
        private PostureState state;
        private Guid lastActionId;
        private string lastActorId;
        public double Current => state == null ? 0 : state.Current;
        public double Maximum => state == null ? 0 : state.Maximum;
        public double StaggerRemaining => state == null ? 0 : state.StaggerRemaining;
        public double RegenDelayRemaining => state == null ? 0 : state.RegenDelayRemaining;
        public PosturePhase Phase => state == null ? PosturePhase.Normal : state.Phase;
        public bool IsStaggered => state != null && state.IsStaggered;
        public string LastImpact { get; private set; } = "无";
        public event Action<string> Broken;
        public event Action Recovered;

        public bool Initialize(CombatVitals owner, Behaviour clockOwner)
        {
            if (owner == null || owner.gameObject != gameObject || owner.Health == null ||
                clockOwner == null || clockOwner.gameObject != gameObject) return false;
            if (state != null) return ReferenceEquals(vitals, owner) && ReferenceEquals(driver, clockOwner);
            if (float.IsNaN(impactMultiplier) || float.IsInfinity(impactMultiplier) || impactMultiplier < 0) return false;
            try { state = new PostureState(maximum, staggerDuration, recoveryFraction, recoveryDelay, recoveryRate); }
            catch (ArgumentException) { return false; }
            vitals = owner;
            driver = clockOwner;
            vitals.Died += OnDied;
            return true;
        }

        /// <summary>仅消费同一接触批次存活者的Applied/Guarded结果，倍率由网关计算；不再次Resolve或寻找目标。</summary>
        public void ReceiveImpact(HitOutcome outcome, double impact)
        {
            // 根关闭后不能再留下无人推进的硬直；只依赖显式注入的生命周期，不反查具体根类型。
            if (!isActiveAndEnabled || driver == null || !driver.isActiveAndEnabled ||
                state == null || vitals == null || !vitals.IsAvailable ||
                outcome == null || (outcome.Reason != HitReason.Applied && outcome.Reason != HitReason.Guarded) || outcome.HpAfter <= 0 ||
                outcome.TargetId != vitals.Health.Id || outcome.HpAfter != vitals.Health.Current ||
                outcome.ActionId == Guid.Empty || string.IsNullOrWhiteSpace(outcome.ActorId) ||
                outcome.ActorId == outcome.TargetId ||
                (lastActionId == outcome.ActionId && lastActorId == outcome.ActorId) ||
                double.IsNaN(impact) || double.IsInfinity(impact) || impact <= 0) return;
            lastActionId = outcome.ActionId;
            lastActorId = outcome.ActorId;
            LastImpact = outcome.ActorId + " #" + outcome.ActionId.ToString("N");
            if (state.ApplyImpact(impact * impactMultiplier)) Broken?.Invoke(LastImpact);
        }

        /// <summary>只有本体根调用；失焦/暂停由根传零，硬直结束只通知自身owner。</summary>
        public void Tick(double delta, bool primaryActionActive)
        {
            if (!isActiveAndEnabled || driver == null || !driver.isActiveAndEnabled ||
                state == null || vitals == null || !vitals.IsAvailable) return;
            var wasStaggered = state.IsStaggered;
            state.Tick(delta, primaryActionActive);
            if (wasStaggered && !state.IsStaggered) Recovered?.Invoke();
        }

        public void ResetState()
        {
            if (state == null) return;
            var wasStaggered = state.IsStaggered;
            state.Reset();
            lastActionId = Guid.Empty;
            lastActorId = null;
            LastImpact = "重置";
            if (wasStaggered) Recovered?.Invoke();
        }
        private void OnDied(HitOutcome outcome) { ResetState(); }
        private void OnDisable() { ResetState(); }
        private void OnDestroy()
        {
            ResetState();
            if (vitals != null) vitals.Died -= OnDied;
        }
    }
}
