using UnityEngine;

public readonly struct CombatEffectContext
{
    /// <summary>
    /// 发起动作的角色。
    /// </summary>
    public readonly CharacterRoot attackerRoot;

    /// <summary>
    /// 发起动作时锁定的武器运行时数据。
    /// </summary>
    public readonly WeaponRuntimeData sourceWeapon;

    /// <summary>
    /// 当前执行的近战动作资产。
    /// </summary>
    public readonly MeleeComboActionSO action;

    /// <summary>
    /// 当前是动作中的第几段。
    /// </summary>
    public readonly int stepIndex;

    /// <summary>
    /// 一次攻击动作的唯一运行时 ID。
    /// 可用于命中去重、日志和后续联机同步。
    /// </summary>
    public readonly int attackInstanceId;

    /// <summary>
    /// 实际被检测到的碰撞体。
    /// </summary>
    public readonly Collider2D hitCollider;

    /// <summary>
    /// 归一化后的目标根对象。
    /// </summary>
    public readonly GameObject target;

    /// <summary>
    /// 目标的伤害接收接口。
    /// 目标不可受伤时可以为空。
    /// </summary>
    public readonly IDamageable damageable;

    /// <summary>
    /// 本次命中的世界坐标。
    /// </summary>
    public readonly Vector2 hitPoint;

    /// <summary>
    /// 本次命中的近似法线。
    /// </summary>
    public readonly Vector2 hitNormal;

    /// <summary>
    /// 攻击者指向攻击方向的归一化向量。
    /// 不应依赖目标位于攻击者哪一边临时反算。
    /// </summary>
    public readonly Vector2 attackDirection;

    /// <summary>
    /// 蓄力、Buff 或特殊派生动作提供的统一强度倍率。
    /// 普通攻击为 1。
    /// </summary>
    public readonly float powerMultiplier;

    public GameObject Attacker
    {
        get
        {
            return attackerRoot != null
                ? attackerRoot.gameObject
                : null;
        }
    }

    public bool HasTarget => target != null;

    public bool CanDamage
    {
        get
        {
            return damageable != null &&
                damageable.CanTakeDamage;
        }
    }

    public CombatEffectContext(
        CharacterRoot attackerRoot,
        WeaponRuntimeData sourceWeapon,
        MeleeComboActionSO action,
        int stepIndex,
        int attackInstanceId,
        Collider2D hitCollider,
        GameObject target,
        IDamageable damageable,
        Vector2 hitPoint,
        Vector2 hitNormal,
        Vector2 attackDirection,
        float powerMultiplier = 1f)
    {
        this.attackerRoot = attackerRoot;
        this.sourceWeapon = sourceWeapon;
        this.action = action;
        this.stepIndex = stepIndex;
        this.attackInstanceId = attackInstanceId;
        this.hitCollider = hitCollider;
        this.target = target;
        this.damageable = damageable;
        this.hitPoint = hitPoint;
        this.hitNormal = hitNormal;

        this.attackDirection =
            attackDirection.sqrMagnitude > 0.0001f
                ? attackDirection.normalized
                : Vector2.right;

        this.powerMultiplier =
            Mathf.Max(0f, powerMultiplier);
    }
}