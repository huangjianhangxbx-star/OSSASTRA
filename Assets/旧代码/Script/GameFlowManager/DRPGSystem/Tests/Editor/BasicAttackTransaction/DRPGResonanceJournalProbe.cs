#if UNITY_EDITOR
using System;

internal static partial class DRPGActionResourceJournalProbe
{
    internal static void RunResonanceJournalChecks()
    {
        ResonanceJournalCommitsWithBattleResources();
        ResonanceJournalFailureAndAbaPreserveLiveState();
        ResonanceJournalForkDedupeAndConsume();
        ResonanceJournalCrossBattleProgressSurvives();
    }

    internal static void ResonanceJournalCommitsWithBattleResources()
    {
        var expedition = new DRPGResonanceExpeditionState();
        expedition.AddProgress("workspace_player", "resonance", 2);
        using (var test = new Fixture(resonance: expedition))
        {
            var workspace = test.Stage();
            Check(workspace.ActionResources.TryStageResonance(test.Identity, "root-hit", "workspace_player", "resonance", 2, 3, false, out var error), error);
            Check(expedition.GetProgress("workspace_player", "resonance") == 2 && test.ProgressValue == 10f && test.Weapon.CurrentDurability == 100,
                "Building resonance mutated expedition or battle resources.");
            var committed = test.Battle.State.TryCommitJournal(test.Lease, workspace);
            Check(committed.Succeeded && expedition.GetProgress("workspace_player", "resonance") == 3 && expedition.Revision == 2 &&
                test.ProgressValue == 7f && test.Weapon.CurrentDurability == 99 && test.Battle.Enemy.CurrentHp == 90f &&
                test.Battle.Player.CurrentMp == 25f && test.Battle.State.BattleRevision == 1, "Resonance did not commit atomically: " + committed.Error);
            Check(!test.Battle.State.TryCommitJournal(test.Lease, workspace).Succeeded && expedition.Revision == 2,
                "A stale workspace charged expedition progress again.");
        }
    }

    internal static void ResonanceJournalFailureAndAbaPreserveLiveState()
    {
        for (int scenario = 0; scenario < 4; scenario++)
        {
            var expedition = new DRPGResonanceExpeditionState(); expedition.AddProgress("workspace_player", "resonance", 2);
            using (var test = new Fixture(resonance: expedition))
            {
                var workspace = test.Stage();
                Check(workspace.ActionResources.TryStageResonance(test.Identity, "root-hit", "workspace_player", "resonance", 1, 5, false, out var error), error);
                if (scenario == 0) workspace.For(new ParticipantId(test.Battle.Enemy.RuntimeId)).SetHitPoints(float.NaN);
                if (scenario == 1) expedition.AddProgress("workspace_player", "resonance", 1);
                if (scenario == 2) { expedition.ClearAll(); expedition.AddProgress("workspace_player", "resonance", 2); }
                if (scenario == 3) test.Weapon.RestoreDurability(100, 98);
                var commit = test.Battle.State.TryCommitJournal(test.Lease, workspace);
                Check(!commit.Succeeded && expedition.GetProgress("workspace_player", "resonance") == (scenario == 1 ? 3 : 2) &&
                    test.ProgressValue == 10f && test.Battle.Enemy.CurrentHp == 120f && test.Battle.Player.CurrentMp == 30f &&
                    test.Weapon.CurrentDurability == (scenario == 3 ? 98 : 100) && test.Battle.State.BattleRevision == 0,
                    "Resonance drift, ABA or late validation failure leaked a partial commit: " + scenario);
            }
        }
    }

    internal static void ResonanceJournalForkDedupeAndConsume()
    {
        var expedition = new DRPGResonanceExpeditionState(); expedition.AddProgress("workspace_player", "resonance", 2);
        using (var test = new Fixture(resonance: expedition))
        {
            var workspace = test.Stage();
            Check(workspace.ActionResources.TryStageResonance(test.Identity, "gain", "workspace_player", "resonance", 1, 3, false, out var error), error);
            var fork = workspace.Fork();
            Check(fork.ActionResources.TryStageResonance(test.Identity, "gain", "workspace_player", "resonance", 1, 3, false, out error), error);
            Check(!fork.ActionResources.TryStageResonance(test.Identity, "gain", "workspace_player", "resonance", 2, 3, false, out _), "Conflicting resonance event was accepted.");
            Check(!fork.ActionResources.TryStageResonance(test.Identity, "foreign", "outside", "resonance", 1, 3, false, out _), "Foreign resonance owner accepted.");
            Check(!fork.ActionResources.TryStageResonance(test.Identity, "too-early", "workspace_player", "resonance", 0, 4, true, out _), "Uncharged resonance was consumed.");
            Check(fork.ActionResources.TryStageResonance(test.Identity, "consume", "workspace_player", "resonance", 0, 3, true, out error), error);
            Check(test.Battle.State.TryCommitJournal(test.Lease, fork).Succeeded && expedition.GetProgress("workspace_player", "resonance") == 0,
                "Resonance gain/consume chain could not commit from a fork.");
        }
    }

    internal static void ResonanceJournalCrossBattleProgressSurvives()
    {
        var expedition = new DRPGResonanceExpeditionState(); expedition.AddProgress("workspace_player", "resonance", 2);
        using (var first = new Fixture(resonance: expedition))
        using (var second = new Fixture(resonance: expedition))
        {
            var stale = second.Stage();
            Check(stale.ActionResources.TryStageResonance(second.Identity, "second", "workspace_player", "resonance", 1, 8, false, out var error), error);
            var current = first.Stage();
            Check(current.ActionResources.TryStageResonance(first.Identity, "first", "workspace_player", "resonance", 2, 8, false, out error), error);
            Check(first.Battle.State.TryCommitJournal(first.Lease, current).Succeeded && expedition.GetProgress("workspace_player", "resonance") == 4,
                "A new battle reset or could not advance expedition progress.");
            Check(!second.Battle.State.TryCommitJournal(second.Lease, stale).Succeeded && second.ProgressValue == 10f,
                "Another battle's stale expedition snapshot overwrote current progress.");
            var fresh = second.Stage();
            Check(fresh.ActionResources.TryStageResonance(second.Identity, "second", "workspace_player", "resonance", 1, 8, false, out error), error);
            Check(second.Battle.State.TryCommitJournal(second.Lease, fresh).Succeeded && expedition.GetProgress("workspace_player", "resonance") == 5,
                "Fresh next-battle workspace did not preserve and advance expedition progress.");
        }
    }
}
#endif
