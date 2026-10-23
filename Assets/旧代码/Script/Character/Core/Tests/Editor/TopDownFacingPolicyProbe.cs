#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

public static class TopDownFacingPolicyProbe
{
    public static string Run()
    {
        VerifyCursorFacingDoesNotRequireAim();
        VerifyMovementFacingStrategyIsPreserved();
        VerifyTopDownDefaultsToCursorMode();
        VerifyCommittedActionBlocksOrdinaryMovement();
        return "PASS topdown facing and movement control";
    }

    private static void VerifyCursorFacingDoesNotRequireAim()
    {
        var visual = new GameObject("TopDownFacingVisual");
        try
        {
            var bus = new CharacterDataBus
            {
                characterPosition = Vector3.zero,
                mousePositionOnScreen = Vector3.right * 5f,
                moveInput = Vector2.left,
                facingDirection = Vector3.left,
                aimHeld = false,
                houndAttackHeld = false
            };

            var controller = new MouseOrientationController();
            controller.Initialize(bus, visual.transform);
            controller.OrientMethod();

            if (bus.requestedFacingDirection.x <= 0f)
                throw new Exception("Cursor mode still yielded to movement input without aim");
            if (bus.requestedFacingSource != CharacterDataBus.RequestedFacingSource.Aim)
                throw new Exception("Cursor mode did not emit an immediate facing request");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(visual);
        }
    }

    private static void VerifyMovementFacingStrategyIsPreserved()
    {
        var visual = new GameObject("MovementFacingVisual");
        try
        {
            var bus = new CharacterDataBus
            {
                moveInput = Vector2.left,
                facingDirection = Vector3.right,
                mousePositionOnScreen = Vector3.right * 5f
            };
            var controller = new MovementOrientationController();
            controller.Initialize(bus, visual.transform);
            controller.OrientMethod();

            if (bus.requestedFacingDirection.x >= 0f ||
                bus.requestedFacingSource != CharacterDataBus.RequestedFacingSource.Move)
            {
                throw new Exception("Movement facing strategy is no longer selectable");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(visual);
        }
    }

    private static void VerifyTopDownDefaultsToCursorMode()
    {
        FieldInfo field = typeof(OrientationSystem).GetField(
            "defaultFacingMode",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
            throw new Exception("OrientationSystem has no selectable default facing mode");

        var go = new GameObject("OrientationDefaultProbe");
        try
        {
            var orientation = go.AddComponent<OrientationSystem>();
            object value = field.GetValue(orientation);
            if (!Equals(CharacterFacingMode.Cursor, value))
                throw new Exception("TopDown facing does not default to Cursor");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static void VerifyCommittedActionBlocksOrdinaryMovement()
    {
        var go = new GameObject("TopDownMovementControlProbe");
        try
        {
            var root = go.AddComponent<CharacterRoot>();
            var rigidbody = go.AddComponent<Rigidbody2D>();
            var bus = new CharacterDataBus
            {
                moveInput = Vector2.up,
                currentJumpState = EState.Jump_None,
                isAlive = true,
                canMove = true
            };
            var machine = new MovementStateMachine();
            var moveState = new MoveState();
            moveState.Initialize(machine, bus, go.transform, rigidbody, root);

            SetPrivate(machine, "dataBus", bus);
            SetPrivate(machine, "rigidbody", rigidbody);
            SetPrivate(machine, "characterRoot", root);
            SetPrivate(machine, "currentState", moveState);

            object actionOwner = new object();
            root.controlAuthority.SetOverride(
                actionOwner,
                CharacterControlOverride.CommittedAttack());

            MethodInfo execute = typeof(MovementStateMachine).GetMethod(
                "MovementExcute",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (execute == null)
                throw new Exception("Movement execution seam is missing");

            execute.Invoke(machine, new object[] { 0.02f });
            if (rigidbody.velocity.sqrMagnitude > 0.0001f)
                throw new Exception("Blocked Move still produced ordinary locomotion velocity");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static void SetPrivate(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
            throw new Exception($"Missing field: {fieldName}");
        field.SetValue(target, value);
    }
}
#endif
