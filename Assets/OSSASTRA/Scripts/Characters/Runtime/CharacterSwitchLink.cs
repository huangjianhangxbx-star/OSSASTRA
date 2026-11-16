/* 功能：核对队伍的出手机会、地面位置和入场动作，再从手动换人入口提交连携。
 * 边界：角色控制归原队伍协调器，移动和动作各用原状态机，演出只消费成功事实。
 * 协作：释放回调只登记机会。之后队伍更新中的同一次手势选择连携或普通切换。
 */
using System;
using System.Collections.Generic;
using Ossastra.CharacterCore;
using Ossastra.Combat;
using UnityEngine;

namespace Ossastra.Characters
{
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterControlCoordinator))]
    public sealed class CharacterSwitchLink : MonoBehaviour
    {
        public enum SwitchAttackMode { Action, None }

        [Serializable]
        private sealed class SwitchAttackSlot
        {
            public CharacterRoot actor;
            public SwitchAttackMode mode;
            public ActionDefinitionSO action;
        }

        private const float DefaultRetreatBodyWidths = 27.5f;

        [SerializeField, Min(.01f)] private float opportunitySeconds = 1f;
        [SerializeField, Min(0f)] private float cooldownSeconds = 2.5f;
        [SerializeField, Min(0f)] private float retreatBodyWidths = DefaultRetreatBodyWidths;
        [SerializeField, Min(.01f)] private float retreatSeconds = .2f;
        [SerializeField] private SwitchAttackSlot[] slots = Array.Empty<SwitchAttackSlot>();
        [SerializeField] private SwitchLinkPresentation presentation;
        [SerializeField] private bool showDiagnostics;

        private readonly List<Action> unsubscribe = new List<Action>();
        private CharacterControlCoordinator control;
        private SwitchLinkWindow window;
        private double combatTime;
        private int handoverFrame = -1;
        private bool started;
        private SwitchLinkOpportunity latest;
        private CharacterRoot lastOutgoing;

        public int LinkedSwitchCount { get; private set; }
        public double CooldownRemaining => window?.CooldownRemaining(combatTime) ?? 0;
        public string LastResult { get; private set; } = "尚未连携";

        private readonly struct Exchange
        {
            internal readonly CharacterRoot Outgoing, Incoming;
            internal readonly SwitchLinkIdentity Identity;
            internal readonly SwitchLinkOpportunity Opportunity;
            internal readonly SideViewSwitchPlacement Placement;
            internal readonly CharacterCombatStateMachine.PreparedSwitchAttack Attack;
            internal readonly Vector3 Direction;
            internal readonly float Retreat;
            internal readonly bool HasAttack;

            internal Exchange(CharacterRoot outgoing, CharacterRoot incoming, SwitchLinkIdentity identity,
                SwitchLinkOpportunity opportunity, SideViewSwitchPlacement placement,
                CharacterCombatStateMachine.PreparedSwitchAttack attack, Vector3 direction, float retreat, bool hasAttack)
            {
                Outgoing = outgoing;
                Incoming = incoming;
                Identity = identity;
                Opportunity = opportunity;
                Placement = placement;
                Attack = attack;
                Direction = direction;
                Retreat = retreat;
                HasAttack = hasAttack;
            }
        }

        private void Awake()
        {
            control = GetComponent<CharacterControlCoordinator>();
            window = new SwitchLinkWindow(Finite(opportunitySeconds, 1, .01f), Finite(cooldownSeconds, 2.5f, 0));
        }

        private void Start()
        {
            started = true;
            BindCombat();
        }

        private void OnEnable()
        {
            if (started) BindCombat();
        }

        private void BindCombat()
        {
            foreach (var actor in control.Characters)
            {
                var combat = actor != null ? actor.StateArbitrator?.Combat : null;
                if (combat == null) continue;
                void Release(SwitchLinkIdentity identity, Guid action, SwitchLinkReleaseSource source)
                    => RecordRelease(actor, identity, action, source);
                void NewAction()
                {
                    if (actor == control.Current) ClearOpportunity();
                }
                combat.SwitchLinkAttackReleased += Release;
                combat.SwitchLinkNewAction += NewAction;
                unsubscribe.Add(() =>
                {
                    combat.SwitchLinkAttackReleased -= Release;
                    combat.SwitchLinkNewAction -= NewAction;
                });
            }
        }

        private void OnDisable()
        {
            foreach (var release in unsubscribe) release();
            unsubscribe.Clear();
            Interrupt();
        }

        internal void Advance(float scaledDelta)
        {
            if (!isActiveAndEnabled) return;
            if (scaledDelta > 0) combatTime += scaledDelta;
            var current = control.Current;
            if (!CanAct(current) || !control.CanManuallySwitch)
                Interrupt();
        }

        private void RecordRelease(CharacterRoot actor, SwitchLinkIdentity accepted, Guid id, SwitchLinkReleaseSource source)
        {
            // 这一回调可发生在原 Runtime 遍历里。只更新窗口，不写控制或位置。
            if (actor != control.Current || !CanAct(actor) || !control.CanManuallySwitch) return;
            window.ObserveRelease(accepted, IdentityOf(actor), id, source, combatTime);
        }

        private void ClearOpportunity() => window?.ClearOpportunity();

        internal void Interrupt()
        {
            ClearOpportunity();
            if (presentation != null) presentation.Cancel();
        }

        internal bool BlocksCompanion(CharacterRoot actor)
            => Time.frameCount == handoverFrame || actor.StateArbitrator?.Movement?.IsSwitchRetreatActive == true;

        public bool CanShowReady()
        {
            if (!isActiveAndEnabled || !control.CanManuallySwitch || !control.TryGetNext(out var next))
                return false;
            return TryPrepare(next, out _, out _, prepareRetreat: false);
        }

        internal bool TrySwitch(CharacterRoot incoming)
        {
            string reason = "连携停用";
            if (!isActiveAndEnabled || !TryPrepare(incoming, out var exchange, out reason))
            {
                LastResult = reason;
                return false;
            }

            bool Enter() => incoming.StateMovement.TryCommitSwitchPlacement(exchange.Placement);
            void HandOver()
            {
                // 同一个战斗时间内，准备对象对应唯一机会。普通切换的清理路径不介入这次消费。
                if (!window.TryCommit(exchange.Opportunity, exchange.Identity, combatTime))
                    throw new InvalidOperationException("The prepared switch opportunity changed during commit.");
                handoverFrame = Time.frameCount;
                control.CommitLinkedSwitch(exchange.Outgoing, incoming);
                exchange.Outgoing.StateArbitrator.Combat.ExitForSwitch();
                exchange.Outgoing.StateArbitrator.Movement.BeginSwitchRetreat(exchange.Retreat,
                    Finite(retreatSeconds, .2f, .01f));
            }

            if (exchange.HasAttack)
            {
                if (!incoming.StateArbitrator.Combat.TryCommitSwitchAttack(exchange.Attack, exchange.Direction,
                    Enter, HandOver, out reason))
                {
                    LastResult = reason;
                    return false;
                }
            }
            else
            {
                if (!Enter())
                {
                    LastResult = "入场落点已失效";
                    return false;
                }
                incoming.StateArbitrator.Combat.ApplySwitchFacing(exchange.Direction);
                HandOver();
                incoming.StateArbitrator.Combat.ExitForSwitch();
            }
            lastOutgoing = exchange.Outgoing;
            latest = exchange.Opportunity;
            LinkedSwitchCount++;
            LastResult = exchange.HasAttack ? "交接与切换技已受理" : "仅交接";
            if (presentation != null)
                presentation.PlayCommitted(exchange.Opportunity.ExecutionId, exchange.Outgoing, incoming);
            return true;
        }

        private bool TryPrepare(CharacterRoot incoming, out Exchange exchange, out string reason, bool prepareRetreat = true)
        {
            exchange = default;
            var outgoing = control.Current;
            if (outgoing == null || incoming == null || incoming == outgoing || control.Characters.Count != 2)
            {
                reason = "需要两名不同的可控角色";
                return false;
            }
            var identity = IdentityOf(outgoing);
            if (!window.TryPrepare(identity, combatTime, out var opportunity, out var status))
            {
                reason = status.ToString();
                return false;
            }
            if (!StableGround(outgoing) || !StableGround(incoming) ||
                !outgoing.StateArbitrator.Combat.CanYieldForSwitch(opportunity.ExecutionId) ||
                !incoming.StateArbitrator.Combat.CanYieldForSwitch(Guid.Empty))
            {
                reason = "角色未处于可交接的地面动作";
                return false;
            }
            SwitchAttackSlot slot = null;
            foreach (var candidate in slots)
            {
                if (candidate == null || candidate.actor != incoming) continue;
                if (slot != null)
                {
                    reason = "切换技槽重复";
                    return false;
                }
                slot = candidate;
            }
            if (slot == null || (slot.mode != SwitchAttackMode.Action && slot.mode != SwitchAttackMode.None))
            {
                reason = "切换技槽缺失或模式无效";
                return false;
            }
            bool hasAttack = slot.mode == SwitchAttackMode.Action;
            CharacterCombatStateMachine.PreparedSwitchAttack attack = default;
            if (hasAttack && !incoming.StateArbitrator.Combat.TryPrepareSwitchAttack(slot.action, out attack, out reason))
                return false;
            if (!incoming.StateMovement.TryPrepareSwitchPlacement(outgoing.StateMovement, out var placement))
            {
                reason = "入场空间或地面支撑不足";
                return false;
            }
            if (!CombatSight.IsClear(outgoing.transform, incoming.transform, .5f))
            {
                reason = "两名角色之间有实体阻挡";
                return false;
            }
            var orientation = outgoing.StateOrientation;
            float facing = outgoing.ActionActive ? orientation.PresentationDirection.x : orientation.VisualFacingX;
            int side = (Mathf.Abs(facing) > .01f ? facing : orientation.VisualFacingX) < 0 ? -1 : 1;
            var retreat = prepareRetreat
                ? outgoing.StateMovement.PrepareSwitchRetreat(side, Finite(retreatBodyWidths, DefaultRetreatBodyWidths, 0)) : 0f;
            exchange = new Exchange(outgoing, incoming, identity, opportunity, placement, attack,
                Vector3.right * side, retreat, hasAttack);
            reason = null;
            return true;
        }

        private static SwitchLinkIdentity IdentityOf(CharacterRoot actor)
            => new SwitchLinkIdentity(actor.ActorId, actor.LifeGeneration, actor.ActionControlRevision);

        private static bool CanAct(CharacterRoot actor)
            => actor != null && actor.CanSimulateIntent && actor.Vitals?.IsHitReacting != true;

        private static bool StableGround(CharacterRoot actor)
            => CanAct(actor) && actor.StateMovement?.IsSideView == true && actor.StateMovement.Grounded &&
                actor.StateArbitrator.Movement.TraversalMode == SideViewTraversalMode.Grounded &&
                !actor.StateArbitrator.Movement.IsSwitchRetreatActive && actor.StateStatusEffects?.HasForcedMotion != true &&
                actor.StateArbitrator.Dodge.IsActive == false && actor.StateOffhand?.IsAccepting != true &&
                actor.StateGuard?.IsGuarding != true && (actor.StateAbilities as CharacterAbilities)?.IsBusy != true;

        private static float Finite(float value, float fallback, float minimum)
            => float.IsNaN(value) || float.IsInfinity(value) || value < minimum ? fallback : value;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            if (!showDiagnostics || !Application.isPlaying || window == null) return;
            var current = control.Current;
            var remaining = current != null ? window.OpportunityRemaining(IdentityOf(current), combatTime) : 0;
            var distance = lastOutgoing != null ? lastOutgoing.StateArbitrator.Movement.LastSwitchRetreatDistance : 0;
            GUILayout.BeginArea(new Rect(12, 352, 390, 90), GUI.skin.box);
            GUILayout.Label($"切换连携 {remaining:0.00}s　共享冷却 {CooldownRemaining:0.00}s　时间 {Time.timeScale:0.##}");
            GUILayout.Label($"退场位移 {distance:0.00}　出手 {latest.ExecutionId.ToString("N").Substring(0, 8)}");
            GUILayout.Label(LastResult);
            GUILayout.EndArea();
        }
#endif
    }
}
