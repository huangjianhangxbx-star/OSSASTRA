
//����
//�����Ƿ�װ��������
//���� R �л�װ��/����
//��ȫ���Զ�����
//�Զ�������
//���� Equip / Cancel ����
//���������Ƿ����
//�ύ������ͼ��
//�ύ���壺
//EAnimSemantic.Weapon_Equip
//EAnimSemantic.Weapon_Cancel
using UnityEngine;

public class WeaponEquipStateMachine : IParallelCharacterStateMachine
{
    public string StateMachineName => "WeaponEquipStateMachine";

    // Ҫ�� HoundCombatActionStateMachine ǰִ��
    public int Priority => 14;

    public string CurrentStateName => dataBus != null
        ? dataBus.weaponEquipState.ToString()
        : "None";

    public bool IsActive
    {
        get
        {
            return dataBus != null && dataBus.isAlive;
        }
    }

    private CharacterDataBus dataBus;
    private CharacterRoot characterRoot;
    private CharacterAnimationComposer animationComposer;

    private CharacterWeaponInventory inventory;
    private CharacterEquipmentController equipmentController;
    private ExternalWeaponMount weaponMount;

    private float transitionTimer;
    private int replayId;

    // ��һ����д����������Դ� WeaponCombatProfileSO �� WeaponVisualProfileSO ��ȡ
    private const float DefaultEquipDuration = 0.35f;
    private const float DefaultCancelDuration = 0.35f;

    /*
 * ��ִ�е��в�����
 *
 * ����ֻ�����װ��״̬���У�
 * DataBus ����Ҫ�����λ�л��㷨���������ڡ�
 */
    private bool hasPendingSlotSwitch;
    private WeaponSlotId pendingSlotId;

    private bool equipAfterSwitch;

    private bool HasActiveWeapon =>
        inventory != null &&
        inventory.ActiveWeapon != null;




    public void Initialize(CharacterDataBus bus, Rigidbody2D rb, Transform tf, CharacterRoot root)
    {
        dataBus = bus;
        characterRoot = root;

        if (characterRoot != null)
        {
            animationComposer =
                characterRoot.characterAnimationComposer;

            inventory =
                characterRoot.characterWeaponInventory;

            if (inventory == null)
            {
                inventory =
                    characterRoot.GetComponent<
                        CharacterWeaponInventory>();
            }

            equipmentController =
                characterRoot.GetComponent<
                    CharacterEquipmentController>();

            weaponMount =
                characterRoot.GetComponentInChildren<
                    ExternalWeaponMount>(true);
        }
        if (dataBus == null)
            return;

        dataBus.weaponEquipState =
            WeaponEquipState.Cancelled;

        ApplyWeaponVisibility(false);
    }

    public void Enter()
    {
    }

    public void Update(float deltaTime)
    {

        /*
 * �����л��������κ�װ�����ɽ׶ζ����Խ��ա�
 *
 * ������� Cancelling���µ����ּ���������
 * pendingSlotId������ʹ�����һ������
 */
        ConsumeWeaponSwitchInput();
        /*
 * �ȴ�����ɡ�
 * �������ʱ����ִ�в�λ�滻����ʼ��һ�� Equip��
 */
        UpdateTransition(deltaTime);
        /*
 * ֻ�е�ǰ���ڹ�����ʱ�Ž�����ͨ����
 * �в��������ͨ�� RequestSwitchSlot() ������ʱ�̸��� pending��
 */
        if (!IsTransitioning())
        {
            HandleRequests();
        }

        /*
 * û��������û�д��в�ʱ�����ֳ�������
 */
        if (!HasActiveWeapon &&
            !hasPendingSlotSwitch)
        {
            RemoveEquipIntent();
            ForceCancelled();
            return;
        }
        SyncWeaponVisualState();
    }

    public void FixedUpdate(float fixedDeltaTime)
    {
    }

    public bool RequestState(object stateData)
    {
        /*
           * �����Ժ�ͨ��ͳһ״̬������ WeaponSlotId��
           */
        if (stateData is WeaponSlotId slotId)
        {
            return RequestSwitchSlot(slotId);
        }

        return false;
    }

