using System;
using System.Collections.Generic;

public static class BattleHudSnapshotBuilder
{
    public static IReadOnlyList<DRPGSkillSO> FixedSlots(
        IReadOnlyList<DRPGSkillSO> configured)
    {
        if (configured != null && configured.Count > DRPGSkillLoadout.SlotCount)
            throw new ArgumentException("Configured skill loadout exceeds four slots.",
                nameof(configured));

        var result = new DRPGSkillSO[DRPGSkillLoadout.SlotCount];
        if (configured != null)
        {
            for (int i = 0; i < configured.Count; i++)
                result[i] = configured[i];
        }
        return Array.AsReadOnly(result);
    }

    public static BattleHudSnapshot Build(
        long generation,
        BattleParticipantRuntime actor,
        long revision,
        bool isPreviewData)
    {
        if (actor == null)
            throw new ArgumentNullException(nameof(actor));

        IReadOnlyList<DRPGSkillSO> slots = FixedSlots(
            actor.Build.ConfiguredSkills);
        var cells = new BattleSkillCell[DRPGSkillLoadout.SlotCount];
        for (int i = 0; i < cells.Length; i++)
        {
            DRPGSkillSO skill = slots[i];
            bool modeAllowed = skill != null &&
                DRPGBattleSkillModePolicy.IsAllowed(skill);
            bool affordable = skill != null &&
                actor.BattleStamina.Available >= skill.StaminaCost;
            cells[i] = skill == null
                ? BattleSkillCell.Empty(i)
                : new BattleSkillCell(i, skill, skill.StaminaCost,
                    BattleEffectPanel.Calculate(
                        skill.Power,
                        0d,
                        Math.Max(0f, actor.Build.DamageMultiplier) - 1d),
                    ResolveAttributeKey(skill),
                    modeAllowed && affordable,
                    !modeAllowed
                        ? "该技能不是当前标准攻击技能"
                        : affordable
                            ? string.Empty
                            : "体力豆不足");
        }

        return new BattleHudSnapshot(generation, actor.RuntimeId, revision,
            cells, isPreviewData);
    }

    private static string ResolveAttributeKey(DRPGSkillSO skill)
    {
        for (int i = 0; i < skill.Effects.Count; i++)
        {
            DRPGActionEffectDefinition effect = skill.Effects[i];
            if (effect != null && effect.Kind == DRPGActionEffectKind.Damage)
                return effect.DamageTags.ToString();
        }
        return skill.Purpose.ToString();
    }
}
