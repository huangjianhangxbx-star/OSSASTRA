using System;
using System.Collections.Generic;

public sealed class DRPGEquipmentDefinitionRegistry
{
    private readonly Dictionary<string, DRPGWeaponDefinitionSO> weapons;
    private readonly Dictionary<string, DRPGOutfitDefinitionSO> outfits;
    private readonly Dictionary<string, DRPGAccessoryDefinitionSO> accessories;
    private readonly Dictionary<string, DRPGTraitDefinitionSO> traits;
    private readonly Dictionary<string, DRPGWeaponAffixDefinitionSO> affixes;
    private readonly Dictionary<string, DRPGWeaponCardDefinitionSO> cards;
    private readonly Dictionary<string, DRPGWeaponAttachmentDefinitionSO>
        attachments;
    private readonly Dictionary<string, DRPGLootItemDefinitionSO> lootItems;

    public DRPGEquipmentDefinitionRegistry(
        DRPGWeaponDefinitionSO[] weapons,
        DRPGOutfitDefinitionSO[] outfits,
        DRPGAccessoryDefinitionSO[] accessories,
        DRPGTraitDefinitionSO[] traits,
        DRPGWeaponAffixDefinitionSO[] affixes,
        DRPGWeaponCardDefinitionSO[] cards,
        DRPGWeaponAttachmentDefinitionSO[] attachments)
        : this(
            weapons,
            outfits,
            accessories,
            traits,
            affixes,
            cards,
            attachments,
            Array.Empty<DRPGLootItemDefinitionSO>())
    {
    }

    public DRPGEquipmentDefinitionRegistry(
        DRPGWeaponDefinitionSO[] weapons,
        DRPGOutfitDefinitionSO[] outfits,
        DRPGAccessoryDefinitionSO[] accessories,
        DRPGTraitDefinitionSO[] traits,
        DRPGWeaponAffixDefinitionSO[] affixes,
        DRPGWeaponCardDefinitionSO[] cards,
        DRPGWeaponAttachmentDefinitionSO[] attachments,
        DRPGLootItemDefinitionSO[] lootItems)
    {
        this.weapons = Build(weapons, value => value.WeaponId, "weapon");
        this.outfits = Build(outfits, value => value.OutfitId, "outfit");
        this.accessories = Build(
            accessories,
            value => value.AccessoryId,
            "accessory");
        this.traits = Build(traits, value => value.TraitId, "trait");
        this.affixes = Build(affixes, value => value.AffixId, "affix");
        this.cards = Build(cards, value => value.CardId, "card");
        this.attachments = Build(
            attachments,
            value => value.AttachmentId,
            "attachment");
        this.lootItems = Build(
            lootItems,
            value => value.ItemId,
            "loot item");
    }

    public bool TryGetWeapon(string id, out DRPGWeaponDefinitionSO value)
    {
        return weapons.TryGetValue(id ?? string.Empty, out value);
    }

    public bool TryGetOutfit(string id, out DRPGOutfitDefinitionSO value)
    {
        return outfits.TryGetValue(id ?? string.Empty, out value);
    }

    public bool TryGetAccessory(
        string id,
        out DRPGAccessoryDefinitionSO value)
    {
        return accessories.TryGetValue(id ?? string.Empty, out value);
    }

    public bool ContainsTrait(string id)
    {
        return traits.ContainsKey(id ?? string.Empty);
    }

    public bool TryGetTrait(string id, out DRPGTraitDefinitionSO value)
    {
        return traits.TryGetValue(id ?? string.Empty, out value);
    }

    public bool TryGetAffix(string id, out DRPGWeaponAffixDefinitionSO value)
    {
        return affixes.TryGetValue(id ?? string.Empty, out value);
    }

    public bool TryGetCard(string id, out DRPGWeaponCardDefinitionSO value)
    {
        return cards.TryGetValue(id ?? string.Empty, out value);
    }

    public bool TryGetAttachment(
        string id,
        out DRPGWeaponAttachmentDefinitionSO value)
    {
        return attachments.TryGetValue(id ?? string.Empty, out value);
    }

    public bool TryGetLootItem(
        string id,
        out DRPGLootItemDefinitionSO value)
    {
        return lootItems.TryGetValue(id ?? string.Empty, out value);
    }

    private static Dictionary<string, T> Build<T>(
        T[] source,
        Func<T, string> getId,
        string label)
        where T : UnityEngine.Object
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        if (source == null)
            return result;
        for (int i = 0; i < source.Length; i++)
        {
            T value = source[i];
            string id = value != null ? getId(value) : null;
            if (value == null || string.IsNullOrWhiteSpace(id) ||
                result.ContainsKey(id))
            {
                throw new InvalidOperationException(
                    "Equipment registry contains a null, unnamed, or duplicate " +
                    label + ".");
            }
            result.Add(id, value);
        }
        return result;
    }
}
