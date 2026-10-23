/* AM02：保留原移动/装备姿态过渡选择算法；移除未接通旧FacingController分支。
 * 接续延迟交由Handle按真实TrackComplete与TimeScale计算，不使用固定0.05秒。 */
namespace Ossastra.CharacterAnimation
{
using System.Collections.Generic;
using UnityEngine;

public class AnimationTransitionRouter
{
    private static readonly HashSet<string> warnedKeys = new HashSet<string>();

    private class TransitionDecision
    {
        public bool hasTransitionIntent;
        public bool shouldApply;

        public EAnimationLogicState transitionLogicState = EAnimationLogicState.None;

        public TransitionSuppressReason suppressReason = TransitionSuppressReason.None;
        public bool shouldWarnWhenSkipped = true;


        public string reason;
    }

    public CharacterAnimationPlan BuildTransitionedPlan(AnimationIntent intent,
        EAnimationLogicState previousLogicState, EAnimationLogicState targetLogicState,
        CharacterAnimationPlan targetPlan, AnimationComposeContext context, CharacterAnimationConfigManager configManager)
    {
        if (intent == null || targetPlan == null || context == null || configManager == null || intent.oneShot) return targetPlan;
        var decision = EvaluateTransition(intent, previousLogicState, targetLogicState, context);
        if (decision == null || !decision.hasTransitionIntent || !decision.shouldApply) return targetPlan;
        var transition = configManager.ResolvePlan(decision.transitionLogicState, context);
        if (transition == null) return targetPlan;
        return CombineTransitionAndTarget(transition, targetPlan);
    }



    private TransitionDecision EvaluateTransition(
    AnimationIntent intent,
    EAnimationLogicState previousLogicState,
    EAnimationLogicState targetLogicState,
    AnimationComposeContext context)
    {
        /*
         * 以后可以自己扩展：
         *
         * Idle -> StartMove
         * Move -> Stop
         * Run -> Brake
         * Ground -> JumpStart
         * Air -> Land
         * DrawWeapon -> Aim
         * ReloadStart -> ReloadLoop
         */

        TransitionDecision weaponPoseDecision =
            EvaluateWeaponPoseTransition(intent, previousLogicState, targetLogicState, context);

        if (weaponPoseDecision != null && weaponPoseDecision.hasTransitionIntent)
            return weaponPoseDecision;

        //TransitionDecision turnAroundDecision =
        //    EvaluateTurnAround(intent, targetLogicState, context);

        //if (turnAroundDecision != null && turnAroundDecision.hasTransitionIntent)
        //    return turnAroundDecision;

        TransitionDecision movementDecision =
            EvaluateMovementTransition(intent, previousLogicState, targetLogicState, context);

        if (movementDecision != null && movementDecision.hasTransitionIntent)
            return movementDecision;

        return null;
    }

    private CharacterAnimationPlan CombineTransitionAndTarget(CharacterAnimationPlan transitionPlan, CharacterAnimationPlan targetPlan)
    {
        var result = transitionPlan.CloneFor(targetPlan.characterRoot);
        foreach (var track in targetPlan.clearTracks) if (!result.clearTracks.Contains(track)) result.clearTracks.Add(track);
        foreach (var clip in targetPlan.clips)
        {
            if (clip == null) continue;
            bool hasTransition = result.clips.Exists(c => c != null && c.track == clip.track)
                || result.queuedClips.Exists(q => q?.clip != null && q.clip.track == clip.track);
            if (hasTransition) result.queuedClips.Add(new CharacterAnimationQueuedClip { clip = clip.Clone(), delay = 0, runtimeAfterPreviousEnd = true });
            else result.clips.Add(clip.Clone());
        }
        // 原过渡队列已由Clone保留；目标自身队列按目标轨道继续，不能丢弃。
        foreach (var queued in targetPlan.queuedClips) if (queued?.clip != null) result.queuedClips.Add(queued.Clone());
        return result;
    }

