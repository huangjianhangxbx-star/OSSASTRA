#if UNITY_EDITOR
using System;

public static class DRPGNodePartyFormationProbe
{
    public static string Run()
    {
        var formation = new DRPGNodePartyFormation(
            new[] { "hero", "a", "b" });

        if (!formation.TrySwap("hero", "b") ||
            formation.Order[2] != "hero")
        {
            throw new InvalidOperationException(
                "Protagonist physical slot must be movable.");
        }

        formation.Remove("a");
        formation.Add("new");
        formation.RestoreDefault();
        if (string.Join(",", formation.Order) != "hero,b,new")
        {
            throw new InvalidOperationException(
                "Reset must skip absent members and append new members.");
        }

        DRPGNodeFormationSnapshot snapshot = formation.Capture();
        long revision = snapshot.Revision;
        formation.TrySwap("hero", "b");
        if (snapshot.Order[0] != "hero" ||
            snapshot.IndexOf("b") != 1 ||
            snapshot.Revision != revision)
        {
            throw new InvalidOperationException(
                "A captured formation snapshot must be immutable.");
        }

        bool duplicateRejected = false;
        try
        {
            _ = new DRPGNodePartyFormation(
                new[] { "hero", "hero" });
        }
        catch (ArgumentException)
        {
            duplicateRejected = true;
        }

        if (!duplicateRejected)
            throw new InvalidOperationException("Duplicate stable ids must fail.");

        return "PASS node-local party formation";
    }
}
#endif
