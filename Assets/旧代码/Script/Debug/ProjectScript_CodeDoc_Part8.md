# ProjectScript Code Documentation - Part 8

> Source: `E:\WORLDCREATOR\new\Project_VELGITA\Assets\Script`  |  Total files: 212  |  This part: 20

## Architecture summary

This module contains 212 supported source files. The summary is inferred from directory layout, namespaces, declared types, and explicit asset references.

## File index

| File | Lines | Namespaces | Types | Asset refs |
|---|---:|---|---|---:|
| `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunTracerVisual.cs` | 21 | - | class GunTracerVisual | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/HunterGunBonePositionMatcher.cs` | 252 | - | class HunterGunBonePositionMatcher | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/IWeaponSubsystem.cs` | 8 | - | interface IWeaponSubsystem | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/CharacterCombatGeometryProvider.cs` | 63 | - | class CharacterCombatGeometryProvider | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/CombatEffectContext.cs` | 123 | - | struct CombatEffectContext | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/CombatEffectExecutor.cs` | 30 | - | class CombatEffectExecutor | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/CombatEffectSO.cs` | 31 | - | enum CombatEffectTargetType, class CombatEffectSO | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/CombatHitAnchor.cs` | 12 | - | class CombatHitAnchor | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/CombatTargetResolver.cs` | 28 | - | class CombatTargetResolver | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/DamageEffectSO.cs` | 87 | - | class DamageEffectSO | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/MeleeAttackStep.cs` | 59 | - | class MeleeAttackStep | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/MeleeComboActionSO.cs` | 30 | - | class MeleeComboActionSO | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/MeleeHitbox.cs` | 29 | - | class MeleeHitbox | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/MeleeHitController.cs` | 142 | - | class MeleeHitController | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/MeleeHitProfileSO.cs` | 35 | - | enum MeleeHitShapeType, class MeleeHitProfileSO | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/MeleeHitWindowDefinition.cs` | 33 | - | enum MeleeHitGeometrySource, class MeleeHitWindowDefinition | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/WeaponActionProfileSO.cs` | 6 | - | class WeaponActionProfileSO | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/WeaponConfigSO.cs` | 75 | - | class WeaponConfigSO | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/WeaponDataBus.cs` | 33 | - | class WeaponDataBus | 0 |
| `Assets/Script/Character/Core/Systems/WeaponSystem/WeaponRoot.cs` | 582 | - | class WeaponRoot | 0 |

## Public API

### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunTracerVisual.cs`
- `public class GunTracerVisual : MonoBehaviour
{`
- `public void Initialize(Vector2 origin, Vector2 endPoint, float lifetime)
    {`
### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/HunterGunBonePositionMatcher.cs`
- `public class HunterGunBonePositionMatcher : MonoBehaviour, ICharacterSubsystem,ICharacterTickable,ICharacterLateTickable
{`
- `public void Initialize(CharacterRoot root)
    {`
- `public void ManagedUpdate(float deltaTime)
    {`
- `public void ManagedLateUpdate(float deltaTime)
    {`
- `public void ForceRefresh()
    {`
### `Assets/Script/Character/Core/Systems/WeaponSystem/IWeaponSubsystem.cs`
- `public interface IWeaponSubsystem
{`
### `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/CharacterCombatGeometryProvider.cs`
- `public class CharacterCombatGeometryProvider : MonoBehaviour
{`
- `public void RebuildCache()
    {`
- `public bool TryGetCharacterAnchor(
        string anchorId,
        out CombatHitAnchor anchor)
    {`
### `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/CombatEffectContext.cs`
- `public readonly struct CombatEffectContext
{`
- `public readonly CharacterRoot attackerRoot;`
- `public readonly WeaponRuntimeData sourceWeapon;`
- `public readonly MeleeComboActionSO action;`
- `public readonly int stepIndex;`
- `public readonly int attackInstanceId;`
- `public readonly Collider2D hitCollider;`
- `public readonly GameObject target;`
- `public readonly IDamageable damageable;`
- `public readonly Vector2 hitPoint;`
- `public readonly Vector2 hitNormal;`
- `public readonly Vector2 attackDirection;`
- `public readonly float powerMultiplier;`
- `public GameObject Attacker
    {`
- `public bool HasTarget => target != null;`
- `public bool CanDamage
    {`
- `public CombatEffectContext(
        CharacterRoot attackerRoot,
        WeaponRuntimeData sourceWeapon,
        MeleeComboActionSO action,
        int stepIndex,
        int attackInstanceId,
        Collider2D hitCollider,
        GameObject target,
        IDamageable damageable,
        Vector2 hitPoint,
        Vector2 hitNormal,
        Vector2 attackDirection,
        float powerMultiplier = 1f)
    {`
### `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/CombatEffectExecutor.cs`
- `public static class CombatEffectExecutor
{`
- `public static void ApplyEffects( MeleeHitProfileSO profile,in CombatEffectContext context)
    {`
### `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/CombatEffectSO.cs`
- `public enum CombatEffectTargetType
{`
- `public abstract class CombatEffectSO : ScriptableObject
{`
- `public CombatEffectTargetType targetTypes = CombatEffectTargetType.Any;`
- `public abstract bool CanApply(
        in CombatEffectContext context);`
- `public abstract void Apply(
        in CombatEffectContext context);`
### `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/CombatHitAnchor.cs`
- `public class CombatHitAnchor : MonoBehaviour
{`
- `public string AnchorId => anchorId;`
- `public Vector2 WorldPosition =>
        transform.position;`
### `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/CombatTargetResolver.cs`
- `public static class CombatTargetResolver
{`
- `public static GameObject ResolveTarget(
        Collider2D collider,
        out IDamageable damageable)
    {`
### `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/DamageEffectSO.cs`
- `public class DamageEffectSO : CombatEffectSO
{`
- `public CombatDamageValueMode valueMode =
        CombatDamageValueMode.WeaponCoreMultiplier;`
- `public float flatDamage;`
- `public float weaponDamageMultiplier = 1f;`
- `public CombatImpactType impactType = CombatImpactType.Unknown;`
- `public override bool CanApply(
        in CombatEffectContext context)
    {`
- `public override void Apply(
        in CombatEffectContext context)
    {`
### `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/MeleeAttackStep.cs`
- `public class MeleeAttackStep
{`
- `public EAnimSemantic semantic =
        EAnimSemantic.Attack_Normal_1;`
- `public float fallbackDuration = 0.5f;`
- `public bool allowInputBuffer = true;`
- `public float fallbackComboWindowDuration = 0.35f;`
- `public string hitOpenEvent = "HitOpen";`
- `public string hitCloseEvent = "HitClose";`
- `public string comboOpenEvent = "ComboOpen";`
- `public string comboCloseEvent = "ComboClose";`
- `public string cancelOpenEvent = "CancelOpen";`
- `public string cancelCloseEvent = "CancelClose";`
- `public string actionEndEvent = "ActionEnd";`
- `public List<MeleeHitWindowDefinition> hitWindows = new List<MeleeHitWindowDefinition>();`
- `public AudioEventSO swingAudio;`
- `public float hitStopDuration;`
- `public void Validate()
    {`