    private void WarnTransitionSkippedOnce(
        AnimationComposeContext context,
        AnimationIntent intent,
        EAnimationLogicState targetLogicState,
        EAnimationLogicState transitionLogicState,
        string reason)
    {
        string characterName = context != null && context.root != null
            ? context.root.name
            : "NULL";

        AnimationIntentSourceStateMachine source =
            intent != null
                ? intent.intentSourceStateMatine
                : AnimationIntentSourceStateMachine.None;

        EAnimSemantic semantic =
            intent != null
                ? intent.semantic
                : EAnimSemantic.None;

        string key =
            $"{characterName}|{source}|{semantic}|{targetLogicState}|{transitionLogicState}|{reason}";

        if (warnedKeys.Contains(key))
            return;

        warnedKeys.Add(key);

        Object contextObject = context != null && context.root != null
            ? context.root
            : null;

        Debug.LogWarning(
            $"[AnimationTransitionRouter] 检测到过渡请求，但未播放过渡。\n" +
            $"Character: {characterName}\n" +
            $"Source: {source}\n" +
            $"Semantic: {semantic}\n" +
            $"TargetLogicState: {targetLogicState}\n" +
            $"Transition: {transitionLogicState}\n" +
            $"Reason: {reason}",
            contextObject
        );
    }
    private TransitionDecision EvaluateWeaponPoseTransition(
    AnimationIntent intent,
    EAnimationLogicState previousLogicState,
    EAnimationLogicState targetLogicState,
    AnimationComposeContext context)
    {
        TransitionDecision decision = new TransitionDecision();

        if (intent == null || context == null)
            return decision;

        if (intent.intentSourceStateMatine == AnimationIntentSourceStateMachine.OffHand)
        {
            if (targetLogicState == EAnimationLogicState.Guard && previousLogicState != EAnimationLogicState.Guard)
            { decision.hasTransitionIntent = decision.shouldApply = true; decision.transitionLogicState = EAnimationLogicState.GuardEnter; }
            else if (previousLogicState == EAnimationLogicState.Guard && targetLogicState == EAnimationLogicState.Idle)
            { decision.hasTransitionIntent = decision.shouldApply = true; decision.transitionLogicState = EAnimationLogicState.GuardExit; }
            return decision;
        }

        // 只处理攻击/瞄准的来源，不处理 Movement 也不是枪的过渡。
        if (intent.intentSourceStateMatine != AnimationIntentSourceStateMachine.HoundWeaponAction &&
            intent.intentSourceStateMatine != AnimationIntentSourceStateMachine.HoundWeaponPose)
        {
            return decision;
        }

        if (previousLogicState == EAnimationLogicState.None)
            return decision;

        if (previousLogicState == targetLogicState)
            return decision;

        if (previousLogicState == EAnimationLogicState.Aim &&
            targetLogicState == EAnimationLogicState.Weapon_LowReady)
        {
            decision.hasTransitionIntent = true;
            decision.shouldApply = true;
            decision.transitionLogicState = EAnimationLogicState.AimToGundown;
            decision.reason = "Aim -> GunDown";
            return decision;
        }

        if (previousLogicState == EAnimationLogicState.Weapon_LowReady &&
            targetLogicState == EAnimationLogicState.Aim)
        {
            decision.hasTransitionIntent = true;
            decision.shouldApply = true;
            decision.transitionLogicState = EAnimationLogicState.GundownToAim;
            decision.reason = "GunDown -> Aim";
            return decision;
        }

        if (previousLogicState == EAnimationLogicState.Weapon_LowReady &&
            targetLogicState == EAnimationLogicState.NotAimShootReady)
        {
            decision.hasTransitionIntent = true;
            decision.shouldApply = true;
            decision.transitionLogicState = EAnimationLogicState.GundownToNotAimShoot;
            decision.reason = "GunDown -> NotAimShoot";
            return decision;
        }

        if (previousLogicState == EAnimationLogicState.NotAimShootReady &&
            targetLogicState == EAnimationLogicState.Weapon_LowReady)
        {
            decision.hasTransitionIntent = true;
            decision.shouldApply = true;
            decision.transitionLogicState = EAnimationLogicState.NotAimShootToGundown;
            decision.reason = "NotAimShoot -> GunDown";
            return decision;
        }
        return decision;
    }


