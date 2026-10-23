using System.Collections.Generic;
using UnityEngine;

public class AnimationTransitionRouter
{
    private static readonly HashSet<string> warnedKeys = new HashSet<string>();

    private float turnAroundActiveUntil;
    private float turnAroundDuration = 0.25f;
    private const float TurnAroundFallbackDuration = 0.15f;

    [SerializeField, ReadOnly] private const float TransitionAnimCombineDuration = 0.05f;

    private class TransitionDecision
    {
        public bool hasTransitionIntent;
        public bool shouldApply;

        public EAnimationLogicState transitionLogicState = EAnimationLogicState.None;

        public TransitionSuppressReason suppressReason = TransitionSuppressReason.None;
        public bool shouldWarnWhenSkipped = true;


        public string reason;
    }

    public CharacterAnimationPlan BuildTransitionedPlan(
    AnimationIntent intent,
    EAnimationLogicState previousLogicState,
    EAnimationLogicState targetLogicState,
    CharacterAnimationPlan targetPlan,
    AnimationComposeContext context,
    CharacterAnimationConfigManager configManager)
    {
        if (intent == null || targetPlan == null || context == null || configManager == null)
            return targetPlan;

        targetPlan = NormalizeMoveBackTargetWhileMoveTurnPending(
    intent,
    targetLogicState,
    targetPlan,
    context,
    configManager,
    out targetLogicState
);

        TransitionDecision decision = EvaluateTransition(
    intent,
    previousLogicState,
    targetLogicState,
    context
        );

        /*
         * 没有任何过渡请求
         * 这种情况下不需要 Warning。
         */
        if (decision == null || !decision.hasTransitionIntent)
            return targetPlan;

        /*
         * 有过渡请求，但是被挡掉了。
         * 这种情况下只想看到的话给色提示。
         */
        if (!decision.shouldApply)
        {
            if (decision.shouldWarnWhenSkipped)
            {
                WarnTransitionSkippedOnce(
                    context,
                    intent,
                    targetLogicState,
                    decision.transitionLogicState,
                    decision.reason
                );
            }

            return targetPlan;
        }

        CharacterAnimationPlan transitionPlan =
            configManager.ResolvePlan(decision.transitionLogicState);

        /*
         * 有过渡请求也不需要播放过渡，
         * 如果没有找到对应的映射
         */
        if (transitionPlan == null)
        {
            WarnTransitionSkippedOnce(
                context,
                intent,
                targetLogicState,
                decision.transitionLogicState,
                $"缺少过渡动画映射: {decision.transitionLogicState}"
            );

            return targetPlan;
        }

        if (decision.transitionLogicState == EAnimationLogicState.TurnAround)
        {
            turnAroundDuration = ResolveAnimationDuration(transitionPlan, context);
            turnAroundActiveUntil = Time.time + turnAroundDuration;

            EAnimationLogicState fixedTargetLogicState =
                NormalizeTargetAfterTurn(targetLogicState);

            if (fixedTargetLogicState != targetLogicState)
            {
                CharacterAnimationPlan fixedTargetPlan =
                    configManager.ResolvePlan(fixedTargetLogicState);

                if (fixedTargetPlan != null)
                {
                    targetPlan = fixedTargetPlan;
                }
                else
                {
                    WarnTransitionSkippedOnce(
                        context,
                        intent,
                        targetLogicState,
                        decision.transitionLogicState,
                        $"完成后目标动画映射缺失: {fixedTargetLogicState}，继续使用原目标: {targetLogicState}"
                    );
                }
            }
        }

//        Debug.Log(
//    $"[TransitionRouter] Source={intent.intentSourceStateMatine}, " +
//    $"Semantic={intent.semantic}, " +
//    $"Previous={previousLogicState}, Target={targetLogicState}",
//    context.root
//);
        return CombineTransitionAndTarget(transitionPlan, targetPlan);
    }
    private CharacterAnimationPlan NormalizeMoveBackTargetWhileMoveTurnPending(
    AnimationIntent intent,
    EAnimationLogicState targetLogicState,
    CharacterAnimationPlan targetPlan,
    AnimationComposeContext context,
    CharacterAnimationConfigManager configManager,
    out EAnimationLogicState normalizedTargetLogicState)
    {
        normalizedTargetLogicState = targetLogicState;

        if (intent == null || context == null || context.dataBus == null || configManager == null)
            return targetPlan;

        if (intent.intentSourceStateMatine != AnimationIntentSourceStateMachine.Movement)
            return targetPlan;

        CharacterDataBus bus = context.dataBus;

        if (bus.requestedFacingSource != CharacterDataBus.RequestedFacingSource.Move)
            return targetPlan;

        if (!bus.isFacingChanging)
            return targetPlan;

        switch (targetLogicState)
        {
            case EAnimationLogicState.WalkBack:
                normalizedTargetLogicState = EAnimationLogicState.Walk;
                break;

            case EAnimationLogicState.JogBack:
                normalizedTargetLogicState = EAnimationLogicState.Jog;
                break;

            default:
                return targetPlan;
        }

        CharacterAnimationPlan normalizedPlan =
            configManager.ResolvePlan(normalizedTargetLogicState);

        if (normalizedPlan == null)
            return targetPlan;

        return normalizedPlan;
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

    private TransitionDecision EvaluateTurnAround(
        AnimationIntent intent,
        EAnimationLogicState targetLogicState,
        AnimationComposeContext context)
    {
        TransitionDecision decision = new TransitionDecision();
        decision.transitionLogicState = EAnimationLogicState.TurnAround;

        if (intent == null || context == null || context.dataBus == null)
            return decision;
        CharacterDataBus bus = context.dataBus;

        /*
         * 第一层：TurnAround 只处理 Movement。
         * 不满足来源触发"转身"的 intent，这里不 Warning。
         */
        if (intent.intentSourceStateMatine != AnimationIntentSourceStateMachine.Movement)
            return decision;

        /*
         * 只有移动方向下的朝向变化，才触发移动转身过渡。
         * Aim / Attack 时不触发 TurnAround。
         */
        if (bus.requestedFacingSource != CharacterDataBus.RequestedFacingSource.Move)
            return decision;

        if (Time.time < turnAroundActiveUntil)
        {
            decision.hasTransitionIntent = true;
            decision.shouldApply = true;
            decision.reason = "TurnAround grace period active";
            return decision;
        }
        /*
         * 当前朝向与目标朝向不同，
         * 说明已经处于"转身请求中"。
         */
        if (bus.currentFacing != bus.targetFacing)
        {
            decision.hasTransitionIntent = true;
        }
        else
        {
            return decision;
        }

        /*
         * 到这里说明有意图
         * 但移动转身请求被卡住了
         * 这里本应 return false 但应该给角色提示
         */

        if (!bus.isFacingChanging)
        {
            decision.shouldApply = false;
            decision.reason =
                "检测到 currentFacing != targetFacing，但 isFacingChanging == false，说明 FacingController 没有正确写入 DataBus，骨架可能被前面吞了。";
            return decision;
        }
//        Debug.Log(
//    $"[TurnAroundTest] APPLY TurnAround. " +
//    $"currentFacing={bus.currentFacing}, targetFacing={bus.targetFacing}, " +
//    $"isFacingChanging={bus.isFacingChanging}, " +
//    $"speed={bus.currentMoveSpeed:F2}, " +
//    $"targetLogicState={targetLogicState}",
//    context.root
//);

        decision.shouldApply = true;
        decision.reason = "需要播放 TurnAround 过渡。";

        return decision;
    }

    private EAnimationLogicState NormalizeTargetAfterTurn(EAnimationLogicState targetLogicState)
    {
        switch (targetLogicState)
        {
            case EAnimationLogicState.WalkBack:
                return EAnimationLogicState.Walk;

            case EAnimationLogicState.JogBack:
                return EAnimationLogicState.Jog;

            default:
                return targetLogicState;
        }
    }

    private float ResolveAnimationDuration(
        CharacterAnimationPlan plan,
        AnimationComposeContext context)
    {
        if (plan?.clips == null || plan.clips.Count == 0)
            return TurnAroundFallbackDuration;

        string animName = plan.clips[0]?.animaName;
        if (string.IsNullOrEmpty(animName))
            return TurnAroundFallbackDuration;

        if (context?.root?.skeletonAnimation?.Skeleton?.Data == null)
            return TurnAroundFallbackDuration;

        Spine.Animation anim = context.root.skeletonAnimation.Skeleton.Data.FindAnimation(animName);
        if (anim == null)
            return TurnAroundFallbackDuration;

        return anim.Duration;
    }

    private CharacterAnimationPlan CombineTransitionAndTarget(
        CharacterAnimationPlan transitionPlan,
        CharacterAnimationPlan targetPlan)
    {
        CharacterAnimationPlan result = new CharacterAnimationPlan();
        result.characterRoot = targetPlan.characterRoot;

        if (transitionPlan.clearTracks != null)
        {
            for (int i = 0; i < transitionPlan.clearTracks.Count; i++)
            {
                int track = transitionPlan.clearTracks[i];

                if (!result.clearTracks.Contains(track))
                    result.clearTracks.Add(track);
            }
        }

        if (targetPlan.clearTracks != null)
        {
            for (int i = 0; i < targetPlan.clearTracks.Count; i++)
            {
                int track = targetPlan.clearTracks[i];

                if (!result.clearTracks.Contains(track))
                    result.clearTracks.Add(track);
            }
        }

        if (transitionPlan.clips != null)
        {
            for (int i = 0; i < transitionPlan.clips.Count; i++)
            {
                CharacterAnimationClip clip = transitionPlan.clips[i];

                if (clip == null)
                    continue;

                CharacterAnimationClip copy = clip.Clone();
                copy.characterRoot = result.characterRoot;

                result.clips.Add(copy);
            }
        }

        if (targetPlan.clips != null)
        {
            for (int i = 0; i < targetPlan.clips.Count; i++)
            {
                CharacterAnimationClip clip = targetPlan.clips[i];

                if (clip == null)
                    continue;

                CharacterAnimationClip copy = clip.Clone();
                copy.characterRoot = result.characterRoot;

                result.queuedClips.Add(new CharacterAnimationQueuedClip
                {
                    clip = copy,
                    delay = TransitionAnimCombineDuration// 这里可以根据需要调整延迟时间，确保过渡动画播放完毕后再进入目标动画
                });
            }
        }

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
                    return false;//步幅太小不需要过渡

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
                    return false;//步幅太小不需要过渡

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