### `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/MeleeComboActionSO.cs`
- `public class MeleeComboActionSO : ScriptableObject
{`
- `public string actionId;`
- `public List<MeleeAttackStep> steps = new List<MeleeAttackStep>();`
### `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/MeleeHitbox.cs`
- `public class MeleeHitbox : MonoBehaviour
{`
- `public string HitboxId => hitboxId;`
- `public Collider2D HitCollider => hitCollider;`
### `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/MeleeHitController.cs`
- `public class MeleeHitController : MonoBehaviour
{`
### `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/MeleeHitProfileSO.cs`
- `public enum MeleeHitShapeType
{`
- `public class MeleeHitProfileSO : ScriptableObject
{`
- `public LayerMask targetMask;`
- `public bool allowRepeatedHit;`
- `public List<CombatEffectSO> effects = new List<CombatEffectSO>();`
### `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/MeleeHitWindowDefinition.cs`
- `public enum MeleeHitGeometrySource
{`
- `public class MeleeHitWindowDefinition
{`
- `public string windowId = "Primary";`
- `public MeleeHitGeometrySource geometrySource = MeleeHitGeometrySource.CharacterAnchor;`
- `public string characterAnchorId = "KickFoot";`
- `public float anchorRadius = 0.35f;`
- `public string weaponBoundingBoxId;`
- `public string openEvent = "MeleeHitOpen";`
- `public string closeEvent = "MeleeHitClose";`
- `public MeleeHitProfileSO hitProfile;`
### `Assets/Script/Character/Core/Systems/WeaponSystem/WeaponActionProfileSO.cs`
- `public abstract class WeaponActionProfileSO : ScriptableObject
{`
- `public abstract WeaponType WeaponType {`
### `Assets/Script/Character/Core/Systems/WeaponSystem/WeaponConfigSO.cs`
- `public class WeaponConfigSO : ScriptableObject
{`
- `public string weaponName;`
- `public WeaponType weaponType = WeaponType.Firearm;`
- `public WeaponDefaultFacing defaultFacing = WeaponDefaultFacing.Right;`
- `public WeaponStatsSO stats;`
- `public WeaponActionProfileSO weaponActionProfile;`
- `public WeaponAudioProfileSO audioProfile;`
- `public AnimationPlanOverrideRuleSetSO animationOverride;`
- `public WeaponRoot weaponPrefab;`
- `public bool IsFirearm => weaponType == WeaponType.Firearm;`
- `public bool IsMelee => weaponType == WeaponType.Melee;`
### `Assets/Script/Character/Core/Systems/WeaponSystem/WeaponDataBus.cs`
- `public class WeaponDataBus
{`
- `public WeaponRoot weaponRoot;`
- `public CharacterRoot owner;`
- `public WeaponConfigSO config;`
- `public WeaponStatsSO weaponStats;`
- `public WeaponActionProfileSO weaponActionProfile;`
- `public FirearmActionProfileSO firearmActionProfile;`
- `public AnimationPlanOverrideRuleSetSO weaponAnimationOverride;`
- `public bool isEquipped;`
- `public bool isAttacking;`
- `public void LoadFromSO(WeaponConfigSO config)
    {`
### `Assets/Script/Character/Core/Systems/WeaponSystem/WeaponRoot.cs`
- `public class WeaponRoot : MonoBehaviour
{`
- `public WeaponConfigSO Config => config;`
- `public WeaponDataBus WeaponDataBus => weaponDataBus;`
- `public CharacterRoot OwnerRoot => ownerRoot;`
- `public Transform VisualRoot => visualRoot;`
- `public SkeletonAnimation WeaponSkeletonAnimation => weaponSkeletonAnimation;`
- `public bool IsInitialized => isInitialized;`
- `public Bone MagazineRootBone => magazineRoot;`
- `public Bone MagazineMouthBone => magazineMouth;`
- `public Bone MagazineGripPointBone => magazineGripPoint;`
- `public Bone GunBodyGripPointBone => gunBodyGripPoint;`
- `public Bone FrontgripGripPointBone => frontgrip_GripPointBone;`
- `public Bone NoFrontgripGripPointBone => noFrontgrip_GripPointBone;`
- `public WeaponActionProfileSO ActionProfile
    {`
- `public FirearmActionProfileSO FirearmActionProfile
    {`
- `public WeaponType WeaponType
    {`
- `public bool IsFirearm => WeaponType == WeaponType.Firearm;`
- `public bool IsMelee => WeaponType == WeaponType.Melee;`
- `public void Initialize(WeaponConfigSO weaponConfig, CharacterRoot owner)
    {`
- `public bool TryGetWeaponSubsystem<T>(out T subsystem) where T : class, IWeaponSubsystem
    {`
- `public void BindSkeletonAnimation(SkeletonAnimation skeletonAnimation)
    {`
- `public void CacheBones()
    {`
- `public bool TryGetRightHandGripWorldPosition(out Vector3 worldPosition)
    {`
- `public bool TryGetLeftHandGripWorldPosition(out Vector3 worldPosition)
    {`
- `public bool TryGetFrontGripWorldPosition(out Vector3 worldPosition)
    {`
- `public bool TryGetNoFrontGripWorldPosition(out Vector3 worldPosition)
    {`
- `public bool TryGetMagazineRootWorldPosition(out Vector3 worldPosition)
    {`
- `public bool TryGetMagazineMouthWorldPosition(out Vector3 worldPosition)
    {`
- `public bool TryGetMagazineGripPointWorldPosition(out Vector3 worldPosition)
    {`

## Referenced assets

- No explicit `Assets/...` references detected.

## Complete source

### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/GunTracerVisual.cs`

```cs
using UnityEngine;

public class GunTracerVisual : MonoBehaviour
{
    [SerializeField] private LineRenderer lineRenderer;

    public void Initialize(Vector2 origin, Vector2 endPoint, float lifetime)
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, origin);
            lineRenderer.SetPosition(1, endPoint);
        }

        Destroy(gameObject, lifetime);
    }
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/GunSystem/HunterGunBonePositionMatcher.cs`

