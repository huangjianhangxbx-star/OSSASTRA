using System;
using System.Collections.Generic;
using UnityEngine;

public enum DRPGFormationRow
{
    Front = 0,
    Middle = 1,
    Rear = 2
}

[Flags]
public enum DRPGFormationRowMask
{
    None = 0,
    Front = 1 << 0,
    Middle = 1 << 1,
    Rear = 1 << 2,
    All = Front | Middle | Rear
}

public static class DRPGFormationRowMaskExtensions
{
    public static bool Contains(
        this DRPGFormationRowMask mask,
        DRPGFormationRow row)
    {
        return (mask & ToMask(row)) != 0;
    }

    public static DRPGFormationRowMask ToMask(DRPGFormationRow row)
    {
        return (DRPGFormationRowMask)(1 << (int)row);
    }
}

[Serializable]
public sealed class DRPGFormationRules
{
    [SerializeField, Min(1)] private int initialEnemyLimit = 6;
    [SerializeField, Min(1)] private int activeEnemyLimit = 6;
    [SerializeField, Min(1)] private int perRowLimit = 2;
    [SerializeField, Min(0)] private int reinforcementDelayActions = 1;

    public DRPGFormationRules()
    {
    }

    public DRPGFormationRules(
        int initialEnemyLimit,
        int activeEnemyLimit,
        int perRowLimit,
        int reinforcementDelayActions)
    {
        this.initialEnemyLimit = initialEnemyLimit;
        this.activeEnemyLimit = activeEnemyLimit;
        this.perRowLimit = perRowLimit;
        this.reinforcementDelayActions = reinforcementDelayActions;
    }

    public int InitialEnemyLimit => Mathf.Clamp(
        initialEnemyLimit,
        1,
        ActiveEnemyLimit);
    public int ActiveEnemyLimit => Mathf.Clamp(
        activeEnemyLimit,
        1,
        PerRowLimit * 3);
    public int PerRowLimit => Mathf.Max(1, perRowLimit);
    public int ReinforcementDelayActions =>
        Mathf.Max(0, reinforcementDelayActions);

    public static DRPGFormationRules Default =>
        new DRPGFormationRules(6, 6, 2, 1);
}

public static class DRPGFormationNormalizer
{
    public static bool CompactOccupiedRows(
        IReadOnlyList<BattleParticipantRuntime> participants,
        DRPGBattleSide side)
    {
        if (participants == null)
            throw new ArgumentNullException(nameof(participants));

        var occupied = new bool[3];
        for (int i = 0; i < participants.Count; i++)
        {
            BattleParticipantRuntime participant = participants[i];
            if (IsIncluded(participant, side))
                occupied[(int)participant.FormationRow] = true;
        }

        var compacted = new DRPGFormationRow[3];
        int next = 0;
        for (int i = 0; i < occupied.Length; i++)
        {
            if (occupied[i])
                compacted[i] = (DRPGFormationRow)next++;
        }

        bool changed = false;
        for (int i = 0; i < participants.Count; i++)
        {
            BattleParticipantRuntime participant = participants[i];
            if (!IsIncluded(participant, side))
                continue;
            DRPGFormationRow target = compacted[(int)participant.FormationRow];
            changed |= participant.TrySetFormationRow(target);
        }

        return changed;
    }

    private static bool IsIncluded(
        BattleParticipantRuntime participant,
        DRPGBattleSide side)
    {
        if (participant == null || !participant.IsAlive ||
            !participant.IsDeployed || participant.Side != side)
        {
            return false;
        }

        return side == DRPGBattleSide.Enemy || participant.IsBattlefieldMember;
    }
}