    //=======================移动过渡解析逻辑=======================//
    public bool TryResolveMovementTransition(
    EAnimSemantic from,
    EAnimSemantic to,
    out EAnimSemantic transitionSemantic)
    {
        transitionSemantic = EAnimSemantic.None;

        if (from == to)
            return false;

        if (from == EAnimSemantic.Idle && IsMoving(to))
        {
            transitionSemantic = EAnimSemantic.MoveStart;
            return true;
        }

        if (IsMoving(from) && to == EAnimSemantic.Idle)
        {
            transitionSemantic = EAnimSemantic.MoveStop;
            return true;
        }

        if (IsOppositeDirection(from, to))
        {
            transitionSemantic = EAnimSemantic.TurnAround;
            return true;
        }

        return false;
    }

    private TransitionDecision EvaluateMovementTransition(
    AnimationIntent intent,
    EAnimationLogicState previousLogicState,
    EAnimationLogicState targetLogicState,
    AnimationComposeContext context)
    {
        TransitionDecision decision = new TransitionDecision();

        if (intent == null || context == null)
            return decision;

        if (intent.intentSourceStateMatine != AnimationIntentSourceStateMachine.Movement)
            return decision;

        if (previousLogicState == EAnimationLogicState.None)
            return decision;

        if (previousLogicState == targetLogicState)
            return decision;

        EAnimationLogicState transitionLogicState;

        if (!TryResolveMoveTransitionLogicState(
            previousLogicState,
            targetLogicState,
            out transitionLogicState))
        {
            return decision;
        }

        decision.hasTransitionIntent = true;
        decision.transitionLogicState = transitionLogicState;

        if (ShouldSuppressTransition(
      intent,
      previousLogicState,
      targetLogicState,
      transitionLogicState,
      context,
      out TransitionSuppressReason suppressReason,
      out string suppressText))
        {
            decision.shouldApply = false;
            decision.shouldWarnWhenSkipped = false;
            decision.suppressReason = suppressReason;
            decision.reason = suppressText;
            return decision;
        }



        decision.shouldApply = true;

        decision.reason = $"MoveTransition: {previousLogicState} -> {targetLogicState}";

        return decision;
    }

    private bool TryResolveMoveTransitionLogicState(
    EAnimationLogicState from,
    EAnimationLogicState to,
    out EAnimationLogicState transitionLogicState)
    {
        transitionLogicState = EAnimationLogicState.None;

        // Idle -> Move起步
        if (from == EAnimationLogicState.Idle)
        {
            switch (to)
            {
                case EAnimationLogicState.Walk:
                case EAnimationLogicState.WalkBack:
                    transitionLogicState = EAnimationLogicState.IdleToWalk;
                    return true; // 当前可显式配置Walk起停；无映射则直接目标。

                case EAnimationLogicState.Jog:
                case EAnimationLogicState.JogBack:
                    transitionLogicState = EAnimationLogicState.IdleToJogSlow;
                    return true;

                case EAnimationLogicState.JogFast:
                case EAnimationLogicState.Dash:
                    transitionLogicState = EAnimationLogicState.IdleToJogFast;
                    return true;
            }
        }

        // Move -> Idle停步
        if (to == EAnimationLogicState.Idle)
        {
            switch (from)
            {
                case EAnimationLogicState.Walk:
                case EAnimationLogicState.WalkBack:
                    transitionLogicState = EAnimationLogicState.WalkToIdle;
                    return true; // 当前可显式配置Walk起停；无映射则直接目标。

                case EAnimationLogicState.Jog:
                case EAnimationLogicState.JogBack:
                    transitionLogicState = EAnimationLogicState.JogSlowToIdle;
                    return true;

                case EAnimationLogicState.JogFast:
                case EAnimationLogicState.Dash:
                    transitionLogicState = EAnimationLogicState.JogFastToIdle;
                    return true;
            }
        }

        return false;
    }
    private bool IsMoving(EAnimSemantic semantic)
    {
        return semantic == EAnimSemantic.Walk
            || semantic == EAnimSemantic.Jog
            || semantic == EAnimSemantic.JogFast
            || semantic == EAnimSemantic.Walk_Back
            || semantic == EAnimSemantic.Jog_Back
            || semantic == EAnimSemantic.Dash;
    }