```cs
using Spine;
using Spine.Unity;
using UnityEngine;

public class HunterGunBonePositionMatcher : MonoBehaviour, ICharacterSubsystem,ICharacterTickable,ICharacterLateTickable
{
    [Header("Character Root")]
    [SerializeField] private CharacterRoot characterRoot;

    [Header("Source: Hound Skeleton")]
    [SerializeField] private SkeletonAnimation houndSkeletonAnimation;

    [Tooltip("Hound 角色文件里作为位置来源的骨骼")]
    [SerializeField] private string houndSourceBoneName = "Root_Position_Hund";

    [Header("Target: HunterGun Skeleton")]
    [SerializeField] private SkeletonAnimation hunterGunSkeletonAnimation;

    [Tooltip("HunterGun 文件里需要对齐到 HoundSourceBone 的骨骼")]
    [SerializeField] private string hunterGunTargetBoneName = "Root_Hunter";

    [Header("Move This Transform")]
    [Tooltip("通常填 VisualRoot_HunterGun。如果为空，则默认移动本物体 transform")]
    [SerializeField] private Transform hunterGunVisualRoot;

    [Header("Offset")]
    [Tooltip("世界坐标偏移，用于微调 HunterGun 整体位置")]
    [SerializeField] private Vector3 worldOffset = Vector3.zero;

    [Header("Options")]
    [Tooltip("是否在 LateUpdate 中执行。建议开启，因为 Spine 通常先 Update 再 LateUpdate 对齐")]
    [SerializeField] private bool followInLateUpdate = true;

    [Tooltip("是否每帧强制 UpdateWorldTransform。一般不需要，除非你发现骨骼位置晚一帧")]
    [SerializeField] private bool forceUpdateWorldTransform = false;

    [Header("Debug")]
    [SerializeField] private bool logRefresh = false;
    [SerializeField] private bool drawGizmos = true;

    private Skeleton cachedHoundSkeleton;
    private Skeleton cachedHunterGunSkeleton;

    private Bone houndSourceBone;
    private Bone hunterGunTargetBone;

    private bool _isInitialized;

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized)
            return;

        characterRoot = root;

        ResolveReferences();
        RefreshBones("Initialize");

        _isInitialized = true;
    }

    private void Awake()
    {
        if (characterRoot == null)
            characterRoot = GetComponentInParent<CharacterRoot>();

        ResolveReferences();
        RefreshBones("Awake");
    }

    private void Start()
    {
        ResolveReferences();
        RefreshBones("Start");
        MatchPosition();
    }

    public void ManagedUpdate(float deltaTime)
    {
        if (!followInLateUpdate)
            MatchPosition();
    }
    public void ManagedLateUpdate(float deltaTime)
    {
        if (followInLateUpdate)
            MatchPosition();
    }


    public void ForceRefresh()
    {
        ResolveReferences();
        RefreshBones("ForceRefresh");
    }

    private void ResolveReferences()
    {
        if (hunterGunVisualRoot == null)
            hunterGunVisualRoot = transform;

        if (characterRoot == null)
            characterRoot = GetComponentInParent<CharacterRoot>();

        if (characterRoot != null)
        {
            if (houndSkeletonAnimation == null)
                houndSkeletonAnimation = characterRoot.houndSkeletonAnimation;

            if (hunterGunSkeletonAnimation == null)
                hunterGunSkeletonAnimation = characterRoot.hunterGunSkeletonAnimation;
        }

        if (houndSkeletonAnimation == null)
        {
            SkeletonAnimation[] skeletons = GetComponentsInParent<CharacterRoot>(true)
                .Length > 0
                ? GetComponentsInParent<CharacterRoot>(true)[0].GetComponentsInChildren<SkeletonAnimation>(true)
                : null;

            if (skeletons != null)
            {
                foreach (SkeletonAnimation skel in skeletons)
                {
                    if (skel != null && skel.gameObject.name.Contains("Hound"))
                    {
                        houndSkeletonAnimation = skel;
                        break;
                    }
                }
            }
        }

        if (hunterGunSkeletonAnimation == null)
            hunterGunSkeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
    }

    private void RefreshBones(string reason)
    {
        cachedHoundSkeleton = houndSkeletonAnimation != null
            ? houndSkeletonAnimation.Skeleton
            : null;

        cachedHunterGunSkeleton = hunterGunSkeletonAnimation != null
            ? hunterGunSkeletonAnimation.Skeleton
            : null;

        houndSourceBone = cachedHoundSkeleton != null
            ? cachedHoundSkeleton.FindBone(houndSourceBoneName)
            : null;

        hunterGunTargetBone = cachedHunterGunSkeleton != null
            ? cachedHunterGunSkeleton.FindBone(hunterGunTargetBoneName)
            : null;

        if (logRefresh)
        {
            Debug.Log(
                $"[HunterGunBonePositionMatcher] RefreshBones: {reason}\n" +
                $"HoundSkeleton={(cachedHoundSkeleton != null ? cachedHoundSkeleton.GetHashCode().ToString() : "NULL")}, " +
                $"HoundBone={(houndSourceBone != null ? houndSourceBone.Data.Name : "NULL")}\n" +
                $"HunterGunSkeleton={(cachedHunterGunSkeleton != null ? cachedHunterGunSkeleton.GetHashCode().ToString() : "NULL")}, " +
                $"HunterGunBone={(hunterGunTargetBone != null ? hunterGunTargetBone.Data.Name : "NULL")}",
                this
            );
        }

        if (houndSourceBone == null && houndSkeletonAnimation != null)
        {
            Debug.LogWarning(
                $"[HunterGunBonePositionMatcher] Hound 中找不到骨骼: {houndSourceBoneName}",
                this
            );
        }

        if (hunterGunTargetBone == null && hunterGunSkeletonAnimation != null)
        {
            Debug.LogWarning(
                $"[HunterGunBonePositionMatcher] HunterGun 中找不到骨骼: {hunterGunTargetBoneName}",
                this
            );
        }
    }

    private void MatchPosition()
    {
        if (houndSkeletonAnimation == null || hunterGunSkeletonAnimation == null)
            return;

        Skeleton houndSkeleton = houndSkeletonAnimation.Skeleton;
        Skeleton hunterGunSkeleton = hunterGunSkeletonAnimation.Skeleton;

        if (houndSkeleton == null || hunterGunSkeleton == null)
            return;

        if (houndSkeleton != cachedHoundSkeleton || hunterGunSkeleton != cachedHunterGunSkeleton)
            RefreshBones("Skeleton Changed");

        if (houndSourceBone == null || hunterGunTargetBone == null)
            return;

        if (forceUpdateWorldTransform)
        {
            houndSkeleton.UpdateWorldTransform(Spine.Physics.Pose);
            hunterGunSkeleton.UpdateWorldTransform(Spine.Physics.Pose);
        }

        Vector3 houndBoneWorldPosition = GetBoneWorldPosition(
            houndSkeletonAnimation,
            houndSourceBone
        );

        Vector3 hunterGunBoneWorldPosition = GetBoneWorldPosition(
            hunterGunSkeletonAnimation,
            hunterGunTargetBone
        );

        Vector3 delta = houndBoneWorldPosition - hunterGunBoneWorldPosition;

        hunterGunVisualRoot.position += delta + worldOffset;

        // 重点：
        // 不同步 rotation
        // 不同步 scale
    }

    private Vector3 GetBoneWorldPosition(SkeletonAnimation skeletonAnimation, Bone bone)
    {
        Vector3 skeletonLocalPosition = new Vector3(bone.AppliedPose.WorldX, bone.AppliedPose.WorldY, 0f);
        return skeletonAnimation.transform.TransformPoint(skeletonLocalPosition);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        if (!Application.isPlaying)
            return;

        if (houndSkeletonAnimation != null && houndSourceBone != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(GetBoneWorldPosition(houndSkeletonAnimation, houndSourceBone), 0.08f);
        }

        if (hunterGunSkeletonAnimation != null && hunterGunTargetBone != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(GetBoneWorldPosition(hunterGunSkeletonAnimation, hunterGunTargetBone), 0.06f);
        }
    }
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/IWeaponSubsystem.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IWeaponSubsystem
{
    void Initialize(WeaponRoot root);
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/CharacterCombatGeometryProvider.cs`

