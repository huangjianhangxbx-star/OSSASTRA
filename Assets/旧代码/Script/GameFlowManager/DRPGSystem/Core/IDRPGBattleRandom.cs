using System;

public interface IDRPGBattleRandom
{
    int Next(int exclusiveMax);
}

public sealed class SystemDRPGBattleRandom : IDRPGBattleRandom
{
    private readonly Random random;

    public SystemDRPGBattleRandom()
        : this(new Random())
    {
    }

    internal SystemDRPGBattleRandom(Random random)
    {
        this.random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public int Next(int exclusiveMax)
    {
        if (exclusiveMax <= 0)
            throw new ArgumentOutOfRangeException(nameof(exclusiveMax));

        return random.Next(exclusiveMax);
    }
}
