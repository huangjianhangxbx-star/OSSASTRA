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