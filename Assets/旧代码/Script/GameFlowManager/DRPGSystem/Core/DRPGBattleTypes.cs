public enum DRPGBattleSide
{
    Player = 0,
    Enemy = 1
}

public enum DRPGBattleRosterRole
{
    Protagonist = 0,
    PartyMember = 1,
    Enemy = 2
}

public enum DRPGBattleOutcome
{
    None = 0,
    Victory = 1,
    Defeat = 2,
    Aborted = 3,
    Escaped = 4
}

public enum DRPGBattleCommandKind
{
    BasicAttack = 0,
    Skill = 1,
    Item = 2,
    Defend = 3,
    Escape = 4,
    Wait = 5,
    Reposition = 6,
    SwitchWeapon = 7,
    ResonanceSkill = 8
}

public enum DRPGBattlePhase
{
    Inactive = 0,
    Starting = 1,
    TurnBegin = 2,
    AwaitingCommand = 3,
    TargetSelection = 4,
    PreparingAction = 5,
    ExecutingAction = 6,
    ActionEnd = 7,
    TurnEnd = 8,
    Result = 9
    ,
    InteractiveAction = 10
}
