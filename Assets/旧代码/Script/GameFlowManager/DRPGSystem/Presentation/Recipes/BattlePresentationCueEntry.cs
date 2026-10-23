using System;
using UnityEngine;

[Serializable]
public sealed class BattlePresentationCueEntry
{
    [SerializeField] private BattlePresentationCueSO cue;
    [SerializeField] private BattlePresentationCondition condition;
    [SerializeField] private BattlePresentationLaneKind lane;
    [SerializeField] private BattlePresentationConflictPolicy conflictPolicy;
    [SerializeField] private bool blocksBeat = true;

    public BattlePresentationCueSO Cue => cue;
    public BattlePresentationCondition Condition => condition;
    public BattlePresentationLaneKind Lane => lane;
    public BattlePresentationConflictPolicy ConflictPolicy => conflictPolicy;
    public bool BlocksBeat => blocksBeat;
}
