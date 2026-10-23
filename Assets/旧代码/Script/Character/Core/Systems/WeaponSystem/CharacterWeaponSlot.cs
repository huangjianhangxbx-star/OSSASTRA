using System;
using UnityEngine;

[Serializable]
public class CharacterWeaponSlot
{
    [SerializeField]
    private WeaponSlotId slotId;

    /*
     * WeaponRuntimeData 如果不是 Unity 可序列化类型，
     * Inspector 中不会显示，但运行时赋值仍然可以使用。
     */
    [SerializeField]
    private WeaponRuntimeData weaponRuntimeData;

    [SerializeField]
    private bool isUnlocked = true;

    public WeaponSlotId SlotId => slotId;

    public WeaponRuntimeData Weapon => weaponRuntimeData;

    public bool IsUnlocked => isUnlocked;

    public bool IsEmpty => weaponRuntimeData == null;

    public bool CanSelect =>
        isUnlocked && weaponRuntimeData != null;

    public void Initialize(WeaponSlotId id)
    {
        slotId = id;
    }

    public void SetWeapon(WeaponRuntimeData weapon)
    {
        weaponRuntimeData = weapon;
    }

    public WeaponRuntimeData RemoveWeapon()
    {
        WeaponRuntimeData removedWeapon = weaponRuntimeData;
        weaponRuntimeData = null;
        return removedWeapon;
    }

    public void SetUnlocked(bool unlocked)
    {
        isUnlocked = unlocked;
    }
}