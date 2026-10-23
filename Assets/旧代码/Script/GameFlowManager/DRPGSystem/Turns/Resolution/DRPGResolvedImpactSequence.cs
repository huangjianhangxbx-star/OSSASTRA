using System;
using System.Collections.Generic;

public sealed class DRPGResolvedImpactSequence
{
    private readonly DRPGResolvedImpactPartition[] partitions;

    public DRPGResolvedImpactSequence(
        IReadOnlyList<DRPGResolvedImpactPartition> source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source.Count == 0)
        {
            throw new ArgumentException(
                "An impact sequence must contain at least one partition.",
                nameof(source));
        }

        partitions = new DRPGResolvedImpactPartition[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            DRPGResolvedImpactPartition partition = source[i] ??
                throw new ArgumentException(
                    "Impact partitions cannot contain null entries.",
                    nameof(source));
            if (partition.Index != i)
            {
                throw new ArgumentException(
                    "Impact partition indices must be contiguous and ordered.",
                    nameof(source));
            }
            partitions[i] = partition;
        }
    }

    public int Count => partitions.Length;

    public DRPGResolvedImpactPartition GetAt(int index)
    {
        if (index < 0 || index >= partitions.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        return partitions[index];
    }

    public static DRPGResolvedImpactSequence CreateFor(
        DRPGBattleCommand command,
        DRPGResolvedActionSpec spec)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));
        if (spec == null)
            throw new ArgumentNullException(nameof(spec));

        IReadOnlyList<float> authored = command.Skill != null
            ? command.Skill.ImpactDamageMultipliers
            : Array.Empty<float>();
        bool supportsIndexedImpacts =
            spec.ExecutionMode == DRPGSkillExecutionMode.Standard &&
            command.Stage == DRPGBattleCommandStage.Immediate &&
            (command.Kind == DRPGBattleCommandKind.BasicAttack ||
             command.Kind == DRPGBattleCommandKind.Skill ||
             command.Kind == DRPGBattleCommandKind.ResonanceSkill);
        int count = supportsIndexedImpacts && authored.Count > 0
            ? authored.Count
            : 1;
        var result = new DRPGResolvedImpactPartition[count];
        for (int i = 0; i < count; i++)
        {
            float multiplier = supportsIndexedImpacts && authored.Count > 0
                ? Math.Max(0f, authored[i])
                : 1f;
            result[i] = new DRPGResolvedImpactPartition(
                i,
                command.TargetRuntimeId,
                DRPGBattleImpactContext.ForStrike(
                    DRPGStrikeGrade.Body,
                    multiplier));
        }
        return new DRPGResolvedImpactSequence(result);
    }
}
