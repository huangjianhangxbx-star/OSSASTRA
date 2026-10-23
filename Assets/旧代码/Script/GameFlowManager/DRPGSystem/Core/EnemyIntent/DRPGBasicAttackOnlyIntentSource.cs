using System;

public readonly struct BasicAttackOnlyAcceptanceOptions
{
    public BasicAttackOnlyAcceptanceOptions(int battleSeed, string scenarioId)
    {
        if (string.IsNullOrWhiteSpace(scenarioId)) throw new ArgumentException("Acceptance requires a scenario identity.", nameof(scenarioId));
        Enabled = true; BattleSeed = battleSeed; ScenarioId = scenarioId;
    }
    public bool Enabled { get; }
    public int BattleSeed { get; }
    public string ScenarioId { get; }
}

// A per-session intent source, not another action executor or a global AI toggle.
internal sealed class DRPGBasicAttackOnlyIntentSource
{
    internal DRPGEnemyActionSelection Select(BattleParticipantRuntime actor,
        Func<DRPGSkillSO, bool> canAfford, Func<DRPGSkillSO, bool> hasLegalTarget)
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        if (canAfford == null) throw new ArgumentNullException(nameof(canAfford));
        if (hasLegalTarget == null) throw new ArgumentNullException(nameof(hasLegalTarget));
        var basic = actor.Build.BasicAttack;
        if (basic == null || basic.ExecutionMode != DRPGSkillExecutionMode.Standard ||
            basic.TargetRule != DRPGSkillTargetRule.SingleEnemy || !basic.CanUseFrom(actor.FormationRow) ||
            !canAfford(basic) || !hasLegalTarget(basic)) return default;
        return new DRPGEnemyActionSelection(DRPGEnemyActionSelectionPath.OrdinaryStandardAttack, basic, true);
    }
}
