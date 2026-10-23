using System;
using System.Collections.Generic;

public sealed class DRPGResolvedIdentityContribution
{
    public static DRPGResolvedIdentityContribution Empty { get; } =
        new DRPGResolvedIdentityContribution(
            Array.Empty<DRPGPassiveModifierSO>(),
            Array.Empty<DRPGSkillSO>());

    public DRPGResolvedIdentityContribution(
        IReadOnlyList<DRPGPassiveModifierSO> passiveModifiers,
        IReadOnlyList<DRPGSkillSO> battleSkills)
    {
        PassiveModifiers = Copy(passiveModifiers);
        BattleSkills = Copy(battleSkills);
    }

    public IReadOnlyList<DRPGPassiveModifierSO> PassiveModifiers { get; }
    public IReadOnlyList<DRPGSkillSO> BattleSkills { get; }

    private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source)
        where T : class
    {
        var result = new List<T>();
        if (source != null)
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null)
                    result.Add(source[i]);
            }
        }
        return result.AsReadOnly();
    }
}
