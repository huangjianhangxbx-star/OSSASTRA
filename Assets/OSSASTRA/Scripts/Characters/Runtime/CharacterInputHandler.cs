/*
 * 脚本：CharacterInputHandler
 * 功能：从Inspector配置的InputAction采样单角色输入快照。
 * 边界：只采输入；不接受动作、不驱动物理或写HP；每实例管理Action生命周期。
 * 协作：选择器经接口读取；死亡交接额外等待移动中立，普通Tab仍保留原移动语义。
 */
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ossastra.Characters
{
    public struct CharacterInputFrame
    {
        public Vector2 move, pointer, aimDirection;
        public bool weaponModePressed,attackHeld, dodgePressed, offhandHeld, offhandPressed, hasPointer, hasDirectionalAim, attackPointerBlocked;
        public int debugCommand;
        public bool skillOnePressed,skillTwoPressed,skillThreePressed,skillOneHeld,skillTwoHeld,skillThreeHeld;
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
        [FormerlySerializedAs("attackAction")] public InputAction rightHandAction = new InputAction("RightHandAction", InputActionType.Button);
        public InputAction attackAction { get=>rightHandAction; set=>rightHandAction=value; }
        [Header("可选本体闪避 / 只在新验证场绑定")]
        public InputAction dodgeAction = new InputAction("Dodge", InputActionType.Button);
        [Header("副手操作输入；具体按下/持续/长按行为由当前副手装备SO绑定决定，例如盾防、盾击、射击或投掷。")]
        [FormerlySerializedAs("offhandAction")] public InputAction leftHandAction = new InputAction("LeftHandAction", InputActionType.Button);
        public InputAction offhandAction { get=>leftHandAction; set=>leftHandAction=value; }
        [Tooltip("非空时以共享SO为绑定来源；每次进入游戏克隆，修改下方本地Action不会覆盖SO。")] public PlayerInputLayoutSO inputLayout;
        [Min(0),Tooltip("双扳机组合识别宽限秒数，避免先按下的一侧立即施放。0为无等待。")] public float triggerChordGraceSeconds=.08f;
        private double triggerStarted=-1;
        private bool triggerChordLatched;
        public InputAction weaponModeAction=new InputAction("WeaponMode",InputActionType.Button);
        private void Awake(){if(inputLayout!=null)inputLayout.Apply(this);}
        public InputAction skillOneAction = new InputAction("SkillOne",InputActionType.Button);
        public InputAction skillTwoAction = new InputAction("SkillTwo",InputActionType.Button);
        public InputAction skillThreeAction = new InputAction("SkillThree",InputActionType.Button);
        [Header("开发检查入口")]
        public InputAction debugValidAction = new InputAction("DebugValid", InputActionType.Button);
        public InputAction debugBlockedAction = new InputAction("DebugBlocked", InputActionType.Button);
        public InputAction debugResetAction = new InputAction("DebugReset", InputActionType.Button);
        public InputAction debugInspectAction = new InputAction("DebugInspect", InputActionType.Button);

        private readonly Dictionary<object, Rect> pointerRegions = new Dictionary<object, Rect>();
        private readonly Dictionary<object,bool> inventoryCaptures=new Dictionary<object,bool>();
        public bool InventoryCaptured=>inventoryCaptures.Count>0;
        /// <summary>开包只捕获战斗；数值输入再捕获移动，不撤销Current或暂停已接受动作。</summary>
        public void SetInventoryCapture(object owner,bool captured,bool captureMovement=false)
        {
            if(owner==null)return;
            var prior=InventoryCaptured;if(captured)inventoryCaptures[owner]=captureMovement;else inventoryCaptures.Remove(owner);
            if(prior!=InventoryCaptured)ResetPlayerInput();
        }
        private ICharacterPlayerControlSource playerControl;
        private bool managedPlayerInput, attackArmed = true;
        private bool movementArmed=true;
        private bool dodgeArmed;
        private bool offhandArmed;

        // 控制身份与设备采样许可分开：UI捕获不能暂停本体已接受副手/CD。
        public bool HasPlayerControl => !managedPlayerInput || (ControlSourceAlive && playerControl.IsPlayerControlOwner(this));
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
        { CurrentFrame=default; triggerStarted=-1; triggerChordLatched=false; attackArmed=false; dodgeArmed=false; offhandArmed=false; if (requireMovementRelease) movementArmed=false; }
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
            yield return dodgeAction; yield return weaponModeAction;
            yield return offhandAction;
            yield return skillOneAction; yield return skillTwoAction; yield return skillThreeAction;
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
            if(InventoryCaptured)
            {
                if(inventoryCaptures.Values.Any(value=>value))frame.move=Vector2.zero;
                CurrentFrame=frame;return frame;
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
            frame.dodgePressed=dodgeHeld&&dodgeArmed&&Pressed(dodgeAction)&&!GamepadInputPolicy.Modified(dodgeAction);
            if(frame.dodgePressed)dodgeArmed=false;
            var offhandHeld=offhandAction!=null&&offhandAction.enabled&&offhandAction.IsPressed();
            if(!offhandHeld)offhandArmed=true;
            frame.offhandHeld=offhandHeld&&offhandArmed;
            frame.offhandPressed=frame.offhandHeld&&Pressed(offhandAction);
            if(offhandAction!=null&&offhandAction.activeControl!=null&&offhandAction.activeControl.device is Pointer&&!frame.hasPointer)
            {frame.offhandHeld=false;frame.offhandPressed=false;}
            if (attackAction != null && attackAction.activeControl != null && attackAction.activeControl.device is Pointer &&
                !frame.hasPointer) frame.attackPointerBlocked = true;
            frame.skillOnePressed=Pressed(skillOneAction); frame.skillTwoPressed=Pressed(skillTwoAction);
            frame.skillThreePressed=Pressed(skillThreeAction);
            frame.skillOneHeld=skillOneAction!=null&&skillOneAction.enabled&&skillOneAction.IsPressed();
            frame.skillTwoHeld=skillTwoAction!=null&&skillTwoAction.enabled&&skillTwoAction.IsPressed();
            frame.skillThreeHeld=skillThreeAction!=null&&skillThreeAction.enabled&&skillThreeAction.IsPressed();
            frame.weaponModePressed=Pressed(weaponModeAction);
            frame.debugCommand = Pressed(debugValidAction) ? 1 : Pressed(debugBlockedAction) ? 2 :
                Pressed(debugResetAction) ? 3 : Pressed(debugInspectAction) ? 4 : 0;
            var triggerPad=rightHandAction.activeControl?.device as Gamepad??leftHandAction.activeControl?.device as Gamepad;
            if(triggerPad!=null)
            {
                bool any=triggerPad.leftTrigger.isPressed||triggerPad.rightTrigger.isPressed;
                if(!any){triggerStarted=-1;triggerChordLatched=false;}
                else if(triggerStarted<0)triggerStarted=Time.realtimeSinceStartupAsDouble;
                if(GamepadInputPolicy.WeaponChord(rightHandAction)||GamepadInputPolicy.WeaponChord(leftHandAction))triggerChordLatched=true;
                if(triggerChordLatched||any&&Time.realtimeSinceStartupAsDouble-triggerStarted<triggerChordGraceSeconds){frame.attackHeld=false;frame.offhandHeld=false;frame.offhandPressed=false;}
                else if(frame.offhandHeld)frame.offhandPressed=offhandAction.WasPressedThisFrame()||(!CurrentFrame.offhandHeld);
            }
            CurrentFrame = frame;
            return frame;
        }

        private static bool Pressed(InputAction action) => action != null && action.enabled && action.WasPressedThisFrame();
    }
}




