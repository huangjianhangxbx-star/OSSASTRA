/* 功能：主动作状态机的切换技准备、受控受理与原投递完成通知。
 * 边界：沿用现有运行时、费用和匿名命中网关，不交接控制或写坐标。
 * 协作：队伍侧准备空间和身份。实际动作受理后才允许对外发布换人事实。
 */
using System;
using Ossastra.CharacterCore;
using UnityEngine;

namespace Ossastra.Characters
{
    public sealed partial class CharacterCombatStateMachine
    {
        internal readonly struct PreparedSwitchAttack
        {
            internal readonly CharacterActionRequest Request;
            internal readonly ConfiguredActionSnapshot Phase;
            internal readonly float Cost;
            internal readonly string RecoveryMarker;

            internal PreparedSwitchAttack(CharacterActionRequest request, ConfiguredActionSnapshot phase,
                float cost, string recoveryMarker)
            {
                Request = request;
                Phase = phase;
                Cost = cost;
                RecoveryMarker = recoveryMarker;
            }
        }

        private Guid eligibleBasicId;
        private SwitchLinkIdentity eligibleBasicIdentity;
        private bool basicReleased;
        private Guid switchAttackId;
        private Vector3 switchAttackDirection;
        internal event Action<SwitchLinkIdentity, Guid, SwitchLinkReleaseSource> SwitchLinkAttackReleased;
        internal event Action SwitchLinkNewAction;

        internal SwitchLinkIdentity CaptureSwitchIdentity()
            => new SwitchLinkIdentity(owner.ActorId, owner.LifeGeneration, owner.ActionControlRevision);

        private void TrackBasicAttack(Guid actionId, bool player)
        {
            eligibleBasicId = player ? actionId : Guid.Empty;
            eligibleBasicIdentity = player ? CaptureSwitchIdentity() : default;
            basicReleased = false;
        }

        private void RecordBasicRelease(Guid actionId)
        {
            if (eligibleBasicId == Guid.Empty || eligibleBasicId != actionId) return;
            basicReleased = true;
            SwitchLinkAttackReleased?.Invoke(eligibleBasicIdentity, actionId, SwitchLinkReleaseSource.PlayerAttack);
        }

        internal void RecordOptInRelease(SwitchLinkIdentity identity, Guid actionId)
            => SwitchLinkAttackReleased?.Invoke(identity, actionId, SwitchLinkReleaseSource.PlayerOptInAbility);

        internal bool CanYieldForSwitch(Guid releaseId)
            => !IsActive || (runtime.ActionId == eligibleBasicId && basicReleased &&
                (runtime.ActionId == releaseId || runtime.AttackReady));

        internal void ExitForSwitch()
        {
            ClearBuffer();
            SuppressInput();
            if (!runtime.IsActive) return;
            owner.StateMainAction?.CloseAttachedContacts(runtime.ActionId);
            CancelOwnedMainAction(runtime.ActionId, "switch-link-handover");
        }

        internal void ApplySwitchFacing(Vector3 direction)
        {
            owner.StateOrientation.ResolveDirections(new CharacterInputFrame { move = new Vector2(direction.x, 0) },
                direction, false, false);
            owner.StateOrienter.FaceAttackDirection();
        }

        internal bool TryPrepareSwitchAttack(ActionDefinitionSO definition, out PreparedSwitchAttack prepared, out string reason)
        {
            prepared = default;
            if (definition == null)
            {
                reason = "切换技动作引用缺失";
                return false;
            }
            if (!TryResolveConfiguredAction(definition, out var phase, out reason, new CharacterAnimationPreparation(true, 1)))
                return false;
            if (phase.Timeline.WaitsForPhaseChange)
            {
                reason = "切换技应使用可正常释放的单段动作";
                return false;
            }
            var exposure = definition.actionPostureExposure == -1 ? 20 : definition.actionPostureExposure;
            var request = owner.Actions.CreateRequest(CharacterActionKind.Main, CharacterActionSource.SwitchLink, "switch-link")
                .WithPostureTarget(exposure);
            var admission = (CharacterActionCoordinator)owner.Actions;
            if (!admission.CanPrepareSwitchAttack(request, definition.staminaCost, out reason)) return false;
            var marker = string.IsNullOrWhiteSpace(definition.postureRecoveryEvent)
                ? owner.PostureBasicRecoveryEvent : definition.postureRecoveryEvent;
            prepared = new PreparedSwitchAttack(request, phase, definition.staminaCost, marker);
            return true;
        }

        internal bool TryCommitSwitchAttack(PreparedSwitchAttack prepared, Vector3 direction, Func<bool> enter,
            Action handOver, out string reason)
        {
            var admission = (CharacterActionCoordinator)owner.Actions;
            ActionRequestResult accepted = null;
            if (!admission.TryCommitSwitchAttack(prepared.Request, prepared.Cost, () =>
            {
                // 准入已复核。入场若失效，不启动动作或扣费。
                if (!enter()) return Guid.Empty;
                accepted = runtime.Request(prepared.Phase.Timeline, "switch-link", owner.StateAuthority, owner.IsPaused);
                if (!accepted.Accepted)
                    throw new InvalidOperationException("Switch attack readiness changed during grounded placement.");
                return accepted.Fact.ActionId;
            }, out reason)) return false;

            handOver();
            if (accepted.PreviousTerminal != null)
            {
                owner.StateMainAction?.CloseAttachedContacts(accepted.PreviousTerminal.ActionId);
                ObserveFact(accepted.PreviousTerminal);
            }
            chain?.Clear();
            autonomousAction = false;
            autonomousTarget = null;
            AttackStage = 1;
            switchAttackId = accepted.Fact.ActionId;
            switchAttackDirection = direction;
            TrackBasicAttack(switchAttackId, false);
            ApplySwitchFacing(direction);
            BindPostureRecovery(switchAttackId, prepared.Phase, prepared.RecoveryMarker);
            ((ICharacterConfiguredAnimation)owner.StatePresentation).CapturePhase(switchAttackId, prepared.Phase);
            ObserveFact(accepted.Fact);
            owner.StateOrienter.FaceAttackDirection();
            return true;
        }

        private void ObserveSwitchActionFact(ActionFact fact)
        {
            if (fact.Kind == ActionFactKind.Accepted)
                SwitchLinkNewAction?.Invoke();
            else if (fact.Kind == ActionFactKind.Finished || fact.Kind == ActionFactKind.Cancelled)
            {
                if (fact.ActionId == switchAttackId) switchAttackId = Guid.Empty;
                if (fact.ActionId == eligibleBasicId)
                {
                    eligibleBasicId = Guid.Empty;
                    basicReleased = false;
                }
            }
        }
    }
}