    /// <summary>
    /// �����л���ָ�������ۡ�
    ///
    /// ��ǰ�׶ο��ɵ��԰�ťֱ�ӵ��á�
    /// ��һ�׶����� InputHandler �� 1/2/3 ������á�
    /// </summary>
    public bool RequestSwitchSlot(
        WeaponSlotId slotId)
    {
        if (dataBus == null ||
            inventory == null ||
            equipmentController == null)
        {
            return false;
        }

        if (!inventory.CanSelectSlot(slotId))
        {
            Debug.LogWarning(
                $"[WeaponEquip] ��λ����ѡ��: {slotId}",
                characterRoot
            );

            return false;
        }

        /*
         * �Ѿ��ǵ�ǰ�ۣ�����û�������л������ظ�ִ�С�
         */
        if (slotId == inventory.ActiveSlotId &&
            !hasPendingSlotSwitch)
        {
            switch (dataBus.weaponEquipState)
            {
                case WeaponEquipState.Cancelled:
                    {
                        /*
                         * ��ǰ��λ��ȷ��ֻ��������������״̬��
                         * ���ּ�Ӧֱ���ó���ǰ������
                         */
                        if (!dataBus.isInSafeZone)
                        {
                            StartEquip();
                        }
                        Debug.Log( $"[WeaponEquip] {slotId} �Ѿ��ǵ�ǰ��λ��" +$"ִ�е�ǰ�����ó���",characterRoot);
                        return true;
                    }
                case WeaponEquipState.Equipped:
                    {
                        Debug.Log( $"[WeaponEquip] �����л�����" +$"{slotId} �Ѿ�װ��������ʹ��״̬��", characterRoot);
                        return true;
                    }
                case WeaponEquipState.Equipping:
                    {
                        Debug.Log($"[WeaponEquip] �����л�����" +$"{slotId} ����װ����",characterRoot);
                        return true;
                    }

                case WeaponEquipState.Cancelling:
                    {
                        /*
                         * ��ǰ�����������𣬵�����ְ��˵�ǰ��λ��
                         * �� Cancel ��ɺ�����װ����ǰ��λ��
                         */
                        pendingSlotId = slotId;
                        hasPendingSlotSwitch = true;
                        equipAfterSwitch = true;
                        Debug.Log($"[WeaponEquip] ��ǰ��λ��������" +$"��ɺ�����װ�� {slotId}��",characterRoot);
                        return true;
                    }
            }
        }

        /*
         * ����л��������ٴ������������ּ���
         * ʹ�����һ������
         */
        pendingSlotId = slotId;
        hasPendingSlotSwitch = true;

        /*
         * ���ּ��л���Ŀ�����ó���������
         */
        equipAfterSwitch = true;

        Debug.Log($"[WeaponEquip] �յ��в�����" +$" Current={inventory.ActiveSlotId}," + $" Target={pendingSlotId}," + $" EquipState={dataBus.weaponEquipState}",characterRoot);

        switch (dataBus.weaponEquipState)
        {
            case WeaponEquipState.Equipped:
            case WeaponEquipState.Equipping:
                StartCancel();
                break;

            case WeaponEquipState.Cancelled:
                CommitPendingSlotSwitch();
                break;

            case WeaponEquipState.Cancelling:
                /*
                 * ��������ʱֻ���� pendingSlotId��
                 * Cancel ��ɺ�ʹ����������
                 */
                break;
        }

        return true;
    }

    public bool RequestSwitchToNextSlot()
    {
        if (inventory == null)
            return false;
        /*
 * ����Ѿ���һ�����л�Ŀ�꣬
 * ��һ���ֻ�Ӧ�Ӵ��л�Ŀ���������顣
 */
        WeaponSlotId cycleStartSlot =
            hasPendingSlotSwitch
                ? pendingSlotId
                : inventory.ActiveSlotId;
        bool found =inventory.TryGetNextOccupiedSlotFrom(cycleStartSlot,out WeaponSlotId nextSlotId);
        if (!found)
        {
            Debug.Log("[WeaponEquip] û���ҵ���һ�����������ۡ�",characterRoot );
            return false;
        }
        return RequestSwitchSlot(nextSlotId);
    }

    private void HandleRequests()
    {
        if (dataBus == null)
            return;

        if (dataBus.isInSafeZone)
        {
            if (dataBus.weaponEquipState ==
                    WeaponEquipState.Equipped ||
                dataBus.weaponEquipState ==
                    WeaponEquipState.Equipping)
            {
                /*
                 * ��ȫ���������вۡ�
                 */
                hasPendingSlotSwitch = false;
                equipAfterSwitch = false;
                StartCancel();
            }

            dataBus.requestCancelWeapon = false;
            return;
        }

        if (dataBus.requestCancelWeapon)
        {
            dataBus.requestCancelWeapon = false;

            if (dataBus.weaponEquipState ==
                    WeaponEquipState.Equipped ||
                dataBus.weaponEquipState ==
                    WeaponEquipState.Equipping)
            {
                hasPendingSlotSwitch = false;
                equipAfterSwitch = false;
                StartCancel();
            }

            return;
        }

        if (!dataBus.weaponToggleLongPressedThisFrame)
            return;

        if (dataBus.weaponEquipState ==
            WeaponEquipState.Cancelled)
        {
            StartEquip();
            return;
        }

        if (dataBus.weaponEquipState ==
            WeaponEquipState.Equipped)
        {
            hasPendingSlotSwitch = false;
            equipAfterSwitch = false;
            StartCancel();
        }
    }









