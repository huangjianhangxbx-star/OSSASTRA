using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ossastra.Characters
{
    public struct CharacterInputFrame
    {
        public Vector2 move, pointer, aimDirection;
        public bool attackHeld, cancel, pause, hasPointer, hasDirectionalAim;
        public int debugCommand;
    }

    // Each character owns its serialized actions. Input does not accept attacks or write gameplay state.
    [DisallowMultipleComponent]
    public sealed class CharacterInputHandler : MonoBehaviour
    {
        [Header("移动 / VELGITA movementAction")]
        public InputAction movementAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
        [Header("鼠标位置 / VELGITA mouseAction")]
        public InputAction mouseAction = new InputAction("Pointer", InputActionType.Value, expectedControlType: "Vector2");
        [Header("手柄瞄准方向 / VELGITA gamePad_MouseAction")]
        public InputAction gamepadAimAction = new InputAction("GamepadAim", InputActionType.Value, expectedControlType: "Vector2");
        [Header("当前普攻 / VELGITA HoundWeaponAttack_Action")]
        public InputAction attackAction = new InputAction("Attack", InputActionType.Button);
        [Header("取消与暂停")]
        public InputAction cancelAction = new InputAction("Cancel", InputActionType.Button);
        public InputAction pauseAction = new InputAction("Pause", InputActionType.Button);
        [Header("开发检查入口")]
        public InputAction debugValidAction = new InputAction("DebugValid", InputActionType.Button);
        public InputAction debugBlockedAction = new InputAction("DebugBlocked", InputActionType.Button);
        public InputAction debugResetAction = new InputAction("DebugReset", InputActionType.Button);
        public InputAction debugInspectAction = new InputAction("DebugInspect", InputActionType.Button);

        public CharacterInputFrame CurrentFrame { get; private set; }

        public bool IsConfigured => movementAction != null && movementAction.bindings.Count > 0 &&
            attackAction != null && attackAction.bindings.Count > 0 &&
            ((mouseAction != null && mouseAction.bindings.Count > 0) ||
             (gamepadAimAction != null && gamepadAimAction.bindings.Count > 0));

        private IEnumerable<InputAction> Actions()
        {
            yield return movementAction;
            yield return mouseAction;
            yield return gamepadAimAction;
            yield return attackAction;
            yield return cancelAction;
            yield return pauseAction;
            yield return debugValidAction;
            yield return debugBlockedAction;
            yield return debugResetAction;
            yield return debugInspectAction;
        }

        private void OnEnable() { foreach (var action in Actions()) if (action != null) action.Enable(); }
        private void OnDisable() { CurrentFrame = default; foreach (var action in Actions()) if (action != null) action.Disable(); }
        private void OnDestroy() { foreach (var action in Actions()) if (action != null) action.Dispose(); }

        public CharacterInputFrame Read()
        {
            var frame = new CharacterInputFrame();
            if (!isActiveAndEnabled || movementAction == null || !movementAction.enabled)
            { CurrentFrame = frame; return frame; }
            frame.move = movementAction.ReadValue<Vector2>();
            if (mouseAction != null && mouseAction.enabled)
            {
                frame.pointer = mouseAction.ReadValue<Vector2>();
                frame.hasPointer = mouseAction.controls.Count > 0;
            }
            if (gamepadAimAction != null && gamepadAimAction.enabled)
            {
                frame.aimDirection = gamepadAimAction.ReadValue<Vector2>();
                frame.hasDirectionalAim = frame.aimDirection.sqrMagnitude > .01f;
            }
            frame.attackHeld = attackAction != null && attackAction.enabled && attackAction.IsPressed();
            frame.cancel = Pressed(cancelAction);
            frame.pause = Pressed(pauseAction);
            frame.debugCommand = Pressed(debugValidAction) ? 1 : Pressed(debugBlockedAction) ? 2 :
                Pressed(debugResetAction) ? 3 : Pressed(debugInspectAction) ? 4 : 0;
            CurrentFrame = frame;
            return frame;
        }

        private static bool Pressed(InputAction action) => action != null && action.enabled && action.WasPressedThisFrame();
    }
}