```cs
using System.Collections.Generic;
using UnityEngine;

public class CharacterCombatGeometryProvider : MonoBehaviour
{
    [SerializeField]
    private List<CombatHitAnchor> anchors =
        new List<CombatHitAnchor>();

    private readonly Dictionary<string, CombatHitAnchor>
        anchorMap =
            new Dictionary<string, CombatHitAnchor>();

    private void Awake()
    {
        RebuildCache();
    }

    public void RebuildCache()
    {
        anchorMap.Clear();

        for (int i = 0; i < anchors.Count; i++)
        {
            CombatHitAnchor anchor = anchors[i];

            if (anchor == null ||
                string.IsNullOrWhiteSpace(anchor.AnchorId))
            {
                continue;
            }

            anchorMap[anchor.AnchorId] = anchor;
        }
    }

    public bool TryGetCharacterAnchor(
        string anchorId,
        out CombatHitAnchor anchor)
    {
        if (string.IsNullOrWhiteSpace(anchorId))
        {
            anchor = null;
            return false;
        }

        return anchorMap.TryGetValue(
            anchorId,
            out anchor
        );
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (anchors == null)
        {
            anchors =
                new List<CombatHitAnchor>();
        }
    }
#endif
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/CombatEffectContext.cs`

```cs
using UnityEngine;

public readonly struct CombatEffectContext
{
    /// <summary>
    /// 发起动作的角色。
    /// </summary>
    public readonly CharacterRoot attackerRoot;

    /// <summary>
    /// 发起动作时锁定的武器运行时数据。
    /// </summary>
    public readonly WeaponRuntimeData sourceWeapon;

    /// <summary>
    /// 当前执行的近战动作资产。
    /// </summary>
    public readonly MeleeComboActionSO action;

    /// <summary>
    /// 当前是动作中的第几段。
    /// </summary>
    public readonly int stepIndex;

    /// <summary>
    /// 一次攻击动作的唯一运行时 ID。
    /// 可用于命中去重、日志和后续联机同步。
    /// </summary>
    public readonly int attackInstanceId;

    /// <summary>
    /// 实际被检测到的碰撞体。
    /// </summary>
    public readonly Collider2D hitCollider;

    /// <summary>
    /// 归一化后的目标根对象。
    /// </summary>
    public readonly GameObject target;

    /// <summary>
    /// 目标的伤害接收接口。
    /// 目标不可受伤时可以为空。
    /// </summary>
    public readonly IDamageable damageable;

    /// <summary>
    /// 本次命中的世界坐标。
    /// </summary>
    public readonly Vector2 hitPoint;

    /// <summary>
    /// 本次命中的近似法线。
    /// </summary>
    public readonly Vector2 hitNormal;

    /// <summary>
    /// 攻击者指向攻击方向的归一化向量。
    /// 不应依赖目标位于攻击者哪一边临时反算。
    /// </summary>
    public readonly Vector2 attackDirection;

    /// <summary>
    /// 蓄力、Buff 或特殊派生动作提供的统一强度倍率。
    /// 普通攻击为 1。
    /// </summary>
    public readonly float powerMultiplier;

    public GameObject Attacker
    {
        get
        {
            return attackerRoot != null
                ? attackerRoot.gameObject
                : null;
        }
    }

    public bool HasTarget => target != null;

    public bool CanDamage
    {
        get
        {
            return damageable != null &&
                damageable.CanTakeDamage;
        }
    }

    public CombatEffectContext(
        CharacterRoot attackerRoot,
        WeaponRuntimeData sourceWeapon,
        MeleeComboActionSO action,
        int stepIndex,
        int attackInstanceId,
        Collider2D hitCollider,
        GameObject target,
        IDamageable damageable,
        Vector2 hitPoint,
        Vector2 hitNormal,
        Vector2 attackDirection,
        float powerMultiplier = 1f)
    {
        this.attackerRoot = attackerRoot;
        this.sourceWeapon = sourceWeapon;
        this.action = action;
        this.stepIndex = stepIndex;
        this.attackInstanceId = attackInstanceId;
        this.hitCollider = hitCollider;
        this.target = target;
        this.damageable = damageable;
        this.hitPoint = hitPoint;
        this.hitNormal = hitNormal;

        this.attackDirection =
            attackDirection.sqrMagnitude > 0.0001f
                ? attackDirection.normalized
                : Vector2.right;

        this.powerMultiplier =
            Mathf.Max(0f, powerMultiplier);
    }
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/CombatEffectExecutor.cs`

```cs
//检测 Collider2D
//→ 归一化目标
//→ 构造 CombatEffectContext
//→ CombatEffectExecutor.ApplyEffects()



public static class CombatEffectExecutor
{
    public static void ApplyEffects( MeleeHitProfileSO profile,in CombatEffectContext context)
    {
        if (profile == null || profile.effects == null)
        {
            return;
        }

        for (int i = 0;i < profile.effects.Count;i++)
        {
            CombatEffectSO effect = profile.effects[i];

            if (effect == null)
                continue;

            if (!effect.CanApply(context))
                continue;

            effect.Apply(context);
        }
    }
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/CombatEffectSO.cs`

```cs
using System;
using UnityEngine;


[Flags]
public enum CombatEffectTargetType
{
    None = 0,

    Damageable = 1 << 0,
    Character = 1 << 1,
    PhysicsObject = 1 << 2,
    Breakable = 1 << 3,

    Any = ~0
}

public abstract class CombatEffectSO : ScriptableObject
{
    [Header("适用目标")]
    public CombatEffectTargetType targetTypes = CombatEffectTargetType.Any;

    /// <summary>
    /// 效果执行器自己判断目标是否具有所需能力。
    /// </summary>
    public abstract bool CanApply(
        in CombatEffectContext context);

    public abstract void Apply(
        in CombatEffectContext context);
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/CombatHitAnchor.cs`

```cs
using UnityEngine;

public class CombatHitAnchor : MonoBehaviour
{
    [SerializeField]
    private string anchorId;

    public string AnchorId => anchorId;

    public Vector2 WorldPosition =>
        transform.position;
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/CombatTargetResolver.cs`

```cs
using UnityEngine;
public static class CombatTargetResolver
{
    public static GameObject ResolveTarget(
        Collider2D collider,
        out IDamageable damageable)
    {
        damageable = null;

        if (collider == null)
            return null;

        damageable =
            collider.GetComponentInParent<IDamageable>();

        if (damageable is Component component)
        {
            return component.gameObject;
        }

        if (collider.attachedRigidbody != null)
        {
            return collider.attachedRigidbody.gameObject;
        }

        return collider.transform.root.gameObject;
    }
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/DamageEffectSO.cs`

