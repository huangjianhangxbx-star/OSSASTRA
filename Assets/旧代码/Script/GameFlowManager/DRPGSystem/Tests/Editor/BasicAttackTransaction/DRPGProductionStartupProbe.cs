#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class DRPGProductionStartupProbe
{
    public static string Status { get; private set; } = "NotRun";
    public static string Start() => StartCore(false);
    public static string StartNormalAiContinuous() => StartCore(true);
    public static string StartBasicAttackOnlyContinuous() => StartCore(true, basicOnly: true);
    public static string StartMotionRootContinuous() => StartCore(true, basicOnly: true, motionRoots: true);
    public static string StartSettlementNoChainContinuous() => StartCore(true, basicOnly: true, verifySettlementOrder: true);
    internal static IEnumerator ProductionActorAnchorsFollowMotionRoot()
    {
        yield return null;
        GameFlowRoot root = GameFlowRoot.Instance;
        if (root == null || root.drpgGameLoop == null)
            throw new InvalidOperationException(
                "Motion-root production scenario requires GameFlowRoot.");
        if (root.drpgGameLoop.AnkalutePhase == AnkaluteGameLoopPhase.Title &&
            !root.drpgGameLoop.StartGame())
            throw new InvalidOperationException(
                "Motion-root scenario could not enter the strategic hub.");
        if (root.drpgGameLoop.AnkalutePhase ==
            AnkaluteGameLoopPhase.StrategicHub)
        {
            if (!root.drpgGameLoop.OpenDungeonSelection() ||
                !root.drpgGameLoop.LaunchExpedition(
                    new ExpeditionStartRequest(
                        new ExpeditionId("production-motion-root"))))
                throw new InvalidOperationException(
                    "Motion-root scenario could not enter the expedition.");
        }
        string begin = StartMotionRootContinuous();
        if (begin != "Running")
            throw new InvalidOperationException(
                "Motion-root scenario did not start: " + begin);
        float deadline = Time.realtimeSinceStartup + 150f;
        while (Status == "Running" && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (Status == "Running")
            throw new TimeoutException(
                "Motion-root scenario exceeded its bounded runtime.");
        if (!Status.StartsWith("PASS", StringComparison.Ordinal))
            throw new InvalidOperationException(Status);
        while (Resources.FindObjectsOfTypeAll<Host>().Any(host =>
                   host != null && host.gameObject.scene.IsValid()) &&
               Time.realtimeSinceStartup < deadline)
            yield return null;
        if (Resources.FindObjectsOfTypeAll<Host>().Any(host =>
                host != null && host.gameObject.scene.IsValid()))
            throw new TimeoutException(
                "Motion-root fixture did not release its host.");
    }
    internal static IEnumerator ProductionSettlementNoChainFire()
    {
        yield return null;
        GameFlowRoot root = GameFlowRoot.Instance;
        if (root == null || root.drpgGameLoop == null)
            throw new InvalidOperationException("Settlement-order production scenario requires GameFlowRoot.");
        if (root.drpgGameLoop.AnkalutePhase == AnkaluteGameLoopPhase.Title && !root.drpgGameLoop.StartGame())
            throw new InvalidOperationException("Settlement-order scenario could not enter the strategic hub.");
        if (root.drpgGameLoop.AnkalutePhase == AnkaluteGameLoopPhase.StrategicHub &&
            (!root.drpgGameLoop.OpenDungeonSelection() ||
             !root.drpgGameLoop.LaunchExpedition(new ExpeditionStartRequest(new ExpeditionId("production-settlement-order")))))
            throw new InvalidOperationException("Settlement-order scenario could not enter the expedition.");
        string begin = StartSettlementNoChainContinuous();
        if (begin != "Running")
            throw new InvalidOperationException("Settlement-order scenario did not start: " + begin);
        float deadline = Time.realtimeSinceStartup + 150f;
        while (Status == "Running" && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (Status == "Running")
            throw new TimeoutException("Settlement-order scenario exceeded its bounded runtime.");
        if (!Status.StartsWith("PASS", StringComparison.Ordinal))
            throw new InvalidOperationException(Status);
        while (Resources.FindObjectsOfTypeAll<Host>().Any(host =>
                   host != null && host.gameObject.scene.IsValid()) &&
               Time.realtimeSinceStartup < deadline)
            yield return null;
        if (Resources.FindObjectsOfTypeAll<Host>().Any(host =>
                host != null && host.gameObject.scene.IsValid()))
            throw new TimeoutException("Settlement-order fixture did not release its host.");
    }
    public static string StartObserverIsolation() => StartCore(false, true);
    public static string StartCompletionObserverIsolation() => StartCore(false, false, true);
    private static string StartCore(bool continuous, bool observerFailure = false, bool completionObserverFailure = false, bool basicOnly = false, bool motionRoots = false, bool verifySettlementOrder = false)
    {
        if (!Application.isPlaying || Status == "Running" || GameFlowRoot.Instance == null ||
            GameFlowRoot.Instance.drpgBattleFlow == null || !GameFlowRoot.Instance.drpgGameLoop.CanBeginBattle ||
            GameFlowRoot.Instance.drpgBattleFlow.CurrentSession != null)
            throw new InvalidOperationException("Requires idle expedition PlayMode and real battle flow root.");
        Status = "Running";
        new GameObject("ProductionStartupVerification").AddComponent<Host>().Begin(continuous, observerFailure, completionObserverFailure, basicOnly, motionRoots, verifySettlementOrder);
        return Status;
    }
    private sealed class Host : MonoBehaviour
    {
        private DRPGEncounterDefinitionSO encounter;
        private DRPGBattlePresentationSO config;
        private DRPGBattleFlowRoot root;
        private DRPGBattleSession session;
        private bool notified, earlyActor;
        private bool continuous;
        private bool observerFailure;
        private bool completionObserverFailure;
        private int completionNotices;
        private bool basicOnly;
        private bool motionRoots;
        private bool verifySettlementOrder;
        private BattleWorldActorView privateActorTemplate;
        private string motionBindingError;
        private readonly System.Collections.Generic.List<DRPGCombatantProfileSO> acceptanceProfiles = new System.Collections.Generic.List<DRPGCombatantProfileSO>();
        internal void Begin(bool value, bool throwObserver, bool throwCompletion, bool onlyBasic, bool useMotionRoots, bool verifyOrder) { continuous = value; observerFailure = throwObserver; completionObserverFailure = throwCompletion; basicOnly = onlyBasic; motionRoots = useMotionRoots; verifySettlementOrder = verifyOrder; StartCoroutine(Drive()); }
        private void ThrowingStarted(DRPGBattleSession value, DRPGBattleStartRequest request)
        { throw new InvalidOperationException("Intentional startup observer failure."); }
        private void ThrowingCompleted(DRPGBattleCompletionContext value)
        { throw new InvalidOperationException("Intentional completion observer failure."); }
        private void Completed(DRPGBattleCompletionContext value) { completionNotices++; }
        private IEnumerator Drive()
        {
            var run = Run();
            while (true)
            {
                bool more;
                try { more = run.MoveNext(); }
                catch (Exception failure) { Status = "FAIL: " + failure; break; }
                if (!more) break;
                yield return run.Current;
            }
            Destroy(gameObject);
        }
        private void Started(DRPGBattleSession value, DRPGBattleStartRequest request)
        {
            session = value; notified = true;
            earlyActor = value.CurrentActor != null || value.TransactionArbiter.CurrentOwner.Kind != BattleTransactionKind.BattleStart;
            if (motionRoots)
            {
                try
                {
                    var pool = UnityEngine.Resources.FindObjectsOfTypeAll<BattleWorldActorPool>().Single(p => p.gameObject.scene.IsValid() && p.gameObject.activeInHierarchy);
                    var field = typeof(BattleWorldActorPool).GetField("viewPrefab", BindingFlags.Instance | BindingFlags.NonPublic);
                    var template = field.GetValue(pool) as BattleWorldActorView;
                    if (template == null) throw new InvalidOperationException("Formal actor template missing.");
                    privateActorTemplate = Instantiate(template, transform); privateActorTemplate.gameObject.SetActive(false);
                    DRPGBasicAttackActorWiring.Wire(privateActorTemplate);
                    field.SetValue(pool, privateActorTemplate);
                    foreach (var actor in pool.GetComponentsInChildren<BattleWorldActorView>(true))
                        DRPGBasicAttackActorWiring.Wire(actor);
                }
                catch (Exception e) { motionBindingError = e.ToString(); }
            }
        }
        private IEnumerator Run()
        {
            var party = AssetDatabase.LoadAssetAtPath<DRPGPartySetupSO>("Assets/GameResources/DRPG/Progression/Test/Resonance/DRPGParty_ResonanceTest.asset");
            var source = AssetDatabase.LoadAssetAtPath<DRPGEncounterDefinitionSO>("Assets/GameResources/DRPG/Config/Test/DRPGEncounter_Test2.asset");
            if (party == null || source == null || source.Presentation == null) throw new InvalidOperationException("Formal encounter assets missing.");
            encounter = Instantiate(source); config = Instantiate(source.Presentation);
            if (basicOnly)
            {
                // Explicit acceptance content: retain alternate skills and art,
                // but author a genuine Standard basic slot on these private profiles.
                var deployments = new System.Collections.Generic.List<DRPGEnemyDeploymentDefinition>();
                foreach (var entry in source.CreateResolvedEnemyDeployments())
                {
                    var profile = Instantiate(entry.Profile); acceptanceProfiles.Add(profile);
                    var basic = profile.Skills.FirstOrDefault(s => s != null && s.ExecutionMode == DRPGSkillExecutionMode.Standard && s.TargetRule == DRPGSkillTargetRule.SingleEnemy && s.CanUseFrom(DRPGFormationRow.Front));
                    if (basic == null) throw new InvalidOperationException("Acceptance fixture requires authored Standard basic content.");
                    var skills = new System.Collections.Generic.List<DRPGSkillSO> { basic };
                    skills.AddRange(profile.Skills.Where(s => s != basic));
                    typeof(DRPGCombatantProfileSO).GetField("skills", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(profile, skills);
                    deployments.Add(new DRPGEnemyDeploymentDefinition(profile, DRPGFormationRow.Front));
                }
                typeof(DRPGEncounterDefinitionSO).GetField("enemyDeployments", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(encounter, deployments);
            }
            typeof(DRPGEncounterDefinitionSO).GetField("presentation", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(encounter, config);
            typeof(DRPGBattlePresentationSO).GetField("standardBasicAttackTransactionEnabled", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, true);
            root = GameFlowRoot.Instance.drpgBattleFlow;
            if (observerFailure) root.BattleStarted += ThrowingStarted;
            root.BattleStarted += Started;
            var flow = new DRPGBattleFlowStateMachine(root);
            if (!flow.RequestState(new DRPGBattleStartRequest(party, encounter, default, 42,
                basicOnly ? new BasicAttackOnlyAcceptanceOptions(42, "normal-hit") : default)))
                throw new InvalidOperationException("Configured startup rejected: " + flow.LastError);
            session = flow.Session;
            if (!string.IsNullOrEmpty(motionBindingError)) throw new InvalidOperationException(motionBindingError);
            if (session.AcceptanceOptions.Enabled != basicOnly) throw new InvalidOperationException("FlowRoot lost the per-battle acceptance policy.");
            if (!basicOnly)
            {
                var registry = new DevDebugRegistry();
                using (ANKALUTEDevDebugActions.RegisterInto(registry, () => null, () => null, () => "probe", battleSessionResolver: () => session))
                {
                    foreach (var id in new[] { "battle.basic_only.reset", "battle.basic_only.disable" })
                        if (registry.Execute(id).Status != DevDebugActionStatus.Unavailable)
                            throw new InvalidOperationException("Acceptance control mutated a normal battle: " + id);
                }
            }
            if (!session.StandardBasicAttackEnabled || !notified || earlyActor || session.CurrentActor != null)
                throw new InvalidOperationException("Real FlowRoot bypassed frozen standard startup or published actor before BattleStarted.");
            // Changing the temporary config after creation must not reroute this battle.
            typeof(DRPGBattlePresentationSO).GetField("standardBasicAttackTransactionEnabled", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, false);
            float deadline = Time.realtimeSinceStartup + 15;
            while (session.CurrentActor == null && Time.realtimeSinceStartup < deadline)
            { yield return null; flow.Update(.016f); }
            if (session.CurrentActor == null || !session.StandardBasicAttackEnabled || session.TurnEntryFault.Length != 0)
                throw new InvalidOperationException("Configured startup did not reach a gated actor: " + session.TurnEntryFault);
            if (completionObserverFailure)
            {
                root.BattleCompleted += ThrowingCompleted;
                root.BattleCompleted += Completed;
                session.Abort();
                deadline = Time.realtimeSinceStartup + 8;
                while (root.CurrentSession != null && Time.realtimeSinceStartup < deadline)
                { yield return null; flow.Update(.016f); }
                if (completionNotices != 1 || root.CurrentSession != null || flow.IsActive || session.TransactionArbiter.HasOwner)
                    throw new InvalidOperationException("Completion observer interrupted host cleanup or later notifications: notices=" + completionNotices);
                Status = "PASS: Throwing FlowRoot completion observer cannot suppress later observers; Session, host flow and transaction ownership close once.";
                yield break;
            }
            if (continuous)
            {
                var sequence = RunNormalAi(flow);
                try { while (sequence.MoveNext()) yield return sequence.Current; }
                finally { (sequence as IDisposable)?.Dispose(); }
                yield break;
            }
            Status = observerFailure ? "PASS: Throwing FlowRoot BattleStarted observer cannot suppress later observers or cancel the prepared startup; initial wave and candidate still complete." :
                "PASS: Real FlowRoot freezes enabled policy, binds presentation, emits BattleStarted before entry/candidate, runs initial wave, publishes actor; later config mutation cannot reroute session. Disk assets unchanged.";
            yield return new WaitForSecondsRealtime(3);
        }
        private IEnumerator RunNormalAi(DRPGBattleFlowStateMachine flow)
        {
            var presentation = UnityEngine.Resources.FindObjectsOfTypeAll<DRPGBattlePresentationRoot>()
                .FirstOrDefault(p => p.gameObject.scene.IsValid() && p.gameObject.activeInHierarchy && p.GetComponent<BattleStandardPresentationBinding>() != null);
            var menu = presentation == null ? null : presentation.GetComponentInChildren<BattleCommandMenu>(true);
            var button = menu == null ? null : (UnityEngine.UI.Button)typeof(BattleCommandMenu)
                .GetField("basicAttackButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu);
            if (button == null) throw new InvalidOperationException("Real command button unavailable.");
            int completed = 0, enemy = 0, player = 0, legacy = 0, standard = 0;
            int audibleResults = 0;
            int flashedResults = 0;
            int displacedMotionSamples = 0;
            string settlementOrderFault = null;
            DRPGBattleHitResult pendingFlash = null;
            DRPGCommittedActionFacts pendingAudio = null;
            var audioPalette = AssetDatabase.LoadAssetAtPath<DRPGBattleAudioPaletteSO>("Assets/GameResources/DRPG/Audio/Test/DRPGBattleAudioPalette_Test.asset");
            session.ActionPrepared += _ => legacy++;
            session.ActionLedgerCommitted += ledger =>
            {
                if (verifySettlementOrder && standard != completed)
                    settlementOrderFault = "A new impact committed before the previous ActionCompleted.";
                standard++; pendingAudio = ledger; pendingFlash = ledger.Events[0].Hit;
            };
            session.ActionCompleted += action =>
            {
                if (verifySettlementOrder && standard != completed + 1)
                    settlementOrderFault = "ActionCompleted was duplicated or published without one committed action.";
                completed++;
                if (session.Participants.First(p => p.RuntimeId == action.ActorRuntimeId).Side == DRPGBattleSide.Enemy) enemy++; else player++;
            };
            session.CurrentActorChanged += _ =>
            {
                if (verifySettlementOrder && standard != completed)
                    settlementOrderFault = "CurrentActorChanged crossed Stage9 before ActionCompleted.";
            };
            ActorTurnToken selected = default;
            float deadline = Time.realtimeSinceStartup + 120;
            while ((completed < 6 || enemy < 2) && Time.realtimeSinceStartup < deadline && session.Outcome == DRPGBattleOutcome.None)
            {
                var actor = session.CurrentActor;
                if (actor != null && actor.Side == DRPGBattleSide.Player && session.TransactionArbiter.IsCommandInputOpen &&
                    selected != session.CurrentActorTurnToken && button.gameObject.activeInHierarchy && button.interactable)
                {
                    button.onClick.Invoke();
                    if (session.Phase != DRPGBattlePhase.TargetSelection) throw new InvalidOperationException("Normal player bypassed targeting.");
                    var target = session.Participants.First(p => p.Side == DRPGBattleSide.Enemy && p.IsAlive && p.IsDeployed);
                    var proxy = presentation.GetComponentsInChildren<BattleActorScreenHitProxy>(true)
                        .FirstOrDefault(p => p.RuntimeId == target.RuntimeId && p.gameObject.activeInHierarchy && p.raycastTarget);
                    if (proxy == null || proxy.rectTransform.rect.width <= 0 || proxy.rectTransform.rect.height <= 0)
                        throw new InvalidOperationException("Normal target has no nonempty hit proxy.");
                    selected = session.CurrentActorTurnToken;
                    proxy.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));
                }
                yield return null; flow.Update(.016f);
                if (!string.IsNullOrEmpty(settlementOrderFault))
                    throw new InvalidOperationException(settlementOrderFault);
                if (motionRoots)
                {
                    foreach (var view in UnityEngine.Resources.FindObjectsOfTypeAll<BattleWorldActorView>().Where(v => v.gameObject.scene.IsValid() && v.gameObject.activeInHierarchy && v.RuntimeId != 0))
                    {
                        if (view.MotionRoot == view.transform) throw new InvalidOperationException("Formal actor lost the bound motion root.");
                        foreach (var anchor in new[] { view.VisualRoot, view.ShadowAnchor, view.VfxAnchor, view.TargetAnchor, view.StatusAnchor, view.PresentationAnchor })
                            if (anchor == null || anchor.parent != view.MotionRoot) throw new InvalidOperationException("Formal actor anchor detached from motion root.");
                        if (view.MotionRoot.localPosition.sqrMagnitude > .0001f) displacedMotionSamples++;
                    }
                }
                if (pendingFlash != null)
                {
                    var worldTarget = UnityEngine.Resources.FindObjectsOfTypeAll<BattleWorldActorView>().FirstOrDefault(v =>
                        v.gameObject.scene.IsValid() && v.gameObject.activeInHierarchy && v.RuntimeId == pendingFlash.TargetRuntimeId);
                    bool playingFlash = worldTarget != null && worldTarget.VisualRoot != null && worldTarget.VisualRoot.GetComponentsInChildren<SpriteRenderer>()
                        .Any(r => r.sharedMaterial != null && r.sharedMaterial.HasProperty("_AnkaluteHitFlash") && r.sharedMaterial.GetFloat("_AnkaluteHitFlash") > 0);
                    if (pendingFlash.Outcome == DRPGHitOutcome.Evaded)
                    {
                        if (playingFlash) throw new InvalidOperationException("Evaded production result flashed as a hit.");
                        flashedResults++; pendingFlash = null;
                    }
                    else if (playingFlash) { flashedResults++; pendingFlash = null; }
                }
                if (pendingAudio != null)
                {
                    var hit = pendingAudio.Events[0].Hit;
                    var cue = hit.Outcome == DRPGHitOutcome.Evaded ? BattleAudioCue.Miss :
                        hit.Outcome == DRPGHitOutcome.Guarded ? BattleAudioCue.Guard : BattleAudioCue.Hit;
                    AudioEventSO audioEvent; BattleAudioPlaybackChannel channel;
                    if (audioPalette == null || !audioPalette.TryResolve(new BattleAudioCueRequest(cue, hit.ActorRuntimeId, hit.TargetRuntimeId, hit.SkillId), out audioEvent, out channel))
                        throw new InvalidOperationException("Formal result audio palette is unresolved.");
                    var audioRoot = GameFlowRoot.Instance.audioRoot;
                    var sources = audioRoot == null ? Array.Empty<AudioSource>() : audioRoot.GetComponentsInChildren<AudioSource>(true);
                    // Observe real Unity playback, not only an injected cue recorder.
                    // AudioEvent may randomize its clip, so compare its configured clips.
                    var clips = (AudioClip[])typeof(AudioEventSO).GetField("clips", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(audioEvent);
                    if (sources.Any(s => s.isPlaying && s.clip != null && clips.Contains(s.clip)))
                    { audibleResults++; pendingAudio = null; }
                }
                if (session.LastBattleFault.HasValue || session.TurnEntryFault.Length != 0 || flow.LastError.Length != 0)
                    throw new InvalidOperationException("Normal AI integration fault: " + session.TurnEntryFault + " / " + flow.LastError);
            }
            if (completed < 6 || enemy < 2 || player == 0 || standard == 0 || audibleResults != standard || flashedResults != standard)
                throw new InvalidOperationException("Normal AI stalled: completed=" + completed + "; enemy=" + enemy + "; player=" + player +
                    "; legacy=" + legacy + "; standard=" + standard + "; audio source results=" + audibleResults + "; flash results=" + flashedResults + "; phase=" + session.Phase);
            if (verifySettlementOrder && (standard != completed || !string.IsNullOrEmpty(settlementOrderFault)))
                throw new InvalidOperationException("Settlement ordering did not finish one-for-one: " + settlementOrderFault);
            if (basicOnly)
            {
                if (motionRoots && displacedMotionSamples == 0) throw new InvalidOperationException("No real motion-root displacement was rendered.");
                if (legacy != 0 || standard != completed) throw new InvalidOperationException("BasicAttackOnly escaped into a Legacy action.");
                var registry = new DevDebugRegistry();
                using (ANKALUTEDevDebugActions.RegisterInto(registry, () => null, () => null, () => "probe", battleSessionResolver: () => session))
                {
                    var inspection = registry.Execute("battle.basic_only.inspect");
                    if (inspection.Status != DevDebugActionStatus.Succeeded || !inspection.Message.Contains("seed=42") || !inspection.Message.Contains("normal-hit"))
                        throw new InvalidOperationException("Acceptance inspector lost frozen request context.");
                    if (registry.Execute("battle.basic_only.disable").Status != DevDebugActionStatus.Succeeded)
                        throw new InvalidOperationException("Development control did not request acceptance shutdown.");
                    deadline = Time.realtimeSinceStartup + 8;
                    while (root.CurrentSession != null && Time.realtimeSinceStartup < deadline)
                    { yield return null; flow.Update(.016f); }
                    if (root.CurrentSession != null || session.AcceptanceOptions.Enabled || session.TransactionArbiter.HasOwner)
                        throw new InvalidOperationException("Acceptance scope or lease survived battle shutdown.");
                    if (registry.Execute("battle.basic_only.reset").Status != DevDebugActionStatus.Unavailable)
                        throw new InvalidOperationException("Closed acceptance must reject a repeated reset.");
                }
            }
            Status = "PASS: motion-root displaced samples=" + displacedMotionSamples + "; Real FlowRoot and " + (basicOnly ? "scoped BasicAttackOnly with explicit valid acceptance profiles" : "original enemy builds/AI") + " completed " + completed + " actions; player=" + player + "; enemy=" + enemy +
                "; standard impacts=" + standard + "; legacy prepared=" + legacy + "; real AudioSource results=" + audibleResults + "; world flash results=" + flashedResults + ". Actual attack button and target proxies; disk assets unchanged. " + (basicOnly ? "Acceptance closed with no remaining lease." : "No AI/loadout override.") + " Playback state is not a human listening verdict.";
            yield return new WaitForSecondsRealtime(3);
        }

        private void OnDestroy()
        {
            if (root != null) { root.BattleStarted -= Started; root.BattleStarted -= ThrowingStarted; root.BattleCompleted -= ThrowingCompleted; root.BattleCompleted -= Completed; }
            if (session != null && session.IsActive) session.Abort();
            if (encounter != null) Destroy(encounter);
            if (config != null) Destroy(config);
            foreach (var profile in acceptanceProfiles) if (profile != null) Destroy(profile);
        }
    }
}
#endif
