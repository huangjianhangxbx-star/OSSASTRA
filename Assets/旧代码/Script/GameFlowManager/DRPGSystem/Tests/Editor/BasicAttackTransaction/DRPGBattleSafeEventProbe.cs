#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

internal static class DRPGBattleSafeEventProbe
{
    internal static void ThrowingObserversCannotInterruptImpactCompletionOrResult()
    {
        foreach (string kind in new[] { "ParticipantChanged", "ActionCompleted", "BattleEnded" })
        using (var f = new Fixture())
        {
            int later = 0;
            if (kind == "ParticipantChanged")
            { f.Session.ParticipantChanged += _ => Throw(); f.Session.ParticipantChanged += _ => later++; }
            if (kind == "ActionCompleted")
            { f.Session.ActionCompleted += _ => Throw(); f.Session.ActionCompleted += _ => later++; }
            if (kind == "BattleEnded")
            { f.Session.BattleEnded += _ => Throw(); f.Session.BattleEnded += _ => later++; }
            Check(f.Session.TryPrepareCommand(DRPGBattleCommand.BasicAttack(1, 2, f.Attack), out var action, out var error), error);
            Check(f.Session.TryResolveImpact(action, out _, out error), error);
            Check(f.Session.TryCompleteAction(action, out error), error);
            Check(later > 0 && f.Session.Outcome == DRPGBattleOutcome.Victory && !f.Session.TransactionArbiter.HasOwner,
                kind + " interrupted committed flow or stranded ownership.");
            Check(!f.Session.TryCompleteAction(action, out _), "Observer failure permitted duplicate completion.");
        }
    }

    internal static void PublicationSnapshotsSubscribersAndDefersReentrantCommands()
    {
        using (var f = new Fixture())
        {
            int original = 0, newlyAdded = 0, completed = 0;
            Action<DRPGBattlePreparedAction> tail = _ => original++;
            f.Session.ActionCompleted += completedAction =>
            {
                completed++;
                f.Session.ActionCompleted -= tail;
                f.Session.ActionCompleted += __ => newlyAdded++;
                Check(!f.Session.TryPrepareCommand(DRPGBattleCommand.BasicAttack(1, 2, f.Attack), out _, out _),
                    "Completion observer reopened a command.");
            };
            f.Session.ActionCompleted += tail;
            Check(f.Session.TryPrepareCommand(DRPGBattleCommand.BasicAttack(1, 2, f.Attack), out var action, out var error), error);
            Check(f.Session.TryResolveImpact(action, out _, out error), error);
            Check(f.Session.TryCompleteAction(action, out error), error);
            Check(completed == 1 && original == 1 && newlyAdded == 0, "Publication did not snapshot its invocation list.");
        }
    }

    internal static void ObserverFailuresHaveBoundedPerSessionDiagnostics()
    {
        using (var f = new Fixture())
        {
            f.Session.BattleEnded += _ => Throw();
            f.Session.Abort();
            var property = typeof(DRPGBattleSession).GetProperty("ObserverDiagnostics", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(property != null, "Session has no observer diagnostic snapshot.");
            var diagnostics = (System.Collections.IEnumerable)property.GetValue(f.Session);
            int count = 0;
            foreach (var diagnostic in diagnostics)
            { count++; Check(diagnostic.ToString().Contains("BattleEnded") && diagnostic.ToString().Contains("observer-sentinel"), "Diagnostic lost event/subscriber error context."); }
            Check(count == 1, "Observer failure was lost or recorded twice.");
        }
    }

    private static void Throw() => throw new InvalidOperationException("observer-sentinel");
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class Fixture : IDisposable
    {
        internal readonly DRPGSkillSO Attack = ScriptableObject.CreateInstance<DRPGSkillSO>();
        private readonly DRPGCombatantProfileSO player = ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        private readonly DRPGCombatantProfileSO enemy = ScriptableObject.CreateInstance<DRPGCombatantProfileSO>();
        internal readonly DRPGBattleSession Session;
        internal Fixture()
        {
            Set(Attack, "power", 10000f); Set(Attack, "skillId", "safe-event-basic");
            Set(player, "agility", 30f); Set(enemy, "agility", 20f);
            Set(player, "skills", new List<DRPGSkillSO> { Attack }); Set(enemy, "skills", new List<DRPGSkillSO> { Attack });
            Session = DRPGBattleHeadlessSessionFactory.Create(new[] {
                new BattleParticipantRuntime(1, new BattleParticipantSpec(player, DRPGBattleSide.Player, DRPGBattleRosterRole.Protagonist, 0)),
                new BattleParticipantRuntime(2, new BattleParticipantSpec(enemy, DRPGBattleSide.Enemy, DRPGBattleRosterRole.Enemy, 0)) });
            Session.Start();
        }
        public void Dispose() { UnityEngine.Object.DestroyImmediate(Attack); UnityEngine.Object.DestroyImmediate(player); UnityEngine.Object.DestroyImmediate(enemy); }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
#endif
