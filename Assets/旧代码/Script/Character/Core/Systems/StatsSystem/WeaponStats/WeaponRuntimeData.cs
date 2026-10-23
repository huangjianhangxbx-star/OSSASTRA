using System.Collections.Generic;
using UnityEngine;


//WeaponRuntimeData(config)
//  ->Stats
//  ->FirearmStats
//  ->ActionProfile
//  ->FirearmActionProfile


public struct FirearmFinalStats
{
    public float damage;
    public float critChance;
    public float critDamage;
    public float poiseDamage;
    public float knockbackForce;

    public float range;
    public int magazineSize;
    public float reloadTime;
    public float projectileSpeed;
    public float spreadAngle;
    public float recoil;
}
public class WeaponRuntimeData
{
    public WeaponConfigSO config;
    public WeaponRoot weaponRoot;

    public List<StatsModifier> runtimeModifiers = new List<StatsModifier>();

    public WeaponRuntimeData(WeaponConfigSO config)
    {
        this.config = config;
    }

    public WeaponType WeaponType
    {
        get
        {
            if (ActionProfile != null)
                return ActionProfile.WeaponType;

            if (config != null)
                return config.weaponType;

            return WeaponType.Melee;
        }
    }

    public bool IsFirearm => WeaponType == WeaponType.Firearm;
    public bool IsMelee => WeaponType == WeaponType.Melee;


    public WeaponStatsSO Stats
    {
        get
        {
            if (config == null)
                return null;

            return config.stats;
        }
    }

    public WeaponCommonCoreStats CommonStats
    {
        get
        {
            if (Stats == null)
                return null;

            return Stats.commonCoreStats;
        }
    }

    public FirearmWeaponStats FirearmStats
    {
        get
        {
            if (Stats == null)
                return null;

            if (!Stats.IsFirearm)
                return null;

            return Stats.firearmStats;
        }
    }

    public MeleeWeaponStats MeleeStats
    {
        get
        {
            if (Stats == null)
                return null;

            if (!Stats.IsMelee)
                return null;

            return Stats.meleeStats;
        }
    }

    public AnimationPlanOverrideRuleSetSO AnimationOverride
    {
        get
        {
            if (config == null)
                return null;

            return config.animationOverride;
        }
    }

    public WeaponActionProfileSO ActionProfile
    {
        get
        {
            if (config == null)
                return null;

            return config.weaponActionProfile;
        }
    }
    public WeaponAudioProfileSO AudioProfile
    {
        get
        {
            if (config == null)
                return null;

            return config.audioProfile;
        }
    }
    public FirearmActionProfileSO FirearmActionProfile
    {
        get
        {
            return ActionProfile as FirearmActionProfileSO;
        }
    }

    public float GetFinalFireInterval(GunFireModeConfig fireModeConfig, CharacterStatsController statsController)
    {
        if (fireModeConfig == null)
            return 0.15f;

        float fireRateBonus = statsController != null
            ? statsController.GetStat(CharacterStats.FireRateBonus)
            : 0f;

        return fireModeConfig.fireInterval / Mathf.Max(0.01f, 1f + fireRateBonus);
    }














    public WeaponRoot WeaponRoot => weaponRoot;


    public FirearmFinalStats BuildFirearmFinalStats(CharacterStatsController statsController)
    {
        FirearmFinalStats result = new FirearmFinalStats();

        if (Stats == null || !Stats.IsFirearm)
            return result;

        WeaponCommonCoreStats core = Stats.commonCoreStats;
        FirearmWeaponStats firearm = Stats.firearmStats;

        if (core == null || firearm == null)
            return result;

        float firearmDamageBonus = statsController != null
            ? statsController.GetStat(CharacterStats.FirearmDamageBonus)
            : 0f;

        float reloadSpeedBonus = statsController != null
            ? statsController.GetStat(CharacterStats.ReloadSpeedBonus)
            : 0f;

        float recoilControl = statsController != null
            ? statsController.GetStat(CharacterStats.RecoilControl)
            : 0f;

        float accuracyBonus = statsController != null
            ? statsController.GetStat(CharacterStats.AccuracyBonus)
            : 0f;

        float critChanceBonus = statsController != null
            ? statsController.GetStat(CharacterStats.CritChanceBonus)
            : 0f;

        float critDamageBonus = statsController != null
            ? statsController.GetStat(CharacterStats.CritDamageBonus)
            : 0f;

        float projectileSpeedBonus = statsController != null
            ? statsController.GetStat(CharacterStats.ProjectileSpeedBonus)
            : 0f;

        result.damage = core.damage * (1f + firearmDamageBonus);
        result.critChance = core.critChance + critChanceBonus;
        result.critDamage = core.critDamage + critDamageBonus;
        result.poiseDamage = core.poiseDamage;
        result.knockbackForce = core.knockbackForce;

        result.range = firearm.range;
        result.magazineSize = firearm.magazineSize;
        result.reloadTime = firearm.reloadTime / Mathf.Max(0.01f, 1f + reloadSpeedBonus);
        result.projectileSpeed = firearm.projectileSpeed * (1f + projectileSpeedBonus);
        result.spreadAngle = firearm.spreadAngle / Mathf.Max(0.01f, 1f + accuracyBonus);
        result.recoil = firearm.recoil * Mathf.Max(0f, 1f - recoilControl);

        return result;
    }




}