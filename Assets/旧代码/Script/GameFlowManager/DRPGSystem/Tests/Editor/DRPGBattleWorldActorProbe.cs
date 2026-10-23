#if UNITY_EDITOR
using System;
using UnityEngine;

public static class DRPGBattleWorldActorProbe
{
    public static string Run()
    {
        var host = new GameObject("WorldActorProbeHost");
        var prefabObject = new GameObject("WorldActorProbePrefab");
        try
        {
            BattleWorldActorView prefab =
                prefabObject.AddComponent<BattleWorldActorView>();
            prefab.EnsureAnchors();
            AssertNotNull(prefab.VisualRoot, "Visual root is required");
            AssertNotNull(prefab.ShadowAnchor, "Shadow anchor is required");
            AssertNotNull(prefab.VfxAnchor, "VFX anchor is required");
            AssertNotNull(prefab.TargetAnchor, "Target anchor is required");
            AssertNotNull(prefab.StatusAnchor, "Status anchor is required");
            AssertNotNull(prefab.PresentationAnchor,
                "Presentation anchor is required");

            BattleWorldActorPool pool =
                host.AddComponent<BattleWorldActorPool>();
            pool.Configure(prefab, host.transform);
            BattleWorldActorView first = pool.Acquire(11);
            BattleWorldActorView second = pool.Acquire(12);
            AssertFalse(ReferenceEquals(first, second),
                "Concurrent actors need independent world views");
            AssertEqual(11, first.RuntimeId,
                "Acquired views must bind the requested runtime ID");

            pool.Release(11);
            BattleWorldActorView reused = pool.Acquire(13);
            AssertTrue(ReferenceEquals(first, reused),
                "Released world views should be reused without allocation");
            AssertEqual(13, reused.RuntimeId,
                "Reused views must discard the previous actor identity");

            var binding = new BattleWorldActorBinding(pool);
            AssertTrue(binding.TryGet(13, out BattleWorldActorView resolved),
                "Binding service must resolve already active pooled views");
            AssertTrue(ReferenceEquals(reused, resolved),
                "Runtime ID lookup must return the exact active view");
            binding.ReleaseAll();
            AssertEqual(0, pool.ActiveCount,
                "ReleaseAll must leave no stale runtime bindings");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(prefabObject);
            UnityEngine.Object.DestroyImmediate(host);
        }

        return "PASS DRPG world actor proxies";
    }

    private static void AssertNotNull(object value, string message)
    {
        if (value == null)
            throw new InvalidOperationException(message);
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
    }

    private static void AssertFalse(bool value, string message) =>
        AssertTrue(!value, message);

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new InvalidOperationException(message);
    }
}
#endif
