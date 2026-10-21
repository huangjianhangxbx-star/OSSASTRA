/*
 * 脚本：CharacterInputHandler
 * 功能：从Inspector配置的InputAction采样单角色输入快照。
 * 边界：只采输入；不接受动作、不驱动物理或写HP；每实例管理Action生命周期。
 * 协作：选择器经接口读取；死亡交接额外等待移动中立，普通Tab仍保留原移动语义。
 */
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ossastra.Characters
{
    public struct CharacterInputFrame
    {
        public Vector2 move, pointer, aimDirection;
        public bool attackHeld, dodgePressed, cancel, pause, hasPointer, hasDirectionalAim, attackPointerBlocked;
        public int debugCommand;
    }

    // Each character owns its serialized actions. Input does not accept attacks or write gameplay state.
    [DisallowMultipleComponent]
    public sealed class CharacterInputHandler : MonoBehaviour, ICharacterInputSource, ICharacterInputAvailability
    {
        [Header("移动 / VELGITA movementAction")]
        public InputAction movementAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
        [Header("鼠标位置 / VELGITA mouseAction")]
        public InputAction mouseAction = new InputAction("Pointer", InputActionType.Value, expectedControlType: "Vector2");
        [Header("手柄瞄准方向 / VELGITA gamePad_MouseAction")]
        public InputAction gamepadAimAction = new InputAction("GamepadAim", InputActionType.Value, expectedControlType: "Vector2");
        [Header("当前普攻 / VELGITA HoundWeaponAttack_Action")]
        public InputAction attackAction = new InputAction("Attack", InputActionType.Button);
        [Header("可选本体闪避 / 只在新验证场绑定")]
        public InputAction dodgeAction = new InputAction("Dodge", InputActionType.Button);
        [Header("取消与暂停")]
        public InputAction cancelAction = new InputAction("Cancel", InputActionType.Button);
        public InputAction pauseAction = new InputAction("Pause", InputActionType.Button);
        [Header("开发检查入口")]
        public InputAction debugValidAction = new InputAction("DebugValid", InputActionType.Button);
        public InputAction debugBlockedAction = new InputAction("DebugBlocked", InputActionType.Button);
        public InputAction debugResetAction = new InputAction("DebugReset", InputActionType.Button);
        public InputAction debugInspectAction = new InputAction("DebugInspect", InputActionType.Button);

        private readonly Dictionary<object, Rect> pointerRegions = new Dictionary<object, Rect>();
        private ICharacterPlayerControlSource playerControl;
        private bool managedPlayerInput, attackArmed = true;
        private bool movementArmed=true;
        private bool dodgeArmed;

        public bool HasPlayerInput => !managedPlayerInput || (ControlSourceAlive && playerControl.IsPlayerInputAllowed(this));
        private bool ControlSourceAlive => playerControl != null &&
            (!(playerControl is Object unityObject) || unityObject != null);

        public bool CanBindPlayerControl(ICharacterPlayerControlSource source)
            => source != null && (!managedPlayerInput || !ControlSourceAlive || ReferenceEquals(playerControl, source));

        /// <summary>绑定唯一场景权威；拒绝另一存活协调者抢占同一输入。</summary>
        public bool BindPlayerControl(ICharacterPlayerControlSource source)
        {
            if (!CanBindPlayerControl(source)) return false;
            playerControl = source;
            managedPlayerInput = true;
            ResetPlayerInput();
            return true;
        }

        /// <summary>移交时清掉旧快照；不假装一次松键，必须真正观察释放后才重武装攻击。</summary>
        public void ResetPlayerInput(bool requireMovementRelease=false)
        { CurrentFrame=default; attackArmed=false; dodgeArmed=false; if (requireMovementRelease) movementArmed=false; }
        public void RegisterPointerRegion(object owner, Rect region) { pointerRegions[owner] = region; }
        public void RemovePointerRegion(object owner) { pointerRegions.Remove(owner); }

        private bool IsPointerBlocked(Vector2 pointer)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return true;
            var guiPoint = new Vector2(pointer.x, Screen.height - pointer.y);
            foreach (var region in pointerRegions.Values) if (region.Contains(guiPoint)) return true;
            return false;
        }

        public CharacterInputFrame CurrentFrame { get; private set; }

        public bool IsConfigured => movementAction != null && movementAction.bindings.Count > 0 &&
            attackAction != null && attackAction.bindings.Count > 0 &&
            ((mouseAction != null && mouseAction.bindings.Count > 0) ||
             (gamepadAimAction != null && gamepadAimAction.bindings.Count > 0));

        private IEnumerable<InputAction> Actions()
        {
            // 同步枚举仅统一启用/禁用/释放清单；不是Unity协程，没有跨帧等待。
            yield return movementAction;
            yield return mouseAction;
            yield return gamepadAimAction;
            yield return attackAction;
            yield return dodgeAction;
            yield return cancelAction;
            yield return pauseAction;
            yield return debugValidAction;
            yield return debugBlockedAction;
            yield return debugResetAction;
            yield return debugInspectAction;
        }

        private void OnEnable() { foreach (var action in Actions()) if (action != null) action.Enable(); }
        private void OnDisable() { ResetPlayerInput(); foreach (var action in Actions()) if (action != null) action.Disable(); }
        private void OnDestroy() { foreach (var action in Actions()) if (action != null) action.Dispose(); }

        public CharacterInputFrame Read()
        {
            var frame = new CharacterInputFrame();
            // 非实控在读取设备前返回空帧；Root仍推进自己已接受的动作，不会偷读另一角色输入。
            if (!HasPlayerInput || !isActiveAndEnabled || movementAction == null || !movementAction.enabled)
            { CurrentFrame = frame; return frame; }
            frame.move = movementAction.ReadValue<Vector2>();
            if (!movementArmed)
            {
                // 死亡交接不能把旧WASD/摇杆位移转发给幸存者；必须真实回中才重新采移动。
                if (frame.move.sqrMagnitude<.0001f) movementArmed=true;
                frame.move=Vector2.zero;
            }
            if (mouseAction != null && mouseAction.enabled)
            {
                frame.pointer = mouseAction.ReadValue<Vector2>();
                frame.hasPointer = mouseAction.controls.Count > 0 && frame.pointer.x >= 0 && frame.pointer.x <= Screen.width &&
                    frame.pointer.y >= 0 && frame.pointer.y <= Screen.height && !IsPointerBlocked(frame.pointer);
            }
            if (gamepadAimAction != null && gamepadAimAction.enabled)
            {
                frame.aimDirection = gamepadAimAction.ReadValue<Vector2>();
                frame.hasDirectionalAim = frame.aimDirection.sqrMagnitude > .01f;
            }
            var held = attackAction != null && attackAction.enabled && attackAction.IsPressed();
            if (!held) attackArmed = true;
            frame.attackHeld = held && attackArmed;
            var dodgeHeld=dodgeAction!=null&&dodgeAction.enabled&&dodgeAction.IsPressed();
            if(!dodgeHeld)dodgeArmed=true;
            frame.dodgePressed=dodgeHeld&&dodgeArmed&&Pressed(dodgeAction);
            if(frame.dodgePressed)dodgeArmed=false;
            if (attackAction != null && attackAction.activeControl != null && attackAction.activeControl.device is Pointer &&
                !frame.hasPointer) frame.attackPointerBlocked = true;
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