    public bool CanBeInterruptedBy(ICharacterStateMachine other)
    {
        return true;
    }

    public bool CanInterrupt(ICharacterStateMachine other)
    {
        return false;
    }


    private void StartEquip()
    {
        if (dataBus == null ||
            !HasActiveWeapon)
        {
            return;
        }

        if (dataBus.weaponEquipState ==
                WeaponEquipState.Equipping ||
            dataBus.weaponEquipState ==
                WeaponEquipState.Equipped)
        {
            return;
        }

        replayId++;

        dataBus.weaponEquipState =
            WeaponEquipState.Equipping;

        /*
         * װ��������ʼʱ�������֡�
         */
        ApplyWeaponVisibility(true);

        transitionTimer = GetEquipDuration();

        SubmitEquipIntent(
            EAnimSemantic.Weapon_Equip,
            transitionTimer,
            replayId
        );
    }


    private void StartCancel()
    {
        if (dataBus == null)
            return;

        if (dataBus.weaponEquipState ==
                WeaponEquipState.Cancelled ||
            dataBus.weaponEquipState ==
                WeaponEquipState.Cancelling)
        {
            return;
        }

        replayId++;

        dataBus.weaponEquipState =
            WeaponEquipState.Cancelling;

        transitionTimer = GetCancelDuration();

        SubmitEquipIntent(
            EAnimSemantic.Weapon_Cancel,
            transitionTimer,
            replayId
        );

        if (characterRoot != null &&
            characterRoot.stateArbitrator != null &&
            characterRoot.stateArbitrator
                .houndCombatActionSM != null)
        {
            characterRoot.stateArbitrator
                .houndCombatActionSM
                .HardClearWeaponAnimation();
        }
    }

    private void UpdateTransition(float deltaTime)
    {
        if (dataBus == null ||
            !IsTransitioning())
        {
            return;
        }

        transitionTimer -= deltaTime;

        if (transitionTimer > 0f)
            return;

        if (dataBus.weaponEquipState ==
            WeaponEquipState.Equipping)
        {
            dataBus.weaponEquipState =
                WeaponEquipState.Equipped;

            RemoveEquipIntent();
            return;
        }

        if (dataBus.weaponEquipState ==
            WeaponEquipState.Cancelling)
        {
            dataBus.weaponEquipState =
                WeaponEquipState.Cancelled;

            RemoveEquipIntent();
            ApplyWeaponVisibility(false);

            /*
             * �������Ѿ���ȫ����
             * ���ڲ����滻ģ�ͺ� ActiveSlot��
             */
            if (hasPendingSlotSwitch)
            {
                CommitPendingSlotSwitch();
            }
        }
    }


    private void CommitPendingSlotSwitch()
    {
        if (!hasPendingSlotSwitch ||
            inventory == null ||
            equipmentController == null)
        {
            return;
        }

        WeaponSlotId targetSlot =
            pendingSlotId;

        bool shouldEquip =
            equipAfterSwitch;

        /*
         * ���� pending������ص���ʧ�ܷ�֧���롣
         */
        hasPendingSlotSwitch = false;
        equipAfterSwitch = false;
        ApplyWeaponVisibility(false);
        bool slotChanged =inventory.TrySetActiveSlot(targetSlot);
        if (!slotChanged)
        {
            Debug.LogWarning(
                $"[WeaponEquip] ActiveSlot �л�ʧ��: " +
                $"{targetSlot}",
                characterRoot
            );

            return;
        }

        bool mounted =
            equipmentController
                .MountActiveInventoryWeapon();

        if (!mounted)
        {
            Debug.LogError(
                $"[WeaponEquip] ����������ʧ��: " +
                $"Slot={targetSlot}",
                characterRoot
            );

            ForceCancelled();
            return;
        }

        /*
         * MountWeaponRuntimeData ����ս����ǣ�
         * ������ȷ����װ��״̬Ϊ Cancelled��
         * Ȼ��ʼ������ Equip��
         */
        dataBus.weaponEquipState =
            WeaponEquipState.Cancelled;

        Debug.Log(
            $"[WeaponEquip] ���л���λ��" +
            $" Active={inventory.ActiveSlotId}," +
            $" Weapon={GetActiveWeaponName()}",
            characterRoot
        );

        if (shouldEquip &&
            !dataBus.isInSafeZone)
        {
            StartEquip();
        }
    }


