using UnityEngine;

public static class DRPGBattleBackgroundResolver
{
    public static Sprite Resolve(
        DRPGEncounterDefinitionSO encounter,
        DRPGBattleEnvironmentContext environmentContext)
    {
        if (encounter != null && encounter.BattleBackground != null)
            return encounter.BattleBackground;

        if (environmentContext.RegionDefaultBattleBackground != null)
            return environmentContext.RegionDefaultBattleBackground;

        if (encounter != null &&
            encounter.Presentation != null &&
            encounter.Presentation.FallbackBattleBackground != null)
        {
            return encounter.Presentation.FallbackBattleBackground;
        }

        return null;
    }
}