```cs
using UnityEngine;

[CreateAssetMenu(
    fileName = "DamageEffect",
    menuName = "ScriptableObject/ս��Ч��/�˺�")]
public class DamageEffectSO : CombatEffectSO
{
    [Header("�˺�����")]
    public CombatDamageValueMode valueMode =
        CombatDamageValueMode.WeaponCoreMultiplier;

    [Min(0f)]
    public float flatDamage;

    [Min(0f)]
    public float weaponDamageMultiplier = 1f;

    [Header("受击反应类型")]
    public CombatImpactType impactType = CombatImpactType.Unknown;

    public override bool CanApply(
        in CombatEffectContext context)
    {
        return context.CanDamage;
    }

    public override void Apply(
        in CombatEffectContext context)
    {
        if (!CanApply(context))
            return;

        float weaponDamage = 0f;

        if (context.sourceWeapon != null &&
            context.sourceWeapon.CommonStats != null)
        {
            weaponDamage =
                context.sourceWeapon.CommonStats.damage;
        }

        float damage;

        switch (valueMode)
        {
            case CombatDamageValueMode.Flat:
                damage = flatDamage;
                break;

            case CombatDamageValueMode
                .WeaponCoreMultiplier:
                damage =
                    weaponDamage *
                    weaponDamageMultiplier;
                break;

            case CombatDamageValueMode
                .FlatPlusWeaponCoreMultiplier:
                damage =
                    flatDamage +
                    weaponDamage *
                    weaponDamageMultiplier;
                break;

            default:
                damage = 0f;
                break;
        }

        damage *= context.powerMultiplier;

        if (damage <= 0f)
            return;

        DamageInfo damageInfo = new DamageInfo
        {
            attacker = context.Attacker,
            damage = damage,
            hitPoint = context.hitPoint,
            hitNormal = context.hitNormal,
            shotDirection = context.attackDirection,
            impactType = impactType
        };

        context.damageable.TakeDamage(damageInfo);
    }
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/MeleeAttackStep.cs`

```cs
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MeleeAttackStep
{
    [Header("动画语义")]
    public EAnimSemantic semantic =
        EAnimSemantic.Attack_Normal_1;

    [Header("动画结束保底")]
    [Min(0.05f)]
    public float fallbackDuration = 0.5f;

    [Header("连段")]
    public bool allowInputBuffer = true;

    [Min(0f)]
    public float fallbackComboWindowDuration = 0.35f;

    [Header("事件名称")]
    public string hitOpenEvent = "HitOpen";
    public string hitCloseEvent = "HitClose";
    public string comboOpenEvent = "ComboOpen";
    public string comboCloseEvent = "ComboClose";
    public string cancelOpenEvent = "CancelOpen";
    public string cancelCloseEvent = "CancelClose";
    public string actionEndEvent = "ActionEnd";

    [Header("命中窗口")]
    public List<MeleeHitWindowDefinition> hitWindows = new List<MeleeHitWindowDefinition>();

    [Header("表现配置")]
    public AudioEventSO swingAudio;

    [Min(0f)]
    public float hitStopDuration;

    public void Validate()
    {
        fallbackDuration =
             Mathf.Max(0.05f, fallbackDuration);

        fallbackComboWindowDuration =
            Mathf.Max(
                0f,
                fallbackComboWindowDuration
            );

        hitStopDuration =
            Mathf.Max(0f, hitStopDuration);

        if (hitWindows == null)
        {
            hitWindows =
                new List<MeleeHitWindowDefinition>();
        }
    }
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/MeleeComboActionSO.cs`

```cs
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu( fileName = "MeleeComboAction",menuName = "ScriptableObject/战斗动作/近战连击")]
public class MeleeComboActionSO : ScriptableObject
{
    [Header("识别")]
    public string actionId;

    [Header("攻击段")]
    public List<MeleeAttackStep> steps = new List<MeleeAttackStep>();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (steps == null)
        {
            steps = new List<MeleeAttackStep>();
        }

        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] != null)
            {
                steps[i].Validate();
            }
        }
    }
#endif
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/MeleeHitbox.cs`

```cs
using UnityEngine;

public class MeleeHitbox : MonoBehaviour
{
    [Header("识别")]
    [SerializeField] private string hitboxId;

    [Header("判定形状")]
    [SerializeField] private Collider2D hitCollider;

    public string HitboxId => hitboxId;
    public Collider2D HitCollider => hitCollider;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (hitCollider == null)
        {
            hitCollider =
                GetComponent<Collider2D>();
        }

        if (hitCollider != null)
        {
            hitCollider.isTrigger = true;
        }
    }
#endif
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/MeleeHitController.cs`

```cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 近战命中检测控制器。
/// 订阅 CharacterSpineEventSystem 的 Spine 事件，
/// 在 HitOpen/HitClose 窗口期间使用 OverlapCircle 检测敌人，
/// 命中后通过 CombatEffectExecutor 执行伤害效果。
/// </summary>
[RequireComponent(typeof(CharacterSpineEventSystem))]
[RequireComponent(typeof(CharacterCombatGeometryProvider))]
public class MeleeHitController : MonoBehaviour
{
    [Header("命中检测配置")]
    [SerializeField] private float hitCheckRadius = 0.5f;
    [SerializeField] private string kickAnchorId = "KickFoot";

    [Header("命中效果")]
    [SerializeField] private MeleeHitProfileSO kickHitProfile;

    private CharacterSpineEventSystem _spineEventSystem;
    private CharacterCombatGeometryProvider _geometryProvider;
    private CharacterRoot _characterRoot;

    private bool _isHitWindowOpen;
    private readonly List<Collider2D> _hitTargets = new();
    private readonly List<Collider2D> _resultsBuffer = new();

    private void Awake()
    {
        _spineEventSystem = GetComponent<CharacterSpineEventSystem>();
        _geometryProvider = GetComponent<CharacterCombatGeometryProvider>();
        _characterRoot = GetComponent<CharacterRoot>();
    }

    private void OnEnable()
    {
        if (_spineEventSystem != null)
            _spineEventSystem.OnSpineSignal += HandleSpineSignal;
    }

    private void OnDisable()
    {
        if (_spineEventSystem != null)
            _spineEventSystem.OnSpineSignal -= HandleSpineSignal;
        _isHitWindowOpen = false;
    }

    private void HandleSpineSignal(CharacterSpineAnimationSignal signal)
    {
        if (signal.type != CharacterSpineSignalType.EventReached)
            return;

        if (signal.eventName == "HitOpen")
        {
            _isHitWindowOpen = true;
            _hitTargets.Clear();
            Debug.Log($"[MeleeHit] HitOpen | anim={signal.animationName}");
        }
        else if (signal.eventName == "HitClose")
        {
            _isHitWindowOpen = false;
            Debug.Log($"[MeleeHit] HitClose | hits={_hitTargets.Count}");
        }
    }

    private void FixedUpdate()
    {
        if (!_isHitWindowOpen)
            return;

        if (kickHitProfile == null || _characterRoot == null)
            return;

        if (!_geometryProvider.TryGetCharacterAnchor(kickAnchorId, out var anchor))
            return;

        Vector2 center = anchor.WorldPosition;

        int count = Physics2D.OverlapCircle(
            center,
            hitCheckRadius,
            new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = kickHitProfile.targetMask,
                useTriggers = false
            },
            _resultsBuffer
        );

        for (int i = 0; i < count; i++)
        {
            Collider2D col = _resultsBuffer[i];
            if (col == null || _hitTargets.Contains(col))
                continue;

            if (!CombatTargetResolver.ResolveTarget(col, out IDamageable damageable))
                continue;

            if (!damageable.CanTakeDamage)
                continue;

            _hitTargets.Add(col);

            Vector2 hitPoint = col.ClosestPoint(center);

            // 攻击方向 = 从角色根位置指向敌人根位置
            // 不依赖碰撞体表面点，避免近距离时锚点在碰撞体内部导致方向错误
            Vector2 enemyPos = col.transform.position;
            Vector2 charPos = _characterRoot.transform.position;
            Vector2 attackDirection = (enemyPos - charPos).normalized;
            if (attackDirection.sqrMagnitude < 0.001f)
            {
                attackDirection = (Vector2)_characterRoot.transform.right;
            }

            Vector2 hitNormal = (center - hitPoint).normalized;
            if (hitNormal.sqrMagnitude < 0.001f)
                hitNormal = -attackDirection;

            var context = new CombatEffectContext(
                _characterRoot,
                null,
                null,
                0,
                0,
                col,
                col.gameObject,
                damageable,
                hitPoint,
                hitNormal,
                attackDirection
            );

            CombatEffectExecutor.ApplyEffects(kickHitProfile, context);

            Debug.Log($"[MeleeHit] Hit {col.gameObject.name} at {hitPoint}", col.gameObject);
        }
    }
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/MeleeHitProfileSO.cs`

