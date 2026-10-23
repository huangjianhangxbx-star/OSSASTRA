#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class DRPGFormalPresentationBindingProbe
{
    public static string Status { get; private set; } = "NotRun";
    public static string StartLive(bool enemyFirst = false, bool statuses = false)
        => StartCore(enemyFirst, statuses, false);
    public static string StartContinuous() => StartCore(false, true, true);
    public static string StartWaveContinuous() => StartCore(false, false, true, 9);
    public static string StartWaveReplacement() => StartCore(false, false, true, 10);
    public static string StartWaveTurnEntryDeath() => StartCore(false, false, true, 11);
    public static string StartWaveLastEntryDeath() => StartCore(false, false, true, 12);
    public static string StartWaveBindingFault() => StartCore(false, false, true, 13);
    public static string StartRealEntrances() => StartCore(false, false, true, 14);
    public static string StartLegacyContinuation() => StartCore(false, false, true, 15);
    public static string StartLegacyWaveReplacement() => StartCore(false, false, true, 16);
    public static string StartLegacyPartialWave() => StartCore(false, false, true, 17);
    public static string StartFaultExit(bool afterImpact = false) => StartCore(false, false, true, afterImpact ? 4 : 3);
    public static string StartEntryFaultExit(bool duringStartup = false) => StartCore(false, false, true, duringStartup ? 5 : 6);
    public static string StartContinuationFaultExit(bool afterTransfer = false) => StartCore(false, false, true, afterTransfer ? 8 : 7);
    public static string StartFinalizationEdge(bool abortAfterCompletion) => StartCore(false, true, true, abortAfterCompletion ? 2 : 1);

    internal static IEnumerator ProductionParityThroughRealEntrances()
        => RunProductionScenario(StartRealEntrances, 90f);

    internal static IEnumerator ProductionWavePlaceholderBatchDeploysAtomically()
        => RunProductionScenario(StartWaveReplacement, 140f);

    internal static IEnumerator ProductionWaveRegistrationFailureFaultsCleanly()
        => RunProductionScenario(StartWaveBindingFault, 45f);

    private static IEnumerator RunProductionScenario(
        Func<string> start,
        float timeoutSeconds)
    {
        yield return null;
        GameFlowRoot root = GameFlowRoot.Instance;
        if (root == null || root.drpgGameLoop == null)
            throw new InvalidOperationException(
                "Formal production scenario requires the loaded GameFlowRoot.");
        if (root.drpgGameLoop.AnkalutePhase == AnkaluteGameLoopPhase.Title &&
            !root.drpgGameLoop.StartGame())
            throw new InvalidOperationException(
                "Formal production scenario could not enter the strategic hub.");
        if (root.drpgGameLoop.AnkalutePhase ==
            AnkaluteGameLoopPhase.StrategicHub)
        {
            if (!root.drpgGameLoop.OpenDungeonSelection() ||
                !root.drpgGameLoop.LaunchExpedition(
                    new ExpeditionStartRequest(
                        new ExpeditionId("production-matrix"))))
                throw new InvalidOperationException(
                    "Formal production scenario could not enter the expedition.");
        }
        if (!root.drpgGameLoop.CanBeginBattle)
            throw new InvalidOperationException(
                "Formal production scenario has no battle authority.");

        string begin = start();
        if (begin != "Running")
            throw new InvalidOperationException(
                "Formal production scenario did not start: " + begin);
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (Status == "Running" && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (Status == "Running")
            throw new TimeoutException(
                "Formal production scenario exceeded its bounded runtime.");
        if (!Status.StartsWith("PASS", StringComparison.Ordinal))
            throw new InvalidOperationException(Status);

        // Status is published before the fixture's readable tail completes.
        // Do not let the matrix start another owner until OnDestroy has
        // aborted/unbound the previous session and hidden its presentation.
        while (Resources.FindObjectsOfTypeAll<Host>().Any(host =>
                   host != null && host.gameObject.scene.IsValid()) &&
               Time.realtimeSinceStartup < deadline)
            yield return null;
        if (Resources.FindObjectsOfTypeAll<Host>().Any(host =>
                host != null && host.gameObject.scene.IsValid()))
            throw new TimeoutException(
                "Formal production fixture did not release its host.");
    }

    private static string StartCore(bool enemyFirst, bool statuses, bool continuous, int edge = 0)
    {
        if (!Application.isPlaying || Status == "Running") throw new InvalidOperationException("Requires idle PlayMode verification.");
        if (GameFlowRoot.Instance == null || GameFlowRoot.Instance.drpgGameLoop == null || !GameFlowRoot.Instance.drpgGameLoop.CanBeginBattle)
            throw new InvalidOperationException("Enter the expedition first: title/hub overlays would hide the actual battle recording.");
        if (Resources.FindObjectsOfTypeAll<DRPGBattleFlowRoot>().Any(f => f.gameObject.scene.IsValid() && f.CurrentSession != null))
            throw new InvalidOperationException("An existing battle is active; it must not be replaced by validation.");
        Status = "Running";
        new GameObject("FormalTransactionIntegrationVerification").AddComponent<Host>().Begin(enemyFirst, statuses, continuous, edge);
        return Status;
    }

    private sealed class Host : MonoBehaviour
    {
        private IEnumerator run;
        private DRPGBattlePresentationRoot presentation;
        private DRPGBattleSession session;
        private DRPGStandardBasicAttackTransactionModule module;
        private IDisposable suspension;
        private string logError;
        private bool enemyFirst, statuses, continuous;
        private int edge;
        internal void Begin(bool enemy, bool withStatuses, bool runContinuous, int finalizationEdge) { enemyFirst = enemy; statuses = withStatuses; continuous = runContinuous; edge = finalizationEdge; Application.logMessageReceived += Observe; run = Run(); StartCoroutine(Drive()); }
        private void Observe(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) logError = message; }
        private IEnumerator Drive()
        {
            while (true)
            {
                bool next;
                try { if (logError != null) throw new InvalidOperationException("Console: " + logError); next = run.MoveNext(); }
                catch (Exception error) { Status = "FAIL: " + error; break; }
                if (!next) break;
                yield return run.Current;
            }
            Destroy(gameObject);
        }
        private IEnumerator Run()
        {
            var party = AssetDatabase.LoadAssetAtPath<DRPGPartySetupSO>("Assets/GameResources/DRPG/Progression/Test/Resonance/DRPGParty_ResonanceTest.asset");
            var encounter = AssetDatabase.LoadAssetAtPath<DRPGEncounterDefinitionSO>("Assets/GameResources/DRPG/Config/Test/DRPGEncounter_Test2.asset");
            var roster = DRPGBattleSessionFactory.CreateRoster(party, encounter);
            if (!roster.IsSuccess) throw new InvalidOperationException(roster.ErrorMessage);
            if (edge == 16 || edge == 17)
                roster = DRPGBattleSessionCreationResult.Success(roster.Participants.Select(p =>
                {
                    if (p.Side != DRPGBattleSide.Player) return p;
                    var basic = p.Build.BasicAttack;
                    var build = new DRPGResolvedCombatantBuild(p.Profile, null, basic, new[] { basic }, new[] { basic }, Array.Empty<DRPGPassiveModifierSO>());
                    return new BattleParticipantRuntime(p.RuntimeId, new BattleParticipantSpec(p.Profile, p.Side, p.RosterRole,
                        p.FormationSlot, build, p.FormationRow, p.PreferredFormationRow, p.IsDeployed, p.PartyOrderIndex));
                }).ToArray(), roster.ReserveEnemies, roster.FormationRules);
            if (edge == 1) roster = DRPGBattleSessionCreationResult.Success(roster.Participants.Where(p => p.Side == DRPGBattleSide.Player || p.RuntimeId == 4).ToArray());
            session = edge >= 9 && edge <= 13 || edge == 16 || edge == 17 ? DRPGBattleSessionFactory.CreateWaveSession(CreateBasicOnlyVerificationRoster(roster), 42,
                "formal-wave", new DRPGEnemyWavePolicySnapshot(edge == 16 ? 1 : 2, edge == 16 ? 1 : 2, .75, .3, roster.FormationRules.PerRowLimit * 3)) :
                continuous ? DRPGBattleSessionFactory.CreateStandardSession(CreateBasicOnlyVerificationRoster(roster)) : DRPGBattleSessionFactory.CreateSession(roster);
            if (statuses) SeedVisibleStatuses(session);
            if (edge >= 10 && edge <= 12 || edge == 16 || edge == 17)
            {
                var owner = BattleTransactionIdentity.ForTestHarness(new BattleStartIdentity(session.BattleGeneration, 703));
                if (!session.TransactionArbiter.TryAcquire(owner, out var seedLease, out var seedError)) throw new InvalidOperationException(seedError);
                using (seedLease)
                {
                    var draft = session.TransactionState.CreateWorkspace();
                    var lethal = ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
                    if (edge == 11 || edge == 12)
                    {
                        foreach (var field in new System.Collections.Generic.Dictionary<string, object> {
                            {"statusId", "wave-turn-start-lethal"}, {"triggerTiming", DRPGStatusTriggerTiming.TurnStart},
                            {"turnStartHpDelta", -10000f}, {"durationTurns", 2} })
                            typeof(DRPGStatusDefinitionSO).GetField(field.Key, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(lethal, field.Value);
                    }
                    int seededEnemies = 0;
                    foreach (var enemy in session.ReserveEnemies)
                    {
                        var id = new ParticipantId(enemy.RuntimeId);
                        draft.For(id).SetHitPoints(1f);
                        if ((edge == 11 || edge == 12) && seededEnemies++ < 2) draft.For(id).ApplyStatus(DRPGResolvedStatusSpec.From(lethal), 1, 0, id);
                    }
                    Destroy(lethal);
                    if (!session.TransactionState.TryCommitJournal(seedLease, draft).Succeeded) throw new InvalidOperationException("Wave lethal setup failed.");
                }
            }
            if (edge == 1)
            {
                var owner = BattleTransactionIdentity.ForTestHarness(new BattleStartIdentity(session.BattleGeneration, 702));
                if (!session.TransactionArbiter.TryAcquire(owner, out var seedLease, out var seedError)) throw new InvalidOperationException(seedError);
                using (seedLease)
                {
                    var draft = session.TransactionState.CreateWorkspace();
                    draft.For(new ParticipantId(4)).SetHitPoints(1f);
                    if (!session.TransactionState.TryCommitJournal(seedLease, draft).Succeeded) throw new InvalidOperationException("Lethal edge setup failed.");
                }
            }
            presentation = Resources.FindObjectsOfTypeAll<DRPGBattlePresentationRoot>().FirstOrDefault(r => r.gameObject.scene.IsValid() && r.GetComponent<BattleStandardPresentationBinding>() != null);
            if (presentation == null) throw new InvalidOperationException("Loaded scene has no current formal prefab binding.");
            new ExplorationBattleSuspension(GameFlowRoot.Instance).TryAcquire(out suspension);
            presentation.ShowPrepared(session, new DRPGBattleStartRequest(party, encounter, default, 42));
            var clock = presentation.PreparedClock;
            if (session.PrepareStart(clock) != BattleStartupPrepareStatus.Prepared || session.CurrentActor != null)
                throw new InvalidOperationException("Formal startup prepared an actor too early or could not prepare.");
            int reserveBeforeEntry = session.ReserveEnemies.Count;
            if (edge == 12)
            {
                if (!session.TransactionArbiter.TryCaptureCurrentLease(session.TransactionArbiter.CurrentOwner, out var setup))
                    throw new InvalidOperationException("Startup lease unavailable for candidate order fixture.");
                var draft = session.TransactionState.CreateWorkspace();
                foreach (var member in session.Participants) draft.ScheduleAt(new ParticipantId(member.RuntimeId), 1000, 0);
                if (!session.TransactionState.TryCommitJournal(setup, draft).Succeeded) throw new InvalidOperationException("Candidate order setup rejected.");
            }
            if (enemyFirst)
            {
                // Presentation parity fixture, not an AI-policy test: ordinary
                // attack plans always wait for a completed player response,
                // including zero-warning plans. Seed an explicitly ready plan
                // under the already-owned startup capability, never a fake turn.
                var enemy = session.Participants.First(p => p.Side == DRPGBattleSide.Enemy && p.IsDeployed);
                var player = session.Participants.First(p => p.Side == DRPGBattleSide.Player && p.IsDeployed);
                var owner = session.TransactionArbiter.CurrentOwner;
                if (!session.TransactionArbiter.TryCaptureCurrentLease(owner, out var setup)) throw new InvalidOperationException("Startup lease missing.");
                using (session.TransactionState.OpenCompatibilityMutationScope(setup))
                {
                    var plans = session.TransactionState.TurnCandidates.Plans;
                    var plan = plans[enemy.RuntimeId];
                    plans[enemy.RuntimeId] = plan.WithSampledWarning(plan.ReadyAtTime).CommitTarget(player.RuntimeId, 0).WithSatisfiedPlayerResponse(1);
                }
            }
            if (!session.EnqueuePreparedStartup(session.TransactionArbiter.CurrentOwner.BattleStart)) throw new InvalidOperationException("Formal startup did not enqueue.");
            if (edge == 13)
            {
                var sequence = RunWaveBindingFault(clock);
                try { while (sequence.MoveNext()) yield return sequence.Current; }
                finally { (sequence as IDisposable)?.Dispose(); }
                yield break;
            }
            if (edge == 5 || edge == 6)
            {
                var sequence = RunEntryFaultExit(clock);
                try { while (sequence.MoveNext()) yield return sequence.Current; }
                finally { (sequence as IDisposable)?.Dispose(); }
                yield break;
            }
            double deadline = clock.Now + 15;
            bool seededEntryPlans = false;
            while (session.CurrentActor == null && clock.Now < deadline)
            {
                yield return null; session.TickTurnEntry();
                if (edge == 12 && !seededEntryPlans && session.TransactionArbiter.CurrentOwner.Kind == BattleTransactionKind.WaveTransition &&
                    session.TransactionState.TurnCandidates.Plans.Count > 0)
                {
                    var plans = session.TransactionState.TurnCandidates.Plans;
                    foreach (int id in plans.Keys.ToArray())
                    {
                        var plan = plans[id];
                        plans[id] = DRPGEnemyActionPlan.CreateWait(plan.PlanId, id, 0, 0).MarkReady();
                    }
                    seededEntryPlans = true;
                }
            }
            if (session.CurrentActor == null) throw new InvalidOperationException("Formal entry never published a candidate: " + session.TurnEntryFault);
            if (edge == 12)
            {
                if (session.ReserveEnemies.Count != reserveBeforeEntry - 4 || session.Outcome != DRPGBattleOutcome.None ||
                    session.Participants.Count(p => p.Side == DRPGBattleSide.Enemy && p.IsDeployed) != 2)
                    throw new InvalidOperationException("Last TurnEntry death did not advance exactly one complete wave.");
                Status = "PASS: Both first-wave enemies died in TurnStart; full departure and second-wave entry completed before the first actor publication.";
                yield return new WaitForSecondsRealtime(5); yield break;
            }
            yield return new WaitForSecondsRealtime(1);
            if (edge == 7 || edge == 8)
            {
                var sequence = RunContinuationFaultExit(clock);
                try { while (sequence.MoveNext()) yield return sequence.Current; }
                finally { (sequence as IDisposable)?.Dispose(); }
                yield break;
            }
            if (edge == 3 || edge == 4)
            {
                var sequence = RunFaultExit(clock);
                try { while (sequence.MoveNext()) yield return sequence.Current; }
                finally { (sequence as IDisposable)?.Dispose(); }
                yield break;
            }
            if (continuous)
            {
                var sequence = edge == 17 ? RunLegacyPartialWave(clock) : edge == 16 ? RunLegacyWaveReplacement(clock) : edge == 15 ? RunLegacyContinuation(clock) : edge == 14 ? RunRealEntrances(clock) : edge == 10 || edge == 11 ? RunWaveReplacement(clock) : RunContinuous(clock);
                try { while (sequence.MoveNext()) yield return sequence.Current; }
                finally { (sequence as IDisposable)?.Dispose(); }
                yield break;
            }
            var actor = session.CurrentActor;
            if (enemyFirst && actor.Side != DRPGBattleSide.Enemy) throw new InvalidOperationException("Enemy-first fixture did not select its actual enemy.");
            var target = session.Participants.First(p => p.Side != actor.Side && p.IsAlive && p.IsDeployed);
            var skill = actor.Build.BasicAttack;
            if (skill == null) throw new InvalidOperationException("Actual candidate has no basic attack.");
            var identities = (DRPGBattleIdentityAuthority)typeof(DRPGBattleSession).GetField("transactionIdentityAuthority", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
            var turn = session.CurrentActorTurnToken;
            var selection = identities.AllocateSelection(turn);
            var identity = identities.AllocateAction(turn);
            if (!session.TransactionPresentationPortSlot.TryGet(session.BattleGeneration, out var port) ||
                !port.TryCaptureFrozenSnapshot(new DRPGBasicAttackPresentationFreezeRequest(session.BattleGeneration,
                    turn.Actor, new ParticipantId(target.RuntimeId), skill.SkillId, skill.PresentationKey), out var frozen))
                throw new InvalidOperationException("Formal view could not freeze its actual actor and target.");
            var spec = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(actor.RuntimeId, target.RuntimeId, skill), actor);
            var snapshot = new DRPGBasicAttackFrozenSnapshot(turn, selection, new ParticipantId(target.RuntimeId), skill, actor.Side, target.Side,
                frozen.ActorHomePosition, frozen.ActorConflictPosition, frozen.TargetHomePosition, frozen.TargetConflictPosition,
                frozen.ActorFacingSign, frozen.TargetFacingSign, frozen.ActorVisualBaseline, frozen.TargetVisualBaseline, frozen.Timing, frozen.Semantics,
                session.Participants.Where(p => p.IsDeployed).Select(p => new ParticipantId(p.RuntimeId)).ToArray(), true, true, true, true, spec, actor.Profile.DisplayName).WithIdentity(identity);
            if (!session.TransactionArbiter.TryAcquire(BattleTransactionIdentity.ForRootAction(identity), out var lease, out var error)) throw new InvalidOperationException(error);
            module = new DRPGStandardBasicAttackTransactionModule(session.TransactionPresentationPortSlot);
            module.BindRules(session.StandardBasicAttackRules);
            int impacts = 0, completions = 0;
            session.ActionLedgerCommitted += facts => impacts++;
            session.ActionCompleted += action => completions++;
            var started = module.TryStart(snapshot, identity, lease, "formal-integration-locked");
            if (!started.IsAccepted) throw new InvalidOperationException("Formal root start rejected.");
            double began = clock.Now; deadline = clock.Now + 20;
            while (!module.ReadyForFinalization && !started.Receipt.Completion.IsCompleted && clock.Now < deadline)
            {
                yield return null; module.Tick();
                if (!ReferenceEquals(actor, session.CurrentActor) || completions != 0) throw new InvalidOperationException("Formal root advanced before finalization.");
            }
            if (!module.ReadyForFinalization || impacts != 1 || !lease.IsActive)
                throw new InvalidOperationException("Formal root did not reach its final barrier: impacts=" + impacts + "; " + (started.Receipt.Completion.IsCompleted ? started.Receipt.Completion.Result.Detail : "pending"));
            if (statuses && (module.SettlementLedger == null || clock.Now - began < 3.3)) throw new InvalidOperationException("Layered Stage9 did not keep its visible dwell.");
            Status = "PASS: Actual F8 assets; actor=" + actor.Side + "; layeredStatuses=" + statuses + "; prepared entry + first candidate + Stage3-9; one impact; no early actor/ActionCompleted; elapsed=" + (clock.Now - began).ToString("F2") + "s. Finalization/F8 routing NOT claimed.";
            yield return new WaitForSecondsRealtime(12);
        }

        private IEnumerator RunFaultExit(IDRPGBattlePresentationClock clock)
        {
            int impacts = 0, completed = 0, ended = 0;
            session.ActionLedgerCommitted += _ => impacts++;
            session.ActionCompleted += _ => completed++;
            session.BattleEnded += _ => ended++;
            var actor = session.CurrentActor;
            var target = session.Participants.First(p => p.Side != actor.Side && p.IsAlive && p.IsDeployed);
            var result = session.TryStartStandardBasicAttack(new ParticipantId(target.RuntimeId));
            if (!result.IsAccepted) throw new InvalidOperationException("Fault fixture could not lock its actual root.");
            var surface = (DRPGBattleUGUISurface)typeof(DRPGBattlePresentationRoot).GetField("surface", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(presentation);
            int faults = 0;
            bool invalidFault = false;
            session.BattleFaulted += _ => { throw new InvalidOperationException("expected-fault-observer"); };
            session.BattleFaulted += terminal =>
            {
                faults++;
                invalidFault = session.IsActive || session.TransactionArbiter.HasOwner || surface.ProjectionArbiter.IsLeased ||
                    terminal.Owner != BattleTransactionIdentity.ForRootAction(result.Receipt.Identity) ||
                    terminal.LastValidBattleRevision != session.TransactionState.BattleRevision || terminal.Trace.Count == 0;
                session.Abort(); session.TickTurnEntry();
                if (session.TryStartStandardBasicAttack(new ParticipantId(target.RuntimeId)).IsAccepted) invalidFault = true;
            };
            var host = new FaultHostController(session);
            var flow = new DRPGBattleFlowStateMachine(host);
            if (!flow.RequestState(new DRPGBattleStartRequest(null, null, default, 42)))
                throw new InvalidOperationException("Fault host fixture could not bind actual flow state machine.");
            if (edge == 4)
            {
                double deadline = clock.Now + 10;
                while (impacts == 0 && clock.Now < deadline) { yield return null; flow.Update(.016f); }
                if (impacts != 1) throw new InvalidOperationException("Post-impact fixture never committed its real journal.");
            }
            float committedHp = target.CurrentHp;
            var view = surface.WorldRosterPresenter.ActiveViews.First(v => v.RuntimeId == target.RuntimeId);
            view.gameObject.SetActive(false); // actual leased actor loss, not a fake terminal
            for (int i = 0; i < 8; i++) { yield return null; flow.Update(.016f); session.TickTurnEntry(); }
            if (!result.Receipt.Completion.IsCompleted || result.Receipt.Completion.Result.Kind != RootActionTerminalKind.Faulted)
                throw new InvalidOperationException("Lost actor did not fault the root.");
            if (session.IsActive || session.TransactionArbiter.HasOwner || surface.ProjectionArbiter.IsLeased)
                throw new InvalidOperationException("Cleaned root fault left Session active or retained ownership.");
            if (impacts != (edge == 4 ? 1 : 0) || target.CurrentHp != committedHp || completed != 0 || ended != 0 || session.Outcome != DRPGBattleOutcome.None)
                throw new InvalidOperationException("Technical fault masqueraded as an action/battle outcome.");
            if (faults != 1 || invalidFault || session.ObserverDiagnostics.Count != 1 || flow.IsActive ||
                host.Completions != 1 || host.Result.Outcome != DRPGBattleOutcome.Aborted || !session.LastBattleFault.HasValue)
                throw new InvalidOperationException("Fault notification/host recovery duplicated, leaked ownership or swallowed later observers.");
            Status = "PASS: Real-asset actor loss afterImpact=" + (edge == 4) + "; impacts=" + impacts +
                "; ActionCompleted=0, BattleEnded=0, BattleFaulted=1, host Aborted=1; observer/reentry isolated; projection/rule ownership clear.";
        }

        private IEnumerator RunEntryFaultExit(IDRPGBattlePresentationClock clock)
        {
            var surface = (DRPGBattleUGUISurface)typeof(DRPGBattlePresentationRoot).GetField("surface", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(presentation);
            var host = new FaultHostController(session);
            var flow = new DRPGBattleFlowStateMachine(host);
            int faults = 0, actions = 0, ended = 0, actors = 0;
            bool invalidTerminal = false;
            BattleTransactionIdentity failedOwner = default;
            session.ActionCompleted += _ => actions++;
            session.BattleEnded += _ => ended++;
            session.CurrentActorChanged += _ => actors++;
            session.BattleFaulted += _ => throw new InvalidOperationException("expected-entry-fault-observer");
            session.BattleFaulted += terminal =>
            {
                faults++;
                invalidTerminal |= terminal.Owner != failedOwner || session.IsActive || session.TransactionArbiter.HasOwner ||
                    surface.ProjectionArbiter.IsLeased || !session.LastBattleFault.HasValue;
                session.Abort(); session.Start(); session.TickTurnEntry();
            };
            if (!flow.RequestState(new DRPGBattleStartRequest(null, null, default, 42)))
                throw new InvalidOperationException("Entry fault host could not bind the real FlowStateMachine.");
            flow.Update(.016f); // creates the real entry handle, not a test receipt
            if (edge == 5)
            {
                failedOwner = session.TransactionArbiter.CurrentOwner;
                if (failedOwner.Kind != BattleTransactionKind.BattleStart ||
                    !session.TransactionPresentationPortSlot.TryGet(session.BattleGeneration, out var port))
                    throw new InvalidOperationException("No live startup binding to invalidate.");
                session.TransactionPresentationPortSlot.TryUnbind(session.BattleGeneration, port);
            }
            else
            {
                double deadline = clock.Now + 12;
                while (session.TransactionArbiter.CurrentOwner.Kind != BattleTransactionKind.TurnEntry && clock.Now < deadline)
                { yield return null; flow.Update(.016f); }
                failedOwner = session.TransactionArbiter.CurrentOwner;
                if (failedOwner.Kind != BattleTransactionKind.TurnEntry || !surface.ProjectionArbiter.IsLeased || actors != 0)
                    throw new InvalidOperationException("No real candidate projection to invalidate before actor publication.");
                var view = surface.WorldRosterPresenter.ActiveViews.First(v => v.RuntimeId == failedOwner.TurnEntry.Actor.Value);
                view.gameObject.SetActive(false);
            }
            for (int i = 0; i < 12; i++) { yield return null; flow.Update(.016f); session.TickTurnEntry(); }
            if (faults != 1 || invalidTerminal || actions != 0 || ended != 0 || actors != 0 || session.IsActive ||
                session.TransactionArbiter.HasOwner || surface.ProjectionArbiter.IsLeased || session.Outcome != DRPGBattleOutcome.None ||
                session.ObserverDiagnostics.Count != 1 || host.Completions != 1 || host.Result.Outcome != DRPGBattleOutcome.Aborted || flow.IsActive)
                throw new InvalidOperationException("Real entry fault did not close cleanly: faults=" + faults + " actions=" + actions +
                    " ended=" + ended + " actors=" + actors + " owner=" + session.TransactionArbiter.HasOwner +
                    " projection=" + surface.ProjectionArbiter.IsLeased + " error=" + session.TurnEntryFault);
            Status = "PASS: Real-asset " + (edge == 5 ? "Startup binding loss" : "TurnEntry actor loss") +
                "; ActionCompleted=0, BattleEnded=0, CurrentActorChanged=0, BattleFaulted=1, host Aborted=1; projection/rule clear; observer/restart isolated.";
        }

        // Catches two distinct seams: rejected sealed facts before transfer,
        // and a failed real candidate factory after the atomic owner transfer.
        private IEnumerator RunContinuationFaultExit(IDRPGBattlePresentationClock clock)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var surface = (DRPGBattleUGUISurface)typeof(DRPGBattlePresentationRoot).GetField("surface", flags).GetValue(presentation);
            var host = new FaultHostController(session);
            var flow = new DRPGBattleFlowStateMachine(host);
            if (!flow.RequestState(new DRPGBattleStartRequest(null, null, default, 42))) throw new InvalidOperationException("Continuation fault Flow binding failed.");
            var actor = session.CurrentActor;
            var target = session.Participants.First(p => p.Side != actor.Side && p.IsAlive && p.IsDeployed);
            int impacts = 0, actions = 0, faults = 0, ended = 0, actors = 0;
            bool invalidTerminal = false;
            session.ActionLedgerCommitted += _ => impacts++;
            session.ActionCompleted += _ => actions++;
            session.BattleEnded += _ => ended++;
            session.CurrentActorChanged += _ => actors++;
            session.BattleFaulted += terminal =>
            {
                faults++;
                invalidTerminal |= session.IsActive || session.TransactionArbiter.HasOwner || surface.ProjectionArbiter.IsLeased ||
                    terminal.Owner.Kind != (edge == 7 ? BattleTransactionKind.RootAction : BattleTransactionKind.TurnEntry);
                session.Abort(); session.Start(); session.TickTurnEntry();
            };
            var started = session.TryStartStandardBasicAttack(new ParticipantId(target.RuntimeId));
            if (!started.IsAccepted) throw new InvalidOperationException("Continuation fault root rejected.");
            double deadline = clock.Now + 16;
            while (!started.Receipt.Completion.IsCompleted && clock.Now < deadline)
            { yield return null; flow.Update(.016f); }
            if (!started.Receipt.Completion.IsCompleted || started.Receipt.Completion.Result.Kind == RootActionTerminalKind.Faulted ||
                impacts != 1 || actions != 0 || surface.ProjectionArbiter.IsLeased)
                throw new InvalidOperationException("Fixture did not reach actual sealed Stage9 before continuation.");
            if (edge == 7)
            {
                var owner = BattleTransactionIdentity.ForRootAction(started.Receipt.Identity);
                if (!session.TransactionArbiter.TryCaptureCurrentLease(owner, out var held)) throw new InvalidOperationException("Root lease missing.");
                var draft = session.TransactionState.CreateWorkspace();
                draft.For(new ParticipantId(target.RuntimeId)).SetHitPoints(Math.Max(1, target.CurrentHp - 1));
                if (!session.TransactionState.TryCommitJournal(held, draft).Succeeded) throw new InvalidOperationException("Revision drift injection failed.");
            }
            else
            {
                var runner = typeof(DRPGBattleSession).GetField("turnEntryRunner", flags).GetValue(session);
                var field = runner.GetType().GetField("presentationFactory", flags);
                field.SetValue(runner, new RejectedNextCandidateFactory());
            }
            float committedHp = target.CurrentHp;
            for (int i = 0; i < 14; i++) { yield return null; flow.Update(.016f); session.TickTurnEntry(); }
            if (faults != 1 || invalidTerminal || impacts != 1 || actions != (edge == 7 ? 0 : 1) || ended != 0 || actors != 0 ||
                target.CurrentHp != committedHp || session.IsActive || session.TransactionArbiter.HasOwner || surface.ProjectionArbiter.IsLeased ||
                session.Outcome != DRPGBattleOutcome.None || host.Completions != 1 || host.Result.Outcome != DRPGBattleOutcome.Aborted || flow.IsActive)
                throw new InvalidOperationException("Continuation fault stuck or replayed: afterTransfer=" + (edge == 8) + " faults=" + faults +
                    " actions=" + actions + " actors=" + actors + " owner=" + session.TransactionArbiter.HasOwner + " error=" + session.TurnEntryFault);
            Status = "PASS: Real-asset Root continuation failure afterTransfer=" + (edge == 8) + "; impact=1, ActionCompleted=" + actions +
                ", BattleEnded=0, next actor=0, BattleFaulted=1, host Aborted=1; committed HP retained; sealed root not replayed.";
        }

        private sealed class RejectedNextCandidateFactory : IBattleTurnEntryPresentationFactory
        {
            public bool TryOpen(BattleTransactionIdentity owner, out IBattleTurnEntryPresentationScope scope, out string error)
            { scope = null; error = "injected unavailable next candidate view"; return false; }
        }

        private sealed class FaultHostController : IDRPGBattleFlowController
        {
            private readonly DRPGBattleSession bound;
            internal int Completions;
            internal DRPGBattleResult Result;
            internal FaultHostController(DRPGBattleSession bound) { this.bound = bound; }
            public bool TryBeginBattle(DRPGBattleStartRequest request, out DRPGBattleSession battle, out string error)
            { battle = bound; error = string.Empty; return true; }
            public void CompleteBattle(DRPGBattleSession battle, DRPGBattleResult result)
            {
                if (!ReferenceEquals(bound, battle)) throw new InvalidOperationException("Host received a different battle.");
                Completions++; Result = result;
            }
        }

        private IEnumerator RunContinuous(IDRPGBattlePresentationClock clock)
        {
            int impacts = 0, completions = 0, enemyActions = 0;
            bool invalidPublication = false;
            var surface = (DRPGBattleUGUISurface)typeof(DRPGBattlePresentationRoot).GetField("surface", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(presentation);
            session.ActionLedgerCommitted += facts => impacts++;
            if (edge == 1 || edge == 2)
            {
                session.ActionCompleted += action => { throw new InvalidOperationException("expected-edge-observer"); };
                session.BattleEnded += result => { throw new InvalidOperationException("expected-result-observer"); };
            }
            int resultEvents = 0;
            session.BattleEnded += result => resultEvents++;
            session.ActionCompleted += action =>
            {
                completions++;
                if (session.TransactionArbiter.IsCommandInputOpen ||
                    session.TransactionArbiter.CurrentOwner != BattleTransactionIdentity.ForRootAction(action.Identity) ||
                    surface.ProjectionArbiter.IsLeased) invalidPublication = true;
                if (edge == 2) session.Abort();
            };
            int expectedActions = edge == 0 || edge == 9 ? 5 : 1;
            if (edge == 9 && (session.Participants.Count(p => p.Side == DRPGBattleSide.Enemy && p.IsDeployed) != 2 ||
                surface.WorldRosterPresenter.ActiveViews.Count(v => v.BoundActor.IsEnemy) != 2))
                throw new InvalidOperationException("Initial wave did not publish exactly two bound enemy actors.");
            for (int index = 0; index < expectedActions; index++)
            {
                var actor = session.CurrentActor;
                var turn = session.CurrentActorTurnToken;
                if (actor == null || !session.TransactionArbiter.IsCommandInputOpen) throw new InvalidOperationException("Next actor did not become operable.");
                var target = session.Participants.First(p => p.Side != actor.Side && p.IsAlive && p.IsDeployed);
                var result = session.TryStartStandardBasicAttack(new ParticipantId(target.RuntimeId));
                if (!result.IsAccepted) throw new InvalidOperationException("Session standard action rejected: " + result.Rejection +
                    "; index=" + index + "; actor=" + actor.RuntimeId + "/" + actor.Side + "; phase=" + session.Phase);
                if (actor.Side == DRPGBattleSide.Enemy) enemyActions++;
                double deadline = clock.Now + 15;
                while (session.CurrentActorTurnToken == turn && session.Outcome == DRPGBattleOutcome.None && clock.Now < deadline)
                {
                    if (!result.Receipt.Completion.IsCompleted &&
                        (completions != index || session.CurrentActor != actor || session.TransactionArbiter.IsCommandInputOpen))
                        throw new InvalidOperationException("Continuation escaped the last Root barrier.");
                    yield return null; session.TickTurnEntry();
                    if (session.TurnEntryFault.Length != 0) throw new InvalidOperationException(session.TurnEntryFault);
                }
                if (completions != index + 1 || impacts != index + 1 || invalidPublication ||
                    !result.Receipt.Completion.IsCompleted || result.Receipt.Completion.Result.Kind == RootActionTerminalKind.Faulted ||
                    session.CurrentActorTurnToken == turn)
                    throw new InvalidOperationException("Continuous action failed: index=" + index + " impacts=" + impacts + " completions=" + completions + " invalid=" + invalidPublication);
                yield return new WaitForSecondsRealtime(.5f);
            }
            if (edge == 1 || edge == 2)
            {
                var expected = edge == 1 ? DRPGBattleOutcome.Victory : DRPGBattleOutcome.Aborted;
                if (session.Outcome != expected || resultEvents != 1 || session.TransactionArbiter.HasOwner ||
                    surface.ProjectionArbiter.IsLeased || session.ObserverDiagnostics.Count != 2)
                    throw new InvalidOperationException("Finalization edge failed: outcome=" + session.Outcome + "; resultEvents=" + resultEvents + "; diagnostics=" + session.ObserverDiagnostics.Count);
                for (int i = 0; i < 3; i++) { yield return null; session.TickTurnEntry(); }
                if (resultEvents != 1 || completions != 1) throw new InvalidOperationException("Late tick duplicated terminal events.");
                Status = "PASS: Real-asset finalization edge=" + expected + "; impact=1, ActionCompleted=1, BattleEnded=1; throwing observers isolated; projection/rule leases clear.";
                yield return new WaitForSecondsRealtime(3);
                yield break;
            }
            Status = "PASS: Real-asset Session owned five consecutive standard actions; impacts=" + impacts +
                "; ActionCompleted=" + completions + "; enemyActions=" + enemyActions +
                "; canonical projection closed before notification; next candidate gated. Enemy loadouts explicitly basic-only in test scope; UI/F8/normal AI routing NOT claimed.";
            yield return new WaitForSecondsRealtime(12);
        }

        private IEnumerator RunWaveReplacement(IDRPGBattlePresentationClock clock)
        {
            int completed = 0;
            session.ActionCompleted += _ => completed++;
            var initial = session.Participants.Where(p => p.Side == DRPGBattleSide.Enemy && p.IsDeployed).Select(p => p.RuntimeId).ToArray();
            int reserve = session.ReserveEnemies.Count;
            if ((edge == 11 ? initial.Length < 1 || initial.Length > 2 : initial.Length != 2) || reserve < 2)
                throw new InvalidOperationException("Replacement fixture needs at least two batches; active=" + initial.Length + "; reserve=" + reserve);
            for (int i = 0; i < 12 && session.ReserveEnemies.Count == reserve; i++)
            {
                var actor = session.CurrentActor;
                var turn = session.CurrentActorTurnToken;
                var target = session.Participants.First(p => p.Side != actor.Side && p.IsAlive && p.IsDeployed);
                var action = session.TryStartStandardBasicAttack(new ParticipantId(target.RuntimeId));
                if (!action.IsAccepted) throw new InvalidOperationException("Replacement action rejected.");
                double deadline = clock.Now + 20;
                while (session.CurrentActorTurnToken == turn && clock.Now < deadline)
                {
                    yield return null; session.TickTurnEntry();
                    if (session.TurnEntryFault.Length != 0) throw new InvalidOperationException(session.TurnEntryFault);
                }
                if (completed != i + 1 || session.CurrentActorTurnToken == turn || session.Outcome != DRPGBattleOutcome.None)
                    throw new InvalidOperationException("Wave replacement escaped or stalled the root completion gate.");
            }
            var active = session.Participants.Where(p => p.Side == DRPGBattleSide.Enemy && p.IsDeployed).ToArray();
            if (session.ReserveEnemies.Count != reserve - 2 || active.Length != 2 || active.Any(p => initial.Contains(p.RuntimeId)))
                throw new InvalidOperationException("Next wave was not activated exactly after the prior wave departed.");
            Status = "PASS: Real Sprite initial batch and second batch; prior two enemies departed before next two deployed; ActionCompleted=" + completed + "; next actor published only after wave handoff.";
            yield return new WaitForSecondsRealtime(5);
        }

        private IEnumerator RunWaveBindingFault(IDRPGBattlePresentationClock clock)
        {
            var surface = (DRPGBattleUGUISurface)typeof(DRPGBattlePresentationRoot).GetField("surface", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(presentation);
            int faults = 0, actions = 0, ended = 0, actors = 0;
            session.BattleFaulted += _ => faults++;
            session.ActionCompleted += _ => actions++;
            session.BattleEnded += _ => ended++;
            session.CurrentActorChanged += _ => actors++;
            double deadline = clock.Now + 10;
            while (session.TransactionArbiter.CurrentOwner.Kind != BattleTransactionKind.WaveTransition && clock.Now < deadline)
            { yield return null; session.TickTurnEntry(); }
            var owner = session.TransactionArbiter.CurrentOwner;
            if (owner.Kind != BattleTransactionKind.WaveTransition || !surface.ProjectionArbiter.IsLeased ||
                !session.TransactionPresentationPortSlot.TryGet(session.BattleGeneration, out var port))
                throw new InvalidOperationException("No owned wave binding available for fault injection.");
            long revision = session.TransactionState.BattleRevision;
            session.TransactionPresentationPortSlot.TryUnbind(session.BattleGeneration, port);
            deadline = clock.Now + 6;
            while (session.IsActive && clock.Now < deadline) { yield return null; session.TickTurnEntry(); }
            if (faults != 1 || actions != 0 || ended != 0 || actors != 0 || session.IsActive || session.TransactionArbiter.HasOwner ||
                surface.ProjectionArbiter.IsLeased || session.TransactionState.BattleRevision != revision ||
                session.Participants.Any(p => p.Side == DRPGBattleSide.Enemy && p.IsDeployed) ||
                !session.LastBattleFault.HasValue || session.LastBattleFault.Value.Owner != owner)
                throw new InvalidOperationException("Lost wave binding deployed actors or retained ownership: faults=" + faults + "; actors=" + actors + "; fault=" + session.TurnEntryFault);
            Status = "PASS: Removed wave binding faults once; no deployment, actor publication, action or battle result; original wave rule/projection leases cleaned.";
        }

        private IEnumerator RunRealEntrances(IDRPGBattlePresentationClock clock)
        {
            var menu = presentation.GetComponentInChildren<BattleCommandMenu>(true);
            var interaction = presentation.GetComponent<DRPGBattleInteractionController>();
            var button = menu == null ? null : (UnityEngine.UI.Button)typeof(BattleCommandMenu)
                .GetField("basicAttackButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu);
            if (button == null || interaction == null) throw new InvalidOperationException("Authored basic attack button/controller missing.");
            var flow = new DRPGBattleFlowStateMachine(new FaultHostController(session));
            if (!flow.RequestState(new DRPGBattleStartRequest(null, null, default, 42))) throw new InvalidOperationException("Real AI flow failed to bind.");
            int legacy = 0, impacts = 0, completed = 0, enemy = 0, player = 0;
            session.ActionPrepared += _ => legacy++;
            session.ActionLedgerCommitted += _ => impacts++;
            session.ActionCompleted += action => { completed++; if (session.Participants.First(p => p.RuntimeId == action.ActorRuntimeId).Side == DRPGBattleSide.Enemy) enemy++; else player++; };
            bool checkedCancel = false;
            ActorTurnToken selected = default;
            double deadline = clock.Now + 60;
            while ((enemy == 0 || player == 0) && clock.Now < deadline)
            {
                var actor = session.CurrentActor;
                if (actor != null && actor.Side == DRPGBattleSide.Player && session.TransactionArbiter.IsCommandInputOpen && selected != session.CurrentActorTurnToken)
                {
                    if (!button.gameObject.activeInHierarchy || !button.interactable)
                    { yield return null; flow.Update(.016f); continue; }
                    button.onClick.Invoke();
                    if (session.Phase != DRPGBattlePhase.TargetSelection || impacts != completed)
                        throw new InvalidOperationException("Actual button bypassed target selection.");
                    if (!checkedCancel)
                    {
                        if (!interaction.TryNavigateBack() || session.Phase != DRPGBattlePhase.AwaitingCommand)
                            throw new InvalidOperationException("Actual target cancellation did not restore command menu.");
                        button.onClick.Invoke(); checkedCancel = true;
                    }
                    var target = session.Participants.First(p => p.Side == DRPGBattleSide.Enemy && p.IsAlive && p.IsDeployed);
                    var proxy = presentation.GetComponentsInChildren<BattleActorScreenHitProxy>(true)
                        .FirstOrDefault(p => p.RuntimeId == target.RuntimeId && p.gameObject.activeInHierarchy && p.raycastTarget);
                    if (proxy == null || proxy.rectTransform.rect.width <= 0 || proxy.rectTransform.rect.height <= 0)
                        throw new InvalidOperationException("Actual enemy hit proxy is missing or empty.");
                    selected = session.CurrentActorTurnToken;
                    proxy.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));
                }
                yield return null; flow.Update(.016f);
                if (legacy != 0) throw new InvalidOperationException("Real entrance still used Legacy ActionPrepared; standard impacts=" + impacts);
                if (session.TurnEntryFault.Length != 0 || flow.LastError.Length != 0)
                    throw new InvalidOperationException("Real entrance failed: " + session.TurnEntryFault + " / " + flow.LastError);
            }
            if (enemy == 0 || player == 0 || impacts != completed || legacy != 0)
                throw new InvalidOperationException("Actual UI/AI parity did not complete both sides: player=" + player + "; enemy=" + enemy);
            Status = "PASS: Actual authored attack button, cancel, nonempty enemy hit proxy and FlowStateMachine AI; player=" + player + "; enemy=" + enemy + "; impacts/completions=" + impacts + "; Legacy ActionPrepared=0. Explicit basic-only test loadouts; F8 not claimed.";
            yield return new WaitForSecondsRealtime(5);
        }

        private IEnumerator RunLegacyPartialWave(IDRPGBattlePresentationClock clock)
        {
            var firstWave = session.Participants.Where(p => p.Side == DRPGBattleSide.Enemy && p.IsAlive && p.IsDeployed).ToArray();
            if (firstWave.Length != 2) throw new InvalidOperationException("Partial-wave fixture requires two enemies.");
            int reserves = session.ReserveEnemies.Count, completions = 0, publications = 0;
            bool waveSeen = false, earlyRefill = false;
            session.ActionCompleted += _ => completions++;
            session.CurrentActorChanged += _ => publications++;
            for (int action = 0; action < 8 && !waveSeen; action++)
            {
                var actor = session.CurrentActor;
                if (actor == null) throw new InvalidOperationException("Partial-wave fixture lost its actor.");
                var command = actor.Side == DRPGBattleSide.Player
                    ? DRPGBattleCommand.UseSkill(actor.RuntimeId, firstWave.First(p => p.IsAlive).RuntimeId, actor.Build.BasicAttack)
                    : DRPGBattleCommand.Defend(actor.RuntimeId);
                int before = publications;
                if (!session.TryPrepareCommand(command, out _, out var error)) throw new InvalidOperationException(error);
                double deadline = clock.Now + 25;
                while (publications == before && clock.Now < deadline)
                {
                    yield return null; session.TickTurnEntry();
                    bool inWave = session.TransactionArbiter.CurrentOwner.Kind == BattleTransactionKind.WaveTransition;
                    earlyRefill |= firstWave.Any(p => p.IsAlive) && (inWave || session.ReserveEnemies.Count != reserves);
                    waveSeen |= inWave;
                }
                if (publications != before + 1 || completions != action + 1 || earlyRefill || session.TurnEntryFault.Length != 0)
                    throw new InvalidOperationException("Partial legacy wave advanced early or stalled: " + session.TurnEntryFault);
            }
            if (!waveSeen || firstWave.Any(p => p.IsAlive || p.IsDeployed) || session.ReserveEnemies.Count != reserves - 2)
                throw new InvalidOperationException("Two exact legacy departures failed to unlock precisely the next two-member wave.");
            Status = "PASS: Two-member wave remains unfilled while one enemy lives; separate Legacy completions accumulate exact departures, then one whole replacement wave enters.";
            yield return new WaitForSecondsRealtime(3);
        }

        private IEnumerator RunLegacyWaveReplacement(IDRPGBattlePresentationClock clock)
        {
            var actor = session.CurrentActor;
            var target = session.Participants.Single(p => p.Side == DRPGBattleSide.Enemy && p.IsDeployed && p.IsAlive);
            int reserves = session.ReserveEnemies.Count, completed = 0, published = 0;
            bool sawWave = false, escaped = false;
            session.ActionCompleted += _ => completed++;
            session.CurrentActorChanged += _ => { published++; escaped |= !sawWave || completed != 1; };
            // Explicit legacy Skill command; not a standard BasicAttack route.
            if (!session.TryPrepareCommand(DRPGBattleCommand.UseSkill(actor.RuntimeId, target.RuntimeId, actor.Build.BasicAttack), out _, out var error))
                throw new InvalidOperationException("Legacy skill fixture rejected: " + error);
            double deadline = clock.Now + 25;
            while (published == 0 && clock.Now < deadline)
            {
                yield return null; session.TickTurnEntry();
                sawWave |= session.TransactionArbiter.CurrentOwner.Kind == BattleTransactionKind.WaveTransition;
            }
            if (escaped || !sawWave || completed != 1 || published != 1 || target.IsAlive || target.IsDeployed ||
                session.ReserveEnemies.Count != reserves - 1 || session.Outcome != DRPGBattleOutcome.None || session.TurnEntryFault.Length != 0)
                throw new InvalidOperationException("Legacy lethal action bypassed whole-wave continuation: wave=" + sawWave + "; completed=" + completed +
                    "; published=" + published + "; departed=" + !target.IsDeployed + "; reserve=" + session.ReserveEnemies.Count + "; fault=" + session.TurnEntryFault);
            Status = "PASS: Legacy Skill lethal action seals departure, completes once, enters next whole wave, then publishes one candidate; no immediate legacy reinforcement.";
            yield return new WaitForSecondsRealtime(3);
        }

        private IEnumerator RunLegacyContinuation(IDRPGBattlePresentationClock clock)
        {
            var menu = presentation.GetComponentInChildren<BattleCommandMenu>(true);
            var button = menu == null ? null : (UnityEngine.UI.Button)typeof(BattleCommandMenu)
                .GetField("defendButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu);
            if (button == null) throw new InvalidOperationException("Actual Defend button missing.");
            double deadline = clock.Now + 8;
            while ((!button.gameObject.activeInHierarchy || !button.interactable) && clock.Now < deadline) yield return null;
            if (!button.gameObject.activeInHierarchy || !button.interactable) throw new InvalidOperationException("Defend button unavailable.");
            var previous = session.CurrentActorTurnToken;
            int completions = 0, publications = 0; bool escaped = false;
            session.ActionCompleted += _ => completions++;
            session.CurrentActorChanged += actor =>
            {
                publications++;
                escaped |= completions != 1 || session.TransactionArbiter.CurrentOwner.Kind != BattleTransactionKind.TurnEntry;
            };
            button.onClick.Invoke();
            deadline = clock.Now + 15;
            while (publications == 0 && clock.Now < deadline)
            { yield return null; session.TickTurnEntry(); }
            if (publications != 1 || completions != 1 || escaped || session.CurrentActorTurnToken == previous || session.TurnEntryFault.Length != 0)
                throw new InvalidOperationException("Legacy Defend bypassed deferred TurnEntry: completed=" + completions + "; published=" + publications + "; escaped=" + escaped + "; fault=" + session.TurnEntryFault);
            Status = "PASS: Real Defend button preserves legacy action but yields once to the shared gated TurnEntry; one completion and one actor publication.";
            yield return new WaitForSecondsRealtime(3);
        }

        private static DRPGBattleSessionCreationResult CreateBasicOnlyVerificationRoster(DRPGBattleSessionCreationResult original)
        {
            // Existing legacy enemy profiles use skills[0] as BasicAttack,
            // including enemy_special_triple. This acceptance fixture explicitly
            // builds ordinary-attack-only enemies; no profile/SO/AI is modified.
            var actors = original.Participants.Select(p =>
            {
                if (p.Side != DRPGBattleSide.Enemy) return p;
                var basic = p.Profile.Skills.Single(s => s != null && s.SkillId == "basic_attack");
                var build = new DRPGResolvedCombatantBuild(p.Profile, null, basic,
                    Array.Empty<DRPGSkillSO>(), new[] { basic }, Array.Empty<DRPGPassiveModifierSO>());
                return new BattleParticipantRuntime(p.RuntimeId, new BattleParticipantSpec(p.Profile, p.Side, p.RosterRole,
                    p.FormationSlot, build, p.FormationRow, p.PreferredFormationRow, p.IsDeployed, p.PartyOrderIndex));
            }).ToArray();
            return DRPGBattleSessionCreationResult.Success(actors, original.ReserveEnemies, original.FormationRules);
        }
        private void OnDestroy()
        {
            Application.logMessageReceived -= Observe;
            (run as IDisposable)?.Dispose(); run = null;
            module?.Dispose(); module = null;
            if (session != null && session.Outcome == DRPGBattleOutcome.None) { session.Abort(); for (int i = 0; i < 4; i++) session.TickTurnEntry(); }
            if (presentation != null) presentation.Hide();
            suspension?.Dispose(); suspension = null;
        }

        private static void SeedVisibleStatuses(DRPGBattleSession battle)
        {
            var player = battle.Participants.First(p => p.Side == DRPGBattleSide.Player && p.IsDeployed);
            var enemy = battle.Participants.First(p => p.Side == DRPGBattleSide.Enemy && p.IsDeployed);
            var owner = BattleTransactionIdentity.ForTestHarness(new BattleStartIdentity(battle.BattleGeneration, 701));
            if (!battle.TransactionArbiter.TryAcquire(owner, out var lease, out var error)) throw new InvalidOperationException(error);
            using (lease)
            {
                var draft = battle.TransactionState.CreateWorkspace();
                AddStatus(draft, player.RuntimeId, "verification-party-poison", "中毒", -5, 2);
                AddStatus(draft, player.RuntimeId, "verification-party-regen", "再生", 3, 1);
                AddStatus(draft, enemy.RuntimeId, "verification-enemy-poison", "中毒", -7, 1);
                if (!battle.TransactionState.TryCommitJournal(lease, draft).Succeeded) throw new InvalidOperationException("Visible status fixture failed to commit.");
            }
        }
        private static void AddStatus(DRPGBattleMutationWorkspace draft, int owner, string id, string caption, float delta, int stacks)
        {
            var definition = ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
            try
            {
                void Set(string field, object value) => typeof(DRPGStatusDefinitionSO).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(definition, value);
                Set("statusId", id); Set("displayName", caption); Set("triggerTiming", DRPGStatusTriggerTiming.AfterAction);
                Set("durationTurns", 2); Set("maxStacks", 3); Set("turnStartHpDelta", delta);
                draft.For(new ParticipantId(owner)).ApplyStatus(DRPGResolvedStatusSpec.From(definition), stacks, 0, new ParticipantId(owner));
            }
            finally { DestroyImmediate(definition); }
        }
    }
    public static string ValidateAuthoredBinding()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameResources/DRPG/Prefabs/PF_DRPGBattlePresentation.prefab");
        if (prefab == null) throw new InvalidOperationException("Formal battle presentation prefab missing.");
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("BattleStandardPresentationBinding", false)).FirstOrDefault(t => t != null);
        if (type == null || prefab.GetComponent(type) == null)
            return "FAIL: Formal prefab has no standard transaction presentation binding.";
        var binding = new SerializedObject(prefab.GetComponent(type));
        foreach (string name in new[] { "actionName", "actionNameGroup" })
            if (binding.FindProperty(name)?.objectReferenceValue == null) return "FAIL: Missing authored " + name;
        return "PASS: Authored binding and action announcement references exist (not a PlayMode acceptance).";
    }
}
#endif
