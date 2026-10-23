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