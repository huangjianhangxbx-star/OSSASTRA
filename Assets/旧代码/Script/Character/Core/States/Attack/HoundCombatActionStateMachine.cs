using System;
using System.Collections.Generic;
using UnityEngine;


public class HoundCombatActionStateMachine : IParallelCharacterStateMachine
{
    public enum CombatInputDecision
    {
        None,

        EnterAim,
        Fire,
        Reload,

        MeleeAttack,
        MeleeSpecial
    }


    //负责Aim / Fire / Reload / Melee
    public string StateMachineName => "HoundCombatActionStateMachine";
    public int Priority => 15;
    public string CurrentStateName => currentState != null ? currentState.StateName : "None";
    private const float WeaponFacingReleaseDelay = 0.08f;

    public bool IsActive
    {
        get
        {
            WeaponRuntimeData weapon =GetActiveWeapon();
            return dataBus != null&& dataBus.isAlive&& weapon != null&& weapon.ActionProfile != null;
        }
    }

    private Dictionary<Type, HoundCombatActionState> states;
    private HoundCombatActionState currentState;

    private CharacterDataBus dataBus;
    private CharacterRoot characterRoot;
    private Transform transform;
    private Rigidbody2D rigidbody;
    private CharacterAnimationComposer animationComposer;
    private int actionReplayId;
    private CharacterWeaponInventory inventory;

    private HoundCombatActionStateAnimationBridge animationBridge;
    private bool wasActive;


    public GunSystemController gunController
    {
        get
        {
            return characterRoot != null ? characterRoot.gunSystemController : null;
        }
    }

    public void Initialize(CharacterDataBus bus, Rigidbody2D rb, Transform tf, CharacterRoot root)
    {
        dataBus = bus;
        rigidbody = rb;
        transform = tf;
        characterRoot = root;

        if (characterRoot != null)
            animationComposer = characterRoot.characterAnimationComposer;



        animationBridge = new HoundCombatActionStateAnimationBridge(dataBus,characterRoot,animationComposer);
        if (characterRoot != null && characterRoot.characterSpineEventSystem != null)
        {
            characterRoot.characterSpineEventSystem.OnSpineSignal += HandleSpineSignal;
        }
        if (characterRoot != null)
        {
            inventory = characterRoot.characterWeaponInventory;
            if (inventory == null)
            {
                inventory =characterRoot.GetComponent<CharacterWeaponInventory>();
            }
        }
        InitializeStates();
        TransitionTo<HoundCombatIdleState>();
    }

    private void InitializeStates()
    {
        states = new Dictionary<Type, HoundCombatActionState>
        {
            [typeof(HoundCombatIdleState)] = new HoundCombatIdleState(),
            [typeof(HoundCombatAimState)] = new HoundCombatAimState(),
            [typeof(HoundCombatFireState)] = new HoundCombatFireState(),
            [typeof(HoundCombatReloadState)] = new HoundCombatReloadState()
        };

        foreach (HoundCombatActionState state in states.Values)
        {
            state.Initialize(this, dataBus, transform, rigidbody, characterRoot);
        }
    }

    public void Enter()
    {
    }
    public void Exit()
    {
        currentState?.Exit();
        currentState?.ForceReleaseControl();
        wasActive = false;
        ClearRuntimeFlags();
        animationBridge?.ClearAll();
    }

    public void Update(float deltaTime)
    {
        bool isActive = IsActive;
        if (!isActive)
        {
            if (wasActive)
                currentState?.ForceReleaseControl();

            wasActive = false;
            ClearRuntimeFlags();
            animationBridge?.ClearAll();
            return;
        }

        wasActive = true;
        currentState?.Update(deltaTime);
        animationBridge?.Tick();

    }

    public void FixedUpdate(float fixedDeltaTime)
    {
        if (!IsActive)
            return;

        currentState?.FixedUpdate(fixedDeltaTime);
    }

    public bool RequestState(object stateData)
    {
        return false;
    }

    public bool CanBeInterruptedBy(ICharacterStateMachine other)
    {
        return true;
    }

