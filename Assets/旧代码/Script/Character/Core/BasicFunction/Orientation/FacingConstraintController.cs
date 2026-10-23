using UnityEngine;
using static CharacterDataBus;

/// <summary>
/// 武器朝向约束控制器。
/// 使用 WeaponFacingPolicy 统一判断武器是否拥有朝向所有权。
/// 维护 blockMoveFacingByWeapon 租约。
/// </summary>
public class FacingConstraintController
{
    private readonly CharacterDataBus dataBus;
    private readonly float defaultWeaponReleaseDelay;

    public FacingConstraintController(
        CharacterDataBus dataBus,
        float defaultWeaponReleaseDelay)
    {
        this.dataBus = dataBus;
        this.defaultWeaponReleaseDelay = Mathf.Max(0.01f, defaultWeaponReleaseDelay);
    }

    public void Tick(float deltaTime)
    {
        if (dataBus == null)
            return;

        UpdateWeaponMoveFacingLock();
    }

    public void NotifyWeaponFacingActivity(float releaseDelay = -1f)
    {
        if (dataBus == null)
            return;

        dataBus.lastWeaponFacingActivityTime = Time.time;

        float delay = releaseDelay > 0f
            ? releaseDelay
            : defaultWeaponReleaseDelay;

        dataBus.blockMoveFacingExpireTime = Mathf.Max(
            dataBus.blockMoveFacingExpireTime,
            Time.time + Mathf.Max(0.01f, delay)
        );
    }

    public void ClearWeaponFacingLock()
    {
        if (dataBus == null)
            return;

        dataBus.blockMoveFacingExpireTime = 0f;
        dataBus.blockMoveFacingByWeapon = false;
    }

    /// <summary>
    /// 武器是否拥有朝向所有权（统一判断）。
    /// </summary>
    public bool HasWeaponFacingOwnership => WeaponFacingPolicy.HasWeaponFacingOwnership(dataBus);

    private void UpdateWeaponMoveFacingLock()
    {
        // 使用统一的武器活跃判断
        if (IsWeaponInputActive() || IsWeaponStateActive())
            NotifyWeaponFacingActivity(defaultWeaponReleaseDelay);

        dataBus.blockMoveFacingByWeapon =
            Time.time < dataBus.blockMoveFacingExpireTime;
    }

    private bool IsWeaponInputActive()
    {
        return dataBus.aimHeld;
    }

    private bool IsWeaponStateActive()
    {
        return dataBus.isAiming
            || dataBus.currentHoundCombatAction == HoundCombatActionType.Aim;
    }
}
