using System;
using UnityEngine;

public enum DRPGResonanceProgressActorRule
{
    Any = 0,
    Protagonist = 1,
    Partner = 2
}

[Serializable]
public sealed class DRPGResonanceProgressSource
{
    [SerializeField] private DRPGBattleEventKind eventKind =
        DRPGBattleEventKind.ActionImpactResolved;
    [SerializeField] private DRPGResonanceProgressActorRule actorRule =
        DRPGResonanceProgressActorRule.Partner;
    [Min(1)]
    [SerializeField] private int progressAmount = 1;

    public DRPGResonanceProgressSource()
    {
    }

    public DRPGResonanceProgressSource(
        DRPGBattleEventKind eventKind,
        DRPGResonanceProgressActorRule actorRule,
        int progressAmount)
    {
        this.eventKind = eventKind;
        this.actorRule = actorRule;
        this.progressAmount = Mathf.Max(1, progressAmount);
    }

    public DRPGBattleEventKind EventKind => eventKind;
    public DRPGResonanceProgressActorRule ActorRule => actorRule;
    public int ProgressAmount => Mathf.Max(1, progressAmount);

    public bool Matches(
        DRPGBattleEvent battleEvent,
        int protagonistRuntimeId,
        int partnerRuntimeId)
    {
        if (battleEvent == null || battleEvent.Kind != eventKind)
            return false;

        switch (actorRule)
        {
            case DRPGResonanceProgressActorRule.Any:
                return true;
            case DRPGResonanceProgressActorRule.Protagonist:
                return protagonistRuntimeId > 0 &&
                    battleEvent.SourceActorRuntimeId == protagonistRuntimeId;
            case DRPGResonanceProgressActorRule.Partner:
                return partnerRuntimeId > 0 &&
                    battleEvent.SourceActorRuntimeId == partnerRuntimeId;
            default:
                return false;
        }
    }
}
