using System;
using System.Collections.Generic;

public readonly struct BattleSkillCell
{
    public BattleSkillCell(
        int slotIndex,
        DRPGSkillSO skill,
        int? cost,
        int? panel,
        string attributeKey,
        bool enabled,
        string reason)
    {
        if (slotIndex < 0 || slotIndex >= DRPGSkillLoadout.SlotCount)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        if (cost.HasValue && cost.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(cost));
        if (panel.HasValue && panel.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(panel));

        SlotIndex = slotIndex;
        Skill = skill;
        Cost = cost;
        Panel = panel;
        AttributeKey = attributeKey ?? string.Empty;
        Enabled = skill != null && enabled;
        Reason = reason ?? string.Empty;
    }

    public int SlotIndex { get; }
    public DRPGSkillSO Skill { get; }
    public int? Cost { get; }
    public int? Panel { get; }
    public string AttributeKey { get; }
    public bool Enabled { get; }
    public string Reason { get; }

    public static BattleSkillCell Empty(int slotIndex) =>
        new BattleSkillCell(slotIndex, null, null, null, string.Empty, false,
            "未配置技能");
}

public sealed class BattleHudSnapshot
{
    private readonly IReadOnlyList<BattleSkillCell> skills;

    public BattleHudSnapshot(
        long generation,
        int actorRuntimeId,
        long revision,
        IReadOnlyList<BattleSkillCell> skills,
        bool isPreviewData)
    {
        if (generation < 0)
            throw new ArgumentOutOfRangeException(nameof(generation));
        if (actorRuntimeId < 0)
            throw new ArgumentOutOfRangeException(nameof(actorRuntimeId));
        if (revision < 0)
            throw new ArgumentOutOfRangeException(nameof(revision));
        if (skills == null)
            throw new ArgumentNullException(nameof(skills));
        if (skills.Count != DRPGSkillLoadout.SlotCount)
            throw new ArgumentException("Battle HUD requires exactly four skill cells.",
                nameof(skills));

        var copy = new BattleSkillCell[skills.Count];
        for (int i = 0; i < skills.Count; i++)
        {
            if (skills[i].SlotIndex != i)
                throw new ArgumentException("Skill cell slot index is not canonical.",
                    nameof(skills));
            copy[i] = skills[i];
        }

        Generation = generation;
        ActorRuntimeId = actorRuntimeId;
        Revision = revision;
        this.skills = Array.AsReadOnly(copy);
        IsPreviewData = isPreviewData;
    }

    public long Generation { get; }
    public int ActorRuntimeId { get; }
    public long Revision { get; }
    public IReadOnlyList<BattleSkillCell> Skills => skills;
    public bool IsPreviewData { get; }
}
