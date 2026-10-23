using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DRPGBattlePresentationEventBridge : MonoBehaviour
{
    [SerializeField] private DRPGBattlePresentationDirector directorComponent;
    [SerializeField] private DRPGBattleUGUISurface surface;
    [SerializeField] private DRPGBattleInteractionController interaction;

    private readonly Queue<QueuedSignal> queuedSignals =
        new Queue<QueuedSignal>();
    private readonly BattlePresentationRequestFactory requestFactory =
        new BattlePresentationRequestFactory();
    private IBattlePresentationDirector configuredDirector;
    private IBattlePresentationDirector director;
    private IBattleAudioCueSink audioCueSink = NullBattleAudioCueSink.Instance;
    private DRPGBattleSession session;
    private long battleGeneration;
    private long bindingVersion;
    private bool usesDomainIdentity;
    private bool expectsPairedTimeline;
    private bool isFlushing;

    public int PendingSignalCount => queuedSignals.Count;
    public long CurrentTurnSerial { get; private set; }
    internal IReadOnlyList<BattleProjectedParticipant> AuthoritativeParticipants { get; private set; } =
        Array.Empty<BattleProjectedParticipant>();

    public void Configure(
        IBattlePresentationDirector presentationDirector,
        DRPGBattleUGUISurface presentationSurface,
        IBattleAudioCueSink cueSink,
        DRPGBattleInteractionController interactionController = null)
    {
        if (session != null)
        {
            throw new InvalidOperationException(
                "Event bridge dependencies cannot change while bound.");
        }

        configuredDirector = presentationDirector ??
            throw new ArgumentNullException(nameof(presentationDirector));
        surface = presentationSurface ??
            throw new ArgumentNullException(nameof(presentationSurface));
        audioCueSink = cueSink ?? NullBattleAudioCueSink.Instance;
        if (interactionController != null)
            interaction = interactionController;
    }

    public void Bind(DRPGBattleSession battleSession)
    {
        if (battleSession == null)
            throw new ArgumentNullException(nameof(battleSession));
        BindInternal(
            battleSession,
            battleSession.BattleGeneration,
            true);
    }

    internal void Bind(DRPGBattleSession battleSession, long generation)
    {
        BindInternal(battleSession, generation, false);
    }

    private void BindInternal(
        DRPGBattleSession battleSession,
        long generation,
        bool useDomainIdentity)
    {
        if (battleSession == null)
            throw new ArgumentNullException(nameof(battleSession));
        if (generation <= 0L)
            throw new ArgumentOutOfRangeException(nameof(generation));

        Unbind();
        director = configuredDirector ?? directorComponent;
        if (director == null)
            throw new InvalidOperationException(
                "A presentation director is required for event translation.");
        if (surface == null)
            throw new InvalidOperationException(
                "A UGUI surface is required for event translation.");

        session = battleSession;
        battleGeneration = generation;
        usesDomainIdentity = useDomainIdentity;
        CurrentTurnSerial = ResolveCurrentTurnSerial(battleSession);
        bindingVersion++;
        battleSession.CurrentActorChanged += OnCurrentActorChanged;
        battleSession.TimelineChanged += OnTimelineChanged;
        battleSession.ParticipantChanged += OnParticipantChanged;
        battleSession.ProgressChanged += OnProgressChanged;
        battleSession.CommandPreviewed += OnCommandPreviewed;
        battleSession.BattleEnded += OnBattleEnded;
        battleSession.FormationChanged += OnFormationChanged;
        battleSession.ReinforcementsChanged += OnReinforcementsChanged;
        battleSession.EnemyPlansChanged += OnEnemyPlansChanged;
        battleSession.EnemyTargetDisclosureChanged +=
            OnEnemyTargetDisclosureChanged;
        surface.CanonicalProjectionClosing += OnCanonicalProjectionClosing;
        battleSession.ActionLedgerCommitted += OnActionLedgerCommitted;
        CaptureAuthoritativeParticipants();
    }

    public void Unbind()
    {
        if (surface != null)
            surface.CanonicalProjectionClosing -= OnCanonicalProjectionClosing;
        if (session != null)
        {
            session.CurrentActorChanged -= OnCurrentActorChanged;
            session.TimelineChanged -= OnTimelineChanged;
            session.ParticipantChanged -= OnParticipantChanged;
            session.ActionLedgerCommitted -= OnActionLedgerCommitted;
            session.ProgressChanged -= OnProgressChanged;
            session.CommandPreviewed -= OnCommandPreviewed;
            session.BattleEnded -= OnBattleEnded;
            session.FormationChanged -= OnFormationChanged;
            session.ReinforcementsChanged -= OnReinforcementsChanged;
            session.EnemyPlansChanged -= OnEnemyPlansChanged;
            session.EnemyTargetDisclosureChanged -=
                OnEnemyTargetDisclosureChanged;
        }

        bindingVersion++;
        queuedSignals.Clear();
        AuthoritativeParticipants = Array.Empty<BattleProjectedParticipant>();
        session = null;
        director = null;
        battleGeneration = 0L;
        CurrentTurnSerial = 0L;
        usesDomainIdentity = false;
        expectsPairedTimeline = false;
        isFlushing = false;
    }

    public void FlushQueuedSignals()
    {
        if (isFlushing || queuedSignals.Count == 0)
            return;

        isFlushing = true;
        int dispatchCount = queuedSignals.Count;
        try
        {
            for (int i = 0; i < dispatchCount; i++)
            {
                if (queuedSignals.Count == 0)
                    break;
                Dispatch(queuedSignals.Dequeue());
            }
        }
        finally
        {
            isFlushing = false;
        }
    }

    private void OnCurrentActorChanged(BattleParticipantRuntime actor)
    {
        if (session == null || actor == null)
            return;

        if (usesDomainIdentity)
        {
            ActorTurnToken token = session.CurrentActorTurnToken;
            if (!token.IsValid ||
                token.BattleGeneration != battleGeneration ||
                token.Actor.Value != actor.RuntimeId)
            {
                Debug.LogError(
                    "The presentation bridge received a current actor without " +
                    "the matching domain turn token.",
                    this);
                return;
            }
            CurrentTurnSerial = token.TurnSerial;
        }
        else
        {
            CurrentTurnSerial++;
        }
        expectsPairedTimeline = true;
        try
        {
            DRPGBattlePresentationSnapshot snapshot =
                DRPGBattlePresentationSnapshot.Capture(session);
            BattlePresentationRequest request =
                requestFactory.CreateTurnActivated(
                    BattlePresentationRequestIds.Next(),
                    battleGeneration,
                    CurrentTurnSerial,
                    session);
            queuedSignals.Enqueue(QueuedSignal.TurnActivated(
                bindingVersion,
                snapshot,
                request));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private long ResolveCurrentTurnSerial(DRPGBattleSession battleSession)
    {
        if (!usesDomainIdentity)
            return battleSession.CurrentActor != null ? 1L : 0L;

        ActorTurnToken token = battleSession.CurrentActorTurnToken;
        return token.IsValid &&
            token.BattleGeneration == battleGeneration
                ? token.TurnSerial
                : 0L;
    }

    private void OnTimelineChanged()
    {
        if (session == null)
            return;
        if (expectsPairedTimeline && session.CurrentActor != null)
        {
            expectsPairedTimeline = false;
            return;
        }

        queuedSignals.Enqueue(QueuedSignal.Timeline(
            bindingVersion,
            DRPGBattlePresentationSnapshot.Capture(session)));
    }

    private void OnParticipantChanged(BattleParticipantRuntime participant)
    {
        if (session == null)
            return;
        CaptureAuthoritativeParticipants();
        queuedSignals.Enqueue(QueuedSignal.Combatants(
            bindingVersion,
            DRPGBattlePresentationSnapshot.Capture(session)));
    }

    private void CaptureAuthoritativeParticipants()
    {
        var values = new List<BattleProjectedParticipant>();
        if (session != null)
            foreach (var participant in session.Participants)
                values.Add(new BattleProjectedParticipant(participant.RuntimeId,
                    participant.CurrentHp, participant.EffectiveMaxHp));
        AuthoritativeParticipants = values.AsReadOnly();
    }

    private void OnActionLedgerCommitted(DRPGCommittedActionFacts facts)
    {
        if (session == null || facts.Identity.BattleGeneration != battleGeneration) return;
        CaptureAuthoritativeParticipants();
        // Dedicated domain channel: never replay legacy participant/reaction
        // events and never publish the final HP as a presentation cursor.
        surface.DeferCanonicalProjection("ActionLedgerCommitted");
    }

    private void OnProgressChanged(DRPGProgressMutationResult mutation)
    {
        if (session == null)
            return;
        queuedSignals.Enqueue(QueuedSignal.Combatants(
            bindingVersion,
            DRPGBattlePresentationSnapshot.Capture(session)));
    }

    private void OnFormationChanged()
    {
        if (session == null)
            return;
        DRPGBattlePresentationSnapshot snapshot =
            DRPGBattlePresentationSnapshot.Capture(session);
        queuedSignals.Enqueue(QueuedSignal.Combatants(bindingVersion, snapshot));
        queuedSignals.Enqueue(QueuedSignal.Timeline(bindingVersion, snapshot));
    }

    private void OnReinforcementsChanged()
    {
        if (session == null)
            return;
        queuedSignals.Enqueue(QueuedSignal.Combatants(
            bindingVersion,
            DRPGBattlePresentationSnapshot.Capture(session)));
    }

    private void OnEnemyPlansChanged()
    {
        if (session == null)
            return;
        queuedSignals.Enqueue(QueuedSignal.EnemyPlans(
            bindingVersion,
            DRPGBattlePresentationSnapshot.Capture(session)));
    }

    private void OnEnemyTargetDisclosureChanged(
        DRPGEnemyActionPlanSnapshot disclosure)
    {
        if (session == null || disclosure.DisclosedTargetRuntimeId <= 0)
            return;
        if (!TryResolveRequestTurnSerial(out long requestTurnSerial))
            return;

        try
        {
            DRPGBattlePresentationSnapshot snapshot =
                DRPGBattlePresentationSnapshot.Capture(session);
            BattlePresentationRequest request =
                requestFactory.CreateEnemyIntentTargetingChanged(
                    BattlePresentationRequestIds.Next(),
                    battleGeneration,
                    requestTurnSerial,
                    disclosure,
                    session);
            queuedSignals.Enqueue(QueuedSignal.EnemyIntent(
                bindingVersion,
                snapshot,
                request));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private void OnCommandPreviewed(DRPGBattleCommand command)
    {
        if (session == null || command == null)
            return;
        BattleParticipantRuntime actor = FindParticipant(command.ActorRuntimeId);
        if (actor == null || actor.Side != DRPGBattleSide.Enemy)
            return;
        if (!TryResolveRequestTurnSerial(out long requestTurnSerial))
            return;

        try
        {
            BattlePresentationRequest request =
                requestFactory.CreateTargetingChanged(
                    BattlePresentationRequestIds.Next(),
                    battleGeneration,
                    requestTurnSerial,
                    command,
                    session);
            queuedSignals.Enqueue(QueuedSignal.EnemyIntent(
                bindingVersion,
                DRPGBattlePresentationSnapshot.Capture(session),
                request));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private bool TryResolveRequestTurnSerial(out long requestTurnSerial)
    {
        if (!usesDomainIdentity)
        {
            requestTurnSerial = Math.Max(1L, CurrentTurnSerial);
            return true;
        }

        ActorTurnToken token = session != null
            ? session.CurrentActorTurnToken
            : default;
        if (!token.IsValid ||
            token.BattleGeneration != battleGeneration ||
            token.TurnSerial != CurrentTurnSerial)
        {
            requestTurnSerial = 0L;
            return false;
        }

        requestTurnSerial = token.TurnSerial;
        return true;
    }

    private void OnBattleEnded(DRPGBattleResult result)
    {
        if (session == null)
            return;
        queuedSignals.Enqueue(QueuedSignal.BattleEnded(
            bindingVersion,
            DRPGBattlePresentationSnapshot.Capture(session),
            result));
    }

    private void Dispatch(QueuedSignal signal)
    {
        if (session == null || signal.BindingVersion != bindingVersion)
            return;
        if (surface.DeferCanonicalProjection("EventBridge." + signal.Kind))
            return;

        switch (signal.Kind)
        {
            case SignalKind.Timeline:
                surface.RenderTimelineImmediate(signal.Snapshot.Timeline);
                break;
            case SignalKind.Combatants:
                surface.RenderCombatants(signal.Snapshot);
                ApplyPublicEnemyIntentFeedback(signal.Snapshot);
                break;
            case SignalKind.EnemyPlans:
                DispatchEnemyPlans(signal);
                break;
            case SignalKind.TurnActivated:
                DispatchTurnActivated(signal);
                break;
            case SignalKind.EnemyIntent:
                DispatchEnemyIntent(signal);
                break;
            case SignalKind.BattleEnded:
                DispatchBattleEnded(signal);
                break;
        }
    }

    private void DispatchTurnActivated(QueuedSignal signal)
    {
        surface.ClearTargetFeedback();
        surface.RenderCombatants(signal.Snapshot);
        interaction?.OnTurnActivated();
        ApplyPublicEnemyIntentFeedback(signal.Snapshot);
        BattlePresentationRun run = PlayFailOpen(signal.Request);
        if (run == null)
        {
            surface.RenderTimelineImmediate(signal.Snapshot.Timeline);
            return;
        }

        long capturedBinding = bindingVersion;
        long capturedEpoch = surface.ProjectionEpoch;
        run.WhenFinished(_ =>
        {
            if (session != null && bindingVersion == capturedBinding &&
                surface.ProjectionEpoch == capturedEpoch)
                surface.RenderTimelineImmediate(signal.Snapshot.Timeline);
        });
    }

    private void DispatchEnemyIntent(QueuedSignal signal)
    {
        surface.ClearTargetFeedback();
        ApplyPublicEnemyIntentFeedback(signal.Snapshot);
        if (signal.Request.Targets.Count > 0)
        {
            BattleParticipantPresentationSnapshot target =
                signal.Request.Targets[0];
            surface.SetTargetPrompt("敌方锁定 · " + target.DisplayName);
        }
        PlayFailOpen(signal.Request);
    }

    private void DispatchEnemyPlans(QueuedSignal signal)
    {
        surface.ClearTargetFeedback();
        surface.RenderCombatants(signal.Snapshot);
        surface.RenderTimelineImmediate(signal.Snapshot.Timeline);
        ApplyPublicEnemyIntentFeedback(signal.Snapshot);
    }

    private void ApplyPublicEnemyIntentFeedback(
        DRPGBattlePresentationSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        var targets = new HashSet<int>();
        string firstTargetName = string.Empty;
        for (int i = 0; i < snapshot.Enemies.Count; i++)
        {
            int targetRuntimeId =
                snapshot.Enemies[i].DisclosedTargetRuntimeId;
            if (targetRuntimeId <= 0 || !targets.Add(targetRuntimeId))
                continue;
            surface.ApplyTargetFeedback(
                targetRuntimeId,
                DRPGBattleTargetFeedback.EnemyIntent);
            if (string.IsNullOrEmpty(firstTargetName))
            {
                BattleParticipantRuntime target =
                    FindParticipant(targetRuntimeId);
                firstTargetName = target?.Profile?.DisplayName ?? string.Empty;
            }
        }

        if (targets.Count == 1 && !string.IsNullOrEmpty(firstTargetName))
        {
            surface.SetTargetPrompt(
                "\u654C\u65B9\u9501\u5B9A \u00B7 " + firstTargetName);
        }
        else if (targets.Count > 1)
        {
            surface.SetTargetPrompt(
                "\u654C\u65B9\u9501\u5B9A \u00D7" + targets.Count);
        }
    }

    private void DispatchBattleEnded(QueuedSignal signal)
    {
        interaction?.Suspend();
        surface.RenderCombatants(signal.Snapshot);
        if (signal.Result.HasValue &&
            signal.Result.Value.Outcome == DRPGBattleOutcome.Victory)
        {
            audioCueSink.Play(new BattleAudioCueRequest(BattleAudioCue.Victory));
        }
        else if (signal.Result.HasValue &&
            signal.Result.Value.Outcome == DRPGBattleOutcome.Defeat)
        {
            audioCueSink.Play(new BattleAudioCueRequest(BattleAudioCue.Defeat));
        }
    }

    private BattlePresentationRun PlayFailOpen(
        BattlePresentationRequest request)
    {
        try
        {
            return director?.Play(request);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return null;
        }
    }

    private BattleParticipantRuntime FindParticipant(int runtimeId)
    {
        if (session == null)
            return null;
        for (int i = 0; i < session.Participants.Count; i++)
        {
            BattleParticipantRuntime participant = session.Participants[i];
            if (participant.RuntimeId == runtimeId)
                return participant;
        }
        return null;
    }

    private void LateUpdate()
    {
        FlushQueuedSignals();
    }

    private void OnCanonicalProjectionClosing()
    {
        // Discard stale mutable invalidations, not callbacks produced by the
        // new canonical flush. Existing turn callbacks also fail their epoch.
        queuedSignals.Clear();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private enum SignalKind
    {
        Timeline,
        Combatants,
        EnemyPlans,
        TurnActivated,
        EnemyIntent,
        BattleEnded
    }

    private sealed class QueuedSignal
    {
        private QueuedSignal(
            SignalKind kind,
            long bindingVersion,
            DRPGBattlePresentationSnapshot snapshot,
            BattlePresentationRequest request,
            DRPGBattleResult? result)
        {
            Kind = kind;
            BindingVersion = bindingVersion;
            Snapshot = snapshot;
            Request = request;
            Result = result;
        }

        public SignalKind Kind { get; }
        public long BindingVersion { get; }
        public DRPGBattlePresentationSnapshot Snapshot { get; }
        public BattlePresentationRequest Request { get; }
        public DRPGBattleResult? Result { get; }

        public static QueuedSignal Timeline(
            long bindingVersion,
            DRPGBattlePresentationSnapshot snapshot)
        {
            return new QueuedSignal(
                SignalKind.Timeline,
                bindingVersion,
                snapshot,
                null,
                null);
        }

        public static QueuedSignal Combatants(
            long bindingVersion,
            DRPGBattlePresentationSnapshot snapshot)
        {
            return new QueuedSignal(
                SignalKind.Combatants,
                bindingVersion,
                snapshot,
                null,
                null);
        }

        public static QueuedSignal TurnActivated(
            long bindingVersion,
            DRPGBattlePresentationSnapshot snapshot,
            BattlePresentationRequest request)
        {
            return new QueuedSignal(
                SignalKind.TurnActivated,
                bindingVersion,
                snapshot,
                request,
                null);
        }

        public static QueuedSignal EnemyPlans(
            long bindingVersion,
            DRPGBattlePresentationSnapshot snapshot)
        {
            return new QueuedSignal(
                SignalKind.EnemyPlans,
                bindingVersion,
                snapshot,
                null,
                null);
        }

        public static QueuedSignal EnemyIntent(
            long bindingVersion,
            DRPGBattlePresentationSnapshot snapshot,
            BattlePresentationRequest request)
        {
            return new QueuedSignal(
                SignalKind.EnemyIntent,
                bindingVersion,
                snapshot,
                request,
                null);
        }

        public static QueuedSignal BattleEnded(
            long bindingVersion,
            DRPGBattlePresentationSnapshot snapshot,
            DRPGBattleResult result)
        {
            return new QueuedSignal(
                SignalKind.BattleEnded,
                bindingVersion,
                snapshot,
                null,
                result);
        }
    }
}
