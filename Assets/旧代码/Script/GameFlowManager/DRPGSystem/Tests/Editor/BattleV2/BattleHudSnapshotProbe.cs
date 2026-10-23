#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

public static class BattleHudSnapshotProbe
{
    public static void RunAll()
    {
        FixedSlotsPreservePositionsAndCopyInput();
        OverflowIsRejectedInsteadOfTruncated();
        SnapshotCopiesCellsAndKeepsUnknownValuesUnknown();
        SnapshotContractsDoNotExposeEnemyStamina();
    }

    private static void FixedSlotsPreservePositionsAndCopyInput()
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        try
        {
            var input = new DRPGSkillSO[] { skill, null, skill, null };
            var slots = BattleHudSnapshotBuilder.FixedSlots(input);
            input[0] = null;

            Check(slots.Count == 4, "fixed skill slot count changed");
            Check(slots[0] == skill && slots[1] == null &&
                slots[2] == skill && slots[3] == null,
                "slot positions or defensive copy lost");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(skill);
        }
    }

    private static void OverflowIsRejectedInsteadOfTruncated()
    {
        bool rejected = false;
        try
        {
            BattleHudSnapshotBuilder.FixedSlots(new DRPGSkillSO[5]);
        }
        catch (ArgumentException)
        {
            rejected = true;
        }

        Check(rejected, "configured skill overflow was silently truncated");
    }

    private static void SnapshotCopiesCellsAndKeepsUnknownValuesUnknown()
    {
        DRPGSkillSO skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
        try
        {
            var source = new BattleSkillCell[]
            {
                new BattleSkillCell(0, skill, null, null, string.Empty,
                    false, "新体力和面板尚未接入"),
                BattleSkillCell.Empty(1),
                BattleSkillCell.Empty(2),
                BattleSkillCell.Empty(3)
            };
            var snapshot = new BattleHudSnapshot(7, 11, 13, source, true);
            source[0] = BattleSkillCell.Empty(0);

            Check(snapshot.Skills.Count == 4 && snapshot.Skills[0].Skill == skill,
                "snapshot did not defensively copy cells");
            Check(!snapshot.Skills[0].Cost.HasValue &&
                !snapshot.Skills[0].Panel.HasValue,
                "unknown values were forged as zero");
            Check(snapshot.IsPreviewData, "preview provenance was lost");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(skill);
        }
    }

    private static void SnapshotContractsDoNotExposeEnemyStamina()
    {
        foreach (Type type in new[] { typeof(BattleHudSnapshot), typeof(BattleSkillCell) })
        {
            foreach (PropertyInfo property in type.GetProperties(
                BindingFlags.Instance | BindingFlags.Public))
            {
                Check(property.Name.IndexOf("EnemyStamina",
                    StringComparison.OrdinalIgnoreCase) < 0,
                    type.Name + " leaks enemy stamina through " + property.Name);
            }
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
