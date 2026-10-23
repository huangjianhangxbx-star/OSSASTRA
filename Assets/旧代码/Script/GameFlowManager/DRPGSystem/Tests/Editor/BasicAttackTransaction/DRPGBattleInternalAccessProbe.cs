#if UNITY_EDITOR
using System;

internal static class DRPGBattleInternalAccessProbe
{
    public static string Run()
    {
        Type internalType = typeof(DRPGActionSpecContributionResolver);
        Type sessionType = typeof(DRPGBattleSession);

        if (internalType.IsPublic || internalType.IsNestedPublic)
        {
            throw new InvalidOperationException(
                "The action-spec contribution resolver must remain internal.");
        }

        if (!ReferenceEquals(internalType.Assembly, sessionType.Assembly))
        {
            throw new InvalidOperationException(
                "The internal resolver and battle session must come from the same runtime assembly.");
        }

        return "PASS DRPG battle internal access boundary";
    }
}
#endif
