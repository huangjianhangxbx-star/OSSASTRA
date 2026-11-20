/* 动作架势接线。消费原动作接受、移动事实与已复核的 Spine 信号，缺口和时钟由 CombatPosture 持有。 */
using System;
using System.Collections.Generic;
using Ossastra.CharacterAnimation;
using Ossastra.Combat;
using Ossastra.CombatCore;
using UnityEngine;

namespace Ossastra.Characters
{
    internal sealed class CharacterActionPosture
    {
        private readonly Dictionary<Guid, string> recoveryEvents = new Dictionary<Guid, string>();
        private CharacterRoot owner;
        private CombatPosture posture;
        private Guid jump, traverse;
        private SideViewTraversalMode traversalMode;
        private bool leftSupport;
        internal string LastSpineRecovery { get; private set; }

        internal void Initialize(CharacterRoot root, CombatPosture resource)
        {
            owner = root;
            posture = resource;
            float multiplier = root.Definition != null ? root.Definition.actionPostureRecoveryMultiplier : 1;
            if (float.IsInfinity(multiplier) || float.IsNaN(multiplier) || multiplier < 0)
            {
                Debug.LogWarning("无效的架势恢复倍率，使用 1。", root);
                multiplier = 1;
            }
            posture.SetActionRecoveryMultiplier(multiplier);
        }

        internal void Register(CharacterActionRequest request, Guid id, float? target)
        {
            if (posture == null) return;
            switch (request.Kind)
            {
                case CharacterActionKind.Main:
                    if (request.IsAttack)
                        posture.BeginActionExposure(id, PostureActionGroup.MainAttack, target ?? 20, true);
                    break;
                case CharacterActionKind.Dodge:
                    posture.BeginActionExposure(id, PostureActionGroup.Dodge, 25, true);
                    break;
                case CharacterActionKind.Jump:
                    End(jump);
                    jump = id;
                    leftSupport = false;
                    posture.BeginActionExposure(id, PostureActionGroup.VerticalTraversal, 15, true);
                    break;
                case CharacterActionKind.Offhand:
                    if (request.IsAttack)
                        posture.BeginActionExposure(id, PostureActionGroup.OffhandAction, target ?? 15, true);
                    break;
            }
        }

        internal void BindEvent(Guid id, string eventName)
        {
            if (posture != null && id != Guid.Empty && !string.IsNullOrWhiteSpace(eventName))
                recoveryEvents[id] = eventName;
        }

        internal void OnSpineSignal(AnimationSignal signal)
        {
            if (posture == null || signal.ActorId != owner.ActorId || signal.LifeGeneration != owner.LifeGeneration ||
                signal.ActionId != owner.DataBus.ActionId ||
                signal.Track != 0 ||
                !recoveryEvents.TryGetValue(signal.ActionId, out string marker) ||
                !string.Equals(marker, signal.Name, StringComparison.Ordinal)) return;
            LastSpineRecovery = $"{signal.Name} @ {signal.Time:F3} · {signal.ActionId:N}";
            End(signal.ActionId);
        }

        internal void End(Guid id)
        {
            if (id == Guid.Empty) return;
            recoveryEvents.Remove(id);
            posture?.EndActionExposure(id);
        }

        internal void Update()
        {
            if (posture == null || owner.IsSimulationSuspended) return;
            if (posture.IsStaggered)
            {
                Clear();
                return;
            }
            var movement = owner.StateMovement;
            var mode = owner.StateArbitrator.Movement.TraversalMode;
            if (mode != traversalMode)
            {
                bool entering = mode == SideViewTraversalMode.WallSlide ||
                    mode == SideViewTraversalMode.LedgeHang || mode == SideViewTraversalMode.LedgeClimb;
                if (entering)
                {
                    var previous = traverse;
                    traverse = Guid.NewGuid();
                    bool climbing = mode == SideViewTraversalMode.LedgeClimb;
                    // Attach while the old owner still holds this group, then release the earlier phase.
                    var exposed = climbing ? 20 : posture.ActionLedger.GetDeficit(PostureActionGroup.VerticalTraversal);
                    posture.BeginActionExposure(traverse, PostureActionGroup.VerticalTraversal, exposed, climbing);
                    End(previous);
                    End(jump);
                    jump = Guid.Empty;
                }
                else
                {
                    End(traverse);
                    traverse = Guid.Empty;
                }
                traversalMode = mode;
            }

            if (jump != Guid.Empty)
            {
                leftSupport |= !movement.Grounded;
                if (movement.Grounded && leftSupport)
                {
                    End(jump);
                    jump = Guid.Empty;
                }
            }
            bool modernGuard = owner.StateAbilities is CharacterAbilities abilities && abilities.isActiveAndEnabled;
            posture.SetGuardHold(modernGuard ? ((CharacterAbilities)owner.StateAbilities).IsGuarding :
                owner.StateGuard?.IsGuarding == true);
        }

        internal void Clear()
        {
            foreach (var id in recoveryEvents.Keys) posture?.EndActionExposure(id);
            recoveryEvents.Clear();
            LastSpineRecovery = null;
            End(jump);
            End(traverse);
            jump = traverse = Guid.Empty;
            traversalMode = SideViewTraversalMode.Grounded;
            posture?.SetGuardHold(false);
        }
    }
}
