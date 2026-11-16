/*
 * 脚本：CombatPosture
 * 功能：给单一实体连接旧受击硬直或横版动作架势账本；报告经过结算的破防事实。
 * 边界：不扣HP、不查世界目标、不找玩家根，不自带Update；无组件时旧场不变。
 * 协作：CharacterRoot/EnemyRoot初始化、Tick、监听并管理自己的权限owner。
 */
using System;
using System.Collections.Generic;
using Ossastra.CharacterCore;
using Ossastra.CombatCore;
using UnityEngine;

namespace Ossastra.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatPosture : MonoBehaviour, IResolvedCombatPostureReceiver
    {
        [SerializeField] private float maximum = 100, impactMultiplier = 1;
        [SerializeField] private float staggerDuration = .6f, recoveryFraction = .5f;
        [SerializeField] private float recoveryDelay = 1.5f, recoveryRate = .25f;
        [SerializeField] private bool actionPostureEnabled;
        [SerializeField, Min(0)] private float actionRecoveryDelay = .2f, actionRecoveryRate = 40;
        [SerializeField, Min(0)] private float actionGuardDrainPerSecond = 10;
        private CombatVitals vitals;
        private Behaviour driver;
        private PostureState state;
        private ActionPostureLedger ledger;
        private Func<bool> reactionActive;
        private HashSet<(Guid Action, string Actor)> resolvedActionImpacts;
        private Guid lastActionId;
        private string lastActorId;
        public bool UsesActionPosture => ledger != null;
        public ActionPostureLedger ActionLedger => ledger;
        public double Current => ledger?.Current ?? state?.Current ?? 0;
        public double Maximum => ledger?.Maximum ?? state?.Maximum ?? 0;
        public double StaggerRemaining => state?.StaggerRemaining ?? 0;
        public double RegenDelayRemaining => ledger?.WaitingSeconds ?? state?.RegenDelayRemaining ?? 0;
        public PosturePhase Phase => IsStaggered ? PosturePhase.Staggered :
            Current < Maximum ? PosturePhase.Recovering : PosturePhase.Normal;
        public bool IsStaggered => UsesActionPosture ? reactionActive?.Invoke() == true : state?.IsStaggered == true;
        public string LastImpact { get; private set; } = "无";
        public event Action<string> Broken;
        public event Action Recovered;
        public event Action<ResolvedPostureContact> ActionPostureBroken;

        public bool Initialize(CombatVitals owner, Behaviour clockOwner)
        {
            if (owner == null || owner.gameObject != gameObject || owner.Health == null ||
                clockOwner == null || clockOwner.gameObject != gameObject) return false;
            if (state != null || ledger != null) return ReferenceEquals(vitals, owner) && ReferenceEquals(driver, clockOwner);
            if (float.IsNaN(impactMultiplier) || float.IsInfinity(impactMultiplier) || impactMultiplier < 0) return false;
            try
            {
                if (actionPostureEnabled)
                {
                    ledger = new ActionPostureLedger(maximum, actionRecoveryDelay, actionRecoveryRate, actionGuardDrainPerSecond);
                    resolvedActionImpacts = new HashSet<(Guid, string)>();
                }
                else state = new PostureState(maximum, staggerDuration, recoveryFraction, recoveryDelay, recoveryRate);
            }
            catch (ArgumentException) { return false; }
            vitals = owner;
            driver = clockOwner;
            vitals.Died += OnDied;
            return true;
        }

        public void BindActionPostureReaction(Func<bool> active) => reactionActive = active;

        public double BeginActionExposure(Guid owner, PostureActionGroup group, double target, bool blocksAllRecovery)
            => ledger == null || !CanAdvance || IsStaggered ? 0 : ledger.Begin(owner, group, target, blocksAllRecovery);

        public void EndActionExposure(Guid owner) => ledger?.End(owner);
        public void SetGuardHold(bool active) => ledger?.SetGuardHold(active && CanAdvance && !IsStaggered);
        public void SetActionRecoveryMultiplier(double multiplier) => ledger?.SetRecoveryMultiplier(multiplier);

        private bool CanAdvance => isActiveAndEnabled && driver != null && driver.isActiveAndEnabled &&
            vitals != null && vitals.IsAvailable;

        /// <summary>已结算的允许破防接触。HP损失与有效架势冲击分别判定。</summary>
        public void ReceiveContact(ResolvedPostureContact contact)
        {
            if (ledger == null || !contact.CanBreak || !CanAdvance || !IsOwnContact(contact.Outcome)) return;
            var outcome = contact.Outcome;
            bool healthLost = outcome.HpBefore > outcome.HpAfter;
            double impact = contact.EffectiveImpact * impactMultiplier;
            if (double.IsNaN(impact) || double.IsInfinity(impact) || impact < 0 ||
                (impact == 0 && !healthLost) || !resolvedActionImpacts.Add((outcome.ActionId, outcome.ActorId))) return;
            RecordImpact(outcome);
            if (ledger.ApplyImpact(impact, healthLost, true, IsStaggered)) ActionPostureBroken?.Invoke(contact);
        }

        /// <summary>仅消费同一接触批次存活者的Applied/Guarded结果，倍率由网关计算；不再次Resolve或寻找目标。</summary>
        public void ReceiveImpact(HitOutcome outcome, double impact)
        {
            // 根关闭后不能再留下无人推进的硬直；只依赖显式注入的生命周期，不反查具体根类型。
            if (state == null || !CanAdvance || !IsOwnContact(outcome) ||
                (lastActionId == outcome.ActionId && lastActorId == outcome.ActorId) ||
                double.IsNaN(impact) || double.IsInfinity(impact) || impact <= 0) return;
            lastActionId = outcome.ActionId;
            lastActorId = outcome.ActorId;
            RecordImpact(outcome);
            if (state.ApplyImpact(impact * impactMultiplier)) Broken?.Invoke(LastImpact);
        }

        private bool IsOwnContact(HitOutcome outcome) => outcome != null &&
            (outcome.Reason == HitReason.Applied || outcome.Reason == HitReason.Guarded) &&
            outcome.HpAfter > 0 && outcome.TargetId == vitals.Health.Id && outcome.HpAfter == vitals.Health.Current &&
            outcome.ActionId != Guid.Empty && !string.IsNullOrWhiteSpace(outcome.ActorId) && outcome.ActorId != outcome.TargetId;

        private void RecordImpact(HitOutcome outcome)
            => LastImpact = outcome.ActorId + " #" + outcome.ActionId.ToString("N");

        /// <summary>只有本体根调用；失焦/暂停由根传零，硬直结束只通知自身owner。</summary>
        public void Tick(double delta, bool primaryActionActive)
        {
            if (!CanAdvance) return;
            if (ledger != null)
            {
                ledger.Tick(delta);
                return;
            }
            if (state == null) return;
            var wasStaggered = state.IsStaggered;
            state.Tick(delta, primaryActionActive);
            if (wasStaggered && !state.IsStaggered) Recovered?.Invoke();
        }

        public void ResetState()
        {
            if (ledger != null)
            {
                ledger.Reset();
                resolvedActionImpacts.Clear();
                LastImpact = "重置";
                return;
            }
            if (state == null) return;
            var wasStaggered = state.IsStaggered;
            state.Reset();
            lastActionId = Guid.Empty;
            lastActorId = null;
            LastImpact = "重置";
            if (wasStaggered) Recovered?.Invoke();
        }

        public void SuspendState()
        {
            if (ledger != null) ledger.Suspend();
            else ResetState();
        }

        private void OnDied(HitOutcome outcome) { ResetState(); }
        private void OnDisable() { SuspendState(); }
        private void OnDestroy()
        {
            ResetState();
            reactionActive = null;
            if (vitals != null) vitals.Died -= OnDied;
        }
    }
}
