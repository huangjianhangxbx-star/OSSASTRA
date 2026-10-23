public interface IDRPGBattlePresentation
{
    void Show(DRPGBattleSession session, DRPGBattleStartRequest request);
    void Hide();
}
