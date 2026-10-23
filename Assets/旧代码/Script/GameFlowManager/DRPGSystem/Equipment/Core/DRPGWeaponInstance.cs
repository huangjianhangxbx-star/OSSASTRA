using System;
using System.Collections.Generic;
using UnityEngine;

public interface IDRPGRandomSource
{
    float Range(float minimumInclusive, float maximumInclusive);
    int RangeInclusive(int minimumInclusive, int maximumInclusive);
}

public sealed class DRPGUnityRandomSource : IDRPGRandomSource
{
    public float Range(float minimumInclusive, float maximumInclusive)
    {
        return UnityEngine.Random.Range(minimumInclusive, maximumInclusive);
    }

    public int RangeInclusive(int minimumInclusive, int maximumInclusive)
    {
        if (maximumInclusive <= minimumInclusive)
            return minimumInclusive;
        return UnityEngine.Random.Range(minimumInclusive, maximumInclusive + 1);
    }
}

public sealed class DRPGWeaponInstance
{
    private DRPGWeaponInstance(
        string instanceId,
        DRPGWeaponDefinitionSO definition,
        int rolledMaxDurability,
        int currentDurability,
        IReadOnlyList<string> affixIds,
        IReadOnlyList<string> cardIds,
        IReadOnlyList<string> attachmentIds)
    {
        InstanceId = instanceId;
        Definition = definition;
        RolledMaxDurability = rolledMaxDurability;
        CurrentMaxDurability = rolledMaxDurability;
        CurrentDurability = currentDurability;
        AffixIds = affixIds;
        CardIds = cardIds;
        AttachmentIds = attachmentIds;
    }

    public string InstanceId { get; }
    public DRPGWeaponDefinitionSO Definition { get; }
    public int RolledMaxDurability { get; }
    public int CurrentMaxDurability { get; private set; }
    public int CurrentDurability { get; private set; }
    public IReadOnlyList<string> AffixIds { get; }
    public IReadOnlyList<string> CardIds { get; }
    public IReadOnlyList<string> AttachmentIds { get; }
    public bool IsBroken => CurrentDurability <= 0;
    internal long DurabilityRevision { get; private set; }

    public static DRPGWeaponInstance Create(
        string instanceId,
        DRPGWeaponDefinitionSO definition,
        IDRPGRandomSource randomSource,
        IReadOnlyList<DRPGWeaponAffixDefinitionSO> affixes = null,
        IReadOnlyList<DRPGWeaponCardDefinitionSO> cards = null,
        IReadOnlyList<DRPGWeaponAttachmentDefinitionSO> attachments = null)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            throw new ArgumentException("Instance ID is required.", nameof(instanceId));
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        if (randomSource == null)
            throw new ArgumentNullException(nameof(randomSource));

        definition.ValidateOrThrow();
        DRPGWeaponRarityBudget budget = definition.RarityBudget;
        List<string> affixIds = SnapshotAffixes(
            definition.FixedAffixes,
            affixes,
            budget.AffixSlots);
        List<string> cardIds = SnapshotCards(cards, budget);
        List<string> attachmentIds = SnapshotAttachments(attachments);

        int maximum;
        int current;
        if (definition.OwnershipKind == DRPGWeaponOwnershipKind.Signature)
        {
            maximum = definition.BaseDurability;
            current = maximum;
        }
        else
        {
            DRPGWeaponDurabilityProfile profile =
                DRPGWeaponRarityRules.GetDurabilityProfile(definition.Rarity);
            float factor = Mathf.Clamp(
                randomSource.Range(
                    profile.MinimumMaximumFactor,
                    profile.MaximumMaximumFactor),
                profile.MinimumMaximumFactor,
                profile.MaximumMaximumFactor);
            maximum = Mathf.Max(
                1,
                Mathf.RoundToInt(definition.BaseDurability * factor));
            int currentFloor = Mathf.Clamp(
                Mathf.CeilToInt(
                    maximum * definition.InitialDurabilityFloorRatio),
                1,
                maximum);
            current = Mathf.Clamp(
                randomSource.RangeInclusive(currentFloor, maximum),
                currentFloor,
                maximum);
        }

