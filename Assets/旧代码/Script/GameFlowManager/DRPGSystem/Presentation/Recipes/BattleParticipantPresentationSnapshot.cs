using System;
using UnityEngine;

public sealed class BattleParticipantPresentationSnapshot
{
    public BattleParticipantPresentationSnapshot(
        int runtimeId,
        string displayName,
        DRPGBattleSide side,
        DRPGBattleRosterRole rosterRole,
        float currentHp,
        float maxHp,
        DRPGLowHpPresentationState lowHpState,
        Sprite fullPortrait,
        Sprite hitPortrait,
        Sprite damageBrokenPortrait,
        DRPGBattleQuoteSetSO battleQuotes = null,
        Sprite attackCutInPortrait = null)
    {
        if (runtimeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(runtimeId));

        RuntimeId = runtimeId;
        DisplayName = displayName ?? string.Empty;
        Side = side;
        RosterRole = rosterRole;
        CurrentHp = currentHp;
        MaxHp = maxHp;
        LowHpState = lowHpState;
        FullPortrait = fullPortrait;
        HitPortrait = hitPortrait;
        DamageBrokenPortrait = damageBrokenPortrait;
        BattleQuotes = battleQuotes;
        AttackCutInPortrait = attackCutInPortrait != null
            ? attackCutInPortrait
            : fullPortrait;
    }

    public int RuntimeId { get; }
    public string DisplayName { get; }
    public DRPGBattleSide Side { get; }
    public DRPGBattleRosterRole RosterRole { get; }
    public float CurrentHp { get; }
    public float MaxHp { get; }
    public DRPGLowHpPresentationState LowHpState { get; }
    public Sprite FullPortrait { get; }
    public Sprite HitPortrait { get; }
    public Sprite DamageBrokenPortrait { get; }
    public DRPGBattleQuoteSetSO BattleQuotes { get; }
    public Sprite AttackCutInPortrait { get; }
    public bool IsAlive => CurrentHp > 0f;

    public static BattleParticipantPresentationSnapshot From(
        BattleParticipantRuntime participant)
    {
        if (participant == null)
            throw new ArgumentNullException(nameof(participant));

        DRPGCombatantProfileSO profile = participant.Profile;
        return new BattleParticipantPresentationSnapshot(
            participant.RuntimeId,
            profile.DisplayName,
            participant.Side,
            participant.RosterRole,
            participant.CurrentHp,
            profile.MaxHp,
            participant.LowHpState,
            profile.FullPortrait,
            profile.HitPortrait,
            profile.DamageBrokenPortrait,
            profile.BattleQuotes,
            profile.AttackCutInPortrait);
    }
}
