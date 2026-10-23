using System;

/// <summary>
/// Applies the Battle V2 two-layer health rule to the authoritative mutation
/// workspace. Posture is the first, recoverable layer; only overflow reaches HP.
/// </summary>
internal static class BattleLayeredHealthMutation
{
    internal static BattleDamageResult Apply(
        ParticipantMutationBuilder target,
        int panel,
        int resistance)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));

        int hp = BattleIntegerMath.RoundNonnegative(target.HitPoints);
        long posture = checked((long)Math.Round(
            target.Posture,
            MidpointRounding.AwayFromZero));
        BattleDamageResult result = BattleDamageMath.Resolve(
            panel,
            resistance,
            posture,
            hp);
        // No posture outcome is emitted when the layer was already empty.
        // Do not add a phantom posture mutation to the committed journal.
        if (target.Posture != (float)result.PostureAfter)
            target.SetPosture((float)result.PostureAfter);
        target.SetHitPoints(result.HpAfter);
        return result;
    }

    internal static long RestorePosture(
        ParticipantMutationBuilder target,
        int amount)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));
        long posture = checked((long)Math.Round(
            target.Posture,
            MidpointRounding.AwayFromZero));
        long restored = BattleDamageMath.RestorePosture(posture, amount);
        target.SetPosture((float)restored);
        return restored;
    }
}