    public bool CanInterrupt(ICharacterStateMachine other)
    {
        return false;
    }
    public bool TransitionTo<T>() where T : HoundCombatActionState
    {
        if (!states.TryGetValue(typeof(T), out HoundCombatActionState newState))
        {
            Debug.LogWarning($"[HoundCombatActionStateMachine] 状态不存在: {typeof(T).Name}");
            return false;
        }

        if (currentState != null && !currentState.CanTransitionTo(newState))
            return false;

        currentState?.Exit();
        currentState?.ForceReleaseControl();
        currentState = newState;
        currentState.Enter();
        return true;
    }

    public bool IsCombatAvailable()
    {
        WeaponRuntimeData weapon = GetActiveWeapon();
        return dataBus != null
            && dataBus.isAlive
            && weapon != null
            && weapon.ActionProfile != null
            && dataBus.weaponEquipState == WeaponEquipState.Equipped
            && dataBus.canUseWeapon;


    }
    public bool IsCurrentWeaponFirearm()
    {
        WeaponRuntimeData weapon = GetActiveWeapon();
        return weapon != null
            && weapon.IsFirearm
            && dataBus != null
            && dataBus.weaponEquipState == WeaponEquipState.Equipped
            && weapon.FirearmActionProfile != null;
    }

    public bool IsCurrentWeaponMelee()
    {
        WeaponRuntimeData weapon = GetActiveWeapon();
        return weapon != null
            && weapon.IsMelee
            && dataBus != null
            && dataBus.weaponEquipState == WeaponEquipState.Equipped;
    }

    public bool CanAim()
    {
        return IsCombatAvailable() && IsCurrentWeaponFirearm();
    }

    public bool CanFireCurrentFirearm()
    {
        return dataBus != null
            && IsCurrentWeaponFirearm()
            && IsCombatAvailable();
    }

    public static CombatInputDecision ResolveIdleFirearmInput(
        bool reloadPressed,
        bool attackPressed,
        bool aimHeld)
    {
        if (reloadPressed)
            return CombatInputDecision.Reload;
        if (attackPressed)
            return CombatInputDecision.Fire;
        if (aimHeld)
            return CombatInputDecision.EnterAim;
        return CombatInputDecision.None;
    }

    public static FirearmStance ResolveFirearmStance(bool aimHeld)
    {
        return aimHeld ? FirearmStance.Ads : FirearmStance.HipFire;
    }

    public static FirearmFireContinuation ResolveFireContinuation(
        bool attackHeld,
        bool aimHeld,
        bool firePoseExpired)
    {
        if (attackHeld)
            return FirearmFireContinuation.ContinueFire;
        if (!firePoseExpired)
            return FirearmFireContinuation.WaitForPose;
        return aimHeld
            ? FirearmFireContinuation.Aim
            : FirearmFireContinuation.Idle;
    }

    public bool CanReload()
    {
        return IsCombatAvailable() && IsCurrentWeaponFirearm();
    }

    public void MarkCombatIntent()
    {
        if (dataBus == null)
            return;

        dataBus.lastCombatIntentTime = Time.time;
    }

    private WeaponRuntimeData GetActiveWeapon()
    {
        if (inventory != null)
        {
            return inventory.ActiveWeapon;
        }
        /*
         * 迁移期兼容回退。
         */
        return dataBus != null ? dataBus.currentWeaponRuntimeData : null;
    }


    public CombatInputDecision EvaluateIdleInput()
    {
        if (!IsCombatAvailable())
            return CombatInputDecision.None;

        WeaponRuntimeData weapon =
            GetActiveWeapon();

        if (weapon == null)
            return CombatInputDecision.None;

        if (weapon.IsFirearm)
        {
            return EvaluateIdleFirearmInput();
        }

        if (weapon.IsMelee)
        {
            return EvaluateIdleMeleeInput();
        }

        return CombatInputDecision.None;
    }

