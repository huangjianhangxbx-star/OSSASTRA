using UnityEngine;

[CreateAssetMenu(
    fileName = "BattleQuoteCue",
    menuName = "ANKALUTE/DRPG/Presentation Cues/Turn Quote")]
public sealed class BattleQuoteCueSO : BattlePresentationCueSO
{
    [SerializeField] private DRPGBattleQuoteContext context =
        DRPGBattleQuoteContext.TurnBegin;

    public override BattlePresentationCueKind Kind =>
        BattlePresentationCueKind.TurnQuote;
    public DRPGBattleQuoteContext Context => context;
}
