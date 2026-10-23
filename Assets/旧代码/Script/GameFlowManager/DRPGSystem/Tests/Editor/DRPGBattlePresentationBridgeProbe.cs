#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class DRPGBattlePresentationBridgeProbe
{
    public static string Run()
    {
        DRPGSkillSO basicAttack = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGSkillSO attack = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGCombatantProfileSO playerProfile = CreateProfile(
            new[] { basicAttack, attack },
            100f);
        DRPGCombatantProfileSO enemyProfile = CreateProfile(attack, 10f);
        GameObject host = new GameObject("Presentation Bridge Probe");
        ProbeBridgeDirector director = null;

        try
        {
            var player = new BattleParticipantRuntime(
                1,
                new BattleParticipantSpec(
                    playerProfile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist,
                    0));
            var enemy = new BattleParticipantRuntime(
                2,
                new BattleParticipantSpec(
                    enemyProfile,
                    DRPGBattleSide.Enemy,
                    DRPGBattleRosterRole.Enemy,
                    0));
            var session = DRPGBattleHeadlessSessionFactory.Create(new[] { player, enemy });

            BattleSkillMenu skillMenu = CreateSkillMenu(host.transform,
                out Button attackButton);
            BattlefieldEnemyRosterView enemyRoster = CreateEnemyRoster(
                host.transform,
                out Button enemyButton);
            DRPGBattleUGUISurface surface =
                host.AddComponent<DRPGBattleUGUISurface>();
            SetField(surface, "skillMenu", skillMenu);
            SetField(surface, "enemyRoster", enemyRoster);

            director = new ProbeBridgeDirector();
            DRPGBattlePresentationEventBridge bridge =
                host.AddComponent<DRPGBattlePresentationEventBridge>();
            DRPGBattleInteractionController interaction =
                host.AddComponent<DRPGBattleInteractionController>();
            SetField(interaction, "skillMenu", skillMenu);
            SetField(interaction, "enemyRoster", enemyRoster);
            interaction.Configure(
                director,
                surface,
                NullBattleAudioCueSink.Instance,
                () => bridge.CurrentTurnSerial);
            bridge.Configure(
                director,
                surface,
                NullBattleAudioCueSink.Instance,
                interaction);

            surface.Bind(session);
            surface.RenderInitial(
                DRPGBattlePresentationSnapshot.Capture(session));
            bridge.Bind(session, 1L);
            interaction.Bind(session, 1L);
            director.Played += request =>
            {
                if (request.Moment == BattlePresentationMoment.TurnActivated)
                    session.RemoveDefeatedFromTimeline();
            };

            session.Start();
            bridge.FlushQueuedSignals();

            AssertTrue(surface.LastSnapshot.CurrentActor != null,
                "The surface must render the current actor snapshot");
            AssertEqual(
                BattlePresentationMoment.TurnActivated,
                director.LastRequest.Moment,
                "CurrentActorChanged must translate to TurnActivated");
            AssertTrue(bridge.PendingSignalCount > 0,
                "Signals raised during dispatch must wait for the next flush");
            bridge.FlushQueuedSignals();
            AssertEqual(0, bridge.PendingSignalCount,
                "The next flush must process deferred re-entrant signals");

            attackButton.onClick.Invoke();
            AssertEqual(DRPGBattlePhase.TargetSelection, session.Phase,
                "Skill selection must enter target selection through interaction");
            enemyButton.onClick.Invoke();
            AssertEqual(DRPGBattlePhase.TargetSelection, session.Phase,
                "Target confirmation must wait for its presentation gate");
            AssertFalse(interaction.AcceptsInput,
                "Interaction must reject a second click while confirmation runs");

            director.ReleaseGate(BattlePresentationMoment.TargetingChanged);
            AssertEqual(DRPGBattlePhase.PreparingAction, session.Phase,
                "Preparing the command must occur only after gate release");

            interaction.Unbind();
            bridge.Unbind();
            surface.Unbind();

            VerifyPublicEnemyIntentFeedback();
        }
        finally
        {
            director?.Dispose();
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(enemyProfile);
            UnityEngine.Object.DestroyImmediate(playerProfile);
            UnityEngine.Object.DestroyImmediate(attack);
            UnityEngine.Object.DestroyImmediate(basicAttack);
        }

        return "PASS DRPG queued presentation bridge and gated interaction";
    }

    private static void VerifyPublicEnemyIntentFeedback()
    {
        DRPGSkillSO revealedAttack =
            ScriptableObject.CreateInstance<DRPGSkillSO>();
        SetField(revealedAttack, "skillId", "revealed_intent_attack");
        SetField(revealedAttack, "executionMode",
            DRPGSkillExecutionMode.Standard);
        SetField(revealedAttack, "enemyWarningSteps", 0);
        SetField(revealedAttack, "enemyTargetDisclosureMode",
            DRPGEnemyTargetDisclosureMode.RevealAtCommit);
        DRPGCombatantProfileSO playerProfile =
            CreateProfile(revealedAttack, 10f);
        DRPGCombatantProfileSO enemyProfile =
            CreateProfile(revealedAttack, 100f);
        SetField(playerProfile, "baseTimelineDelay", 1000f);
        SetField(enemyProfile, "baseTimelineDelay", 100f);
        GameObject host = new GameObject("Public Enemy Intent Bridge Probe");
        var director = new RecordingBridgeDirector();

        try
        {
            var player = new BattleParticipantRuntime(
                101,
                new BattleParticipantSpec(
                    playerProfile,
                    DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist,
                    0));
            var enemy = new BattleParticipantRuntime(
                102,
                new BattleParticipantSpec(
                    enemyProfile,
                    DRPGBattleSide.Enemy,
                    DRPGBattleRosterRole.Enemy,
                    0));
            var session = DRPGBattleHeadlessSessionFactory.Create(new[] { enemy, player });

            GameObject memberObject = new GameObject(
                "PartyMember",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline));
            memberObject.transform.SetParent(host.transform, false);
            BattlePartyMemberView memberView =
                memberObject.AddComponent<BattlePartyMemberView>();
            Outline targetOutline = memberObject.GetComponent<Outline>();
            targetOutline.enabled = false;
            SetField(memberView, "targetOutline", targetOutline);

            BattlePartyHud partyHud = host.AddComponent<BattlePartyHud>();
            SetField(partyHud, "memberViews",
                new List<BattlePartyMemberView> { memberView });
            DRPGBattleUGUISurface surface =
                host.AddComponent<DRPGBattleUGUISurface>();
            SetField(surface, "partyHud", partyHud);
            DRPGBattlePresentationEventBridge bridge =
                host.AddComponent<DRPGBattlePresentationEventBridge>();
            bridge.Configure(
                director,
                surface,
                NullBattleAudioCueSink.Instance);

            surface.Bind(session);
            surface.RenderInitial(
                DRPGBattlePresentationSnapshot.Capture(session));
            bridge.Bind(session, 2L);
            session.Start();
            bridge.FlushQueuedSignals();

            AssertTrue(targetOutline.enabled,
                "RevealAtCommit must survive the player-turn refresh as public target feedback");
            AssertEqual(
                1,
                director.Count(BattlePresentationMoment.TargetingChanged),
                "A public ordinary intent must emit one targeting presentation request");

            bridge.Unbind();
            surface.Unbind();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(enemyProfile);
            UnityEngine.Object.DestroyImmediate(playerProfile);
            UnityEngine.Object.DestroyImmediate(revealedAttack);
        }
    }

    private static BattleSkillMenu CreateSkillMenu(
        Transform parent,
        out Button button)
    {
        GameObject menuObject = new GameObject("Skill Menu");
        menuObject.transform.SetParent(parent, false);
        GameObject buttonObject = new GameObject(
            "Attack",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(menuObject.transform, false);
        button = buttonObject.GetComponent<Button>();
        BattleSkillMenu menu = menuObject.AddComponent<BattleSkillMenu>();
        SetField(menu, "buttons", new List<Button> { button });
        SetField(menu, "labels", new List<TMP_Text>());
        return menu;
    }

    private static BattlefieldEnemyRosterView CreateEnemyRoster(
        Transform parent,
        out Button button)
    {
        GameObject rosterObject = new GameObject("Enemy Roster");
        rosterObject.transform.SetParent(parent, false);
        GameObject enemyObject = new GameObject(
            "Enemy",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        enemyObject.transform.SetParent(rosterObject.transform, false);
        button = enemyObject.GetComponent<Button>();
        BattlefieldEnemyView enemyView =
            enemyObject.AddComponent<BattlefieldEnemyView>();
        SetField(enemyView, "targetButton", button);
        BattlefieldEnemyRosterView roster =
            rosterObject.AddComponent<BattlefieldEnemyRosterView>();
        SetField(roster, "enemyViews", new List<BattlefieldEnemyView>
        {
            enemyView
        });
        return roster;
    }

    private static DRPGCombatantProfileSO CreateProfile(
        DRPGSkillSO skill,
        float agility) => CreateProfile(new[] { skill }, agility);

    private static DRPGCombatantProfileSO CreateProfile(
        IReadOnlyList<DRPGSkillSO> skills,
        float agility)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        SetField(profile, "agility", agility);
        SetField(profile, "skills", new List<DRPGSkillSO>(skills));
        return profile;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        Type type = target.GetType();
        while (type != null)
        {
            FieldInfo field = type.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }
            type = type.BaseType;
        }
        throw new MissingFieldException(target.GetType().Name, fieldName);
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertFalse(bool value, string message)
    {
        AssertTrue(!value, message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }
}

public sealed class RecordingBridgeDirector : IBattlePresentationDirector
{
    private readonly List<BattlePresentationRequest> requests =
        new List<BattlePresentationRequest>();
    private readonly BattleInputGate inputGate = new BattleInputGate();

    public BattleInputGate InputGate => inputGate;

    public BattlePresentationRun Play(BattlePresentationRequest request)
    {
        requests.Add(request);
        return null;
    }

    public void CancelScope(
        BattlePresentationScope scope,
        BattlePresentationCancelReason reason)
    {
    }

    public int Count(BattlePresentationMoment moment)
    {
        int count = 0;
        for (int i = 0; i < requests.Count; i++)
        {
            if (requests[i].Moment == moment)
                count++;
        }
        return count;
    }
}

public sealed class BridgeProbeCueSO : BattlePresentationCueSO
{
    public override BattlePresentationCueKind Kind =>
        BattlePresentationCueKind.ScreenFlash;
}

public sealed class ProbeBridgeCueExecutor : IBattlePresentationCueExecutor
{
    private readonly List<StartedCue> started = new List<StartedCue>();

    public BattlePresentationCueKind Kind =>
        BattlePresentationCueKind.ScreenFlash;
    public IReadOnlyList<StartedCue> Started => started;

    public IBattlePresentationHandle Play(
        BattlePresentationCueSO cue,
        BattlePresentationRequest request)
    {
        var handle = new BattlePresentationHandle();
        started.Add(new StartedCue(request.Moment, handle));
        if (request.Moment != BattlePresentationMoment.TargetingChanged)
            handle.TrySettle(BattlePresentationOutcome.Completed);
        return handle;
    }

    public sealed class StartedCue
    {
        public StartedCue(
            BattlePresentationMoment moment,
            BattlePresentationHandle handle)
        {
            Moment = moment;
            Handle = handle;
        }

        public BattlePresentationMoment Moment { get; }
        public BattlePresentationHandle Handle { get; }
    }
}

public sealed class ProbeBridgeDirector :
    IBattlePresentationDirector,
    IDisposable
{
    private readonly List<ScriptableObject> created =
        new List<ScriptableObject>();
    private readonly ProbeBridgeCueExecutor executor =
        new ProbeBridgeCueExecutor();
    private readonly BattlePresentationDirector core;

    public ProbeBridgeDirector()
    {
        BattlePresentationRecipeSetSO set =
            ScriptableObject.CreateInstance<BattlePresentationRecipeSetSO>();
        created.Add(set);
        SetField(set, "baseRecipes", new List<BattlePresentationRecipeSO>
        {
            CreateRecipe(BattlePresentationMoment.TurnActivated,
                BattlePresentationGateKind.None),
            CreateRecipe(BattlePresentationMoment.TargetingChanged,
                BattlePresentationGateKind.Input)
        });
        core = new BattlePresentationDirector(
            new BattlePresentationRecipeResolver(),
            set,
            new[] { executor },
            new BattleInputGate(),
            new BattlePresentationTraceBuffer(32));
    }

    public event Action<BattlePresentationRequest> Played;

    public BattlePresentationRequest LastRequest { get; private set; }
    public BattleInputGate InputGate => core.InputGate;

    public BattlePresentationRun Play(BattlePresentationRequest request)
    {
        LastRequest = request;
        BattlePresentationRun run = core.Play(request, 0f);
        Played?.Invoke(request);
        return run;
    }

    public void CancelScope(
        BattlePresentationScope scope,
        BattlePresentationCancelReason reason)
    {
        core.CancelScope(scope, reason);
    }

    public void ReleaseGate(BattlePresentationMoment moment)
    {
        for (int i = executor.Started.Count - 1; i >= 0; i--)
        {
            ProbeBridgeCueExecutor.StartedCue cue = executor.Started[i];
            if (cue.Moment == moment && !cue.Handle.IsSettled)
            {
                cue.Handle.TrySettle(BattlePresentationOutcome.Completed);
                core.Tick(0f);
                return;
            }
        }
        throw new Exception("No pending cue for " + moment);
    }

    public void Dispose()
    {
        for (int i = created.Count - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(created[i]);
    }

    private BattlePresentationRecipeSO CreateRecipe(
        BattlePresentationMoment moment,
        BattlePresentationGateKind gateKind)
    {
        BridgeProbeCueSO cue =
            ScriptableObject.CreateInstance<BridgeProbeCueSO>();
        created.Add(cue);
        var entry = new BattlePresentationCueEntry();
        SetField(entry, "cue", cue);
        SetField(entry, "condition", BattlePresentationCondition.Always);
        SetField(entry, "lane", BattlePresentationLaneKind.ScreenFX);
        SetField(entry, "conflictPolicy", BattlePresentationConflictPolicy.Blend);
        SetField(entry, "blocksBeat", true);
        var beat = new BattlePresentationBeat();
        SetField(beat, "beatId", moment + "_beat");
        SetField(beat, "releasesGate", gateKind != BattlePresentationGateKind.None);
        SetField(beat, "cues", new List<BattlePresentationCueEntry> { entry });
        BattlePresentationRecipeSO recipe =
            ScriptableObject.CreateInstance<BattlePresentationRecipeSO>();
        created.Add(recipe);
        SetField(recipe, "recipeId", moment + "_recipe");
        SetField(recipe, "moment", moment);
        SetField(recipe, "gateKind", gateKind);
        SetField(recipe, "maximumGateSeconds", 5f);
        SetField(recipe, "beats", new List<BattlePresentationBeat> { beat });
        return recipe;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        Type type = target.GetType();
        while (type != null)
        {
            FieldInfo field = type.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }
            type = type.BaseType;
        }
        throw new MissingFieldException(target.GetType().Name, fieldName);
    }
}
#endif
