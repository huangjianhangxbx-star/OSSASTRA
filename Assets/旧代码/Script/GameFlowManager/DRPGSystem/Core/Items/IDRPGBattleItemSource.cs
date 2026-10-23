using System.Collections.Generic;

public interface IDRPGBattleItemSource
{
    IReadOnlyList<DRPGBattleItemDefinition> Items { get; }
    int GetQuantity(DRPGBattleItemDefinition definition);
    bool TryConsume(DRPGBattleItemDefinition definition, int amount);
}
