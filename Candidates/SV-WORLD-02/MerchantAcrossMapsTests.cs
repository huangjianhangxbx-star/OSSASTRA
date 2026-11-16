using System;
using System.Linq;
using Ossastra.Commerce;
using Ossastra.Inventory;

internal sealed class TestAccess : ICommerceAccessPolicy
{
    public CommerceAccess Check(string actorId, string merchantId, long sessionGeneration)
        => new CommerceAccess(true, null, 1, new[] { "team", "safe" }, "team", "team", new[] { "team", "safe" });
}

internal static class MerchantAcrossMapsTests
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static InventoryLedger NewLedger()
    {
        var ledger = new InventoryLedger();
        ledger.Register(new InventoryDefinition("trade-coin", 1, 1, 999999));
        ledger.Register(new InventoryDefinition("food", 1, 1, 10));
        ledger.AddContainer(new InventoryContainer("team", 5, 2));
        ledger.AddContainer(new InventoryContainer("safe", 2, 1, true));
        return ledger;
    }

    private static int Count(InventoryLedger ledger, string definitionId)
        => ledger.Items.Where(item => item.DefinitionId == definitionId).Sum(item => item.Quantity);

    private static void Main()
    {
        var definition = new MerchantDefinition("cave-merchant",
            new[] { new MerchantGoods("food", 10, 4, 5) }, 30);

        // Source map contains the visible shop. A real purchase changes stock and ledger.
        using var source = new CommerceEconomy(NewLedger(), new TestAccess());
        source.RegisterMerchant(definition, "merchant-01");
        Require(source.Ledger.Grant("team", "trade-coin", 30).Ok, "source grant failed");
        var quoted = source.QuotePurchase("hero", "merchant-01", "food", 1, 1);
        Require(quoted.Ok, "source quote failed: " + quoted.Reason);
        var bought = source.Execute(new TradeCommand(Guid.NewGuid(), quoted.Quote));
        Require(bought.Ok, "source purchase failed: " + bought.Reason);
        source.Advance(12);
        var departure = source.Capture();
        Require(source.Merchant("merchant-01").stock.Single().quantity == 4,
            "source merchant stock did not change");

        // Destination map has the same global definition catalog and no shop instance.
        using var destination = new CommerceEconomy(NewLedger(), new TestAccess());
        destination.RegisterDefinition(definition);
        Require(destination.Merchants.Length == 0 && destination.Merchant("merchant-01") == null,
            "destination unexpectedly registered a local merchant");
        var restored = destination.Restore(departure);
        Require(restored.Ok, "destination restore failed: " + restored.Reason);
        Require(destination.Merchant("merchant-01")?.stock.Single().quantity == 4
                && Count(destination.Ledger, "trade-coin") == 20
                && Count(destination.Ledger, "food") == 1,
            "merchant or ledger changed while crossing into a map without a shop");
        destination.Advance(5);
        var returnTrip = destination.Capture();

        // Returning to the source map must not seed its visible shop again.
        using var returned = new CommerceEconomy(NewLedger(), new TestAccess());
        returned.RegisterMerchant(definition, "merchant-01");
        restored = returned.Restore(returnTrip);
        Require(restored.Ok, "return restore failed: " + restored.Reason);
        Require(returned.Merchant("merchant-01").stock.Single().quantity == 4
                && returned.SimulationTime == 17
                && Count(returned.Ledger, "trade-coin") == 20
                && Count(returned.Ledger, "food") == 1,
            "return reseeded merchant stock, time, or inventory");

        Console.WriteLine("PASS merchant stock and ledger survive source → no-local-shop map → source");
    }
}
