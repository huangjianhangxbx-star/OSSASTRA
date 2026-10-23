public static class DRPGAttackPresentationResolver
{
    public static string ResolveAttackPresentationKey(
        DRPGResolvedCombatantBuild actor,
        DRPGSkillSO selectedSkill)
    {
        if (actor != null &&
            !string.IsNullOrWhiteSpace(actor.AttackPresentationKey))
        {
            return actor.AttackPresentationKey;
        }
        if (selectedSkill != null)
            return selectedSkill.PresentationKey;
        if (actor != null && actor.BasicAttack != null)
            return actor.BasicAttack.PresentationKey;
        return string.Empty;
    }
}
