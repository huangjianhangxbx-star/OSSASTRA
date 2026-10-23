public enum BattleHudVisibilityPolicy
{
    KeepAll = 0,
    HideCommand = 1,
    HideAll = 2
}

public enum BattleStagePresentationPolicy
{
    KeepCurrent = 0,
    UseBattleStage = 1,
    FullScreenCloseUp = 2
}

public enum BattleBackgroundPresentationPolicy
{
    Keep = 0,
    Dim = 1,
    Blur = 2,
    Replace = 3,
    PostProcess = 4
}

public enum BattleOtherTargetPresentationPolicy
{
    Keep = 0,
    Dim = 1,
    Blur = 2,
    Hide = 3
}

public enum BattlePresentationExitPolicy
{
    Immediate = 0,
    Fade = 1,
    WaitForMarker = 2
}
