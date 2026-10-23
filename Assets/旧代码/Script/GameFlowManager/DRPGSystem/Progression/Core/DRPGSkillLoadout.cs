using System;
using System.Collections.Generic;

public sealed class DRPGSkillLoadout
{
    public const int SlotCount = 4;
    private readonly DRPGSkillSO[] slots = new DRPGSkillSO[SlotCount];

    public IReadOnlyList<DRPGSkillSO> Slots => slots;

    public DRPGProgressionOperationResult TryEquip(
        DRPGSkillSO skill,
        int slotIndex,
        ISet<string> allowedSkillIds)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount)
        {
            return DRPGProgressionOperationResult.Fail(
                DRPGProgressionError.InvalidSlot,
                "Skill slot must be between 0 and 3.");
        }

        if (skill == null)
        {
            slots[slotIndex] = null;
            return DRPGProgressionOperationResult.Ok();
        }

        if (allowedSkillIds == null || !allowedSkillIds.Contains(skill.SkillId))
        {
            return DRPGProgressionOperationResult.Fail(
                DRPGProgressionError.SkillLocked,
                "Skill is not unlocked in the active profession: " + skill.SkillId);
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (i != slotIndex && slots[i] != null &&
                string.Equals(
                    slots[i].SkillId,
                    skill.SkillId,
                    StringComparison.Ordinal))
            {
                return DRPGProgressionOperationResult.Fail(
                    DRPGProgressionError.SkillAlreadyEquipped,
                    "Skill is already equipped: " + skill.SkillId);
            }
        }

        slots[slotIndex] = skill;
        return DRPGProgressionOperationResult.Ok();
    }

    public DRPGProgressionOperationResult TrySwap(int firstSlot, int secondSlot)
    {
        if (firstSlot < 0 || firstSlot >= SlotCount ||
            secondSlot < 0 || secondSlot >= SlotCount)
        {
            return DRPGProgressionOperationResult.Fail(
                DRPGProgressionError.InvalidSlot,
                "Skill slot must be between 0 and 3.");
        }

        if (firstSlot == secondSlot)
            return DRPGProgressionOperationResult.Ok();

        DRPGSkillSO first = slots[firstSlot];
        slots[firstSlot] = slots[secondSlot];
        slots[secondSlot] = first;
        return DRPGProgressionOperationResult.Ok();
    }

    public void RemoveUnavailable(ISet<string> allowedSkillIds)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null &&
                (allowedSkillIds == null ||
                 !allowedSkillIds.Contains(slots[i].SkillId)))
            {
                slots[i] = null;
            }
        }
    }

    public DRPGSkillSO[] Snapshot()
    {
        return (DRPGSkillSO[])slots.Clone();
    }
}
