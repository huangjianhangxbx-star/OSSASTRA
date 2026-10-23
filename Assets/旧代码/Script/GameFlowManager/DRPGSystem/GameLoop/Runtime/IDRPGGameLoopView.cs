public interface IDRPGGameLoopView
{
    void Configure(DRPGGameLoopRuntimeRoot owner);
    void ShowTitle();
    void ShowStrategicHub(StrategicHubSnapshot snapshot);
    void HideAll();
    void ShowSettlement(
        DRPGVictorySettlement settlement,
        DRPGPartyInventoryState inventory);
    void RefreshSettlement(
        DRPGVictorySettlement settlement,
        DRPGPartyInventoryState inventory,
        string feedback);
}
