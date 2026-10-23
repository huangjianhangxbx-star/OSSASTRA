#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGDamageBrokenTerminologyProbe
{
    public static string Run()
    {
        Type stateType = RequireType("DRPGLowHpPresentationState");
        Require(Enum.IsDefined(stateType, "Normal"),
            "Low-HP presentation state must retain Normal.");
        Require(Enum.IsDefined(stateType, "DamageBroken"),
            "Low-HP presentation state must explicitly use DamageBroken.");
        Require(!Enum.IsDefined(stateType, "Broken"),
            "An unqualified Broken value would collide with PostureBreak.");
        Require(Type.GetType("DRPGBattleDamageState, Assembly-CSharp") == null,
            "The ambiguous DRPGBattleDamageState type must leave active code.");

        PropertyInfo lowHpState = RequireProperty(
            typeof(BattleParticipantRuntime),
            "LowHpState");
        PropertyInfo triggeredDamageBroken = RequireProperty(
            typeof(DRPGBattleHitResult),
            "TriggeredDamageBroken");
        Require(typeof(DRPGBattleHitResult).GetProperty("TriggeredBreak") == null,
            "Low-HP hits must not publish the ambiguous TriggeredBreak fact.");

        DRPGSkillSO attack = ScriptableObject.CreateInstance<DRPGSkillSO>();
        DRPGCombatantProfileSO attackerProfile = CreateProfile(attack);
        DRPGCombatantProfileSO teammateProfile = CreateProfile(attack);
        DRPGCombatantProfileSO protagonistProfile = CreateProfile(attack);
        try
        {
            SetField(attack, "power", 60f);
            SetField(teammateProfile, "damageBrokenHpRatio", 0.5f);
            SetField(protagonistProfile, "damageBrokenHpRatio", 0.5f);

            BattleParticipantRuntime attacker = CreateParticipant(
                1,
                attackerProfile,
                DRPGBattleSide.Enemy,
                DRPGBattleRosterRole.Enemy);
            BattleParticipantRuntime teammate = CreateParticipant(
                2,
                teammateProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.PartyMember);
            BattleParticipantRuntime protagonist = CreateParticipant(
                3,
                protagonistProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.Protagonist);
            BattleParticipantRuntime postureOnly = CreateParticipant(
                4,
                teammateProfile,
                DRPGBattleSide.Player,
                DRPGBattleRosterRole.PartyMember);
            var resolver = new DRPGActionResolver();
            BattleParticipantRuntime[] participants =
                { attacker, teammate, protagonist, postureOnly };
            DRPGBattleSessionState state = CreateState(
                51002L,
                participants,
                out BattleRuleMutationLease lease);
            try
            {
            DRPGBattleMutationWorkspace postureWorkspace =
                state.CreateWorkspace();
            postureWorkspace.For(new ParticipantId(postureOnly.RuntimeId))
                .SetPosture(0f);
            // Posture is the first health layer. Exhaust it explicitly before
            // this probe exercises the independent low-HP presentation rule.
            postureWorkspace.For(new ParticipantId(teammate.RuntimeId))
                .SetPosture(0f);
            postureWorkspace.For(new ParticipantId(protagonist.RuntimeId))
                .SetPosture(0f);
            BattleJournalCommitResult postureCommit =
                state.TryCommitJournal(lease, postureWorkspace);
            Require(postureCommit.Succeeded,
                "Posture-only workspace setup failed: " + postureCommit.Error);
            Require(
                string.Equals(
                    "Normal",
                    lowHpState.GetValue(postureOnly).ToString(),
                    StringComparison.Ordinal),
                "PostureBreak must not mutate the low-HP presentation state.");

            DRPGBattleHitResult teammateHit = ResolveAndCommit(
                resolver,
                new DRPGBattleCommand(
                    attacker.RuntimeId,
                    teammate.RuntimeId,
                    attack),
                attacker,
                teammate,
                participants,
                state,
                lease);
            Require((bool)triggeredDamageBroken.GetValue(teammateHit),
                "Crossing the low-HP threshold must trigger DamageBroken once.");
            Require(
                string.Equals(
                    "DamageBroken",
                    lowHpState.GetValue(teammate).ToString(),
                    StringComparison.Ordinal),
                "The low-HP presentation state must persist DamageBroken.");

            SetField(attack, "power", 5f);
            DRPGBattleHitResult repeatedHit = ResolveAndCommit(
                resolver,
                new DRPGBattleCommand(
                    attacker.RuntimeId,
                    teammate.RuntimeId,
                    attack),
                attacker,
                teammate,
                participants,
                state,
                lease);
            Require(!(bool)triggeredDamageBroken.GetValue(repeatedHit),
                "Damage below an already-crossed threshold must not retrigger.");

            SetField(attack, "power", 60f);
            DRPGBattleHitResult protagonistHit = ResolveAndCommit(
                resolver,
                new DRPGBattleCommand(
                    attacker.RuntimeId,
                    protagonist.RuntimeId,
                    attack),
                attacker,
                protagonist,
                participants,
                state,
                lease);
            Require((bool)triggeredDamageBroken.GetValue(protagonistHit),
                "The protagonist still owns the real low-HP state.");

            Type presenterType = RequireType("BattleDamageBrokenCutInPresenter");
            MethodInfo filterEligible = presenterType.GetMethod(
                "FilterEligible",
                BindingFlags.Public | BindingFlags.Static);
            Require(filterEligible != null,
                "DamageBroken cut-in presenter needs one eligibility contract.");
            IList protagonistOnly = (IList)filterEligible.Invoke(
                null,
                new object[] { new[] { protagonistHit } });
            IList mixed = (IList)filterEligible.Invoke(
                null,
                new object[] { new[] { protagonistHit, teammateHit } });
            Require(protagonistOnly.Count == 0,
                "The protagonist must remain excluded from teammate cut-ins.");
            Require(mixed.Count == 1,
                "A DamageBroken teammate must remain eligible for the cut-in.");
            }
            finally
            {
                lease.Dispose();
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(protagonistProfile);
            UnityEngine.Object.DestroyImmediate(teammateProfile);
            UnityEngine.Object.DestroyImmediate(attackerProfile);
            UnityEngine.Object.DestroyImmediate(attack);
        }

        return "PASS explicit DamageBroken terminology and behavior";
    }

    private static Type RequireType(string typeName)
    {
        Type type = Type.GetType(typeName + ", Assembly-CSharp");
        Require(type != null, "Missing required type " + typeName + ".");
        return type;
    }

    private static PropertyInfo RequireProperty(Type type, string name)
    {
        PropertyInfo property = type.GetProperty(name, BindingFlags.Public |
            BindingFlags.Instance);
        Require(property != null,
            "Missing required property " + type.Name + "." + name + ".");
        return property;
    }

    private static BattleParticipantRuntime CreateParticipant(
        int runtimeId,
        DRPGCombatantProfileSO profile,
        DRPGBattleSide side,
        DRPGBattleRosterRole role)
    {
        return new BattleParticipantRuntime(
            runtimeId,
            new BattleParticipantSpec(profile, side, role, 0));
    }

    private static DRPGCombatantProfileSO CreateProfile(DRPGSkillSO skill)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        SetField(profile, "maxHp", 100f);
        SetField(profile, "maxPosture", 100f);
        SetField(profile, "skills", new List<DRPGSkillSO> { skill });
        return profile;
    }

    private static DRPGBattleSessionState CreateState(
        long battleGeneration,
        IReadOnlyList<BattleParticipantRuntime> participants,
        out BattleRuleMutationLease lease)
    {
        var scheduler = new DRPGTurnScheduler();
        var arbiter = new DRPGBattleTransactionArbiter(
            battleGeneration,
            new DRPGBattleActionTraceWriter(
                new DRPGBoundedActionTraceSink(64)));
        var state = new DRPGBattleSessionState(
            battleGeneration,
            participants,
            scheduler,
            arbiter);
        BattleTransactionIdentity owner =
            BattleTransactionIdentity.ForTestHarness(
                new BattleStartIdentity(battleGeneration, 1L));
        if (!arbiter.TryAcquire(owner, out lease, out string error))
            throw new InvalidOperationException(error);
        return state;
    }

    private static DRPGBattleHitResult ResolveAndCommit(
        DRPGActionResolver resolver,
        DRPGBattleCommand command,
        BattleParticipantRuntime actor,
        BattleParticipantRuntime target,
        IReadOnlyList<BattleParticipantRuntime> participants,
        DRPGBattleSessionState state,
        BattleRuleMutationLease lease)
    {
        DRPGBattleMutationWorkspace workspace = state.CreateWorkspace();
        DRPGWorkspaceResolutionResult resolution =
            resolver.ResolveIntoWorkspace(
                DRPGActionSpecResolver.Resolve(command, actor),
                actor,
                target,
                participants,
                workspace,
                DRPGBattleImpactContext.ForStrike(
                    DRPGStrikeGrade.Body,
                    1f));
        Require(resolution.Status ==
                    DRPGWorkspaceResolutionStatus.Succeeded &&
                resolution.Batch != null &&
                resolution.Batch.Hits.Count == 1,
            "DamageBroken resolution must stage one workspace hit: " +
            resolution.Error);
        BattleJournalCommitResult committed =
            state.TryCommitJournal(lease, workspace);
        Require(committed.Succeeded,
            "DamageBroken workspace commit failed: " + committed.Error);
        return resolution.Batch.Hits[0];
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
        field.SetValue(target, value);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
