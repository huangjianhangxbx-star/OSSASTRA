#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// These probes intentionally exercise live production objects.  They are RED
// specifications for the old basic-attack path, not substitutes for it.
public static class DRPGBattleBasicAttackProductionPresentationProbe
{
    private const string LethalLeaseCase =
        "LethalTargetRemainsLeasedUntilReturn";
    private const string RecoilCase =
        "RecoilIsPureHorizontalAndRebindSafe";
    private const string ParityCase =
        "PlayerAndEnemyImpactFeedbackAreSymmetric";
    private const string AttackerRestoreCase =
        "AttackerRestoresWithoutVisualRootSnap";
    private const string SettlementCase =
        "PostActionSettlementIsSerialAndBlocksContinuation";

    public static void LethalTargetRemainsLeasedUntilReturn()
    {
        using (var fixture = new DRPGBattleBasicAttackTransactionFixture())
        {
            GameObject root = fixture.CreateTracked("LegacyLeaseRoot");
            BattleWorldActorPool pool = root.AddComponent<BattleWorldActorPool>();
            BattleWorldRosterPresenter roster = root.AddComponent<BattleWorldRosterPresenter>();
            roster.Configure(pool, null, null);

            DRPGCombatantProfileSO playerProfile = CreateProfile(30f);
            DRPGCombatantProfileSO enemyProfile = CreateProfile(20f);
            DRPGSkillSO lethal = ScriptableObject.CreateInstance<DRPGSkillSO>();
            try
            {
                SetField(lethal, "skillId", "legacy_lethal_lease");
                SetField(lethal, "power", 10000f);
                SetField(playerProfile, "skills", new List<DRPGSkillSO> { lethal });
                BattleParticipantRuntime player = CreateParticipant(
                    101, playerProfile, DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist);
                BattleParticipantRuntime enemy = CreateParticipant(
                    102, enemyProfile, DRPGBattleSide.Enemy,
                    DRPGBattleRosterRole.Enemy);
                var session = new DRPGBattleSession(new[] { player, enemy });
                DRPGBattleUGUISurface surface = root.AddComponent<DRPGBattleUGUISurface>();
                surface.ConfigureWorldPresentation(null, roster, null);
                surface.Bind(session);
                session.Start();
                RequireSetup(ReferenceEquals(session.CurrentActor, player),
                    "The lethal lease probe must begin on the configured player actor.");
                surface.RenderInitial(DRPGBattlePresentationSnapshot.Capture(session));
                DRPGBattlePresentationEventBridge bridge =
                    root.AddComponent<DRPGBattlePresentationEventBridge>();
                DRPGBattlePresentationDirector director =
                    root.AddComponent<DRPGBattlePresentationDirector>();
                bridge.Configure(director, surface, NullBattleAudioCueSink.Instance);
                bridge.Bind(session, 1L);
                ViewIdentitySnapshot before = fixture.CaptureViewIdentity(pool, enemy.RuntimeId);
                DRPGBattleCommand command = DRPGBattleCommand.BasicAttack(
                    player.RuntimeId,
                    enemy.RuntimeId,
                    lethal);
                RequireSetup(command.Kind == DRPGBattleCommandKind.BasicAttack &&
                        ReferenceEquals(player.Build.BasicAttack, lethal),
                    "The lethal lease probe did not construct a real basic attack.");
                RequireSetup(session.TryPrepareCommand(command,
                    out DRPGBattlePreparedAction prepared, out string error), error);
                RequireSetup(session.TryResolveImpact(
                    prepared,
                    out _,
                    out error), error);
                RequireSetup(!enemy.IsAlive && enemy.CurrentHp <= 0f,
                    "The configured basic attack did not produce a lethal impact.");
                RequireSetup(bridge.PendingSignalCount > 0,
                    "The real ParticipantChanged signal was not queued by the event bridge.");
                bridge.FlushQueuedSignals();
                ViewIdentitySnapshot after = fixture.CaptureViewIdentity(pool, enemy.RuntimeId);

                RequireSetup(before.Active && before.InstanceId != 0,
                    "The real world roster did not acquire the target view.");
                RequireInvariant(
                    LethalLeaseCase,
                    after.Active && after.InstanceId == before.InstanceId,
                    "A lethal participant change releases the world view before the return barrier.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(lethal);
                UnityEngine.Object.DestroyImmediate(playerProfile);
                UnityEngine.Object.DestroyImmediate(enemyProfile);
            }
        }
    }

    public static void RecoilIsPureHorizontalAndRebindSafe()
    {
        throw new InvalidOperationException(
            "RecoilIsPureHorizontalAndRebindSafe requires PlayMode frames; call RunCoroutine.");
    }

    private static IEnumerator RecoilIsPureHorizontalAndRebindSafeCoroutine()
    {
        using (var fixture = new DRPGBattleBasicAttackTransactionFixture())
        {
            GameObject root = fixture.CreateTracked("LegacyRecoilRoot");
            BattleWorldActorPool pool = root.AddComponent<BattleWorldActorPool>();
            BattleWorldRosterPresenter roster =
                root.AddComponent<BattleWorldRosterPresenter>();
            roster.Configure(pool, null, null);
            DRPGCombatantProfileSO playerProfile = CreateProfile(30f);
            DRPGCombatantProfileSO enemyProfile = CreateProfile(20f);
            DRPGSkillSO attack = ScriptableObject.CreateInstance<DRPGSkillSO>();
            BattleConflictPresentationContext context =
                root.AddComponent<BattleConflictPresentationContext>();
            try
            {
                SetField(attack, "skillId", "legacy_recoil_basic_attack");
                SetField(attack, "power", 1f);
                SetField(playerProfile, "skills",
                    new List<DRPGSkillSO> { attack });
                BattleParticipantRuntime player = CreateParticipant(
                    201,
                    playerProfile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist);
                BattleParticipantRuntime enemy = CreateParticipant(
                    202,
                    enemyProfile,
                    DRPGBattleSide.Enemy,
                    DRPGBattleRosterRole.Enemy);
                var session = new DRPGBattleSession(new[] { player, enemy });
                DRPGBattleUGUISurface surface =
                    root.AddComponent<DRPGBattleUGUISurface>();
                surface.ConfigureWorldPresentation(null, roster, null);
                surface.Bind(session);
                session.Start();
                RequireSetup(ReferenceEquals(session.CurrentActor, player),
                    "The recoil probe must begin on the configured player actor.");
                surface.RenderInitial(
                    DRPGBattlePresentationSnapshot.Capture(session));
                RequireSetup(pool.TryGet(
                        enemy.RuntimeId,
                        out BattleWorldActorView target) && target != null,
                    "The real world roster did not acquire the recoil target.");

                DRPGBattlePresentationEventBridge bridge =
                    root.AddComponent<DRPGBattlePresentationEventBridge>();
                DRPGBattlePresentationDirector director =
                    root.AddComponent<DRPGBattlePresentationDirector>();
                bridge.Configure(
                    director,
                    surface,
                    NullBattleAudioCueSink.Instance);
                bridge.Bind(session, 1L);
                ConfigureConflictContext(root, context, pool);
                BattlePresentationRequest request = CreateRequest(
                    player.RuntimeId,
                    enemy.RuntimeId);
                var observations = new List<TransformObservation>();
                Vector3 basePosition = target.transform.position;
                Vector3 baseScale = target.transform.localScale;
                fixture.SampleProductionTransform(
                    target.transform,
                    "EnemyActorMotionRoot",
                    observations);
                IBattlePresentationHandle feedback =
                    context.PlayImpactFeedback(
                        request,
                        new BattleFeedbackPresentationDefinition());
                RequireSetup(feedback != null && !feedback.IsSettled,
                    "The production conflict feedback did not start a recoil handle.");

                double movementDeadline =
                    Time.realtimeSinceStartupAsDouble + 0.25d;
                while (MaxPositionExcursion(observations, basePosition) <=
                           0.001f &&
                       Time.realtimeSinceStartupAsDouble < movementDeadline)
                {
                    yield return null;
                    fixture.SampleProductionTransform(
                        target.transform,
                        "EnemyActorMotionRoot",
                        observations);
                }
                RequireSetup(MaxPositionExcursion(observations, basePosition) >
                        0.001f,
                    "The production recoil never moved the target.");

                DRPGBattleCommand command = DRPGBattleCommand.BasicAttack(
                    player.RuntimeId,
                    enemy.RuntimeId,
                    attack);
                RequireSetup(session.TryPrepareCommand(
                    command,
                    out DRPGBattlePreparedAction prepared,
                    out string error), error);
                RequireSetup(session.TryResolveImpact(
                    prepared,
                    out _,
                    out error), error);
                RequireSetup(bridge.PendingSignalCount > 0,
                    "ParticipantChanged was not queued during the live recoil.");
                int targetInstanceId = target.GetInstanceID();
                yield return new WaitForEndOfFrame();
                fixture.SampleProductionTransform(
                    target.transform,
                    "EnemyActorMotionRoot",
                    observations);
                RequireSetup(bridge.PendingSignalCount == 0,
                    "The queued ParticipantChanged signal did not traverse LateUpdate.");
                RequireSetup(pool.TryGet(enemy.RuntimeId, out var rebound) &&
                        rebound != null,
                    "The non-lethal target disappeared during roster rebind.");

                double feedbackDeadline =
                    Time.realtimeSinceStartupAsDouble + 2d;
                while (!feedback.IsSettled &&
                    Time.realtimeSinceStartupAsDouble < feedbackDeadline)
                {
                    yield return null;
                    fixture.SampleProductionTransform(
                        target.transform,
                        "EnemyActorMotionRoot",
                        observations);
                }
                RequireSetup(observations.Count >= 8,
                    "The recoil journal did not capture multiple production frames.");
                bool settled = feedback.IsSettled &&
                    feedback.Outcome == BattlePresentationOutcome.Completed;
                bool sameInstance = rebound.GetInstanceID() == targetInstanceId;
                bool horizontal = IsPureHorizontal(observations, basePosition);
                bool scaleInvariant = IsScaleInvariant(observations, baseScale);
                bool singlePath = IsSingleOutAndBackPath(
                    observations,
                    basePosition);
                RequireInvariant(
                    RecoilCase,
                    settled && sameInstance && horizontal && scaleInvariant &&
                    singlePath,
                    "Production recoil or its LateUpdate rebind changes scale, " +
                    "interrupts or kinks the horizontal path, or replaces the " +
                    "target view. settled=" + settled +
                    "; sameInstance=" + sameInstance +
                    "; horizontal=" + horizontal +
                    "; scaleInvariant=" + scaleInvariant +
                    "; singlePath=" + singlePath +
                    "; samples=" + observations.Count +
                    "; excursion=" + MaxPositionExcursion(
                        observations,
                        basePosition));
            }
            finally
            {
                context.RestoreAll();
                UnityEngine.Object.DestroyImmediate(attack);
                UnityEngine.Object.DestroyImmediate(playerProfile);
                UnityEngine.Object.DestroyImmediate(enemyProfile);
            }
        }
    }

    public static void PlayerAndEnemyImpactFeedbackAreSymmetric()
    {
        throw new InvalidOperationException(
            "PlayerAndEnemyImpactFeedbackAreSymmetric requires PlayMode " +
            "frames; call RunCoroutine.");
    }

    private static IEnumerator PlayerAndEnemyImpactFeedbackAreSymmetricCoroutine()
    {
        using (var fixture = new DRPGBattleBasicAttackTransactionFixture())
        {
            GameObject root = fixture.CreateTracked("LegacyImpactParityRoot");
            BattleImpactFeedbackPresenter presenter =
                root.AddComponent<BattleImpactFeedbackPresenter>();
            DRPGCombatantProfileSO playerProfile = CreateProfile();
            DRPGCombatantProfileSO enemyProfile = CreateProfile();
            BattlePartyMemberView playerView = CreatePartyView(
                root.transform,
                301,
                playerProfile);
            BattlefieldEnemyView enemyView = CreateEnemyView(
                root.transform,
                302,
                enemyProfile);
            BattlePartyHud partyHud = root.AddComponent<BattlePartyHud>();
            BattlefieldEnemyRosterView enemyRoster = root.AddComponent<BattlefieldEnemyRosterView>();
            SetField(partyHud, "memberViews", new List<BattlePartyMemberView> { playerView });
            SetField(enemyRoster, "enemyViews", new List<BattlefieldEnemyView> { enemyView });
            SetField(presenter, "partyHud", partyHud);
            SetField(presenter, "enemyRoster", enemyRoster);
            GameObject feedbackLayerObject = new GameObject(
                "FeedbackLayer",
                typeof(RectTransform));
            RectTransform feedbackLayer =
                feedbackLayerObject.transform as RectTransform;
            feedbackLayer.SetParent(root.transform, false);
            GameObject numberObject = new GameObject(
                "DamageNumberTemplate",
                typeof(RectTransform));
            RectTransform numberRect = numberObject.transform as RectTransform;
            numberRect.SetParent(feedbackLayer, false);
            BattleDamageNumberView numberTemplate =
                numberObject.AddComponent<BattleDamageNumberView>();
            SetField(numberTemplate, "rectTransform", numberRect);
            SetField(numberTemplate, "canvasGroup",
                numberObject.AddComponent<CanvasGroup>());
            numberObject.SetActive(false);
            SetField(presenter, "feedbackLayer", feedbackLayer);
            SetField(presenter, "damageNumberTemplate", numberTemplate);

            try
            {
                List<IBattlePresentationHandle> impactHandles =
                    GetField<List<IBattlePresentationHandle>>(
                        presenter,
                        "activeImpactHandles");
                List<BattleDamageNumberView> activeNumbers =
                    GetField<List<BattleDamageNumberView>>(
                        presenter,
                        "activeNumbers");
                Vector3 playerBaseScale = playerView.transform.localScale;
                int playerIssuedFrame = Time.frameCount;
                presenter.Present(new[] { CreateHit(302, 301,
                    DRPGBattleRosterRole.Protagonist) }, 0.5f);
                int playerNumberFrame = activeNumbers.Count > 0
                    ? Time.frameCount
                    : -1;
                int playerLegacyTracks = impactHandles.Count;
                RequireSetup(activeNumbers.Count == 1,
                    "The player impact did not issue its damage number.");
                yield return null;
                float playerScaleDelta = Vector3.Distance(
                    playerBaseScale,
                    playerView.transform.localScale);
                presenter.CancelAll();
                yield return null;

                Vector3 enemyBaseScale = enemyView.transform.localScale;
                int enemyIssuedFrame = Time.frameCount;
                presenter.Present(new[] { CreateHit(301, 302,
                    DRPGBattleRosterRole.Enemy) }, 0.5f);
                int enemyNumberFrame = activeNumbers.Count > 0
                    ? Time.frameCount
                    : -1;
                int enemyLegacyTracks = impactHandles.Count;
                RequireSetup(activeNumbers.Count == 1,
                    "The enemy impact did not issue its damage number.");
                yield return null;
                float enemyScaleDelta = Vector3.Distance(
                    enemyBaseScale,
                    enemyView.transform.localScale);

                int playerNumberOffset = playerNumberFrame -
                    playerIssuedFrame;
                int enemyNumberOffset = enemyNumberFrame -
                    enemyIssuedFrame;
                RequireInvariant(
                    ParityCase,
                    playerNumberOffset == enemyNumberOffset &&
                    playerLegacyTracks == 0 && enemyLegacyTracks == 0 &&
                    playerScaleDelta < 0.0001f &&
                    enemyScaleDelta < 0.0001f,
                    "Player and enemy impact feedback diverge: damage-number " +
                    "offsets=" + playerNumberOffset + "/" +
                    enemyNumberOffset + ", legacy tracks=" +
                    playerLegacyTracks + "/" + enemyLegacyTracks +
                    ", scale deltas=" + playerScaleDelta + "/" +
                    enemyScaleDelta + ".");
            }
            finally
            {
                presenter.CancelAll();
                UnityEngine.Object.DestroyImmediate(playerProfile);
                UnityEngine.Object.DestroyImmediate(enemyProfile);
            }
        }
    }

    public static void AttackerRestoresWithoutVisualRootSnap()
    {
        throw new InvalidOperationException(
            "AttackerRestoresWithoutVisualRootSnap requires PlayMode frames; call RunCoroutine.");
    }

    private static IEnumerator AttackerRestoresWithoutVisualRootSnapCoroutine()
    {
        using (var fixture = new DRPGBattleBasicAttackTransactionFixture())
        {
            GameObject root = fixture.CreateTracked("LegacyRestoreRoot");
            BattleWorldActorPool pool = root.AddComponent<BattleWorldActorPool>();
            BattleWorldActorView attacker = pool.Acquire(401);
            BattleWorldActorView target = pool.Acquire(402);
            Transform visualRoot = attacker.VisualRoot;
            SpriteRenderer renderer =
                visualRoot.gameObject.AddComponent<SpriteRenderer>();
            BattleWorldActorSpriteDriver driver = visualRoot.gameObject
                .AddComponent<BattleWorldActorSpriteDriver>();
            DRPGCombatantProfileSO attackerProfile = CreateProfile();
            DRPGCombatantProfileSO targetProfile = CreateProfile();
            BattleConflictPresentationContext context =
                root.AddComponent<BattleConflictPresentationContext>();
            try
            {
                driver.Configure(renderer, 2f);
                attacker.ConfigureVisualDriver(driver);
                BattleParticipantRuntime attackerRuntime = CreateParticipant(
                    401,
                    attackerProfile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist);
                BattleParticipantRuntime targetRuntime = CreateParticipant(
                    402,
                    targetProfile,
                    DRPGBattleSide.Enemy,
                    DRPGBattleRosterRole.Enemy);
                attacker.Bind(401, new DRPGBattleParticipantViewModel(
                    attackerRuntime,
                    0f,
                    true));
                target.Bind(402, new DRPGBattleParticipantViewModel(
                    targetRuntime,
                    0f,
                    false));
                ConfigureConflictContext(root, context, pool);
                BattlePresentationRequest request = CreateRequest(401, 402);
                Vector3 home = visualRoot.position;
                context.ApplyActors(
                    request,
                    1f,
                    1f,
                    0f,
                    BattleOtherTargetPresentationPolicy.Keep);
                IBattlePresentationHandle intent =
                    context.PlayActorIntent(request);
                RequireSetup(intent != null,
                    "The production intent path did not return a handle.");
                var intentJournal = new List<TransformObservation>();
                double intentDeadline = Time.realtimeSinceStartupAsDouble + 2d;
                while (!intent.IsSettled &&
                    Time.realtimeSinceStartupAsDouble < intentDeadline)
                {
                    yield return null;
                    fixture.SampleProductionTransform(
                        visualRoot,
                        "AttackerVisualRoot",
                        intentJournal);
                }
                RequireSetup(intent.IsSettled &&
                        intent.Outcome == BattlePresentationOutcome.Completed,
                    "The production intent did not reach its terminal receipt.");
                RequireSetup(Vector3.Distance(
                        visualRoot.position,
                        home) >= 0.30f,
                    "The rendered attacker never entered a conflict pose.");

                IBattlePresentationHandle restore = context.PlayRestore(
                    request,
                    new BattleExitPresentationDefinition());
                RequireSetup(restore != null && !restore.IsSettled,
                    "The production Stage8 restore handle did not start.");
                var restoreJournal = new List<TransformObservation>();
                fixture.SampleProductionTransform(
                    visualRoot,
                    "AttackerVisualRoot.Restore",
                    restoreJournal);
                double restoreDeadline = Time.realtimeSinceStartupAsDouble + 2d;
                while (!restore.IsSettled &&
                    Time.realtimeSinceStartupAsDouble < restoreDeadline)
                {
                    yield return null;
                    fixture.SampleProductionTransform(
                        visualRoot,
                        "AttackerVisualRoot.Restore",
                        restoreJournal);
                }
                fixture.SampleProductionTransform(
                    visualRoot,
                    "AttackerVisualRoot.Restore",
                    restoreJournal);
                RequireSetup(restore.IsSettled,
                    "The production Stage8 restore did not reach a terminal receipt.");
                RequireSetup(Approximately(visualRoot.position, home),
                    "The production Stage8 restore did not reach the home pose.");

                float largestStep = LargestPositionStep(restoreJournal);
                int movingFrames = CountPositionChanges(restoreJournal);
                RequireInvariant(
                    AttackerRestoreCase,
                    restoreJournal.Count >= 8 &&
                    movingFrames >= 2 &&
                    largestStep <= 0.25f,
                    "Stage8 restores the child visual root in one snap " +
                    "(largest frame step=" + largestStep +
                    ", moving frames=" + movingFrames + ").");
            }
            finally
            {
                context.RestoreAll();
                UnityEngine.Object.DestroyImmediate(attackerProfile);
                UnityEngine.Object.DestroyImmediate(targetProfile);
            }
        }
    }

    public static void PostActionSettlementIsSerialAndBlocksContinuation()
    {
        throw new InvalidOperationException(
            "PostActionSettlementIsSerialAndBlocksContinuation requires " +
            "PlayMode frames; call RunCoroutine.");
    }

    private static IEnumerator PostActionSettlementIsSerialAndBlocksContinuationCoroutine()
    {
        DRPGSkillSO attack = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGCombatantProfileSO playerProfile = CreateProfile(30f);
        DRPGCombatantProfileSO enemyProfile = CreateProfile(20f);
        try
        {
            SetField(attack, "skillId", "legacy_settlement_probe");
            SetField(attack, "power", 3f);
            SetField(playerProfile, "skills", new List<DRPGSkillSO> { attack });
            BattleParticipantRuntime player = CreateParticipant(501, playerProfile,
                DRPGBattleSide.Player, DRPGBattleRosterRole.Protagonist);
            BattleParticipantRuntime enemy = CreateParticipant(502, enemyProfile,
                DRPGBattleSide.Enemy, DRPGBattleRosterRole.Enemy);
            var session = new DRPGBattleSession(new[] { player, enemy });
            int completed = 0;
            int participantChanges = 0;
            var continuationOrder = new List<string>();
            session.ParticipantChanged += _ => participantChanges++;
            session.Start();
            RequireSetup(ReferenceEquals(session.CurrentActor, player),
                "The settlement probe must begin on the configured player actor.");
            session.ActionCompleted += _ =>
            {
                completed++;
                continuationOrder.Add("ActionCompleted");
            };
            session.CurrentActorChanged += actor => continuationOrder.Add(
                "CurrentActorChanged:" +
                (actor != null ? actor.RuntimeId.ToString() : "none"));
            DRPGBattleCommand command = DRPGBattleCommand.BasicAttack(
                player.RuntimeId,
                enemy.RuntimeId,
                attack);
            RequireSetup(command.Kind == DRPGBattleCommandKind.BasicAttack &&
                    ReferenceEquals(player.Build.BasicAttack, attack),
                "The settlement probe did not construct a real basic attack.");
            RequireSetup(session.TryPrepareCommand(
                command,
                out DRPGBattlePreparedAction prepared,
                out string error), error);
            RequireSetup(session.TryResolveImpact(
                prepared,
                out _,
                out error), error);
            RequireSetup(session.TryCompleteAction(prepared, out error), error);

            RequireSetup(participantChanges > 0,
                "Impact did not publish the real participant change.");
            RequireInvariant(
                SettlementCase,
                completed == 0 &&
                continuationOrder.Count == 0 &&
                ReferenceEquals(session.CurrentActor, player),
                "Action completion advances before serial Stage9 settlement presentation releases its gate.");

            double settlementStarted = Time.realtimeSinceStartupAsDouble;
            double settlementDeadline = settlementStarted + 3d;
            while (completed == 0 &&
                Time.realtimeSinceStartupAsDouble < settlementDeadline)
            {
                yield return null;
            }
            RequireSetup(completed == 1,
                "Stage9 never released exactly one ActionCompleted event.");
            RequireSetup(!ReferenceEquals(session.CurrentActor, player),
                "Stage9 completed without transferring to the next actor.");
            RequireSetup(continuationOrder.Count >= 2 &&
                    continuationOrder[0] == "ActionCompleted" &&
                    continuationOrder[1].StartsWith(
                        "CurrentActorChanged:",
                        StringComparison.Ordinal),
                "Stage9 continuation did not publish ActionCompleted before " +
                "the next current actor.");
            RequireSetup(
                Time.realtimeSinceStartupAsDouble - settlementStarted >= 0.29d,
                "The empty Stage9 settlement did not preserve its readable dwell.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(attack);
            UnityEngine.Object.DestroyImmediate(playerProfile);
            UnityEngine.Object.DestroyImmediate(enemyProfile);
        }
    }

    private static void ConfigureConflictContext(GameObject root,
        BattleConflictPresentationContext context, BattleWorldActorPool pool)
    {
        BattleHudPresenter hud = root.AddComponent<BattleHudPresenter>();
        hud.Configure(Array.Empty<BattleHudGroupView>());
        BattleCameraPresenter camera = root.AddComponent<BattleCameraPresenter>();
        camera.Configure(root.AddComponent<Camera>(), root.transform);
        BattleStageRoot stageRoot = root.AddComponent<BattleStageRoot>();
        stageRoot.Configure(root.transform);
        BattleStagePresenter stage = root.AddComponent<BattleStagePresenter>();
        stage.Configure(stageRoot);
        context.Configure(hud, camera, stage, pool, null);
    }

    private static BattlePresentationRequest CreateRequest(int actorId, int targetId)
    {
        return new BattlePresentationRequest(1, BattlePresentationMoment.ImpactResolved,
            new BattlePresentationScope(1, 1, 1), Snapshot(actorId, DRPGBattleSide.Player),
            new[] { Snapshot(targetId, DRPGBattleSide.Enemy) }, Array.Empty<int>(),
            "legacy_basic_attack", EAnimationLogicState.Skill_1,
            Array.Empty<BattleHitPresentationSnapshot>(), BattlePresentationFacts.Empty);
    }

    private static BattleParticipantPresentationSnapshot Snapshot(int id, DRPGBattleSide side) =>
        new BattleParticipantPresentationSnapshot(id, "Probe", side,
            side == DRPGBattleSide.Enemy ? DRPGBattleRosterRole.Enemy : DRPGBattleRosterRole.Protagonist,
            10f, 10f, DRPGLowHpPresentationState.Normal, null, null, null);

    private static BattleHitPresentationSnapshot CreateHit(int actorId, int targetId,
        DRPGBattleRosterRole role) => new BattleHitPresentationSnapshot(actorId, targetId,
            "legacy_hit", 10f, 1f, 9f, true, false, false,
            DRPGLowHpPresentationState.Normal, role);

    private static BattlePartyMemberView CreatePartyView(
        Transform parent,
        int id,
        DRPGCombatantProfileSO profile)
    {
        GameObject item = new GameObject("PlayerHud", typeof(RectTransform));
        item.transform.SetParent(parent, false);
        BattlePartyMemberView view = item.AddComponent<BattlePartyMemberView>();
        SetField(view, "portrait", item.AddComponent<Image>());
        view.Bind(new DRPGBattleParticipantViewModel(CreateParticipant(id, profile,
            DRPGBattleSide.Player, DRPGBattleRosterRole.Protagonist), 0f, false));
        return view;
    }

    private static BattlefieldEnemyView CreateEnemyView(
        Transform parent,
        int id,
        DRPGCombatantProfileSO profile)
    {
        GameObject item = new GameObject("EnemyAnchor", typeof(RectTransform));
        item.transform.SetParent(parent, false);
        BattlefieldEnemyView view = item.AddComponent<BattlefieldEnemyView>();
        SetField(view, "portrait", item.AddComponent<Image>());
        view.Bind(new DRPGBattleParticipantViewModel(CreateParticipant(id, profile,
            DRPGBattleSide.Enemy, DRPGBattleRosterRole.Enemy), 0f, false), null);
        return view;
    }

    private static BattleParticipantRuntime CreateParticipant(int id,
        DRPGCombatantProfileSO profile, DRPGBattleSide side, DRPGBattleRosterRole role) =>
        new BattleParticipantRuntime(id, new BattleParticipantSpec(profile, side, role, 0));

    public static IEnumerator RunCoroutine(string caseId)
    {
        if (string.Equals(caseId, RecoilCase, StringComparison.Ordinal))
            return RecoilIsPureHorizontalAndRebindSafeCoroutine();
        if (string.Equals(caseId, ParityCase, StringComparison.Ordinal))
            return PlayerAndEnemyImpactFeedbackAreSymmetricCoroutine();
        if (string.Equals(caseId, AttackerRestoreCase, StringComparison.Ordinal))
            return AttackerRestoresWithoutVisualRootSnapCoroutine();
        if (string.Equals(caseId, SettlementCase, StringComparison.Ordinal))
            return PostActionSettlementIsSerialAndBlocksContinuationCoroutine();
        throw new ArgumentOutOfRangeException(nameof(caseId), caseId,
            "Only DOTween presentation cases require the PlayMode coroutine runner.");
    }

    private static DRPGCombatantProfileSO CreateProfile(float agility = 10f)
    {
        DRPGCombatantProfileSO profile = ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        SetField(profile, "agility", agility);
        return profile;
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, name);
        field.SetValue(target, value);
    }

    private static T GetField<T>(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, name);
        return (T)field.GetValue(target);
    }

