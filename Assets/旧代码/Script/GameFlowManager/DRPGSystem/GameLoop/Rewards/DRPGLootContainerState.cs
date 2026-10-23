using System;
using System.Collections.Generic;

public sealed class DRPGLootStackState
{
    public DRPGLootStackState(
        DRPGLootItemDefinitionSO definition,
        int quantity)
    {
        Definition = definition ??
            throw new ArgumentNullException(nameof(definition));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        Quantity = quantity;
    }

    public DRPGLootItemDefinitionSO Definition { get; }
    public int Quantity { get; private set; }

    internal void Add(int quantity)
    {
        if (quantity <= 0)
            return;
        checked { Quantity += quantity; }
    }

    internal int RemoveUpTo(int quantity)
    {
        int removed = Math.Min(Quantity, Math.Max(0, quantity));
        Quantity -= removed;
        return removed;
    }
}

/// <summary>
/// Capacity-exempt, one-shot reward container. It owns only unclaimed loot;
/// transfer into the party backpack is performed by DRPGLootTransferService.
/// </summary>
public sealed class DRPGLootContainerState
{
    private readonly List<DRPGLootStackState> orderedStacks =
        new List<DRPGLootStackState>();
    private readonly Dictionary<string, DRPGLootStackState> stacksById =
        new Dictionary<string, DRPGLootStackState>(StringComparer.Ordinal);

    public DRPGLootContainerState()
    {
    }

    public DRPGLootContainerState(IEnumerable<DRPGLootStackState> stacks)
    {
        if (stacks == null)
            return;
        foreach (DRPGLootStackState stack in stacks)
            Add(stack?.Definition, stack != null ? stack.Quantity : 0);
    }

    public IReadOnlyList<DRPGLootStackState> Stacks =>
        orderedStacks.AsReadOnly();
    public bool IsEmpty => orderedStacks.Count == 0;
    public int Revision { get; private set; }

    public int GetQuantity(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId) &&
            stacksById.TryGetValue(itemId, out DRPGLootStackState stack)
            ? stack.Quantity
            : 0;
    }

    public void Add(DRPGLootItemDefinitionSO definition, int quantity)
    {
        if (definition == null || quantity <= 0)
            return;
        string itemId = definition.ItemId;
        if (stacksById.TryGetValue(itemId, out DRPGLootStackState existing))
        {
            if (!ReferenceEquals(existing.Definition, definition))
            {
                throw new InvalidOperationException(
                    "Loot definitions share an item ID: " + itemId);
            }
            existing.Add(quantity);
        }
        else
        {
            var created = new DRPGLootStackState(definition, quantity);
            stacksById.Add(itemId, created);
            orderedStacks.Add(created);
        }
        Revision++;
    }

    internal int RemoveUpTo(string itemId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0 ||
            !stacksById.TryGetValue(itemId, out DRPGLootStackState stack))
        {
            return 0;
        }

        int removed = stack.RemoveUpTo(quantity);
        if (stack.Quantity == 0)
        {
            stacksById.Remove(itemId);
            orderedStacks.Remove(stack);
        }
        if (removed > 0)
            Revision++;
        return removed;
    }
}
