#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

public static class DRPGInPlaceContinuityProbe
{
    public static string Run()
    {
        DRPGCombatantProfileSO hero = CreateProfile("continuity-hero");
        DRPGCombatantProfileSO enemy = CreateProfile("continuity-enemy");
        try
        {
            var firstHero = new BattleParticipantRuntime(1,
                new BattleParticipantSpec(hero, DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist, 0,
                    initialHitPoints: 37f));
            var firstEnemy = new BattleParticipantRuntime(2,
                new BattleParticipantSpec(enemy, DRPGBattleSide.Enemy,
                    DRPGBattleRosterRole.Enemy, 0));
            var session = new DRPGBattleSession(new[] { firstHero, firstEnemy });
            var state = new DRPGNodePartyBattleState();
            state.Capture(session);
            state.Capture(session);
            if (state.Revision != 1 ||
                !state.TryGetHitPoints(hero.CombatantId, out float saved) ||
                Math.Abs(saved - 37f) > 0.001f)
            {
                throw new InvalidOperationException(
                    "Node HP capture must be committed and idempotent.");
            }

            var secondHero = new BattleParticipantRuntime(3,
                new BattleParticipantSpec(hero, DRPGBattleSide.Player,
                    DRPGBattleRosterRole.Protagonist, 0,
                    initialHitPoints: saved));
            if (Math.Abs(secondHero.CurrentHp - 37f) > 0.001f)
                throw new InvalidOperationException(
                    "A later battle must start from node-local HP, not full HP.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(hero);
            UnityEngine.Object.DestroyImmediate(enemy);
        }
        return "PASS node-local committed HP continuity";
    }

    private static DRPGCombatantProfileSO CreateProfile(string id)
    {
        DRPGCombatantProfileSO profile =
            ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        typeof(DRPGCombatantProfileSO)
            .GetField("combatantId", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(profile, id);
        return profile;
    }
}
#endif
