using System;
using System.Collections.Generic;

[Serializable]
public sealed class DRPGSourceBoundTraitSaveData
{
    public string sourceId;
    public string traitId;
}

[Serializable]
public sealed class DRPGTraitPoolSnapshot
{
    public List<string> regularTraitIds = new List<string>();
    public List<DRPGSourceBoundTraitSaveData> sourceBoundTraits =
        new List<DRPGSourceBoundTraitSaveData>();
}

public sealed class DRPGTraitPoolState
{
    public const int RegularCapacity = 10;

    private readonly List<string> regularTraitIds = new List<string>();
    private readonly Dictionary<string, string> sourceBoundTraits =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyList<string> RegularTraitIds =>
        regularTraitIds.AsReadOnly();

    public IReadOnlyDictionary<string, string> SourceBoundTraits =>
        new Dictionary<string, string>(sourceBoundTraits, StringComparer.Ordinal);

    public IReadOnlyList<string> ResolvedTraitIds
    {
        get
        {
            var result = new List<string>(regularTraitIds);
            var sources = new List<string>(sourceBoundTraits.Keys);
            sources.Sort(StringComparer.Ordinal);
            for (int i = 0; i < sources.Count; i++)
                result.Add(sourceBoundTraits[sources[i]]);
            return result.AsReadOnly();
        }
    }

    public DRPGEquipmentOperationResult TryAddRegularTrait(string traitId)
    {
        if (string.IsNullOrWhiteSpace(traitId))
            return InvalidTraitId();
        if (ContainsResolvedTrait(traitId))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.TraitDuplicate,
                "Trait is already present: " + traitId);
        }
        if (regularTraitIds.Count >= RegularCapacity)
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.TraitCapacityExceeded,
                "Regular trait capacity is fixed at ten.");
        }

        regularTraitIds.Add(traitId);
        return DRPGEquipmentOperationResult.Ok();
    }

    public DRPGEquipmentOperationResult TryRemoveRegularTrait(string traitId)
    {
        if (string.IsNullOrWhiteSpace(traitId))
            return InvalidTraitId();
        if (regularTraitIds.Remove(traitId))
            return DRPGEquipmentOperationResult.Ok();
        if (sourceBoundTraits.ContainsValue(traitId))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.SourceTraitProtected,
                "Source-bound traits must be removed through their source.");
        }
        return DRPGEquipmentOperationResult.Fail(
            DRPGEquipmentError.TraitNotFound,
            "Regular trait was not found: " + traitId);
    }

    public DRPGEquipmentOperationResult TryBindSourceTrait(
        string sourceId,
        string traitId)
    {
        if (string.IsNullOrWhiteSpace(sourceId) ||
            string.IsNullOrWhiteSpace(traitId))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.InvalidRequest,
                "Source ID and trait ID are required.");
        }

        if (sourceBoundTraits.TryGetValue(sourceId, out string current) &&
            string.Equals(current, traitId, StringComparison.Ordinal))
        {
            return DRPGEquipmentOperationResult.Ok();
        }
        if (regularTraitIds.Contains(traitId) ||
            IsBoundByAnotherSource(sourceId, traitId))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.TraitDuplicate,
                "Trait is already present: " + traitId);
        }

        sourceBoundTraits[sourceId] = traitId;
        return DRPGEquipmentOperationResult.Ok();
    }

    public DRPGEquipmentOperationResult TryRemoveSourceTrait(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.InvalidRequest,
                "Source ID is required.");
        }
        if (!sourceBoundTraits.Remove(sourceId))
        {
            return DRPGEquipmentOperationResult.Fail(
                DRPGEquipmentError.TraitNotFound,
                "Source-bound trait was not found: " + sourceId);
        }
        return DRPGEquipmentOperationResult.Ok();
    }

    public bool ContainsResolvedTrait(string traitId)
    {
        return !string.IsNullOrWhiteSpace(traitId) &&
            (regularTraitIds.Contains(traitId) ||
             sourceBoundTraits.ContainsValue(traitId));
    }

    public DRPGTraitPoolSnapshot ExportSnapshot()
    {
        var snapshot = new DRPGTraitPoolSnapshot
        {
            regularTraitIds = new List<string>(regularTraitIds)
        };
        var sources = new List<string>(sourceBoundTraits.Keys);
        sources.Sort(StringComparer.Ordinal);
        for (int i = 0; i < sources.Count; i++)
        {
            string source = sources[i];
            snapshot.sourceBoundTraits.Add(new DRPGSourceBoundTraitSaveData
            {
                sourceId = source,
                traitId = sourceBoundTraits[source]
            });
        }
        return snapshot;
    }

    public DRPGEquipmentOperationResult ImportSnapshot(
        DRPGTraitPoolSnapshot snapshot)
    {
        if (snapshot == null || snapshot.regularTraitIds == null ||
            snapshot.sourceBoundTraits == null)
        {
            return InvalidSnapshot("Trait snapshot is incomplete.");
        }
        if (snapshot.regularTraitIds.Count > RegularCapacity)
        {
            return InvalidSnapshot(
                "Trait snapshot exceeds regular trait capacity.");
        }

        var restoredRegular = new List<string>();
        var restoredSources = new Dictionary<string, string>(StringComparer.Ordinal);
        var resolved = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < snapshot.regularTraitIds.Count; i++)
        {
            string traitId = snapshot.regularTraitIds[i];
            if (string.IsNullOrWhiteSpace(traitId) || !resolved.Add(traitId))
                return InvalidSnapshot("Regular trait IDs must be unique and named.");
            restoredRegular.Add(traitId);
        }
        for (int i = 0; i < snapshot.sourceBoundTraits.Count; i++)
        {
            DRPGSourceBoundTraitSaveData entry = snapshot.sourceBoundTraits[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.sourceId) ||
                string.IsNullOrWhiteSpace(entry.traitId) ||
                restoredSources.ContainsKey(entry.sourceId) ||
                !resolved.Add(entry.traitId))
            {
                return InvalidSnapshot(
                    "Source-bound traits must have unique sources and trait IDs.");
            }
            restoredSources.Add(entry.sourceId, entry.traitId);
        }

        regularTraitIds.Clear();
        regularTraitIds.AddRange(restoredRegular);
        sourceBoundTraits.Clear();
        foreach (KeyValuePair<string, string> pair in restoredSources)
            sourceBoundTraits.Add(pair.Key, pair.Value);
        return DRPGEquipmentOperationResult.Ok();
    }

    private bool IsBoundByAnotherSource(string sourceId, string traitId)
    {
        foreach (KeyValuePair<string, string> pair in sourceBoundTraits)
        {
            if (!string.Equals(pair.Key, sourceId, StringComparison.Ordinal) &&
                string.Equals(pair.Value, traitId, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static DRPGEquipmentOperationResult InvalidTraitId()
    {
        return DRPGEquipmentOperationResult.Fail(
            DRPGEquipmentError.InvalidRequest,
            "Trait ID is required.");
    }

    private static DRPGEquipmentOperationResult InvalidSnapshot(string message)
    {
        return DRPGEquipmentOperationResult.Fail(
            DRPGEquipmentError.InvalidSaveData,
            message);
    }
}
