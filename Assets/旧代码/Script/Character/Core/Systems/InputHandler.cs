using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

public class InputHandler : MonoBehaviour,ICharacterSubsystem, ICharacterTickable,ICharacterLateTickable
{
    private bool _isInitialized;


    #region 输入系统字段声明
    [Header("移动动作")]
    public InputAction movementAction;//默认为Jog
    [Header("跳跃动作")]
    public InputAction jumpAction;//默认为Dush
    [Header("强制慢走动作")]
    public InputAction forceSlowWalkAction;//强制慢走，只在键鼠下按住时生效
    [Header("蹲下动作")]
    public InputAction crouchAction;//按住蹲下
    [Header("装弹/卸弹动作")]
    public InputAction equipOrReloadAction;//短按装弹/卸弹，长按切换
    [Header("冲刺/回避动作")]
    public InputAction dushOrAvoidAction;//默认为Dush
    [Header("回避动作")]
    public InputAction avoidAction;//回避
    [Header("瞄准设备_鼠标")]
    public InputAction mouseAction;//鼠标，手柄右摇杆控制
    [Header("瞄准设备_手柄")]
    public InputAction gamePad_MouseAction;//鼠标，手柄右摇杆控制
    [Header("猎人武器攻击动作")]
    public InputAction HunterWeaponAttack_Action;//攻击按钮，手柄直接扳机

    [Header("猎犬武器攻击动作")]
    public InputAction HoundWeaponAttack_Action;//攻击按钮，手柄直接扳机

    [Header("瞄准动作")]
    public InputAction AimAction;//鼠标右键，手柄LT键

    [Header("武器槽位切换")]
    [Tooltip("手柄选择武器槽位 1")]
    public InputAction weaponSlot1Action;

    [Tooltip("手柄选择武器槽位 2")]
    public InputAction weaponSlot2Action;

    [Tooltip("手柄选择武器槽位 3")]
    public InputAction weaponSlot3Action;

    [Tooltip("手机端循环切换下一个武器")]
    public InputAction weaponCycleNextAction;

    [Header("拾取动作")]
    [Tooltip("按E拾取附近物品")]
    public InputAction pickupAction;

    [Header("背包开关")]
    [Tooltip("按Tab打开/关闭背包")]
    public InputAction packageToggleAction;


    #endregion
    //================== 输入处理配置 =================
    [Header("是否使用新的瞄准系统")]
    public bool useNewCursorSystem = true;

    // ==== Gameplay 输入总开关 ====
    // 当背包 UI 打开时禁用，关闭时恢复。packageToggleAction 永不参与（保证能再次按 Tab 关闭）。
    private bool _isGameplayInputEnabled = true;
    private readonly List<InputAction> _gameplayActions = new List<InputAction>();


    private Vector2 currentMoveInput { get; set; }

    private Vector2 virtualMousePosition;
    private Vector2 lastMousePos { get; set; }
    private Vector3 MousePosDelta { get; set; }
    public Vector2 currentMousePos { get; set; }
    public Vector2 gamepadInput { get; set; }
    // 两种输入设备同步位置来源
    public Vector2 GamepadScreenPosition;    // 手柄在屏幕位置
    public Vector2 ActiveScreenPosition;     // 当前有效屏幕位置

    private bool dashOrAvoidPerformed;

