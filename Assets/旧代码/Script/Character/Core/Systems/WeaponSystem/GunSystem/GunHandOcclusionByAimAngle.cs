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