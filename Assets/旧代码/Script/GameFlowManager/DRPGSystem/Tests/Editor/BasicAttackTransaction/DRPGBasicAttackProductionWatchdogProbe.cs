#if UNITY_EDITOR
using System.Collections;

internal static class DRPGBasicAttackProductionWatchdogProbe
{
    internal static IEnumerator ProductionImpactFeedbackTimeoutDoesNotRemutate()
    {
        yield return null;
        DRPGBasicAttackImpactSequenceProbe.ReactionTimeoutContinuesWithoutRemutation();
        yield return null;
    }

    internal static IEnumerator ProductionReactionTimeoutContinuesSerially()
    {
        yield return null;
        DRPGBasicAttackReactionMotionProbe.TimedOutPreparationPreservesTheNextLedgerCursor();
        yield return null;
    }

    internal static IEnumerator ProductionRootSettlementTimeoutStaysSerial()
    {
        yield return null;
        DRPGSettlementSequenceProbe.TimeoutWinsAndStillWaitsForRenderedFrame();
        DRPGSettlementDefeatProbe.FormationTimeoutCannotBecomeDegradedCompletion();
        yield return null;
    }

    internal static IEnumerator ProductionTurnEntryTimeoutStaysSerial()
    {
        yield return null;
        DRPGTurnEntrySequenceProbe.CandidateTimeoutCleansBeforeDegradingAndRejectsLateCallback();
        yield return null;
    }

    internal static IEnumerator ProductionWaveEntryAnnouncementTimeout()
    {
        yield return null;
        DRPGEnemyWavePresentationProbe.AnnouncementTimeoutRetractsWithoutStaging();
        yield return null;
    }

    internal static IEnumerator ProductionWavePartialReadyTimeout()
    {
        yield return null;
        DRPGEnemyWavePresentationProbe.ReadyTimeoutRetractsStagingWithoutDeployment();
        DRPGEnemyWavePresentationProbe.CompletionArrivingAfterDeadlineCannotBeatRecovery();
        yield return null;
    }

    internal static IEnumerator ProductionWaveSceneDisableCleansHandles()
    {
        yield return null;
        DRPGEnemyWavePresentationProbe.CancellationInsideNormalCloseRetractsBeforeAnyCommit();
        DRPGWaveSpritePresentationProbe.CancellationKillsMotionAndNeverCompletesReady();
        yield return null;
    }

    internal static IEnumerator ProductionWaveReceiptIdentityMatrix()
    {
        yield return null;
        DRPGWaveSpritePresentationProbe.ForgedMembersCannotCompleteOrCancelTheRealBatch();
        yield return null;
    }
}
#endif
