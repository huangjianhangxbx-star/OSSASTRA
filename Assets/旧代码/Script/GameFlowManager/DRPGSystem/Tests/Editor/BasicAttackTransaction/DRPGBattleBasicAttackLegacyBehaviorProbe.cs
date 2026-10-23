#if UNITY_EDITOR
using System;
using System.Collections;

public sealed class DRPGBattleLegacyInvariantException :
    InvalidOperationException
{
    public DRPGBattleLegacyInvariantException(string caseId, string message)
        : base(message)
    {
        if (string.IsNullOrEmpty(caseId))
            throw new ArgumentException("A case id is required.", nameof(caseId));

        CaseId = caseId;
    }

    public string CaseId { get; }
}

public static class DRPGBattleBasicAttackLegacyBehaviorProbe
{
    public static readonly string[] CaseIds =
    {
        "LethalTargetRemainsLeasedUntilReturn",
        "RecoilIsPureHorizontalAndRebindSafe",
        "PlayerAndEnemyImpactFeedbackAreSymmetric",
        "AttackerRestoresWithoutVisualRootSnap",
        "PostActionSettlementIsSerialAndBlocksContinuation"
    };

    public static IEnumerator RunCoroutine(string caseId)
    {
        if (string.IsNullOrEmpty(caseId))
            throw new ArgumentException("A legacy RED case id is required.", nameof(caseId));

        if (string.Equals(caseId, CaseIds[1], StringComparison.Ordinal) ||
            string.Equals(caseId, CaseIds[2], StringComparison.Ordinal) ||
            string.Equals(caseId, CaseIds[3], StringComparison.Ordinal) ||
            string.Equals(caseId, CaseIds[4], StringComparison.Ordinal))
        {
            IEnumerator coroutine =
                DRPGBattleBasicAttackProductionPresentationProbe
                    .RunCoroutine(caseId);
            while (coroutine.MoveNext())
                yield return coroutine.Current;
            yield break;
        }

        if (string.Equals(caseId, CaseIds[0], StringComparison.Ordinal))
            DRPGBattleBasicAttackProductionPresentationProbe
                .LethalTargetRemainsLeasedUntilReturn();
        else
            throw new ArgumentOutOfRangeException(
                nameof(caseId), caseId, "Unknown legacy RED case.");
    }
}
#endif
