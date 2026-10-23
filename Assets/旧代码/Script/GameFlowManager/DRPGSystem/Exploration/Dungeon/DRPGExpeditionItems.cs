using System;
/// <summary>Uses the existing party inventory; external loot never swaps into occupied slots.</summary>
public static class DRPGExpeditionItems
{
    public static int Collect(DRPGLootContainerState loot, DRPGPartyInventoryState bag, string id, int slot=-1)
    {
        if(loot==null||bag==null)return 0;
        if(slot<0)return DRPGLootTransferService.Transfer(loot,bag,id,loot.GetQuantity(id)).TransferredQuantity;
        if(slot>=bag.Capacity)return 0;
        DRPGLootItemDefinitionSO definition=null;
        foreach(var s in loot.Stacks)if(s.Definition.ItemId==id)definition=s.Definition;
        if(definition==null)return 0;
        var target=bag.GetSlot(slot).Item;
        if(target!=null&&(target.LootDefinition!=definition||target.Quantity>=target.MaxStack))return 0;
        int amount=Math.Min(loot.GetQuantity(id),definition.MaxStack-(target?.Quantity??0));
        var source=new DRPGTransientInventoryContainerState("expedition-loot-transfer",1);
        source.TryPlace(0,DRPGInventoryItemStack.FromLoot(definition,amount));
        var result=DRPGInventoryTransferService.Transfer(source,0,bag,slot);
        if(!result.Success)return 0;
        loot.RemoveUpTo(id,amount);return amount;
    }
    public static bool Consume(DRPGPartyInventoryState bag,int slot,string id)
    {
        if(bag==null)return false;
        var item=bag.GetSlot(slot).Item;
        if(item==null||item.LootDefinition==null||item.LootDefinition.ItemId!=id)return false;
        var mutable=(IDRPGInventoryMutableContainer)bag;
        var before=mutable.CaptureTransferMemento();var after=before.Copy();
        after.Items[slot]=item.Quantity>1?item.WithQuantity(item.Quantity-1):null;
        if(!mutable.ValidateTransferMemento(after).Success)return false;
        mutable.ApplyTransferMemento(after);return true;
    }
}
