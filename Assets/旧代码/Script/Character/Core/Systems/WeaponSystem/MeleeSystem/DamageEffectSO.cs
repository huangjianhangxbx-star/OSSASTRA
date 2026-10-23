using UnityEngine;

[CreateAssetMenu(
    fileName = "DamageEffect",
    menuName = "ScriptableObject/ս��Ч��/�˺�")]
public class DamageEffectSO : CombatEffectSO
{
    [Header("�˺�����")]
    public CombatDamageValueMode valueMode =
        CombatDamageValueMode.WeaponCoreMultiplier;

    [Min(0f)]
    public float flatDamage;

    [Min(0f)]
    public float weaponDamageMultiplier = 1f;

    [Header("受击反应类型")]
    public CombatImpactType impactType = CombatImpactType.Unknown;

    public override bool CanApply(
        in CombatEffectContext context)
    {
        return context.CanDamage;
    }

    public override void Apply(
        in CombatEffectContext context)
    {
        if (!CanApply(context))
            return;

        float weaponDamage = 0f;

        if (context.sourceWeapon != null &&
            context.sourceWeapon.CommonStats != null)
        {
            weaponDamage =
                context.sourceWeapon.CommonStats.damage;
        }

        float damage;

        switch (valueMode)
        {
            case CombatDamageValueMode.Flat:
                damage = flatDamage;
                break;

            case CombatDamageValueMode
                .WeaponCoreMultiplier:
                damage =
                    weaponDamage *
                    weaponDamageMultiplier;
                break;

            case CombatDamageValueMode
                .FlatPlusWeaponCoreMultiplier:
                damage =
                    flatDamage +
                    weaponDamage *
                    weaponDamageMultiplier;
                break;

            default:
                damage = 0f;
                break;
        }

        damage *= context.powerMultiplier;

        if (damage <= 0f)
            return;

        DamageInfo damageInfo = new DamageInfo
        {
            attacker = context.Attacker,
            damage = damage,
            hitPoint = context.hitPoint,
            hitNormal = context.hitNormal,
            shotDirection = context.attackDirection,
            impactType = impactType
        };

        context.damageable.TakeDamage(damageInfo);
    }
}