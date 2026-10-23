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
    private AimTrackingMode currentTrackingMode = AimTrackingMode.FollowCursor;
    private Vector2 heldAimDirection = Vector2.right;
    private float heldAimDistance;
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

        Vector2 originWorld = GetAimOriginWorldPosition();
        AimTrackingMode trackingMode = ResolveAimTrackingMode();
        HandleTrackingModeTransition(trackingMode, originWorld);

        if (!ShouldWriteProgrammaticTarget(trackingMode))
            return;

        if (trackingMode == AimTrackingMode.FollowCursor && cursorTransform == null)
            return;

        Vector2 rawCursorWorld = cursorTransform != null
            ? (Vector2)cursorTransform.position
            : originWorld;

        Vector2 targetSafeAimWorld = trackingMode == AimTrackingMode.Hold
            ? CalculateHeldAimWorldPosition(originWorld, heldAimDirection, heldAimDistance)
            : CalculateSafeAimWorldPosition(rawCursorWorld, originWorld);

        if (trackingMode == AimTrackingMode.Hold)
        {
            currentSafeAimWorld = targetSafeAimWorld;
            hasCurrentSafeAim = true;
        }
        else if (!hasCurrentSafeAim)
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

    public static bool ShouldWriteProgrammaticTarget(AimTrackingMode mode)
    {
        return mode != AimTrackingMode.AnimationDriven;
    }

    public static Vector2 CalculateHeldAimWorldPosition(
        Vector2 originWorld,
        Vector2 direction,
        float distance)
    {
        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector2.right;
        return originWorld + safeDirection * Mathf.Max(0f, distance);
    }

    private AimTrackingMode ResolveAimTrackingMode()
    {
        CharacterControlAuthority authority = characterRoot != null
            ? characterRoot.controlAuthority
            : null;
        if (authority == null)
            return AimTrackingMode.FollowCursor;

        return authority.IsAllowed(CharacterControlChannel.AimTracking)
            ? authority.ResolveAimTrackingMode(AimTrackingMode.FollowCursor)
            : authority.ResolveAimTrackingMode(AimTrackingMode.AnimationDriven);
    }

    private void HandleTrackingModeTransition(
        AimTrackingMode nextMode,
        Vector2 originWorld)
    {
        if (nextMode == currentTrackingMode)
            return;

        if (nextMode == AimTrackingMode.Hold)
        {
            Vector2 delta = hasCurrentSafeAim
                ? currentSafeAimWorld - originWorld
                : lastValidAimDirection * minAimDistance;
            heldAimDirection = delta.sqrMagnitude > 0.0001f
                ? delta.normalized
                : GetInitialAimDirection();
            heldAimDistance = Mathf.Clamp(
                delta.magnitude,
                minAimDistance,
                maxAimDistance);
        }
        else if (nextMode == AimTrackingMode.FollowCursor)
        {
            hasCurrentSafeAim = false;
        }

        currentTrackingMode = nextMode;
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
