using Spine;
using Spine.Unity;
using UnityEngine;

public class GunWeaponModule : MonoBehaviour, IWeaponSubsystem
{
    private WeaponRoot weaponRoot;

    [Header("Ç¹¿ÚÉèÖÃ")]
    [SerializeField] private SkeletonAnimation weaponSkeletonAnimation;
    [SerializeField] private string muzzleBoneName = "Muzzle";

    public SkeletonAnimation WeaponSkeletonAnimation
    {
        get
        {
            if (weaponSkeletonAnimation != null)
                return weaponSkeletonAnimation;

            if (weaponRoot != null)
                return weaponRoot.WeaponSkeletonAnimation;

            return null;
        }
    }

    public string MuzzleBoneName => muzzleBoneName;

    public void Initialize(WeaponRoot root)
    {
        weaponRoot = root;

        if (weaponSkeletonAnimation == null && weaponRoot != null)
            weaponSkeletonAnimation = weaponRoot.WeaponSkeletonAnimation;
    }

    public bool TryGetMuzzleBone(out Bone muzzleBone, out SkeletonAnimation skeletonAnimation)
    {
        muzzleBone = null;
        skeletonAnimation = WeaponSkeletonAnimation;

        if (skeletonAnimation == null)
            return false;

        if (skeletonAnimation.Skeleton == null)
            return false;

        if (string.IsNullOrEmpty(muzzleBoneName))
            return false;

        muzzleBone = skeletonAnimation.Skeleton.FindBone(muzzleBoneName);
        return muzzleBone != null;
    }
}