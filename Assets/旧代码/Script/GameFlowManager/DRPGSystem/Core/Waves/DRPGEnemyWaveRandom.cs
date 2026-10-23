using System;
using System.Text;

// Independent stream: neither AI selection nor hit rolls can advance it.
internal sealed class DRPGEnemyWaveRandom
{
    private ulong state;
    public ulong SeedHash { get; }
    public DRPGEnemyWaveRandom(string encounterId, int battleSeed)
    {
        if (string.IsNullOrWhiteSpace(encounterId)) throw new ArgumentException("Encounter identity is required.", nameof(encounterId));
        byte[] prefix = Encoding.UTF8.GetBytes(encounterId.Normalize(NormalizationForm.FormC) + "\0enemy-wave-plan\0");
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            foreach (byte b in prefix) hash = (hash ^ b) * 1099511628211UL;
            uint seed = (uint)battleSeed;
            for (int i = 0; i < 4; i++) hash = (hash ^ (byte)(seed >> (8 * i))) * 1099511628211UL;
            SeedHash = state = hash;
        }
    }
    public ulong NextUInt64()
    {
        unchecked
        {
            ulong z = (state += 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
    public int NextSize(int minimum, int maximum)
    {
        if (minimum <= 0 || maximum < minimum) throw new ArgumentOutOfRangeException(nameof(minimum));
        ulong bound = (ulong)((long)maximum - minimum + 1);
        ulong threshold = unchecked(0UL - bound) % bound;
        ulong value;
        do { value = NextUInt64(); } while (value < threshold);
        return minimum + (int)(value % bound);
    }
}
