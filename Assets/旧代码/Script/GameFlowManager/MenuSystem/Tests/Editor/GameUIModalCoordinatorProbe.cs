#if UNITY_EDITOR
using System;

public static class GameUIModalCoordinatorProbe
{
    public static string Run()
    {
        var coordinator = new GameUIModalCoordinator();
        object package = new object();
        object menu = new object();

        AssertTrue(coordinator.TryAcquire(package),
            "The first modal owner must acquire the UI lease");
        AssertTrue(ReferenceEquals(package, coordinator.ActiveOwner),
            "The active owner must be observable");
        AssertTrue(coordinator.TryAcquire(package),
            "Duplicate acquisition by the same owner must be idempotent");
        AssertFalse(coordinator.TryAcquire(menu),
            "A second modal owner must be rejected");
        AssertFalse(coordinator.Release(menu),
            "A non-owner must not release another modal");
        AssertTrue(coordinator.Release(package),
            "The owner must release its lease");
        AssertTrue(coordinator.ActiveOwner == null,
            "Release must restore modal availability");
        AssertTrue(coordinator.TryAcquire(menu),
            "Another owner must acquire after release");

        return "PASS exclusive UI modal ownership";
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertFalse(bool value, string message) =>
        AssertTrue(!value, message);
}
#endif
