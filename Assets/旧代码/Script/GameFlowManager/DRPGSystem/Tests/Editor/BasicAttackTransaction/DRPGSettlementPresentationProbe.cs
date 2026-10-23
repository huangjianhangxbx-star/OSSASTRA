#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

internal static class DRPGSettlementPresentationProbe
{
    internal static void RunAll()
    { RealAdapterProjectsOneRootAtATime(); EmptyStageHasOneReadableHandoff(); ClosingScopeCancelsSettlementFeedback(); }

    // Break caught: the production adapter is unbound, renders canonical final
    // HP, overlaps labels, or starts the enemy lane before all party items.
    internal static void RealAdapterProjectsOneRootAtATime()
    {
        using (var f = new Fixture(true))
        using (var sequence = f.Start())
        {
            Check(sequence.Fault.Length == 0, "Real Stage9 adapter rejected settlement: " + sequence.Fault);
            Check(f.Domain.Player.CurrentHp == 93 && f.Domain.Enemy.CurrentHp == 93, "Wrong committed settlement fixture.");
            Check(f.PlayerHp == 90 && f.EnemyHp == 100 && f.Visuals.ActiveNumbers().Length == 1 &&
                f.Visuals.ActiveNumbers()[0].GetComponent<TMP_Text>().text.Contains("×2"),
                "First stacked party item leaked future HP or lost its one ×N feedback.");
            f.Step(sequence, .449);
            Check(f.PlayerHp == 90 && f.EnemyHp == 100 && !sequence.ReadyForDefeatFinalization, "First item lost its minimum hold.");
            f.Step(sequence, .002);
            Check(f.PlayerHp == 93 && f.EnemyHp == 100 && f.Visuals.ActiveNumbers().Length == 1,
                "Second party item overlapped or exposed the enemy lane.");
            f.Step(sequence, .451);
            Check(f.PlayerHp == 93 && f.EnemyHp == 93 && f.Visuals.ActiveNumbers().Length == 1,
                "Enemy lane did not follow the complete party lane.");
            f.Step(sequence, .451);
            Check(sequence.ReadyForDefeatFinalization && f.RuleLease.IsActive && f.Projection.IsActive && f.Completed == 0 &&
                ReferenceEquals(f.Domain.Session.CurrentActor, f.Domain.Player), "Settlement advanced or completed the action before finalization.");
        }
    }

    internal static void EmptyStageHasOneReadableHandoff()
    {
        using (var f = new Fixture(false))
        using (var sequence = f.Start())
        {
            Check(sequence.Fault.Length == 0 && !sequence.ReadyForDefeatFinalization && f.Visuals.ActiveNumbers().Length == 0,
                "Empty Stage9 was skipped or manufactured a number.");
            f.Clock.Now += .31; sequence.Tick();
            Check(!sequence.ReadyForDefeatFinalization, "Elapsed time replaced the empty-stage rendered fence.");
            f.Clock.RenderedFrame++; sequence.Tick();
            Check(sequence.ReadyForDefeatFinalization && f.Completed == 0 && f.RuleLease.IsActive,
                "Empty stage added a second lane wait or completed the Root.");
        }
    }

    internal static void ClosingScopeCancelsSettlementFeedback()
    {
        using (var f = new Fixture(true))
        using (var sequence = f.Start())
        {
            Check(sequence.Fault.Length == 0 && f.Visuals.ActiveNumbers().Length == 1, "No live settlement feedback to close.");
            f.Projection.Dispose();
            Check(f.Visuals.ActiveNumbers().Length == 0 && f.Completed == 0, "Settlement feedback survived its projection owner.");
        }
    }