    private CombatInputDecision
    EvaluateIdleFirearmInput()
    {
        return ResolveIdleFirearmInput(
            dataBus.reloadPressedThisFrame,
            dataBus.houndAttackPressedThisFrame,
            dataBus.aimHeld);
    }
    private CombatInputDecision
    EvaluateIdleMeleeInput()
    {
        /*
         * 真正近战武器左键。
         *
         * 当前先保留入口，不直接进入现有 MeleeState，
         * 因为现有 MeleeState 很可能是枪械踢击逻辑。
         */
        if (dataBus.houndAttackPressedThisFrame)
        {
            return CombatInputDecision.MeleeAttack;
        }
        /*
         * 真正近战武器右键特殊动作入口。
         */
        if (dataBus.aimPressedThisFrame)
        {
            return CombatInputDecision.MeleeSpecial;
        }
        return CombatInputDecision.None;
    }

    public void ExecuteIdleInputDecision(
    CombatInputDecision decision)
    {
        switch (decision)
        {
            case CombatInputDecision.None:
                return;

            case CombatInputDecision.EnterAim:
                MarkCombatIntent();
                TransitionTo<HoundCombatAimState>();
                return;

            case CombatInputDecision.Fire:
                MarkCombatIntent();
                TransitionTo<HoundCombatFireState>();
                return;

            case CombatInputDecision.Reload:
                TryStartReloadFromIdle();
                return;

            case CombatInputDecision.MeleeAttack:
                HandleMeleeAttackNotImplemented();
                return;

            case CombatInputDecision.MeleeSpecial:
                HandleMeleeSpecialNotImplemented();
                return;
        }
    }
    private void TryStartReloadFromIdle()
    {
        //if (!CanReload())
        //    return;

        //if (gunController == null)
        //    return;

        //if (!gunController.CanStartReload())
        //    return;

        //MarkCombatIntent();
        //TransitionTo<HoundCombatReloadState>();
        if (!CanRequestReload())
            return;

        MarkCombatIntent();
        TransitionTo<HoundCombatReloadState>();
    }
    private void HandleMeleeAttackNotImplemented()
    {
        WeaponRuntimeData weapon =
            GetActiveWeapon();

        Debug.Log(
            $"[HoundCombat] MeleeAttack 尚未实现。" +
            $" Weapon={GetWeaponDebugName(weapon)}",
            characterRoot
        );
    }

    private void HandleMeleeSpecialNotImplemented()
    {
        WeaponRuntimeData weapon =
            GetActiveWeapon();

        Debug.Log(
            $"[HoundCombat] MeleeSpecial 尚未实现。" +
            $" Weapon={GetWeaponDebugName(weapon)}",
            characterRoot
        );
    }
    private string GetWeaponDebugName(
    WeaponRuntimeData weapon)
    {
        if (weapon == null)
            return "NULL";

        if (weapon.config == null)
            return "RuntimeWithoutConfig";

        return
            $"{weapon.config.weaponName}" +
            $" / Type={weapon.config.weaponType}" +
            $" / Asset={weapon.config.name}";
    }


    /// <summary>
    /// 判断当前是否能够发起换弹请求。
    /// 只用于决定是否进入 ReloadState。
    /// </summary>
    public bool CanRequestReload()
    {
        if (!CanReload())
            return false;

        if (gunController == null)
            return false;

        return gunController.CanStartReload();
    }





    #region 武器桥接
    public FirearmActionProfileSO GetFirearmProfile()
    {
        WeaponRuntimeData weapon =GetActiveWeapon();
        return weapon != null? weapon.FirearmActionProfile: null;
    }
    public WeaponActionProfileSO GetActionProfile()
    {
        WeaponRuntimeData weapon = GetActiveWeapon();
        return weapon != null ? weapon.ActionProfile: null;
    }

    public float GetFireDuration()
    {
        FirearmActionProfileSO profile = GetFirearmProfile();

        if (profile == null)
            return 0.15f;

        return Mathf.Max(0.05f, profile.fireAnimationDuration);
    }
    public float GetReloadDuration()
    {
        FirearmActionProfileSO profile = GetFirearmProfile();

        if (profile == null)
            return 2.6f;

        return Mathf.Max(0.1f, profile.reloadAnimationFallbackDuration);
    }

