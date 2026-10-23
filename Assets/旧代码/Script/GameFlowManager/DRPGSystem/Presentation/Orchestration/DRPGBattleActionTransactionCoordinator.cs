using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DRPGBattleActionTransactionCoordinator : MonoBehaviour
{
    [SerializeField] private DRPGBattlePresentationDirector directorComponent;
    [SerializeField] private BattleInteractiveCombatView interactiveView;

    private readonly BattlePresentationRequestFactory requestFactory =
        new BattlePresentationRequestFactory();
    private IBattlePresentationDirector configuredDirector;
    private IBattlePresentationDirector director;
    private DRPGBattleUGUISurface interactiveSurface;
    private IBattleAudioCueSink interactiveAudio = NullBattleAudioCueSink.Instance;
    private DRPGBattleSession session;
    private DRPGBattlePreparedAction activeAction;
    private ActionIdentity activeActionIdentity;
    private DRPGBattleActionResolution activeResolution;
    private BattleInteractiveActionRun activeInteractiveRun;
    private BattleParticipantRuntime interactiveActor;
    private BattleParticipantRuntime interactiveTarget;
    private readonly List<BattleFreeStrikeTargetAnchor> freeStrikeTargets =
        new List<BattleFreeStrikeTargetAnchor>();
    private IDisposable interactiveInputLease;
    private long battleGeneration;
    private long turnSerial;
    private long activeActionTurnSerial;
    private long bindingVersion;
    private bool usesDomainIdentity;
    private int interactiveStartedFrame = -1;
    private bool impactRequested;
    private bool completionRequested;
    private bool finishingInteractive;

    public bool HasActiveAction => activeAction != null;
    public bool HasActiveInteractiveAction => activeInteractiveRun != null;
    public DRPGBattleInteractionKind? ActiveInteractionKind =>
        activeInteractiveRun != null
            ? activeInteractiveRun.Kind
            : (DRPGBattleInteractionKind?)null;

    public void ConfigureInteractiveView(
        BattleInteractiveCombatView value,
        DRPGBattleUGUISurface surface = null,
        IBattleAudioCueSink audioCueSink = null)
    {
        if (session != null)
        {
            throw new InvalidOperationException(
                "Interactive presentation dependencies cannot change while bound.");
        }

        interactiveView = value;
        interactiveSurface = surface;
        interactiveAudio = audioCueSink ?? NullBattleAudioCueSink.Instance;
    }

    public void ConfigureDirector(IBattlePresentationDirector value)
    {
        if (session != null)
        {
            throw new InvalidOperationException(
                "The presentation director cannot change while bound.");
        }
        configuredDirector = value ?? throw new ArgumentNullException(nameof(value));
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

        IBattlePresentationDirector resolvedDirector = ResolveDirector();
        Unbind(false);
        if (!battleSession.TryAttachStagedActionConsumer(
                this,
                OnActionPrepared))
        {
            throw new InvalidOperationException(
                "A DRPG battle session already has a staged-action owner.");
        }

        session = battleSession;
        director = resolvedDirector;
        battleGeneration = generation;
        usesDomainIdentity = useDomainIdentity;
        turnSerial = ResolveCurrentTurnSerial(battleSession);
        bindingVersion++;
        battleSession.CurrentActorChanged += OnCurrentActorChanged;
    }

    public void Unbind(bool abortIncompleteAction)
    {
        DRPGBattleSession previousSession = session;
        DRPGBattlePreparedAction previousAction = activeAction;
        IBattlePresentationDirector previousDirector = director;
        BattlePresentationScope previousScope = previousAction != null
            ? new BattlePresentationScope(
                battleGeneration,
                activeActionTurnSerial,
                previousAction.ActionId)
            : default;

        if (previousSession != null)
        {
            previousSession.CurrentActorChanged -= OnCurrentActorChanged;
            previousSession.DetachStagedActionConsumer(this);
        }

        bindingVersion++;
        session = null;
        director = null;
        battleGeneration = 0L;
        turnSerial = 0L;
        usesDomainIdentity = false;
        ClearActiveAction();

        if (previousAction != null)
        {
            previousDirector?.CancelScope(
                previousScope,
                BattlePresentationCancelReason.ScopeEnded);
            if (abortIncompleteAction && previousSession != null &&
                (ReferenceEquals(previousSession.ActiveAction, previousAction) ||
                 ReferenceEquals(
                     previousSession.PendingFinalizationAction,
                     previousAction)))
            {
                previousSession.Abort();
            }
        }
    }

    public bool SubmitGuardInput()
    {
        return activeInteractiveRun != null &&
            activeInteractiveRun.Kind == DRPGBattleInteractionKind.Defense &&
            activeInteractiveRun.SubmitGuard();
    }

    public bool SubmitFreeStrike(DRPGStrikeGrade grade)
    {
        if (grade == DRPGStrikeGrade.Miss || interactiveTarget == null)
        {
            return SubmitFreeStrikeContacts(
                Array.Empty<DRPGFreeStrikeBodyContact>());
        }
        return SubmitFreeStrikeContacts(new[]
        {
            new DRPGFreeStrikeBodyContact(
                interactiveTarget.RuntimeId,
                0f,
                new Vector2(0.5f, 0.5f),
                1f,
                0,
                DRPGFreeStrikeImpactRole.Primary)
        });
    }

    private bool SubmitFreeStrikeContacts(
        IReadOnlyList<DRPGFreeStrikeBodyContact> contacts)
    {
        if (activeInteractiveRun == null ||
            activeInteractiveRun.Kind != DRPGBattleInteractionKind.FreeStrike ||
            activeAction == null || session == null ||
            !activeInteractiveRun.TryAcceptStrike())
        {
            return false;
        }

        if (!session.TryResolveFreeStrikeInput(
                activeAction,
                contacts,
                out DRPGFreeStrikeInputResult result,
                out string resolveError))
        {
            interactiveView?.ShowInputRejected(resolveError);
            return false;
        }

        PlayInteractiveAudio(BattleAudioCue.Attack);
        for (int i = 0; i < result.Impacts.Count; i++)
        {
            PlayInteractiveImpact(result.Impacts[i].Hit);
        }

        interactiveView?.ShowStrikeResult(result.HighestStrikeGrade);
        if (result.Impacts.Count == 0)
            PlayInteractiveAudio(BattleAudioCue.Miss);
        RefreshInteractiveResourceSnapshot();

        if (!HasLivingOpponent(session, interactiveActor))
        {
            activeInteractiveRun.End(
                DRPGBattleInteractionEndReason.Completed);
        }
        else if (result.ActorPostureExhausted)
        {
            activeInteractiveRun.End(
                DRPGBattleInteractionEndReason.PostureExhausted);
        }
        else if (result.WindowResourceExhausted)
        {
            activeInteractiveRun.End(
                DRPGBattleInteractionEndReason.ResourceExhausted);
        }

        if (activeInteractiveRun.IsComplete)
            FinishInteractiveAction(activeInteractiveRun.EndReason);
        return true;
    }

    public bool SubmitFreeStrikeInput(Vector2 screenPosition)
    {
        DRPGFreeStrikeWindowContext window = session?.ActiveFreeStrikeWindow;
        if (activeInteractiveRun == null || interactiveView == null ||
            window == null ||
            !interactiveView.TryCreateStrikeSample(
                screenPosition,
                window.Spec,
                out _,
                out DRPGFreeStrikeBodyContact[] contacts))
        {
            return false;
        }

        return SubmitFreeStrikeContacts(contacts);
    }

    public void TickInteractive(float unscaledDeltaTime)
    {
        if (unscaledDeltaTime < 0f)
            throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));
        if (activeInteractiveRun == null || finishingInteractive)
            return;

        if (session == null || !session.IsActive)
        {
            activeInteractiveRun.End(DRPGBattleInteractionEndReason.BattleEnded);
            FinishInteractiveAction(activeInteractiveRun.EndReason);
            return;
        }

        activeInteractiveRun.Advance(unscaledDeltaTime);
        if (activeInteractiveRun.Kind == DRPGBattleInteractionKind.Defense)
            ResolvePendingGuardImpacts();

        float postureNormalized = interactiveActor != null
            ? interactiveActor.CurrentPosture /
                Mathf.Max(1f, interactiveActor.EffectiveMaxPosture)
            : 0f;
        interactiveView?.UpdateWindow(
            activeInteractiveRun,
            postureNormalized);
        RefreshInteractiveResourceSnapshot();

        if (activeInteractiveRun != null && activeInteractiveRun.IsComplete)
            FinishInteractiveAction(activeInteractiveRun.EndReason);
    }

    private void RefreshInteractiveResourceSnapshot()
    {
        if (session == null || activeAction == null ||
            activeAction.Spec.ExecutionMode !=
                DRPGSkillExecutionMode.FreeStrike)
        {
            return;
        }
        if (session.TryGetInteractiveInputResourceSnapshot(
                activeAction,
                out DRPGInteractiveInputResourceSnapshot snapshot))
        {
            interactiveView?.UpdateInputResources(snapshot);
        }
    }

    private IBattlePresentationDirector ResolveDirector()
    {
        if (configuredDirector != null)
            return configuredDirector;
        if (directorComponent != null)
            return directorComponent;
        throw new InvalidOperationException(
            "A DRPG battle presentation director is required.");
    }

    private void OnCurrentActorChanged(BattleParticipantRuntime actor)
    {
        if (session == null || actor == null)
            return;
        if (!usesDomainIdentity)
        {
            turnSerial++;
            return;
        }

        ActorTurnToken token = session.CurrentActorTurnToken;
        if (!token.IsValid ||
            token.BattleGeneration != battleGeneration ||
            token.Actor.Value != actor.RuntimeId)
        {
            FailDomainTransaction(
                "The action coordinator received a current actor without " +
                "the matching domain turn token.");
            return;
        }
        turnSerial = token.TurnSerial;
    }

    private void OnActionPrepared(DRPGBattlePreparedAction action)
    {
        if (session == null || action == null)
            return;
        if (activeAction != null)
        {
            FailDomainTransaction(
                "A new action was prepared before the previous action settled. " +
                $"active={activeAction.ActionId}/" +
                $"{activeAction.Command.Kind}; incoming={action.ActionId}/" +
                $"{action.Command.Kind}; phase={session.Phase}; " +
                $"actor={session.CurrentActor?.RuntimeId ?? 0}.");
            return;
        }

        ActionIdentity domainActionIdentity = session.CurrentActionIdentity;
        if (usesDomainIdentity &&
            (!domainActionIdentity.IsValid ||
             domainActionIdentity.BattleGeneration != battleGeneration ||
             domainActionIdentity.TurnSerial != turnSerial ||
             domainActionIdentity.ActionId != action.ActionId))
        {
            FailDomainTransaction(
                "The prepared action does not carry the Session domain identity.");
            return;
        }

        if (IsSilentOrdinaryEnemyWait(action))
        {
            SettleSilentOrdinaryEnemyWait(action);
            return;
        }

        activeAction = action;
        activeActionTurnSerial = usesDomainIdentity
            ? domainActionIdentity.TurnSerial
            : turnSerial;
        activeActionIdentity = usesDomainIdentity
            ? domainActionIdentity
            : new ActionIdentity(
                battleGeneration,
                activeActionTurnSerial,
                action.ActionId);
        activeResolution = null;
        impactRequested = false;
        completionRequested = false;
        long capturedBinding = bindingVersion;
        DRPGBattleSession capturedSession = session;
        ActionIdentity capturedActionIdentity = activeActionIdentity;

        BattlePresentationRequest request;
        try
        {
            request = requestFactory.CreateWindup(
                BattlePresentationRequestIds.Next(),
                capturedActionIdentity.BattleGeneration,
                activeActionTurnSerial,
                action,
                capturedSession);
        }
        catch (Exception exception)
        {
            FailDomainTransaction(exception.Message);
            return;
        }

        PlayGateFailOpen(
            request,
            _ => OnWindupGateReleased(
                capturedSession,
                capturedBinding,
                capturedActionIdentity,
                action));
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

    private bool IsSilentOrdinaryEnemyWait(
        DRPGBattlePreparedAction action)
    {
        return session != null && action != null &&
            action.Command.Kind == DRPGBattleCommandKind.Wait &&
            session.CurrentActor != null &&
            session.CurrentActor.Side == DRPGBattleSide.Enemy &&
            session.IsCurrentEnemyCommandFromOrdinaryIntent;
    }

    private void SettleSilentOrdinaryEnemyWait(
        DRPGBattlePreparedAction action)
    {
        DRPGBattleSession capturedSession = session;
        if (!capturedSession.TryResolveImpact(
                action,
                out _,
                out string resolveError))
        {
            FailDomainTransaction(resolveError, capturedSession);
            return;
        }

        if (!capturedSession.TryCompleteAction(
                action,
                out string completionError))
        {
            FailDomainTransaction(completionError, capturedSession);
        }
    }

    private void OnWindupGateReleased(
        DRPGBattleSession capturedSession,
        long capturedBinding,
        ActionIdentity capturedActionIdentity,
        DRPGBattlePreparedAction action)
    {
        if (!IsCurrentAction(
                capturedSession,
                capturedBinding,
                capturedActionIdentity,
                action,
                DRPGBattlePhase.PreparingAction) ||
            impactRequested)
        {
            return;
        }

        impactRequested = true;
        if (RequiresInteractiveResolution(action))
        {
            BeginInteractiveAction(
                capturedSession,
                capturedBinding,
                capturedActionIdentity,
                action);
            return;
        }

        if (!capturedSession.TryResolveImpact(
                action,
                out DRPGBattleActionResolution resolution,
                out string error))
        {
            FailDomainTransaction(error);
            return;
        }

        activeResolution = resolution;
        if (action.Command.Stage == DRPGBattleCommandStage.Telegraph)
        {
            OnImpactGateReleased(
                capturedSession,
                capturedBinding,
                capturedActionIdentity,
                action);
            return;
        }

        BattlePresentationRequest request;
        try
        {
            request = requestFactory.CreateImpact(
                BattlePresentationRequestIds.Next(),
                capturedActionIdentity.BattleGeneration,
                activeActionTurnSerial,
                resolution,
                capturedSession);
        }
        catch (Exception exception)
        {
            FailDomainTransaction(exception.Message);
            return;
        }

        PlayGateFailOpen(
            request,
            _ => OnImpactGateReleased(
                capturedSession,
                capturedBinding,
                capturedActionIdentity,
                action));
    }

    private void BeginInteractiveAction(
        DRPGBattleSession capturedSession,
        long capturedBinding,
        ActionIdentity capturedActionIdentity,
        DRPGBattlePreparedAction action)
    {
        if (!capturedSession.TryBeginInteractiveResolution(
                action,
                out string error))
        {
            FailDomainTransaction(error);
            return;
        }

        interactiveActor = FindParticipant(
            capturedSession,
            action.ActorRuntimeId);
        interactiveTarget = FindParticipant(
            capturedSession,
            action.TargetRuntimeId);
        if (interactiveActor == null || interactiveTarget == null)
        {
            FailDomainTransaction("Interactive action participants are missing.");
            return;
        }

        DRPGSkillSO interactiveSkill = action.Command.Skill;
        if (interactiveSkill == null)
        {
            FailDomainTransaction(
                "Interactive actions require a skill definition.");
            return;
        }

        DRPGFreeStrikeWindowContext freeStrikeWindow =
            action.Spec.ExecutionMode ==
                DRPGSkillExecutionMode.FreeStrike
                ? capturedSession.ActiveFreeStrikeWindow
                : null;
        if (action.Spec.ExecutionMode ==
                DRPGSkillExecutionMode.FreeStrike &&
            (freeStrikeWindow == null ||
             freeStrikeWindow.ActionId != action.ActionId))
        {
            FailDomainTransaction(
                "The free-strike domain window was not created.");
            return;
        }

        try
        {
            activeInteractiveRun =
                action.Command.Stage == DRPGBattleCommandStage.Release
                    ? BattleInteractiveActionRun
                        .CreateDefenseFromSpecWithAvailability(
                        interactiveSkill,
                        action.Spec.GuardSpec,
                        !interactiveTarget.PostureBreak.IsExposed,
                        interactiveTarget.PostureBreak.IsExposed
                            ? "PostureBreak exposure disables active defense."
                            : string.Empty)
                    : BattleInteractiveActionRun.CreateFreeStrike(
                        interactiveSkill,
                        freeStrikeWindow.Spec);
            if (activeInteractiveRun.Kind == DRPGBattleInteractionKind.Defense &&
                !interactiveTarget.IsAlive)
            {
                activeInteractiveRun.End(
                    DRPGBattleInteractionEndReason.TargetInvalid);
                FinishInteractiveAction(activeInteractiveRun.EndReason);
                return;
            }
            interactiveInputLease = director.InputGate.Acquire(
                "interactive-action:" + action.ActionId);
            interactiveStartedFrame = Time.frameCount;

            BattleImpactAnchor anchor = null;
            interactiveSurface?.TryResolveParticipantAnchor(
                interactiveTarget.RuntimeId,
                out anchor);
            if (activeInteractiveRun.Kind == DRPGBattleInteractionKind.Defense)
            {
                interactiveView?.ShowDefense(
                    action,
                    interactiveActor,
                    interactiveTarget,
                    anchor);
                interactiveView?.ClearTelegraph();
            }
            else
            {
                CollectFreeStrikeTargets(capturedSession, interactiveActor);
                // Runtime presentation requires at least one visible battlefield
                // target. Headless probes intentionally have no surface/view and
                // submit classified hits directly, so keep that path alive.
                if (freeStrikeTargets.Count == 0 && interactiveView != null)
                {
                    activeInteractiveRun.End(
                        DRPGBattleInteractionEndReason.TargetInvalid);
                    FinishInteractiveAction(activeInteractiveRun.EndReason);
                    return;
                }
                interactiveView?.ShowFreeStrike(
                    action,
                    interactiveActor,
                    freeStrikeTargets);
                RefreshInteractiveResourceSnapshot();
            }

            float postureNormalized = interactiveActor.CurrentPosture /
                Mathf.Max(1f, interactiveActor.EffectiveMaxPosture);
            interactiveView?.UpdateWindow(
                activeInteractiveRun,
                postureNormalized);
            PlayInteractiveAudio(BattleAudioCue.InteractiveWindowOpen);
        }
        catch (Exception exception)
        {
            FailDomainTransaction(exception.Message);
        }
    }

    private void ResolvePendingGuardImpacts()
    {
        if (activeInteractiveRun == null || activeAction == null ||
            session == null)
        {
            return;
        }

        var decisions = activeInteractiveRun.DrainGuardDecisions();
        for (int i = 0; i < decisions.Count; i++)
        {
            if (interactiveTarget == null || !interactiveTarget.IsAlive)
            {
                activeInteractiveRun.End(
                    DRPGBattleInteractionEndReason.TargetInvalid);
                break;
            }

            DRPGGuardGrade grade = decisions[i].Grade;
            DRPGResolvedGuardSpec guardSpec = activeAction.Spec.GuardSpec;
            if (guardSpec == null)
            {
                FailDomainTransaction(
                    "The resolved special action has no guard specification.");
                return;
            }
            float multiplier = grade == DRPGGuardGrade.Perfect
                ? 0f
                : grade == DRPGGuardGrade.Guard
                    ? guardSpec.GuardedDamageMultiplier
                    : 1f;
            float postureDamage = grade == DRPGGuardGrade.Perfect
                ? 0f
                : grade == DRPGGuardGrade.Guard
                    ? guardSpec.GuardedPostureDamage
                    : guardSpec.MissedGuardPostureDamage;
            if (!session.TryResolveInteractiveImpact(
                    activeAction,
                    DRPGBattleImpactContext.ForGuard(
                        grade,
                        multiplier,
                        postureDamage),
                    out DRPGBattleHitResult hit,
                    out string error))
            {
                FailDomainTransaction(error);
                return;
            }

            interactiveView?.ShowGuardResult(grade);
            PlayInteractiveAudio(grade == DRPGGuardGrade.Perfect
                ? BattleAudioCue.PerfectGuard
                : grade == DRPGGuardGrade.Guard
                    ? BattleAudioCue.Guard
                    : BattleAudioCue.Miss);
            if (grade != DRPGGuardGrade.Perfect)
                PlayInteractiveImpact(hit);
        }
    }

    private void PlayInteractiveImpact(DRPGBattleHitResult hit)
    {
        if (hit == null || activeAction == null || session == null ||
            director == null)
        {
            return;
        }

        try
        {
            var resolution = new DRPGBattleActionResolution(
                activeAction,
                new[] { hit });
            BattlePresentationRequest request = requestFactory.CreateImpact(
                BattlePresentationRequestIds.Next(),
                battleGeneration,
                activeActionTurnSerial,
                resolution,
                session);
            director.Play(request);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private void FinishInteractiveAction(DRPGBattleInteractionEndReason reason)
    {
        if (finishingInteractive || activeInteractiveRun == null ||
            activeAction == null || session == null)
        {
            return;
        }

        finishingInteractive = true;
        DRPGBattleSession capturedSession = session;
        DRPGBattlePreparedAction capturedAction = activeAction;
        long capturedBinding = bindingVersion;
        ActionIdentity capturedActionIdentity = activeActionIdentity;
        activeInteractiveRun.End(reason);

        if (!capturedSession.TryEndInteractiveResolution(
                capturedAction,
                out DRPGBattleActionResolution resolution,
                out string error))
        {
            finishingInteractive = false;
            FailDomainTransaction(error);
            return;
        }

        activeResolution = resolution;
        PlayInteractiveAudio(BattleAudioCue.InteractiveWindowClose);
        ReleaseInteractiveState(false);
        finishingInteractive = false;
        OnImpactGateReleased(
            capturedSession,
            capturedBinding,
            capturedActionIdentity,
            capturedAction);
    }

    private void OnImpactGateReleased(
        DRPGBattleSession capturedSession,
        long capturedBinding,
        ActionIdentity capturedActionIdentity,
        DRPGBattlePreparedAction action)
    {
        if (!IsCurrentAction(
                capturedSession,
                capturedBinding,
                capturedActionIdentity,
                action,
                DRPGBattlePhase.ExecutingAction) ||
            activeResolution == null || completionRequested)
        {
            return;
        }

        completionRequested = true;
        bool showTelegraph =
            action.Command.Stage == DRPGBattleCommandStage.Telegraph;
        long completedTurnSerial = activeActionTurnSerial;
        if (!capturedSession.TryCommitAction(action, out string error))
        {
            FailDomainTransaction(error, capturedSession);
            return;
        }

        if (!IsSameBinding(
                capturedSession,
                capturedBinding,
                capturedActionIdentity.BattleGeneration))
        {
            return;
        }

        if (showTelegraph)
        {
            interactiveView?.ShowTelegraph(
                FindParticipant(capturedSession, action.ActorRuntimeId),
                FindParticipant(capturedSession, action.TargetRuntimeId),
                action.Command.Skill);
            PlayInteractiveAudio(
                BattleAudioCue.SpecialWarning,
                action);
        }

        try
        {
            BattlePresentationRequest settled = requestFactory.CreateSettled(
                BattlePresentationRequestIds.Next(),
                capturedActionIdentity.BattleGeneration,
                completedTurnSerial,
                action,
                capturedSession);
            PlayFinishedFailOpen(
                settled,
                _ => OnSettlementFinished(
                    capturedSession,
                    capturedBinding,
                    capturedActionIdentity,
                    action));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            OnSettlementFinished(
                capturedSession,
                capturedBinding,
                capturedActionIdentity,
                action);
        }
    }

    private void OnSettlementFinished(
        DRPGBattleSession capturedSession,
        long capturedBinding,
        ActionIdentity capturedActionIdentity,
        DRPGBattlePreparedAction action)
    {
        if (!IsSameBinding(
                capturedSession,
                capturedBinding,
                capturedActionIdentity.BattleGeneration) ||
            (usesDomainIdentity &&
             capturedSession.CurrentActionIdentity != capturedActionIdentity) ||
            action == null ||
            !ReferenceEquals(activeAction, action) ||
            !ReferenceEquals(
                capturedSession.PendingFinalizationAction,
                action) ||
            capturedSession.Phase != DRPGBattlePhase.ActionEnd ||
            !completionRequested)
        {
            return;
        }

        ClearActiveAction();
        if (!capturedSession.TryFinalizeCommittedAction(
                action,
                out string error))
        {
            FailDomainTransaction(error, capturedSession);
        }
    }

    private void PlayGateFailOpen(
        BattlePresentationRequest request,
        Action<BattlePresentationOutcome> continuation)
    {
        try
        {
            BattlePresentationRun run = director.Play(request);
            if (run == null)
            {
                continuation(BattlePresentationOutcome.Skipped);
                return;
            }
            run.WhenGateReleased(continuation);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            continuation(BattlePresentationOutcome.Faulted);
        }
    }

    private void PlayFinishedFailOpen(
        BattlePresentationRequest request,
        Action<BattlePresentationOutcome> continuation)
    {
        try
        {
            BattlePresentationRun run = director.Play(request);
            if (run == null)
            {
                continuation(BattlePresentationOutcome.Skipped);
                return;
            }
            run.WhenFinished(continuation);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            continuation(BattlePresentationOutcome.Faulted);
        }
    }

    private bool IsCurrentAction(
        DRPGBattleSession capturedSession,
        long capturedBinding,
        ActionIdentity capturedActionIdentity,
        DRPGBattlePreparedAction action,
        DRPGBattlePhase requiredPhase)
    {
        return IsSameBinding(
                capturedSession,
                capturedBinding,
                capturedActionIdentity.BattleGeneration) &&
            (!usesDomainIdentity ||
             capturedSession.CurrentActionIdentity == capturedActionIdentity) &&
            action != null &&
            ReferenceEquals(activeAction, action) &&
            ReferenceEquals(capturedSession.ActiveAction, action) &&
            capturedSession.Phase == requiredPhase;
    }

    private bool IsSameBinding(
        DRPGBattleSession capturedSession,
        long capturedBinding,
        long capturedGeneration)
    {
        return ReferenceEquals(session, capturedSession) &&
            bindingVersion == capturedBinding &&
            battleGeneration == capturedGeneration;
    }

    private static bool RequiresInteractiveResolution(
        DRPGBattlePreparedAction action)
    {
        return action != null &&
            (action.Command.Stage == DRPGBattleCommandStage.Release ||
             action.Spec != null &&
             action.Spec.ExecutionMode ==
                DRPGSkillExecutionMode.FreeStrike);
    }

    private static BattleParticipantRuntime FindParticipant(
        DRPGBattleSession battleSession,
        int runtimeId)
    {
        if (battleSession == null)
            return null;
        for (int i = 0; i < battleSession.Participants.Count; i++)
        {
            BattleParticipantRuntime participant =
                battleSession.Participants[i];
            if (participant.RuntimeId == runtimeId)
                return participant;
        }
        return null;
    }

    private void CollectFreeStrikeTargets(
        DRPGBattleSession battleSession,
        BattleParticipantRuntime actor)
    {
        freeStrikeTargets.Clear();
        if (battleSession == null || actor == null || interactiveSurface == null)
            return;

        for (int i = 0; i < battleSession.Participants.Count; i++)
        {
            BattleParticipantRuntime participant = battleSession.Participants[i];
            if (participant == null || !participant.IsAlive ||
                participant.Side == actor.Side ||
                activeAction == null ||
                !activeAction.Spec.CanTarget(actor, participant) ||
                !interactiveSurface.TryResolveParticipantAnchor(
                    participant.RuntimeId,
                    out BattleImpactAnchor anchor) ||
                anchor?.RectTransform == null)
            {
                continue;
            }

            freeStrikeTargets.Add(new BattleFreeStrikeTargetAnchor(
                participant,
                anchor));
        }
    }

    private static bool HasLivingOpponent(
        DRPGBattleSession battleSession,
        BattleParticipantRuntime actor)
    {
        if (battleSession == null || actor == null)
            return false;
        for (int i = 0; i < battleSession.Participants.Count; i++)
        {
            BattleParticipantRuntime participant = battleSession.Participants[i];
            if (participant != null && participant.IsAlive &&
                participant.Side != actor.Side)
            {
                return true;
            }
        }
        return false;
    }

    private void PlayInteractiveAudio(
        BattleAudioCue cue,
        DRPGBattlePreparedAction actionOverride = null)
    {
        DRPGBattlePreparedAction action = actionOverride ?? activeAction;
        if (action == null)
            return;
        interactiveAudio.Play(new BattleAudioCueRequest(
            cue,
            action.ActorRuntimeId,
            action.TargetRuntimeId,
            action.Spec != null ? action.Spec.SkillId : string.Empty));
    }

    private void ReleaseInteractiveState(bool immediate)
    {
        bool hadRun = activeInteractiveRun != null;
        activeInteractiveRun = null;
        interactiveActor = null;
        interactiveTarget = null;
        freeStrikeTargets.Clear();
        interactiveStartedFrame = -1;
        IDisposable lease = interactiveInputLease;
        interactiveInputLease = null;
        lease?.Dispose();

        if (!hadRun || interactiveView == null)
            return;
        if (immediate)
            interactiveView.HideImmediate();
        else
            interactiveView.HideAnimated();
    }

    private void ClearActiveAction()
    {
        ReleaseInteractiveState(true);
        activeAction = null;
        activeActionIdentity = default;
        activeResolution = null;
        activeActionTurnSerial = 0L;
        impactRequested = false;
        completionRequested = false;
        finishingInteractive = false;
    }

    private void FailDomainTransaction(
        string error,
        DRPGBattleSession failedSession = null)
    {
        Debug.LogError(
            "[DRPGPresentationTransaction] " +
            (string.IsNullOrWhiteSpace(error) ? "Unknown transaction error." : error),
            this);
        DRPGBattleSession target = failedSession ?? session;
        ClearActiveAction();
        if (target != null && target.IsActive)
            target.Abort();
    }

    private void Update()
    {
        if (activeInteractiveRun == null)
            return;

        bool acceptsPhysicalInput = Time.frameCount > interactiveStartedFrame;
        if (acceptsPhysicalInput &&
            activeInteractiveRun.Kind == DRPGBattleInteractionKind.Defense)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E))
                SubmitGuardInput();
        }
        else if (activeInteractiveRun.Kind ==
            DRPGBattleInteractionKind.FreeStrike)
        {
            DRPGFreeStrikeWindowContext window =
                session?.ActiveFreeStrikeWindow;
            if (interactiveView != null && window != null)
            {
                interactiveView.TryCreateStrikeSample(
                    Input.mousePosition,
                    window.Spec,
                    out _,
                    out _);
            }
            if (acceptsPhysicalInput && Input.GetMouseButtonDown(0))
                SubmitFreeStrikeInput(Input.mousePosition);
        }

        TickInteractive(Time.unscaledDeltaTime);
    }

    private void OnDisable()
    {
        Unbind(true);
    }
}
