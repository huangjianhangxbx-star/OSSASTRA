#if UNITY_EDITOR
using System;
using UnityEngine;

public static class CombatControlLifecycleProbe
{
    private sealed class ProbeCommittedState : HoundCombatActionState
    {
        public override string StateName => "ProbeCommitted";

        public void Acquire()
        {
            AcquireCommittedControl();
        }

        public override void Exit()
        {
            ReleaseCommittedControl();
        }
    }

    public static string Run()
    {
        GameObject rootObject = new GameObject("CombatControlLifecycleProbe");

        try
        {
            CharacterRoot root = rootObject.AddComponent<CharacterRoot>();
            Rigidbody2D body = rootObject.AddComponent<Rigidbody2D>();
            var state = new ProbeCommittedState();
            state.Initialize(null, new CharacterDataBus(), rootObject.transform, body, root);

            state.Acquire();
            AssertFalse(
                root.controlAuthority.IsAllowed(CharacterControlChannel.Move),
                "Committed state must block movement");
            AssertEqual(
                CharacterFacingMode.Hold,
                root.controlAuthority.ResolveFacingMode(CharacterFacingMode.Cursor),
                "Committed state must hold facing");
            AssertEqual(
                AimTrackingMode.AnimationDriven,
                root.controlAuthority.ResolveAimTrackingMode(AimTrackingMode.FollowCursor),
                "Committed state must yield IK to animation");

            state.Exit();
            state.Exit();
            AssertTrue(
                root.controlAuthority.IsAllowed(CharacterControlChannel.Move),
                "Exit must restore movement idempotently");
            AssertEqual(
                CharacterFacingMode.Cursor,
                root.controlAuthority.ResolveFacingMode(CharacterFacingMode.Cursor),
                "Exit must restore the default facing policy");
            AssertEqual(
                AimTrackingMode.FollowCursor,
                root.controlAuthority.ResolveAimTrackingMode(AimTrackingMode.FollowCursor),
                "Exit must restore cursor IK tracking");

            return "PASS combat control lifecycle";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rootObject);
        }
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertFalse(bool value, string message)
    {
        AssertTrue(!value, message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }
}
#endif
