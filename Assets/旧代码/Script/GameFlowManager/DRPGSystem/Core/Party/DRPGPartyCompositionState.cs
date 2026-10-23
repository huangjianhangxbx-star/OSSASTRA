using System;
using System.Collections.Generic;

/// <summary>
/// Mutable menu-side party order. Battles only consume immutable snapshots.
/// </summary>
public sealed class DRPGPartyCompositionState
{
    public const int MaximumPartyCount = 5;

    private readonly List<DRPGCombatantProfileSO> orderedProfiles;

    public DRPGPartyCompositionState(
        DRPGCombatantProfileSO protagonist,
        IEnumerable<DRPGCombatantProfileSO> partyMembers)
    {
        var candidate = new List<DRPGCombatantProfileSO>();
        candidate.Add(protagonist);
        if (partyMembers != null)
            candidate.AddRange(partyMembers);

        Validate(candidate);
        orderedProfiles = candidate;
    }

    public int Count => orderedProfiles.Count;
    public DRPGCombatantProfileSO Protagonist => orderedProfiles[0];

    public static DRPGPartyCompositionState FromSetup(DRPGPartySetupSO setup)
    {
        if (setup == null)
            throw new ArgumentNullException(nameof(setup));
        return new DRPGPartyCompositionState(
            setup.Protagonist,
            setup.PartyMembers);
    }

    public DRPGPartyRosterSnapshot CreateSnapshot()
    {
        return new DRPGPartyRosterSnapshot(orderedProfiles);
    }

    public bool ContainsCharacterId(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
            return false;
        for (int i = 0; i < orderedProfiles.Count; i++)
        {
            if (string.Equals(
                    orderedProfiles[i].CombatantId,
                    characterId,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    public bool TrySetResonancePartner(
        DRPGCombatantProfileSO partner,
        out string error)
    {
        if (partner == null)
        {
            error = "A resonance partner is required.";
            return false;
        }

        int partnerIndex = -1;
        for (int i = 1; i < orderedProfiles.Count; i++)
        {
            if (ReferenceEquals(orderedProfiles[i], partner) ||
                string.Equals(
                    orderedProfiles[i].CombatantId,
                    partner.CombatantId,
                    StringComparison.Ordinal))
            {
                partnerIndex = i;
                break;
            }
        }
        if (partnerIndex < 0)
        {
            error = "The resonance partner must already be in the party.";
            return false;
        }
        if (partnerIndex == 1)
        {
            error = string.Empty;
            return true;
        }

        var candidate = new List<DRPGCombatantProfileSO>(orderedProfiles);
        candidate.RemoveAt(partnerIndex);
        candidate.Insert(1, partner);
        bool changed = TrySetOrderedProfiles(candidate, out error);
        return changed || string.IsNullOrEmpty(error);
    }

    public DRPGPartyCompositionSaveData CreateSaveData()
    {
        var ids = new string[orderedProfiles.Count];
        for (int i = 0; i < orderedProfiles.Count; i++)
            ids[i] = orderedProfiles[i].CombatantId;
        return new DRPGPartyCompositionSaveData { orderedCharacterIds = ids };
    }

    public bool TrySetOrderedProfiles(
        IReadOnlyList<DRPGCombatantProfileSO> candidate,
        out string error)
    {
        try
        {
            Validate(candidate);
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }

        if (!ReferenceEquals(candidate[0], Protagonist) &&
            !string.Equals(
                candidate[0].CombatantId,
                Protagonist.CombatantId,
                StringComparison.Ordinal))
        {
            error = "Party index zero must remain the protagonist.";
            return false;
        }

        bool changed = candidate.Count != orderedProfiles.Count;
        if (!changed)
        {
            for (int i = 0; i < candidate.Count; i++)
            {
                if (!ReferenceEquals(candidate[i], orderedProfiles[i]))
                {
                    changed = true;
                    break;
                }
            }
        }

        if (!changed)
        {
            error = string.Empty;
            return false;
        }

        orderedProfiles.Clear();
        for (int i = 0; i < candidate.Count; i++)
            orderedProfiles.Add(candidate[i]);
        error = string.Empty;
        return true;
    }

    private static void Validate(
        IReadOnlyList<DRPGCombatantProfileSO> candidate)
    {
        if (candidate == null || candidate.Count == 0 || candidate[0] == null)
            throw new ArgumentException("A required protagonist is missing.");
        if (candidate.Count > MaximumPartyCount)
        {
            throw new ArgumentException(
                $"A party may contain at most {MaximumPartyCount} members including the protagonist.");
        }

        var stableIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < candidate.Count; i++)
        {
            DRPGCombatantProfileSO profile = candidate[i];
            if (profile == null)
                throw new ArgumentException($"Party member at index {i} is missing.");
            string stableId = profile.CombatantId;
            if (string.IsNullOrWhiteSpace(stableId))
                throw new ArgumentException($"Party member at index {i} has no stable ID.");
            if (!stableIds.Add(stableId))
                throw new ArgumentException($"Duplicate party stable ID: {stableId}.");
        }
    }
}
