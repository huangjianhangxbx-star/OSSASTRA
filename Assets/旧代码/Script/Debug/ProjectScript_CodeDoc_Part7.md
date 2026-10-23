# ProjectScript Code Documentation - Part 7

> Source: `E:\WORLDCREATOR\new\Project_VELGITA\Assets\Script`  |  Total files: 212  |  This part: 20

## Architecture summary

This module contains 212 supported source files. The summary is inferred from directory layout, namespaces, declared types, and explicit asset references.

## File index

| File | Lines | Namespaces | Types | Asset refs |
|---|---:|---|---|---:|
| `Assets/Script/Character/Core/Systems/StatsSystem/WeaponStats/WeaponStatsSOEditor.cs` | 63 | - | class WeaponStatsSOEditor | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/AutoAttackGizmosDrawer.cs` | 196 | - | class AutoAttackGizmosDrawer | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/CharacterEquipmentController.cs` | 510 | - | class CharacterEquipmentController | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/CharacterWeaponInventory.cs` | 362 | - | class CharacterWeaponInventory | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/CharacterWeaponSlot.cs` | 52 | - | class CharacterWeaponSlot | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/Debug/WeaponSlotSwitchDebug.cs` | 54 | - | class WeaponSlotSwitchDebug | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/ExternalWeaponMount.cs` | 526 | - | class ExternalWeaponMount, struct BindData | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/AimCursorRadialLimiter.cs` | 237 | - | class AimCursorRadialLimiter | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/AimCursorSystem.cs` | 256 | - | class AimCursorSystem | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/AimIKController.cs` | 400 | - | class AimIKController | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/FirearmActionProfileSO.cs` | 83 | - | class FirearmActionProfileSO | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/FirearmActionProfileSOEditor.cs` | 248 | - | class FirearmActionProfileSOEditor | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunBonePositionFollower.cs` | 766 | - | class GunBoneMountFollower | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunFireModeConfig.cs` | 35 | - | class GunFireModeConfig | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunHandIKBinder.cs` | 598 | - | class GunHandIKBinder | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunHandOcclusionByAimAngle.cs` | 364 | - | class GunHandOcclusionByAimAngle | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunModule.cs` | 54 | - | class GunWeaponModule | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunMuzzlePointBinder.cs` | 135 | - | class GunMuzzlePointBinder | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunShotVisualController.cs` | 190 | - | class GunShotVisualController | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunSystemController.cs` | 717 | - | class GunSystemController | 0 |

## Public API

### `Assets/Script/Character/Core/Systems/StatsSystem/WeaponStats/WeaponStatsSOEditor.cs`
- `public class WeaponStatsSOEditor : Editor
{`
- `public override void OnInspectorGUI()
    {`
### `Assets/Script/Character/Core/Systems/WeaponSystem/AutoAttackGizmosDrawer.cs`
- `public class AutoAttackGizmosDrawer : MonoBehaviour
{`
### `Assets/Script/Character/Core/Systems/WeaponSystem/CharacterEquipmentController.cs`
- `public class CharacterEquipmentController : MonoBehaviour, ICharacterSubsystem
{`
- `public WeaponRuntimeData MountedWeaponRuntimeData {`
- `public WeaponConfigSO CurrentWeaponConfig => currentWeaponConfig;`
- `public void Initialize(CharacterRoot root)
    {`
- `public void SetHoundWeapon(WeaponConfigSO weaponConfig)// ���𴴽�����ʱ���ݲ�д���λ
    {`
- `public void ClearHoundWeapon()
    {`
- `public bool MountWeaponRuntimeData(WeaponRuntimeData runtimeData) // ����ʵ��������ģ��
    {`
- `public void UnmountCurrentWeaponVisual()
    {`
- `public bool MountActiveInventoryWeapon()
    {`
### `Assets/Script/Character/Core/Systems/WeaponSystem/CharacterWeaponInventory.cs`
- `public class CharacterWeaponInventory :
    MonoBehaviour,
    ICharacterSubsystem
{`
- `public WeaponSlotId ActiveSlotId => activeSlotId;`
- `public WeaponRuntimeData ActiveWeapon
    {`
- `public bool HasActiveWeapon =>
        ActiveWeapon != null;`
- `public bool ActiveWeaponIsFirearm =>
        ActiveWeapon != null &&
        ActiveWeapon.IsFirearm;`
- `public bool ActiveWeaponIsMelee =>
        ActiveWeapon != null &&
        ActiveWeapon.IsMelee;`
- `public event Action<
        WeaponSlotId,
        WeaponRuntimeData,
        WeaponSlotId,
        WeaponRuntimeData> ActiveWeaponChanged;`
- `public void Initialize(CharacterRoot root)
    {`
- `public CharacterWeaponSlot GetSlot(WeaponSlotId slotId)
    {`
- `public WeaponRuntimeData GetWeapon(WeaponSlotId slotId)
    {`
- `public bool HasWeapon(WeaponSlotId slotId)
    {`
- `public bool CanSelectSlot(WeaponSlotId slotId)
    {`
- `public bool TryAssignWeapon(
        WeaponSlotId slotId,
        WeaponRuntimeData weapon,
        bool replaceExisting = false)
    {`
- `public WeaponRuntimeData RemoveWeapon(WeaponSlotId slotId)
    {`
- `public bool TrySetActiveSlot(WeaponSlotId slotId)
    {`
- `public bool TryGetNextOccupiedSlot(out WeaponSlotId nextSlotId)
    {`
- `public bool TrySelectNextOccupiedSlot()
    {`
- `public bool TryGetNextOccupiedSlotFrom(WeaponSlotId fromSlotId, out WeaponSlotId nextSlotId)
    {`
### `Assets/Script/Character/Core/Systems/WeaponSystem/CharacterWeaponSlot.cs`
- `public class CharacterWeaponSlot
{`
- `public WeaponSlotId SlotId => slotId;`
- `public WeaponRuntimeData Weapon => weaponRuntimeData;`
- `public bool IsUnlocked => isUnlocked;`
- `public bool IsEmpty => weaponRuntimeData == null;`
- `public bool CanSelect =>
        isUnlocked && weaponRuntimeData != null;`
- `public void Initialize(WeaponSlotId id)
    {`
- `public void SetWeapon(WeaponRuntimeData weapon)
    {`
- `public WeaponRuntimeData RemoveWeapon()
    {`
- `public void SetUnlocked(bool unlocked)
    {`
### `Assets/Script/Character/Core/Systems/WeaponSystem/Debug/WeaponSlotSwitchDebug.cs`
- `public class WeaponSlotSwitchDebug :
    MonoBehaviour
{`
### `Assets/Script/Character/Core/Systems/WeaponSystem/ExternalWeaponMount.cs`
- `public class ExternalWeaponMount : MonoBehaviour
{`
- `public struct BindData
    {`
- `public SkeletonRenderer SkeletonRenderer;`
- `public SkeletonRenderSeparator RenderSeparator;`
- `public string FollowBoneName;`
- `public string RenderSlotName;`
- `public WeaponRoot CurrentWeapon => currentWeapon;`
- `public void Bind(BindData data)
    {`
- `public WeaponRoot SetWeaponPrefab(WeaponRoot weaponPrefab)
    {`
- `public void ClearWeapon()
    {`
- `public void SetWeaponVisible(bool visible)
    {`
### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/AimCursorRadialLimiter.cs`
- `public class AimCursorRadialLimiter : MonoBehaviour
{`
### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/AimCursorSystem.cs`
- `public class AimCursorSystem : MonoBehaviour, ICharacterSubsystem,ICharacterTickable
{`
- `public float defaultDistance = 3f;`
- `public float followSmoothTime = 0.06f;`
- `public float gamepadCursorSpeed = 8f;`
- `public float mousePixelDeadZone = 0.5f;`
- `public float gamepadDeadZone = 0.15f;`
- `public void Initialize(CharacterRoot root)
    {`
- `public void ManagedUpdate(float deltaTime)
    {`
### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/AimIKController.cs`
- `public class AimIKController : MonoBehaviour, ICharacterSubsystem,ICharacterLateTickable
{`
- `public void Initialize(CharacterRoot root)
    {`
- `public void ManagedLateUpdate(float deltaTime)
    {`
- `public void SetCursorTransform(Transform cursor)
    {`
- `public void SetAimOriginTransform(Transform origin)
    {`
- `public void ForceRefresh()
    {`
### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/FirearmActionProfileSO.cs`
- `public class FirearmActionProfileSO : WeaponActionProfileSO
{`
- `public override WeaponType WeaponType => WeaponType.Firearm;`
- `public FirearmCategory firearmCategory = FirearmCategory.Rifle;`
- `public GunFireModeType defaultFireMode = GunFireModeType.SemiAuto;`
- `public List<GunFireModeConfig> fireModes = new List<GunFireModeConfig>();`
- `public MeleeComboActionSO auxiliaryMeleeAction;`
- `public GunHitType hitType = GunHitType.Hitscan;`
- `public float fireAnimationDuration = 0.12f;`
- `public float reloadAnimationFallbackDuration = 0.6f;`
- `public GunFireModeConfig GetDefaultFireModeConfig()
    {`
- `public GunFireModeConfig GetFireModeConfig(GunFireModeType modeType)
    {`
### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/FirearmActionProfileSOEditor.cs`
- `public class FirearmActionProfileSOEditor : Editor
{`
- `public override void OnInspectorGUI()
    {`
### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunBonePositionFollower.cs`
- `public class GunBoneMountFollower : MonoBehaviour
{`
- `public void Bind(
        SkeletonAnimation targetSkeleton,
        string targetBoneName)
    {`
- `public void SetVisualFlipRoot(
        Transform targetVisualFlipRoot)
    {`
- `public void SetRenderSeparator(
        SkeletonRenderSeparator targetRenderSeparator,
        string targetSeparatorSlotName)
    {`
- `public void SetWeapon(WeaponRoot weapon)
    {`
- `public void RefreshNow()
    {`
### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunFireModeConfig.cs`
- `public class GunFireModeConfig
{`
- `public GunFireModeType fireModeType = GunFireModeType.SemiAuto;`
- `public float fireInterval = 0.15f;`
- `public int burstCount = 3;`
- `public float burstShotInterval = 0.08f;`
- `public float burstCycleInterval = 0.35f;`
- `public bool allowHoldToFire = true;`
- `public float chargeTime = 0f;`
### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunHandIKBinder.cs`
- `public class GunHandIKBinder : MonoBehaviour
{`
- `public bool IsRightHandIKBindingEnabled => runtimeBindRightHand;`
- `public bool IsLeftHandIKBindingEnabled => runtimeBindLeftHand;`
- `public void SetRightHandIKBindingEnabled(bool enabled)
    {`
- `public void SetLeftHandIKBindingEnabled(bool enabled)
    {`
- `public void SetHandIKBindingEnabled(bool rightEnabled, bool leftEnabled)
    {`
- `public void Bind(
        SkeletonAnimation skeletonAnimation,
        WeaponRoot weaponRoot,
        GunBoneMountFollower mountFollower = null)
    {`
- `public void SetWeapon(WeaponRoot weapon)
    {`
- `public void SetSkeletonAnimation(SkeletonAnimation skeletonAnimation)
    {`
### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunHandOcclusionByAimAngle.cs`
- `public class GunHandOcclusionByAimAngle : MonoBehaviour, ICharacterSubsystem, ICharacterLateTickable
{`
- `public void Initialize(CharacterRoot root)
    {`
- `public void ManagedLateUpdate(float deltaTime)
    {`
### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunModule.cs`
- `public class GunWeaponModule : MonoBehaviour, IWeaponSubsystem
{`
- `public SkeletonAnimation WeaponSkeletonAnimation
    {`
- `public string MuzzleBoneName => muzzleBoneName;`
- `public void Initialize(WeaponRoot root)
    {`
- `public bool TryGetMuzzleBone(out Bone muzzleBone, out SkeletonAnimation skeletonAnimation)
    {`
### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunMuzzlePointBinder.cs`
- `public class GunMuzzlePointBinder : MonoBehaviour
{`
- `public bool IsBound =>
        boundGunModule != null &&
        boundSkeletonAnimation != null &&
        boundMuzzleBone != null;`
- `public void SetWeaponRoot(WeaponRoot root)
    {`
- `public void ClearBinding()
    {`
### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunShotVisualController.cs`
- `public class GunShotVisualController : MonoBehaviour
{`
- `public void PlayShot(
        Vector2 origin,
        Vector2 endPoint,
        RaycastHit2D hit,
        Transform muzzlePoint
    )
    {`
- `public void PlayMuzzleFlash(Transform muzzlePoint, Vector2 shootDirection)
    {`
- `public void PlayImpact(Vector3 position, Vector2 normal, bool hitEnemy)
    {`
### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunSystemController.cs`
- `public class GunSystemController : MonoBehaviour, ICharacterSubsystem,ICharacterTickable
{`
- `public int AmmoInMagazine => ammoInMagazine;`
- `public int MagazineSize => magazineSize;`
- `public void Initialize(CharacterRoot root)
    {`
- `public void ManagedUpdate(float deltaTime)
    {`
- `public void SetCurrentLockTargetPoint(LockTargetPoint targetPoint)
    {`
- `public bool CanFire()
    {`
- `public bool TryFire()
    {`
- `public bool CanStartReload()
    {`
- `public void BeginReload()
    {`
- `public void CommitReload()
    {`
- `public void EndReload()
    {`
- `public void ApplyWeaponRuntimeData(WeaponRuntimeData weaponRuntimeData)
    {`
- `public GunFireModeType CurrentFireModeType => currentFireModeType;`
- `public GunFireModeConfig CurrentFireModeConfig => currentFireModeConfig;`
- `public void SwitchToNextFireMode()
    {`
- `public bool SetFireMode(GunFireModeType modeType)
    {`
- `public bool TryHandleFireInput(bool attackPressedThisFrame, bool attackHeld)
    {`
- `public void SetRightHandIKLocked(bool locked)
    {`
- `public void SetLeftHandIKLocked(bool locked)
    {`
- `public void SetHandIKLocked(bool rightLocked, bool leftLocked)
    {`
- `public void ReleaseLeftHandForAction()
    {`
- `public void RestoreLeftHandIK()
    {`
- `public void ClearWeaponRuntimeData()
    {`

## Referenced assets

- No explicit `Assets/...` references detected.

## Complete source

### `Assets/Script/Character/Core/Systems/StatsSystem/WeaponStats/WeaponStatsSOEditor.cs`

```cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponStatsSO))]
public class WeaponStatsSOEditor : Editor
{
    private SerializedProperty weaponTypeProp;
    private SerializedProperty coreStatsProp;
    private SerializedProperty characterBonusStatsProp;
    private SerializedProperty firearmStatsProp;
    private SerializedProperty meleeStatsProp;

    private void OnEnable()
    {
        weaponTypeProp = serializedObject.FindProperty("weaponType");
        coreStatsProp = serializedObject.FindProperty("commonCoreStats");
        characterBonusStatsProp = serializedObject.FindProperty("characterBonusStats");
        firearmStatsProp = serializedObject.FindProperty("firearmStats");
        meleeStatsProp = serializedObject.FindProperty("meleeStats");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("武器数值配置", EditorStyles.boldLabel);

        DrawRequiredProperty(weaponTypeProp, "武器类型");
        DrawRequiredProperty(coreStatsProp, "武器核心硬数值");
        DrawRequiredProperty(characterBonusStatsProp, "装备后角色加成");

        if (weaponTypeProp != null)
        {
            WeaponType weaponType = (WeaponType)weaponTypeProp.enumValueIndex;

            switch (weaponType)
            {
                case WeaponType.Firearm:
                    DrawRequiredProperty(firearmStatsProp, "枪械硬数值");
                    break;

                case WeaponType.Melee:
                    DrawRequiredProperty(meleeStatsProp, "近战硬数值");
                    break;
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawRequiredProperty(SerializedProperty property, string label)
    {
        if (property == null)
        {
            EditorGUILayout.HelpBox($"找不到字段：{label}。请检查 WeaponStatsSO 字段名。", MessageType.Error);
            return;
        }

        EditorGUILayout.PropertyField(property, new GUIContent(label), true);
    }
}
#endif
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/AutoAttackGizmosDrawer.cs`

```cs
using UnityEngine;

[ExecuteAlways]
public class AutoAttackGizmosDrawer : MonoBehaviour
{
    //[Header("References")]
    [SerializeField] private CharacterRoot characterRoot;
    [SerializeField] private CharacterStatsController statsController;
    [SerializeField] private BoxCollider2D bodyCollider;

    [Header("Foot Point")]
    [Tooltip("如果指定了这个点，则优先用它作为索敌圈和连线起点")]
    [SerializeField] private Transform footPointOverride;

    [Tooltip("没有 FootPointOverride 时，是否使用 BoxCollider2D 底部作为脚底点")]
    [SerializeField] private bool useColliderBottomAsFootPoint = false;

    [SerializeField] private Vector3 footPointOffset = Vector3.zero;

    [Header("Draw Options")]
    [SerializeField] private bool drawSearchRange = true;
    [SerializeField] private bool drawAttackRange = true;
    [SerializeField] private bool drawTargetLine = true;
    [SerializeField] private bool drawFootPoint = true;

    [Tooltip("勾上后只有选中角色时才画")]
    [SerializeField] private bool drawOnlyWhenSelected = false;

    [Header("Colors")]
    [SerializeField] private Color searchRangeColor = new Color(1f, 0.9f, 0.1f, 0.9f);
    [SerializeField] private Color attackRangeColor = new Color(1f, 0.1f, 0.1f, 0.9f);
    [SerializeField] private Color targetLineColor = new Color(0.1f, 1f, 0.2f, 1f);
    [SerializeField] private Color targetPointColor = new Color(0.1f, 1f, 0.2f, 1f);
    [SerializeField] private Color footPointColor = Color.white;
    [SerializeField] private Color warningColor = Color.magenta;

    [Header("Debug")]
    [SerializeField] private bool logRangeWhenPlaying = false;

    private float lastLogTime;

    private void Reset()
    {
        AutoFindReferences();
    }

    private void Awake()
    {
        AutoFindReferences();
    }

    private void OnValidate()
    {
        AutoFindReferences();
    }

    private void AutoFindReferences()
    {
        if (characterRoot == null)
            characterRoot = GetComponent<CharacterRoot>();

        if (statsController == null)
            statsController = GetComponent<CharacterStatsController>();

        if (bodyCollider == null)
            bodyCollider = GetComponent<BoxCollider2D>();
    }

    private void OnDrawGizmos()
    {
        if (drawOnlyWhenSelected)
            return;

        DrawGizmosInternal();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawOnlyWhenSelected)
            return;

        DrawGizmosInternal();
    }

    private void DrawGizmosInternal()
    {
        AutoFindReferences();

        if (characterRoot == null)
            return;

        Vector3 origin = GetFootPoint();

        if (drawFootPoint)
        {
            Gizmos.color = footPointColor;
            Gizmos.DrawWireSphere(origin, 0.12f);
        }

        float searchRange = 0f;
        float attackRange = 0f;

        if (statsController != null)
        {
            searchRange = statsController.GetStat(CharacterStats.SearchRange);
            //attackRange = statsController.GetStat(CharacterStats.AttackRange);
        }

        if (Application.isPlaying && logRangeWhenPlaying)
        {
            if (Time.realtimeSinceStartup - lastLogTime > 1f)
            {
                lastLogTime = Time.realtimeSinceStartup;
                Debug.Log(
                    $"[AutoAttackGizmosDrawer] SearchRange={searchRange}, AttackRange={attackRange}, Origin={origin}",
                    this
                );
            }
        }

        if (drawSearchRange)
        {
            if (searchRange > 0f)
            {
                Gizmos.color = searchRangeColor;
                Gizmos.DrawWireSphere(origin, searchRange);
            }
            else
            {
                Gizmos.color = warningColor;
                Gizmos.DrawWireSphere(origin, 0.35f);
            }
        }

        if (drawAttackRange)
        {
            if (attackRange > 0f)
            {
                Gizmos.color = attackRangeColor;
                Gizmos.DrawWireSphere(origin, attackRange);
            }
        }

        if (drawTargetLine)
        {
            DrawTargetLine(origin);
        }
    }

    private Vector3 GetFootPoint()
    {
        Vector3 point;

        if (footPointOverride != null)
        {
            point = footPointOverride.position;
        }
        else if (useColliderBottomAsFootPoint && bodyCollider != null)
        {
            Bounds bounds = bodyCollider.bounds;
            point = new Vector3(bounds.center.x, bounds.min.y, transform.position.z);
        }
        else
        {
            point = transform.position;
        }

        point += footPointOffset;
        return point;
    }

    private void DrawTargetLine(Vector3 origin)
    {
        CharacterDataBus dataBus = characterRoot.characterDataBus;

        if (dataBus == null)
            return;

        if (!dataBus.hasAttackTarget)
            return;

        if (dataBus.lockedAttackTarget == null)
            return;

        if (!dataBus.lockedAttackTarget.IsValid())
            return;

        Vector3 targetPoint = dataBus.lockedAttackTarget.GetWorldPosition();

        Gizmos.color = targetLineColor;
        Gizmos.DrawLine(origin, targetPoint);

        Gizmos.color = targetPointColor;
        Gizmos.DrawWireSphere(targetPoint, 2f);
    }
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/CharacterEquipmentController.cs`

```cs
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
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/CharacterWeaponInventory.cs`

```cs
using System;
using UnityEngine;

/// <summary>
/// Adapter layer between the new PackageSystem (CharacterWeaponSlots) and the existing weapon pipeline.
/// Reads from CharacterWeaponSlots at runtime; no longer accepts test injection from Inspector.
/// </summary>
[DisallowMultipleComponent]
public class CharacterWeaponInventory :
    MonoBehaviour,
    ICharacterSubsystem
{
    private const int SlotCount = 3;

    [Header("Weapon Slots")]
    [SerializeField]
    private CharacterWeaponSlot[] slots =
        new CharacterWeaponSlot[SlotCount];

    [Header("Runtime")]
    [SerializeField]
    private WeaponSlotId activeSlotId =
        WeaponSlotId.Slot1;

    private CharacterRoot characterRoot;
    private CharacterDataBus dataBus;
    private CharacterPackageSystem packageSystem;
    private bool isInitialized;

    public WeaponSlotId ActiveSlotId => activeSlotId;

    public WeaponRuntimeData ActiveWeapon
    {
        get
        {
            CharacterWeaponSlot slot =
                GetSlot(activeSlotId);

            return slot != null
                ? slot.Weapon
                : null;
        }
    }

    public bool HasActiveWeapon =>
        ActiveWeapon != null;

    public bool ActiveWeaponIsFirearm =>
        ActiveWeapon != null &&
        ActiveWeapon.IsFirearm;

    public bool ActiveWeaponIsMelee =>
        ActiveWeapon != null &&
        ActiveWeapon.IsMelee;

    public event Action<
        WeaponSlotId,
        WeaponRuntimeData,
        WeaponSlotId,
        WeaponRuntimeData> ActiveWeaponChanged;

    public void Initialize(CharacterRoot root)
    {
        if (isInitialized)
            return;

        characterRoot = root;

        if (characterRoot == null)
        {
            Debug.LogError("[CharacterWeaponInventory] CharacterRoot == null", this);
            return;
        }

        dataBus = characterRoot.characterDataBus;
        packageSystem = characterRoot.characterPackageSystem;

        if (dataBus == null)
        {
            Debug.LogError("[CharacterWeaponInventory] CharacterDataBus == null", this);
            return;
        }

        EnsureSlots();

        // Sync from PackageSystem if available (new game init already placed weapons there)
        if (packageSystem != null && packageSystem.WeaponSlots != null)
        {
            // Initialize activeSlotId from PackageSystem
            activeSlotId = (WeaponSlotId)packageSystem.WeaponSlots.ActiveSlotIndex;

            SyncFromPackageSystem();

            // Subscribe to runtime weapon slot and active slot changes
            packageSystem.OnWeaponSlotChanged += OnPackageWeaponSlotChanged;
            packageSystem.OnActiveWeaponSlotChanged += OnPackageActiveWeaponSlotChanged;
        }

        dataBus.weaponInventory = this;

        MigrateLegacyCurrentWeapon();
        SyncLegacyCurrentWeapon();

        isInitialized = true;
    }

    private void OnPackageActiveWeaponSlotChanged(int previousIndex, int newIndex)
    {
        WeaponRuntimeData previousWeapon = ActiveWeapon;
        activeSlotId = (WeaponSlotId)newIndex;
        SyncLegacyCurrentWeapon();
        NotifyActiveWeaponChanged((WeaponSlotId)previousIndex, previousWeapon, activeSlotId, ActiveWeapon);
    }

    /// <summary>
    /// Called when CharacterPackageSystem weapon slots change at runtime (e.g. drag-drop equip/unequip).
    /// Syncs the new weapon data into the legacy pipeline.
    /// Does NOT auto-switch active slot when a weapon is removed — allows empty active slot.
    /// </summary>
    private void OnPackageWeaponSlotChanged(int slotIndex, ItemInstance item)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return;

        CharacterWeaponSlot slot = GetSlot((WeaponSlotId)slotIndex);
        if (slot == null) return;

        bool isAcitveSlot = (slotIndex == (int)activeSlotId);

        if (item == null)
        {
            // Weapon was removed from this slot
            WeaponRuntimeData previousActive = isAcitveSlot ? ActiveWeapon : null;

            if (!slot.IsEmpty)
                slot.RemoveWeapon();

            // Only notify if active slot changed
            if (isAcitveSlot)
            {
                NotifyActiveWeaponChanged(activeSlotId, previousActive, activeSlotId, null);
            }
        }
        else if (item.definition is WeaponItemDefinitionSO weaponDef)
        {
            WeaponRuntimeData previousActive = isAcitveSlot ? ActiveWeapon : null;

            // Cache: reuse existing WeaponRuntimeData if same instanceId
            WeaponRuntimeData runtimeData = FindOrCreateRuntimeData(item, weaponDef);
            slot.SetWeapon(runtimeData);

            if (isAcitveSlot)
            {
                NotifyActiveWeaponChanged(activeSlotId, previousActive, activeSlotId, slot.Weapon);
            }
        }

        SyncLegacyCurrentWeapon();
    }

    private readonly System.Collections.Generic.Dictionary<string, WeaponRuntimeData> _runtimeDataCache = new();

    private WeaponRuntimeData FindOrCreateRuntimeData(ItemInstance item, WeaponItemDefinitionSO weaponDef)
    {
        if (!string.IsNullOrEmpty(item.instanceId) && _runtimeDataCache.TryGetValue(item.instanceId, out var cached))
            return cached;

        var data = new WeaponRuntimeData(weaponDef.WeaponConfig);
        if (!string.IsNullOrEmpty(item.instanceId))
            _runtimeDataCache[item.instanceId] = data;
        return data;
    }

    /// <summary>
    /// Copy weapon instances from CharacterWeaponSlots into the legacy slot array.
    /// This bridges the new ItemInstance-based system with the existing WeaponRuntimeData pipeline.
    /// </summary>
    private void SyncFromPackageSystem()
    {
        var weaponSlots = packageSystem.WeaponSlots;
        for (int i = 0; i < SlotCount && i < weaponSlots.Capacity; i++)
        {
            var itemInstance = weaponSlots.GetItem(i);
            if (itemInstance == null) continue;

            // Extract WeaponConfigSO from the WeaponItemDefinitionSO
            if (itemInstance.definition is WeaponItemDefinitionSO weaponDef)
            {
                var slot = GetSlot((WeaponSlotId)i);
                if (slot != null && slot.IsEmpty)
                {
                    slot.SetWeapon(new WeaponRuntimeData(weaponDef.WeaponConfig));
                }
            }
        }
    }

    public CharacterWeaponSlot GetSlot(WeaponSlotId slotId)
    {
        int index = (int)slotId;
        if (slots == null || index < 0 || index >= slots.Length)
            return null;
        return slots[index];
    }

    public WeaponRuntimeData GetWeapon(WeaponSlotId slotId)
    {
        CharacterWeaponSlot slot = GetSlot(slotId);
        return slot != null ? slot.Weapon : null;
    }

    public bool HasWeapon(WeaponSlotId slotId)
    {
        return GetWeapon(slotId) != null;
    }

    public bool CanSelectSlot(WeaponSlotId slotId)
    {
        CharacterWeaponSlot slot = GetSlot(slotId);
        return slot != null && slot.CanSelect;
    }

    public bool TryAssignWeapon(
        WeaponSlotId slotId,
        WeaponRuntimeData weapon,
        bool replaceExisting = false)
    {
        if (weapon == null)
            return false;

        CharacterWeaponSlot slot = GetSlot(slotId);
        if (slot == null || !slot.IsUnlocked)
            return false;

        if (!slot.IsEmpty && !replaceExisting)
            return false;

        WeaponRuntimeData oldActiveWeapon = ActiveWeapon;
        slot.SetWeapon(weapon);

        if (slotId == activeSlotId)
        {
            NotifyActiveWeaponChanged(activeSlotId, oldActiveWeapon, activeSlotId, weapon);
        }

        return true;
    }

    public WeaponRuntimeData RemoveWeapon(WeaponSlotId slotId)
    {
        CharacterWeaponSlot slot = GetSlot(slotId);
        if (slot == null || slot.IsEmpty)
            return null;

        WeaponRuntimeData oldActiveWeapon = ActiveWeapon;
        WeaponRuntimeData removedWeapon = slot.RemoveWeapon();

        if (slotId == activeSlotId)
        {
            NotifyActiveWeaponChanged(activeSlotId, oldActiveWeapon, activeSlotId, null);
        }

        return removedWeapon;
    }

    public bool TrySetActiveSlot(WeaponSlotId slotId)
    {
        // Route through PackageSystem when available
        if (packageSystem != null && packageSystem.WeaponSlots != null)
        {
            return packageSystem.SetActiveWeaponSlot((int)slotId);
        }

        // Legacy fallback (no PackageSystem)
        if (!CanSelectSlot(slotId))
            return false;
        if (slotId == activeSlotId)
            return true;

        WeaponSlotId previousSlotId = activeSlotId;
        WeaponRuntimeData previousWeapon = ActiveWeapon;
        activeSlotId = slotId;
        NotifyActiveWeaponChanged(previousSlotId, previousWeapon, activeSlotId, ActiveWeapon);
        return true;
    }

    public bool TryGetNextOccupiedSlot(out WeaponSlotId nextSlotId)
    {
        return TryGetNextOccupiedSlotFrom(activeSlotId, out nextSlotId);
    }

    public bool TrySelectNextOccupiedSlot()
    {
        if (!TryGetNextOccupiedSlot(out WeaponSlotId nextSlotId))
            return false;
        return TrySetActiveSlot(nextSlotId);
    }

    private void EnsureSlots()
    {
        if (slots == null || slots.Length != SlotCount)
            Array.Resize(ref slots, SlotCount);

        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] == null)
                slots[i] = new CharacterWeaponSlot();
            slots[i].Initialize((WeaponSlotId)i);
        }
    }

    private void MigrateLegacyCurrentWeapon()
    {
        if (dataBus.currentWeaponRuntimeData == null)
            return;

        CharacterWeaponSlot firstSlot = GetSlot(WeaponSlotId.Slot1);
        if (firstSlot == null || !firstSlot.IsEmpty)
            return;

        firstSlot.SetWeapon(dataBus.currentWeaponRuntimeData);
        activeSlotId = WeaponSlotId.Slot1;

        Debug.Log("[CharacterWeaponInventory] Migrated currentWeaponRuntimeData to Slot1", characterRoot);
    }

    private void NotifyActiveWeaponChanged(
        WeaponSlotId previousSlotId,
        WeaponRuntimeData previousWeapon,
        WeaponSlotId newSlotId,
        WeaponRuntimeData newWeapon)
    {
        SyncLegacyCurrentWeapon();
        ActiveWeaponChanged?.Invoke(previousSlotId, previousWeapon, newSlotId, newWeapon);
    }

    public bool TryGetNextOccupiedSlotFrom(WeaponSlotId fromSlotId, out WeaponSlotId nextSlotId)
    {
        nextSlotId = fromSlotId;
        if (slots == null || slots.Length == 0)
            return false;

        int startIndex = (int)fromSlotId;
        for (int offset = 1; offset <= slots.Length; offset++)
        {
            int candidateIndex = (startIndex + offset) % slots.Length;
            CharacterWeaponSlot candidate = slots[candidateIndex];
            if (candidate == null) continue;
            if (!candidate.CanSelect) continue;
            nextSlotId = candidate.SlotId;
            return true;
        }
        return false;
    }

    private void SyncLegacyCurrentWeapon()
    {
        if (dataBus == null)
            return;
        dataBus.currentWeaponRuntimeData = ActiveWeapon;
    }
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/CharacterWeaponSlot.cs`

```cs
using System;
using UnityEngine;

[Serializable]
public class CharacterWeaponSlot
{
    [SerializeField]
    private WeaponSlotId slotId;

    /*
     * WeaponRuntimeData 如果不是 Unity 可序列化类型，
     * Inspector 中不会显示，但运行时赋值仍然可以使用。
     */
    [SerializeField]
    private WeaponRuntimeData weaponRuntimeData;

    [SerializeField]
    private bool isUnlocked = true;

    public WeaponSlotId SlotId => slotId;

    public WeaponRuntimeData Weapon => weaponRuntimeData;

    public bool IsUnlocked => isUnlocked;

    public bool IsEmpty => weaponRuntimeData == null;

    public bool CanSelect =>
        isUnlocked && weaponRuntimeData != null;

    public void Initialize(WeaponSlotId id)
    {
        slotId = id;
    }

    public void SetWeapon(WeaponRuntimeData weapon)
    {
        weaponRuntimeData = weapon;
    }

    public WeaponRuntimeData RemoveWeapon()
    {
        WeaponRuntimeData removedWeapon = weaponRuntimeData;
        weaponRuntimeData = null;
        return removedWeapon;
    }

    public void SetUnlocked(bool unlocked)
    {
        isUnlocked = unlocked;
    }
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/Debug/WeaponSlotSwitchDebug.cs`

```cs
using UnityEngine;

public class WeaponSlotSwitchDebug :
    MonoBehaviour
{
    [SerializeField]
    private CharacterRoot characterRoot;

    private WeaponEquipStateMachine EquipStateMachine
    {
        get
        {
            if (characterRoot == null ||
                characterRoot.stateArbitrator == null)
            {
                return null;
            }

            return characterRoot
                .stateArbitrator
                .weaponEquipSM;
        }
    }

    [ContextMenu("Switch To Slot 1")]
    private void SwitchToSlot1()
    {
        EquipStateMachine?.RequestSwitchSlot(
            WeaponSlotId.Slot1
        );
    }

    [ContextMenu("Switch To Slot 2")]
    private void SwitchToSlot2()
    {
        EquipStateMachine?.RequestSwitchSlot(
            WeaponSlotId.Slot2
        );
    }

    [ContextMenu("Switch To Slot 3")]
    private void SwitchToSlot3()
    {
        EquipStateMachine?.RequestSwitchSlot(
            WeaponSlotId.Slot3
        );
    }

    [ContextMenu("Switch To Next Slot")]
    private void SwitchToNextSlot()
    {
        EquipStateMachine?.RequestSwitchToNextSlot();
    }
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/ExternalWeaponMount.cs`

```cs
using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 把武器 prefab 挂到角色 LongRangeWeapon 骨骼上
///处理武器排序
///保存 currentWeapon
/// </summary>
[DefaultExecutionOrder(10000)]
public class ExternalWeaponMount : MonoBehaviour
{
    [System.Serializable]
    public struct BindData
    {
        public SkeletonRenderer SkeletonRenderer;
        public SkeletonRenderSeparator RenderSeparator;
        public string FollowBoneName;
        public string RenderSlotName;
    }

    [Header("Runtime Binding")]
    [SerializeField] private SkeletonRenderer skeletonRenderer;
    [SerializeField] private SkeletonRenderSeparator renderSeparator;
    [SerializeField] private string followBoneName = "LongRangeWeapon";
    [SerializeField] private string renderSlotName = "LongRangeWeapon";

    [Header("Weapon Sorting Boundary")]
    [SerializeField] private SkeletonPartsRenderer weaponBackPart;
    [SerializeField] private SkeletonPartsRenderer weaponFrontPart;
    private int lastAppliedSeparatorRank = -1;
    private int lastAppliedSortingOrder = int.MinValue;



    [Header("Weapon")]
    [SerializeField] private WeaponRoot currentWeapon;
    [SerializeField] private Renderer[] weaponRenderers;

    [Header("Sorting")]
    [SerializeField] private bool autoApplySorting = true;
    [SerializeField] private int weaponSortingOffsetFromLowerPart = 5;

    [Header("Render Layer")]
    [SerializeField] private bool inheritMountLayer = true;
    [SerializeField] private bool applyLayerToWholeWeapon = false;


    private BoneFollower boneFollower;

    public WeaponRoot CurrentWeapon => currentWeapon;

    public void Bind(BindData data)
    {
        skeletonRenderer = data.SkeletonRenderer;
        renderSeparator = data.RenderSeparator;

        if (!string.IsNullOrEmpty(data.FollowBoneName))
            followBoneName = data.FollowBoneName;

        if (!string.IsNullOrEmpty(data.RenderSlotName))
            renderSlotName = data.RenderSlotName;

        BindMountFollower();
        if (currentWeapon != null)
            BindGunIKBinder(currentWeapon);
        ApplySorting();
    }

    private void BindMountFollower()
    {
        GunBoneMountFollower follower = GetComponent<GunBoneMountFollower>();

        if (follower == null)
        {
            Debug.LogError(
                "[ExternalWeaponMount] 当前物体上没有 GunBoneMountFollower，无法绑定骨骼跟随。",
                this
            );
            return;
        }

        if (skeletonRenderer == null)
        {
            Debug.LogError(
                "[ExternalWeaponMount] skeletonRenderer 为空，无法绑定 GunBoneMountFollower。",
                this
            );
            return;
        }

        SkeletonAnimation skeletonAnimation =
            skeletonRenderer.GetComponent<SkeletonAnimation>();

        if (skeletonAnimation == null)
        {
            Debug.LogError(
                $"[ExternalWeaponMount] {skeletonRenderer.name} 上没有 SkeletonAnimation 组件，无法绑定 GunBoneMountFollower。",
                skeletonRenderer
            );
            return;
        }

        follower.Bind(skeletonAnimation, followBoneName);
    }




    private void LateUpdate()
    {
        ApplySortingFromCurrentDrawOrder();
    }
    private void ApplySortingFromCurrentDrawOrder()
    {
        if (!autoApplySorting)
            return;

        if (currentWeapon == null)
            return;

        if (skeletonRenderer == null ||
            skeletonRenderer.Skeleton == null)
        {
            return;
        }

        if (renderSeparator == null ||
            renderSeparator.partsRenderers == null)
        {
            return;
        }

        List<Spine.Slot> separatorSlots =
            skeletonRenderer.separatorSlots;

        if (separatorSlots == null || separatorSlots.Count == 0)
            return;

        Spine.Slot targetSlot =
            skeletonRenderer.Skeleton.FindSlot(renderSlotName);

        if (targetSlot == null)
            return;

        if (!separatorSlots.Contains(targetSlot))
        {
            Debug.LogError(
                $"[ExternalWeaponMount] Slot [{renderSlotName}] " +
                "没有配置到 SkeletonRenderer.separatorSlots。",
                this
            );
            return;
        }

        int separatorRank = GetCurrentSeparatorRank(
            targetSlot,
            separatorSlots
        );

        if (separatorRank < 0)
            return;

        int lowerPartIndex = separatorRank;
        int upperPartIndex = separatorRank + 1;

        if (lowerPartIndex < 0 ||
            upperPartIndex >= renderSeparator.partsRenderers.Count)
        {
            Debug.LogError(
                $"[ExternalWeaponMount] PartsRenderer 数量不足。" +
                $" Rank={separatorRank}, " +
                $"Parts={renderSeparator.partsRenderers.Count}",
                this
            );
            return;
        }

        SkeletonPartsRenderer lowerPart =
            renderSeparator.partsRenderers[lowerPartIndex];

        SkeletonPartsRenderer upperPart =
            renderSeparator.partsRenderers[upperPartIndex];

        if (lowerPart == null || upperPart == null)
            return;

        MeshRenderer lowerRenderer = lowerPart.MeshRenderer;
        MeshRenderer upperRenderer = upperPart.MeshRenderer;

        if (lowerRenderer == null || upperRenderer == null)
            return;

        int lowerOrder = lowerRenderer.sortingOrder;
        int upperOrder = upperRenderer.sortingOrder;

        int minOrder = Mathf.Min(lowerOrder, upperOrder);
        int maxOrder = Mathf.Max(lowerOrder, upperOrder);

        if (maxOrder - minOrder < 2)
        {
            Debug.LogError(
                $"[ExternalWeaponMount] Separator 两侧排序间隔不足：" +
                $"{lowerOrder} / {upperOrder}",
                this
            );
            return;
        }

        int weaponOrder =
            minOrder + ((maxOrder - minOrder) / 2);

        // 没变化就不重复设置。
        if (separatorRank == lastAppliedSeparatorRank &&
            weaponOrder == lastAppliedSortingOrder)
        {
            return;
        }

        if (weaponRenderers == null || weaponRenderers.Length == 0)
        {
            weaponRenderers =
                currentWeapon.GetComponentsInChildren<Renderer>(true);
        }

        if (weaponRenderers == null || weaponRenderers.Length == 0)
            return;

        for (int i = 0; i < weaponRenderers.Length; i++)
        {
            Renderer weaponRenderer = weaponRenderers[i];

            if (weaponRenderer == null)
                continue;

            weaponRenderer.sortingLayerID =
                lowerRenderer.sortingLayerID;

            weaponRenderer.sortingOrder = weaponOrder;
        }

        lastAppliedSeparatorRank = separatorRank;
        lastAppliedSortingOrder = weaponOrder;

        //Debug.Log($"[ExternalWeaponMount] Slot={renderSlotName}, " +$"SeparatorRank={separatorRank}, " + $"Part={lowerPartIndex}->{upperPartIndex}, " +
        //    $"Order={lowerOrder}->{weaponOrder}->{upperOrder}",
        //    this
        //);
    }
    private int GetCurrentSeparatorRank(
    Spine.Slot targetSlot,
    List<Spine.Slot> separatorSlots)
    {
        if (skeletonRenderer == null ||
            skeletonRenderer.Skeleton == null)
        {
            return -1;
        }

        Spine.ExposedList<Spine.Slot> drawOrder =
            skeletonRenderer.Skeleton.DrawOrder.AppliedPose;

        if (drawOrder == null)
            return -1;

        int rank = 0;

        for (int i = 0; i < drawOrder.Count; i++)
        {
            Spine.Slot currentSlot = drawOrder.Items[i];

            if (currentSlot == null)
                continue;

            if (!separatorSlots.Contains(currentSlot))
                continue;

            if (currentSlot == targetSlot)
                return rank;

            rank++;
        }

        return -1;
    }
    public WeaponRoot SetWeaponPrefab(WeaponRoot weaponPrefab)
    {
        ClearWeapon();

        if (weaponPrefab == null)
            return null;
        currentWeapon = Instantiate(weaponPrefab, transform);

        currentWeapon.transform.localPosition = Vector3.zero;
        currentWeapon.transform.localRotation = Quaternion.identity;
        currentWeapon.transform.localScale = Vector3.one;

        // 必须在武器开始参与渲染前，将它并入角色相机的渲染层。
        ApplyWeaponRenderLayer(currentWeapon);

        weaponRenderers = currentWeapon.GetComponentsInChildren<Renderer>(true);

        BindWeaponFollower(currentWeapon);

        BindGunIKBinder(currentWeapon);
        ApplySorting();

        return currentWeapon;
    }

    private void BindGunIKBinder(WeaponRoot weapon)
    {
        GunHandIKBinder ikBinder = GetComponent<GunHandIKBinder>();

        if (ikBinder == null)
            return;

        if (skeletonRenderer == null)
            return;

        SkeletonAnimation skeletonAnimation =
            skeletonRenderer.GetComponent<SkeletonAnimation>();

        if (skeletonAnimation == null)
            return;

        GunBoneMountFollower follower = GetComponent<GunBoneMountFollower>();

        ikBinder.Bind(
            skeletonAnimation,
            weapon,
            follower
        );
    }

    public void ClearWeapon()
    {
        if (currentWeapon != null)
        {
            Destroy(currentWeapon.gameObject);
        }
        currentWeapon = null;
        weaponRenderers = null;
        GunBoneMountFollower follower = GetComponent<GunBoneMountFollower>();
        if (follower != null)
            follower.SetWeapon(null);
        GunHandIKBinder ikBinder = GetComponent<GunHandIKBinder>();
        if (ikBinder != null)
            ikBinder.SetWeapon(null);
    }

    private void Reset()
    {
        //boneFollower = GetComponent<BoneFollower>();
    }

    private void Awake()
    {
        BindMountFollower();
        ApplySorting();
    }

    private void ApplySorting()
    {

        if (!autoApplySorting)
            return;

        if (weaponBackPart == null || weaponFrontPart == null)
        {
            Debug.LogError(
                "[ExternalWeaponMount] 请配置 Weapon Back Part 和 Weapon Front Part。",
                this
            );
            return;
        }

        MeshRenderer backRenderer = weaponBackPart.MeshRenderer;
        MeshRenderer frontRenderer = weaponFrontPart.MeshRenderer;

        if (backRenderer == null || frontRenderer == null)
        {
            Debug.LogError(
                "[ExternalWeaponMount] 武器排序边界缺少 MeshRenderer。",
                this
            );
            return;
        }

        if (backRenderer.sortingLayerID != frontRenderer.sortingLayerID)
        {
            Debug.LogError(
                "[ExternalWeaponMount] MainBody 和 Hand_L2 不在同一个 Sorting Layer。",
                this
            );
            return;
        }

        int backOrder = backRenderer.sortingOrder;
        int frontOrder = frontRenderer.sortingOrder;

        int minOrder = Mathf.Min(backOrder, frontOrder);
        int maxOrder = Mathf.Max(backOrder, frontOrder);

        if (maxOrder - minOrder < 2)
        {
            Debug.LogError(
                $"[ExternalWeaponMount] 武器两侧排序间隔不足：" +
                $"{weaponBackPart.name}={backOrder}, " +
                $"{weaponFrontPart.name}={frontOrder}",
                this
            );
            return;
        }

        int weaponOrder = minOrder + (maxOrder - minOrder) / 2;

        if (currentWeapon != null)
        {
            weaponRenderers =
                currentWeapon.GetComponentsInChildren<Renderer>(true);
        }

        if (weaponRenderers == null || weaponRenderers.Length == 0)
        {
            Debug.LogWarning(
                "[ExternalWeaponMount] 当前武器下没有找到 Renderer。",
                this
            );
            return;
        }

        for (int i = 0; i < weaponRenderers.Length; i++)
        {
            Renderer weaponRenderer = weaponRenderers[i];

            if (weaponRenderer == null)
                continue;

            weaponRenderer.sortingLayerID = backRenderer.sortingLayerID;
            weaponRenderer.sortingOrder = weaponOrder;
        }

        Debug.Log(
            $"[ExternalWeaponMount] Weapon sorting applied: " +
            $"{weaponBackPart.name}={backOrder}, " +
            $"Weapon={weaponOrder}, " +
            $"{weaponFrontPart.name}={frontOrder}",
            this
        );
    }


    private void BindWeaponFollower(WeaponRoot weapon)
    {
        GunBoneMountFollower follower =
            GetComponent<GunBoneMountFollower>();

        if (follower == null)
            return;

        follower.SetWeapon(weapon);
    }

    public void SetWeaponVisible(bool visible)
    {
        if (currentWeapon == null)
            return;

        if (weaponRenderers == null || weaponRenderers.Length == 0)
            weaponRenderers = currentWeapon.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < weaponRenderers.Length; i++)
        {
            Renderer renderer = weaponRenderers[i];

            if (renderer == null)
                continue;

            renderer.enabled = visible;
        }
    }

    private void ApplyWeaponRenderLayer(WeaponRoot weapon)
    {
        if (!inheritMountLayer || weapon == null)
            return;

        int targetLayer = gameObject.layer;

        GameObject targetRoot;

        if (applyLayerToWholeWeapon || weapon.VisualRoot == null)
        {
            targetRoot = weapon.gameObject;
        }
        else
        {
            targetRoot = weapon.VisualRoot.gameObject;
        }

        SetLayerRecursively(targetRoot, targetLayer);
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null)
            return;

        root.layer = layer;

        Transform rootTransform = root.transform;

        for (int i = 0; i < rootTransform.childCount; i++)
        {
            Transform child = rootTransform.GetChild(i);
            SetLayerRecursively(child.gameObject, layer);
        }
    }




}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/AimCursorRadialLimiter.cs`

```cs
using UnityEngine;

[DefaultExecutionOrder(10000)]
public class AimCursorRadialLimiter : MonoBehaviour
{
    [Header("References")]
    [Tooltip("受控制的光标 Transform，也就是角色身边的圆圈 Cursor")]
    [SerializeField] private Transform cursorTransform;

    [Tooltip("禁止圆圆心。通常就是角色根节点/腰部的根节点")]
    [SerializeField] private Transform centerTransform;

    [Header("Forbidden Circle")]
    [Tooltip("禁止圆的绝对圆不可进入半径")]
    [SerializeField] private float forbiddenRadius = 2.5f;

    [Tooltip("离开禁止状态的光标半径，必须大于 Forbidden Radius 一点")]
    [SerializeField] private float releaseExtraRadius = 0.15f;

    [Header("Arc Slide")]
    [Tooltip("沿圆周滑动的角速度，越大越紧跟，越小越平滑")]
    [SerializeField] private float maxAngularSpeed = 720f;

    [Tooltip("光标几乎压住圆心时，沿用上一个方向，防止光标瞬间翻转")]
    [SerializeField] private float centerDeadZone = 0.05f;

    [Tooltip("光标刚进入禁止圆心。无法判断走上弧还是下弧时，true 默认走上半弧，false 默认走下半弧")]
    [SerializeField] private bool preferUpperArc = true;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    private bool isInsideForbiddenZone;

    private Vector2 lastAllowedDirection = Vector2.right;
    private Vector2 lastRawCursorWorld;
    private bool hasLastRawCursorWorld;

    // +1 逆时针，-1 顺时针
    private int lockedArcSign = 1;

    private void LateUpdate()
    {
        ApplyLimit();
    }

    private void ApplyLimit()
    {
        if (cursorTransform == null)
            return;

        if (centerTransform == null)
            return;

        Vector3 rawCursorWorld3 = cursorTransform.position;
        Vector2 rawCursorWorld = rawCursorWorld3;

        Vector2 centerWorld = centerTransform.position;
        Vector2 delta = rawCursorWorld - centerWorld;

        float distance = delta.magnitude;
        float releaseRadius = forbiddenRadius + Mathf.Max(0f, releaseExtraRadius);

        // 在合法区域外，直接放行。
        if (!isInsideForbiddenZone && distance >= forbiddenRadius)
        {
            if (distance > 0.0001f)
                lastAllowedDirection = delta / distance;

            RememberRawCursor(rawCursorWorld);
            return;
        }

        // 已经进入禁止圆状态，只有超出 releaseRadius 才释放。
        if (isInsideForbiddenZone && distance >= releaseRadius)
        {
            isInsideForbiddenZone = false;

            if (distance > 0.0001f)
                lastAllowedDirection = delta / distance;

            RememberRawCursor(rawCursorWorld);
            return;
        }

        // 进入禁止圆的一瞬间，锁定方向。
        if (!isInsideForbiddenZone)
        {
            isInsideForbiddenZone = true;
            lockedArcSign = DecideArcSign(rawCursorWorld, centerWorld);
        }

        Vector2 rawDirection;

        if (distance > centerDeadZone)
        {
            rawDirection = delta / distance;
        }
        else
        {
            rawDirection = lastAllowedDirection;
        }

        if (rawDirection.sqrMagnitude < 0.0001f)
            rawDirection = Vector2.right;

        rawDirection.Normalize();

        Vector2 newDirection = MoveDirectionOnCircle(lastAllowedDirection, rawDirection);

        if (newDirection.sqrMagnitude < 0.0001f)
            newDirection = lastAllowedDirection;

        newDirection.Normalize();

        lastAllowedDirection = newDirection;

        Vector2 limitedCursorWorld = centerWorld + lastAllowedDirection * forbiddenRadius;

        cursorTransform.position = new Vector3(
            limitedCursorWorld.x,
            limitedCursorWorld.y,
            rawCursorWorld3.z
        );

        RememberRawCursor(rawCursorWorld);
    }

    private Vector2 MoveDirectionOnCircle(Vector2 currentDirection, Vector2 targetDirection)
    {
        if (currentDirection.sqrMagnitude < 0.0001f)
            currentDirection = Vector2.right;

        if (targetDirection.sqrMagnitude < 0.0001f)
            targetDirection = currentDirection;

        currentDirection.Normalize();
        targetDirection.Normalize();

        float currentAngle = Mathf.Atan2(currentDirection.y, currentDirection.x) * Mathf.Rad2Deg;
        float targetAngle = Mathf.Atan2(targetDirection.y, targetDirection.x) * Mathf.Rad2Deg;

        float deltaAngle = Mathf.DeltaAngle(currentAngle, targetAngle);

        float sign;

        // 当接近角色正后方移过去时，target 可能接近 180 度。
        // 此时强制锁定逆时针或顺时针，沿圆边缘走，防止光标瞬间翻转。
        if (Mathf.Abs(deltaAngle) > 170f)
        {
            sign = lockedArcSign;
        }
        else if (Mathf.Abs(deltaAngle) < 0.01f)
        {
            sign = lockedArcSign;
        }
        else
        {
            sign = Mathf.Sign(deltaAngle);
        }

        float maxStep = maxAngularSpeed * Time.deltaTime;
        float step = Mathf.Min(Mathf.Abs(deltaAngle), maxStep) * sign;

        float newAngle = currentAngle + step;
        float rad = newAngle * Mathf.Deg2Rad;

        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private int DecideArcSign(Vector2 rawCursorWorld, Vector2 centerWorld)
    {
        Vector2 currentDir = lastAllowedDirection;

        if (currentDir.sqrMagnitude < 0.0001f)
            currentDir = Vector2.right;

        currentDir.Normalize();

        Vector2 rawDir = rawCursorWorld - centerWorld;

        if (rawDir.sqrMagnitude > centerDeadZone * centerDeadZone)
        {
            rawDir.Normalize();

            float cross = Cross(currentDir, rawDir);

            if (Mathf.Abs(cross) > 0.001f)
                return cross > 0f ? 1 : -1;
        }

        // 如果 rawDir 判断不出来，就看光标移动方向在当前圆周切线上的分量。
        if (hasLastRawCursorWorld)
        {
            Vector2 rawMove = rawCursorWorld - lastRawCursorWorld;

            if (rawMove.sqrMagnitude > 0.000001f)
            {
                Vector2 counterClockwiseTangent = new Vector2(-currentDir.y, currentDir.x);
                float tangentAmount = Vector2.Dot(rawMove, counterClockwiseTangent);

                if (Mathf.Abs(tangentAmount) > 0.001f)
                    return tangentAmount > 0f ? 1 : -1;
            }
        }

        // 穷到底，默认走上弧或下弧。
        return preferUpperArc ? 1 : -1;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    private void RememberRawCursor(Vector2 rawCursorWorld)
    {
        lastRawCursorWorld = rawCursorWorld;
        hasLastRawCursorWorld = true;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        if (centerTransform == null)
            return;

        Gizmos.color = new Color(1f, 0.25f, 0.1f, 1f);
        Gizmos.DrawWireSphere(centerTransform.position, forbiddenRadius);

        Gizmos.color = new Color(1f, 0.75f, 0.1f, 0.75f);
        Gizmos.DrawWireSphere(centerTransform.position, forbiddenRadius + Mathf.Max(0f, releaseExtraRadius));
    }
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/AimCursorSystem.cs`

```cs
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class AimCursorSystem : MonoBehaviour, ICharacterSubsystem,ICharacterTickable
{
    [Header("׼����ʾ����")]
    [SerializeField] private Transform cursorVisual;
    [SerializeField] private SpriteRenderer cursorSprite;

    [Header("׼�Ļ�������")]
    public float defaultDistance = 3f;

    [Header("׼���ƶ�����")]
    public float followSmoothTime = 0.06f;
    public float gamepadCursorSpeed = 8f;

    [Header("��������")]
    public float mousePixelDeadZone = 0.5f;
    public float gamepadDeadZone = 0.15f;

    private CharacterRoot characterRoot;
    private CharacterDataBus dataBus;
    private InputHandler inputHandler;
    private Camera mainCamera;

    private Vector2 lastMouseScreenPosition;
    private bool hasMouseInitialized;

    private float horizontalFollowOffset;
    private Vector3 targetWorldPosition;
    private Vector3 smoothVelocity;
    private bool _isInitialized;

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized)
            return;

        characterRoot = root;
        dataBus = root.characterDataBus;
        inputHandler = root.GetComponentInChildren<InputHandler>();
        mainCamera = Camera.main;

        if (inputHandler == null)
        {
            Debug.LogError($"[{name}] AimCursorSystem �Ҳ��� InputHandler");
            return;
        }

        if (cursorVisual == null)
        {
            Debug.LogError($"[{name}] AimCursorSystem û��ָ�� Cursor Visual����� Cursor2 �ϵ� cursorVisual �ϡ�");
            return;
        }

        if (cursorSprite == null)
        {
            cursorSprite = cursorVisual.GetComponent<SpriteRenderer>();
        }

        inputHandler.mouseAction?.Enable();
        inputHandler.gamePad_MouseAction?.Enable();

        Vector3 characterPosition = GetCharacterPosition();
        Vector3 initDirection = GetInitialDirection();

        horizontalFollowOffset = initDirection.x * defaultDistance;
        targetWorldPosition = new Vector3(
            characterPosition.x + horizontalFollowOffset,
            characterPosition.y,
            characterPosition.z
        );

        cursorVisual.position = targetWorldPosition;

        if (inputHandler.mouseAction != null)
        {
            lastMouseScreenPosition = inputHandler.mouseAction.ReadValue<Vector2>();
            hasMouseInitialized = true;
        }

        // ���Ĭ��ʼ�տɼ�
        SetCursorVisible(true);

        dataBus.mouseMoving = false;
        dataBus.mousePositionOnScreen = cursorVisual.position;
        dataBus.aimCursorWorldPosition = cursorVisual.position;
        WriteDataBus();

        _isInitialized = true;
    }
    public void ManagedUpdate(float deltaTime)
    {
        if (characterRoot == null || dataBus == null || inputHandler == null || cursorVisual == null)
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;

        // �������� isAiming�����ʼ�ո���
        SetCursorVisible(true);

        UpdateCursor(deltaTime);
        WriteDataBus();
    }
 
    private void UpdateCursor(float deltaTime)
    {
        Vector3 characterPosition = GetCharacterPosition();

        Vector2 mouseScreenPosition = ReadMouseScreenPosition();
        Vector2 gamepadInput = ReadGamepadInput();

        bool gamepadActive = gamepadInput.magnitude > gamepadDeadZone;
        bool mouseActive = IsMouseMoved(mouseScreenPosition);

        if (gamepadActive)
        {
            HandleGamepadCursor(characterPosition, gamepadInput, deltaTime);
        }
        else if (mouseActive)
        {
            HandleMouseCursor(characterPosition, mouseScreenPosition);
        }
        else
        {
            HandleFollowCharacter(characterPosition);
        }

        cursorVisual.position = Vector3.SmoothDamp(
            cursorVisual.position,
            targetWorldPosition,
            ref smoothVelocity,
            followSmoothTime
        );

        lastMouseScreenPosition = mouseScreenPosition;
        dataBus.mouseMoving = gamepadActive || mouseActive;
    }

    private void HandleGamepadCursor(Vector3 characterPosition, Vector2 gamepadInput, float deltaTime)
    {
        dataBus.currentInputDevice = CharacterDataBus.InputDevice_AimType.Gamepad;

        Vector3 move = new Vector3(gamepadInput.x, gamepadInput.y, 0f);
        targetWorldPosition = cursorVisual.position + move * gamepadCursorSpeed * deltaTime;

        horizontalFollowOffset = targetWorldPosition.x - characterPosition.x;
    }

    private void HandleMouseCursor(Vector3 characterPosition, Vector2 mouseScreenPosition)
    {
        dataBus.currentInputDevice = CharacterDataBus.InputDevice_AimType.Mouse;

        targetWorldPosition = ScreenToWorldOnCharacterPlane(mouseScreenPosition, characterPosition);
        horizontalFollowOffset = targetWorldPosition.x - characterPosition.x;
    }

    private void HandleFollowCharacter(Vector3 characterPosition)
    {
        targetWorldPosition = new Vector3(
            characterPosition.x + horizontalFollowOffset,
            targetWorldPosition.y,
            targetWorldPosition.z
        );
    }

    private Vector2 ReadMouseScreenPosition()
    {
        if (inputHandler == null || inputHandler.mouseAction == null)
            return lastMouseScreenPosition;

        return inputHandler.mouseAction.ReadValue<Vector2>();
    }

    private Vector2 ReadGamepadInput()
    {
        if (inputHandler == null || inputHandler.gamePad_MouseAction == null)
            return Vector2.zero;

        return inputHandler.gamePad_MouseAction.ReadValue<Vector2>();
    }

    private bool IsMouseMoved(Vector2 currentMouseScreenPosition)
    {
        if (!hasMouseInitialized)
        {
            lastMouseScreenPosition = currentMouseScreenPosition;
            hasMouseInitialized = true;
            return false;
        }

        float delta = Vector2.Distance(currentMouseScreenPosition, lastMouseScreenPosition);
        return delta > mousePixelDeadZone;
    }

    private Vector3 ScreenToWorldOnCharacterPlane(Vector2 screenPosition, Vector3 characterPosition)
    {
        if (mainCamera == null)
            return characterPosition;

        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        Plane plane = new Plane(Vector3.forward, characterPosition);

        if (plane.Raycast(ray, out float distance))
        {
            Vector3 world = ray.GetPoint(distance);
            world.z = characterPosition.z;
            return world;
        }

        return characterPosition;
    }

    private Vector3 GetCharacterPosition()
    {
        if (characterRoot != null)
            return characterRoot.transform.position;

        return Vector3.zero;
    }

    private Vector3 GetInitialDirection()
    {
        if (dataBus != null && dataBus.facingDirection.sqrMagnitude > 0.001f)
        {
            Vector3 dir = dataBus.facingDirection;
            dir.z = 0f;
            return dir.normalized;
        }

        return Vector3.right;
    }

    private void WriteDataBus()
    {
        dataBus.mousePositionOnScreen = cursorVisual.position;
        dataBus.aimCursorWorldPosition = cursorVisual.position;

        Vector3 characterPosition = GetCharacterPosition();
        Vector3 aimDir = cursorVisual.position - characterPosition;
        aimDir.z = 0f;

        if (aimDir.sqrMagnitude > 0.001f)
        {
            dataBus.aimDirection = aimDir.normalized;
        }
    }

    private void SetCursorVisible(bool visible)
    {
        if (cursorSprite != null)
            cursorSprite.enabled = visible;
    }
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/AimIKController.cs`

```cs
using Spine;
using Spine.Unity;
using UnityEngine;

public class AimIKController : MonoBehaviour, ICharacterSubsystem,ICharacterLateTickable
{
    [Header("Runtime References")]
    [SerializeField] private CharacterRoot characterRoot;
    [SerializeField] private SkeletonAnimation skeletonAnimation;

    [Tooltip("光标/准星/瞄准 Transform")]
    [SerializeField] private Transform cursorTransform;

    [Tooltip("瞄准原点。强烈建议设为枪口/肩部/枪管根节点")]
    [SerializeField] private Transform aimOriginTransform;

    [Header("Spine Bone")]
    [Tooltip("Spine 中控制IK_Aim 的骨骼名称，需要和 Spine 中完全一致")]
    [SerializeField] private string ikAimBoneName = "IK_Aim_Hound";

    [Header("Safe Aim Distance")]
    [Tooltip("IK_Aim 最小距离。游戏运行时IK_Aim 不会小于此距离")]
    [SerializeField] private float minAimDistance = 2f;

    [Tooltip("IK_Aim 最大距离")]
    [SerializeField] private float maxAimDistance = 8f;

    [Tooltip("最小死区距离，只用于防止光标几乎压住原点时的钉钉")]
    [SerializeField] private float deadZoneDistance = 1f;

    [Header("Invalid Cursor Hold")]
    [Tooltip("当光标靠近角色身边时，不更新 IK_Aim，而是保持上一帧的安全位置")]
    [SerializeField] private bool holdAimWhenCursorInvalid = true;

    [Tooltip("当光标距 AimOrigin 小于该半径时，视为非法输入，IK_Aim 保持")]
    [SerializeField] private float invalidAimRadius = 2f;

    [Tooltip("当角色刚生成或还没有合法 Aim，用默认出场的方向")]
    [SerializeField] private Vector2 initialAimDirection = Vector2.right;

    [Header("Aim Smoothing")]
    [SerializeField] private bool enableAimSmoothing = true;

    [Tooltip("IK_Aim 追逐安全目标的速度")]
    [SerializeField] private float aimFollowSpeed = 25f;

    [Header("Skeleton Space Offset")]
    [Tooltip("Skeleton 坐标下的偏移量，用于微调 IK_Aim 准星的位置")]
    [SerializeField] private Vector2 skeletonSpaceOffset;

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmos = true;
    [SerializeField] private bool logRefresh = false;

    [Header("IK Safety Clamp")]
    [SerializeField] private AimIKSafetyConfig ikSafetyConfig = AimIKSafetyConfig.Default;
    private AimIKClampResult lastClampResult;

    private Skeleton cachedSkeleton;
    private Bone ikAimBone;

    private Vector2 lastValidAimDirection;
    private Vector2 currentSafeAimWorld;
    private bool hasCurrentSafeAim;
    private bool _isInitialized;
    private float _lastRawDist = -999f;
    private float _lastDirX = 0f;

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized)
            return;
        characterRoot = root;
        ResolveHunterGunSkeletonAnimation();
        NormalizeInitialDirection();
        RefreshSpineReferences("Initialize");
        _isInitialized = true;
    }

    private void Awake()
    {
        if (characterRoot == null)
            characterRoot = GetComponentInParent<CharacterRoot>();
        ResolveHunterGunSkeletonAnimation();
        NormalizeInitialDirection();
        RefreshSpineReferences("Awake");
    }

    private void Start()
    {
        RefreshSpineReferences("Start");
    }

    public void ManagedLateUpdate(float deltaTime)
    {
        ApplyAim();
    }

    private void ResolveHunterGunSkeletonAnimation()
    {
        if (characterRoot == null)
            characterRoot = GetComponentInParent<CharacterRoot>();
        if (characterRoot != null && characterRoot.houndSkeletonAnimation != null)
        {
            skeletonAnimation = characterRoot.houndSkeletonAnimation;
            return;
        }
        if (skeletonAnimation == null)
        {
            Debug.LogWarning("[AimIKController] aim skeleton is null. Please assign Hound SkeletonAnimation.", this);
        }
    }

    public void SetCursorTransform(Transform cursor)
    {
        cursorTransform = cursor;
    }

    public void SetAimOriginTransform(Transform origin)
    {
        aimOriginTransform = origin;
    }

    public void ForceRefresh()
    {
        RefreshSpineReferences("ForceRefresh");
    }

    private void ApplyAim()
    {
        if (skeletonAnimation == null)
            return;

        Skeleton currentSkeleton = skeletonAnimation.Skeleton;
        if (currentSkeleton == null)
            return;

        if (cachedSkeleton != currentSkeleton)
            RefreshSpineReferences("Skeleton changed");

        if (ikAimBone == null)
            return;

        if (cursorTransform == null)
            return;

        Vector2 originWorld = GetAimOriginWorldPosition();
        Vector2 rawCursorWorld = cursorTransform.position;

        Vector2 targetSafeAimWorld = CalculateSafeAimWorldPosition(rawCursorWorld, originWorld);

        if (!hasCurrentSafeAim)
        {
            currentSafeAimWorld = targetSafeAimWorld;
            hasCurrentSafeAim = true;
        }
        else
        {
            if (enableAimSmoothing)
            {
                float t = 1f - Mathf.Exp(-aimFollowSpeed * Time.deltaTime);
                currentSafeAimWorld = Vector2.Lerp(currentSafeAimWorld, targetSafeAimWorld, t);
            }
            else
            {
                currentSafeAimWorld = targetSafeAimWorld;
            }
        }

        // 朝向符号
        int facingSign = characterRoot != null && characterRoot.characterDataBus != null
            ? (characterRoot.characterDataBus.facingDirection.x >= 0f ? 1 : -1)
            : 1;

        // 世界空间 IK 目标 X 半平面限制：
        // 确保 IK 目标不越过 SkeletonAnimation transform 的世界 X（角色 Spine root 位置）
        // 朝右时 IK 目标 X 必须 >= skeletonTransform.x + epsilon
        // 朝左时 IK 目标 X 必须 <= skeletonTransform.x - epsilon
        float boundaryEpsilon = 0.05f;
        Vector2 finalAimWorld = currentSafeAimWorld;
        float skeletonX = skeletonAnimation.transform.position.x;

        if (facingSign > 0)
        {
            // 朝右：IK 目标不应在角色左边（背后）
            float minX = skeletonX + boundaryEpsilon;
            if (finalAimWorld.x < minX)
            {
                finalAimWorld.x = minX;
            }
        }
        else
        {
            // 朝左：IK 目标不应在角色右边（背后）
            float maxX = skeletonX - boundaryEpsilon;
            if (finalAimWorld.x > maxX)
            {
                finalAimWorld.x = maxX;
            }
        }

        // 用钳制后的世界坐标转换到 skeleton space
        Vector3 skeletonLocal3 = skeletonAnimation.transform.InverseTransformPoint(finalAimWorld);
        Vector2 targetSkeletonSpace = new Vector2(skeletonLocal3.x, skeletonLocal3.y);
        targetSkeletonSpace += skeletonSpaceOffset;

        // IK 安全钳制
        lastClampResult = AimIKTargetSafetyClamp.Clamp(
            currentSafeAimWorld,
            originWorld,
            facingSign,
            ikSafetyConfig);

        if (lastClampResult.wasClamped)
        {
            Vector3 clampedLocal = skeletonAnimation.transform.InverseTransformPoint(lastClampResult.clampedWorldTarget);
            targetSkeletonSpace = new Vector2(clampedLocal.x, clampedLocal.y) + skeletonSpaceOffset;
        }

        
        // 事件式诊断：只在关键值变化时输出
        {
            float rawDist = (rawCursorWorld - originWorld).magnitude;
            bool distChanged = Mathf.Abs(rawDist - _lastRawDist) > 0.5f;
            bool jumpActive = characterRoot != null && characterRoot.characterDataBus != null
                && characterRoot.characterDataBus.currentJumpState != EState.Jump_None;
            bool dirFlipped = Mathf.Sign(lastValidAimDirection.x) != Mathf.Sign(_lastDirX);

            if ((jumpActive && (distChanged || dirFlipped)) || dirFlipped)
            {
                int fs = characterRoot != null && characterRoot.characterDataBus != null
                    ? (characterRoot.characterDataBus.facingDirection.x >= 0f ? 1 : -1)
                    : 0;
                //Debug.Log($"[IKDiag] jump={characterRoot?.characterDataBus?.currentJumpState} facing={fs} rawDist={rawDist:F2} deadZone={deadZoneDistance} minAim={minAimDistance} lastDir={lastValidAimDirection} target={targetSafeAimWorld} smooth={currentSafeAimWorld} skelSpace={targetSkeletonSpace}");
                _lastRawDist = rawDist;
                _lastDirX = lastValidAimDirection.x;
            }
        }

        ikAimBone.SetPositionSkeletonSpace(targetSkeletonSpace);
        currentSkeleton.UpdateWorldTransform(Spine.Physics.Pose);
    }

    private Vector2 CalculateSafeAimWorldPosition(Vector2 rawCursorWorld, Vector2 originWorld)
    {
        Vector2 delta = rawCursorWorld - originWorld;
        float rawDistance = delta.magnitude;

        // 当光标靠近角色身边时，保持上一帧的安全位置
        if (holdAimWhenCursorInvalid && rawDistance < invalidAimRadius)
        {
            return GetHoldAimWorld(originWorld);
        }

        Vector2 direction;

        if (rawDistance < deadZoneDistance)
        {
            direction = lastValidAimDirection;
        }
        else
        {
            direction = delta / rawDistance;
        }

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = GetInitialAimDirection();
        }

        direction.Normalize();
        lastValidAimDirection = direction;

        float safeDistance = Mathf.Clamp(rawDistance, minAimDistance, maxAimDistance);
        return originWorld + direction * safeDistance;
    }

    private Vector2 GetHoldAimWorld(Vector2 originWorld)
    {
        if (hasCurrentSafeAim)
        {
            return currentSafeAimWorld;
        }

        Vector2 dir = lastValidAimDirection;
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = GetInitialAimDirection();
        }
        dir.Normalize();
        return originWorld + dir * minAimDistance;
    }

    private void RefreshSpineReferences(string reason)
    {
        ResolveHunterGunSkeletonAnimation();

        if (skeletonAnimation == null)
        {
            cachedSkeleton = null;
            ikAimBone = null;
            return;
        }

        cachedSkeleton = skeletonAnimation.Skeleton;
        if (cachedSkeleton == null)
        {
            ikAimBone = null;
            if (logRefresh)
            {
                Debug.LogWarning($"[AimIKController] Refresh failed. Skeleton is null. Reason={reason}", this);
            }
            return;
        }

        ikAimBone = cachedSkeleton.FindBone(ikAimBoneName);

        if (lastValidAimDirection.sqrMagnitude < 0.0001f)
        {
            lastValidAimDirection = GetInitialAimDirection();
        }

        hasCurrentSafeAim = false;

        if (logRefresh)
        {
            Debug.Log(
                $"[AimIKController] RefreshSpineReferences: {reason}, " +
                $"Skeleton={(skeletonAnimation != null ? skeletonAnimation.gameObject.name : "NULL")}, " +
                $"SkeletonHash={cachedSkeleton.GetHashCode()}, " +
                $"Bone={(ikAimBone != null ? ikAimBone.Data.Name : "NULL")}",
                this);
        }

        if (ikAimBone == null)
        {
            Debug.LogWarning($"[AimIKController] 找不到 HunterGun Spine Bone: {ikAimBoneName}，当前 Skeleton={skeletonAnimation.gameObject.name}", this);
        }
    }

    private Vector2 GetAimOriginWorldPosition()
    {
        if (aimOriginTransform != null)
            return aimOriginTransform.position;
        if (characterRoot != null)
            return characterRoot.transform.position;
        return transform.position;
    }

    private void NormalizeInitialDirection()
    {
        if (initialAimDirection.sqrMagnitude < 0.0001f)
            initialAimDirection = Vector2.right;
        initialAimDirection.Normalize();
        if (lastValidAimDirection.sqrMagnitude < 0.0001f)
            lastValidAimDirection = initialAimDirection;
    }

    private Vector2 GetInitialAimDirection()
    {
        if (initialAimDirection.sqrMagnitude < 0.0001f)
            return Vector2.right;
        return initialAimDirection.normalized;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
            return;

        Vector2 origin;
        if (Application.isPlaying)
            origin = GetAimOriginWorldPosition();
        else
            origin = transform.position;

        Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
        Gizmos.DrawWireSphere(origin, invalidAimRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, minAimDistance);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin, maxAimDistance);

        if (Application.isPlaying && hasCurrentSafeAim)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(currentSafeAimWorld, 0.08f);
            Gizmos.DrawLine(origin, currentSafeAimWorld);
        }

        if (cursorTransform != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(cursorTransform.position, 0.06f);
        }
    }
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/FirearmActionProfileSO.cs`

```cs
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "FirearmActionProfile", menuName = "ScriptableObject/武器/枪械行为配置")]
public class FirearmActionProfileSO : WeaponActionProfileSO
{
    public override WeaponType WeaponType => WeaponType.Firearm;

    [Header("枪械分类")]
    public FirearmCategory firearmCategory = FirearmCategory.Rifle;

    [Header("默认射击模式")]
    public GunFireModeType defaultFireMode = GunFireModeType.SemiAuto;

    [Header("可用射击模式")]
    public List<GunFireModeConfig> fireModes = new List<GunFireModeConfig>();

    [Header("配套近身攻击")]
    public MeleeComboActionSO auxiliaryMeleeAction;



    [Header("命中方式")]
    public GunHitType hitType = GunHitType.Hitscan;

    [Header("动画")]
    [Min(0.01f)]
    public float fireAnimationDuration = 0.12f;

    [Min(0.01f)]
    public float reloadAnimationFallbackDuration = 0.6f;

    public GunFireModeConfig GetDefaultFireModeConfig()
    {
        return GetFireModeConfig(defaultFireMode);
    }

    public GunFireModeConfig GetFireModeConfig(GunFireModeType modeType)
    {
        if (fireModes == null)
            return null;

        for (int i = 0; i < fireModes.Count; i++)
        {
            GunFireModeConfig config = fireModes[i];

            if (config == null)
                continue;

            if (config.fireModeType == modeType)
                return config;
        }

        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (fireModes == null)
            fireModes = new List<GunFireModeConfig>();

        for (int i = fireModes.Count - 1; i >= 0; i--)
        {
            if (fireModes[i] == null)
                fireModes.RemoveAt(i);
        }

        if (fireModes.Count == 0)
        {
            fireModes.Add(new GunFireModeConfig
            {
                fireModeType = defaultFireMode
            });
        }

        if (GetFireModeConfig(defaultFireMode) == null && fireModes.Count > 0)
            defaultFireMode = fireModes[0].fireModeType;

        //UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/FirearmActionProfileSOEditor.cs`

```cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FirearmActionProfileSO))]
public class FirearmActionProfileSOEditor : Editor
{
    private SerializedProperty firearmCategoryProp;
    private SerializedProperty defaultFireModeProp;
    private SerializedProperty fireModesProp;
    private SerializedProperty hitTypeProp;
    private SerializedProperty fireAnimationDurationProp;
    private SerializedProperty reloadAnimationFallbackDurationProp;
    private SerializedProperty auxiliaryMeleeActionProp;

    private void OnEnable()
    {
        firearmCategoryProp = serializedObject.FindProperty("firearmCategory");
        defaultFireModeProp = serializedObject.FindProperty("defaultFireMode");
        fireModesProp = serializedObject.FindProperty("fireModes");
        hitTypeProp = serializedObject.FindProperty("hitType");
        fireAnimationDurationProp = serializedObject.FindProperty("fireAnimationDuration");
        reloadAnimationFallbackDurationProp = serializedObject.FindProperty("reloadAnimationFallbackDuration");
        auxiliaryMeleeActionProp = serializedObject.FindProperty("auxiliaryMeleeAction");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("ǹе��Ϊ����", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(firearmCategoryProp, new GUIContent("ǹе����"));
        EditorGUILayout.PropertyField(defaultFireModeProp, new GUIContent("Ĭ�����ģʽ"));

        EditorGUILayout.Space(8);
        DrawFireModes();

        EditorGUILayout.Space(8);
        EditorGUILayout.PropertyField(hitTypeProp, new GUIContent("���з�ʽ"));

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("����", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(fireAnimationDurationProp, new GUIContent("���𶯻�ʱ��"));
        EditorGUILayout.PropertyField(reloadAnimationFallbackDurationProp, new GUIContent("������������ʱ��"));

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("配套近身攻击", EditorStyles.boldLabel );
        EditorGUILayout.PropertyField(auxiliaryMeleeActionProp,new GUIContent("近身攻击动作"));

        serializedObject.ApplyModifiedProperties();
    }
    private void DrawProperty(
    SerializedProperty property,
    string label,
    string fieldName)
    {
        if (property == null)
        {
            EditorGUILayout.HelpBox(
                $"�Ҳ������л��ֶΣ�{fieldName}",
                MessageType.Error
            );
            return;
        }

        EditorGUILayout.PropertyField(
            property,
            new GUIContent(label)
        );
    }
    private void DrawFireModes()
    {
        EditorGUILayout.LabelField("�������ģʽ", EditorStyles.boldLabel);

        if (fireModesProp == null)
        {
            EditorGUILayout.HelpBox("�Ҳ��� fireModes �ֶΡ�", MessageType.Error);
            return;
        }

        for (int i = 0; i < fireModesProp.arraySize; i++)
        {
            SerializedProperty element = fireModesProp.GetArrayElementAtIndex(i);

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();

            SerializedProperty modeProp = element.FindPropertyRelative("fireModeType");
            GunFireModeType mode = modeProp != null
                ? (GunFireModeType)modeProp.enumValueIndex
                : GunFireModeType.SemiAuto;

            EditorGUILayout.LabelField($"ģʽ {i + 1}: {mode}", EditorStyles.boldLabel);

            if (GUILayout.Button("ɾ��", GUILayout.Width(50)))
            {
                fireModesProp.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.EndHorizontal();

            DrawFireModeConfig(element);

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("�������ģʽ"))
        {
            fireModesProp.arraySize++;

            SerializedProperty newElement = fireModesProp.GetArrayElementAtIndex(fireModesProp.arraySize - 1);
            SerializedProperty modeProp = newElement.FindPropertyRelative("fireModeType");

            if (modeProp != null)
                modeProp.enumValueIndex = (int)GunFireModeType.SemiAuto;
        }
    }

    private void DrawFireModeConfig(SerializedProperty configProp)
    {
        SerializedProperty modeProp = configProp.FindPropertyRelative("fireModeType");

        if (modeProp == null)
        {
            EditorGUILayout.HelpBox("�Ҳ��� fireModeType �ֶΡ�", MessageType.Error);
            return;
        }

        EditorGUILayout.PropertyField(modeProp, new GUIContent("���ģʽ"));

        GunFireModeType mode = (GunFireModeType)modeProp.enumValueIndex;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("ͨ��", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(
            configProp.FindPropertyRelative("fireInterval"),
            new GUIContent("������")
        );

        switch (mode)
        {
            case GunFireModeType.SemiAuto:
                DrawSemiAutoConfig(configProp);
                break;

            case GunFireModeType.Burst:
                DrawBurstConfig(configProp);
                break;

            case GunFireModeType.FullAuto:
                DrawFullAutoConfig(configProp);
                break;

            case GunFireModeType.Charge:
                DrawChargeConfig(configProp);
                break;

            case GunFireModeType.Beam:
                DrawBeamConfig(configProp);
                break;

            case GunFireModeType.Special:
                DrawSpecialConfig(configProp);
                break;
        }
    }

    private void DrawSemiAutoConfig(SerializedProperty configProp)
    {
    }

    private void DrawBurstConfig(SerializedProperty configProp)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Burst ��������", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(
            configProp.FindPropertyRelative("burstCount"),
            new GUIContent("��������")
        );

        EditorGUILayout.PropertyField(
            configProp.FindPropertyRelative("burstShotInterval"),
            new GUIContent("�����������")
        );

        EditorGUILayout.PropertyField(
            configProp.FindPropertyRelative("burstCycleInterval"),
            new GUIContent("����ѭ�����")
        );

        EditorGUILayout.PropertyField(
            configProp.FindPropertyRelative("allowHoldToFire"),
            new GUIContent("������ס��������")
        );
    }

    private void DrawFullAutoConfig(SerializedProperty configProp)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Full Auto �Զ��������", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(
            configProp.FindPropertyRelative("allowHoldToFire"),
            new GUIContent("������ס���")
        );
    }

    private void DrawChargeConfig(SerializedProperty configProp)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Charge ��������", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(
            configProp.FindPropertyRelative("chargeTime"),
            new GUIContent("����ʱ��")
        );
    }

    private void DrawBeamConfig(SerializedProperty configProp)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Beam ��������", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(
            configProp.FindPropertyRelative("chargeTime"),
            new GUIContent("���� / ����ʱ��")
        );

        EditorGUILayout.PropertyField(
            configProp.FindPropertyRelative("allowHoldToFire"),
            new GUIContent("������ס��������")
        );
    }

    private void DrawSpecialConfig(SerializedProperty configProp)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox("Special ģʽ��ʱû��ר�����á�", MessageType.Info);
    }
}
#endif
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunBonePositionFollower.cs`

```cs
using Spine;
using Spine.Unity;
using UnityEngine;

[DefaultExecutionOrder(10000)]
public class GunBoneMountFollower : MonoBehaviour
{
    [Header("Read: Spine Bone")]
    [SerializeField] private SkeletonAnimation skeletonAnimation;
    [SerializeField] private string boneName = "LongRangeWeapon";

    [Header("Read: Character Flip Source")]
    [Tooltip("只读。拖 VisualFlipRoot_Hound。脚本不会修改它。")]
    [SerializeField] private Transform visualFlipRoot;

    [Header("Read: Spine Render Sorting")]
    [Tooltip("角色上的 SkeletonRenderSeparator。可以直接拖入。")]
    [SerializeField] private SkeletonRenderSeparator renderSeparator;

    [Tooltip("必须是 SkeletonRenderer.separatorSlots 中配置的 Slot 名称。")]
    [SerializeField] private string separatorSlotName = "LongRangeWeapon";

    [Header("Debug")]
    [SerializeField] private bool debugDraw;
    [SerializeField] private bool debugLog;

    private Spine.Bone targetBone;

    private WeaponRoot currentWeapon;
    private Transform weaponVirtualRoot;
    private Renderer[] weaponRenderers;

    private Vector3 virtualRootBaseScale = Vector3.one;
    private bool hasVirtualRootBaseScale;

    private WeaponDefaultFacing weaponDefaultFacing =
        WeaponDefaultFacing.Right;

    private int lastSeparatorRank = -1;
    private int lastWeaponSortingOrder = int.MinValue;

    private bool sortingErrorLogged;

    public void Bind(
        SkeletonAnimation targetSkeleton,
        string targetBoneName)
    {
        skeletonAnimation = targetSkeleton;

        if (!string.IsNullOrEmpty(targetBoneName))
        {
            boneName = targetBoneName;
            separatorSlotName = targetBoneName;
        }

        targetBone = null;

        if (skeletonAnimation != null)
        {
            if (renderSeparator == null)
            {
                renderSeparator =
                    skeletonAnimation.GetComponent<SkeletonRenderSeparator>();
            }

            if (renderSeparator == null)
            {
                renderSeparator =
                    skeletonAnimation.GetComponentInChildren
                    <SkeletonRenderSeparator>(true);
            }
        }

        ResetSortingCache();
        CacheBone();
    }

    public void SetVisualFlipRoot(
        Transform targetVisualFlipRoot)
    {
        visualFlipRoot = targetVisualFlipRoot;
    }

    public void SetRenderSeparator(
        SkeletonRenderSeparator targetRenderSeparator,
        string targetSeparatorSlotName)
    {
        renderSeparator = targetRenderSeparator;

        if (!string.IsNullOrEmpty(targetSeparatorSlotName))
        {
            separatorSlotName = targetSeparatorSlotName;
        }

        ResetSortingCache();
    }

    public void SetWeapon(WeaponRoot weapon)
    {
        currentWeapon = weapon;

        if (currentWeapon == null)
        {
            weaponVirtualRoot = null;
            weaponRenderers = null;
            hasVirtualRootBaseScale = false;

            ResetSortingCache();
            return;
        }

        weaponVirtualRoot = currentWeapon.VisualRoot;

        weaponRenderers =
            currentWeapon.GetComponentsInChildren<Renderer>(true);

        WeaponConfigSO config = currentWeapon.Config;

        if (config != null)
        {
            weaponDefaultFacing = config.defaultFacing;
        }
        else
        {
            weaponDefaultFacing =
                WeaponDefaultFacing.Right;
        }

        CacheVirtualRootBaseScale();
        ResetSortingCache();

        if (debugLog)
        {
            string virtualRootName =
                weaponVirtualRoot != null
                    ? weaponVirtualRoot.name
                    : "NULL";

            Debug.Log(
                "[GunBoneMountFollower] " +
                $"Bind weapon={currentWeapon.name}, " +
                $"virtualRoot={virtualRootName}, " +
                $"defaultFacing={weaponDefaultFacing}",
                this
            );
        }
    }

    private void Awake()
    {
        CacheBone();

        if (renderSeparator == null &&
            skeletonAnimation != null)
        {
            renderSeparator =
                skeletonAnimation.GetComponent<SkeletonRenderSeparator>();
        }

        if (renderSeparator == null &&
            skeletonAnimation != null)
        {
            renderSeparator =
                skeletonAnimation.GetComponentInChildren
                <SkeletonRenderSeparator>(true);
        }
    }

    private void LateUpdate()
    {
        RefreshNow();
    }

    public void RefreshNow()
    {
        if (skeletonAnimation == null)
            return;

        if (skeletonAnimation.Skeleton == null)
            return;

        CacheBone();

        if (targetBone == null)
            return;

        ApplyBoneTransform();
        ApplyWeaponMirror();
        ApplyWeaponSorting();

        if (debugDraw)
            DrawDebug();
    }

    private void CacheBone()
    {
        if (targetBone != null)
            return;

        if (skeletonAnimation == null)
            return;

        if (skeletonAnimation.Skeleton == null)
            return;

        if (string.IsNullOrEmpty(boneName))
            return;

        targetBone =
            skeletonAnimation.Skeleton.FindBone(boneName);

        if (debugLog)
        {
            Debug.Log(
                "[GunBoneMountFollower] " +
                $"CacheBone bone={boneName}, " +
                $"found={targetBone != null}",
                this
            );
        }
    }

    private void ApplyBoneTransform()
    {
        Vector3 position = GetBoneWorldPosition();

        position.z = transform.position.z;

        float angle = GetBoneWorldAngle();

        if (weaponDefaultFacing ==
            WeaponDefaultFacing.Left)
        {
            angle += 180f;
        }

        transform.SetPositionAndRotation(
            position,
            Quaternion.Euler(0f, 0f, angle)
        );
    }

    private Vector3 GetBoneWorldPosition()
    {
        Vector2 skeletonPoint = new Vector2(
            targetBone.AppliedPose.WorldX,
            targetBone.AppliedPose.WorldY
        );

        return skeletonAnimation.transform.TransformPoint(
            new Vector3(
                skeletonPoint.x,
                skeletonPoint.y,
                0f
            )
        );
    }

    private float GetBoneWorldAngle()
    {
        Vector2 origin = new Vector2(
            targetBone.AppliedPose.WorldX,
            targetBone.AppliedPose.WorldY
        );

        /*
         * 使用 Spine 骨骼世界矩阵的负 X 轴
         * 作为枪械朝向。
         */
        Vector2 axis = -new Vector2(
            targetBone.AppliedPose.A,
            targetBone.AppliedPose.C
        );

        if (axis.sqrMagnitude <= 0.000001f)
            return transform.eulerAngles.z;

        Vector2 axisPoint =
            origin + axis.normalized;

        Vector3 originWorld =
            skeletonAnimation.transform.TransformPoint(
                new Vector3(
                    origin.x,
                    origin.y,
                    0f
                )
            );

        Vector3 axisWorld =
            skeletonAnimation.transform.TransformPoint(
                new Vector3(
                    axisPoint.x,
                    axisPoint.y,
                    0f
                )
            );

        Vector2 worldDirection =
            axisWorld - originWorld;

        if (worldDirection.sqrMagnitude <= 0.000001f)
            return transform.eulerAngles.z;

        return Mathf.Atan2(
            worldDirection.y,
            worldDirection.x
        ) * Mathf.Rad2Deg;
    }

    private void ApplyWeaponMirror()
    {
        if (visualFlipRoot == null)
            return;

        if (weaponVirtualRoot == null)
            return;

        if (weaponVirtualRoot == transform)
        {
            Debug.LogError(
                "[GunBoneMountFollower] " +
                "weaponVirtualRoot 不能是当前挂载对象。" +
                "必须是武器实例下的 VirtualRoot。",
                this
            );
            return;
        }

        if (weaponVirtualRoot == visualFlipRoot)
        {
            Debug.LogError(
                "[GunBoneMountFollower] " +
                "weaponVirtualRoot 不能是 VisualFlipRoot。" +
                "必须是武器实例下的 VirtualRoot。",
                this
            );
            return;
        }

        if (!hasVirtualRootBaseScale)
            CacheVirtualRootBaseScale();

        /*
         * 镜像规则：
         * Mirror Axis = Y
         * Invert Mirror = false
         */
        float sign =
            Mathf.Sign(visualFlipRoot.lossyScale.x);

        if (Mathf.Approximately(sign, 0f))
            sign = 1f;

        Vector3 scale = virtualRootBaseScale;

        scale.x =
            Mathf.Abs(virtualRootBaseScale.x);

        scale.y =
            Mathf.Abs(virtualRootBaseScale.y) * sign;

        scale.z =
            Mathf.Abs(virtualRootBaseScale.z);

        weaponVirtualRoot.localScale = scale;
    }

    private void ApplyWeaponSorting()
    {
        if (currentWeapon == null)
            return;

        if (skeletonAnimation == null ||
            skeletonAnimation.Skeleton == null)
        {
            return;
        }

        if (renderSeparator == null)
        {
            LogSortingErrorOnce(
                "[GunBoneMountFollower] " +
                "没有配置 SkeletonRenderSeparator。"
            );
            return;
        }

        if (renderSeparator.partsRenderers == null ||
            renderSeparator.partsRenderers.Count < 2)
        {
            LogSortingErrorOnce(
                "[GunBoneMountFollower] " +
                "SkeletonRenderSeparator 至少需要两个 PartsRenderer。"
            );
            return;
        }

        SkeletonRenderer skeletonRenderer =
            skeletonAnimation.GetComponent<SkeletonRenderer>();

        if (skeletonRenderer == null)
        {
            LogSortingErrorOnce(
                "[GunBoneMountFollower] " +
                "SkeletonAnimation 所在对象上找不到 SkeletonRenderer。"
            );
            return;
        }

        /*
         * 这里使用 var，避免不同 Spine 版本中
         * separatorSlots 的具体集合类型不一致。
         */
        var separatorSlots =
            skeletonRenderer.separatorSlots;

        if (separatorSlots == null ||
            separatorSlots.Count == 0)
        {
            LogSortingErrorOnce(
                "[GunBoneMountFollower] " +
                "SkeletonRenderer.separatorSlots 为空。"
            );
            return;
        }

        Spine.Slot targetSeparatorSlot =
            skeletonAnimation.Skeleton.FindSlot(
                separatorSlotName
            );

        if (targetSeparatorSlot == null)
        {
            LogSortingErrorOnce(
                "[GunBoneMountFollower] 找不到 Separator Slot："
                + $"[{separatorSlotName}]。"
            );
            return;
        }

        if (!separatorSlots.Contains(targetSeparatorSlot))
        {
            LogSortingErrorOnce(
                "[GunBoneMountFollower] Slot "
                + $"[{separatorSlotName}] 没有配置到 "
                + "SkeletonRenderer.separatorSlots。"
            );
            return;
        }

        int separatorRank =
            GetCurrentSeparatorRank(targetSeparatorSlot);

        if (separatorRank < 0)
            return;

        int lowerPartIndex =
            separatorRank;

        int upperPartIndex =
            separatorRank + 1;

        if (upperPartIndex >=
            renderSeparator.partsRenderers.Count)
        {
            LogSortingErrorOnce(
                "[GunBoneMountFollower] PartsRenderer 数量不足。"
                + $" SeparatorRank={separatorRank},"
                + $" PartsCount={renderSeparator.partsRenderers.Count}"
            );
            return;
        }

        SkeletonPartsRenderer lowerPart =
            renderSeparator.partsRenderers[lowerPartIndex];

        SkeletonPartsRenderer upperPart =
            renderSeparator.partsRenderers[upperPartIndex];

        if (lowerPart == null ||
            upperPart == null)
        {
            return;
        }

        MeshRenderer lowerRenderer =
            lowerPart.MeshRenderer;

        MeshRenderer upperRenderer =
            upperPart.MeshRenderer;

        if (lowerRenderer == null ||
            upperRenderer == null)
        {
            return;
        }

        if (lowerRenderer.sortingLayerID !=
            upperRenderer.sortingLayerID)
        {
            LogSortingErrorOnce(
                "[GunBoneMountFollower] " +
                "Separator 两侧 PartsRenderer 不在同一个 Sorting Layer。"
            );
            return;
        }

        int lowerOrder =
            lowerRenderer.sortingOrder;

        int upperOrder =
            upperRenderer.sortingOrder;

        int minOrder =
            Mathf.Min(lowerOrder, upperOrder);

        int maxOrder =
            Mathf.Max(lowerOrder, upperOrder);

        if (maxOrder - minOrder < 2)
        {
            LogSortingErrorOnce(
                "[GunBoneMountFollower] 两侧 PartsRenderer "
                + $"排序间隔不足：{lowerOrder} / {upperOrder}"
            );
            return;
        }

        int weaponOrder =
            minOrder + ((maxOrder - minOrder) / 2);

        int sortingLayerID =
            lowerRenderer.sortingLayerID;

        if (weaponRenderers == null ||
            weaponRenderers.Length == 0)
        {
            weaponRenderers =
                currentWeapon.GetComponentsInChildren<Renderer>(true);
        }

        if (weaponRenderers == null ||
            weaponRenderers.Length == 0)
        {
            LogSortingErrorOnce(
                "[GunBoneMountFollower] 当前武器没有 Renderer。"
            );
            return;
        }

        bool needApply =
            separatorRank != lastSeparatorRank ||
            weaponOrder != lastWeaponSortingOrder ||
            !AreWeaponRenderersCorrect(
                sortingLayerID,
                weaponOrder
            );

        if (!needApply)
            return;

        for (int i = 0;
             i < weaponRenderers.Length;
             i++)
        {
            Renderer weaponRenderer =
                weaponRenderers[i];

            if (weaponRenderer == null)
                continue;

            weaponRenderer.sortingLayerID =
                sortingLayerID;

            weaponRenderer.sortingOrder =
                weaponOrder;
        }

        if (debugLog &&
            separatorRank != lastSeparatorRank)
        {
            Debug.Log(
                "[GunBoneMountFollower] "
                + $"SeparatorSlot={separatorSlotName}, "
                + $"Rank={separatorRank}, "
                + $"Parts={lowerPartIndex}->{upperPartIndex}, "
                + $"Sorting={lowerOrder}->{weaponOrder}->{upperOrder}",
                this
            );
        }

        lastSeparatorRank = separatorRank;
        lastWeaponSortingOrder = weaponOrder;
        sortingErrorLogged = false;
    }

    private int GetCurrentSeparatorRank(
        Spine.Slot targetSeparatorSlot)
    {
        if (skeletonAnimation == null ||
            skeletonAnimation.Skeleton == null)
        {
            return -1;
        }

        SkeletonRenderer skeletonRenderer =
            skeletonAnimation.GetComponent<SkeletonRenderer>();

        if (skeletonRenderer == null)
            return -1;

        var separatorSlots =
            skeletonRenderer.separatorSlots;

        if (separatorSlots == null ||
            separatorSlots.Count == 0)
        {
            return -1;
        }

        /*
         * Spine 4.3：
         *
         * Skeleton.DrawOrder 是 DrawOrder 对象。
         * AppliedPose 才是当前渲染使用的 Slot 顺序。
         */
        var appliedDrawOrder =
            skeletonAnimation.Skeleton
                .DrawOrder
                .AppliedPose;

        if (appliedDrawOrder == null)
            return -1;

        int separatorRank = 0;

        for (int i = 0;
             i < appliedDrawOrder.Count;
             i++)
        {
            Spine.Slot currentSlot =
                appliedDrawOrder.Items[i];

            if (currentSlot == null)
                continue;

            if (!separatorSlots.Contains(currentSlot))
                continue;

            if (currentSlot == targetSeparatorSlot)
                return separatorRank;

            separatorRank++;
        }

        return -1;
    }

    private bool AreWeaponRenderersCorrect(
        int sortingLayerID,
        int sortingOrder)
    {
        if (weaponRenderers == null ||
            weaponRenderers.Length == 0)
        {
            return false;
        }

        for (int i = 0;
             i < weaponRenderers.Length;
             i++)
        {
            Renderer weaponRenderer =
                weaponRenderers[i];

            if (weaponRenderer == null)
                continue;

            if (weaponRenderer.sortingLayerID !=
                    sortingLayerID ||
                weaponRenderer.sortingOrder !=
                    sortingOrder)
            {
                return false;
            }
        }

        return true;
    }

    private void CacheVirtualRootBaseScale()
    {
        if (weaponVirtualRoot == null)
            return;

        virtualRootBaseScale =
            weaponVirtualRoot.localScale;

        virtualRootBaseScale.x =
            Mathf.Abs(virtualRootBaseScale.x);

        virtualRootBaseScale.y =
            Mathf.Abs(virtualRootBaseScale.y);

        virtualRootBaseScale.z =
            Mathf.Abs(virtualRootBaseScale.z);

        hasVirtualRootBaseScale = true;
    }

    private void ResetSortingCache()
    {
        lastSeparatorRank = -1;
        lastWeaponSortingOrder = int.MinValue;
        sortingErrorLogged = false;
    }

    private void LogSortingErrorOnce(string message)
    {
        if (sortingErrorLogged)
            return;

        sortingErrorLogged = true;
        Debug.LogError(message, this);
    }

    private void DrawDebug()
    {
        Vector3 origin =
            GetBoneWorldPosition();

        float angle =
            GetBoneWorldAngle();

        if (weaponDefaultFacing ==
            WeaponDefaultFacing.Left)
        {
            angle += 180f;
        }

        Vector3 direction = new Vector3(
            Mathf.Cos(angle * Mathf.Deg2Rad),
            Mathf.Sin(angle * Mathf.Deg2Rad),
            0f
        );

        Debug.DrawLine(
            origin,
            origin + direction * 0.8f,
            Color.red
        );

        Debug.DrawLine(
            origin + Vector3.left * 0.1f,
            origin + Vector3.right * 0.1f,
            Color.green
        );

        Debug.DrawLine(
            origin + Vector3.down * 0.1f,
            origin + Vector3.up * 0.1f,
            Color.green
        );
    }
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunFireModeConfig.cs`

```cs
using System;
using UnityEngine;

[Serializable]
public class GunFireModeConfig
{
    [Header("射击模式")]
    public GunFireModeType fireModeType = GunFireModeType.SemiAuto;

    [Header("通用")]
    [Tooltip("两次可触发射击行为之间的间隔。SemiAuto / FullAuto 会直接使用它。")]
    [Min(0.01f)]
    public float fireInterval = 0.15f;

    [Header("Burst 连发配置")]
    [Tooltip("Burst 模式下一次输入打几发。三连发填 3。")]
    [Min(1)]
    public int burstCount = 3;

    [Tooltip("Burst 内部每一发之间的间隔。")]
    [Min(0.01f)]
    public float burstShotInterval = 0.08f;

    [Tooltip("一轮 Burst 结束后，下一轮 Burst 允许开始的间隔。")]
    [Min(0.01f)]
    public float burstCycleInterval = 0.35f;

    [Header("自动射击配置")]
    [Tooltip("FullAuto 是否允许按住攻击键持续开火。")]
    public bool allowHoldToFire = true;

    [Header("蓄力 / 特殊预留")]
    [Min(0f)]
    public float chargeTime = 0f;
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunHandIKBinder.cs`

```cs
using Spine;
using Spine.Unity;
using UnityEngine;
//1.Unity Update 阶段
//   SkeletonAnimation 更新动画状态
//   Aim 动画把 IK_Arm_R / IK_Arm_L 等骨骼写入当前动画 Pose
//   Spine 计算出当前帧骨骼世界矩阵
//2. Unity LateUpdate 阶段，GunHandIKBinder 执行，顺序 -1000
//   先刷新武器挂点
//   weaponRoot 拿到武器左右手握点世界坐标
//   把武器握点 Unity World 坐标转换成角色 Skeleton 空间
//   再转换成 IK Target Bone 相对父骨骼的本地坐标
//   写入 IK Target Bone.Pose.X / Pose.Y
//   调用 Skeleton.UpdateWorldTransform(...)
//   Spine IK Constraint 根据新的 IK Target 重新解算手臂
//3. Spine 自己的 LateUpdate / Mesh 生成阶段
//   SkeletonRenderer 使用已经被改过的骨骼结果生成 Mesh
//4. 本帧渲染
//   画面上手臂已经跟随武器握点
//动画先动角色
//↓
//代码覆盖 IK Target
//↓
//Spine 重新解算 IK
//↓
//Spine 用新结果生成 Mesh
//↓
//画面正确
[DefaultExecutionOrder(-1000)]//确保在 SkeletonAnimation LateUpdate 之后执行
public class GunHandIKBinder : MonoBehaviour
{
    // =========================================================
    // Runtime Binding
    // =========================================================

    [Header("Runtime Binding")]
    [SerializeField] private SkeletonAnimation skeletonAnimation;
    [SerializeField] private WeaponRoot weaponRoot;
    [SerializeField] private GunBoneMountFollower mountFollower;

    // =========================================================
    // Character Spine IK Target Bone Names
    // =========================================================

    [Header("角色手部 IK Target Bone Names")]
    [SerializeField] private string rightHandIKTargetBoneName = "IK_Arm_R";
    [SerializeField] private string leftHandIKTargetBoneName = "IK_Arm_L";

    // =========================================================
    // Options
    // =========================================================

    [Header("Options")]
    [SerializeField] private bool bindRightHand = true;
    [SerializeField] private bool bindLeftHand = true;
    [Header("Runtime IK Lock")]
    [SerializeField] private bool runtimeBindRightHand = true;
    [SerializeField] private bool runtimeBindLeftHand = true;

    public bool IsRightHandIKBindingEnabled => runtimeBindRightHand;
    public bool IsLeftHandIKBindingEnabled => runtimeBindLeftHand;
    public void SetRightHandIKBindingEnabled(bool enabled)
    {
        runtimeBindRightHand = enabled;
    }

    public void SetLeftHandIKBindingEnabled(bool enabled)
    {
        runtimeBindLeftHand = enabled;
    }

    public void SetHandIKBindingEnabled(bool rightEnabled, bool leftEnabled)
    {
        runtimeBindRightHand = rightEnabled;
        runtimeBindLeftHand = leftEnabled;
    }


    // =========================================================
    // Debug
    // =========================================================

    [Header("Debug")]
    [SerializeField] private bool debugCacheBones = true;
    [SerializeField] private bool debugIKFollow = true;
    [SerializeField] private bool debugEveryFrame;
    [SerializeField] private float debugLogInterval = 0.5f;
    [SerializeField] private float debugDistanceWarning = 0.05f;

    private float nextDebugLogTime;

    // =========================================================
    // Runtime Cached Character Bones
    // =========================================================

    private Bone rightHandIKTargetBone;
    private Bone leftHandIKTargetBone;

    private bool cached;

    // =========================================================
    // Runtime Debug Data
    // =========================================================

    private bool hasRightTargetThisFrame;
    private bool hasLeftTargetThisFrame;

    private bool rightAssignedThisFrame;
    private bool leftAssignedThisFrame;

    private Vector3 rightGripWorldPosition;
    private Vector3 leftGripWorldPosition;

    private Vector2 rightAssignedSkeletonWorldPosition;
    private Vector2 leftAssignedSkeletonWorldPosition;

    private Vector2 rightAssignedLocalPosition;
    private Vector2 leftAssignedLocalPosition;

    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        if (mountFollower == null)
            mountFollower = GetComponent<GunBoneMountFollower>();
    }

    private void OnEnable()
    {
        CacheSpineBones();
    }

    // =========================================================
    // External Binding
    // =========================================================

    public void Bind(
        SkeletonAnimation skeletonAnimation,
        WeaponRoot weaponRoot,
        GunBoneMountFollower mountFollower = null)
    {
        this.skeletonAnimation = skeletonAnimation;
        this.weaponRoot = weaponRoot;

        if (mountFollower != null)
            this.mountFollower = mountFollower;
        else if (this.mountFollower == null)
            this.mountFollower = GetComponent<GunBoneMountFollower>();

        CacheSpineBones();
    }

    public void SetWeapon(WeaponRoot weapon)
    {
        weaponRoot = weapon;
    }

    public void SetSkeletonAnimation(SkeletonAnimation skeletonAnimation)
    {
        this.skeletonAnimation = skeletonAnimation;
        CacheSpineBones();
    }

    // =========================================================
    // Main Update
    // =========================================================

    private void LateUpdate()
    {
        if (skeletonAnimation == null)
            return;

        if (skeletonAnimation.Skeleton == null)
            return;

        if (weaponRoot == null)
            return;

        if (!cached)
            CacheSpineBones();

        ResetFrameDebugData();

        /*
         * 先刷新武器整体位置。
         * 这一步很重要：
         * 否则 weaponRoot.TryGetXXXGripWorldPosition 拿到的握点位置可能还是上一帧的。
         */
        if (mountFollower != null)
            mountFollower.RefreshNow();

        bool changed = false;

        if (bindRightHand && runtimeBindRightHand)
            changed |= ApplyRightHandIK();

        if (bindLeftHand && runtimeBindLeftHand)
            changed |= ApplyLeftHandIK();

        if (!changed)
        {
            DebugLogFailedToApplyIfNeeded();
            return;
        }

        /*
         * 我们修改了 IK Target Bone 的本地坐标，
         * 必须重新计算角色 Skeleton 世界矩阵。
         */
        skeletonAnimation.Skeleton.UpdateWorldTransform(Spine.Physics.Update);

        /*
         * 调试重点：
         * 在 UpdateWorldTransform 之后检查 IK Target Bone 的最终世界坐标。
         * 如果最终世界坐标接近武器握点，说明程序赋值成功并生效。
         */
        DebugLogIKFollowResultIfNeeded();
    }

    private void ResetFrameDebugData()
    {
        hasRightTargetThisFrame = false;
        hasLeftTargetThisFrame = false;

        rightAssignedThisFrame = false;
        leftAssignedThisFrame = false;

        rightGripWorldPosition = default;
        leftGripWorldPosition = default;

        rightAssignedSkeletonWorldPosition = default;
        leftAssignedSkeletonWorldPosition = default;

        rightAssignedLocalPosition = default;
        leftAssignedLocalPosition = default;
    }

    // =========================================================
    // Cache Character IK Bones
    // =========================================================

    private void CacheSpineBones()
    {
        cached = false;

        rightHandIKTargetBone = null;
        leftHandIKTargetBone = null;

        if (skeletonAnimation == null)
            return;

        if (skeletonAnimation.Skeleton == null)
            return;

        Skeleton skeleton = skeletonAnimation.Skeleton;

        if (!string.IsNullOrEmpty(rightHandIKTargetBoneName))
            rightHandIKTargetBone = skeleton.FindBone(rightHandIKTargetBoneName);

        if (!string.IsNullOrEmpty(leftHandIKTargetBoneName))
            leftHandIKTargetBone = skeleton.FindBone(leftHandIKTargetBoneName);

        cached = true;

        if (debugCacheBones)
        {
            Debug.Log(
                $"[GunHandIKBinder] Cache Character IK Bones on {name}\n" +
                $"SkeletonAnimation: {skeletonAnimation.name}\n" +
                $"Right IK Target [{rightHandIKTargetBoneName}]: {rightHandIKTargetBone != null}\n" +
                $"Left IK Target [{leftHandIKTargetBoneName}]: {leftHandIKTargetBone != null}",
                this
            );
        }
    }

    // =========================================================
    // Apply Right / Left IK
    // =========================================================

    private bool ApplyRightHandIK()
    {
        if (rightHandIKTargetBone == null)
            return false;

        if (weaponRoot == null)
            return false;

        if (!weaponRoot.TryGetRightHandGripWorldPosition(out Vector3 gripWorldPosition))
            return false;

        hasRightTargetThisFrame = true;
        rightGripWorldPosition = gripWorldPosition;

        bool assigned = SetBoneWorldPositionFromUnityWorld(
            rightHandIKTargetBone,
            gripWorldPosition,
            out Vector2 assignedSkeletonWorldPosition,
            out Vector2 assignedLocalPosition
        );

        rightAssignedThisFrame = assigned;
        rightAssignedSkeletonWorldPosition = assignedSkeletonWorldPosition;
        rightAssignedLocalPosition = assignedLocalPosition;

        return assigned;
    }

    private bool ApplyLeftHandIK()
    {
        if (leftHandIKTargetBone == null)
            return false;

        if (weaponRoot == null)
            return false;

        if (!weaponRoot.TryGetLeftHandGripWorldPosition(out Vector3 gripWorldPosition))
            return false;

        hasLeftTargetThisFrame = true;
        leftGripWorldPosition = gripWorldPosition;

        bool assigned = SetBoneWorldPositionFromUnityWorld(
            leftHandIKTargetBone,
            gripWorldPosition,
            out Vector2 assignedSkeletonWorldPosition,
            out Vector2 assignedLocalPosition
        );

        leftAssignedThisFrame = assigned;
        leftAssignedSkeletonWorldPosition = assignedSkeletonWorldPosition;
        leftAssignedLocalPosition = assignedLocalPosition;

        return assigned;
    }

    // =========================================================
    // Set Bone Position
    // =========================================================

    /// <summary>
    /// 把 Unity 世界坐标转换到角色 Skeleton 空间，
    /// 然后设置角色 IK Target Bone 的本地坐标。
    /// </summary>
    private bool SetBoneWorldPositionFromUnityWorld(
        Bone bone,
        Vector3 unityWorldPosition,
        out Vector2 assignedSkeletonWorldPosition,
        out Vector2 assignedLocalPosition)
    {
        assignedSkeletonWorldPosition = default;
        assignedLocalPosition = default;

        if (bone == null)
            return false;

        if (skeletonAnimation == null)
            return false;

        Vector3 skeletonLocalPosition =
            skeletonAnimation.transform.InverseTransformPoint(unityWorldPosition);

        assignedSkeletonWorldPosition = new Vector2(
            skeletonLocalPosition.x,
            skeletonLocalPosition.y
        );

        return SetBoneWorldPositionInSkeletonSpace(
            bone,
            skeletonLocalPosition.x,
            skeletonLocalPosition.y,
            out assignedLocalPosition
        );
    }

    /// <summary>
    /// 设置 Bone 在 Skeleton 空间下的目标世界位置。
    /// 内部会转换成 Bone 相对父骨骼的本地 X/Y。
    /// </summary>
    private bool SetBoneWorldPositionInSkeletonSpace(
        Bone bone,
        float targetWorldX,
        float targetWorldY,
        out Vector2 assignedLocalPosition)
    {
        assignedLocalPosition = default;

        if (bone == null)
            return false;

        Bone parent = bone.Parent;

        if (parent == null)
        {
            bone.Pose.X = targetWorldX;
            bone.Pose.Y = targetWorldY;

            assignedLocalPosition = new Vector2(
                bone.Pose.X,
                bone.Pose.Y
            );

            return true;
        }

        Vector2 localPosition = parent.WorldToLocal(
            new Vector2(targetWorldX, targetWorldY)
        );

        bone.Pose.X = localPosition.x;
        bone.Pose.Y = localPosition.y;

        assignedLocalPosition = localPosition;

        return true;
    }

    // =========================================================
    // Debug Result
    // =========================================================

    private void DebugLogIKFollowResultIfNeeded()
    {
        if (!debugIKFollow)
            return;

        if (!debugEveryFrame)
        {
            if (Time.time < nextDebugLogTime)
                return;

            nextDebugLogTime = Time.time + debugLogInterval;
        }

        string rightLog = BuildHandDebugLog(
            "RightHand",
            rightHandIKTargetBone,
            rightHandIKTargetBoneName,
            hasRightTargetThisFrame,
            rightAssignedThisFrame,
            rightGripWorldPosition,
            rightAssignedSkeletonWorldPosition,
            rightAssignedLocalPosition
        );

        string leftLog = BuildHandDebugLog(
            "LeftHand",
            leftHandIKTargetBone,
            leftHandIKTargetBoneName,
            hasLeftTargetThisFrame,
            leftAssignedThisFrame,
            leftGripWorldPosition,
            leftAssignedSkeletonWorldPosition,
            leftAssignedLocalPosition
        );

        Debug.Log(
            $"[GunHandIKBinder] IK Follow Debug on {name}\n" +
            $"{rightLog}\n" +
            $"{leftLog}",
            this
        );
    }

    private string BuildHandDebugLog(
        string label,
        Bone ikBone,
        string ikBoneName,
        bool hasTarget,
        bool assigned,
        Vector3 targetGripWorldPosition,
        Vector2 assignedSkeletonWorldPosition,
        Vector2 assignedLocalPosition)
    {
        if (ikBone == null)
        {
            return
                $"[{label}]\n" +
                $"IK Bone [{ikBoneName}]: NULL";
        }

        if (!hasTarget)
        {
            return
                $"[{label}]\n" +
                $"IK Bone [{ikBoneName}]: Found\n" +
                $"Weapon Grip Target: NOT FOUND";
        }

        Vector3 actualIKWorldPosition = GetCharacterBoneUnityWorldPosition(ikBone);
        float distance = Vector3.Distance(actualIKWorldPosition, targetGripWorldPosition);

        string result = distance <= debugDistanceWarning
            ? "OK"
            : "MISMATCH";

        return
            $"[{label}] {result}\n" +
            $"IK Bone: {ikBoneName}\n" +
            $"Assigned: {assigned}\n" +
            $"Weapon Grip Unity World:     {FormatVector3(targetGripWorldPosition)}\n" +
            $"Assigned Skeleton World XY:  {FormatVector2(assignedSkeletonWorldPosition)}\n" +
            $"Assigned Bone Local XY:      {FormatVector2(assignedLocalPosition)}\n" +
            $"Actual IK Unity World After: {FormatVector3(actualIKWorldPosition)}\n" +
            $"Distance: {distance:F4}";
    }

    private void DebugLogFailedToApplyIfNeeded()
    {
        if (!debugIKFollow)
            return;

        if (!debugEveryFrame)
        {
            if (Time.time < nextDebugLogTime)
                return;

            nextDebugLogTime = Time.time + debugLogInterval;
        }

        Debug.LogWarning(
            $"[GunHandIKBinder] IK Apply Failed on {name}\n" +
            $"SkeletonAnimation: {skeletonAnimation != null}\n" +
            $"Skeleton: {(skeletonAnimation != null && skeletonAnimation.Skeleton != null)}\n" +
            $"WeaponRoot: {weaponRoot != null}\n" +
            $"Cached: {cached}\n" +
            $"RightIKBone [{rightHandIKTargetBoneName}]: {rightHandIKTargetBone != null}\n" +
            $"LeftIKBone [{leftHandIKTargetBoneName}]: {leftHandIKTargetBone != null}\n" +
            $"HasRightGripThisFrame: {hasRightTargetThisFrame}\n" +
            $"HasLeftGripThisFrame: {hasLeftTargetThisFrame}",
            this
        );
    }

    private Vector3 GetCharacterBoneUnityWorldPosition(Bone bone)
    {
        if (bone == null || skeletonAnimation == null)
            return default;

        Vector3 skeletonLocalPosition = new Vector3(
            bone.AppliedPose.WorldX,
            bone.AppliedPose.WorldY,
            0f
        );

        return skeletonAnimation.transform.TransformPoint(skeletonLocalPosition);
    }

    private string FormatVector3(Vector3 value)
    {
        return $"({value.x:F3}, {value.y:F3}, {value.z:F3})";
    }

    private string FormatVector2(Vector2 value)
    {
        return $"({value.x:F3}, {value.y:F3})";
    }

    // =========================================================
    // Context Menu Debug
    // =========================================================

    [ContextMenu("Debug Cache Character IK Bones")]
    private void DebugCacheCharacterIKBones()
    {
        CacheSpineBones();
    }

    [ContextMenu("Debug Current IK World Positions")]
    private void DebugCurrentIKWorldPositions()
    {
        if (skeletonAnimation == null || skeletonAnimation.Skeleton == null)
        {
            Debug.LogWarning("[GunHandIKBinder] SkeletonAnimation or Skeleton is null.", this);
            return;
        }

        if (!cached)
            CacheSpineBones();

        Vector3 rightWorld = rightHandIKTargetBone != null
            ? GetCharacterBoneUnityWorldPosition(rightHandIKTargetBone)
            : default;

        Vector3 leftWorld = leftHandIKTargetBone != null
            ? GetCharacterBoneUnityWorldPosition(leftHandIKTargetBone)
            : default;

        Debug.Log(
            $"[GunHandIKBinder] Current IK World Positions on {name}\n" +
            $"Right [{rightHandIKTargetBoneName}] Found={rightHandIKTargetBone != null}, World={FormatVector3(rightWorld)}\n" +
            $"Left [{leftHandIKTargetBoneName}] Found={leftHandIKTargetBone != null}, World={FormatVector3(leftWorld)}",
            this
        );
    }
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunHandOcclusionByAimAngle.cs`

```cs
using Spine.Unity;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class GunHandOcclusionByAimAngle : MonoBehaviour, ICharacterSubsystem, ICharacterLateTickable
{
    [Header("Character")]
    [SerializeField] private CharacterRoot characterRoot;

    [Header("Spine / Separator")]
    [SerializeField] private SkeletonAnimation skeletonAnimation;

    [Tooltip("SkeletonRenderSeparator 生成出来的部件名，例如 Hand_L2")]
    [SerializeField] private string handPartName = "Hand_L2";

    [Tooltip("可手动指定。如果不指定，会自动在 SkeletonAnimation 子物体里查找 handPartName")]
    [SerializeField] private Renderer handPartRenderer;

    [Header("Angle Source Auto Find")]
    [SerializeField] private string aimOriginName = "HoundWeaponMount";
    [SerializeField] private string aimTargetName = "GunMuzzlePoint";

    [Tooltip("没找到角度来源时，间隔多久重试一次。")]
    [SerializeField] private float missingReferenceRetryInterval = 0.5f;

    [Header("Activation")]
    [SerializeField] private bool requireUsableWeapon = true;

    [Header("Occlusion Rule")]


    [Header("Occlusion Rule")]
    [Tooltip("枪口相对角色正前方水平线的隐藏最小角度。负数是向下。")]
    [SerializeField] private float hideMinPitchAngle = -65f;

    [Tooltip("枪口相对角色正前方水平线的隐藏最大角度。0=水平，正数=允许轻微向上。")]
    [SerializeField] private float hideMaxPitchAngle = -30f;

    [Tooltip("枪口必须在角色前方达到多少才允许隐藏，避免身后或接近竖直时误触。范围 0~1。")]
    [SerializeField] private float minForwardAmountToHide = 0.2f;


    [Header("Debug")]
    [SerializeField] private bool debugLog;

    [Tooltip("勾上后无视状态和角度，强制隐藏 Hand_L2，方便测试")]
    [SerializeField] private bool forceHideForDebug;

    [SerializeField] private bool logAngle;

    private Transform aimOrigin;
    private Transform aimTarget;
    private bool cached;
    private bool _isInitialized;
    private float nextRetryTime;

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized)
            return;

        characterRoot = root;

        if (skeletonAnimation == null && characterRoot != null)
            skeletonAnimation = characterRoot.houndSkeletonAnimation;

        Cache(true);

        _isInitialized = true;
    }

    private void Awake()
    {
        if (characterRoot == null)
            characterRoot = GetComponentInParent<CharacterRoot>();

        if (skeletonAnimation == null && characterRoot != null)
            skeletonAnimation = characterRoot.houndSkeletonAnimation;
    }

    private void OnEnable()
    {
        Cache(true);
    }

    private void OnValidate()
    {

        missingReferenceRetryInterval = Mathf.Max(0.05f, missingReferenceRetryInterval);
        hideMinPitchAngle = Mathf.Clamp(hideMinPitchAngle, -89f, 89f);
        hideMaxPitchAngle = Mathf.Clamp(hideMaxPitchAngle, -89f, 89f);
        minForwardAmountToHide = Mathf.Clamp01(minForwardAmountToHide);
    }

    private void LateUpdate()
    {
        // 如果还没有被 CharacterRoot 初始化，也允许编辑器临时测试。
        if (_isInitialized)
            return;

        ManagedLateUpdate(Time.deltaTime);
    }

    public void ManagedLateUpdate(float deltaTime)
    {
        if (!cached || aimOrigin == null || aimTarget == null)
            Cache(false);

        if (handPartRenderer == null)
            return;

        if (forceHideForDebug)
        {
            SetHandPartVisible(false);
            return;
        }

        if (!IsOcclusionStateActive())
        {
            SetHandPartVisible(true);
            return;
        }

        if (aimOrigin == null || aimTarget == null)
        {
            SetHandPartVisible(true);
            return;
        }

        bool shouldHide = ShouldHide();

        SetHandPartVisible(!shouldHide);
    }

    private bool IsOcclusionStateActive()
    {
        if (characterRoot == null || characterRoot.characterDataBus == null)
            return false;

        CharacterDataBus bus = characterRoot.characterDataBus;

        if (requireUsableWeapon && !bus.canUseWeapon)
            return false;

        return
            bus.isAiming ||
            bus.isHoundAttacking ||
            bus.isHunterAttacking ||
            bus.currentHoundCombatAction == HoundCombatActionType.Aim ||
            bus.currentHoundCombatAction == HoundCombatActionType.Fire;
    }

    private void Cache(bool force)
    {
        if (!force && Time.time < nextRetryTime)
            return;

        nextRetryTime = Time.time + Mathf.Max(0.05f, missingReferenceRetryInterval);

        cached = false;

        if (characterRoot == null)
            characterRoot = GetComponentInParent<CharacterRoot>();

        if (skeletonAnimation == null && characterRoot != null)
            skeletonAnimation = characterRoot.houndSkeletonAnimation;

        if (handPartRenderer == null)
            handPartRenderer = FindRendererByName(handPartName);

        if (aimOrigin == null)
            aimOrigin = FindTransformByName(aimOriginName);

        if (aimTarget == null)
            aimTarget = FindTransformByName(aimTargetName);

        cached = handPartRenderer != null;

        if (!debugLog)
            return;

        if (handPartRenderer == null)
            Debug.LogWarning($"[GunHandOcclusionByAimAngle] Cannot find hand part renderer: {handPartName}", this);

        if (aimOrigin == null)
            Debug.LogWarning($"[GunHandOcclusionByAimAngle] Cannot find aim origin: {aimOriginName}", this);

        if (aimTarget == null)
            Debug.LogWarning($"[GunHandOcclusionByAimAngle] Cannot find aim target: {aimTargetName}", this);
    }

    private Renderer FindRendererByName(string targetName)
    {
        Transform target = FindTransformByName(targetName);

        if (target == null)
            return null;

        Renderer renderer = target.GetComponent<Renderer>();

        if (renderer != null)
            return renderer;

        return target.GetComponentInChildren<Renderer>(true);
    }

    private Transform FindTransformByName(string targetName)
    {
        if (string.IsNullOrEmpty(targetName))
            return null;

        Transform searchRoot = null;

        if (characterRoot != null)
            searchRoot = characterRoot.transform;
        else if (skeletonAnimation != null)
            searchRoot = skeletonAnimation.transform;
        else
            searchRoot = transform;

        return FindChildRecursive(searchRoot, targetName);
    }

    private Transform FindChildRecursive(Transform parent, string targetName)
    {
        if (parent == null)
            return null;

        if (parent.name == targetName)
            return parent;

        int childCount = parent.childCount;

        for (int i = 0; i < childCount; i++)
        {
            Transform child = parent.GetChild(i);
            Transform result = FindChildRecursive(child, targetName);

            if (result != null)
                return result;
        }

        return null;
    }

    private bool ShouldHide()
    {
        Vector2 aimDirection = aimTarget.position - aimOrigin.position;

        if (aimDirection.sqrMagnitude < 0.0001f)
        {
            LogDecision(Vector2.zero, 0f, 0f, false, "NoAimDirection");
            return false;
        }

        aimDirection.Normalize();

        Vector2 facingDirection = GetFacingDirection();

        if (facingDirection.sqrMagnitude < 0.0001f)
            facingDirection = Vector2.right;

        facingDirection.Normalize();

        float forwardAmount = Vector2.Dot(facingDirection, aimDirection);

        // 枪没有明显指向角色正前方时，不隐藏。
        if (forwardAmount < minForwardAmountToHide)
        {
            LogDecision(aimDirection, 0f, forwardAmount, false, "NotForwardEnough");
            return false;
        }

        // 关键：左右统一的上下倾角。
        // 水平正前方 = 0，向上 = 正数，向下 = 负数。
        float pitchAngle = Mathf.Atan2(aimDirection.y, forwardAmount) * Mathf.Rad2Deg;

        float min = Mathf.Min(hideMinPitchAngle, hideMaxPitchAngle);
        float max = Mathf.Max(hideMinPitchAngle, hideMaxPitchAngle);

        bool shouldHide = pitchAngle >= min && pitchAngle <= max;

        LogDecision(aimDirection, pitchAngle, forwardAmount, shouldHide, "PitchAngle");

        return shouldHide;
    }

    private Vector2 GetFacingDirection()
    {
        if (characterRoot == null || characterRoot.characterDataBus == null)
            return transform.right;

        CharacterDataBus bus = characterRoot.characterDataBus;

        switch (bus.currentFacing)
        {
            case Facing.Right:
                return Vector2.right;

            case Facing.Left:
                return Vector2.left;
        }

        Vector3 facing = bus.facingDirection;

        if (facing.sqrMagnitude > 0.0001f)
            return new Vector2(facing.x, facing.y);

        return transform.right;
    }

    private void SetHandPartVisible(bool visible)
    {
        if (handPartRenderer == null)
            return;

        if (handPartRenderer.enabled == visible)
            return;

        handPartRenderer.enabled = visible;

        if (debugLog)
            Debug.Log($"[GunHandOcclusionByAimAngle] Set {handPartRenderer.name} visible = {visible}", this);
    }

    private void LogDecision(Vector2 aimDirection, float pitchAngle, float forwardAmount, bool shouldHide, string reason)
    {
        if (!logAngle)
            return;

        Debug.Log(
            $"[GunHandOcclusionByAimAngle] Reason={reason}, " +
            $"Dir=({aimDirection.x:F2}, {aimDirection.y:F2}), " +
            $"PitchAngle={pitchAngle:F1}, " +
            $"Range={hideMinPitchAngle:F1}~{hideMaxPitchAngle:F1}, " +
            $"ForwardAmount={forwardAmount:F2}, " +
            $"MinForward={minForwardAmountToHide:F2}, " +
            $"Hide={shouldHide}",
            this
        );
    }

    [ContextMenu("Force Hide Hand Part")]
    private void ForceHideHandPart()
    {
        Cache(true);

        forceHideForDebug = true;

        if (handPartRenderer != null)
            SetHandPartVisible(false);
    }

    [ContextMenu("Force Show Hand Part")]
    private void ForceShowHandPart()
    {
        Cache(true);

        forceHideForDebug = false;

        if (handPartRenderer != null)
            SetHandPartVisible(true);
    }
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunModule.cs`

```cs
using Spine;
using Spine.Unity;
using UnityEngine;

public class GunWeaponModule : MonoBehaviour, IWeaponSubsystem
{
    private WeaponRoot weaponRoot;

    [Header("枪口设置")]
    [SerializeField] private SkeletonAnimation weaponSkeletonAnimation;
    [SerializeField] private string muzzleBoneName = "Muzzle";

    public SkeletonAnimation WeaponSkeletonAnimation
    {
        get
        {
            if (weaponSkeletonAnimation != null)
                return weaponSkeletonAnimation;

            if (weaponRoot != null)
                return weaponRoot.WeaponSkeletonAnimation;

            return null;
        }
    }

    public string MuzzleBoneName => muzzleBoneName;

    public void Initialize(WeaponRoot root)
    {
        weaponRoot = root;

        if (weaponSkeletonAnimation == null && weaponRoot != null)
            weaponSkeletonAnimation = weaponRoot.WeaponSkeletonAnimation;
    }

    public bool TryGetMuzzleBone(out Bone muzzleBone, out SkeletonAnimation skeletonAnimation)
    {
        muzzleBone = null;
        skeletonAnimation = WeaponSkeletonAnimation;

        if (skeletonAnimation == null)
            return false;

        if (skeletonAnimation.Skeleton == null)
            return false;

        if (string.IsNullOrEmpty(muzzleBoneName))
            return false;

        muzzleBone = skeletonAnimation.Skeleton.FindBone(muzzleBoneName);
        return muzzleBone != null;
    }
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunMuzzlePointBinder.cs`

```cs
using Spine;
using Spine.Unity;
using UnityEngine;

public class GunMuzzlePointBinder : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private WeaponRoot sourceWeaponRoot;
    [SerializeField] private float retryInterval = 0.05f;

    private GunWeaponModule boundGunModule;
    private SkeletonAnimation boundSkeletonAnimation;
    private Bone boundMuzzleBone;
    private float nextRetryTime;

    public bool IsBound =>
        boundGunModule != null &&
        boundSkeletonAnimation != null &&
        boundMuzzleBone != null;

    public void SetWeaponRoot(WeaponRoot root)
    {
        if (sourceWeaponRoot == root)
            return;

        sourceWeaponRoot = root;
        ClearBinding();
        nextRetryTime = 0f;
    }

    private void OnEnable()
    {
        nextRetryTime = 0f;
    }

    private void OnDisable()
    {
        ClearBinding();
    }

    private void LateUpdate()
    {
        if (!IsBound)
        {
            if (Time.unscaledTime < nextRetryTime)
                return;

            nextRetryTime = Time.unscaledTime + retryInterval;
            TryBind();
            return;
        }

        if (boundSkeletonAnimation == null || boundMuzzleBone == null)
        {
            ClearBinding();
            return;
        }

        SyncToMuzzle();
    }

    private bool TryBind()
    {
        WeaponRoot weaponRoot = ResolveWeaponRoot();
        if (weaponRoot == null)
            return false;

        if (!weaponRoot.IsInitialized)
            return false;

        if (!weaponRoot.IsFirearm)
            return false;

        if (!weaponRoot.TryGetWeaponSubsystem<GunWeaponModule>(out GunWeaponModule gunModule))
            return false;

        if (!gunModule.TryGetMuzzleBone(out Bone muzzleBone, out SkeletonAnimation skeletonAnimation))
            return false;

        sourceWeaponRoot = weaponRoot;
        boundGunModule = gunModule;
        boundSkeletonAnimation = skeletonAnimation;
        boundMuzzleBone = muzzleBone;

        SyncToMuzzle();
        return true;
    }

    private WeaponRoot ResolveWeaponRoot()
    {
        if (sourceWeaponRoot != null)
            return sourceWeaponRoot;

        WeaponRoot[] roots = Object.FindObjectsOfType<WeaponRoot>(true);

        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && roots[i].IsInitialized && roots[i].IsFirearm)
                return roots[i];
        }

        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && roots[i].IsFirearm)
                return roots[i];
        }

        return null;
    }

    private void SyncToMuzzle()
    {
        if (boundSkeletonAnimation == null || boundMuzzleBone == null)
        {
            ClearBinding();
            return;
        }

        Transform skeletonTransform = boundSkeletonAnimation.transform;

        Vector3 worldPos = skeletonTransform.TransformPoint(
            new Vector3(boundMuzzleBone.AppliedPose.WorldX, boundMuzzleBone.AppliedPose.WorldY, 0f)
        );

        transform.position = worldPos;
        transform.rotation = skeletonTransform.rotation * Quaternion.Euler(0f, 0f, boundMuzzleBone.AppliedPose.WorldRotationX);
    }

    public void ClearBinding()
    {
        boundGunModule = null;
        boundSkeletonAnimation = null;
        boundMuzzleBone = null;
    }
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunShotVisualController.cs`

```cs
using UnityEngine;

public class GunShotVisualController : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private GameObject tracerPrefab;
    [SerializeField] private GameObject defaultImpactPrefab;
    [SerializeField] private GameObject enemyImpactPrefab;

    [Header("Lifetime")]
    [SerializeField] private float muzzleFlashLifetime = 0.5f;
    [SerializeField] private float tracerLifetime = 0.05f;
    [SerializeField] private float impactLifetime = 0.6f;

    [Header("Muzzle Flash Transform")]
    [SerializeField] private Vector3 muzzleFlashRotationOffsetEuler = new Vector3(0f, 90f, 0f);
    [SerializeField] private float muzzleFlashScale = 0.5f;


    [Header("Sorting")]
    [SerializeField] private string muzzleFlashSortingLayer = "VFX";
    [SerializeField] private int muzzleFlashOrderInLayer = 100;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    public void PlayShot(
        Vector2 origin,
        Vector2 endPoint,
        RaycastHit2D hit,
        Transform muzzlePoint
    )
    {
        Vector2 shootDirection = endPoint - origin;

        if (shootDirection.sqrMagnitude <= 0.0001f)
            shootDirection = Vector2.right;

        shootDirection.Normalize();

        PlayMuzzleFlash(muzzlePoint, shootDirection);

        if (tracerPrefab != null)
        {
            PlayTracer(origin, endPoint);
        }

        if (hit.collider != null)
        {
            bool hitEnemy = hit.collider.GetComponentInParent<EnemyRoot>() != null;
            PlayImpact(hit.point, hit.normal, hitEnemy);
        }
    }

    public void PlayMuzzleFlash(Transform muzzlePoint, Vector2 shootDirection)
    {
        if (muzzleFlashPrefab == null)
        {
            Debug.LogWarning("[GunShotVisualController] Muzzle Flash Prefab is null", this);
            return;
        }

        if (muzzlePoint == null)
        {
            Debug.LogWarning("[GunShotVisualController] Muzzle Point is null", this);
            return;
        }

        if (shootDirection.sqrMagnitude <= 0.0001f)
            shootDirection = Vector2.right;

        shootDirection.Normalize();

        float angle = Mathf.Atan2(shootDirection.y, shootDirection.x) * Mathf.Rad2Deg;

        Quaternion aimRotation = Quaternion.Euler(0f, 0f, angle);
        Quaternion offsetRotation = Quaternion.Euler(muzzleFlashRotationOffsetEuler);
        Quaternion finalRotation = aimRotation * offsetRotation;

        Vector3 spawnPosition = muzzlePoint.position;

        // 如果仍然被遮挡，可以试试 -0.1 或 0.1
        spawnPosition.z = -0.1f;

        GameObject instance = Instantiate(
            muzzleFlashPrefab,
            spawnPosition,
            finalRotation
        );

        instance.transform.localScale = Vector3.one * muzzleFlashScale;

        ApplyParticleSorting(instance);

        ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem ps in particles)
        {
            ps.gameObject.SetActive(true);
            ps.Clear(true);
            ps.Play(true);
        }

        if (debugLog)
        {
            Debug.Log(
                $"[GunShotVisualController] Spawn MuzzleFlash at {spawnPosition}, angle={angle}, particles={particles.Length}",
                this
            );
        }

        Destroy(instance, muzzleFlashLifetime);
    }

    private void PlayTracer(Vector2 origin, Vector2 endPoint)
    {
        if (tracerPrefab == null)
            return;

        GameObject instance = Instantiate(
            tracerPrefab,
            origin,
            Quaternion.identity
        );

        LineRenderer lineRenderer = instance.GetComponent<LineRenderer>();

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, origin);
            lineRenderer.SetPosition(1, endPoint);
        }

        Destroy(instance, tracerLifetime);
    }

    public void PlayImpact(Vector3 position, Vector2 normal, bool hitEnemy)
    {
        GameObject prefab = hitEnemy ? enemyImpactPrefab : defaultImpactPrefab;

        if (prefab == null)
            return;

        float angle = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg;

        GameObject instance = Instantiate(
            prefab,
            position,
            Quaternion.Euler(0f, 0f, angle)
        );

        ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem ps in particles)
        {
            ps.gameObject.SetActive(true);
            ps.Clear(true);
            ps.Play(true);
        }

        if (debugLog)
        {
            Debug.Log(
                $"[GunShotVisualController] Spawn Impact at {position}, hitEnemy={hitEnemy}, particles={particles.Length}",
                this
            );
        }

        Destroy(instance, impactLifetime);
    }

    private void ApplyParticleSorting(GameObject instance)
    {
        ParticleSystemRenderer[] renderers =
            instance.GetComponentsInChildren<ParticleSystemRenderer>(true);

        foreach (ParticleSystemRenderer renderer in renderers)
        {
            renderer.sortingLayerName = muzzleFlashSortingLayer;
            renderer.sortingOrder = muzzleFlashOrderInLayer;
        }
    }





}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunSystemController.cs`

```cs
using UnityEngine;

// GunSystemController.ApplyWeaponRuntimeData(runtimeData)
//  -> 读取 damage / range / magazine
//  -> 读取 FirearmActionProfileSO
//  -> 读取 defaultFireMode
//  -> 读取 GunFireModeConfig
//  -> 计算 fireInterval 等运行时数据
//  -> 支持半自动 / 点射 / 全自动
public class GunSystemController : MonoBehaviour, ICharacterSubsystem,ICharacterTickable
{
    private CharacterRoot characterRoot;
    private CharacterDataBus dataBus;

    [Header("Fire Point")]
    [SerializeField] private Transform muzzlePoint;

    [Header("Hitscan")]
    [SerializeField] private LayerMask hitMask;
    [SerializeField] private float range = 30f;
    [SerializeField] private float damage = 20f;

    [Header("Fire Rate")]
    [SerializeField] private float fireCooldown = 0.15f;
    private float nextFireTime;

    [Header("Ammo")]
    [SerializeField] private bool infiniteAmmo = false;//无限子弹
    [SerializeField] private int magazineSize = 0;
    [SerializeField] private int ammoInMagazine = 0;

    [Header("Aim")]
    [SerializeField] private bool preferLockTargetPoint = true;
    private LockTargetPoint currentLockTargetPoint;

    [Header("Debug")]
    [SerializeField] private bool debugDrawRay = true;
    [SerializeField] private bool debugLogFire = true;

    [Header("Fire Mode")]

    [SerializeField] private FirearmActionProfileSO firearmProfile;
    [SerializeField] private GunFireModeType currentFireModeType;
    [SerializeField] private GunFireModeConfig currentFireModeConfig;
    [Header("Runtime Stats")]
    [SerializeField] private FirearmWeaponStats currentFirearmStats;
    [SerializeField] private WeaponAudioProfileSO currentWeaponAudioProfile;
    [SerializeField] private GunShotVisualController shotVisualController;
    [Header("Hand IK")]
    [SerializeField] private GunHandIKBinder gunHandIKBinder;

    private bool burstActive;
    private int burstShotsRemaining;
    private float nextBurstShotTime;
    private float nextFireInputAllowedTime;


    public int AmmoInMagazine => ammoInMagazine;
    public int MagazineSize => magazineSize;



    /// <summary>
    /// Debug 
    /// </summary>
    /// <param name="root"></param>

    [SerializeField] private float emptyMagazineLogInterval = 0.25f;
    private float nextEmptyMagazineLogTime;




    public void Initialize(CharacterRoot root)
    {
        characterRoot = root;

        if (characterRoot != null)
            dataBus = characterRoot.characterDataBus;
        if (gunHandIKBinder == null && characterRoot != null)
            gunHandIKBinder = characterRoot.GetComponentInChildren<GunHandIKBinder>(true);
        if (ammoInMagazine <= 0)
            ammoInMagazine = magazineSize;
    }


    public void ManagedUpdate(float deltaTime)
    {
        UpdateBurstFire();
    }


    private void UpdateBurstFire()
    {
        if (!burstActive)
            return;

        if (currentFireModeConfig == null)
        {
            burstActive = false;
            return;
        }

        if (burstShotsRemaining <= 0)
        {
            burstActive = false;
            nextFireInputAllowedTime = Time.time + currentFireModeConfig.burstCycleInterval;
            return;
        }

        if (Time.time < nextBurstShotTime)
            return;

        bool fired = TryFire();

        if (!fired)
        {
            // 没子弹或超时间时直接中断 burst
            burstActive = false;
            return;
        }

        burstShotsRemaining--;

        nextBurstShotTime = Time.time + currentFireModeConfig.burstShotInterval;

        if (burstShotsRemaining <= 0)
        {
            burstActive = false;
            nextFireInputAllowedTime = Time.time + currentFireModeConfig.burstCycleInterval;
        }
    }






    public void SetCurrentLockTargetPoint(LockTargetPoint targetPoint)
    {
        currentLockTargetPoint = targetPoint;
    }


    public bool CanFire()
    {
        if (characterRoot == null || dataBus == null)
            return false;

        if (muzzlePoint == null)
            return false;

        if (Time.time < nextFireTime)
            return false;

        if (!infiniteAmmo && ammoInMagazine <= 0)
            return false;

        return true;
    }



    public bool TryFire()
    {
        if (!CanFire())
        {
            LogCannotFire();
            return false;
        }

        FireInternal();
        return true;
    }
    private void LogCannotFire()
    {
        if (!debugLogFire)
            return;

        if (!infiniteAmmo && ammoInMagazine <= 0)
        {
            if (Time.time >= nextEmptyMagazineLogTime)
            {
                nextEmptyMagazineLogTime = Time.time + emptyMagazineLogInterval;

                Debug.Log(
                    $"[GunSystemController] Empty magazine. ammo={ammoInMagazine}/{magazineSize}",
                    this
                );
            }

            return;
        }

        Debug.Log(
            $"[GunSystemController] Cannot fire. time={Time.time:F2}, next={nextFireTime:F2}, ammo={ammoInMagazine}/{magazineSize}",
            this
        );
    }

    private void FireInternal()
    {
        nextFireTime = Time.time + fireCooldown;

        if (!infiniteAmmo)
            ammoInMagazine = Mathf.Max(0, ammoInMagazine - 1);

        Vector2 origin = muzzlePoint.position;
        Vector2 direction = GetShootDirection(origin);

        if (direction.sqrMagnitude <= 0.0001f)
            direction = characterRoot.transform.right;

        direction.Normalize();

        PublishWeaponFiredEvent(origin, direction);

        RaycastHit2D hit = Physics2D.Raycast(origin, direction, range, hitMask);



        Vector2 endPoint = origin + direction * range;

        if (hit.collider != null)
        {
            endPoint = hit.point;
            HandleHit(hit, direction);
        }
        else if (debugLogFire)
        {
            Debug.Log(
                $"[GunSystemController] Fire miss. ammo={ammoInMagazine}/{magazineSize}",
                this
            );
        }

        shotVisualController?.PlayShot(
    origin,
    endPoint,
    hit,
    muzzlePoint
);

        SpawnShotVisual(origin, endPoint);

        if (debugDrawRay)
            Debug.DrawLine(origin, endPoint, Color.yellow, 0.25f);

        if (debugLogFire)
        {
            Debug.Log(
                $"[GunSystemController] Shot fired. ammo={ammoInMagazine}/{magazineSize}",
                this
            );
        }
    }



    private Vector2 GetShootDirection(Vector2 origin)
    {
        if (preferLockTargetPoint
            && currentLockTargetPoint != null
            && currentLockTargetPoint.IsValid())
        {
            return (Vector2)currentLockTargetPoint.GetWorldPosition() - origin;
        }

        if (dataBus != null)
        {
            return (Vector2)dataBus.mousePositionOnScreen - origin;
        }

        return Vector2.right;
    }
    private void HandleHit(RaycastHit2D hit, Vector2 shotDirection)
    {
        if (hit.collider == null)
            return;

        EnemyRoot enemyRoot = hit.collider.GetComponentInParent<EnemyRoot>();

        if (enemyRoot != null)
        {
            Debug.Log(
                $"[GunSystemController] Hit enemy: {enemyRoot.name}, collider={hit.collider.name}, damage={damage}, ammo={ammoInMagazine}/{magazineSize}",
                this
            );
        }
        else
        {
            Debug.Log(
                $"[GunSystemController] Hit non-enemy: {hit.collider.name}, layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}",
                this
            );

            SpawnImpactEffect(hit.point, hit.normal, null);
            return;
        }

        if (!enemyRoot.IsAlive || !enemyRoot.CanBeTargeted)
        {
            SpawnImpactEffect(hit.point, hit.normal, enemyRoot);
            return;
        }

        IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();

        if (damageable == null || !damageable.CanTakeDamage)
        {
            SpawnImpactEffect(hit.point, hit.normal, enemyRoot);
            return;
        }

        DamageInfo damageInfo = new DamageInfo
        {
            attacker = characterRoot != null ? characterRoot.gameObject : gameObject,
            damage = damage,
            hitPoint = hit.point,
            hitNormal = hit.normal,
            shotDirection = shotDirection,
            impactType = CombatImpactType.Gunshot
        };

        damageable.TakeDamage(damageInfo);

        SpawnImpactEffect(hit.point, hit.normal, enemyRoot);
    }

  

    private void SpawnShotVisual(Vector2 origin, Vector2 endPoint)
    {
        // 第一版先空开。
        // 后续是枪口火花、LineRenderer 弹道、枪口烟雾等。
    }

    private void SpawnImpactEffect(Vector2 hitPoint, Vector2 hitNormal, EnemyRoot enemyRoot)
    {

        PublishBulletImpactEvent(hitPoint, hitNormal, enemyRoot);
        // 第一版先空开。
        // 后续是血溅、火花、受击特效。
    }
    public bool CanStartReload()
    {
        if (infiniteAmmo)
            return false;

        if (magazineSize <= 0)
            return false;

        if (ammoInMagazine >= magazineSize)
            return false;

        return true;
    }

    public void BeginReload()
    {
        // 当前阶段可以先只标记，以后在这里发 WeaponReloadStartedEvent
    }

    public void CommitReload()
    {
        ammoInMagazine = Mathf.Max(0, magazineSize);

        // 当前阶段可以 Debug
        Debug.Log($"[GunSystemController] Reload committed. ammo={ammoInMagazine}/{magazineSize}", this);
    }


    public void EndReload()
    {
        // 当前阶段可以先只标记，以后在这里发 WeaponReloadFinishedEvent
    }



    public void ApplyWeaponRuntimeData(WeaponRuntimeData weaponRuntimeData)
    {
        if (weaponRuntimeData == null)
        {
            ClearWeaponRuntimeData();
            return;
        }

        /*
         * 切换到非枪械时也要清空枪械数据。
         */
        if (!weaponRuntimeData.IsFirearm)
        {
            ClearWeaponRuntimeData();
            return;
        }

        WeaponCommonCoreStats commonStats = weaponRuntimeData.CommonStats;
        FirearmWeaponStats firearmStats = weaponRuntimeData.FirearmStats;

        currentFirearmStats = firearmStats;
        currentWeaponAudioProfile = weaponRuntimeData.AudioProfile;

        if (commonStats != null)
        {
            damage = commonStats.damage;
        }

        if (firearmStats != null)
        {
            range = firearmStats.range;
            magazineSize = Mathf.Max(1, firearmStats.magazineSize);
            ammoInMagazine = magazineSize;
        }

        firearmProfile = weaponRuntimeData.FirearmActionProfile;

        if (firearmProfile != null)
        {
            currentFireModeType = firearmProfile.defaultFireMode;
            currentFireModeConfig = firearmProfile.GetDefaultFireModeConfig();

            if (currentFireModeConfig != null)
                fireCooldown = currentFireModeConfig.fireInterval;
        }
        else
        {
            currentFireModeConfig = null;
        }

        burstActive = false;
        burstShotsRemaining = 0;
        nextBurstShotTime = 0f;
        nextFireInputAllowedTime = 0f;
        nextFireTime = 0f;


//        Debug.Log(
//    $"[GunSystemController] ApplyWeaponRuntimeData: damage={damage}, range={range}, magazine={ammoInMagazine}/{magazineSize}, fireMode={currentFireModeType}",
//    this
//);


    }


   

    /// <summary>
    /// 切换射击模式接口
    /// </summary>
    public GunFireModeType CurrentFireModeType => currentFireModeType;
    public GunFireModeConfig CurrentFireModeConfig => currentFireModeConfig;

    public void SwitchToNextFireMode()
    {
        if (firearmProfile == null || firearmProfile.fireModes == null)
            return;

        if (firearmProfile.fireModes.Count == 0)
            return;

        int currentIndex = -1;

        for (int i = 0; i < firearmProfile.fireModes.Count; i++)
        {
            if (firearmProfile.fireModes[i] == null)
                continue;

            if (firearmProfile.fireModes[i].fireModeType == currentFireModeType)
            {
                currentIndex = i;
                break;
            }
        }

        int nextIndex = currentIndex + 1;

        if (nextIndex >= firearmProfile.fireModes.Count)
            nextIndex = 0;

        SetFireMode(firearmProfile.fireModes[nextIndex].fireModeType);
    }

    public bool SetFireMode(GunFireModeType modeType)
    {
        if (firearmProfile == null)
            return false;

        GunFireModeConfig config = firearmProfile.GetFireModeConfig(modeType);

        if (config == null)
            return false;

        currentFireModeType = modeType;
        currentFireModeConfig = config;

        fireCooldown = currentFireModeConfig.fireInterval;

        burstActive = false;
        burstShotsRemaining = 0;

        Debug.Log($"[GunSystemController] Switch fire mode: {currentFireModeType}", this);

        return true;
    }

    public bool TryHandleFireInput(bool attackPressedThisFrame, bool attackHeld)
    {
        if (currentFireModeConfig == null)
            return TryFire();

        switch (currentFireModeConfig.fireModeType)
        {
            case GunFireModeType.SemiAuto:
                return HandleSemiAutoFire(attackPressedThisFrame);

            case GunFireModeType.FullAuto:
                return HandleFullAutoFire(attackHeld);

            case GunFireModeType.Burst:
                return HandleBurstFireInput(attackPressedThisFrame);

            default:
                return HandleSemiAutoFire(attackPressedThisFrame);
        }
    }
    private bool HandleSemiAutoFire(bool attackPressedThisFrame)
    {
        if (!attackPressedThisFrame)
            return false;

        if (Time.time < nextFireInputAllowedTime)
            return false;

        bool fired = TryFire();

        if (fired)
        {
            float interval = currentFireModeConfig != null
                ? currentFireModeConfig.fireInterval
                : fireCooldown;

            nextFireInputAllowedTime = Time.time + interval;
        }

        return fired;
    }
    private bool HandleFullAutoFire(bool attackHeld)
    {
        if (currentFireModeConfig != null && !currentFireModeConfig.allowHoldToFire)
            return false;

        if (!attackHeld)
            return false;

        if (Time.time < nextFireInputAllowedTime)
            return false;

        bool fired = TryFire();

        if (fired)
        {
            float interval = currentFireModeConfig != null
                ? currentFireModeConfig.fireInterval
                : fireCooldown;

            nextFireInputAllowedTime = Time.time + interval;
        }

        return fired;
    }

    private bool HandleBurstFireInput(bool attackPressedThisFrame)
    {
        if (!attackPressedThisFrame)
            return false;

        if (burstActive)
            return false;

        if (Time.time < nextFireInputAllowedTime)
            return false;

        StartBurst();

        return true;
    }

    private void StartBurst()
    {
        if (currentFireModeConfig == null)
            return;

        burstActive = true;
        burstShotsRemaining = Mathf.Max(1, currentFireModeConfig.burstCount);
        nextBurstShotTime = Time.time;
    }

    private void PublishWeaponFiredEvent(Vector2 origin, Vector2 direction)
    {
        if (GameFlowRoot.Instance == null)
            return;

        if (GameFlowRoot.Instance.EventHub == null)
            return;

        GameFlowRoot.Instance.EventHub.Hunt.Publish(new WeaponFiredEvent
        {
            owner = characterRoot != null ? characterRoot.gameObject : gameObject,
            position = origin,
            direction = direction,
            audioProfile = currentWeaponAudioProfile,
            isPlayer = true
        });
    }
    private void PublishBulletImpactEvent(Vector2 hitPoint, Vector2 hitNormal, EnemyRoot enemyRoot)
    {
        if (GameFlowRoot.Instance == null)
            return;

        if (GameFlowRoot.Instance.EventHub == null)
            return;

        GameFlowRoot.Instance.EventHub.Hunt.Publish(new BulletImpactEvent
        {
            position = hitPoint,
            normal = hitNormal,
            hitObject = enemyRoot != null ? enemyRoot.gameObject : null,
            surfaceType = enemyRoot != null ? SurfaceType.Flesh : SurfaceType.Default,
            hitEnemy = enemyRoot != null,
            audioProfile = currentWeaponAudioProfile
        });
    }


    public void SetRightHandIKLocked(bool locked)
    {
        if (gunHandIKBinder == null)
            return;

        gunHandIKBinder.SetRightHandIKBindingEnabled(locked);
    }

    public void SetLeftHandIKLocked(bool locked)
    {
        if (gunHandIKBinder == null)
            return;

        gunHandIKBinder.SetLeftHandIKBindingEnabled(locked);
    }

    public void SetHandIKLocked(bool rightLocked, bool leftLocked)
    {
        if (gunHandIKBinder == null)
            return;

        gunHandIKBinder.SetHandIKBindingEnabled(rightLocked, leftLocked);
    }

    public void ReleaseLeftHandForAction()
    {
        SetLeftHandIKLocked(false);
    }

    public void RestoreLeftHandIK()
    {
        SetLeftHandIKLocked(true);
    }


    public void ClearWeaponRuntimeData()
    {
        /*
         * 停止所有未完成的射击行为。
         */
        burstActive = false;
        burstShotsRemaining = 0;
        nextBurstShotTime = 0f;
        nextFireInputAllowedTime = 0f;
        nextFireTime = 0f;

        /*
         * 清空当前枪械配置。
         */
        firearmProfile = null;
        currentFireModeConfig = null;
        currentFirearmStats = null;
        currentWeaponAudioProfile = null;

        /*
         * 关闭精确射击相关状态。
         */
        currentFireModeType = default;

        magazineSize = 0;
        ammoInMagazine = 0;

        /*
         * damage/range 可以保留 Inspector 默认值。
         * 也可以清零。这里建议清零，防止卸枪时
         * 仍然使用上一个枪械数据。
         */
        damage = 0f;
        range = 0f;

        currentLockTargetPoint = null;

        Debug.Log(
            "[GunSystemController] Weapon runtime data cleared.",
            this
        );
    }



}
```