        return new DRPGWeaponInstance(
            instanceId,
            definition,
            maximum,
            current,
            affixIds.AsReadOnly(),
            cardIds.AsReadOnly(),
            attachmentIds.AsReadOnly());
    }

    internal void ApplyDurabilityLoss(int amount)
    {
        CurrentDurability = Math.Max(0, CurrentDurability - Math.Max(0, amount));
        DurabilityRevision++;
    }

    internal void RestoreDurability(int maximum, int current)
    {
        CurrentMaxDurability = Math.Max(1, maximum);
        CurrentDurability = Math.Max(0, Math.Min(CurrentMaxDurability, current));
        DurabilityRevision++;
    }

    internal DRPGWeaponInstanceSaveData ExportSnapshot()
    {
        return new DRPGWeaponInstanceSaveData
        {
            instanceId = InstanceId,
            definitionId = Definition.WeaponId,
            rolledMaxDurability = RolledMaxDurability,
            currentMaxDurability = CurrentMaxDurability,
            currentDurability = CurrentDurability,
            affixIds = new List<string>(AffixIds),
            cardIds = new List<string>(CardIds),
            attachmentIds = new List<string>(AttachmentIds)
        };
    }

    internal static DRPGWeaponInstance Restore(
        DRPGWeaponInstanceSaveData snapshot,
        DRPGWeaponDefinitionSO definition)
    {
        if (snapshot == null || definition == null ||
            string.IsNullOrWhiteSpace(snapshot.instanceId) ||
            snapshot.rolledMaxDurability < 1 ||
            snapshot.currentMaxDurability < 1 ||
            snapshot.currentDurability < 0 ||
            snapshot.currentDurability > snapshot.currentMaxDurability)
        {
            throw new InvalidOperationException(
                "Weapon instance snapshot is invalid.");
        }

        var result = new DRPGWeaponInstance(
            snapshot.instanceId,
            definition,
            snapshot.rolledMaxDurability,
            snapshot.currentDurability,
            new List<string>(snapshot.affixIds ?? new List<string>()).AsReadOnly(),
            new List<string>(snapshot.cardIds ?? new List<string>()).AsReadOnly(),
            new List<string>(
                snapshot.attachmentIds ?? new List<string>()).AsReadOnly());
        result.RestoreDurability(
            snapshot.currentMaxDurability,
            snapshot.currentDurability);
        return result;
    }

    private static List<string> SnapshotAffixes(
        IReadOnlyList<DRPGWeaponAffixDefinitionSO> fixedAffixes,
        IReadOnlyList<DRPGWeaponAffixDefinitionSO> rolledAffixes,
        int capacity)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        AddAffixes(fixedAffixes, result, seen);
        AddAffixes(rolledAffixes, result, seen);
        if (result.Count > capacity)
        {
            throw new InvalidOperationException(
                "Weapon affixes exceed the rarity slot budget.");
        }
        return result;
    }

    private static void AddAffixes(
        IReadOnlyList<DRPGWeaponAffixDefinitionSO> source,
        ICollection<string> destination,
        ISet<string> seen)
    {
        if (source == null)
            return;
        for (int i = 0; i < source.Count; i++)
        {
            DRPGWeaponAffixDefinitionSO affix = source[i];
            if (affix == null || string.IsNullOrWhiteSpace(affix.AffixId) ||
                !seen.Add(affix.AffixId))
            {
                throw new InvalidOperationException(
                    "Weapon instance contains a null, unnamed, or duplicate affix.");
            }
            destination.Add(affix.AffixId);
        }
    }

    private static List<string> SnapshotCards(
        IReadOnlyList<DRPGWeaponCardDefinitionSO> cards,
        DRPGWeaponRarityBudget budget)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int totalCost = 0;
        if (cards != null)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                DRPGWeaponCardDefinitionSO card = cards[i];
                if (card == null || string.IsNullOrWhiteSpace(card.CardId) ||
                    !seen.Add(card.CardId))
                {
                    throw new InvalidOperationException(
                        "Weapon instance contains a null, unnamed, or duplicate card.");
                }
                totalCost += card.Cost;
                result.Add(card.CardId);
            }
        }
        if (result.Count > budget.CardSlots ||
            totalCost > budget.CardCostCapacity)
        {
            throw new InvalidOperationException(
                "Weapon cards exceed the rarity slot or Cost budget.");
        }
        return result;
    }

    private static List<string> SnapshotAttachments(
        IReadOnlyList<DRPGWeaponAttachmentDefinitionSO> attachments)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (attachments != null)
        {
            for (int i = 0; i < attachments.Count; i++)
            {
                DRPGWeaponAttachmentDefinitionSO attachment = attachments[i];
                if (attachment == null ||
                    string.IsNullOrWhiteSpace(attachment.AttachmentId) ||
                    !seen.Add(attachment.AttachmentId))
                {
                    throw new InvalidOperationException(
                        "Weapon instance contains a null, unnamed, or duplicate attachment.");
                }
                result.Add(attachment.AttachmentId);
            }
        }
        if (result.Count > DRPGWeaponRarityRules.AttachmentSlots)
        {
            throw new InvalidOperationException(
                "Weapon attachments exceed the fixed slot budget.");
        }
        return result;
    }
}
