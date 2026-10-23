using System;
using System.Collections.Generic;

public sealed class BattlePresentationRequestFactory
{
    public BattlePresentationRequest CreateTurnActivated(
        long requestId,
        long battleGeneration,
        long turnSerial,
        DRPGBattleSession session)
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));
        if (session.CurrentActor == null)
            throw new InvalidOperationException(
                "Turn activation requires a current actor.");

        return CreateMoment(
            requestId,
            BattlePresentationMoment.TurnActivated,
            battleGeneration,
            turnSerial,
            session.CurrentActor,
            Array.Empty<BattleParticipantRuntime>(),
            null,
            session);
    }

    public BattlePresentationRequest CreateTargetingChanged(
        long requestId,
        long battleGeneration,
        long turnSerial,
        DRPGBattleCommand command,
        DRPGBattleSession session)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));
        if (session == null)
            throw new ArgumentNullException(nameof(session));

        BattleParticipantRuntime actor = FindParticipant(
            session,
            command.ActorRuntimeId);
        BattleParticipantRuntime target = command.TargetRuntimeId > 0
            ? FindParticipant(session, command.TargetRuntimeId)
            : null;
        return CreateMoment(
            requestId,
            BattlePresentationMoment.TargetingChanged,
            battleGeneration,
            turnSerial,
            actor,
            new[] { target },
            command.Skill,
            session);
    }

    public BattlePresentationRequest CreateEnemyIntentTargetingChanged(
        long requestId,
        long battleGeneration,
        long turnSerial,
        DRPGEnemyActionPlanSnapshot disclosure,
        DRPGBattleSession session)
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));
        if (disclosure.ActorRuntimeId <= 0 ||
            disclosure.DisclosedTargetRuntimeId <= 0)
        {
            throw new ArgumentException(
                "Enemy intent disclosure requires an actor and public target.",
                nameof(disclosure));
        }

        BattleParticipantRuntime actor = FindParticipant(
            session,
            disclosure.ActorRuntimeId);
        BattleParticipantRuntime target = FindParticipant(
            session,
            disclosure.DisclosedTargetRuntimeId);
        return CreateMoment(
            requestId,
            BattlePresentationMoment.TargetingChanged,
            battleGeneration,
            turnSerial,
            actor,
            new[] { target },
            null,
            session);
    }

    public BattlePresentationRequest CreateWindup(
        long requestId,
        long battleGeneration,
        long turnSerial,
        DRPGBattlePreparedAction action,
        DRPGBattleSession session)
    {
        Validate(action, session);
        return CreateForAction(
            requestId,
            BattlePresentationMoment.ActionWindup,
            battleGeneration,
            turnSerial,
            action,
            session,
            Array.Empty<BattleHitPresentationSnapshot>(),
            BattlePresentationFacts.Empty);
    }

    public BattlePresentationRequest CreateImpact(
        long requestId,
        long battleGeneration,
        long turnSerial,
        DRPGBattleActionResolution resolution,
        DRPGBattleSession session)
    {
        if (resolution == null)
            throw new ArgumentNullException(nameof(resolution));
        Validate(resolution.PreparedAction, session);

        var hits = new List<BattleHitPresentationSnapshot>(
            resolution.Hits.Count);
        for (int i = 0; i < resolution.Hits.Count; i++)
        {
            DRPGBattleHitResult hit = resolution.Hits[i];
            if (hit == null)
                throw new ArgumentException(
                    "Resolution hits cannot contain null.",
                    nameof(resolution));
            hits.Add(BattleHitPresentationSnapshot.From(
                hit,
                FindParticipant(session, hit.TargetRuntimeId)));
        }

        BattlePresentationFacts facts = CreateImpactFacts(hits);
        return CreateForAction(
            requestId,
            BattlePresentationMoment.ImpactResolved,
            battleGeneration,
            turnSerial,
            resolution.PreparedAction,
            session,
            hits,
            facts);
    }

    public BattlePresentationRequest CreateSettled(
        long requestId,
        long battleGeneration,
        long turnSerial,
        DRPGBattlePreparedAction action,
        DRPGBattleSession session)
    {
        Validate(action, session);
        return CreateForAction(
            requestId,
            BattlePresentationMoment.ActionSettled,
            battleGeneration,
            turnSerial,
            action,
            session,
            Array.Empty<BattleHitPresentationSnapshot>(),
            BattlePresentationFacts.Empty);
    }

    private static BattlePresentationRequest CreateForAction(
        long requestId,
        BattlePresentationMoment moment,
        long battleGeneration,
        long turnSerial,
        DRPGBattlePreparedAction action,
        DRPGBattleSession session,
        IReadOnlyList<BattleHitPresentationSnapshot> hits,
        BattlePresentationFacts facts)
    {
        BattleParticipantRuntime actor = FindParticipant(
            session,
            action.ActorRuntimeId);
        var targets = new List<BattleParticipantPresentationSnapshot>();
        var targetIds = new HashSet<int>();
        AddTarget(session, action.TargetRuntimeId, targetIds, targets);
        for (int i = 0; i < hits.Count; i++)
            AddTarget(session, hits[i].TargetRuntimeId, targetIds, targets);

        return new BattlePresentationRequest(
            requestId,
            moment,
            new BattlePresentationScope(
                battleGeneration,
                turnSerial,
                action.ActionId),
            BattleParticipantPresentationSnapshot.From(actor),
            targets,
            CreateTimelineSnapshot(session),
            action.Spec.PresentationKey,
            action.Spec.AnimationIntent,
            hits,
            facts);
    }

    private static BattlePresentationRequest CreateMoment(
        long requestId,
        BattlePresentationMoment moment,
        long battleGeneration,
        long turnSerial,
        BattleParticipantRuntime actor,
        IReadOnlyList<BattleParticipantRuntime> targets,
        DRPGSkillSO skill,
        DRPGBattleSession session)
    {
        var targetSnapshots = new List<BattleParticipantPresentationSnapshot>();
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != null)
            {
                targetSnapshots.Add(
                    BattleParticipantPresentationSnapshot.From(targets[i]));
            }
        }

        return new BattlePresentationRequest(
            requestId,
            moment,
            new BattlePresentationScope(battleGeneration, turnSerial, 0L),
            actor != null
                ? BattleParticipantPresentationSnapshot.From(actor)
                : null,
            targetSnapshots,
            CreateTimelineSnapshot(session),
            skill != null ? skill.PresentationKey : string.Empty,
            skill != null ? skill.AnimationIntent : EAnimationLogicState.None,
            Array.Empty<BattleHitPresentationSnapshot>(),
            BattlePresentationFacts.Empty);
    }

    private static IReadOnlyList<int> CreateTimelineSnapshot(
        DRPGBattleSession session)
    {
        var ids = new List<int>();
        var seen = new HashSet<int>();
        IReadOnlyList<DRPGBattleParticipantViewModel> timeline =
            new DRPGBattleViewModel(session).Timeline;
        for (int i = 0; i < timeline.Count; i++)
        {
            int runtimeId = timeline[i].RuntimeId;
            if (runtimeId > 0 && seen.Add(runtimeId))
                ids.Add(runtimeId);
        }
        return ids;
    }

    private static BattlePresentationFacts CreateImpactFacts(
        IReadOnlyList<BattleHitPresentationSnapshot> hits)
    {
        var damageBrokenTargets = new List<int>();
        var defeatedTargets = new List<int>();
        var redirectedTargets = new List<int>();
        for (int i = 0; i < hits.Count; i++)
        {
            BattleHitPresentationSnapshot hit = hits[i];
            if (hit.TriggeredDamageBroken &&
                hit.TargetRosterRole == DRPGBattleRosterRole.PartyMember)
            {
                damageBrokenTargets.Add(hit.TargetRuntimeId);
            }
            if (hit.WasDefeated)
                defeatedTargets.Add(hit.TargetRuntimeId);
            if (hit.WasRedirected)
                redirectedTargets.Add(hit.TargetRuntimeId);
        }

        return new BattlePresentationFacts(
            damageBrokenTargets,
            defeatedTargets,
            redirectedTargets);
    }

    private static void AddTarget(
        DRPGBattleSession session,
        int runtimeId,
        ISet<int> targetIds,
        ICollection<BattleParticipantPresentationSnapshot> targets)
    {
        if (runtimeId <= 0)
            return;
        if (!targetIds.Add(runtimeId))
            return;
        targets.Add(BattleParticipantPresentationSnapshot.From(
            FindParticipant(session, runtimeId)));
    }

    private static string CreatePresentationKey(DRPGBattleCommand command)
    {
        if (command == null)
            return string.Empty;
        if (command.Skill != null)
            return command.Skill.PresentationKey;
        if (command.Kind == DRPGBattleCommandKind.Item &&
            command.ItemDefinition != null)
        {
            return "item:" + command.ItemDefinition.ItemId;
        }
        return command.Kind.ToString().ToLowerInvariant();
    }

    private static BattleParticipantRuntime FindParticipant(
        DRPGBattleSession session,
        int runtimeId)
    {
        IReadOnlyList<BattleParticipantRuntime> participants =
            session.Participants;
        for (int i = 0; i < participants.Count; i++)
        {
            if (participants[i].RuntimeId == runtimeId)
                return participants[i];
        }

        throw new InvalidOperationException(
            "Presentation participant was not found: " + runtimeId);
    }

    private static void Validate(
        DRPGBattlePreparedAction action,
        DRPGBattleSession session)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));
        if (session == null)
            throw new ArgumentNullException(nameof(session));
    }
}
