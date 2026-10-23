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