    private bool equipOrReloadLongPerformed;
    private CharacterRoot characterRoot { get; set; }
    private CharacterDataBus dataBus;
    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized)
            return;
        characterRoot = root;
        dataBus = root.characterDataBus;
        this.dataBus = characterRoot.characterDataBus;
        _isInitialized = true;
    }
    void Start()
    {
        //===========输入设备初始化================
        // 初始化虚拟鼠标位置为屏幕中心
        virtualMousePosition = new Vector2(Screen.width / 2f, Screen.height / 2f);
        // 可选择是否同步真实鼠标（很多游戏不同步）
        //Mouse.current.WarpCursorPosition(virtualMousePosition);
        //===========鼠标/手柄设备初始化================
        #region 动作绑定初始化
        movementAction.Enable();//移动只需要enable即可，因为Input_Movement需要在update中每一帧调用
        mouseAction.Enable();//只需要enable即可
        gamePad_MouseAction.Enable();//只需要enable即可
        //==============强制慢走动作================
        forceSlowWalkAction.Enable();
        forceSlowWalkAction.started += (context) =>
        {
            Input_ForceSlowWalk(context);
        };
        forceSlowWalkAction.performed += (context) =>
        {
            Input_ForceSlowWalk(context);
        };
        forceSlowWalkAction.canceled += (context) =>
        {
            Input_ForceSlowWalk(context);
        };
        //=========蹲下===========
        crouchAction.Enable();
        crouchAction.started += (context) =>
        {
            Input_Crouch(context);
        };
        crouchAction.performed += (context) =>
        {
            Input_Crouch(context);
        };
        crouchAction.canceled += (context) =>
        {
            Input_Crouch(context);
        };
        //=========冲刺/回避键===========
        dushOrAvoidAction.Enable();
        dushOrAvoidAction.started += (context) =>
        {
            Input_DashOrAvoid(context);
        };
        dushOrAvoidAction.performed += (context) =>
        {
            Input_DashOrAvoid(context);
        };
        dushOrAvoidAction.canceled += (context) =>
        {
            Input_DashOrAvoid(context);
        };
        //=========瞄准键===========
        AimAction.Enable();
        AimAction.started += (context) =>
        {
            Input_Aim(context);
        };
        AimAction.performed += (context) =>
        {
            Input_Aim(context);
        };
        AimAction.canceled += (context) =>
        {
            Input_Aim(context);
        };
        //=========装弹/卸弹键===========
        equipOrReloadAction.Enable();
        equipOrReloadAction.started += (context) =>
        {
            Input_EquipOrReload(context);
        };
        equipOrReloadAction.performed += (context) =>
        {
            Input_EquipOrReload(context);
        };
        equipOrReloadAction.canceled += (context) =>
        {
            Input_EquipOrReload(context);
        };
        //=========跳跃键===========
        jumpAction.Enable();
        jumpAction.started += (context) =>
        {
            Input_Jump(context);
        };
        // jumpAction.performed 已移除，避免 started+performed 双触发
        jumpAction.canceled += (context) =>
        {
            Input_Jump(context);
        };
        //=========攻击键===========
        HunterWeaponAttack_Action.Enable();
        HunterWeaponAttack_Action.started += HunterWeapon_Attack;
        HunterWeaponAttack_Action.performed += HunterWeapon_Attack;
        HunterWeaponAttack_Action.canceled += HunterWeapon_Attack;

        HoundWeaponAttack_Action.Enable();
        HoundWeaponAttack_Action.started += HoundWeapon_Attack;
        HoundWeaponAttack_Action.performed += HoundWeapon_Attack;
        HoundWeaponAttack_Action.canceled += HoundWeapon_Attack;
        //===========武器切换键===========
        weaponSlot1Action.Enable();
        weaponSlot1Action.started += Input_WeaponSlot1;
        weaponSlot2Action.Enable();
        weaponSlot2Action.started += Input_WeaponSlot2;
        weaponSlot3Action.Enable();
        weaponSlot3Action.started += Input_WeaponSlot3;
        weaponCycleNextAction.Enable();
        weaponCycleNextAction.started += Input_WeaponCycleNext;

        //=========拾取键===========
        if (pickupAction == null)
        {
            pickupAction = new InputAction("Pickup", binding: "<Keyboard>/e");
        }
        pickupAction.Enable();
        pickupAction.started += Input_Pickup;

        //=========背包开关===========
        // ANKALUTE owns Tab in DRPGPartyBackpackInputRouter. Keep the legacy
        // PackageSystem action unbound so two independent inventories cannot
        // open from the same key.

        #endregion

        // 收集所有 Gameplay 输入动作（packageToggleAction 除外）
        RegisterGameplayAction(movementAction);
        RegisterGameplayAction(jumpAction);
        RegisterGameplayAction(forceSlowWalkAction);
        RegisterGameplayAction(crouchAction);
        RegisterGameplayAction(equipOrReloadAction);
        RegisterGameplayAction(dushOrAvoidAction);
        RegisterGameplayAction(avoidAction);
        RegisterGameplayAction(mouseAction);
        RegisterGameplayAction(gamePad_MouseAction);
        RegisterGameplayAction(HunterWeaponAttack_Action);
        RegisterGameplayAction(HoundWeaponAttack_Action);
        RegisterGameplayAction(AimAction);
        RegisterGameplayAction(weaponSlot1Action);
        RegisterGameplayAction(weaponSlot2Action);
        RegisterGameplayAction(weaponSlot3Action);
        RegisterGameplayAction(weaponCycleNextAction);
        RegisterGameplayAction(pickupAction);
    }

    private void RegisterGameplayAction(InputAction action)
    {
        if (action != null && !_gameplayActions.Contains(action))
            _gameplayActions.Add(action);
    }

    /// <summary>
    ///游戏输入主开关（当背包打开/关闭时由CharacterUIInputRouter调用）。

    ///启用/禁用移动、跳跃、蹲伏、冲刺、瞄准、攻击、重新装弹、武器开关、拾取和瞄准输入。

    ///永远不要禁用packageToggleAction，这样玩家就可以随时按Tab键关闭UI。

    ///禁用时，还会清除保持/粘滞状态，这样当UI打开时，就不会有任何东西留下来。
    /// </summary>
    public void SetGameplayInputEnabled(bool enabled)
    {
        if (_isGameplayInputEnabled == enabled)
        {
            if (enabled && dataBus != null && dataBus.moveInput != Vector2.zero)
                dataBus.moveInput = Vector2.zero;
            return;
        }

        _isGameplayInputEnabled = enabled;

        foreach (var action in _gameplayActions)
        {
            if (action == null) continue;
            if (enabled)
                action.Enable();
            else
                action.Disable();
        }

        ClearGameplayStickyStates();
        if (dataBus == null) return;
        dataBus.moveInput = Vector2.zero;
    }

    public bool IsGameplayInputEnabled => _isGameplayInputEnabled;

    private void ClearGameplayStickyStates()
    {
        currentMoveInput = Vector2.zero;
        if (dataBus == null) return;

        dataBus.aimHeld = false;
        dataBus.dashPressed = false;
        dataBus.jumpHeld = false;
        dataBus.jumpPressed = false;
        dataBus.jumpPressedThisFrame = false;
        dataBus.isForceWalkSlow = false;
        dataBus.isCrouch = false;
        dataBus.moveInput = Vector2.zero;
        dataBus.mouseMoving = false;

        if (characterRoot != null && characterRoot.characterAttackController != null)
        {
            characterRoot.characterAttackController.SetHunterAttackHeld(false);
            characterRoot.characterAttackController.SetHoundAttackHeld(false);
        }
    }


    private void OnDestroy()
    {
        if (HunterWeaponAttack_Action != null)
        {
            HunterWeaponAttack_Action.started -= HunterWeapon_Attack;
            HunterWeaponAttack_Action.performed -= HunterWeapon_Attack;
            HunterWeaponAttack_Action.canceled -= HunterWeapon_Attack;
        }

        if (HoundWeaponAttack_Action != null)
        {
            HoundWeaponAttack_Action.started -= HoundWeapon_Attack;
            HoundWeaponAttack_Action.performed -= HoundWeapon_Attack;
            HoundWeaponAttack_Action.canceled -= HoundWeapon_Attack;
        }

        #region 武器切换解绑
        if (weaponSlot1Action != null)
        {
            weaponSlot1Action.started -=
                Input_WeaponSlot1;

            weaponSlot1Action.Disable();
        }

        if (weaponSlot2Action != null)
        {
            weaponSlot2Action.started -=
                Input_WeaponSlot2;

            weaponSlot2Action.Disable();
        }

        if (weaponSlot3Action != null)
        {
            weaponSlot3Action.started -=
                Input_WeaponSlot3;

            weaponSlot3Action.Disable();
        }

        if (weaponCycleNextAction != null)
        {
            weaponCycleNextAction.started -=
                Input_WeaponCycleNext;

            weaponCycleNextAction.Disable();
        }
        #endregion

        #region 拾取/背包解绑
        if (pickupAction != null)
        {
            pickupAction.started -= Input_Pickup;
            pickupAction.Disable();
        }

        if (packageToggleAction != null)
        {
            packageToggleAction.started -= Input_PackageToggle;
            packageToggleAction.Disable();
        }
        #endregion
    }
    public void ManagedUpdate(float deltaTime)
    {
        if (!_isGameplayInputEnabled)
        {
            // 背包 UI 打开时禁止读取移动，确保角色不动
            if (dataBus != null)
                dataBus.moveInput = Vector2.zero;
            return;
        }

        Input_Movement();
        if (!useNewCursorSystem)
        {
            Input_MouseControl();
        }
    }

    public void ManagedLateUpdate(float deltaTime)//在LateUpdate中清理这些只需要存活一帧的标记，确保它们不会被下一帧误用
    {
        if (dataBus == null)
            return;

        dataBus.aimPressedThisFrame = false;
        dataBus.aimReleasedThisFrame = false;
        dataBus.houndAttackPressedThisFrame = false;
        dataBus.hunterAttackPressedThisFrame = false;
        dataBus.reloadPressedThisFrame = false;
        dataBus.weaponToggleLongPressedThisFrame = false;
        dataBus.jumpPressedThisFrame = false;
        dataBus.jumpReleasedThisFrame = false;
    }


    #region 移动系统
    public void Input_Movement()
    {
        Vector2 rawInput = movementAction.ReadValue<Vector2>();  // 获取原始的二维方向输入
        if (rawInput.magnitude > 0.1f)                 // 只有当输入超过阈值时，才认为是有效输入
        {
            currentMoveInput = rawInput.normalized;    // 关键：对输入进行标准化，确保8个方向长度一致
        }
        else
        {
            currentMoveInput = Vector2.zero;           // 没有输入时，清零移动方向
        }
        dataBus.moveInput = currentMoveInput;
        Logic_CheckMovingBackward();
    }
    private void Logic_CheckMovingBackward()//根据输入与当前朝向判断是否后退
    {
        if (dataBus.moveInput == Vector2.zero)//输入为零时不执行
        {
            dataBus.isMovingBackward = false;
            return;
        }
        // 获取角色朝向方向（标准化）
        Vector2 facingDirection2D = new Vector2(dataBus.facingDirection.x, dataBus.facingDirection.y).normalized;
        // 获取移动方向（标准化）
        // 如果点积小于0，表示方向相反
        float dotProduct = Vector2.Dot(facingDirection2D, dataBus.moveInput);
        // 点积值小于 -0.3 认为是有效后退
        dataBus.isMovingBackward = dotProduct < -0.3f;
    }
    public void Input_ForceSlowWalk(InputAction.CallbackContext context)//手动选择触发模式（Trigger/Press）按下后强制慢走，松开或冲刺时取消
    {
        if (dataBus.dashPressed)
        {
            dataBus.isForceWalkSlow = false;
            return;
        }

        bool isTriger = dataBus.forceSlowWalkInputTypeIsTriger;
        if (isTriger)
        {
            if (context.started)
            {
                bool lastTimeForceSlowWalkState = dataBus.isForceWalkSlow;
                bool newState = !lastTimeForceSlowWalkState;
                dataBus.isForceWalkSlow = newState;
            }
        }
        else
        {
            if (context.started)
            {
                dataBus.isForceWalkSlow = true;
            }

            if (context.canceled)
            {
                dataBus.isForceWalkSlow = false;
            }
        }
    }
    #endregion  
    public void Input_Crouch(InputAction.CallbackContext context)
    {
        if (dataBus.crouchInputTypeIsTriger)
        {
            if (context.started)  // 触发判断按压版本
            {
                if (!dataBus.isCrouch)
                {
                    dataBus.isCrouch = true;
                }
                else
                {
                    dataBus.isCrouch = false;
                }
            }
        }
        else
        {
            if (context.started)
            {
                dataBus.isCrouch = true;
            }
            else if (context.canceled)
            {
                dataBus.isCrouch = false;
            }
        }
    }
    #region 冲刺/回避系统（包含冲刺、回避判断，使用输入缓冲）
    public void Input_DashOrAvoid(InputAction.CallbackContext context)
    {

        //if (IsLocked) return;
        if (context.started)
        {
            dashOrAvoidPerformed = false;
        }
        if (context.performed)//需要按扭扭动因为hold模式
        {
            dataBus.dashPressed = true;//使用状态机方法分发更适合后续要处理的冲刺状态系统（继承冲刺时间、回避持续时间等），需要手动构建状态
            dashOrAvoidPerformed = true; ;
            dataBus.isForceWalkSlow = false;
        }
        if (context.canceled)
        {
            if (!dashOrAvoidPerformed)
            {
                EventCenter.Instance.EventTrigger(GameEvent.ANOIR_IsAvoid);
            }

            dataBus.dashPressed = false;
            dashOrAvoidPerformed = false;
        }
    }
    #endregion
    #region 鼠标系统

    private Vector2 Logic_MousePosition()
    {
        Vector2 hardwareMousePos = mouseAction.ReadValue<Vector2>();
        gamepadInput = gamePad_MouseAction.ReadValue<Vector2>();
        bool isGamepadActive = gamepadInput.magnitude > 0.1f;

        if (!isGamepadActive)
        {
            MousePosDelta = lastMousePos - hardwareMousePos;
        }
        bool isMouseActive = !isGamepadActive && MousePosDelta.magnitude > 0.1f;

        if (isGamepadActive)
        {
            dataBus.currentInputDevice = CharacterDataBus.InputDevice_AimType.Gamepad;

            // 更新手柄光标位置
            GamepadScreenPosition += gamepadInput * dataBus.gamepadMouseControlSensitivity * Time.deltaTime;
            GamepadScreenPosition.x = Mathf.Clamp(GamepadScreenPosition.x, 0, Screen.width);
            GamepadScreenPosition.y = Mathf.Clamp(GamepadScreenPosition.y, 0, Screen.height);

            ActiveScreenPosition = GamepadScreenPosition;
            lastMousePos = hardwareMousePos;

            return ActiveScreenPosition;
        }
        else if (isMouseActive)
        {
            dataBus.currentInputDevice = CharacterDataBus.InputDevice_AimType.Mouse;

            ActiveScreenPosition = hardwareMousePos;
            GamepadScreenPosition = hardwareMousePos;
            lastMousePos = hardwareMousePos;

            return ActiveScreenPosition;
        }
        else
        {
            if (dataBus.currentInputDevice == CharacterDataBus.InputDevice_AimType.Gamepad)
            {
                ActiveScreenPosition = GamepadScreenPosition;
            }
            else
            {
                ActiveScreenPosition = hardwareMousePos;
                GamepadScreenPosition = hardwareMousePos;
                lastMousePos = hardwareMousePos;
            }

            return ActiveScreenPosition;
        }
    }
    Vector3 Logic_GetMouseWorldPositionOnPlane(Vector3 planePosition)
    {
        Vector2 getMouseInputposi = Logic_MousePosition();
        Ray ray = Camera.main.ScreenPointToRay(getMouseInputposi);
        Plane plane = new Plane(Vector3.forward, planePosition); // 使用角色的 Z 平面
        float distance;
        if (plane.Raycast(ray, out distance))//可能，会返回一个distance，表示射线与平面的交点距离
        {
            return ray.GetPoint(distance);//如果有交点，返回交点坐标
        }
        return Vector3.zero; // 如果射线未命中平面（几乎不会发生）
    }
    private void Input_MouseControl()
    {
        Vector3 MouseWorldPosition = Logic_GetMouseWorldPositionOnPlane(dataBus.characterPosition);
        MouseWorldPosition.z = dataBus.characterPosition.z; // 确保 Z 一致
        dataBus.mousePositionOnScreen = MouseWorldPosition;
        if (MousePosDelta.magnitude > 0.1f || gamepadInput.magnitude > 0.1f)
        {
            dataBus.mouseMoving = true;
        }
        else
        {
            dataBus.mouseMoving = false;
        }
    }
    #endregion
    #region 瞄准系统
    public void Input_Aim(InputAction.CallbackContext context)//手动选择触发模式（Trigger/Press)
    {

        if (context.started)
        {
            dataBus.aimHeld = true;
            dataBus.aimPressedThisFrame = true;
        }
        else if (context.canceled)
        {
            dataBus.aimHeld = false;
            dataBus.aimReleasedThisFrame = true;
        }



    }
    public void SyncVirtualMouseToPosition(Vector2 screenPosition)
    {
        GamepadScreenPosition = screenPosition;
        ActiveScreenPosition = screenPosition;
        virtualMousePosition = screenPosition;  // 更新虚拟鼠标位置
    }
    #endregion
    #region 装弹/卸弹系统（包含短按、长按判断，使用输入缓冲）
    //短按装弹/卸弹，长按切换
    public void Input_EquipOrReload(InputAction.CallbackContext context)
    {
        if (dataBus == null)
            return;

        if (context.started)
        {
            // 第一次按下开始，重置长按标记
            equipOrReloadLongPerformed = false;
        }

        if (context.performed)
        {
            if (context.interaction is HoldInteraction)
            {
                // 一次按住期间，只触发一次长按
                if (!equipOrReloadLongPerformed)
                {
                    equipOrReloadLongPerformed = true;
                    dataBus.weaponToggleLongPressedThisFrame = true;

                    Debug.Log("[Input] Weapon Toggle Long Press Triggered Once");
                }
            }
        }

        if (context.canceled)
        {
            // 如果释放时没有触发长按，说明是短按
            if (!equipOrReloadLongPerformed)
            {
                dataBus.reloadPressedThisFrame = true;
            }

            // 一次按键结束，重置长按标记关闭
            equipOrReloadLongPerformed = false;
        }
    }

    #endregion

    public void Input_Jump(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            dataBus.jumpPressed = true;
            dataBus.jumpHeld = true;
            dataBus.jumpPressedThisFrame = true;
            // 不再以 isGrounded 拦截输入，由 JumpStateMachine 判断 Coyote Time / Buffer
            CharacterStateRequest request = new CharacterStateRequest(CharacterStateType.Jump_PreStart, true);
            characterRoot.stateArbitrator.SubmitRequest(request);
        }
        else if (context.canceled)
        {
            dataBus.jumpHeld = false;
            dataBus.jumpReleasedThisFrame = true;
            dataBus.jumpPressed = false;
        }
    }




    #region 攻击系统
    public void HunterWeapon_Attack(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            dataBus.hunterAttackPressedThisFrame = true;
            characterRoot.characterAttackController.SetHunterAttackHeld(true);
        }
        else if (context.canceled)
        {
            characterRoot.characterAttackController.SetHunterAttackHeld(false);
        }
    }

    public void HoundWeapon_Attack(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            dataBus.houndAttackPressedThisFrame = true;
            characterRoot.characterAttackController.SetHoundAttackHeld(true);
        }
        else if (context.canceled)
        {
            characterRoot.characterAttackController.SetHoundAttackHeld(false);
        }
    }


    #endregion


    #region 武器切换系统
    private void Input_WeaponSlot1(
    InputAction.CallbackContext context)
    {
        RequestWeaponSlot(WeaponSlotId.Slot1);
    }

    private void Input_WeaponSlot2(InputAction.CallbackContext context)
    {
        RequestWeaponSlot(WeaponSlotId.Slot2);
    }

    private void Input_WeaponSlot3(InputAction.CallbackContext context)
    {
        RequestWeaponSlot(WeaponSlotId.Slot3);
    }

    private void Input_WeaponCycleNext(InputAction.CallbackContext context)
    {
        if (dataBus == null)
            return;
        if (!dataBus.isAlive)
            return;
        dataBus.RequestWeaponSlotCycleNext();
        Debug.Log( "[Input] Request weapon cycle next.",this);
    }

    private void Input_Pickup(InputAction.CallbackContext context)
    {
        if (characterRoot == null) return;
        var pickupCtrl = characterRoot.characterPickupController;
        if (pickupCtrl != null)
            pickupCtrl.OnPickupPressed();
    }

    private void Input_PackageToggle(InputAction.CallbackContext context)
    {
        if (!context.started) return; // 只在按下瞬间触发一次，避免 started/performed 双触发
        if (characterRoot == null) return;
        var uiRouter = characterRoot.characterUIInputRouter;
        if (uiRouter != null)
            uiRouter.TogglePackageUI();
    }

    private void RequestWeaponSlot(WeaponSlotId slotId)
    {
        if (dataBus == null)
            return;
        if (!dataBus.isAlive)
            return;
        dataBus.RequestWeaponSlotSwitch(slotId);
        Debug.Log( $"[Input] Request weapon slot: {slotId}",this);
    }
    #endregion





}
