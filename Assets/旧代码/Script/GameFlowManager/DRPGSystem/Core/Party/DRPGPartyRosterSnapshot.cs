using System;
using System.Collections.Generic;

/// <summary>
/// Frozen physical party order consumed by one battle. Protagonist identity is
/// stored separately and therefore does not imply physical slot zero.
/// </summary>
public sealed class DRPGPartyRosterSnapshot
{
    private readonly DRPGCombatantProfileSO[] orderedProfiles;
    private readonly IReadOnlyList<DRPGCombatantProfileSO> readOnlyProfiles;
    private readonly DRPGCombatantProfileSO protagonist;

    internal DRPGPartyRosterSnapshot(
        IReadOnlyList<DRPGCombatantProfileSO> source)
        : this(source, source != null && source.Count > 0 ? source[0] : null)
    {
    }

    internal DRPGPartyRosterSnapshot(
        IReadOnlyList<DRPGCombatantProfileSO> source,
        DRPGCombatantProfileSO protagonistProfile)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        orderedProfiles = new DRPGCombatantProfileSO[source.Count];
        for (int i = 0; i < source.Count; i++)
            orderedProfiles[i] = source[i];
        readOnlyProfiles = Array.AsReadOnly(orderedProfiles);
        protagonist = protagonistProfile;
    }

    public int Count => orderedProfiles.Length;
    public DRPGCombatantProfileSO Protagonist => protagonist;
    public IReadOnlyList<DRPGCombatantProfileSO> OrderedProfiles =>
        readOnlyProfiles;

    public int IndexOfStableId(string stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId))
            return -1;
        for (int i = 0; i < orderedProfiles.Length; i++)
        {
            if (string.Equals(
                    orderedProfiles[i].CombatantId,
                    stableId,
                    StringComparison.Ordinal))
            {
                return i;
            }
        }
        return -1;
    }

    public DRPGPartyRosterSnapshot WithPhysicalOrder(
        DRPGNodeFormationSnapshot formation)
    {
        if (formation == null)
            throw new ArgumentNullException(nameof(formation));
        if (formation.Count != orderedProfiles.Length)
            throw new ArgumentException(
                "Formation membership does not match the party roster.",
                nameof(formation));

        var byId = new Dictionary<string, DRPGCombatantProfileSO>(
            StringComparer.Ordinal);
        for (int i = 0; i < orderedProfiles.Length; i++)
        {
            DRPGCombatantProfileSO profile = orderedProfiles[i];
            if (profile == null || string.IsNullOrWhiteSpace(profile.CombatantId) ||
                !byId.TryAdd(profile.CombatantId, profile))
            {
                throw new ArgumentException(
                    "The roster contains a missing or duplicate stable id.");
            }
        }

        var reordered = new DRPGCombatantProfileSO[formation.Count];
        for (int i = 0; i < formation.Count; i++)
        {
            if (!byId.Remove(formation.Order[i], out reordered[i]))
            {
                throw new ArgumentException(
                    "Formation contains an unknown or duplicate stable id: " +
                    formation.Order[i], nameof(formation));
            }
        }
        if (byId.Count != 0)
            throw new ArgumentException("Formation omits party members.", nameof(formation));
        return new DRPGPartyRosterSnapshot(reordered, protagonist);
    }
}
