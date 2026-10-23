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