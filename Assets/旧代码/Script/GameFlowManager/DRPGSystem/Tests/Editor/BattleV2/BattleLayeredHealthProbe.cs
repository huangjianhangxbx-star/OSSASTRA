#if UNITY_EDITOR
using System;

public static class BattleLayeredHealthProbe
{
    public static void RunAll()
    {
        WorkspaceCommitUsesPostureAsFirstHealthLayer();
        PostureRestorationIsNotClampedToTheBattleDefault();
        ZeroPostureHitCommitsForBothSides();
    }

    private static void ZeroPostureHitCommitsForBothSides()
    {
        for (int side = 0; side < 2; side++)
        using (var fixture = DRPGBattleStateMutationFixture.Create())
        {
            var actor = side == 0 ? fixture.Player : fixture.Enemy;
            var target = side == 0 ? fixture.Enemy : fixture.Player;
            var setup = fixture.CreateWorkspace();
            setup.For(new ParticipantId(target.RuntimeId)).SetPosture(0f);
            Check(fixture.State.TryCommitJournal(fixture.Lease, setup).Succeeded,
                "zero posture setup failed");
            var skill = UnityEngine.ScriptableObject.CreateInstance<DRPGSkillSO>();
            try
            {
                var flags = System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic;
                typeof(DRPGSkillSO).GetField("skillId", flags).SetValue(skill, "zero-posture-hit");
                typeof(DRPGSkillSO).GetField("power", flags).SetValue(skill, 20f);
                typeof(DRPGSkillSO).GetField("effects", flags).SetValue(skill,
                    new System.Collections.Generic.List<DRPGActionEffectDefinition> {
                        DRPGActionEffectDefinition.Damage(DRPGActionEffectTarget.PrimaryTarget,
                            1f, DRPGDamageTag.Slash) });
                var identity = new ActionIdentity(fixture.BattleGeneration, 1, 1);
                fixture.Lease.Dispose();
                Check(fixture.Arbiter.TryAcquire(BattleTransactionIdentity.ForRootAction(identity),
                    out var lease, out string error), error);
                using (lease)
                {
                    var actorId = new ParticipantId(actor.RuntimeId);
                    var frozen = new DRPGBasicAttackFrozenSnapshot(
                        new ActorTurnToken(fixture.BattleGeneration, 1, actorId),
                        new TargetSelectionToken(fixture.BattleGeneration, 1, 1, actorId),
                        new ParticipantId(target.RuntimeId), skill).WithIdentity(identity);
                    var workspace = fixture.CreateWorkspace();
                    var resolver = new DRPGActionResolver();
                    var participants = new[] { actor, target };
                    var primary = resolver.ResolveIntoWorkspace(DRPGActionSpecResolver.Resolve(
                        DRPGBattleCommand.BasicAttack(actor.RuntimeId, target.RuntimeId, skill), actor),
                        actor, target, participants, workspace,
                        DRPGBattleImpactContext.ForStrike(DRPGStrikeGrade.Body, 1f));
                    var closure = new DRPGImmediateReactionLedgerBuilder().Build(identity,
                        "zero-posture-primary", primary, workspace, participants,
                        Array.Empty<IDRPGImmediateReactionSource>(), resolver);
                    float before = target.CurrentHp;
                    var store = new DRPGBattleActionResolutionLedgerStore(fixture.State, fixture.Arbiter.Trace);
                    var result = store.TryCommitActionResolution("zero-posture-primary", frozen,
                        primary, closure, lease);
                    Check(result.Status == DRPGPrimaryImpactCommitStatus.Committed,
                        "zero-posture hit rejected for " + target.Side + ": " + result.Error);
                    Check(target.CurrentHp < before && target.CurrentPosture == 0f,
                        "unshielded hit did not reduce HP");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(skill); }
        }
    }

    private static void WorkspaceCommitUsesPostureAsFirstHealthLayer()
    {
        using (var fixture = DRPGBattleStateMutationFixture.Create())
        {
            var setup = fixture.CreateWorkspace();
            var target = setup.For(new ParticipantId(fixture.Enemy.RuntimeId));
            target.SetHitPoints(10f).SetPosture(5f);
            Check(fixture.State.TryCommitJournal(fixture.Lease, setup).Succeeded,
                "layered-health setup did not commit");

            var impact = fixture.CreateWorkspace();
            var mutation = impact.For(new ParticipantId(fixture.Enemy.RuntimeId));
            BattleDamageResult result = BattleLayeredHealthMutation.Apply(
                mutation, 12, 0);
            Check(fixture.State.TryCommitJournal(fixture.Lease, impact).Succeeded,
                "layered-health impact did not commit");
            Check(result.PostureAfter == 0 && result.HpAfter == 3,
                "workspace result did not overflow posture damage into HP");
            Check(fixture.Enemy.CurrentPosture == 0f && fixture.Enemy.CurrentHp == 3f,
                "authoritative participant did not receive the layered result");
        }
    }

    private static void PostureRestorationIsNotClampedToTheBattleDefault()
    {
        using (var fixture = DRPGBattleStateMutationFixture.Create())
        {
            var draft = fixture.CreateWorkspace();
            var target = draft.For(new ParticipantId(fixture.Enemy.RuntimeId));
            target.SetPosture(5f);
            BattleLayeredHealthMutation.RestorePosture(target, 100);
            Check(fixture.State.TryCommitJournal(fixture.Lease, draft).Succeeded,
                "unbounded posture restoration did not commit");
            Check(fixture.Enemy.CurrentPosture == 105f,
                "default posture was incorrectly treated as a gameplay cap");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
