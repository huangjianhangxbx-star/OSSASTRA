using System;

public readonly struct BattleFreeStrikeTargetAnchor
{
    public BattleFreeStrikeTargetAnchor(
        BattleParticipantRuntime participant,
        BattleImpactAnchor anchor)
    {
        Participant = participant ??
            throw new ArgumentNullException(nameof(participant));
        Anchor = anchor ?? throw new ArgumentNullException(nameof(anchor));
    }

    public BattleParticipantRuntime Participant { get; }
    public BattleImpactAnchor Anchor { get; }
}