    public float GetMeleeAnimationDuration(
    EAnimSemantic semantic,
    float fallback)
    {
        fallback = Mathf.Max(0.05f, fallback);

        if (characterRoot == null ||
            characterRoot.characterAnimationConfigManager == null)
        {
            return fallback;
        }

        EAnimationLogicState logicState;

        switch (semantic)
        {
            case EAnimSemantic.Attack_Normal_1:
                logicState = EAnimationLogicState.Attack_Normal_1;
                break;

            case EAnimSemantic.Attack_Normal_2:
                logicState = EAnimationLogicState.Attack_Normal_2;
                break;

            case EAnimSemantic.Attack_Normal_3:
                logicState = EAnimationLogicState.Attack_Normal_3;
                break;

            default:
                Debug.LogWarning(
                    $"[Melee] 不支持的近战语义: {semantic}",
                    characterRoot
                );

                return fallback;
        }

        CharacterAnimationPlan plan =
            characterRoot.characterAnimationConfigManager.ResolvePlan(
                logicState
            );

        if (plan == null ||
            plan.clips == null ||
            plan.clips.Count <= 0)
        {
            Debug.LogWarning(
                $"[Melee] 没有找到动画方案: {logicState}",
                characterRoot
            );

            return fallback;
        }

        string animationName = string.Empty;

        /*
         * 优先寻找 Track 0。
         * 你的 ATK1、ATK2 当前都配置在 Track 0。
         */
        for (int i = 0; i < plan.clips.Count; i++)
        {
            CharacterAnimationClip clip = plan.clips[i];

            if (clip == null)
                continue;

            if (clip.track == 0 &&
                !string.IsNullOrEmpty(clip.animaName))
            {
                animationName = clip.animaName;
                break;
            }
        }

        /*
         * 没找到 Track 0 时，退回到方案中的第一个有效动画。
         */
        if (string.IsNullOrEmpty(animationName))
        {
            for (int i = 0; i < plan.clips.Count; i++)
            {
                CharacterAnimationClip clip = plan.clips[i];

                if (clip != null &&
                    !string.IsNullOrEmpty(clip.animaName))
                {
                    animationName = clip.animaName;
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(animationName))
        {
            Debug.LogWarning(
                $"[Melee] 动画方案中没有有效动画名: {logicState}",
                characterRoot
            );

            return fallback;
        }

        /*
         * 必须明确使用 Hound 骨架。
         *
         * 不要使用 GetComponentInChildren<SkeletonAnimation>()，
         * 因为你的角色下面同时有 Hound 和 HunterGun 骨架，
         * 它可能错误拿到枪械骨架。
         */
        Spine.Unity.SkeletonAnimation skeletonAnimation =
            characterRoot.houndSkeletonAnimation;

        if (skeletonAnimation == null ||
            skeletonAnimation.Skeleton == null ||
            skeletonAnimation.Skeleton.Data == null)
        {
            Debug.LogWarning(
                $"[Melee] Hound Skeleton 尚未初始化，使用保底时长。" +
                $" Animation={animationName}",
                characterRoot
            );

            return fallback;
        }

        Spine.Animation animation =
            skeletonAnimation.Skeleton.Data.FindAnimation(
                animationName
            );

        if (animation == null)
        {
            Debug.LogWarning(
                $"[Melee] Spine 中找不到动画: {animationName}",
                characterRoot
            );

            return fallback;
        }

        return Mathf.Max(0.05f, animation.Duration);
    }

    public bool FireCurrentWeapon()
    {
        if (!CanFireCurrentFirearm())
            return false;

        if (gunController == null)
        {
            Debug.LogWarning(
                "[HoundCombatAction] GunSystemController == null",
                characterRoot
            );

            return false;
        }

        bool fired = gunController.TryHandleFireInput(
            dataBus.houndAttackPressedThisFrame,
            dataBus.houndAttackHeld
        );

        return fired;
    }







    public void ApplyReload()
    {
        if (!IsCurrentWeaponFirearm())
            return;

        if (gunController == null)
        {
            Debug.LogWarning("[HoundCombatAction] GunSystemController == null");
            return;
        }

        if (!gunController.CanStartReload())
            return;

        gunController.CommitReload();

        WeaponRuntimeData weapon = GetActiveWeapon();

        WeaponConfigSO config = weapon != null? weapon.config : null;

        string weaponName = config != null ? config.name: "Unknown";
        Debug.Log( $"[HoundCombatAction] ApplyReload: {weaponName}",characterRoot);

        //WeaponConfigSO config = dataBus.currentWeaponRuntimeData.config;
        //string weaponName = config != null ? config.name : "Unknown";
        //Debug.Log($"[HoundCombatAction] ApplyReload: {weaponName}");
    }
    #endregion

    #region 动画意图桥接
    public AnimationIntent GetWeaponPoseIntent()
    {
        return animationBridge != null ? animationBridge.GetWeaponPoseIntent() : null;
    }

    public bool ShouldKeepHipFirePose()
    {
        return animationBridge != null && animationBridge.ShouldKeepHipFirePose();
    }

    public void KeepHipFirePose(float duration)
    {
        LockMoveFacingByWeapon();
        animationBridge?.KeepHipFirePose(duration);
    }

    public void SubmitAimPoseIntent()
    {
        LockMoveFacingByWeapon();
        animationBridge?.SubmitAimPoseIntent();
    }

    public void SubmitHipFirePoseIntent()
    {
        LockMoveFacingByWeapon();
        animationBridge?.SubmitHipFirePoseIntent();
    }

    public void SubmitFireIntent()
    {
        LockMoveFacingByWeapon();
        animationBridge?.SubmitFireIntent(GetFireDuration());
    }

    public void SubmitReloadIntent(float duration)
    {

        LockMoveFacingByWeapon();
        animationBridge?.SubmitReloadIntent(duration);
    }

    public void SubmitMeleeIntent(EAnimSemantic semantic, float duration, int replayId)
    {
        animationBridge?.SubmitMeleeIntent(semantic, duration, replayId);
    }

    public void RemoveWeaponPoseIntent()
    {
        animationBridge?.RemoveWeaponPoseIntent();
    }
    public void SubmitLowReadyPoseIntent()
    {
        if (characterRoot == null || animationComposer == null)
        {
            return;
        }
        AnimationIntent current = GetWeaponPoseIntent();

        if (current != null && current.semantic == EAnimSemantic.Weapon_LowReady)
            return;

        AnimationIntent intent = new AnimationIntent
        {
            root = characterRoot,
            intentSourceStateMatine = AnimationIntentSourceStateMachine.HoundWeaponPose,
            semantic = EAnimSemantic.Weapon_LowReady,
            layer = AnimationLayer.UpperBody,
            priority = 100,
            exclusiveFullBody = false,
            oneShot = false,
            duration = 0f,
            replayId = 0
        };

        characterRoot.characterAnimationComposer.SubmitIntent(intent);

    }
    /// <summary>
    /// 松开右键 -> SetWeaponPoseLowReady
    //  松开左键 -> SetWeaponPoseLowReady
    //  腰射自然结束 -> SetWeaponPoseLowReady
    /// </summary>
    public void SetWeaponPoseLowReady()
    {
        animationBridge?.StopHipFirePoseHold();
        SubmitLowReadyPoseIntent();
    }
    /// <summary>
    /// 长按 R 卸武器 -> HardClearWeaponAnimation
    //  死亡/缴械/切无武器 -> HardClearWeaponAnimation
    /// </summary>
    public void HardClearWeaponAnimation()
    {
        /*
         * 停止腰射姿态计时。
         */
        animationBridge?.StopHipFirePoseHold();

        /*
         * 清除枪械动作、枪械 Pose 和近身攻击意图。
         */
        RemoveWeaponActionIntent();
        RemoveWeaponPoseIntent();
        RemoveMeleeIntent();

        if (animationComposer != null)
        {
            animationComposer.RemoveIntent(
                AnimationIntentSourceStateMachine
                    .WeaponPosture
            );

            animationComposer.ClearTracks(1, 2);
        }

        /*
         * 清理运行时战斗状态。
         */
        ClearRuntimeFlags();

        /*
         * 武器切换属于强制中断。
         * 不经过旧状态的普通 Exit 结算。
         */
        ForceTransitionToIdle();
    }

    /// <summary>
    /// 武器收起、切槽、死亡或缴械时，
    /// 不经过普通 CanTransitionTo 检查，直接重置到 Idle。
    ///
    /// 这里故意不调用旧状态的 Exit()。
    /// 因为 ReloadState.Exit()、MeleeState.Exit() 等以后可能包含
    /// 动作结算逻辑，强制中断时不应该意外提交换弹或伤害。
    /// </summary>
    private void ForceTransitionToIdle()
    {
        if (states == null)
            return;

        if (!states.TryGetValue(
                typeof(HoundCombatIdleState),
                out HoundCombatActionState idleState))
        {
            Debug.LogWarning(
                "[HoundCombat] 强制重置失败：找不到 IdleState。",
                characterRoot
            );

            return;
        }
        // Forced interruption skips action settlement, but must always return
        // character-control ownership before entering the idle state.
        currentState?.ForceReleaseControl();
        currentState = idleState;
        currentState.Enter();
    }

    public string ResolveReloadAnimationName()
    {
        if (characterRoot == null || characterRoot.characterAnimationConfigManager == null)
            return string.Empty;

        CharacterAnimationPlan plan =
            characterRoot.characterAnimationConfigManager.ResolvePlan(EAnimationLogicState.Reload);

        if (plan == null || plan.clips == null || plan.clips.Count <= 0)
            return string.Empty;

        for (int i = 0; i < plan.clips.Count; i++)
        {
            CharacterAnimationClip clip = plan.clips[i];

            if (clip == null)
                continue;

            if (clip.track == 1 && !string.IsNullOrEmpty(clip.animaName))
                return clip.animaName;
        }

        for (int i = 0; i < plan.clips.Count; i++)
        {
            CharacterAnimationClip clip = plan.clips[i];

            if (clip != null && !string.IsNullOrEmpty(clip.animaName))
                return clip.animaName;
        }

        return string.Empty;
    }

    public float GetAnimationDurationOrFallback(string animationName, float fallback)
    {
        if (string.IsNullOrEmpty(animationName))
            return Mathf.Max(0f, fallback);

        //Spine.Unity.SkeletonAnimation skeletonAnimation =
        //    characterRoot.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();

        Spine.Unity.SkeletonAnimation skeletonAnimation = characterRoot != null ? characterRoot.houndSkeletonAnimation: null;

        if (skeletonAnimation == null ||skeletonAnimation.Skeleton == null ||skeletonAnimation.Skeleton.Data == null)
        {
            return Mathf.Max(0f, fallback);
        }

        Spine.Animation animation = skeletonAnimation.Skeleton.Data.FindAnimation(animationName);

        if (animation == null)
        {
            Debug.LogWarning($"[Reload] Animation not found: {animationName}", characterRoot);
            return Mathf.Max(0f, fallback);
        }

        return Mathf.Max(0f, animation.Duration);
    }

    public void StopFirePoseHold()
    {
        animationBridge?.StopHipFirePoseHold();
    }

    public void RemoveWeaponActionIntent()
    {
        animationBridge?.RemoveWeaponActionIntent();
    }


    public void RemoveMeleeIntent()
    {
        if (animationComposer == null)
            return;

        animationComposer.RemoveIntent(
            AnimationIntentSourceStateMachine.HoundAttack
        );
    }






    #endregion

    #region 生命周期/安全兜底
    public void ClearRuntimeFlags()
    {
        if (dataBus == null)
            return;
        dataBus.isAiming = false;
        dataBus.isReloading = false;
        dataBus.isHoundAttacking = false;
        dataBus.currentHoundCombatAction = HoundCombatActionType.None;
    }


    private void LockMoveFacingByWeapon()
    {
        if (characterRoot == null || characterRoot.orientationSystem == null)
            return;

        characterRoot.orientationSystem.NotifyWeaponFacingActivity(WeaponFacingReleaseDelay);

    }
    #endregion

    #region 动画回调

    private void HandleSpineSignal(CharacterSpineAnimationSignal signal)
    {
        if (!IsActive)
            return;

        currentState?.OnSpineSignal(signal);
    }



    #endregion










}
