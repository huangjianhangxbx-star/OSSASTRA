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