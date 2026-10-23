public interface IDRPGBattleFlowController
{
    bool TryBeginBattle(
        DRPGBattleStartRequest request,
        out DRPGBattleSession session,
        out string error);

    void CompleteBattle(
        DRPGBattleSession session,
        DRPGBattleResult result);
}
