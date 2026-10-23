using UnityEngine;


//WeaponConfigSO
//  stats = WeaponStatsSO
//  weaponActionProfile = FirearmActionProfileSO
//  weaponPrefab = WeaponRoot


//WeaponRuntimeData(config)
//  ->Stats
//  ->FirearmStats
//  ->ActionProfile
//  ->FirearmActionProfile


//GunSystemController.ApplyWeaponRuntimeData(runtimeData)
//  ->读取 damage / range / magazine / fireRate
//  ->读取 FirearmActionProfileSO
//  ->读取 defaultFireMode
//  ->读取 GunFireModeConfig
//  ->决定点射 / 连发 / 自动



[CreateAssetMenu(fileName = "WeaponConfig", menuName = "ScriptableObject/武器/武器总配置")]
public class WeaponConfigSO : ScriptableObject
{
    [Header("武器名字")]
    public string weaponName;

    [Header("武器类型")]
    public WeaponType weaponType = WeaponType.Firearm;

    [Header("武器默认朝向")]
    [Tooltip("武器 prefab 在未旋转、未镜像状态下，枪口默认朝向。大部分枪通常是 Right。")]
    public WeaponDefaultFacing defaultFacing = WeaponDefaultFacing.Right;

    [Header("武器数值")]
    public WeaponStatsSO stats;

    [Header("武器行为")]
    public WeaponActionProfileSO weaponActionProfile;
    [Header("武器音频")]
    public WeaponAudioProfileSO audioProfile;
    [Header("武器动画覆写")]
    public AnimationPlanOverrideRuleSetSO animationOverride;

    [Header("武器预制体")]
    public WeaponRoot weaponPrefab;

    public bool IsFirearm => weaponType == WeaponType.Firearm;
    public bool IsMelee => weaponType == WeaponType.Melee;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (stats != null)
        {
            weaponType = stats.weaponType;
        }

        if (weaponActionProfile != null &&
            weaponActionProfile.WeaponType != weaponType)
        {
            Debug.LogWarning(
                $"[WeaponConfigSO] {name} 的武器类型是 {weaponType}，但行为配置是 {weaponActionProfile.WeaponType}",
                this
            );
        }

        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}