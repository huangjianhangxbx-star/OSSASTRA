using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterEquipmentController : MonoBehaviour, ICharacterSubsystem
{
    private bool _isInitialized;

    [Header("Owner")]
    [SerializeField] private CharacterRoot characterRoot;

    [Header("Spine")]
    [SerializeField] private SkeletonAnimation skeletonAnimation;
    [SerializeField] private SkeletonRenderer skeletonRenderer;
    [SerializeField] private SkeletonRenderSeparator renderSeparator;

    [Header("Roots")]
    [SerializeField] private Transform equipmentRoot;

    [Header("Weapon Mount")]
    [SerializeField] private ExternalWeaponMount houndWeaponMount;

    [Header("Default Names")]
    [SerializeField] private string weaponBoneName = "LongRangeWeapon";
    [SerializeField] private string weaponSlotName = "LongRangeWeapon";

    [Header("װ��")]
    [SerializeField] private WeaponConfigSO currentWeaponConfig;


    private CharacterWeaponInventory weaponInventory;

    public WeaponRuntimeData MountedWeaponRuntimeData { get; private set; }

    public WeaponConfigSO CurrentWeaponConfig => currentWeaponConfig;

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized)
            return;

        characterRoot = root;

        AutoGetReferences();
        ConfigureWeaponMount();
        if (characterRoot != null)
        {
            weaponInventory =
                characterRoot.characterWeaponInventory;

            if (weaponInventory == null)
            {
                weaponInventory =
                    characterRoot.GetComponent<CharacterWeaponInventory>();
            }
        }

        _isInitialized = true;

        // Subscribe to active weapon changes from inventory
        if (weaponInventory != null)
        {
            weaponInventory.ActiveWeaponChanged += OnActiveWeaponChanged;
        }

        /*
   * ��һ�׶�Ǩ����ɺ���������Ӧ���� Inventory��
   */
        if (weaponInventory != null &&
            weaponInventory.ActiveWeapon != null)
        {
            MountWeaponRuntimeData(
                weaponInventory.ActiveWeapon
            );

            return;
        }
        /*
 * ���ݾɳ����е� Inspector ��ʼ������
 * ������տ������г�ʼװ�������� Inventory ��ɾ����
 */
        if (currentWeaponConfig != null)
        {
            SetHoundWeapon(currentWeaponConfig);
        }



        //WeaponConfigSO Weapon = currentWeaponConfig;

        //if (Weapon != null)
        //{
        //    SetHoundWeapon(Weapon);
        //}
    }

    private void Reset()
    {
        AutoGetReferences();
    }

    private void AutoGetReferences()
    {
        if (characterRoot == null)
            characterRoot = GetComponent<CharacterRoot>();

        if (skeletonAnimation == null && characterRoot != null)
            skeletonAnimation = characterRoot.skeletonAnimation;

        if (skeletonAnimation == null)
            skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);

        if (skeletonRenderer == null && skeletonAnimation != null)
            skeletonRenderer = skeletonAnimation.GetComponent<SkeletonRenderer>();

        if (renderSeparator == null && skeletonAnimation != null)
            renderSeparator = skeletonAnimation.GetComponent<SkeletonRenderSeparator>();

        if (equipmentRoot == null)
        {
            Transform found = transform.Find("EquipmentRoot");

            if (found != null)
                equipmentRoot = found;
        }

        if (houndWeaponMount == null && equipmentRoot != null)
            houndWeaponMount = equipmentRoot.GetComponentInChildren<ExternalWeaponMount>(true);

        if (houndWeaponMount == null)
            houndWeaponMount = GetComponentInChildren<ExternalWeaponMount>(true);
    }

    private void ConfigureWeaponMount()
    {
        if (houndWeaponMount == null)
        {
            Debug.LogWarning("[CharacterEquipmentController] houndWeaponMount == null", this);
            return;
        }

        houndWeaponMount.Bind(new ExternalWeaponMount.BindData
        {
            SkeletonRenderer = skeletonRenderer,
            RenderSeparator = renderSeparator,
            FollowBoneName = weaponBoneName,
            RenderSlotName = weaponSlotName
        });
    }

    public void SetHoundWeapon(WeaponConfigSO weaponConfig)// ���𴴽�����ʱ���ݲ�д���λ
    {
        //    ���� WeaponRuntimeData
        //д�� Inventory

        if (weaponConfig == null)
        {
            ClearHoundWeapon();
            return;
        }

        WeaponRuntimeData runtimeData =
            new WeaponRuntimeData(weaponConfig);

        /*
   * ���ݾɵ��á�
   *
   * ��� Inventory �Ѿ����ڣ�Ӧ��������д����ǰ�ۣ�
   * �ٹ��� Inventory �е� ActiveWeapon��
   */
        if (weaponInventory != null)
        {
            bool assigned =
                weaponInventory.TryAssignWeapon(
                    weaponInventory.ActiveSlotId,
                    runtimeData,
                    true
                );

            if (!assigned)
            {
                Debug.LogWarning(
                    "[CharacterEquipmentController] " +
                    "�޷���������д�뵱ǰ��λ��",
                    this
                );

                return;
            }

            runtimeData =
                weaponInventory.ActiveWeapon;
        }

        MountWeaponRuntimeData(runtimeData);

        if (characterRoot != null &&
    characterRoot.characterDataBus != null)
        {
            characterRoot.characterDataBus.weaponEquipState =
                WeaponEquipState.Cancelled;
        }

        characterRoot.stateArbitrator.weaponEquipSM.ApplyWeaponVisibility(false);
    }

    public void ClearHoundWeapon()
    {


        WeaponRuntimeData activeWeapon =
    weaponInventory != null
        ? weaponInventory.ActiveWeapon
        : MountedWeaponRuntimeData;

        UnmountCurrentWeaponVisual();

        /*
         * ����ִ�� ClearHoundWeapon ʱ��
         * �ӵ�ǰ��λ�Ƴ�������
         */
        if (weaponInventory != null &&
            activeWeapon != null)
        {
            weaponInventory.RemoveWeapon(
                weaponInventory.ActiveSlotId
            );
        }

        if (characterRoot != null &&
            characterRoot.characterDataBus != null)
        {
            CharacterDataBus dataBus =
                characterRoot.characterDataBus;

            dataBus.currentWeaponRuntimeData = null;
            dataBus.weaponEquipState =
                WeaponEquipState.Cancelled;

            ResetCombatFlagsAfterWeaponChanged();
        }

        GunSystemController gunSystemController =
            characterRoot != null
                ? characterRoot.gunSystemController
                : null;

        if (gunSystemController != null)
        {
            gunSystemController.ClearWeaponRuntimeData();
        }

        RefreshAfterWeaponChanged();










    }

    private void RefreshAfterWeaponChanged()
    {
        if (characterRoot != null && characterRoot.characterStatsController != null)
        {
            characterRoot.characterStatsController.RecalculateStats();
        }

        if (characterRoot != null && characterRoot.characterAnimationConfigManager != null)
        {
            characterRoot.characterAnimationConfigManager.RefreshWeaponAnimationOverride();
        }
    }






    public bool MountWeaponRuntimeData(WeaponRuntimeData runtimeData) // ����ʵ��������ģ��
    {
        if (characterRoot == null ||
            characterRoot.characterDataBus == null)
        {
            Debug.LogError(
                "[CharacterEquipmentController] " +
                "CharacterRoot �� DataBus Ϊ�ա�",
                this
            );

            return false;
        }

        if (houndWeaponMount == null)
        {
            Debug.LogError(
                "[CharacterEquipmentController] " +
                "HoundWeaponMount Ϊ�ա�",
                this
            );

            return false;
        }

        if (runtimeData == null ||
            runtimeData.config == null)
        {
            Debug.LogWarning(
                "[CharacterEquipmentController] " +
                "�޷����ؿյ� WeaponRuntimeData��",
                this
            );

            return false;
        }

        WeaponConfigSO weaponConfig =
            runtimeData.config;

        if (weaponConfig.weaponPrefab == null)
        {
            Debug.LogError(
                $"[CharacterEquipmentController] ���� " +
                $"{weaponConfig.weaponName} û������ weaponPrefab��",
                weaponConfig
            );

            return false;
        }

        /*
         * ֻж�ؾ�ģ�ͣ���ɾ���ɲ�λ�е� RuntimeData��
         */
        UnmountCurrentWeaponVisual();

        WeaponRoot weaponRoot =
            houndWeaponMount.SetWeaponPrefab(
                weaponConfig.weaponPrefab
            );

        if (weaponRoot == null)
        {
            Debug.LogError(
                "[CharacterEquipmentController] ����ʵ����ʧ�ܡ�",
                this
            );

            return false;
        }

        weaponRoot.Initialize(
            weaponConfig,
            characterRoot
        );

        runtimeData.weaponRoot = weaponRoot;

        MountedWeaponRuntimeData = runtimeData;
        currentWeaponConfig = weaponConfig;

        CharacterDataBus dataBus =
            characterRoot.characterDataBus;

        /*
         * Ǩ���ڼ��ݾ���
         * Inventory ��Ȼ�� ActiveWeapon ����ʵ�����ߡ�
         */
        dataBus.currentWeaponRuntimeData =
            runtimeData;

        GunSystemController gunSystemController =
            characterRoot.gunSystemController;

        if (gunSystemController == null)
        {
            gunSystemController =
                characterRoot.GetComponent<GunSystemController>();
        }

        if (gunSystemController != null)
        {
            gunSystemController.ApplyWeaponRuntimeData(
                runtimeData
            );
        }

        ResetCombatFlagsAfterWeaponChanged();
        RefreshAfterWeaponChanged();

        Debug.Log(
            $"[CharacterEquipmentController] ����������ɡ�" +
            $" Weapon={weaponConfig.weaponName}",
            this
        );

        return true;
    }


    public void UnmountCurrentWeaponVisual()
    {
        if (MountedWeaponRuntimeData != null)
        {
            MountedWeaponRuntimeData.weaponRoot = null;
        }

        if (houndWeaponMount != null)
        {
            houndWeaponMount.ClearWeapon();
        }

        MountedWeaponRuntimeData = null;
        currentWeaponConfig = null;
    }


    public bool MountActiveInventoryWeapon()
    {
        if (weaponInventory == null &&
            characterRoot != null)
        {
            weaponInventory =
                characterRoot.characterWeaponInventory;
        }

        if (weaponInventory == null)
        {
            Debug.LogError(
                "[CharacterEquipmentController] " +
                "CharacterWeaponInventory Ϊ�ա�",
                this
            );

            return false;
        }

        WeaponRuntimeData activeWeapon =
            weaponInventory.ActiveWeapon;

        if (activeWeapon == null)
        {
            UnmountCurrentWeaponVisual();

            if (characterRoot != null &&
                characterRoot.characterDataBus != null)
            {
                characterRoot.characterDataBus
                    .currentWeaponRuntimeData = null;
            }

            RefreshAfterWeaponChanged();
            return false;
        }

        return MountWeaponRuntimeData(activeWeapon);
    }

    private void ResetCombatFlagsAfterWeaponChanged()
    {
        if (characterRoot == null ||
            characterRoot.characterDataBus == null)
        {
            return;
        }

        CharacterDataBus dataBus =
            characterRoot.characterDataBus;

        dataBus.isAiming = false;
        dataBus.isReloading = false;
        dataBus.isHoundAttacking = false;
        dataBus.currentHoundCombatAction =
            HoundCombatActionType.None;
    }

    private void OnActiveWeaponChanged(
        WeaponSlotId prevSlot,
        WeaponRuntimeData prevWeapon,
        WeaponSlotId newSlot,
        WeaponRuntimeData newWeapon)
    {
        if (newWeapon == null)
        {
            // Active weapon removed — unmount visual, clear state
            UnmountCurrentWeaponVisual();

            if (characterRoot != null && characterRoot.characterDataBus != null)
            {
                characterRoot.characterDataBus.weaponEquipState = WeaponEquipState.Cancelled;
                characterRoot.characterDataBus.currentWeaponRuntimeData = null;
            }
        }
        else
        {
            // New active weapon — mount model but keep hidden (Cancelled state)
            MountWeaponRuntimeData(newWeapon);

            if (characterRoot != null && characterRoot.characterDataBus != null)
            {
                characterRoot.characterDataBus.weaponEquipState = WeaponEquipState.Cancelled;
            }
        }
    }
}
