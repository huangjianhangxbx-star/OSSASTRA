using System;
using System.Collections.Generic;

public sealed class PackageBattleItemSource : IDRPGBattleItemSource
{
    private readonly CharacterPackageSystem packageSystem;
    private readonly IReadOnlyList<DRPGBattleItemDefinition> items;

    public PackageBattleItemSource(
        CharacterPackageSystem packageSystem,
        IReadOnlyList<DRPGBattleItemDefinition> items)
    {
        this.packageSystem = packageSystem ??
            throw new ArgumentNullException(nameof(packageSystem));
        this.items = items ?? Array.Empty<DRPGBattleItemDefinition>();
    }

    public IReadOnlyList<DRPGBattleItemDefinition> Items => items;

    public int GetQuantity(DRPGBattleItemDefinition definition)
    {
        if (definition == null || definition.PackageItemDefinition == null ||
            packageSystem.Backpack == null)
        {
            return 0;
        }

        int quantity = 0;
        for (int i = 0; i < packageSystem.Backpack.Capacity; i++)
        {
            ItemInstance item = packageSystem.Backpack.GetItem(i);
            if (item != null &&
                ReferenceEquals(item.definition, definition.PackageItemDefinition))
            {
                quantity += Math.Max(0, item.quantity);
            }
        }
        return quantity;
    }

    public bool TryConsume(DRPGBattleItemDefinition definition, int amount)
    {
        return definition != null &&
            definition.PackageItemDefinition != null &&
            packageSystem.TryConsumeBackpackItem(
                definition.PackageItemDefinition,
                amount);
    }
}