    internal sealed class Fixture : IDisposable
    {
        internal readonly DRPGBasicAttackProjectionProbe.Fixture Domain;
        internal readonly FixtureClock Clock = new FixtureClock();
        internal readonly BattleRuleMutationLease RuleLease;
        internal readonly DRPGBasicAttackHitOutcomeProbe.Visuals Visuals;
        internal readonly DRPGBattleTransactionPresentationAdapter Adapter;
        internal readonly DRPGBattleActionProjectionLease Projection;
        internal readonly DRPGPreparedBasicAttackRuleAction Prepared;
        internal readonly DRPGBattleSettlementLedger Ledger;
        internal readonly BattleDefeatedActorRoster Defeated;
        internal readonly Transform IncapAnchor;
        internal int Completed;
        internal float PlayerHp => Projection.GetPresented(Domain.Player.RuntimeId).Hp;
        internal float EnemyHp => Projection.GetPresented(Domain.Enemy.RuntimeId).Hp;
        private readonly Texture2D texture;
        private readonly Sprite sprite;
        internal Fixture(bool statuses, bool lethalPlayer = false, bool lethalEnemy = false, bool bindIncap = true, bool fullBattlefield = false,
            bool lastWord = false, Action<DRPGBasicAttackProjectionProbe.Fixture> configureSurface = null, int statusDuration = 2)
        {
            Domain = new DRPGBasicAttackProjectionProbe.Fixture(false, fullBattlefield, beforeStart: statuses ?
                (Action<DRPGBattleSession, BattleParticipantRuntime, BattleParticipantRuntime>)((s, p, e) => SeedStatuses(s, p, e, lethalPlayer, lethalEnemy, statusDuration)) : null);
            try
            {
                texture = new Texture2D(4, 4); var pixels = new Color[16]; Array.Fill(pixels, Color.white); texture.SetPixels(pixels); texture.Apply();
                sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(.5f, 0));
                DRPGBasicAttackPreludeProbe.Set(Domain.Player.Profile, "fullPortrait", sprite);
                DRPGBasicAttackPreludeProbe.Set(Domain.Enemy.Profile, "fullPortrait", sprite);
                Domain.Bridge.FlushQueuedSignals(); Domain.Surface.RenderCombatants(DRPGBattlePresentationSnapshot.Capture(Domain.Session));
                Check(Domain.Session.TransactionArbiter.TryAcquire(Domain.Owner, out RuleLease, out var error), error);
                if (fullBattlefield) DRPGBasicAttackDepartureProbe.SetThreeRows(Domain, RuleLease);
                DRPGBasicAttackDefeatTransferProbe.Fixture.ConfigureWorldOrigins(Domain, RuleLease);
                // Explicit rendered fixture bodies, as in the Stage8 defeat pair.
                // Profile mutation alone does not rebuild a previously resolved build.
                foreach (var view in Domain.Roster.ActiveViews)
                    view.VisualRoot.gameObject.AddComponent<SpriteRenderer>().sprite = sprite;
                var turn = Domain.Session.CurrentActorTurnToken;
                var skill = Domain.Player.Build.BasicAttack;
                DRPGBasicAttackPreludeProbe.Set(skill, "effects", new List<DRPGActionEffectDefinition> {
                    DRPGActionEffectDefinition.Damage(DRPGActionEffectTarget.PrimaryTarget, 1f) });
                var frozen = new DRPGBasicAttackFrozenSnapshot(turn,
                    new TargetSelectionToken(turn.BattleGeneration, turn.TurnSerial, 1, turn.Actor),
                    new ParticipantId(Domain.Enemy.RuntimeId), skill).WithIdentity(Domain.Owner.Action);
                var spec = DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(Domain.Player.RuntimeId, Domain.Enemy.RuntimeId, skill), Domain.Player);
                Check(Domain.Session.StandardBasicAttackRules.TryPrepare(frozen, spec, RuleLease, out Prepared, out error,
                    lastWord ? new[] { LastWordSource.Create(Domain.Player, Domain.Enemy) } : null), error);
                Visuals = new DRPGBasicAttackHitOutcomeProbe.Visuals(Domain.Root, Clock);
                var template = new GameObject("SettlementIncapTemplateProbe", typeof(SpriteRenderer));
                template.transform.SetParent(Domain.Root.transform, false); template.GetComponent<SpriteRenderer>().sprite = sprite; template.SetActive(false);
                IncapAnchor = new GameObject("SettlementIncapAnchorProbe").transform;
                IncapAnchor.SetParent(Domain.Root.transform, false);
                Domain.Pool.TryGet(Domain.Player.RuntimeId, out var playerView); IncapAnchor.position = playerView.transform.position;
                Defeated = Domain.Root.AddComponent<BattleDefeatedActorRoster>();
                Defeated.Configure(bindIncap ? new[] { new BattleIncapacitationBinding(new ParticipantId(Domain.Player.RuntimeId), template, IncapAnchor) }
                    : Array.Empty<BattleIncapacitationBinding>());
                Adapter = new DRPGBattleTransactionPresentationAdapter(Clock, Visuals.Announcement,
                    new BattleAnimationPlaybackRouter(Clock, (_, __) => null),
                    _ => { var h = new BattlePresentationHandle(); h.TrySettle(BattlePresentationOutcome.Completed); return h; },
                    projectionArbiter: Domain.Surface.ProjectionArbiter, impactFeedback: Visuals.Feedback, battleCamera: Visuals.Camera, defeatedRoster: Defeated);
                configureSurface?.Invoke(Domain);
                Projection = (DRPGBattleActionProjectionLease)Adapter.OpenProjection(frozen);
                Await(Adapter.EnterConflict(new ConflictCompositionCommand(frozen, Clock.Now)));
                var committed = Prepared.ResolveAndCommit(DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f), DRPGHitOutcome.Hit);
                Check(committed.Status == DRPGPrimaryImpactCommitStatus.Committed, committed.Error);
                Await(Adapter.PresentImpact(new ImpactPresentationCommand(committed.Receipt.Ledger, Clock.Now)));
                Await(Adapter.Return(new ReturnPresentationCommand(frozen.Identity)));
                Check(Prepared.TryDescribeDepartures(out var departures, out error), error);
                Await(Adapter.CompleteDefeats(departures).Receipt);
                Check(Prepared.TryReconcileFormationAfterReturn(out var formation, out error), error);
                Await(Adapter.ReconcileFormation(formation));
                var homes = new Dictionary<ParticipantId, int>();
                foreach (var participant in Domain.Session.Participants)
                    homes.Add(new ParticipantId(participant.RuntimeId), participant.RuntimeId == 7111 || participant.RuntimeId == 7121 ? 1 :
                        participant.RuntimeId == 7112 || participant.RuntimeId == 7122 ? 2 : 0);
                Check(Prepared.TryCommitSettlement(homes, out Ledger, out error), error);
                Domain.Bridge.FlushQueuedSignals(); Domain.Session.ActionCompleted += _ => Completed++;
            }
            catch { Dispose(); throw; }
        }
        internal DRPGBattleSettlementSequence Start() => new DRPGBattleSettlementSequence(Ledger, Adapter, Clock, RuleLease);
        internal static void SeedStatuses(DRPGBattleSession session, BattleParticipantRuntime player, BattleParticipantRuntime enemy,
            bool lethalPlayer, bool lethalEnemy, int duration = 2)
        {
            var owner = BattleTransactionIdentity.ForTestHarness(new BattleStartIdentity(session.BattleGeneration, 321));
            Check(session.TransactionArbiter.TryAcquire(owner, out var setup, out var error), error);
            using (setup)
            {
                var draft = session.TransactionState.CreateWorkspace();
                AddStatus(draft, player.RuntimeId, "party-poison", lethalPlayer ? -100 : -5, 2, duration);
                AddStatus(draft, player.RuntimeId, "party-regen", 3, 1, duration);
                AddStatus(draft, enemy.RuntimeId, "enemy-poison", lethalEnemy ? -100 : -7, 1, duration);
                Check(session.TransactionState.TryCommitJournal(setup, draft).Succeeded, "Status setup failed.");
            }
        }
        internal void Step(DRPGBattleSettlementSequence sequence, double delta)
        { Clock.Now += delta; Clock.RenderedFrame++; sequence.Tick(); Check(sequence.Fault.Length == 0, sequence.Fault); }
        private void Await(PresentationReceipt receipt)
        {
            for (int i = 0; i < 150 && !receipt.Completion.IsCompleted; i++) { Clock.Now += .03; Clock.RenderedFrame++; Adapter.Tick(); }
            Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed,
                "Stage8 fixture did not close its real barrier: " + (receipt.Completion.IsCompleted ? receipt.Completion.Result.Detail : "pending"));
        }
        private static void AddStatus(DRPGBattleMutationWorkspace draft, int owner, string id, float delta, int stacks, int duration)
        {
            var definition = ScriptableObject.CreateInstance<DRPGStatusDefinitionSO>();
            try
            {
                DRPGBasicAttackPreludeProbe.Set(definition, "statusId", id);
                DRPGBasicAttackPreludeProbe.Set(definition, "displayName", id);
                DRPGBasicAttackPreludeProbe.Set(definition, "triggerTiming", DRPGStatusTriggerTiming.AfterAction);
                DRPGBasicAttackPreludeProbe.Set(definition, "durationTurns", duration);
                DRPGBasicAttackPreludeProbe.Set(definition, "maxStacks", 3);
                DRPGBasicAttackPreludeProbe.Set(definition, "turnStartHpDelta", delta);
                draft.For(new ParticipantId(owner)).ApplyStatus(DRPGResolvedStatusSpec.From(definition), stacks, 0, new ParticipantId(owner));
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
        }
        public void Dispose()
        { Projection?.Dispose(); Prepared?.Dispose(); RuleLease?.Dispose(); Domain.Dispose(); if (sprite != null) UnityEngine.Object.DestroyImmediate(sprite); if (texture != null) UnityEngine.Object.DestroyImmediate(texture); }
    }
    internal sealed class FixtureClock : IDRPGBattlePresentationClock
    {
        private double time; private long frame; private IDRPGBattlePresentationClock live;
        public double Now { get => live != null ? live.Now : time; set => time = value; }
        public long RenderedFrame { get => live != null ? live.RenderedFrame : frame; set => frame = value; }
        internal void UseLiveClock(IDRPGBattlePresentationClock source) => live = source;
    }
    private sealed class LastWordSource : IDRPGImmediateReactionSource
    {
        private readonly DRPGResolvedActionSpec spec;
        private LastWordSource(DRPGResolvedActionSpec spec) { this.spec = spec; Participant = new ParticipantId(spec.ActorRuntimeId); }
        public string SourceId => "settlement-last-word";
        public ParticipantId Participant { get; }
        public int HomeSlot => 0;
        public bool IsPassive => false;
        internal static LastWordSource Create(BattleParticipantRuntime player, BattleParticipantRuntime enemy)
        {
            var skill = ScriptableObject.CreateInstance<DRPGSkillSO>();
            try
            {
                DRPGBasicAttackPreludeProbe.Set(skill, "skillId", "settlement-last-word");
                DRPGBasicAttackPreludeProbe.Set(skill, "power", 1000f);
                DRPGBasicAttackPreludeProbe.Set(skill, "effects", new List<DRPGActionEffectDefinition> {
                    DRPGActionEffectDefinition.Damage(DRPGActionEffectTarget.PrimaryTarget, 1f) });
                return new LastWordSource(DRPGActionSpecResolver.Resolve(DRPGBattleCommand.BasicAttack(player.RuntimeId, enemy.RuntimeId, skill), player));
            }
            finally { UnityEngine.Object.DestroyImmediate(skill); }
        }
        public void Collect(DRPGImmediateReactionContext context, ICollection<DRPGImmediateReactionRequest> requests)
        {
            if (context.Settlement != null && context.DefeatedParticipant == Participant)
                requests.Add(new DRPGImmediateReactionRequest(context.Identity, context.EventId, "last-word",
                    DRPGImmediateReactionCategory.OtherImmediate, 0, spec,
                    DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f), onDefeat: true));
        }
    }
    private static void Check(bool value, string detail) { if (!value) throw new InvalidOperationException(detail); }
}
#endif
