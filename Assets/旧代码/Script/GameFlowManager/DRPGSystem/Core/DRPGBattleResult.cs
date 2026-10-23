public readonly struct DRPGBattleResult
{
    public DRPGBattleResult(DRPGBattleOutcome outcome, float battleTime)
    {
        Outcome = outcome;
        BattleTime = battleTime;
    }

    public DRPGBattleOutcome Outcome { get; }
    public float BattleTime { get; }
}
