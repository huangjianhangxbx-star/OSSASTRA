using System;
using System.Collections.Generic;
using System.Text;

internal readonly struct EnemyDeploymentId : IEquatable<EnemyDeploymentId>
{
    public string EncounterId { get; }
    public int AuthoredOrdinal { get; }
    public EnemyDeploymentId(string encounterId, int authoredOrdinal)
    {
        if (string.IsNullOrWhiteSpace(encounterId)) throw new ArgumentException("Encounter identity is required.");
        if (authoredOrdinal < 0) throw new ArgumentOutOfRangeException(nameof(authoredOrdinal));
        EncounterId = encounterId.Normalize(NormalizationForm.FormC);
        AuthoredOrdinal = authoredOrdinal;
    }
    public bool Equals(EnemyDeploymentId other) => AuthoredOrdinal == other.AuthoredOrdinal &&
        string.Equals(EncounterId, other.EncounterId, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is EnemyDeploymentId other && Equals(other);
    // Hash is for dictionary lookup only; never a random seed or persisted fingerprint.
    public override int GetHashCode() => unchecked((EncounterId == null ? 0 : StringComparer.Ordinal.GetHashCode(EncounterId)) * 397 ^ AuthoredOrdinal);
}

internal readonly struct DRPGEnemyWaveMember
{
    public EnemyDeploymentId Id { get; }
    public BattleParticipantSpec Spec { get; }
    internal DRPGEnemyWaveMember(EnemyDeploymentId id, BattleParticipantSpec spec) { Id = id; Spec = spec; }
}

internal sealed class DRPGEnemyWavePlan
{
    public IReadOnlyList<IReadOnlyList<DRPGEnemyWaveMember>> Waves { get; }
    public string EncounterId { get; }
    public int BattleSeed { get; }
    private DRPGEnemyWavePlan(string encounterId, int seed, List<IReadOnlyList<DRPGEnemyWaveMember>> waves)
    { EncounterId = encounterId; BattleSeed = seed; Waves = Array.AsReadOnly(waves.ToArray()); }

    public static DRPGEnemyWavePlan Freeze(int battleSeed, string encounterId,
        IReadOnlyList<BattleParticipantSpec> authoredEnemies, int minimum, int maximum, int capacity)
    {
        if (authoredEnemies == null || authoredEnemies.Count == 0) throw new ArgumentException("A wave plan requires authored enemies.", nameof(authoredEnemies));
        if (capacity <= 0 || minimum <= 0 || maximum < minimum || maximum > capacity)
            throw new ArgumentOutOfRangeException(nameof(minimum), "Wave size must be within 1..capacity.");
        var random = new DRPGEnemyWaveRandom(encounterId, battleSeed);
        string id = encounterId.Normalize(NormalizationForm.FormC);
        var copied = new BattleParticipantSpec[authoredEnemies.Count];
        for (int i = 0; i < copied.Length; i++)
        {
            var member = authoredEnemies[i];
            if (member == null || member.Side != DRPGBattleSide.Enemy || member.RosterRole != DRPGBattleRosterRole.Enemy)
                throw new ArgumentException("Every wave member must be an authored enemy.", nameof(authoredEnemies));
            copied[i] = member;
        }
        var waves = new List<IReadOnlyList<DRPGEnemyWaveMember>>();
        for (int offset = 0; offset < copied.Length;)
        {
            int size = Math.Min(random.NextSize(minimum, maximum), copied.Length - offset);
            var batch = new DRPGEnemyWaveMember[size];
            for (int i = 0; i < size; i++) batch[i] = new DRPGEnemyWaveMember(new EnemyDeploymentId(id, offset + i), copied[offset + i]);
            waves.Add(Array.AsReadOnly(batch));
            offset += size;
        }
        return new DRPGEnemyWavePlan(id, battleSeed, waves);
    }
}
