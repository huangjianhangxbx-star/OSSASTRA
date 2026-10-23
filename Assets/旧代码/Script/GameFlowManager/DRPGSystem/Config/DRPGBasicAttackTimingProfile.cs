using UnityEngine;

[CreateAssetMenu(fileName = "DRPGBasicAttackTimingProfile", menuName = "ANKALUTE/DRPG/Basic Attack Timing")]
public sealed class DRPGBasicAttackTimingProfile : ScriptableObject
{
    [SerializeField] private float announcementSeconds = .04f;
    [SerializeField] private float conflictHoldSeconds = .14f;
    [SerializeField] private float windupMinimumSeconds = .10f;
    [SerializeField] private float recoilOutSeconds = .09f;
    [SerializeField] private float recoilReturnSeconds = .16f;
    [SerializeField] private float returnSeconds = .22f;
    [SerializeField] private float settlementItemSeconds = .45f;
    [SerializeField] private float emptySettlementSeconds = 0f;

    internal DRPGBasicAttackTimingSnapshot Freeze() =>
        new DRPGBasicAttackTimingSnapshot(
            FiniteMinimum(announcementSeconds, 0f),
            FiniteMinimum(conflictHoldSeconds, 0f),
            FiniteMinimum(windupMinimumSeconds, 0f),
            FiniteMinimum(recoilOutSeconds, .01f),
            FiniteMinimum(recoilReturnSeconds, .01f),
            FiniteMinimum(returnSeconds, .01f),
            FiniteMinimum(settlementItemSeconds, 0f),
            FiniteMinimum(emptySettlementSeconds, 0f));

    private static float FiniteMinimum(float value, float minimum) =>
        float.IsNaN(value) || float.IsInfinity(value) ? minimum : Mathf.Max(minimum, value);
}
