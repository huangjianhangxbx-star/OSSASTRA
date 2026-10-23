public enum GameState
{
    gameStarted,
    playingLevel,
    engagingEnemies,
    bossStage,
    engagingBoss,
    levelCompleted,
    gameWon,
    gameLost,
    gamePaused,
    dungeonOverviewMap,
    restartGame
}

/// <summary>
/// 攻击效果携带的攻击类型。由攻击方（DamageEffectSO / GunSystemController）写入，
/// 由受击方（EnemyReactionController）读取并决定反应。
/// </summary>
public enum CombatImpactType
{
    Unknown = 0,
    Gunshot = 10,
    Kick = 20,
    MeleeWeapon = 30,
    Explosion = 40,
    Skill = 50
}

//public enum PlayerDirection
//{
//    PlayerLeft,
//    PlayerRight
//}



//public enum AimDirection
//{

//    Aim_Middle,
//    Aim_Up,
//    Aim_Down


//}

public enum HoldGunState
{
    HoldMachineGun,
    HoldPistal



}