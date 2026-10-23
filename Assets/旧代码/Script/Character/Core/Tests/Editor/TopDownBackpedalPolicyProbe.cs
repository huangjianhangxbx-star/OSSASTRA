#if UNITY_EDITOR
using System;
using UnityEngine;

public static class TopDownBackpedalPolicyProbe
{
    public static string Run()
    {
        VerifyOppositeMovementEntersBackpedal();
        VerifyPerpendicularMovementRemainsOrdinaryLocomotion();
        VerifyRearDiagonalMovementEntersBackpedal();
        VerifyHysteresisPreventsBoundaryChatter();
        VerifyZeroInputNeverBackpedals();
        VerifyCursorFacingUsesWalkBackStateAndSpeed();
        VerifyMovementFacingIgnoresAimDirection();
        VerifyIdleClearsBackpedalRuntimeState();
        return "PASS topdown backpedal policy";
    }

    private static void VerifyOppositeMovementEntersBackpedal()
    {
        bool result = TopDownBackpedalPolicy.Resolve(
            Vector2.left,
            Vector2.right,
            wasBackpedaling: false);

        if (!result)
            throw new Exception("Directly opposite movement did not enter backpedal");
    }

    private static void VerifyPerpendicularMovementRemainsOrdinaryLocomotion()
    {
        bool result = TopDownBackpedalPolicy.Resolve(
            Vector2.up,
            Vector2.right,
            wasBackpedaling: false);

        if (result)
            throw new Exception("Perpendicular strafe was incorrectly classified as backpedal");
    }

    private static void VerifyRearDiagonalMovementEntersBackpedal()
    {
        bool result = TopDownBackpedalPolicy.Resolve(
            new Vector2(-1f, 1f),
            Vector2.right,
            wasBackpedaling: false);

        if (!result)
            throw new Exception("Rear diagonal movement did not enter the backpedal sector");
    }

    private static void VerifyHysteresisPreventsBoundaryChatter()
    {
        Vector2 boundaryDirection = new Vector2(-0.4f, 0.9165151f);

        bool entering = TopDownBackpedalPolicy.Resolve(
            boundaryDirection,
            Vector2.right,
            wasBackpedaling: false);
        if (entering)
            throw new Exception("Movement outside the entry threshold entered backpedal");

        bool staying = TopDownBackpedalPolicy.Resolve(
            boundaryDirection,
            Vector2.right,
            wasBackpedaling: true);
        if (!staying)
            throw new Exception("Backpedal left inside the hysteresis band");

        bool exiting = TopDownBackpedalPolicy.Resolve(
            new Vector2(-0.2f, 0.9797959f),
            Vector2.right,
            wasBackpedaling: true);
        if (exiting)
            throw new Exception("Backpedal did not exit beyond the release threshold");
    }

    private static void VerifyZeroInputNeverBackpedals()
    {
        bool result = TopDownBackpedalPolicy.Resolve(
            Vector2.zero,
            Vector2.right,
            wasBackpedaling: true);

        if (result)
            throw new Exception("Zero movement input retained backpedal");
    }

    private static void VerifyCursorFacingUsesWalkBackStateAndSpeed()
    {
        var go = new GameObject("CursorBackpedalMovementProbe");
        try
        {
            var rigidbody = go.AddComponent<Rigidbody2D>();
            var root = go.AddComponent<CharacterRoot>();
            var bus = new CharacterDataBus
            {
                moveInput = Vector2.down,
                aimDirection = Vector2.up,
                activeFacingMode = CharacterFacingMode.Cursor,
                currentJumpState = EState.Jump_None,
                dashPressed = true
            };
            var state = new MoveState();
            state.Initialize(
                new MovementStateMachine(),
                bus,
                go.transform,
                rigidbody,
                root);

            state.FixedUpdate(0.02f);

            if (bus.currentMovementState != EState.Walk_Back)
                throw new Exception("Cursor-facing reverse input did not resolve to Walk_Back");
            if (!bus.isMovingBackward)
                throw new Exception("Cursor-facing reverse input did not publish backpedal runtime state");
            if (Mathf.Abs(rigidbody.velocity.magnitude - 2f) > 0.001f)
                throw new Exception("Walk_Back did not use the configured fallback backpedal speed");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static void VerifyMovementFacingIgnoresAimDirection()
    {
        var go = new GameObject("MovementFacingBackpedalProbe");
        try
        {
            var rigidbody = go.AddComponent<Rigidbody2D>();
            var root = go.AddComponent<CharacterRoot>();
            var bus = new CharacterDataBus
            {
                moveInput = Vector2.left,
                aimDirection = Vector2.right,
                currentFacing = Facing.Left,
                activeFacingMode = CharacterFacingMode.Movement,
                currentJumpState = EState.Jump_None
            };
            var state = new MoveState();
            state.Initialize(
                new MovementStateMachine(),
                bus,
                go.transform,
                rigidbody,
                root);

            state.FixedUpdate(0.02f);

            if (bus.currentMovementState != EState.Jog)
                throw new Exception("Movement-facing locomotion was incorrectly classified from aim direction");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static void VerifyIdleClearsBackpedalRuntimeState()
    {
        var go = new GameObject("IdleBackpedalResetProbe");
        try
        {
            var rigidbody = go.AddComponent<Rigidbody2D>();
            var root = go.AddComponent<CharacterRoot>();
            var bus = new CharacterDataBus
            {
                isMovingBackward = true,
                currentMovementState = EState.Walk_Back
            };
            var state = new IdleState();
            state.Initialize(
                new MovementStateMachine(),
                bus,
                go.transform,
                rigidbody,
                root);

            state.Enter();

            if (bus.isMovingBackward)
                throw new Exception("Entering Idle retained stale backpedal runtime state");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
#endif
