using System;

public enum DRPGGameLoopPhase
{
    Title = 0,
    Exploration = 1,
    Battle = 2,
    VictorySettlement = 3,
    StrategicHub = 4
}

/// <summary>
/// Pure transition owner for the first playable application loop. It has no
/// Unity or UI dependency, so invalid cross-mode transitions stay testable.
/// </summary>
public sealed class DRPGGameLoopState
{
    public DRPGGameLoopPhase Phase { get; private set; } =
        DRPGGameLoopPhase.Title;
    public DRPGVictorySettlement ActiveSettlement { get; private set; }

    public event Action<DRPGGameLoopPhase> PhaseChanged;

    public bool TryStartExploration()
    {
        if (Phase != DRPGGameLoopPhase.Title)
            return false;
        return SetPhase(DRPGGameLoopPhase.Exploration);
    }

    public bool TryBeginBattle()
    {
        if (Phase != DRPGGameLoopPhase.Exploration)
            return false;
        return SetPhase(DRPGGameLoopPhase.Battle);
    }

    public bool TryResolveBattle(
        DRPGBattleResult result,
        DRPGVictorySettlement settlement)
    {
        if (Phase != DRPGGameLoopPhase.Battle)
            return false;

        switch (result.Outcome)
        {
            case DRPGBattleOutcome.Victory:
                if (settlement == null)
                    return false;
                ActiveSettlement = settlement;
                return SetPhase(DRPGGameLoopPhase.VictorySettlement);

            case DRPGBattleOutcome.Defeat:
                ActiveSettlement = null;
                return SetPhase(DRPGGameLoopPhase.Title);

            case DRPGBattleOutcome.Escaped:
            case DRPGBattleOutcome.Aborted:
                ActiveSettlement = null;
                return SetPhase(DRPGGameLoopPhase.Exploration);

            default:
                return false;
        }
    }

    public bool TryCompleteSettlement()
    {
        if (Phase != DRPGGameLoopPhase.VictorySettlement)
            return false;
        ActiveSettlement = null;
        return SetPhase(DRPGGameLoopPhase.Exploration);
    }

    public void ForceTitle()
    {
        ActiveSettlement = null;
        SetPhase(DRPGGameLoopPhase.Title);
    }

    private bool SetPhase(DRPGGameLoopPhase phase)
    {
        if (Phase == phase)
            return false;
        Phase = phase;
        PhaseChanged?.Invoke(phase);
        return true;
    }
}
