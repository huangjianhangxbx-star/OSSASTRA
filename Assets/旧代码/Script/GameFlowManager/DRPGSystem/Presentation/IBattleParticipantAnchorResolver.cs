using System;
using UnityEngine;
using UnityEngine.UI;

public enum BattleImpactAnchorKind
{
    PartyHud = 0,
    EnemyField = 1
}

public sealed class BattleImpactAnchor
{
    private readonly Action<DRPGBattlePortraitState> setPortraitState;

    public BattleImpactAnchor(
        int runtimeId,
        BattleImpactAnchorKind kind,
        RectTransform rectTransform,
        Graphic portraitGraphic,
        Action<DRPGBattlePortraitState> setPortraitState)
    {
        RuntimeId = runtimeId;
        Kind = kind;
        RectTransform = rectTransform;
        PortraitGraphic = portraitGraphic;
        this.setPortraitState = setPortraitState;
    }

    public int RuntimeId { get; }
    public BattleImpactAnchorKind Kind { get; }
    public RectTransform RectTransform { get; }
    public Graphic PortraitGraphic { get; }

    public void SetPortraitState(DRPGBattlePortraitState state)
    {
        setPortraitState?.Invoke(state);
    }
}

public interface IBattleParticipantAnchorResolver
{
    bool TryResolve(int runtimeId, out BattleImpactAnchor anchor);
}
