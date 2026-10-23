using System;
using UnityEngine;


[Flags]
public enum CombatEffectTargetType
{
    None = 0,

    Damageable = 1 << 0,
    Character = 1 << 1,
    PhysicsObject = 1 << 2,
    Breakable = 1 << 3,

    Any = ~0
}

public abstract class CombatEffectSO : ScriptableObject
{
    [Header("适用目标")]
    public CombatEffectTargetType targetTypes = CombatEffectTargetType.Any;

    /// <summary>
    /// 效果执行器自己判断目标是否具有所需能力。
    /// </summary>
    public abstract bool CanApply(
        in CombatEffectContext context);

    public abstract void Apply(
        in CombatEffectContext context);
}