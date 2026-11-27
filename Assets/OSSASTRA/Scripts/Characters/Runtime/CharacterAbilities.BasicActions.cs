/* 功能：把普通动作的既有受理身份接入能力 Run，并负责释放归属及真实接触路由。
 * 边界：普通攻击的资源支付、动画和身体时钟只归原动作事务。 */
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.CharacterCore;
using Ossastra.CharacterCore.Abilities;
using Ossastra.Combat;
using UnityEngine;

namespace Ossastra.Characters
{
    public sealed partial class CharacterAbilities
    {
        private readonly Dictionary<Guid, Action<CombatContactBatch>> contactRoutes =
            new Dictionary<Guid, Action<CombatContactBatch>>();

        private sealed class BasicActionSource : IAbilitySource
        {
            private readonly CharacterRoot root;
            private readonly CharacterEquipment equipment;
            private readonly string weaponId, selection;
            private readonly int controlRevision;
            public string OwnerId => root.ActorId;
            public string InstanceId { get; }
            public EquipmentAbilityState RuntimeState { get; }
            public bool IsAvailable => root != null && root.IsAlive && root.ActionControlRevision == controlRevision &&
                equipment?.State?.RightInstanceId == weaponId &&
                equipment?.State?.RightRuntime?.MainActionProfileId == selection;

            public BasicActionSource(CharacterRoot root, CharacterEquipment equipment, bool weaponSource)
            {
                this.root = root;
                this.equipment = equipment;
                weaponId = equipment?.State?.RightInstanceId;
                selection = equipment?.State?.RightRuntime?.MainActionProfileId;
                controlRevision = root.ActionControlRevision;
                InstanceId = weaponSource ? weaponId : root.ActorId + ":basic";
                // 无装备仍可使用专职普攻，空 payload 只为兼容原 Context 的只读访问。
                RuntimeState = equipment?.State?.RightRuntime ?? new EquipmentAbilityState();
            }

            public bool TryReserve(Guid id) => id != Guid.Empty && IsAvailable;
            public bool CommitRelease(Guid id) => id != Guid.Empty && IsAvailable;
            public void CancelReservation() { }
        }

        internal bool PrepareBasicBehavior(ActionDefinitionSO definition, int stage, bool weaponSource,
            out IAbilitySource source, out CompiledAbilityExecutor executor, out string reason)
        {
            source = null;
            executor = null;
            reason = null;
            if (!isActiveAndEnabled || owner == null || runs.Count >= 8)
            {
                reason = "专职行为宿主不可用或执行预算已满";
                return false;
            }

            source = new BasicActionSource(owner, equipment, weaponSource);
            try
            {
                executor = definition.executor.CompileExecutor()?.Prepare(owner, source,
                    new CharacterAnimationPreparation(true, stage, useAuthoredLogic: true), definition);
                if (executor == null || !executor.SupportsBasicAction || !executor.UsesBodyAction ||
                    executor.InitialPhase != null || !source.IsAvailable || !executor.CanUseSource(source))
                    throw new ArgumentException("此执行器或当前来源不支持接入普通攻击");
                return true;
            }
            catch (ArgumentException error)
            {
                reason = error.Message;
                return false;
            }
        }

        internal void AttachBasicBehavior(ActionDefinitionSO definition, IAbilitySource source,
            CompiledAbilityExecutor executor, Guid id, int stage, bool companion, ActionImpactRun impact,
            Func<bool> valid, Action<bool, string, CombatVitals> complete, Action<string, CombatVitals, string> report)
        {
            var frozen = new CompiledAbility(definition, executor);
            var direction = owner.StateOrienter.AttackDirection;
            var context = new AbilityRunContext(owner, frozen, source, id, direction, owner.transform.position, Emit,
                AbilityOperation.Contextual, null, .25f, SnapshotEffects(),
                requestSource: companion ? CharacterActionSource.Companion : CharacterActionSource.Player)
            {
                impactRun = impact, isBasicBehavior = true, basicStage = stage,
                behaviorValid = valid, behaviorComplete = complete, behaviorReport = report
            };
            ConfigureRunPorts(context);
            try { RegisterRun(context, executor); }
            catch (Exception ex)
            {
                // 已受理动作继续遵守原动画，失败后也不能漏出原模板近战。
                ownedBodyReleases.Add(id);
                context.CompleteBehavior(false, "初始化失败");
                Debug.LogException(ex, this);
            }
        }

