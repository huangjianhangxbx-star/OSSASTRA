#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

public static class TopDownMovementProbe
{
    public static string Run()
    {
        Type modeType = Type.GetType("CharacterMovementMode, Assembly-CSharp");
        if (modeType == null)
            throw new Exception("CharacterMovementMode is missing");

        PropertyInfo modeProperty = typeof(CharacterRoot).GetProperty(
            "movementMode",
            BindingFlags.Public | BindingFlags.Instance);
        if (modeProperty == null)
            throw new Exception("CharacterRoot.movementMode is missing");

        var go = new GameObject("TopDownMovementProbe");
        try
        {
            var rigidbody = go.AddComponent<Rigidbody2D>();
            rigidbody.gravityScale = 0f;
            var root = go.AddComponent<CharacterRoot>();
            object mode = modeProperty.GetValue(root);
            if (!string.Equals(mode.ToString(), "TopDown", StringComparison.Ordinal))
                throw new Exception("TopDown is not the default movement mode");

            var dataBus = new CharacterDataBus
            {
                moveInput = Vector2.up,
                isGrounded = false,
                currentJumpState = EState.Jump_None
            };
            var state = new MoveState();
            state.Initialize(
                new MovementStateMachine(),
                dataBus,
                go.transform,
                rigidbody,
                root);

            state.FixedUpdate(0.02f);
            if (rigidbody.velocity.y <= 0.01f || Mathf.Abs(rigidbody.velocity.x) > 0.001f)
                throw new Exception("TopDown vertical input did not produce planar Y velocity");

            dataBus.moveInput = new Vector2(1f, 1f);
            rigidbody.velocity = Vector2.zero;
            state.FixedUpdate(0.02f);
            float expectedSpeed = 4f;
            if (Mathf.Abs(rigidbody.velocity.magnitude - expectedSpeed) > 0.001f)
                throw new Exception("TopDown diagonal speed is not normalized");

            dataBus.moveInput = Vector2.zero;
            rigidbody.velocity = new Vector2(2f, 3f);
            state.FixedUpdate(0.02f);
            if (rigidbody.velocity.sqrMagnitude > 0.0001f)
                throw new Exception("TopDown stop preserved velocity on one or more planar axes");

            rigidbody.velocity = new Vector2(2f, 3f);
            var machine = new MovementStateMachine();
            SetPrivate(machine, "dataBus", dataBus);
            SetPrivate(machine, "rigidbody", rigidbody);
            SetPrivate(machine, "characterRoot", root);
            MethodInfo execute = typeof(MovementStateMachine).GetMethod(
                "MovementExcute",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (execute == null)
                throw new Exception("MovementStateMachine physics seam is missing");

            execute.Invoke(machine, new object[] { 0.5f });
            if ((rigidbody.velocity - new Vector2(2f, 3f)).sqrMagnitude > 0.0001f)
                throw new Exception("TopDown normal locomotion still applies side-scroller gravity");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }

        return "PASS TopDown planar movement";
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