    private bool IsForward(EAnimSemantic semantic)
    {
        return semantic == EAnimSemantic.Walk
            || semantic == EAnimSemantic.Jog
            || semantic == EAnimSemantic.JogFast
            || semantic == EAnimSemantic.Dash;
    }

    private bool IsBackward(EAnimSemantic semantic)
    {
        return semantic == EAnimSemantic.Walk_Back
            || semantic == EAnimSemantic.Jog_Back;
    }

    private bool IsOppositeDirection(EAnimSemantic from, EAnimSemantic to)
    {
        return IsForward(from) && IsBackward(to)
            || IsBackward(from) && IsForward(to);
    }
    private bool IsMoveLogicState(EAnimationLogicState state)
{
    return state == EAnimationLogicState.Walk
        || state == EAnimationLogicState.Jog
        || state == EAnimationLogicState.JogFast
        || state == EAnimationLogicState.WalkBack
        || state == EAnimationLogicState.JogBack
        || state == EAnimationLogicState.Dash;
}

private bool IsForwardMoveLogicState(EAnimationLogicState state)
{
    return state == EAnimationLogicState.Walk
        || state == EAnimationLogicState.Jog
        || state == EAnimationLogicState.JogFast
        || state == EAnimationLogicState.Dash;
}




    private bool IsBackwardMoveLogicState(EAnimationLogicState state)
{
    return state == EAnimationLogicState.WalkBack
        || state == EAnimationLogicState.JogBack;
}

    private bool IsOppositeMoveLogicDirection(
    EAnimationLogicState from,
    EAnimationLogicState to)
    {
        return IsForwardMoveLogicState(from) && IsBackwardMoveLogicState(to)
            || IsBackwardMoveLogicState(from) && IsForwardMoveLogicState(to);
    }


    //通用抑制入口
    private bool ShouldSuppressTransition(
    AnimationIntent intent,
    EAnimationLogicState previousLogicState,
    EAnimationLogicState targetLogicState,
    EAnimationLogicState transitionLogicState,
    AnimationComposeContext context,
    out TransitionSuppressReason suppressReason,
    out string reason)
    {
        suppressReason = TransitionSuppressReason.None;
        reason = null;

        if (intent == null || context == null)
            return false;

        if (ShouldSuppressMovementDirectionFlipTransition(
            intent,
            previousLogicState,
            targetLogicState,
            transitionLogicState,
            context,
            out reason))
        {
            suppressReason = TransitionSuppressReason.MovementDirectionFlip;
            return true;
        }

        return false;
    }

    // 前后移动不使用过渡动画 case 的具体规则
    private bool ShouldSuppressMovementDirectionFlipTransition(
    AnimationIntent intent,
    EAnimationLogicState previousLogicState,
    EAnimationLogicState targetLogicState,
    EAnimationLogicState transitionLogicState,
    AnimationComposeContext context,
    out string reason)
    {
        reason = null;

        if (intent.intentSourceStateMatine != AnimationIntentSourceStateMachine.Movement)
            return false;

        if (!IsMoveLogicState(previousLogicState))
            return false;

        if (!IsMoveLogicState(targetLogicState))
            return false;

        if (!IsOppositeMoveLogicDirection(previousLogicState, targetLogicState))
            return false;

        reason =
            $"移动前后方向切换不播放过渡: {previousLogicState} -> {targetLogicState}, transition={transitionLogicState}";

        return true;
    }






}

}
