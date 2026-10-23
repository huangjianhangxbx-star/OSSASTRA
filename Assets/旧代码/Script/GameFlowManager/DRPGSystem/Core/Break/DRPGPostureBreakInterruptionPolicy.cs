public enum DRPGPostureBreakInterruptionPolicy
{
    InterruptBeforeExecution = 0,
    Uninterruptible = 1
}

public static class DRPGPostureBreakInterruptionRules
{
    public static bool CanInterrupt(
        DRPGPostureBreakInterruptionPolicy policy,
        DRPGEnemyPlanState planState)
    {
        if (policy !=
            DRPGPostureBreakInterruptionPolicy.InterruptBeforeExecution)
        {
            return false;
        }

        return planState == DRPGEnemyPlanState.Windup ||
            planState == DRPGEnemyPlanState.AwaitingPlayerResponse;
    }
}
