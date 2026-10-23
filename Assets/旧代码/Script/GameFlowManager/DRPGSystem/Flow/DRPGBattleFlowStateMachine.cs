public sealed class DRPGBattleFlowStateMachine : IGameFlowState
{
    private const float EnemyTargetPreviewSeconds = 0.45f;

    private IDRPGBattleFlowController controller;
    private DRPGBattleSession session;
    private bool isRunning;
    private DRPGBattleCommand pendingEnemyCommand;
    private float enemyPreviewRemaining;

    public DRPGBattleFlowStateMachine()
    {
    }

    public DRPGBattleFlowStateMachine(IDRPGBattleFlowController controller)
    {
        this.controller = controller;
    }

    public string flowStateMachineName => "DRPGBattleFlowStateMachine";
    public string currentFlowStateName =>
        session != null ? session.Phase.ToString() : DRPGBattlePhase.Inactive.ToString();
    public bool IsActive => isRunning;
    public int Priority => 30;
    public string LastError { get; private set; } = string.Empty;
    public DRPGBattleSession Session => session;

    public void Initialize(GameFlowDataBus gameFlowDataBus, GameFlowRoot root)
    {
        if (controller == null && root != null)
            controller = root.drpgBattleFlow;
    }

    public void Enter()
    {
    }

    public void Exit()
    {
    }

    public void Update(float deltaTime)
    {
        if (!isRunning || session == null || !session.IsActive)
            return;

        // BattleEnded can synchronously clear this.session during the tick.
        DRPGBattleSession updatingSession = session;
        if (updatingSession.TickTurnEntry())
        {
            LastError = updatingSession.TurnEntryFault;
            ClearPendingEnemyCommand();
            return;
        }

        // Legacy presentation runs outside the TurnEntry driver, but owns the
        // same input lease. Do not resample or resubmit its still-current actor.
        if (!ReferenceEquals(session, updatingSession) || !updatingSession.TransactionArbiter.IsCommandInputOpen)
        {
            ClearPendingEnemyCommand();
            return;
        }

        BattleParticipantRuntime actor = session.CurrentActor;
        if (actor == null || actor.Side != DRPGBattleSide.Enemy)
        {
            ClearPendingEnemyCommand();
            return;
        }

        if (pendingEnemyCommand == null)
        {
            if (!session.TryCreateEnemyCommand(out DRPGBattleCommand command))
            {
                return;
            }

            if (command.Kind == DRPGBattleCommandKind.BasicAttack && session.StandardBasicAttackEnabled)
            {
                var started = session.TryStartStandardBasicAttack(new ParticipantId(command.TargetRuntimeId));
                LastError = started.IsAccepted ? string.Empty : started.Rejection.ToString();
                return;
            }

            if (command.Kind == DRPGBattleCommandKind.Skill &&
                command.Stage == DRPGBattleCommandStage.Immediate &&
                session.StandardBasicAttackEnabled &&
                DRPGBattleSkillModePolicy.IsAllowed(command.Skill))
            {
                var started = session.TryStartCurrentConfiguredSkill(command);
                LastError = started.IsAccepted ? string.Empty : started.Rejection.ToString();
                return;
            }

            if (session.IsCurrentEnemyCommandFromOrdinaryIntent)
            {
                if (!session.HasStagedActionConsumer)
                {
                    LastError =
                        "Enemy action requires a staged presentation transaction owner.";
                    return;
                }

                if (!session.TryPrepareCommand(
                        command,
                        out _,
                        out string ordinaryError))
                {
                    LastError = ordinaryError ?? string.Empty;
                }
                else
                {
                    LastError = string.Empty;
                }
                return;
            }

            if (!session.TryPreviewCommand(command, out _))
                return;

            pendingEnemyCommand = command;
            enemyPreviewRemaining = EnemyTargetPreviewSeconds;
            return;
        }

        if (pendingEnemyCommand.ActorRuntimeId != actor.RuntimeId)
        {
            ClearPendingEnemyCommand();
            return;
        }

        enemyPreviewRemaining -= deltaTime > 0f ? deltaTime : 0f;
        if (enemyPreviewRemaining > 0f)
            return;

        DRPGBattleCommand confirmedCommand = pendingEnemyCommand;
        ClearPendingEnemyCommand();
        if (!session.HasStagedActionConsumer)
        {
            LastError =
                "Enemy action requires a staged presentation transaction owner.";
            return;
        }

        if (!session.TryPrepareCommand(confirmedCommand, out _, out string error))
            LastError = error ?? string.Empty;
        else
            LastError = string.Empty;
    }

    public bool RequestState(object stateData)
    {
        if (isRunning || controller == null)
            return false;

        if (!(stateData is DRPGBattleStartRequest request))
            return false;

        if (!controller.TryBeginBattle(request, out session, out string error))
        {
            LastError = error ?? string.Empty;
            session = null;
            return false;
        }

        LastError = string.Empty;
        isRunning = true;
        ClearPendingEnemyCommand();
        session.BattleEnded += OnBattleEnded;
        session.BattleFaulted += OnBattleFaulted;
        return true;
    }

    private void OnBattleEnded(DRPGBattleResult result)
    {
        DRPGBattleSession completedSession = session;
        if (completedSession != null)
        {
            completedSession.BattleEnded -= OnBattleEnded;
            completedSession.BattleFaulted -= OnBattleFaulted;
        }

        session = null;
        isRunning = false;
        ClearPendingEnemyCommand();
        controller?.CompleteBattle(completedSession, result);
    }

    private void OnBattleFaulted(DRPGBattleFaultTerminal terminal)
    {
        var failedSession = session;
        if (failedSession == null || !isRunning || terminal.Owner.BattleGeneration != failedSession.BattleGeneration) return;
        LastError = terminal.DiagnosticCode + ": " + terminal.Detail;
        failedSession.BattleEnded -= OnBattleEnded;
        failedSession.BattleFaulted -= OnBattleFaulted;
        session = null; isRunning = false;
        ClearPendingEnemyCommand();
        // Host cleanup/recovery uses Aborted, with no domain BattleEnded event.
        controller?.CompleteBattle(failedSession,
            new DRPGBattleResult(DRPGBattleOutcome.Aborted, (float)failedSession.TransactionState.CaptureRuleSnapshot().CurrentLogicalTime));
    }

    private void ClearPendingEnemyCommand()
    {
        pendingEnemyCommand = null;
        enemyPreviewRemaining = 0f;
    }
}
