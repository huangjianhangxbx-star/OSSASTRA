using System;
using System.Collections.Generic;

public sealed class BattlePresentationRequest
{
    public BattlePresentationRequest(
        long requestId,
        BattlePresentationMoment moment,
        BattlePresentationScope scope,
        BattleParticipantPresentationSnapshot actor,
        IEnumerable<BattleParticipantPresentationSnapshot> targets,
        IEnumerable<int> timelineRuntimeIds,
        string skillPresentationKey,
        EAnimationLogicState animationIntent,
        IEnumerable<BattleHitPresentationSnapshot> hits,
        BattlePresentationFacts facts,
        ActionIdentity standardAction = default,
        DRPGBattleActionStage standardStage = DRPGBattleActionStage.None,
        int standardStep = 0)
    {
        if (requestId <= 0)
            throw new ArgumentOutOfRangeException(nameof(requestId));

        RequestId = requestId;
        Moment = moment;
        Scope = scope;
        Actor = actor;
        Targets = Copy(targets, nameof(targets));
        TimelineRuntimeIds = Copy(timelineRuntimeIds, nameof(timelineRuntimeIds));
        SkillPresentationKey = skillPresentationKey ?? string.Empty;
        AnimationIntent = animationIntent;
        Hits = Copy(hits, nameof(hits));
        Facts = facts ?? BattlePresentationFacts.Empty;
        if (standardAction.IsValid && (scope.BattleGeneration != standardAction.BattleGeneration ||
            scope.TurnSerial != standardAction.TurnSerial || scope.ActionId != standardAction.ActionId ||
            standardStage == DRPGBattleActionStage.None || standardStep < 0))
            throw new ArgumentException("Standard leaf scope must match its complete action/stage/step.");
        StandardAction = standardAction; StandardStage = standardStage; StandardStep = standardStep;
    }

    public long RequestId { get; }
    public BattlePresentationMoment Moment { get; }
    public BattlePresentationScope Scope { get; }
    public BattleParticipantPresentationSnapshot Actor { get; }
    public IReadOnlyList<BattleParticipantPresentationSnapshot> Targets { get; }
    public IReadOnlyList<int> TimelineRuntimeIds { get; }
    public string SkillPresentationKey { get; }
    public EAnimationLogicState AnimationIntent { get; }
    public IReadOnlyList<BattleHitPresentationSnapshot> Hits { get; }
    public BattlePresentationFacts Facts { get; }
    public ActionIdentity StandardAction { get; }
    public DRPGBattleActionStage StandardStage { get; }
    public int StandardStep { get; }
    public bool IsStandardBasicAttackLeaf => StandardAction.IsValid;

    private static IReadOnlyList<T> Copy<T>(
        IEnumerable<T> source,
        string parameterName)
    {
        if (source == null)
            throw new ArgumentNullException(parameterName);
        return Array.AsReadOnly(new List<T>(source).ToArray());
    }
}
