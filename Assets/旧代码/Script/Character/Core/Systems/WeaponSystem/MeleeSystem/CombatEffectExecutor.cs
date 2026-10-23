//检测 Collider2D
//→ 归一化目标
//→ 构造 CombatEffectContext
//→ CombatEffectExecutor.ApplyEffects()



public static class CombatEffectExecutor
{
    public static void ApplyEffects( MeleeHitProfileSO profile,in CombatEffectContext context)
    {
        if (profile == null || profile.effects == null)
        {
            return;
        }

        for (int i = 0;i < profile.effects.Count;i++)
        {
            CombatEffectSO effect = profile.effects[i];

            if (effect == null)
                continue;

            if (!effect.CanApply(context))
                continue;

            effect.Apply(context);
        }
    }
}