public enum DRPGSkillTreeRewardKind
{
    Skill = 0,
    Passive = 1
}

public enum DRPGProgressionError
{
    None = 0,
    MissingProfile = 1,
    MissingDefinition = 2,
    MissingProfession = 3,
    ProfessionUnavailable = 4,
    MissingTree = 5,
    NodeNotFound = 6,
    NodeAlreadyUnlocked = 7,
    PrerequisiteLocked = 8,
    InsufficientPoints = 9,
    InvalidReward = 10,
    InvalidSlot = 11,
    SkillLocked = 12,
    SkillAlreadyEquipped = 13,
    InvalidAttribute = 14,
    StaleDraft = 15,
    LevelCapReached = 16,
    InvalidSaveData = 17,
    BreakthroughUnavailable = 18,
    BreakthroughAlreadyUnlocked = 19,
    ProfessionLocked = 20,
    ProfessionAlreadyUnlocked = 21
}

public enum DRPGSkillPurpose
{
    Damage = 0,
    Healing = 1,
    Defense = 2,
    SpecialAttack = 3,
    SpecialDefense = 4,
    Utility = 5
}

public enum DRPGSkillTargetRule
{
    SingleEnemy = 0,
    SingleAlly = 1,
    Self = 2,
    AllEnemies = 3,
    AllAllies = 4,
    BattlefieldPoint = 5,
    None = 6
}

public enum DRPGSkillCommandExpansionMode
{
    None = 0,
    ReplaceSkillSlotsUntilResolved = 1,
    ReplaceSkillSlotsUntilCancelled = 2
}
