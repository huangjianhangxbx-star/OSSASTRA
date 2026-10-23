using UnityEngine;

public class HoundCombatActionStateAnimationBridge
{
    private readonly CharacterDataBus dataBus;
    private readonly CharacterRoot characterRoot;
    private readonly CharacterAnimationComposer animationComposer;

    private int actionReplayId;
    private float hipFirePoseExpireTime;
    //    解决视觉问题：腰射开一枪
    //-> Fire oneShot 动画还没播完
    //-> 玩家松开左键
    //-> 如果立刻回 LowReady，上半身会突然掉枪


    public HoundCombatActionStateAnimationBridge(
        CharacterDataBus dataBus,
        CharacterRoot characterRoot,
        CharacterAnimationComposer animationComposer)
    {
        this.dataBus = dataBus;
        this.characterRoot = characterRoot;
        this.animationComposer = animationComposer;
    }

    public void Tick()
    {
        //UpdateHipFirePoseLifetime();
        UpdateWeaponPoseSafety();
    }

    public void ClearAll()
    {
        StopHipFirePoseHold();
        RemoveWeaponActionIntent();
        RemoveWeaponPoseIntent();
    }

    public AnimationIntent GetWeaponPoseIntent()
    {
        if (animationComposer == null)
            return null;

        return animationComposer.GetIntent(AnimationIntentSourceStateMachine.HoundWeaponPose);
    }

    public bool ShouldKeepHipFirePose()
    {
        return Time.time < hipFirePoseExpireTime;
    }

    public void KeepHipFirePose(float duration)
    {
        hipFirePoseExpireTime = Mathf.Max(
            hipFirePoseExpireTime,
            Time.time + Mathf.Max(0.05f, duration)
        );

        SubmitHipFirePoseIntent();
    }

    public void SubmitAimPoseIntent()
    {
        if (animationComposer == null)
            return;


        //    Gun_HipFirePose / Gun_AimPose / Weapon_LowReady:
        //持续姿态
        //oneShot = false
        //已经是这个姿态时重复 Submit 没意义，还可能导致重算 / 掉帧
        if (HasWeaponPose(EAnimSemantic.Gun_AimPose))
            return;

        animationComposer.SubmitIntent(new AnimationIntent
        {
            root = characterRoot,
            intentSourceStateMatine = AnimationIntentSourceStateMachine.HoundWeaponPose,
            semantic = EAnimSemantic.Gun_AimPose,
            layer = AnimationLayer.UpperBody,
            priority = 55,
            exclusiveFullBody = false,
            oneShot = false,
            duration = 0f,
            replayId = 0
        });
    }

    public void SubmitHipFirePoseIntent()
    {
        if (animationComposer == null)
            return;

        if (HasWeaponPose(EAnimSemantic.Gun_HipFirePose))
            return;

        animationComposer.SubmitIntent(new AnimationIntent
        {
            root = characterRoot,
            intentSourceStateMatine = AnimationIntentSourceStateMachine.HoundWeaponPose,
            semantic = EAnimSemantic.Gun_HipFirePose,
            layer = AnimationLayer.UpperBody,
            priority = 54,
            exclusiveFullBody = false,
            oneShot = false,
            duration = 0f,
            replayId = 0
        });
    }

    public void SubmitFireIntent(float duration)
    {
        if (animationComposer == null)
            return;

        actionReplayId++;

        animationComposer.SubmitIntent(new AnimationIntent
        {
            root = characterRoot,
            intentSourceStateMatine = AnimationIntentSourceStateMachine.HoundWeaponAction,
            semantic = EAnimSemantic.Gun_Fire,
            layer = AnimationLayer.UpperBody,
            priority = 60,
            exclusiveFullBody = false,
            oneShot = true,
            duration = duration,
            replayId = actionReplayId
        });
    }

