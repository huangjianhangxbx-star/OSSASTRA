public enum DRPGEnemyTargetDisclosureMode
{
    Hidden = 0,
    RevealAtCommit = 1
}

public enum DRPGEnemyPlanState
{
    Windup = 0,
    AwaitingPlayerResponse = 1,
    Executable = 2,
    Executing = 3,
    Scheduled = 4
}

public enum DRPGEnemyPlannedActionKind
{
    Attack = 0,
    Reposition = 1,
    Wait = 2,
    SupportSkill = 3
}