    private static float MaxPositionExcursion(
        IReadOnlyList<TransformObservation> observations,
        Vector3 origin)
    {
        float maximum = 0f;
        for (int i = 0; i < observations.Count; i++)
        {
            if (observations[i].Property == "position")
            {
                maximum = Mathf.Max(
                    maximum,
                    Vector3.Distance(origin, observations[i].After));
            }
        }
        return maximum;
    }

    private static bool IsPureHorizontal(
        IReadOnlyList<TransformObservation> observations,
        Vector3 origin)
    {
        for (int i = 0; i < observations.Count; i++)
        {
            TransformObservation observation = observations[i];
            if (observation.Property == "position" &&
                (Mathf.Abs(observation.After.y - origin.y) > 0.0001f ||
                 Mathf.Abs(observation.After.z - origin.z) > 0.0001f))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsScaleInvariant(
        IReadOnlyList<TransformObservation> observations,
        Vector3 expected)
    {
        for (int i = 0; i < observations.Count; i++)
        {
            TransformObservation observation = observations[i];
            if (observation.Property == "localScale" &&
                !Approximately(observation.After, expected))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsSingleOutAndBackPath(
        IReadOnlyList<TransformObservation> observations,
        Vector3 origin)
    {
        var offsets = new List<float>();
        float signedPeak = 0f;
        for (int i = 0; i < observations.Count; i++)
        {
            TransformObservation observation = observations[i];
            if (observation.Property != "position")
                continue;
            float offset = observation.After.x - origin.x;
            offsets.Add(offset);
            if (Mathf.Abs(offset) > Mathf.Abs(signedPeak))
                signedPeak = offset;
        }
        if (offsets.Count < 4 || Mathf.Abs(signedPeak) <= 0.001f)
            return false;

        float direction = Mathf.Sign(signedPeak);
        int peakIndex = 0;
        float peak = float.MinValue;
        for (int i = 0; i < offsets.Count; i++)
        {
            float projected = offsets[i] * direction;
            if (projected < -0.0005f)
                return false;
            if (projected > peak)
            {
                peak = projected;
                peakIndex = i;
            }
        }
        for (int i = 1; i <= peakIndex; i++)
        {
            if (offsets[i] * direction + 0.0005f <
                offsets[i - 1] * direction)
            {
                return false;
            }
        }
        for (int i = peakIndex + 1; i < offsets.Count; i++)
        {
            if (offsets[i] * direction >
                offsets[i - 1] * direction + 0.0005f)
            {
                return false;
            }
        }
        return Mathf.Abs(offsets[offsets.Count - 1]) <= 0.0005f;
    }

    private static float LargestPositionStep(
        IReadOnlyList<TransformObservation> observations)
    {
        float largest = 0f;
        for (int i = 0; i < observations.Count; i++)
        {
            TransformObservation observation = observations[i];
            if (observation.Property == "position")
                largest = Mathf.Max(
                    largest,
                    Vector3.Distance(observation.Before, observation.After));
        }
        return largest;
    }

    private static int CountPositionChanges(
        IReadOnlyList<TransformObservation> observations)
    {
        int count = 0;
        for (int i = 0; i < observations.Count; i++)
        {
            TransformObservation observation = observations[i];
            if (observation.Property == "position" &&
                Vector3.Distance(observation.Before, observation.After) >
                    0.001f)
            {
                count++;
            }
        }
        return count;
    }

    private static bool Approximately(Vector3 left, Vector3 right) =>
        (left - right).sqrMagnitude < 0.000001f;

    private static void RequireSetup(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireInvariant(
        string caseId,
        bool condition,
        string message)
    {
        if (!condition)
            throw new DRPGBattleLegacyInvariantException(caseId, message);
    }
}
#endif