    public void SubmitReloadIntent(float duration)
    {
        if (animationComposer == null)
            return;

        actionReplayId++;
        animationComposer.SubmitIntent(new AnimationIntent
        {

            root = characterRoot,
            intentSourceStateMatine = AnimationIntentSourceStateMachine.HoundWeaponAction,
            semantic = EAnimSemantic.Gun_Reload,
            layer = AnimationLayer.UpperBody,
            priority = 108,
            exclusiveFullBody = false,
            oneShot = true,
            duration = duration,
            replayId = actionReplayId
        });
    }

    public void SubmitMeleeIntent(EAnimSemantic semantic, float duration, int replayId)
    {
        if (animationComposer == null)
            return;

        animationComposer.SubmitIntent(new AnimationIntent
        {
            root = characterRoot,
            intentSourceStateMatine = AnimationIntentSourceStateMachine.HoundAttack,
            semantic = semantic,
            layer = AnimationLayer.FullBodyAction,
            priority = 60,
            exclusiveFullBody = true,//很重要，让近战动画（必须为轨道0），拥有抑制其他动画的能力，而不需要其他轨道的动画取消intent
                                     //造成需要退出又重新进入状态所产生的GAP。
            oneShot = true,
            duration = duration,
            replayId = replayId
        });
    }

    public void RemoveWeaponPoseIntent()
    {
        if (animationComposer == null)
            return;

        animationComposer.RemoveIntent(AnimationIntentSourceStateMachine.HoundWeaponPose);
    }

    public void RemoveWeaponActionIntent()
    {
        if (animationComposer == null)
            return;

        animationComposer.RemoveIntent(AnimationIntentSourceStateMachine.HoundWeaponAction);
    }

    private void UpdateHipFirePoseLifetime()
    {
        if (animationComposer == null || dataBus == null)
            return;

        if (dataBus.aimHeld)
            return;

        AnimationIntent intent = GetWeaponPoseIntent();

        if (intent == null)
            return;

        if (intent.semantic != EAnimSemantic.Gun_HipFirePose)
            return;

        bool stillFiring =
            dataBus.houndAttackHeld ||
            dataBus.houndAttackPressedThisFrame ||
            dataBus.isHoundAttacking ||
            dataBus.currentHoundCombatAction == HoundCombatActionType.Fire;

        if (stillFiring)
            return;

        if (Time.time < hipFirePoseExpireTime)
            return;

        StopHipFirePoseHold();
        characterRoot.stateArbitrator.houndCombatActionSM.SetWeaponPoseLowReady();
    }

    private void UpdateWeaponPoseSafety()
    {
        if (animationComposer == null || dataBus == null)
            return;

        AnimationIntent intent = GetWeaponPoseIntent();

        if (intent == null)
            return;

        if (intent.semantic == EAnimSemantic.Gun_AimPose)
        {
            bool shouldKeepAim =
                dataBus.aimHeld ||
                dataBus.isAiming ||
                dataBus.currentHoundCombatAction == HoundCombatActionType.Aim;

            if (!shouldKeepAim)
                characterRoot.stateArbitrator.houndCombatActionSM.SetWeaponPoseLowReady();

            return;
        }

        if (intent.semantic == EAnimSemantic.Gun_HipFirePose)
        {
            bool shouldKeepHipFire =
                dataBus.houndAttackHeld ||
                dataBus.houndAttackPressedThisFrame ||
                dataBus.isHoundAttacking ||
                dataBus.currentHoundCombatAction == HoundCombatActionType.Fire ||
                Time.time < hipFirePoseExpireTime;

            if (!shouldKeepHipFire)
            {
                StopHipFirePoseHold();
                characterRoot.stateArbitrator.houndCombatActionSM.SetWeaponPoseLowReady();
            }
        }
    }
    private bool HasWeaponPose(EAnimSemantic semantic)
    {
        AnimationIntent intent = GetWeaponPoseIntent();
        return intent != null && intent.semantic == semantic;
    }
    public void StopHipFirePoseHold()
    {
        hipFirePoseExpireTime = 0f;
    }
}