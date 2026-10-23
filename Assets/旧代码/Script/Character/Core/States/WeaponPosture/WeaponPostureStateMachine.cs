using UnityEngine;

public class WeaponPostureStateMachine : IParallelCharacterStateMachine
{
    public string StateMachineName => "WeaponPostureStateMachine";

    // Ҫ�� HoundCombatActionStateMachine ��ִ��
    public int Priority => 16;

    public string CurrentStateName => dataBus != null
        ? dataBus.weaponPostureState.ToString()
        : "None";

    public bool IsActive
    {
        get
        {
            return dataBus != null
                && dataBus.isAlive
                && dataBus.currentWeaponRuntimeData != null;
        }
    }

    private CharacterDataBus dataBus;
    private CharacterRoot characterRoot;
    private CharacterAnimationComposer animationComposer;

    public void Initialize(CharacterDataBus bus, Rigidbody2D rb, Transform tf, CharacterRoot root)
    {
        dataBus = bus;
        characterRoot = root;

        if (characterRoot != null)
            animationComposer = characterRoot.characterAnimationComposer;
        dataBus.weaponPostureState = WeaponPostureState.None;
    }

    public void Enter()
    {


    }

    public void Exit()
    {
        SetNone();
    }

    public void Update(float deltaTime)
    {
        if (!IsActive)
        {
            SetNone();
            return;
        }

        if (ShouldUseLowReady())
        {
            SetLowReady();
        }
        else
        {
            SetNone();
        }
    }

    public void FixedUpdate(float fixedDeltaTime)
    {
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

    private bool ShouldUseLowReady()
    {
        if (dataBus == null)
            return false;

        // ֻ����װ��״̬������ LowReady
        if (dataBus.weaponEquipState != WeaponEquipState.Equipped)
            return false;

        // ��ȫ������ʾ LowReady
        if (dataBus.isInSafeZone)
            return false;

        // װ��/��������в���ʾ LowReady
        if (dataBus.isWeaponEquipTransitioning)
            return false;

        // ��׼���� Gun_Aim �ӹ�
        if (dataBus.isAiming)
            return false;

        // �������� Gun_Reload �ӹ�
        if (dataBus.isReloading)
            return false;

        // �������� Fire / Melee �ӹ�
        // isHoundAttacking removed - now handled by exclusiveFullBody in MergePlans
        if (dataBus.isHunterAttacking)
            return false;

        return true;
    }

    private void SetLowReady()
    {
        if (dataBus == null)
            return;

        dataBus.weaponPostureState = WeaponPostureState.LowReady;

        SubmitLowReadyIntent();
    }

    private void SetNone()
    {
        if (dataBus != null)
            dataBus.weaponPostureState = WeaponPostureState.None;

        RemovePostureIntent();
    }

    private void SubmitLowReadyIntent()
    {
        if (animationComposer == null)
            return;

        animationComposer.SubmitIntent(new AnimationIntent
        {
            root = characterRoot,
            intentSourceStateMatine = AnimationIntentSourceStateMachine.WeaponPosture,
            semantic = EAnimSemantic.Weapon_LowReady,
            layer = AnimationLayer.UpperBody,
            priority = 40,
            exclusiveFullBody = false,
            oneShot = false,
            duration = 0.2f,
            replayId = 0
        });
    }

    private void RemovePostureIntent()
    {
        if (animationComposer == null)
            return;

        animationComposer.RemoveIntent(AnimationIntentSourceStateMachine.WeaponPosture);
    }
}