```cs
using System.Collections.Generic;
using UnityEngine;



public enum MeleeHitShapeType
{
    Box,
    Circle
}




[CreateAssetMenu( fileName = "MeleeHitProfile", menuName = "ScriptableObject/战斗动作/近战命中")]
public class MeleeHitProfileSO : ScriptableObject
{
    public LayerMask targetMask;
    public bool allowRepeatedHit;
    [Min(1)] public int maxTargets = 8;
    public List<CombatEffectSO> effects = new List<CombatEffectSO>();

#if UNITY_EDITOR
    private void OnValidate()
    {

        maxTargets = Mathf.Max(1, maxTargets);

        if (effects == null)
        {
            effects = new List<CombatEffectSO>();
        }
    }
#endif
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/MeleeSystem/MeleeHitWindowDefinition.cs`

```cs
using UnityEngine;

public enum MeleeHitGeometrySource
{
    CharacterAnchor,
    WeaponBoundingBox
}

[System.Serializable]
public class MeleeHitWindowDefinition
{
    [Header("窗口")]
    public string windowId = "Primary";

    [Header("判定来源")]
    public MeleeHitGeometrySource geometrySource = MeleeHitGeometrySource.CharacterAnchor;

    [Header("角色肢体攻击")]
    public string characterAnchorId = "KickFoot";

    [Min(0.01f)]
    public float anchorRadius = 0.35f;

    [Header("武器近战攻击")]
    public string weaponBoundingBoxId;

    [Header("Spine 事件")]
    public string openEvent = "MeleeHitOpen";
    public string closeEvent = "MeleeHitClose";

    [Header("命中配置")]
    public MeleeHitProfileSO hitProfile;
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/WeaponActionProfileSO.cs`

```cs
using UnityEngine;

public abstract class WeaponActionProfileSO : ScriptableObject
{
    public abstract WeaponType WeaponType { get; }
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/WeaponConfigSO.cs`

```cs
using UnityEngine;


//WeaponConfigSO
//  stats = WeaponStatsSO
//  weaponActionProfile = FirearmActionProfileSO
//  weaponPrefab = WeaponRoot


//WeaponRuntimeData(config)
//  ->Stats
//  ->FirearmStats
//  ->ActionProfile
//  ->FirearmActionProfile


//GunSystemController.ApplyWeaponRuntimeData(runtimeData)
//  ->读取 damage / range / magazine / fireRate
//  ->读取 FirearmActionProfileSO
//  ->读取 defaultFireMode
//  ->读取 GunFireModeConfig
//  ->决定点射 / 连发 / 自动



[CreateAssetMenu(fileName = "WeaponConfig", menuName = "ScriptableObject/武器/武器总配置")]
public class WeaponConfigSO : ScriptableObject
{
    [Header("武器名字")]
    public string weaponName;

    [Header("武器类型")]
    public WeaponType weaponType = WeaponType.Firearm;

    [Header("武器默认朝向")]
    [Tooltip("武器 prefab 在未旋转、未镜像状态下，枪口默认朝向。大部分枪通常是 Right。")]
    public WeaponDefaultFacing defaultFacing = WeaponDefaultFacing.Right;

    [Header("武器数值")]
    public WeaponStatsSO stats;

    [Header("武器行为")]
    public WeaponActionProfileSO weaponActionProfile;
    [Header("武器音频")]
    public WeaponAudioProfileSO audioProfile;
    [Header("武器动画覆写")]
    public AnimationPlanOverrideRuleSetSO animationOverride;

    [Header("武器预制体")]
    public WeaponRoot weaponPrefab;

    public bool IsFirearm => weaponType == WeaponType.Firearm;
    public bool IsMelee => weaponType == WeaponType.Melee;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (stats != null)
        {
            weaponType = stats.weaponType;
        }

        if (weaponActionProfile != null &&
            weaponActionProfile.WeaponType != weaponType)
        {
            Debug.LogWarning(
                $"[WeaponConfigSO] {name} 的武器类型是 {weaponType}，但行为配置是 {weaponActionProfile.WeaponType}",
                this
            );
        }

        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/WeaponDataBus.cs`

```cs
public class WeaponDataBus
{
    public WeaponRoot weaponRoot;
    public CharacterRoot owner;

    public WeaponConfigSO config;
    public WeaponStatsSO weaponStats;
    //public WeaponCombatProfileSO weaponCombatProfile;
    public WeaponActionProfileSO weaponActionProfile;
    public FirearmActionProfileSO firearmActionProfile;


    public AnimationPlanOverrideRuleSetSO weaponAnimationOverride;

    public bool isEquipped;
    public bool isAttacking;

    public void LoadFromSO(WeaponConfigSO config)
    {
        this.config = config;

        if (config == null)
            return;

        weaponStats = config.stats;
        //weaponCombatProfile = config.weaponProfile;

        weaponActionProfile = config.weaponActionProfile;
        firearmActionProfile = weaponActionProfile as FirearmActionProfileSO;

        weaponAnimationOverride = config.animationOverride;
    }
}
```

### `Assets/Script/Character/Core/Systems/WeaponSystem/WeaponRoot.cs`

