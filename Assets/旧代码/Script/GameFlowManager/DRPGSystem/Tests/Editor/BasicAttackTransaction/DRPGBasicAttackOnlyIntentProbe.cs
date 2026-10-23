#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

internal static class DRPGBasicAttackOnlyIntentProbe
{
    internal static void InvalidBasicDoesNotFallBackToAnotherSkill()
    {
        using (var f = new Fixture())
        {
            var normal = new DRPGEnemyActionPlanner().SelectNextAction(f.Actor,
                s => s != f.Basic, _ => true, _ => 0);
            Require(normal.Skill == f.Other, "Fixture must exercise the ordinary alternate-skill route.");
            var selected = new DRPGBasicAttackOnlyIntentSource().Select(f.Actor, s => s != f.Basic, _ => true);
            Require(selected.Path == DRPGEnemyActionSelectionPath.None,
                "Acceptance must reject an unaffordable basic instead of selecting another skill.");
            Require(f.Actor.Build.BasicAttack == f.Basic && f.Actor.Build.BattleSkills.Count == 1 && f.Actor.Build.BattleSkills[0] == f.Other,
                "Intent selection must not rewrite the loadout.");
        }
    }
    internal static void OnlyLegalStandardSingleTargetBasicCanBeSelected()
    {
        using (var f = new Fixture())
        {
            var source = new DRPGBasicAttackOnlyIntentSource();
            Require(source.Select(f.Actor, _ => true, _ => false).Path == DRPGEnemyActionSelectionPath.None,
                "Missing legal target must not be bypassed.");
            var selected = source.Select(f.Actor, _ => true, _ => true);
            Require(selected.IsBasicAttack && selected.Skill == f.Basic &&
                selected.Path == DRPGEnemyActionSelectionPath.OrdinaryStandardAttack, "Valid basic must remain the ordinary intent path.");
            Set(f.Basic, "executionMode", DRPGSkillExecutionMode.TelegraphedSpecial);
            Require(source.Select(f.Actor, _ => true, _ => true).Path == DRPGEnemyActionSelectionPath.None,
                "A special skill in the legacy BasicAttack slot is not valid basic-only content.");
        }
    }
    internal static void EnabledOptionsRequireAScenarioIdentity()
    {
        bool rejected = false;
        try { new BasicAttackOnlyAcceptanceOptions(42, " "); }
        catch (ArgumentException) { rejected = true; }
        Require(rejected, "An enabled acceptance request must name its scenario.");
        Require(!default(BasicAttackOnlyAcceptanceOptions).Enabled, "Normal sessions default to ordinary AI.");
    }
    internal static void ConfiguredSessionConsumesOnlyItsRequestOptions()
    {
        using (var f = new Fixture())
        {
            var encounter = ScriptableObject.CreateInstance<DRPGEncounterDefinitionSO>();
            try
            {
                Set(encounter, "encounterId", "acceptance-probe");
                Set(encounter, "enemies", new List<DRPGCombatantProfileSO> { f.Actor.Spec.Profile });
                var player = new BattleParticipantRuntime(1, new BattleParticipantSpec(f.Actor.Spec.Profile,
                    DRPGBattleSide.Player, DRPGBattleRosterRole.Protagonist, 0));
                var roster = DRPGBattleSessionCreationResult.Success(new[] { player, f.Actor });
                var request = new DRPGBattleStartRequest(null, encounter, default, 42,
                    new BasicAttackOnlyAcceptanceOptions(42, "normal-hit"));
                var session = DRPGBattleSessionFactory.CreateConfiguredSession(roster, request,
                    new DRPGBattleTransactionPolicy(true));
                Require(session.AcceptanceOptions.Enabled && session.AcceptanceOptions.BattleSeed == 42 &&
                    session.AcceptanceOptions.ScenarioId == "normal-hit", "Factory dropped this battle's acceptance scope.");
            }
            finally { UnityEngine.Object.DestroyImmediate(encounter); }
        }
    }
    internal static void AcceptanceRejectsLegacyAndMismatchedSeedBeforeOwningActors()
    {
        foreach (bool legacy in new[] { true, false })
        using (var f = new Fixture())
        {
            var encounter = ScriptableObject.CreateInstance<DRPGEncounterDefinitionSO>();
            try
            {
                Set(encounter, "enemies", new List<DRPGCombatantProfileSO> { f.Actor.Spec.Profile });
                var player = new BattleParticipantRuntime(1, new BattleParticipantSpec(f.Actor.Spec.Profile,
                    DRPGBattleSide.Player, DRPGBattleRosterRole.Protagonist, 0));
                var roster = DRPGBattleSessionCreationResult.Success(new[] { player, f.Actor });
                bool rejected = false;
                try { DRPGBattleSessionFactory.CreateConfiguredSession(roster,
                    new DRPGBattleStartRequest(null, encounter, default, legacy ? 42 : 99,
                        new BasicAttackOnlyAcceptanceOptions(42, "normal-hit")), new DRPGBattleTransactionPolicy(!legacy)); }
                catch (ArgumentException) { rejected = true; }
                Require(rejected, "Acceptance must reject " + (legacy ? "Legacy policy" : "mismatched seed") + " before creating a session.");
                // A rejection must leave the exact runtime roster usable by a normal session.
                var normal = DRPGBattleSessionFactory.CreateSession(roster);
                Require(!normal.AcceptanceOptions.Enabled, "Rejected acceptance leaked into the next normal session.");
            }
            finally { UnityEngine.Object.DestroyImmediate(encounter); }
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Set(object target, string name, object value)
    { target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value); }
    private sealed class Fixture : IDisposable
    {
        internal readonly DRPGSkillSO Basic = ScriptableObject.CreateInstance<DRPGSkillSO>();
        internal readonly DRPGSkillSO Other = ScriptableObject.CreateInstance<DRPGSkillSO>();
        private readonly DRPGCombatantProfileSO profile = ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        internal readonly BattleParticipantRuntime Actor;
        internal Fixture()
        {
            Set(Basic, "skillId", "acceptance_basic"); Set(Other, "skillId", "ordinary_other");
            Set(Basic, "targetRule", DRPGSkillTargetRule.SingleEnemy); Set(Other, "targetRule", DRPGSkillTargetRule.SingleEnemy);
            Set(profile, "skills", new List<DRPGSkillSO> { Basic, Other });
            Actor = new BattleParticipantRuntime(7, new BattleParticipantSpec(profile,
                DRPGBattleSide.Enemy, DRPGBattleRosterRole.Enemy, 0));
        }
        public void Dispose()
        { UnityEngine.Object.DestroyImmediate(profile); UnityEngine.Object.DestroyImmediate(Basic); UnityEngine.Object.DestroyImmediate(Other); }
    }
}
#endif