    private bool IsTransitioning()
    {
        return dataBus != null &&
               (dataBus.weaponEquipState ==
                    WeaponEquipState.Equipping ||
                dataBus.weaponEquipState ==
                    WeaponEquipState.Cancelling);
    }

    private void ForceCancelled()
    {
        if (dataBus == null)
            return;

        dataBus.weaponEquipState =
            WeaponEquipState.Cancelled;

        transitionTimer = 0f;

        RemoveEquipIntent();
        ApplyWeaponVisibility(false);
    }

    private float GetEquipDuration()
    {
        return DefaultEquipDuration;
    }

    private float GetCancelDuration()
    {
        return DefaultCancelDuration;
    }

    private void SubmitEquipIntent(
        EAnimSemantic semantic,
        float duration,
        int currentReplayId)
    {
        if (animationComposer == null)
            return;

        animationComposer.SubmitIntent(
            new AnimationIntent
            {
                root = characterRoot,
                intentSourceStateMatine =
                    AnimationIntentSourceStateMachine
                        .WeaponEquip,
                semantic = semantic,
                layer = AnimationLayer.UpperBody,
                priority = 70,
                exclusiveFullBody = false,
                oneShot = true,
                duration = duration,
                replayId = currentReplayId
            }
        );
    }

    private void RemoveEquipIntent()
    {
        if (animationComposer == null)
            return;

        animationComposer.RemoveIntent(
            AnimationIntentSourceStateMachine
                .WeaponEquip
        );
    }








    public void ApplyWeaponVisibility(bool visible)
    {
        if (weaponMount == null && characterRoot != null)
            weaponMount = characterRoot.GetComponentInChildren<ExternalWeaponMount>(true);

        if (weaponMount == null)
            return;

        weaponMount.SetWeaponVisible(visible);
    }

    private void SyncWeaponVisualState()
    {
        if (dataBus == null)
            return;

        /*
         * Equipping���Ѿ��ó�����ʾ��
         * Equipped����ʾ��
         * Cancelling�����𶯻���û������������ʾ��
         * Cancelled�����ء�
         */
        bool shouldBeVisible =
            dataBus.weaponEquipState !=
            WeaponEquipState.Cancelled;

        ApplyWeaponVisibility(shouldBeVisible);
    }

    private string GetActiveWeaponName()
    {
        WeaponRuntimeData weapon =
            inventory != null
                ? inventory.ActiveWeapon
                : null;

        if (weapon == null ||
            weapon.config == null)
        {
            return "NULL";
        }

        return weapon.config.weaponName;
    }

    private void ConsumeWeaponSwitchInput()
    {
        if (dataBus == null)
            return;

        bool hasRequest = dataBus.TryConsumeWeaponSwitchInput(out WeaponSwitchInputRequestType requestType, out WeaponSlotId requestedSlot );

        if (!hasRequest)
            return;

        switch (requestType)
        {
            case WeaponSwitchInputRequestType.SelectSlot:
                {
                    bool accepted = RequestSwitchSlot(requestedSlot);
                    Debug.Log($"[WeaponEquip] Consume SelectSlot." +$" Slot={requestedSlot}," +$" Accepted={accepted}",characterRoot );
                    break;
                }

            case WeaponSwitchInputRequestType.CycleNext:
                {
                    bool accepted = RequestSwitchToNextSlot();
                    Debug.Log( $"[WeaponEquip] Consume CycleNext." +$" Accepted={accepted}", characterRoot);
                    break;
                }
        }
    }





    public void Exit()
    {
        /*
         * StateArbitrator ��ǰ��Բ���Ծ�Ĳ���״̬��
         * ÿ֡���� Exit���������������ظ�ִ�С�
         */
        RemoveEquipIntent();

        if (dataBus != null &&
            !hasPendingSlotSwitch)
        {
            dataBus.weaponEquipState =
                WeaponEquipState.Cancelled;
        }

        if (!hasPendingSlotSwitch)
        {
            ApplyWeaponVisibility(false);
        }
    }





}