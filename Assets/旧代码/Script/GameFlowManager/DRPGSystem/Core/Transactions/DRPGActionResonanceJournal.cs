using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Text;

internal sealed class DRPGResonanceResourceSnapshot
{
    private readonly DRPGResonanceExpeditionState source;
    private readonly ReadOnlyDictionary<string, int> values;
    internal DRPGResonanceResourceSnapshot(DRPGResonanceExpeditionState source, int revision, IDictionary<string, int> values)
    {
        this.source = source; Revision = revision;
        this.values = new ReadOnlyDictionary<string, int>(new SortedDictionary<string, int>(values, StringComparer.Ordinal));
    }
    internal int Revision { get; }
    internal int SourceIdentity => RuntimeHelpers.GetHashCode(source);
    internal IReadOnlyDictionary<string, int> Values => values;
    internal Dictionary<string, int> CopyValues() => new Dictionary<string, int>(values, StringComparer.Ordinal);
    internal bool Matches(DRPGResonanceResourceSnapshot other)
    {
        if (other == null || !ReferenceEquals(source, other.source) || Revision != other.Revision || values.Count != other.values.Count) return false;
        foreach (var pair in values) if (!other.values.TryGetValue(pair.Key, out var value) || value != pair.Value) return false;
        return true;
    }
}

internal sealed class DRPGActionResonanceMutation
{
    internal DRPGActionResonanceMutation(string eventKey, string partner, string skill, int amount, int capacity,
        bool consume, int before, int after)
    { EventKey = eventKey; Partner = partner; Skill = skill; Amount = amount; Capacity = capacity; Consume = consume; Before = before; After = after; }
    internal string EventKey { get; }
    internal string Partner { get; }
    internal string Skill { get; }
    internal int Amount { get; }
    internal int Capacity { get; }
    internal bool Consume { get; }
    internal int Before { get; }
    internal int After { get; }
    internal bool Same(DRPGActionResonanceMutation other) => other != null && EventKey == other.EventKey &&
        Partner == other.Partner && Skill == other.Skill && Amount == other.Amount && Capacity == other.Capacity &&
        Consume == other.Consume && Before == other.Before && After == other.After;

    internal static DRPGActionResonanceMutation Stage(Dictionary<string, int> values, string eventKey, string partner,
        string skill, int amount, int capacity, bool consume)
    {
        if (string.IsNullOrWhiteSpace(eventKey) || string.IsNullOrWhiteSpace(partner) || string.IsNullOrWhiteSpace(skill) ||
            partner != partner.Trim() || skill != skill.Trim() || partner.IndexOf('\u001f') >= 0 || skill.IndexOf('\u001f') >= 0 ||
            capacity <= 0 || amount < 0 || (consume && amount != 0))
            throw new InvalidOperationException("Resonance requires a stable event, owner/skill and non-negative integer request.");
        string key = partner + '\u001f' + skill;
        values.TryGetValue(key, out int before);
        if (consume && before < capacity) throw new InvalidOperationException("The captured resonance skill is not fully charged.");
        int after = consume ? 0 : (int)Math.Min((long)capacity, (long)before + amount);
        if (before != after)
        {
            if (after == 0) values.Remove(key); else values[key] = after;
        }
        return new DRPGActionResonanceMutation(eventKey, partner, skill, amount, capacity, consume, before, after);
    }
}

internal sealed partial class DRPGBattleActionResourceWorkspace
{
    private Dictionary<string, int> resonance;
    private readonly List<DRPGActionResonanceMutation> resonanceMutations = new List<DRPGActionResonanceMutation>();
    private readonly Dictionary<(string, string, string), DRPGActionResonanceMutation> resonanceKeys =
        new Dictionary<(string, string, string), DRPGActionResonanceMutation>();

    internal bool TryStageResonance(ActionIdentity identity, string eventKey, string partner, string skill,
        int amount, int capacity, bool consume, out string error)
    {
        if (!ValidateIdentity(identity, out error)) return false;
        if (snapshot.Resonance == null || !snapshot.IsKnownOwner(partner))
        { error = "Resonance requires a bound expedition and captured partner."; return false; }
        var key = (eventKey, partner, skill);
        if (resonanceKeys.TryGetValue(key, out var previous))
        {
            if (previous.Amount == amount && previous.Capacity == capacity && previous.Consume == consume) return true;
            error = "Resonance event has a conflicting request."; return false;
        }
        try
        {
            var candidate = resonance != null ? new Dictionary<string, int>(resonance, StringComparer.Ordinal) : snapshot.Resonance.CopyValues();
            var mutation = DRPGActionResonanceMutation.Stage(candidate, eventKey, partner, skill, amount, capacity, consume);
            resonance = candidate; this.identity = identity;
            resonanceKeys.Add(key, mutation); resonanceMutations.Add(mutation);
            return true;
        }
        catch (Exception exception) { error = exception.Message; return false; }
    }
}

internal static partial class DRPGActionResolutionFingerprint
{
    private static void AppendResonance(StringBuilder builder, DRPGBattleActionResourceJournal journal)
    {
        var snapshot = journal.Snapshot.Resonance;
        AppendResonanceRules(builder, journal.Snapshot.ResonanceRules);
        Append(builder, snapshot != null ? 1 : 0);
        if (snapshot != null)
        {
            Append(builder, snapshot.SourceIdentity); Append(builder, snapshot.Revision); Append(builder, snapshot.Values.Count);
            foreach (var pair in snapshot.Values) { Append(builder, pair.Key); Append(builder, pair.Value); }
        }
        Append(builder, journal.ResonanceMutations.Count);
        foreach (var mutation in journal.ResonanceMutations)
        {
            Append(builder, mutation.EventKey); Append(builder, mutation.Partner); Append(builder, mutation.Skill);
            Append(builder, mutation.Amount); Append(builder, mutation.Capacity); Append(builder, mutation.Consume ? 1 : 0);
            Append(builder, mutation.Before); Append(builder, mutation.After);
        }
    }
}
