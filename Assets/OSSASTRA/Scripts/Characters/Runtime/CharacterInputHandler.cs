/*
 * 脚本：CharacterInputHandler
 * 功能：从Inspector配置的InputAction采样单角色输入快照。
 * 边界：只采输入；不接受动作、不驱动物理或写HP；每实例管理Action生命周期。
 * 协作：选择器经接口读取；来源记忆归玩家会话，真实手势代际与背包访问代际分开。
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
        public PlayerInputSource moveSource, aimSource;
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
        public PlayerInputLayoutSO inputLayout;
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
            var priorMovement=inventoryCaptures.Values.Any(value=>value);
            if(captured)inventoryCaptures[owner]=captureMovement;else inventoryCaptures.Remove(owner);
            if(priorMovement!=inventoryCaptures.Values.Any(value=>value))ResetPlayerInput(true);
        }
        private ICharacterPlayerControlSource playerControl;
        private bool managedPlayerInput;
        private readonly DeviceActionSampler sampler = new DeviceActionSampler();
        private readonly PlayerInputSourceState localSources = new PlayerInputSourceState();
        [Header("混合输入 / 已处理Action轴后的意图门槛，不再次执行deadzone")]
        [SerializeField, Range(.01f, 1)] private float intentThreshold = .15f;
        [SerializeField, Range(.01f, 1)] private float directionChangeThreshold = .25f;
        public PlayerInputSourceState InputSources => (playerControl as IPlayerInputSourceSession)?.InputSources ?? localSources;
        // 手势只在真实重置时换代。背包访问revision不等于松键/换控，不能借此终止既有长按。
        public int GestureGeneration { get; private set; }
        private bool movementArmed=true;

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
        { CurrentFrame=default; GestureGeneration++; sampler.Suppress(); if (requireMovementRelease) movementArmed=false; }
        public void RegisterPointerRegion(object owner, Rect region) { pointerRegions[owner] = region; }
        public void RemovePointerRegion(object owner) { pointerRegions.Remove(owner); }

        private bool IsPointerBlocked(Vector2 pointer)
        {
            if (EventSystem.current != null)
            {
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pointer }, hits);
                if (hits.Any(hit => hit.module is UnityEngine.UI.GraphicRaycaster)) return true;
            }
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
        private IEnumerable<InputAction> SamplingActions()
            => inputLayout==null?Actions():Actions().Concat(inputLayout.ConfiguredActions().Where(a=>a!=inputLayout.move&&a!=inputLayout.pointer&&a!=inputLayout.aim));

        private void OnEnable() { foreach (var action in Actions()) if (action != null) action.Enable(); sampler.Synchronize(SamplingActions(),mouseAction,IsPointerBlocked); }
        private void OnDisable() { ResetPlayerInput(true); sampler.Dispose(); foreach (var action in Actions()) if (action != null) action.Disable(); }
        private void OnDestroy() { sampler.Dispose(); foreach (var action in Actions()) if (action != null) action.Dispose(); }

        public CharacterInputFrame Read()
        {
            if (!HasPlayerInput || !isActiveAndEnabled || movementAction == null || !movementAction.enabled)
            { CurrentFrame = default; return CurrentFrame; }
            sampler.Synchronize(SamplingActions(), mouseAction, IsPointerBlocked);
            var candidates = new List<DeviceInputCandidate>();
            bool held = false, dodge = false, offhand = false, offhandPressed = false;
            bool one = false, two = false, three = false, oneHeld = false, twoHeld = false, threeHeld = false, mode = false;
            int debug = 0;
            DeviceActionSampler.DeviceSample pointerDevice = null;
            foreach (var d in sampler.Devices)
            {
                var c = d.SampleAxes(movementAction, gamepadAimAction, intentThreshold, directionChangeThreshold);
                var events=EventSystem.current;
                var ui=events?.currentInputModule as UnityEngine.InputSystem.UI.InputSystemUIInputModule;
                var navigation=events!=null&&events.sendNavigationEvents&&events.currentSelectedGameObject!=null&&events.currentSelectedGameObject.activeInHierarchy&&
                    ui?.move?.action!=null&&ui.move.action.enabled&&ui.move.action.controls.Any(control=>d.Action(movementAction).controls.Contains(control));
                d.CaptureNavigation(navigation);
                var weaponChord = d.Device is Gamepad && d.Held(weaponModeAction);
                var right = d.Held(rightHandAction) && !weaponChord;
                var left = d.Held(leftHandAction) && !weaponChord;
                held |= right; offhand |= left;
                offhandPressed |= left && d.Pressed(leftHandAction);
                var evasion = d.Pressed(dodgeAction) && !d.ConsumedByComposite(dodgeAction);
                dodge |= evasion;
                one |= d.Pressed(skillOneAction); two |= d.Pressed(skillTwoAction); three |= d.Pressed(skillThreeAction);
                oneHeld |= d.Held(skillOneAction); twoHeld |= d.Held(skillTwoAction); threeHeld |= d.Held(skillThreeAction);
                mode |= d.Pressed(weaponModeAction);
                var battleActions = new[] { rightHandAction, leftHandAction, dodgeAction, skillOneAction, skillTwoAction, skillThreeAction };
                foreach (var action in battleActions)
                {
                    bool directional = d.Device is Gamepad || d.Device is Pointer && (action == rightHandAction || action == leftHandAction);
                    if (directional && d.Pressed(action) && !weaponChord && (action != dodgeAction || evasion))
                    { c.BattlePressed = true; c.BattleTime = System.Math.Max(c.BattleTime, d.Time(action)); }
                }
                if (d.Device is Pointer && d.Action(mouseAction)?.controls.Count > 0) pointerDevice = d;
                if (d.Pressed(debugValidAction)) debug = 1; else if (d.Pressed(debugBlockedAction)) debug = 2;
                else if (d.Pressed(debugResetAction)) debug = 3; else if (d.Pressed(debugInspectAction)) debug = 4;
                candidates.Add(c);
            }
            if (!movementArmed)
            {
                // 所有设备真实中立才恢复；另一设备旧保持不能绕过死亡/失焦/模态保护。
                if (candidates.All(c => !c.MoveActive && !c.AimActive)) movementArmed = true;
                foreach (var c in candidates) { c.MoveActive = c.MoveChanged = c.AimActive = c.AimChanged = false; c.Move = c.Aim = Vector2.zero; }
            }
            var captureMovement = inventoryCaptures.Values.Any(value => value);
            InputSources.Resolve(candidates, !captureMovement, !InventoryCaptured);
            pointerDevice=sampler.Devices.Where(d=>d.Device is Pointer&&d.Action(mouseAction)?.controls.Count>0)
                .OrderByDescending(d=>d.Device.deviceId==InputSources.AimDeviceId)
                .ThenByDescending(d=>d.Time(mouseAction)).FirstOrDefault();
            var frame = new CharacterInputFrame { move = InputSources.Move, aimSource = InputSources.AimSource,
                moveSource = InputSources.MoveDeviceId<0?PlayerInputSource.None:
                    candidates.Any(c => c.DeviceId == InputSources.MoveDeviceId && c.IsGamepad) ? PlayerInputSource.Gamepad : PlayerInputSource.KeyboardMouse,
                attackHeld = held, offhandHeld = offhand, offhandPressed = offhandPressed, dodgePressed = dodge,
                skillOnePressed = one, skillTwoPressed = two, skillThreePressed = three,
                skillOneHeld = oneHeld, skillTwoHeld = twoHeld, skillThreeHeld = threeHeld,
                weaponModePressed = mode, debugCommand = debug };
            if (pointerDevice != null)
            {
                frame.pointer = pointerDevice.ReadPointer();
                frame.hasPointer = frame.aimSource == PlayerInputSource.KeyboardMouse && frame.pointer.x >= 0 && frame.pointer.x <= Screen.width &&
                    frame.pointer.y >= 0 && frame.pointer.y <= Screen.height && !IsPointerBlocked(frame.pointer);
            }
            if (!frame.hasPointer)
            {
                frame.hasDirectionalAim = true;
                if (frame.aimSource == PlayerInputSource.KeyboardMouse)
                {
                    var previous = GetComponent<CharacterRoot>()?.DataBus?.AttackDirection ?? Vector3.right;
                    frame.aimDirection = new Vector2(previous.x, previous.z);
                }
                else frame.aimDirection = InputSources.LastAimDirection;
            }
            frame.attackPointerBlocked = frame.aimSource == PlayerInputSource.KeyboardMouse && !frame.hasPointer;
            if (InventoryCaptured)
            {
                // 捕获只阻止新申请；既有长按保持自己的设备手势，不凭空收到释放。
                frame.attackHeld = frame.dodgePressed = frame.offhandPressed = frame.weaponModePressed = false;
                frame.skillOnePressed = frame.skillTwoPressed = frame.skillThreePressed = false;
                frame.offhandHeld &= CurrentFrame.offhandHeld;
                frame.skillOneHeld &= CurrentFrame.skillOneHeld; frame.skillTwoHeld &= CurrentFrame.skillTwoHeld; frame.skillThreeHeld &= CurrentFrame.skillThreeHeld;
                frame.hasPointer = false;
                sampler.SuppressNewPresses();
            }
            CurrentFrame = frame; return frame;
        }
    }
}
