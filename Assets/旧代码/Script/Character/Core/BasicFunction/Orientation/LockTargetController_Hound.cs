using Spine;
using Spine.Unity;
using UnityEngine;

public class LockTargetIKController : MonoBehaviour, ICharacterSubsystem,ICharacterLateTickable
{
    [Header("Runtime References")]
    [SerializeField] private CharacterRoot characterRoot;
    [SerializeField] private SkeletonAnimation skeletonAnimation;

    [Header("Spine Bone")]
    [Tooltip("Spine 中锁定目标用的 IK 骨骼名称。必须和 Spine 里完全一致")]
    [SerializeField] private string ikAimBoneName_Hund = "IK_Aim_Hound";

    [Header("Target Offset")]
    [Tooltip("世界坐标偏移。用于微调 IK_Hund 对准 LockPoint 后的位置")]
    [SerializeField] private Vector2 worldOffset = Vector2.zero;

    [Tooltip("Skeleton 坐标偏移。用于修正 Spine 骨骼坐标")]
    [SerializeField] private Vector2 skeletonSpaceOffset = Vector2.zero;

    [Header("Smoothing")]
    [SerializeField] private bool enableSmoothing = true;

    [Tooltip("IK_Hund 追随 LockPoint 的速度")]
    [SerializeField] private float followSpeed = 30f;

    [Header("When No Target")]
    [Tooltip("没有锁定目标时，是否保持上一帧 IK_Hund 位置")]
    [SerializeField] private bool holdLastPositionWhenNoTarget = false;

    [Tooltip("没有锁定目标且不保持时，IK_Hund 是否回到角色自身位置")]
    [SerializeField] private bool resetToCharacterPositionWhenNoTarget = false;

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmos = true;
    [SerializeField] private bool logRefresh = false;

    private CharacterDataBus dataBus;
    private Skeleton cachedSkeleton;
    private Bone ikHundBone;

    private Vector2 currentIKWorldPosition;
    private bool hasCurrentIKWorldPosition;
    private bool _isInitialized;

    public bool HasValidLockTarget
    {
        get
        {
            if (dataBus == null)
                return false;

            if (!dataBus.hasAttackTarget)
                return false;

            if (dataBus.lockedAttackTarget == null)
                return false;

            if (!dataBus.lockedAttackTarget.IsValid())
                return false;

            return true;
        }
    }

    public Vector3 CurrentIKWorldPosition => currentIKWorldPosition;

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized)
            return;

        characterRoot = root;

        if (characterRoot != null)
        {
            dataBus = characterRoot.characterDataBus;

            if (skeletonAnimation == null)
                skeletonAnimation = characterRoot.houndSkeletonAnimation;
        }

        if (skeletonAnimation == null)
            skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);

        RefreshSpineReferences("Initialize");

        _isInitialized = true;
    }

    private void Awake()
    {
        if (characterRoot == null)
            characterRoot = GetComponentInParent<CharacterRoot>();

        if (characterRoot != null)
        {
            dataBus = characterRoot.characterDataBus;

            if (skeletonAnimation == null)
                skeletonAnimation = characterRoot.houndSkeletonAnimation;
        }

        if (skeletonAnimation == null)
            skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);

        RefreshSpineReferences("Awake");
    }

    private void Start()
    {
        RefreshSpineReferences("Start");
    }

    public void ManagedLateUpdate(float deltaTime)
{
        ApplyLockTargetIK();
    }


    public void ForceRefresh()
    {
        RefreshSpineReferences("ForceRefresh");
    }

    private void ApplyLockTargetIK()
    {
        if (characterRoot != null && dataBus == null)
            dataBus = characterRoot.characterDataBus;

        if (skeletonAnimation == null)
            return;

        Skeleton currentSkeleton = skeletonAnimation.Skeleton;

        if (currentSkeleton == null)
            return;

        if (cachedSkeleton != currentSkeleton)
        {
            RefreshSpineReferences("Skeleton changed");
        }

        if (ikHundBone == null)
            return;

        Vector2 targetWorldPosition;
        bool hasTarget = TryGetLockTargetWorldPosition(out targetWorldPosition);

        if (!hasTarget)
        {
            if (holdLastPositionWhenNoTarget && hasCurrentIKWorldPosition)
                targetWorldPosition = currentIKWorldPosition;
            else if (resetToCharacterPositionWhenNoTarget)
                targetWorldPosition = transform.position;
            else
                return;
        }

        if (!hasCurrentIKWorldPosition)
        {
            currentIKWorldPosition = targetWorldPosition;
            hasCurrentIKWorldPosition = true;
        }
        else
        {
            if (enableSmoothing)
            {
                float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
                currentIKWorldPosition = Vector2.Lerp(currentIKWorldPosition, targetWorldPosition, t);
            }
            else
            {
                currentIKWorldPosition = targetWorldPosition;
            }
        }

        Vector3 skeletonLocal3 = skeletonAnimation.transform.InverseTransformPoint(currentIKWorldPosition);
        Vector2 targetSkeletonSpace = new Vector2(skeletonLocal3.x, skeletonLocal3.y);
        targetSkeletonSpace += skeletonSpaceOffset;

        ikHundBone.SetPositionSkeletonSpace(targetSkeletonSpace);

        currentSkeleton.UpdateWorldTransform(Spine.Physics.Pose);
    }

    private bool TryGetLockTargetWorldPosition(out Vector2 worldPosition)
    {
        worldPosition = Vector2.zero;

        if (dataBus == null)
            return false;

        if (!dataBus.hasAttackTarget)
            return false;

        if (dataBus.lockedAttackTarget == null)
            return false;

        if (!dataBus.lockedAttackTarget.IsValid())
            return false;

        worldPosition = dataBus.lockedAttackTarget.GetWorldPosition();
        worldPosition += worldOffset;

        return true;
    }

    private void RefreshSpineReferences(string reason)
    {
        if (skeletonAnimation == null)
        {
            if (characterRoot != null)
                skeletonAnimation = characterRoot.houndSkeletonAnimation;

            if (skeletonAnimation == null)
                skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
        }

        if (skeletonAnimation == null)
        {
            cachedSkeleton = null;
            ikHundBone = null;
            return;
        }

        cachedSkeleton = skeletonAnimation.Skeleton;

        if (cachedSkeleton == null)
        {
            ikHundBone = null;
            return;
        }

        ikHundBone = cachedSkeleton.FindBone(ikAimBoneName_Hund);

        hasCurrentIKWorldPosition = false;

        if (logRefresh)
        {
            Debug.Log(
                $"[LockTargetIKController] RefreshSpineReferences: {reason}, " +
                $"SkeletonHash={cachedSkeleton.GetHashCode()}, " +
                $"Bone={(ikHundBone != null ? ikHundBone.Data.Name : "NULL")}",
                this
            );
        }

        if (ikHundBone == null)
        {
            Debug.LogWarning(
                $"[LockTargetIKController] 找不到 Spine Bone: {ikAimBoneName_Hund}",
                this
            );
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
            return;

        if (!Application.isPlaying)
            return;

        if (!hasCurrentIKWorldPosition)
            return;

        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(currentIKWorldPosition, 0.1f);

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position, currentIKWorldPosition);
    }
}