        internal void ReleaseBasicBehavior(ActionFact fact, Vector3 direction)
        {
            var run = runs.FirstOrDefault(item => item.Context.isBasicBehavior && item.Context.id == fact.ActionId);
            if (run == null || run.Ended || run.Context.FrozenBasicRelease != null) return;
            var context = run.Context;
            context.FrozenBasicRelease = fact.Release;
            context.BasicReleaseDirection = direction;
            var deliver = context.releaseDeferredDeliveries;
            context.releaseDeferredDeliveries = null;
            deliver?.Invoke();
            if (!run.Ended) run.OnActionFact(fact);
        }

        private static void CancelBasicBehavior(AbilityRun run, string reason)
        {
            if (run.Ended) return;
            run.Context.CompleteBehavior(false, reason);
            run.Context.NotifyFeedback("cancel");
            run.Cancel();
        }

        private void ConfigureRunPorts(AbilityRunContext context)
        {
            context.commitSource = CommitSource;
            context.deliveryPort = EmitDelivery;
        }

        private void RegisterRun(AbilityRunContext context, CompiledAbilityExecutor executor)
        {
            var id = context.id;
            AbilityRun run = null;
            var pending = new Queue<Action>();
            var deferredDeliveries = context.isBasicBehavior ? new Queue<Action>() : pending;
            void Route(Action callback)
            {
                if (run == null) pending.Enqueue(callback);
                else callback();
            }

            // 先安装回执路由。普通攻击的构造期投递还需要等待真实动画释放。
            context.deliveryPort = (current, emission, delivery, channel, posture, flightPosition) =>
            {
                if (run != null) return EmitDelivery(current, emission, delivery, channel, posture, flightPosition);
                deferredDeliveries.Enqueue(() => EmitDelivery(current, emission, delivery, channel, posture, flightPosition));
                return true;
            };
            var originalCommit = context.commitSource;
            bool CanCommit(AbilityRunContext current) =>
                (!current.isBasicBehavior || current.FrozenBasicRelease != null) && originalCommit(current);
            context.commitSource = current => run != null && CanCommit(current);
            var feedback = context.feedback;
            if (feedback != null) context.feedback = (execution, kind, point) => Route(() => feedback(execution, kind, point));
            context.onResolved = (index, channel, receipts) => Route(() =>
            {
                if (context.isBasicBehavior)
                    owner.StateArbitrator.Combat.OnHitReceipt(id, receipts.Any(hit => hit.Reason == HitReason.Applied));
                if (!context.isBasicBehavior || !run.Ended) run.OnResolved(index, channel, receipts);
            });
            if (contactRoutes.Count == 0) CombatContactGateway.Resolved += RouteBehaviorContact;
            contactRoutes.Add(id, batch => Route(() => { if (!run.Ended) run.OnContact(batch); }));
            owner.Vitals?.Recovery?.RetainParent(id);
            deliveries.RegisterParent(id);
            if (executor.UsesBodyAction) ownedBodyReleases.Add(id);

            try
            {
                run = executor.CreateRun(context) ?? throw new InvalidOperationException("执行器没有返回 Run");
                runs.Add(run);
                context.deliveryPort = EmitDelivery;
                context.commitSource = CanCommit;
                context.feedback = feedback;
                if (context.isBasicBehavior && deferredDeliveries.Count > 0)
                    context.releaseDeferredDeliveries = () =>
                    {
                        while (!run.Ended && deferredDeliveries.Count > 0) deferredDeliveries.Dequeue()();
                    };
                while (pending.Count > 0) pending.Dequeue()();
            }
            catch
            {
                runs.Remove(run);
                run?.Cancel();
                contactRoutes.Remove(id);
                if (contactRoutes.Count == 0) CombatContactGateway.Resolved -= RouteBehaviorContact;
                ownedBodyReleases.Remove(id);
                owner.Vitals?.Recovery?.ReleaseParent(id);
                deliveries.CancelParentFlights(id);
                deliveries.CompleteParent(id);
                context.source.CancelReservation();
                stamina?.CancelReservation(context.releasePaymentId);
                throw;
            }
        }

        private void RouteBehaviorContact(CombatContactBatch batch)
        {
            if (batch.Source == owner.Vitals && contactRoutes.TryGetValue(batch.ParentExecutionId, out var route))
                route(batch);
        }
    }
}
