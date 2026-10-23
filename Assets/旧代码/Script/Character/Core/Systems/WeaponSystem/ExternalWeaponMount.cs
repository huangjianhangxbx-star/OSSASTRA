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
