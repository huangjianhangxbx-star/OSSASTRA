using UnityEngine;
using static CharacterDataBus;

public class HoundCombatReloadState : HoundCombatActionState
{
    public override string StateName => "HoundCombat_Reload";
    private bool reloadApplied;
    private bool reloadAnimationFinished;
 
 

    private const string ReloadMainAnimationName = "Weapon/1_Reload";



    private string currentReloadAnimationName;
    private float reloadTimeoutTimer;

    private const float ReloadFallbackExtraTime = 0.2f;
    private const float MinReloadFallbackTimeout = 0.5f;






    public override void Enter()
    {
        if (!stateMachine.IsCurrentWeaponFirearm())
        {
            stateMachine.TransitionTo<
                HoundCombatIdleState>();

            return;
        }


        if (!stateMachine.CanReload())
        {
            if (dataBus.aimHeld && stateMachine.CanAim())
                stateMachine.TransitionTo<HoundCombatAimState>();
            else
                stateMachine.TransitionTo<HoundCombatIdleState>();
            return;
        }

        currentReloadAnimationName = stateMachine.ResolveReloadAnimationName();

        float reloadAnimLength = stateMachine.GetAnimationDurationOrFallback(
            currentReloadAnimationName,
            stateMachine.GetReloadDuration()
        );

        reloadTimeoutTimer = Mathf.Max(
            MinReloadFallbackTimeout,
            reloadAnimLength + ReloadFallbackExtraTime
        );

        stateMachine.SubmitReloadIntent(reloadTimeoutTimer);



        dataBus.currentHoundCombatAction = HoundCombatActionType.Reload;
        dataBus.isReloading = true;
        dataBus.isAiming = false;
        dataBus.isHoundAttacking = false;
        reloadApplied = false;
        reloadAnimationFinished = false;
        reloadTimeoutTimer = Mathf.Max(0.5f, stateMachine.GetReloadDuration() + 0.2f);
        stateMachine.MarkCombatIntent();
  
        stateMachine.gunController.BeginReload(); // 新增：播放换弹表现 / 发音频 / 进动画

        characterRoot.gunSystemController?.SetHandIKLocked(
            rightLocked: true,
            leftLocked: false
        );
    }

    public override void Exit()
    {
        characterRoot.gunSystemController?.SetLeftHandIKLocked(true);
        dataBus.isReloading = false;
        reloadApplied= false;
    }

    public override void Update(float deltaTime)
    {
        if (!stateMachine.IsCombatAvailable())
        {
            FinishReloadState();
            return;
        }

        reloadTimeoutTimer -= deltaTime;

        if (reloadAnimationFinished)
        {
            if (!reloadApplied)
            {
                Debug.LogWarning("[Reload] Ignored reload complete before reload_commit event.");
                reloadAnimationFinished = false;
                return;
            }

            FinishReloadState();
            return;

        }

        if (reloadTimeoutTimer <= 0f)
        {
            if (!reloadApplied)
            {
                Debug.LogWarning("[Reload] Reload timeout ended before reload_commit event. Applying fallback reload.");
                ApplyReloadOnce();
            }

            FinishReloadState();
        }

    }

    private void ApplyReloadOnce()
    {
        if (reloadApplied)
            return;

        reloadApplied = true;
        stateMachine.ApplyReload();
    }

    private void FinishReloadState()
    {
        stateMachine.gunController.EndReload();

        if (dataBus.aimHeld && stateMachine.CanAim())
        {
            stateMachine.SubmitAimPoseIntent();

            // 关键：提交接续姿态后，立刻移除 Reload action。
            // 这样 Track 1 会从 Reload 直接落到 Aim，而不是等 oneShot 过期。
            stateMachine.RemoveWeaponActionIntent();

            stateMachine.TransitionTo<HoundCombatAimState>();
        }
        else
        {
            stateMachine.SubmitLowReadyPoseIntent();

            // 关键：同上，直接释放 Reload 对 Track 1 的高优先级占用。
            stateMachine.RemoveWeaponActionIntent();

            stateMachine.TransitionTo<HoundCombatIdleState>();
        }
    }




    public void OnReloadCommitEvent()
    {
        ApplyReloadOnce();
    }



    public void OnReloadMagazineFallEvent()
    {

        Debug.Log("弹夹掉落");
    }
    public void OnReloadMagazineTakeEvent()
    {

        Debug.Log("弹夹拿取");
    }


    /// <summary>
    /// //////
    /// </summary>
    /// <param name="signal"></param>

    public override void OnSpineSignal(CharacterSpineAnimationSignal signal)
    {
        //Debug.Log(
        //    $"[ReloadState SpineSignal] type={signal.type}, " +
        //    $"anim={signal.animationName}, event={signal.eventName}, " +
        //    $"track={signal.trackIndex}, time={signal.trackTime}"
        //);

        if (signal.type == CharacterSpineSignalType.EventReached)
        {
            if (!IsCurrentReloadAnimation(signal.animationName))
                return;

            HandleReloadSpineEvent(signal.eventName);
            return;
        }

        if (signal.type == CharacterSpineSignalType.AnimationCompleted)
        {
            if (!IsCurrentReloadAnimation(signal.animationName))
                return;

            reloadAnimationFinished = true;
        }
    }

    private void HandleReloadSpineEvent(string eventName)
    {
        switch (eventName)
        {
            case "Reload/Reload_magazineFall":
                OnReloadMagazineFallEvent();
                break;

            case "Reload/Reload_magazineTake":
                OnReloadMagazineTakeEvent();
                break;

            case "Reload/Reload_Commit":
                OnReloadCommitEvent();
                break;
        }
    }

    private bool IsCurrentReloadAnimation(string animationName)
    {
        if (string.IsNullOrEmpty(animationName))
            return false;

        if (string.IsNullOrEmpty(currentReloadAnimationName))
            return false;

        return animationName == currentReloadAnimationName;
    }

}