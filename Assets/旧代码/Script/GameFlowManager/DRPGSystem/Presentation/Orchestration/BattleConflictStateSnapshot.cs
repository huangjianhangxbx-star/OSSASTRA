public sealed class BattleConflictStateSnapshot
{
    public BattleConflictStateSnapshot(
        BattleHudStateSnapshot hud,
        BattleCameraStateSnapshot camera,
        BattleStageStateSnapshot stage)
    {
        Hud = hud;
        Camera = camera;
        Stage = stage;
    }

    public BattleHudStateSnapshot Hud { get; }
    public BattleCameraStateSnapshot Camera { get; }
    public BattleStageStateSnapshot Stage { get; }
}
