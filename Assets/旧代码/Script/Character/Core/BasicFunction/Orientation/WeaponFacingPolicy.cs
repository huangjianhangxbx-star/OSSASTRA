using UnityEngine;

/// <summary>
/// 统一的武器朝向所有权判断。
/// 将"有效武器朝向请求"与"武器租约"分开，
/// 避免未装备武器时误获朝向控制权。
/// </summary>
public static class WeaponFacingPolicy
{
    /// <summary>
    /// 武器是否处于可控制朝向的状态（已装备且可使用）。
    /// </summary>
    public static bool CanWeaponControlFacing(CharacterDataBus bus)
    {
        if (bus == null)
            return false;

        return bus.weaponEquipState == WeaponEquipState.Equipped
            && bus.canUseWeapon;
    }

    /// <summary>
    /// 是否有有效的攻击朝向请求。
    /// 包含输入层和运行状态层。
    /// </summary>
    public static bool HasAttackFacingRequest(CharacterDataBus bus)
    {
        if (!CanWeaponControlFacing(bus))
            return false;

        return bus.houndAttackHeld
            || bus.houndAttackPressedThisFrame
            || bus.isHoundAttacking
            || bus.currentHoundCombatAction == HoundCombatActionType.Fire;
    }

    /// <summary>
    /// 是否有有效的瞄准朝向请求。
    /// 包含输入层和运行状态层。
    /// </summary>
    public static bool HasAimFacingRequest(CharacterDataBus bus)
    {
        if (!CanWeaponControlFacing(bus))
            return false;

        return bus.aimHeld
            || bus.isAiming
            || bus.currentHoundCombatAction == HoundCombatActionType.Aim;
    }

    /// <summary>
    /// 武器朝向租约是否仍然有效（释放延迟内）。
    /// </summary>
    public static bool HasWeaponFacingLease(CharacterDataBus bus)
    {
        if (bus == null)
            return false;

        return bus.blockMoveFacingByWeapon
            && Time.time < bus.blockMoveFacingExpireTime;
    }

    /// <summary>
    /// 武器是否拥有朝向所有权（兼容旧调用）。
    /// 等价于：有攻击请求 或 有瞄准请求。
    /// </summary>
    public static bool HasWeaponFacingOwnership(CharacterDataBus bus)
    {
        return HasAttackFacingRequest(bus) || HasAimFacingRequest(bus);
    }
}
