using UnityEngine;
/// <summary>
/// 当前房间或区域提供的背景等环境信息
/// </summary>
public readonly struct DRPGBattleEnvironmentContext
{
    public DRPGBattleEnvironmentContext(
        string regionId,
        Sprite regionDefaultBattleBackground)
    {
        RegionId = regionId;
        RegionDefaultBattleBackground = regionDefaultBattleBackground;
    }

    public string RegionId { get; }
    public Sprite RegionDefaultBattleBackground { get; }
}