```cs
using Spine;
using Spine.Unity;
using UnityEngine;

public class WeaponRoot : MonoBehaviour
{
    // =========================================================
    // 基础配置
    // =========================================================

    [Header("配置")]
    [SerializeField] private WeaponConfigSO config;

    [Header("表现根节点")]
    [SerializeField] private Transform visualRoot;

    [Header("武器 Spine")]
    [SerializeField] private SkeletonAnimation weaponSkeletonAnimation;

    // =========================================================
    // Debug
    // =========================================================

    [Header("Debug")]
    [SerializeField] private bool debugCacheBones;
    [SerializeField] private bool debugGripWorldPosition;
    [SerializeField] private float debugLogInterval = 0.5f;

    private float nextDebugLogTime;

    // =========================================================
    // 弹夹 / 换弹相关骨骼名字
    // =========================================================

    [Header("弹夹骨骼名字")]
    [SerializeField] private string magazineRootBoneName = "MagazineRoot";
    [SerializeField] private string magazineMouthBoneName = "MagazineMouth";
    [SerializeField] private string magazineGripPointBoneName = "MagazineGripPoint";

    // =========================================================
    // 枪械握点骨骼名字
    // =========================================================

    [Header("握点骨骼名字")]
    [SerializeField] private string gunBodyGripPointBoneName = "GunBody_GripPoint";
    [SerializeField] private string frontgrip_GripPointBoneName = "Front grip_GripPoint";
    [SerializeField] private string noFrontgrip_GripPointBoneName = "NoFront grip_GripPoint";

    // =========================================================
    // Runtime Data
    // =========================================================

    private WeaponDataBus weaponDataBus;
    private CharacterRoot ownerRoot;
    private bool isInitialized;

    // =========================================================
    // Runtime Cached Bones
    // =========================================================

    private Bone magazineRoot;
    private Bone magazineMouth;
    private Bone magazineGripPoint;

    private Bone gunBodyGripPoint;
    private Bone frontgrip_GripPointBone;
    private Bone noFrontgrip_GripPointBone;

    private bool cached;

    // =========================================================
    // Public Properties
    // =========================================================

    public WeaponConfigSO Config => config;
    public WeaponDataBus WeaponDataBus => weaponDataBus;
    public CharacterRoot OwnerRoot => ownerRoot;
    public Transform VisualRoot => visualRoot;
    public SkeletonAnimation WeaponSkeletonAnimation => weaponSkeletonAnimation;
    public bool IsInitialized => isInitialized;

    public Bone MagazineRootBone => magazineRoot;
    public Bone MagazineMouthBone => magazineMouth;
    public Bone MagazineGripPointBone => magazineGripPoint;

    public Bone GunBodyGripPointBone => gunBodyGripPoint;
    public Bone FrontgripGripPointBone => frontgrip_GripPointBone;
    public Bone NoFrontgripGripPointBone => noFrontgrip_GripPointBone;

    public WeaponActionProfileSO ActionProfile
    {
        get
        {
            if (config == null)
                return null;

            return config.weaponActionProfile;
        }
    }

    public FirearmActionProfileSO FirearmActionProfile
    {
        get
        {
            return ActionProfile as FirearmActionProfileSO;
        }
    }

    //public MeleeActionProfileSO MeleeActionProfile
    //{
    //    get
    //    {
    //        return ActionProfile as MeleeActionProfileSO;
    //    }
    //}

    public WeaponType WeaponType
    {
        get
        {
            if (ActionProfile != null)
                return ActionProfile.WeaponType;

            if (config != null)
                return config.weaponType;

            return WeaponType.Melee;
        }
    }

    public bool IsFirearm => WeaponType == WeaponType.Firearm;
    public bool IsMelee => WeaponType == WeaponType.Melee;

    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Reset()
    {
        AutoGetReferences();
    }

    private void Awake()
    {
        AutoGetReferences();
        CacheBones();
    }

    private void OnEnable()
    {
        CacheBones();
    }

    // =========================================================
    // Initialize
    // =========================================================

    public void Initialize(WeaponConfigSO weaponConfig, CharacterRoot owner)
    {
        if (isInitialized)
            return;

        AutoGetReferences();

        config = weaponConfig;
        ownerRoot = owner;

        weaponDataBus = new WeaponDataBus();
        weaponDataBus.LoadFromSO(config);
        weaponDataBus.owner = ownerRoot;
        weaponDataBus.weaponRoot = this;
        weaponDataBus.isEquipped = ownerRoot != null;

        InitializeSubsystems();

        isInitialized = true;
    }

    private void InitializeSubsystems()
    {
        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IWeaponSubsystem subsystem)
                subsystem.Initialize(this);
        }
    }

    public bool TryGetWeaponSubsystem<T>(out T subsystem) where T : class, IWeaponSubsystem
    {
        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is T typed)
            {
                subsystem = typed;
                return true;
            }
        }

        subsystem = null;
        return false;
    }

    // =========================================================
    // Reference
    // =========================================================

    private void AutoGetReferences()
    {
        if (visualRoot == null)
        {
            Transform found = transform.Find("VirtualRoot");

            if (found != null)
                visualRoot = found;
        }

        if (weaponSkeletonAnimation == null)
        {
            if (visualRoot != null)
                weaponSkeletonAnimation = visualRoot.GetComponentInChildren<SkeletonAnimation>(true);
            else
                weaponSkeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
        }
    }

    public void BindSkeletonAnimation(SkeletonAnimation skeletonAnimation)
    {
        weaponSkeletonAnimation = skeletonAnimation;
        CacheBones();
    }

    // =========================================================
    // Cache Spine Bones
    // =========================================================

    public void CacheBones()
    {
        cached = false;

        AutoGetReferences();
        ClearCachedBones();

        if (weaponSkeletonAnimation == null)
        {
            Debug.LogWarning($"[WeaponRoot] Missing weapon SkeletonAnimation on {name}.", this);
            return;
        }

        if (weaponSkeletonAnimation.Skeleton == null)
        {
            Debug.LogWarning($"[WeaponRoot] Weapon Skeleton is null on {name}.", this);
            return;
        }

        Skeleton skeleton = weaponSkeletonAnimation.Skeleton;

        magazineRoot = FindBoneSafe(skeleton, magazineRootBoneName);
        magazineMouth = FindBoneSafe(skeleton, magazineMouthBoneName);
        magazineGripPoint = FindBoneSafe(skeleton, magazineGripPointBoneName);

        gunBodyGripPoint = FindBoneSafe(skeleton, gunBodyGripPointBoneName);
        frontgrip_GripPointBone = FindBoneSafe(skeleton, frontgrip_GripPointBoneName);
        noFrontgrip_GripPointBone = FindBoneSafe(skeleton, noFrontgrip_GripPointBoneName);

        cached = true;

        if (debugCacheBones)
            DebugLogCacheResult();
    }

    private void ClearCachedBones()
    {
        magazineRoot = null;
        magazineMouth = null;
        magazineGripPoint = null;

        gunBodyGripPoint = null;
        frontgrip_GripPointBone = null;
        noFrontgrip_GripPointBone = null;
    }

    private Bone FindBoneSafe(Skeleton skeleton, string boneName)
    {
        if (skeleton == null)
            return null;

        if (string.IsNullOrEmpty(boneName))
            return null;

        Bone bone = skeleton.FindBone(boneName);

        if (debugCacheBones && bone == null)
        {
            Debug.LogWarning(
                $"[WeaponRoot] Bone not found. Weapon={name}, BoneName={boneName}",
                this
            );
        }

        return bone;
    }

    private bool EnsureCached()
    {
        if (cached)
            return true;

        CacheBones();

        return cached;
    }

    // =========================================================
    // Right Hand Grip
    // =========================================================

    /// <summary>
    /// 获取右手握点世界坐标。
    /// 右手默认抓 GunBody_GripPoint。
    /// </summary>
    public bool TryGetRightHandGripWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = default;

        if (!EnsureCached())
            return false;

        bool success = TryGetBoneWorldPosition(
            gunBodyGripPoint,
            out worldPosition
        );

        DebugLogGripPositionThrottled(
            "RightHandGrip",
            gunBodyGripPointBoneName,
            success,
            worldPosition
        );

        return success;
    }

    // =========================================================
    // Left Hand Grip
    // =========================================================

    /// <summary>
    /// 获取左手握点世界坐标。
    /// 优先级：
    /// 1. Front grip_GripPoint
    /// 2. NoFront grip_GripPoint
    /// </summary>
    public bool TryGetLeftHandGripWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = default;

        if (!EnsureCached())
            return false;

        if (TryGetBoneWorldPosition(frontgrip_GripPointBone, out worldPosition))
        {
            DebugLogGripPositionThrottled(
                "LeftHandGrip",
                frontgrip_GripPointBoneName,
                true,
                worldPosition
            );

            return true;
        }

        if (TryGetBoneWorldPosition(noFrontgrip_GripPointBone, out worldPosition))
        {
            DebugLogGripPositionThrottled(
                "LeftHandGrip",
                noFrontgrip_GripPointBoneName,
                true,
                worldPosition
            );

            return true;
        }

        DebugLogGripPositionThrottled(
            "LeftHandGrip",
            $"{frontgrip_GripPointBoneName} / {noFrontgrip_GripPointBoneName}",
            false,
            worldPosition
        );

        return false;
    }

    public bool TryGetFrontGripWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = default;

        if (!EnsureCached())
            return false;

        return TryGetBoneWorldPosition(
            frontgrip_GripPointBone,
            out worldPosition
        );
    }

    public bool TryGetNoFrontGripWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = default;

        if (!EnsureCached())
            return false;

        return TryGetBoneWorldPosition(
            noFrontgrip_GripPointBone,
            out worldPosition
        );
    }

    // =========================================================
    // Magazine Points
    // =========================================================

    public bool TryGetMagazineRootWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = default;

        if (!EnsureCached())
            return false;

        return TryGetBoneWorldPosition(
            magazineRoot,
            out worldPosition
        );
    }

    public bool TryGetMagazineMouthWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = default;

        if (!EnsureCached())
            return false;

        return TryGetBoneWorldPosition(
            magazineMouth,
            out worldPosition
        );
    }

    public bool TryGetMagazineGripPointWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = default;

        if (!EnsureCached())
            return false;

        return TryGetBoneWorldPosition(
            magazineGripPoint,
            out worldPosition
        );
    }

    // =========================================================
    // Bone Position Convert
    // =========================================================

    /// <summary>
    /// 把武器 Spine Bone 的世界坐标转换成 Unity 世界坐标。
    /// 注意：
    /// Bone 的 WorldX / WorldY 是 SkeletonAnimation 本地空间下的世界骨骼坐标。
    /// 最后需要通过 weaponSkeletonAnimation.transform.TransformPoint 转到 Unity World。
    /// </summary>
    private bool TryGetBoneWorldPosition(Bone bone, out Vector3 worldPosition)
    {
        worldPosition = default;

        if (weaponSkeletonAnimation == null)
            return false;

        if (bone == null)
            return false;

        Vector3 skeletonLocalPosition = new Vector3(
            bone.AppliedPose.WorldX,
            bone.AppliedPose.WorldY,
            0f
        );

        worldPosition = weaponSkeletonAnimation.transform.TransformPoint(
            skeletonLocalPosition
        );

        return true;
    }

    // =========================================================
    // Debug
    // =========================================================

    [ContextMenu("Debug Cache Weapon Bones")]
    private void DebugCacheWeaponBones()
    {
        CacheBones();
        DebugLogCacheResult();
    }

    [ContextMenu("Debug Grip World Positions")]
    private void DebugGripWorldPositions()
    {
        Vector3 right;
        Vector3 left;
        Vector3 front;
        Vector3 noFront;
        Vector3 magRoot;
        Vector3 magMouth;
        Vector3 magGrip;

        bool hasRight = TryGetRightHandGripWorldPosition(out right);
        bool hasLeft = TryGetLeftHandGripWorldPosition(out left);
        bool hasFront = TryGetFrontGripWorldPosition(out front);
        bool hasNoFront = TryGetNoFrontGripWorldPosition(out noFront);
        bool hasMagRoot = TryGetMagazineRootWorldPosition(out magRoot);
        bool hasMagMouth = TryGetMagazineMouthWorldPosition(out magMouth);
        bool hasMagGrip = TryGetMagazineGripPointWorldPosition(out magGrip);

        Debug.Log(
            $"[WeaponRoot] Grip World Positions on {name}\n" +
            $"RightHandGrip / {gunBodyGripPointBoneName}: {hasRight}, Pos={FormatVector3(right)}\n" +
            $"LeftHandGrip Auto: {hasLeft}, Pos={FormatVector3(left)}\n" +
            $"FrontGrip / {frontgrip_GripPointBoneName}: {hasFront}, Pos={FormatVector3(front)}\n" +
            $"NoFrontGrip / {noFrontgrip_GripPointBoneName}: {hasNoFront}, Pos={FormatVector3(noFront)}\n" +
            $"MagazineRoot / {magazineRootBoneName}: {hasMagRoot}, Pos={FormatVector3(magRoot)}\n" +
            $"MagazineMouth / {magazineMouthBoneName}: {hasMagMouth}, Pos={FormatVector3(magMouth)}\n" +
            $"MagazineGripPoint / {magazineGripPointBoneName}: {hasMagGrip}, Pos={FormatVector3(magGrip)}",
            this
        );
    }

    private void DebugLogCacheResult()
    {
        Debug.Log(
            $"[WeaponRoot] Bone Cache Result on {name}\n" +
            $"WeaponSkeletonAnimation: {weaponSkeletonAnimation != null}\n" +
            $"MagazineRoot [{magazineRootBoneName}]: {magazineRoot != null}\n" +
            $"MagazineMouth [{magazineMouthBoneName}]: {magazineMouth != null}\n" +
            $"MagazineGripPoint [{magazineGripPointBoneName}]: {magazineGripPoint != null}\n" +
            $"GunBodyGripPoint [{gunBodyGripPointBoneName}]: {gunBodyGripPoint != null}\n" +
            $"FrontgripGripPoint [{frontgrip_GripPointBoneName}]: {frontgrip_GripPointBone != null}\n" +
            $"NoFrontgripGripPoint [{noFrontgrip_GripPointBoneName}]: {noFrontgrip_GripPointBone != null}",
            this
        );
    }

    private void DebugLogGripPositionThrottled(
        string label,
        string boneName,
        bool success,
        Vector3 worldPosition)
    {
        if (!debugGripWorldPosition)
            return;

        if (Time.time < nextDebugLogTime)
            return;

        nextDebugLogTime = Time.time + debugLogInterval;

        Debug.Log(
            $"[WeaponRoot] Grip Position | Weapon={name} | Label={label} | Bone={boneName} | Success={success} | World={FormatVector3(worldPosition)}",
            this
        );
    }

    private string FormatVector3(Vector3 value)
    {
        return $"({value.x:F3}, {value.y:F3}, {value.z:F3})";
    }
}
```
