using System;
using System.Collections.Generic;

public sealed class DRPGResonanceExpeditionState
{
    private const char KeySeparator = '\u001f';
    private Dictionary<string, int> progressBySkill =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public int Revision { get; private set; }

    internal DRPGResonanceResourceSnapshot CaptureActionResources() =>
        new DRPGResonanceResourceSnapshot(this, Revision, progressBySkill);

    internal PreparedActionResources PrepareActionResources(DRPGResonanceResourceSnapshot snapshot,
        IReadOnlyList<DRPGActionResonanceMutation> mutations, DRPGBattleStateMutationCapability capability)
    {
        if (capability == null || snapshot == null || !snapshot.Matches(CaptureActionResources()))
            throw new InvalidOperationException("The expedition resonance snapshot is foreign or stale.");
        var prepared = snapshot.CopyValues();
        var keys = new HashSet<(string, string, string)>();
        bool changed = false;
        foreach (var mutation in mutations)
        {
            if (mutation == null || !keys.Add((mutation.EventKey, mutation.Partner, mutation.Skill)))
                throw new InvalidOperationException("A resonance event is missing or duplicated.");
            var actual = DRPGActionResonanceMutation.Stage(prepared, mutation.EventKey, mutation.Partner,
                mutation.Skill, mutation.Amount, mutation.Capacity, mutation.Consume);
            if (!actual.Same(mutation)) throw new InvalidOperationException("Resonance journal facts do not match their requests.");
            changed |= actual.Before != actual.After;
        }
        return new PreparedActionResources(this, prepared, changed ? checked(Revision + 1) : Revision, capability);
    }

    internal sealed class PreparedActionResources
    {
        private readonly DRPGResonanceExpeditionState state;
        private readonly Dictionary<string, int> values;
        private readonly int revision;
        private readonly DRPGBattleStateMutationCapability capability;
        internal PreparedActionResources(DRPGResonanceExpeditionState state, Dictionary<string, int> values,
            int revision, DRPGBattleStateMutationCapability capability)
        { this.state = state; this.values = values; this.revision = revision; this.capability = capability; }
        internal void Apply(DRPGBattleStateMutationCapability supplied)
        {
            if (capability == null || !ReferenceEquals(capability, supplied))
                throw new InvalidOperationException("Expedition resources require the preparing State's capability.");
            state.progressBySkill = values;
            state.Revision = revision;
        }
    }

    public int GetProgress(string partnerCharacterId, string skillId)
    {
        string key = CreateKey(partnerCharacterId, skillId);
        return key.Length > 0 && progressBySkill.TryGetValue(key, out int value)
            ? value
            : 0;
    }

    public int AddProgress(
        string partnerCharacterId,
        string skillId,
        int amount,
        int maximum = int.MaxValue)
    {
        string key = CreateKey(partnerCharacterId, skillId);
        if (key.Length == 0 || amount <= 0)
            return GetProgress(partnerCharacterId, skillId);

        int current = GetProgress(partnerCharacterId, skillId);
        long summed = (long)current + amount;
        int next = (int)Math.Min(Math.Max(0, maximum), summed);
        if (next == current)
            return current;
        progressBySkill[key] = next;
        Revision++;
        return next;
    }

    public bool Consume(string partnerCharacterId, string skillId)
    {
        string key = CreateKey(partnerCharacterId, skillId);
        if (key.Length == 0 || !progressBySkill.Remove(key))
            return false;
        Revision++;
        return true;
    }

    public bool ClearPartner(string partnerCharacterId)
    {
        if (string.IsNullOrWhiteSpace(partnerCharacterId) ||
            progressBySkill.Count == 0)
        {
            return false;
        }

        string prefix = partnerCharacterId.Trim() + KeySeparator;
        var removals = new List<string>();
        foreach (string key in progressBySkill.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
                removals.Add(key);
        }
        for (int i = 0; i < removals.Count; i++)
            progressBySkill.Remove(removals[i]);
        if (removals.Count == 0)
            return false;
        Revision++;
        return true;
    }

    public bool ClearAll()
    {
        if (progressBySkill.Count == 0)
            return false;
        progressBySkill.Clear();
        Revision++;
        return true;
    }

    private static string CreateKey(string partnerCharacterId, string skillId)
    {
        if (string.IsNullOrWhiteSpace(partnerCharacterId) ||
            string.IsNullOrWhiteSpace(skillId))
        {
            return string.Empty;
        }
        return partnerCharacterId.Trim() + KeySeparator + skillId.Trim();
    }
}
