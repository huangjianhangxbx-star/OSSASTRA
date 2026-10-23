# 武器切换与装备/收纳系统相关代码

## 涉及的文件与职责

| 文件 | 职责 |
|---|---|
| InputHandler.cs | 读取1/2/3键切换武器槽、R键装备/换弹、滚轮切换 |
| CharacterWeaponInventory.cs | 武器槽管理（3槽位），适配PackageSystem |
| CharacterWeaponSlot.cs | 单个武器槽数据（WeaponSlotId, WeaponRuntimeData） |
| CharacterEquipmentController.cs | 武器视觉挂载/卸载，响应武器变化 |
| WeaponEquipStateMachine.cs | 武器装备/取消状态机，处理切换流程 |
| CharacterDataBus.cs | 武器请求数据（requestedWeaponSlot, weaponSwitchInputRequestType） |
| ECharacter.cs | WeaponSlotId 枚举 (Slot1=0, Slot2=1, Slot3=2) |

## InputHandler.cs

```csharp
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

public class InputHandler : MonoBehaviour,ICharacterSubsystem, ICharacterTickable,ICharacterLateTickable
{
    private bool _isInitialized;


    #region 杈撳叆绯荤粺瀛楁澹版槑
    [Header("绉诲姩鍔ㄤ綔")]
    public InputAction movementAction;//榛樿涓篔og
    [Header("璺宠穬鍔ㄤ綔")]
    public InputAction jumpAction;//榛樿涓篋ush
    [Header("寮哄埗鎱㈣蛋鍔ㄤ綔")]
    public InputAction forceSlowWalkAction;//寮哄埗鎱㈣蛋锛屽彧鍦ㄩ敭榧犱笅鎸変綇鏃剁敓鏁?    [Header("韫蹭笅鍔ㄤ綔")]
    public InputAction crouchAction;//鎸変綇韫蹭笅
    [Header("瑁呭脊/鍗稿脊鍔ㄤ綔")]
    public InputAction equipOrReloadAction;//鐭寜瑁呭脊/鍗稿脊锛岄暱鎸夊垏鎹?    [Header("鍐插埡/鍥為伩鍔ㄤ綔")]
    public InputAction dushOrAvoidAction;//榛樿涓篋ush
    [Header("鍥為伩鍔ㄤ綔")]
    public InputAction avoidAction;//鍥為伩
    [Header("鐬勫噯璁惧_榧犳爣")]
    public InputAction mouseAction;//榧犳爣锛屾墜鏌勫彸鎽囨潌鎺у埗
    [Header("鐬勫噯璁惧_鎵嬫焺")]
    public InputAction gamePad_MouseAction;//榧犳爣锛屾墜鏌勫彸鎽囨潌鎺у埗
    [Header("鐚庝汉姝﹀櫒鏀诲嚮鍔ㄤ綔")]
    public InputAction HunterWeaponAttack_Action;//鏀诲嚮鎸夐挳锛屾墜鏌勭洿鎺ユ壋鏈?
    [Header("鐚庣姮姝﹀櫒鏀诲嚮鍔ㄤ綔")]
    public InputAction HoundWeaponAttack_Action;//鏀诲嚮鎸夐挳锛屾墜鏌勭洿鎺ユ壋鏈?
    [Header("鐬勫噯鍔ㄤ綔")]
    public InputAction AimAction;//榧犳爣鍙抽敭锛屾墜鏌凩T閿?
    [Header("姝﹀櫒妲戒綅鍒囨崲")]
    [Tooltip("鎵嬫焺閫夋嫨姝﹀櫒妲戒綅 1")]
    public InputAction weaponSlot1Action;

    [Tooltip("鎵嬫焺閫夋嫨姝﹀櫒妲戒綅 2")]
    public InputAction weaponSlot2Action;

    [Tooltip("鎵嬫焺閫夋嫨姝﹀櫒妲戒綅 3")]
    public InputAction weaponSlot3Action;

    [Tooltip("鎵嬫満绔惊鐜垏鎹笅涓€涓鍣?)]
    public InputAction weaponCycleNextAction;

    [Header("鎷惧彇鍔ㄤ綔")]
    [Tooltip("鎸塃鎷惧彇闄勮繎鐗╁搧")]
    public InputAction pickupAction;

    [Header("鑳屽寘寮€鍏?)]
    [Tooltip("鎸塗ab鎵撳紑/鍏抽棴鑳屽寘")]
    public InputAction packageToggleAction;


    #endregion
    //================== 杈撳叆澶勭悊閰嶇疆 =================
    [Header("鏄惁浣跨敤鏂扮殑鐬勫噯绯荤粺")]
    public bool useNewCursorSystem = true;

    // ==== Gameplay 杈撳叆鎬诲紑鍏?====
    // 褰撹儗鍖?UI 鎵撳紑鏃剁鐢紝鍏抽棴鏃舵仮澶嶃€俻ackageToggleAction 姘镐笉鍙備笌锛堜繚璇佽兘鍐嶆鎸?Tab 鍏抽棴锛夈€?    private bool _isGameplayInputEnabled = true;
    private readonly List<InputAction> _gameplayActions = new List<InputAction>();


    private Vector2 currentMoveInput { get; set; }

    private Vector2 virtualMousePosition;
    private Vector2 lastMousePos { get; set; }
    private Vector3 MousePosDelta { get; set; }
    public Vector2 currentMousePos { get; set; }
    public Vector2 gamepadInput { get; set; }
    // 涓ょ杈撳叆璁惧鍚屾浣嶇疆鏉ユ簮
    public Vector2 GamepadScreenPosition;    // 鎵嬫焺鍦ㄥ睆骞曚綅缃?    public Vector2 ActiveScreenPosition;     // 褰撳墠鏈夋晥灞忓箷浣嶇疆

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
        //===========杈撳叆璁惧鍒濆鍖?===============
        // 鍒濆鍖栬櫄鎷熼紶鏍囦綅缃负灞忓箷涓績
        virtualMousePosition = new Vector2(Screen.width / 2f, Screen.height / 2f);
        // 鍙€夋嫨鏄惁鍚屾鐪熷疄榧犳爣锛堝緢澶氭父鎴忎笉鍚屾锛?        //Mouse.current.WarpCursorPosition(virtualMousePosition);
        //===========榧犳爣/鎵嬫焺璁惧鍒濆鍖?===============
        #region 鍔ㄤ綔缁戝畾鍒濆鍖?        movementAction.Enable();//绉诲姩鍙渶瑕乪nable鍗冲彲锛屽洜涓篒nput_Movement闇€瑕佸湪update涓瘡涓€甯ц皟鐢?        mouseAction.Enable();//鍙渶瑕乪nable鍗冲彲
        gamePad_MouseAction.Enable();//鍙渶瑕乪nable鍗冲彲
        //==============寮哄埗鎱㈣蛋鍔ㄤ綔================
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
        //=========韫蹭笅===========
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
        //=========鍐插埡/鍥為伩閿?==========
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
        //=========鐬勫噯閿?==========
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
        //=========瑁呭脊/鍗稿脊閿?==========
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
        //=========璺宠穬閿?==========
        jumpAction.Enable();
        jumpAction.started += (context) =>
        {
            Input_Jump(context);
        };
        // jumpAction.performed 宸茬Щ闄わ紝閬垮厤 started+performed 鍙岃Е鍙?        jumpAction.canceled += (context) =>
        {
            Input_Jump(context);
        };
        //=========鏀诲嚮閿?==========
        HunterWeaponAttack_Action.Enable();
        HunterWeaponAttack_Action.started += HunterWeapon_Attack;
        HunterWeaponAttack_Action.performed += HunterWeapon_Attack;
        HunterWeaponAttack_Action.canceled += HunterWeapon_Attack;

        HoundWeaponAttack_Action.Enable();
        HoundWeaponAttack_Action.started += HoundWeapon_Attack;
        HoundWeaponAttack_Action.performed += HoundWeapon_Attack;
        HoundWeaponAttack_Action.canceled += HoundWeapon_Attack;
        //===========姝﹀櫒鍒囨崲閿?==========
        weaponSlot1Action.Enable();
        weaponSlot1Action.started += Input_WeaponSlot1;
        weaponSlot2Action.Enable();
        weaponSlot2Action.started += Input_WeaponSlot2;
        weaponSlot3Action.Enable();
        weaponSlot3Action.started += Input_WeaponSlot3;
        weaponCycleNextAction.Enable();
        weaponCycleNextAction.started += Input_WeaponCycleNext;

        //=========鎷惧彇閿?==========
        if (pickupAction == null)
        {
            pickupAction = new InputAction("Pickup", binding: "<Keyboard>/e");
        }
        pickupAction.Enable();
        pickupAction.started += Input_Pickup;

        //=========鑳屽寘寮€鍏?==========
        if (packageToggleAction == null)
        {
            packageToggleAction = new InputAction("PackageToggle", binding: "<Keyboard>/tab");
        }
        packageToggleAction.Enable();
        packageToggleAction.started += Input_PackageToggle;

        #endregion

        // 鏀堕泦鎵€鏈?Gameplay 杈撳叆鍔ㄤ綔锛坧ackageToggleAction 闄ゅ锛?        RegisterGameplayAction(movementAction);
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
    /// Master switch for Gameplay inputs (called by CharacterUIInputRouter when the backpack opens/closes).
    /// Enables/disables movement, jump, crouch, dash, aim, attack, reload, weapon switch, pickup and aim input.
    /// Never disables packageToggleAction so the player can always press Tab to close the UI.
    /// When disabling, also clears held/sticky states so nothing lingers when the UI opens.
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

        #region 姝﹀櫒鍒囨崲瑙ｇ粦
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

        #region 鎷惧彇/鑳屽寘瑙ｇ粦
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
            // 鑳屽寘 UI 鎵撳紑鏃剁姝㈣鍙栫Щ鍔紝纭繚瑙掕壊涓嶅姩
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

    public void ManagedLateUpdate(float deltaTime)//鍦↙ateUpdate涓竻鐞嗚繖浜涘彧闇€瑕佸瓨娲讳竴甯х殑鏍囪锛岀‘淇濆畠浠笉浼氳涓嬩竴甯ц鐢?    {
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


    #region 绉诲姩绯荤粺
    public void Input_Movement()
    {
        Vector2 rawInput = movementAction.ReadValue<Vector2>();  // 鑾峰彇鍘熷鐨勪簩缁存柟鍚戣緭鍏?        if (rawInput.magnitude > 0.1f)                 // 鍙湁褰撹緭鍏ヨ秴杩囬槇鍊兼椂锛屾墠璁や负鏄湁鏁堣緭鍏?        {
            currentMoveInput = rawInput.normalized;    // 鍏抽敭锛氬杈撳叆杩涜鏍囧噯鍖栵紝纭繚8涓柟鍚戦暱搴︿竴鑷?        }
        else
        {
            currentMoveInput = Vector2.zero;           // 娌℃湁杈撳叆鏃讹紝娓呴浂绉诲姩鏂瑰悜
        }
        dataBus.moveInput = currentMoveInput;
        Logic_CheckMovingBackward();
    }
    private void Logic_CheckMovingBackward()//鏍规嵁杈撳叆涓庡綋鍓嶆湞鍚戝垽鏂槸鍚﹀悗閫€
    {
        if (dataBus.moveInput == Vector2.zero)//杈撳叆涓洪浂鏃朵笉鎵ц
        {
            dataBus.isMovingBackward = false;
            return;
        }
        // 鑾峰彇瑙掕壊鏈濆悜鏂瑰悜锛堟爣鍑嗗寲锛?        Vector2 facingDirection2D = new Vector2(dataBus.facingDirection.x, dataBus.facingDirection.y).normalized;
        // 鑾峰彇绉诲姩鏂瑰悜锛堟爣鍑嗗寲锛?        // 濡傛灉鐐圭Н灏忎簬0锛岃〃绀烘柟鍚戠浉鍙?        float dotProduct = Vector2.Dot(facingDirection2D, dataBus.moveInput);
        // 鐐圭Н鍊煎皬浜?-0.3 璁や负鏄湁鏁堝悗閫€
        dataBus.isMovingBackward = dotProduct < -0.3f;
    }
    public void Input_ForceSlowWalk(InputAction.CallbackContext context)//鎵嬪姩閫夋嫨瑙﹀彂妯″紡锛圱rigger/Press锛夋寜涓嬪悗寮哄埗鎱㈣蛋锛屾澗寮€鎴栧啿鍒烘椂鍙栨秷
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
            if (context.started)  // 瑙﹀彂鍒ゆ柇鎸夊帇鐗堟湰
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
    #region 鍐插埡/鍥為伩绯荤粺锛堝寘鍚啿鍒恒€佸洖閬垮垽鏂紝浣跨敤杈撳叆缂撳啿锛?    public void Input_DashOrAvoid(InputAction.CallbackContext context)
    {

        //if (IsLocked) return;
        if (context.started)
        {
            dashOrAvoidPerformed = false;
        }
        if (context.performed)//闇€瑕佹寜鎵壄鍔ㄥ洜涓篽old妯″紡
        {
            dataBus.dashPressed = true;//浣跨敤鐘舵€佹満鏂规硶鍒嗗彂鏇撮€傚悎鍚庣画瑕佸鐞嗙殑鍐插埡鐘舵€佺郴缁燂紙缁ф壙鍐插埡鏃堕棿銆佸洖閬挎寔缁椂闂寸瓑锛夛紝闇€瑕佹墜鍔ㄦ瀯寤虹姸鎬?            dashOrAvoidPerformed = true; ;
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
    #region 榧犳爣绯荤粺

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

            // 鏇存柊鎵嬫焺鍏夋爣浣嶇疆
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
        Plane plane = new Plane(Vector3.forward, planePosition); // 浣跨敤瑙掕壊鐨?Z 骞抽潰
        float distance;
        if (plane.Raycast(ray, out distance))//鍙兘锛屼細杩斿洖涓€涓猟istance锛岃〃绀哄皠绾夸笌骞抽潰鐨勪氦鐐硅窛绂?        {
            return ray.GetPoint(distance);//濡傛灉鏈変氦鐐癸紝杩斿洖浜ょ偣鍧愭爣
        }
        return Vector3.zero; // 濡傛灉灏勭嚎鏈懡涓钩闈紙鍑犱箮涓嶄細鍙戠敓锛?    }
    private void Input_MouseControl()
    {
        Vector3 MouseWorldPosition = Logic_GetMouseWorldPositionOnPlane(dataBus.characterPosition);
        MouseWorldPosition.z = dataBus.characterPosition.z; // 纭繚 Z 涓€鑷?        dataBus.mousePositionOnScreen = MouseWorldPosition;
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
    #region 鐬勫噯绯荤粺
    public void Input_Aim(InputAction.CallbackContext context)//鎵嬪姩閫夋嫨瑙﹀彂妯″紡锛圱rigger/Press)
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
        virtualMousePosition = screenPosition;  // 鏇存柊铏氭嫙榧犳爣浣嶇疆
    }
    #endregion
    #region 瑁呭脊/鍗稿脊绯荤粺锛堝寘鍚煭鎸夈€侀暱鎸夊垽鏂紝浣跨敤杈撳叆缂撳啿锛?    //鐭寜瑁呭脊/鍗稿脊锛岄暱鎸夊垏鎹?    public void Input_EquipOrReload(InputAction.CallbackContext context)
    {
        if (dataBus == null)
            return;

        if (context.started)
        {
            // 绗竴娆℃寜涓嬪紑濮嬶紝閲嶇疆闀挎寜鏍囪
            equipOrReloadLongPerformed = false;
        }

        if (context.performed)
        {
            if (context.interaction is HoldInteraction)
            {
                // 涓€娆℃寜浣忔湡闂达紝鍙Е鍙戜竴娆￠暱鎸?                if (!equipOrReloadLongPerformed)
                {
                    equipOrReloadLongPerformed = true;
                    dataBus.weaponToggleLongPressedThisFrame = true;

                    Debug.Log("[Input] Weapon Toggle Long Press Triggered Once");
                }
            }
        }

        if (context.canceled)
        {
            // 濡傛灉閲婃斁鏃舵病鏈夎Е鍙戦暱鎸夛紝璇存槑鏄煭鎸?            if (!equipOrReloadLongPerformed)
            {
                dataBus.reloadPressedThisFrame = true;
            }

            // 涓€娆℃寜閿粨鏉燂紝閲嶇疆闀挎寜鏍囪鍏抽棴
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
            // 涓嶅啀浠?isGrounded 鎷︽埅杈撳叆锛岀敱 JumpStateMachine 鍒ゆ柇 Coyote Time / Buffer
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




    #region 鏀诲嚮绯荤粺
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


    #region 姝﹀櫒鍒囨崲绯荤粺
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
        if (!context.started) return; // 鍙湪鎸変笅鐬棿瑙﹀彂涓€娆★紝閬垮厤 started/performed 鍙岃Е鍙?        if (characterRoot == null) return;
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
```

## CharacterWeaponInventory.cs

```csharp
using System;
using UnityEngine;

/// <summary>
/// Adapter layer between the new PackageSystem (CharacterWeaponSlots) and the existing weapon pipeline.
/// Reads from CharacterWeaponSlots at runtime; no longer accepts test injection from Inspector.
/// </summary>
[DisallowMultipleComponent]
public class CharacterWeaponInventory :
    MonoBehaviour,
    ICharacterSubsystem
{
    private const int SlotCount = 3;

    [Header("Weapon Slots")]
    [SerializeField]
    private CharacterWeaponSlot[] slots =
        new CharacterWeaponSlot[SlotCount];

    [Header("Runtime")]
    [SerializeField]
    private WeaponSlotId activeSlotId =
        WeaponSlotId.Slot1;

    private CharacterRoot characterRoot;
    private CharacterDataBus dataBus;
    private CharacterPackageSystem packageSystem;
    private bool isInitialized;

    public WeaponSlotId ActiveSlotId => activeSlotId;

    public WeaponRuntimeData ActiveWeapon
    {
        get
        {
            CharacterWeaponSlot slot =
                GetSlot(activeSlotId);

            return slot != null
                ? slot.Weapon
                : null;
        }
    }

    public bool HasActiveWeapon =>
        ActiveWeapon != null;

    public bool ActiveWeaponIsFirearm =>
        ActiveWeapon != null &&
        ActiveWeapon.IsFirearm;

    public bool ActiveWeaponIsMelee =>
        ActiveWeapon != null &&
        ActiveWeapon.IsMelee;

    public event Action<
        WeaponSlotId,
        WeaponRuntimeData,
        WeaponSlotId,
        WeaponRuntimeData> ActiveWeaponChanged;

    public void Initialize(CharacterRoot root)
    {
        if (isInitialized)
            return;

        characterRoot = root;

        if (characterRoot == null)
        {
            Debug.LogError("[CharacterWeaponInventory] CharacterRoot == null", this);
            return;
        }

        dataBus = characterRoot.characterDataBus;
        packageSystem = characterRoot.characterPackageSystem;

        if (dataBus == null)
        {
            Debug.LogError("[CharacterWeaponInventory] CharacterDataBus == null", this);
            return;
        }

        EnsureSlots();

        // Sync from PackageSystem if available (new game init already placed weapons there)
        if (packageSystem != null && packageSystem.WeaponSlots != null)
        {
            SyncFromPackageSystem();

            // Subscribe to runtime weapon slot changes for live sync
            packageSystem.OnWeaponSlotChanged += OnPackageWeaponSlotChanged;
        }

        dataBus.weaponInventory = this;

        MigrateLegacyCurrentWeapon();
        SyncLegacyCurrentWeapon();

        isInitialized = true;
    }

    /// <summary>
    /// Called when CharacterPackageSystem weapon slots change at runtime (e.g. drag-drop equip/unequip).
    /// Syncs the new weapon data into the legacy pipeline and notifies the equipment system.
    /// </summary>
    private void OnPackageWeaponSlotChanged(int slotIndex, ItemInstance item)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return;

        CharacterWeaponSlot slot = GetSlot((WeaponSlotId)slotIndex);
        if (slot == null) return;

        if (item == null)
        {
            // Weapon was removed from this slot
            if (!slot.IsEmpty)
                slot.RemoveWeapon();

            if (slotIndex == (int)activeSlotId)
            {
                // Active weapon removed 鈥?try to find next occupied or clear
                if (!TryGetNextOccupiedSlot(out WeaponSlotId nextSlot))
                {
                    // All slots empty
                    NotifyActiveWeaponChanged(activeSlotId, ActiveWeapon, activeSlotId, null);
                }
            }
        }
        else if (item.definition is WeaponItemDefinitionSO weaponDef)
        {
            // New weapon in this slot
            slot.SetWeapon(new WeaponRuntimeData(weaponDef.WeaponConfig));

            // If this is the active slot, remount the weapon
            if (slotIndex == (int)activeSlotId)
            {
                NotifyActiveWeaponChanged(activeSlotId, ActiveWeapon, activeSlotId, slot.Weapon);
            }
        }

        SyncLegacyCurrentWeapon();
    }

    /// <summary>
    /// Copy weapon instances from CharacterWeaponSlots into the legacy slot array.
    /// This bridges the new ItemInstance-based system with the existing WeaponRuntimeData pipeline.
    /// </summary>
    private void SyncFromPackageSystem()
    {
        var weaponSlots = packageSystem.WeaponSlots;
        for (int i = 0; i < SlotCount && i < weaponSlots.Capacity; i++)
        {
            var itemInstance = weaponSlots.GetItem(i);
            if (itemInstance == null) continue;

            // Extract WeaponConfigSO from the WeaponItemDefinitionSO
            if (itemInstance.definition is WeaponItemDefinitionSO weaponDef)
            {
                var slot = GetSlot((WeaponSlotId)i);
                if (slot != null && slot.IsEmpty)
                {
                    slot.SetWeapon(new WeaponRuntimeData(weaponDef.WeaponConfig));
                }
            }
        }
    }

    public CharacterWeaponSlot GetSlot(WeaponSlotId slotId)
    {
        int index = (int)slotId;
        if (slots == null || index < 0 || index >= slots.Length)
            return null;
        return slots[index];
    }

    public WeaponRuntimeData GetWeapon(WeaponSlotId slotId)
    {
        CharacterWeaponSlot slot = GetSlot(slotId);
        return slot != null ? slot.Weapon : null;
    }

    public bool HasWeapon(WeaponSlotId slotId)
    {
        return GetWeapon(slotId) != null;
    }

    public bool CanSelectSlot(WeaponSlotId slotId)
    {
        CharacterWeaponSlot slot = GetSlot(slotId);
        return slot != null && slot.CanSelect;
    }

    public bool TryAssignWeapon(
        WeaponSlotId slotId,
        WeaponRuntimeData weapon,
        bool replaceExisting = false)
    {
        if (weapon == null)
            return false;

        CharacterWeaponSlot slot = GetSlot(slotId);
        if (slot == null || !slot.IsUnlocked)
            return false;

        if (!slot.IsEmpty && !replaceExisting)
            return false;

        WeaponRuntimeData oldActiveWeapon = ActiveWeapon;
        slot.SetWeapon(weapon);

        if (slotId == activeSlotId)
        {
            NotifyActiveWeaponChanged(activeSlotId, oldActiveWeapon, activeSlotId, weapon);
        }

        return true;
    }

    public WeaponRuntimeData RemoveWeapon(WeaponSlotId slotId)
    {
        CharacterWeaponSlot slot = GetSlot(slotId);
        if (slot == null || slot.IsEmpty)
            return null;

        WeaponRuntimeData oldActiveWeapon = ActiveWeapon;
        WeaponRuntimeData removedWeapon = slot.RemoveWeapon();

        if (slotId == activeSlotId)
        {
            NotifyActiveWeaponChanged(activeSlotId, oldActiveWeapon, activeSlotId, null);
        }

        return removedWeapon;
    }

    public bool TrySetActiveSlot(WeaponSlotId slotId)
    {
        if (!CanSelectSlot(slotId))
            return false;
        if (slotId == activeSlotId)
            return true;

        WeaponSlotId previousSlotId = activeSlotId;
        WeaponRuntimeData previousWeapon = ActiveWeapon;
        activeSlotId = slotId;
        NotifyActiveWeaponChanged(previousSlotId, previousWeapon, activeSlotId, ActiveWeapon);
        return true;
    }

    public bool TryGetNextOccupiedSlot(out WeaponSlotId nextSlotId)
    {
        return TryGetNextOccupiedSlotFrom(activeSlotId, out nextSlotId);
    }

    public bool TrySelectNextOccupiedSlot()
    {
        if (!TryGetNextOccupiedSlot(out WeaponSlotId nextSlotId))
            return false;
        return TrySetActiveSlot(nextSlotId);
    }

    private void EnsureSlots()
    {
        if (slots == null || slots.Length != SlotCount)
            Array.Resize(ref slots, SlotCount);

        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] == null)
                slots[i] = new CharacterWeaponSlot();
            slots[i].Initialize((WeaponSlotId)i);
        }
    }

    private void MigrateLegacyCurrentWeapon()
    {
        if (dataBus.currentWeaponRuntimeData == null)
            return;

        CharacterWeaponSlot firstSlot = GetSlot(WeaponSlotId.Slot1);
        if (firstSlot == null || !firstSlot.IsEmpty)
            return;

        firstSlot.SetWeapon(dataBus.currentWeaponRuntimeData);
        activeSlotId = WeaponSlotId.Slot1;

        Debug.Log("[CharacterWeaponInventory] Migrated currentWeaponRuntimeData to Slot1", characterRoot);
    }

    private void NotifyActiveWeaponChanged(
        WeaponSlotId previousSlotId,
        WeaponRuntimeData previousWeapon,
        WeaponSlotId newSlotId,
        WeaponRuntimeData newWeapon)
    {
        SyncLegacyCurrentWeapon();
        ActiveWeaponChanged?.Invoke(previousSlotId, previousWeapon, newSlotId, newWeapon);
    }

    public bool TryGetNextOccupiedSlotFrom(WeaponSlotId fromSlotId, out WeaponSlotId nextSlotId)
    {
        nextSlotId = fromSlotId;
        if (slots == null || slots.Length == 0)
            return false;

        int startIndex = (int)fromSlotId;
        for (int offset = 1; offset <= slots.Length; offset++)
        {
            int candidateIndex = (startIndex + offset) % slots.Length;
            CharacterWeaponSlot candidate = slots[candidateIndex];
            if (candidate == null) continue;
            if (!candidate.CanSelect) continue;
            nextSlotId = candidate.SlotId;
            return true;
        }
        return false;
    }

    private void SyncLegacyCurrentWeapon()
    {
        if (dataBus == null)
            return;
        dataBus.currentWeaponRuntimeData = ActiveWeapon;
    }
}
```

## CharacterWeaponSlot.cs

```csharp
using System;
using UnityEngine;

[Serializable]
public class CharacterWeaponSlot
{
    [SerializeField]
    private WeaponSlotId slotId;

    /*
     * WeaponRuntimeData 如果不是 Unity 可序列化类型，
     * Inspector 中不会显示，但运行时赋值仍然可以使用。
     */
    [SerializeField]
    private WeaponRuntimeData weaponRuntimeData;

    [SerializeField]
    private bool isUnlocked = true;

    public WeaponSlotId SlotId => slotId;

    public WeaponRuntimeData Weapon => weaponRuntimeData;

    public bool IsUnlocked => isUnlocked;

    public bool IsEmpty => weaponRuntimeData == null;

    public bool CanSelect =>
        isUnlocked && weaponRuntimeData != null;

    public void Initialize(WeaponSlotId id)
    {
        slotId = id;
    }

    public void SetWeapon(WeaponRuntimeData weapon)
    {
        weaponRuntimeData = weapon;
    }

    public WeaponRuntimeData RemoveWeapon()
    {
        WeaponRuntimeData removedWeapon = weaponRuntimeData;
        weaponRuntimeData = null;
        return removedWeapon;
    }

    public void SetUnlocked(bool unlocked)
    {
        isUnlocked = unlocked;
    }
}
```

## CharacterEquipmentController.cs

```csharp
using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterEquipmentController : MonoBehaviour, ICharacterSubsystem
{
    private bool _isInitialized;

    [Header("Owner")]
    [SerializeField] private CharacterRoot characterRoot;

    [Header("Spine")]
    [SerializeField] private SkeletonAnimation skeletonAnimation;
    [SerializeField] private SkeletonRenderer skeletonRenderer;
    [SerializeField] private SkeletonRenderSeparator renderSeparator;

    [Header("Roots")]
    [SerializeField] private Transform equipmentRoot;

    [Header("Weapon Mount")]
    [SerializeField] private ExternalWeaponMount houndWeaponMount;

    [Header("Default Names")]
    [SerializeField] private string weaponBoneName = "LongRangeWeapon";
    [SerializeField] private string weaponSlotName = "LongRangeWeapon";

    [Header("装备")]
    [SerializeField] private WeaponConfigSO currentWeaponConfig;


    private CharacterWeaponInventory weaponInventory;

    public WeaponRuntimeData MountedWeaponRuntimeData { get; private set; }

    public WeaponConfigSO CurrentWeaponConfig => currentWeaponConfig;

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized)
            return;

        characterRoot = root;

        AutoGetReferences();
        ConfigureWeaponMount();
        if (characterRoot != null)
        {
            weaponInventory =
                characterRoot.characterWeaponInventory;

            if (weaponInventory == null)
            {
                weaponInventory =
                    characterRoot.GetComponent<CharacterWeaponInventory>();
            }
        }

        _isInitialized = true;

        /*
   * 第一阶段迁移完成后，启动武器应来自 Inventory。
   */
        if (weaponInventory != null &&
            weaponInventory.ActiveWeapon != null)
        {
            MountWeaponRuntimeData(
                weaponInventory.ActiveWeapon
            );

            return;
        }
        /*
 * 兼容旧场景中的 Inspector 初始武器。
 * 这段最终可在所有初始装备都进入 Inventory 后删除。
 */
        if (currentWeaponConfig != null)
        {
            SetHoundWeapon(currentWeaponConfig);
        }



        //WeaponConfigSO Weapon = currentWeaponConfig;

        //if (Weapon != null)
        //{
        //    SetHoundWeapon(Weapon);
        //}
    }

    private void Reset()
    {
        AutoGetReferences();
    }

    private void AutoGetReferences()
    {
        if (characterRoot == null)
            characterRoot = GetComponent<CharacterRoot>();

        if (skeletonAnimation == null && characterRoot != null)
            skeletonAnimation = characterRoot.skeletonAnimation;

        if (skeletonAnimation == null)
            skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);

        if (skeletonRenderer == null && skeletonAnimation != null)
            skeletonRenderer = skeletonAnimation.GetComponent<SkeletonRenderer>();

        if (renderSeparator == null && skeletonAnimation != null)
            renderSeparator = skeletonAnimation.GetComponent<SkeletonRenderSeparator>();

        if (equipmentRoot == null)
        {
            Transform found = transform.Find("EquipmentRoot");

            if (found != null)
                equipmentRoot = found;
        }

        if (houndWeaponMount == null && equipmentRoot != null)
            houndWeaponMount = equipmentRoot.GetComponentInChildren<ExternalWeaponMount>(true);

        if (houndWeaponMount == null)
            houndWeaponMount = GetComponentInChildren<ExternalWeaponMount>(true);
    }

    private void ConfigureWeaponMount()
    {
        if (houndWeaponMount == null)
        {
            Debug.LogWarning("[CharacterEquipmentController] houndWeaponMount == null", this);
            return;
        }

        houndWeaponMount.Bind(new ExternalWeaponMount.BindData
        {
            SkeletonRenderer = skeletonRenderer,
            RenderSeparator = renderSeparator,
            FollowBoneName = weaponBoneName,
            RenderSlotName = weaponSlotName
        });
    }

    public void SetHoundWeapon(WeaponConfigSO weaponConfig)// 负责创建运行时数据并写入槽位
    {
        //    创建 WeaponRuntimeData
        //写入 Inventory

        if (weaponConfig == null)
        {
            ClearHoundWeapon();
            return;
        }

        WeaponRuntimeData runtimeData =
            new WeaponRuntimeData(weaponConfig);

        /*
   * 兼容旧调用。
   *
   * 如果 Inventory 已经存在，应将新武器写进当前槽，
   * 再挂载 Inventory 中的 ActiveWeapon。
   */
        if (weaponInventory != null)
        {
            bool assigned =
                weaponInventory.TryAssignWeapon(
                    weaponInventory.ActiveSlotId,
                    runtimeData,
                    true
                );

            if (!assigned)
            {
                Debug.LogWarning(
                    "[CharacterEquipmentController] " +
                    "无法把新武器写入当前槽位。",
                    this
                );

                return;
            }

            runtimeData =
                weaponInventory.ActiveWeapon;
        }

        MountWeaponRuntimeData(runtimeData);

        if (characterRoot != null &&
    characterRoot.characterDataBus != null)
        {
            characterRoot.characterDataBus.weaponEquipState =
                WeaponEquipState.Cancelled;
        }

        characterRoot.stateArbitrator.weaponEquipSM.ApplyWeaponVisibility(false);
    }

    public void ClearHoundWeapon()
    {


        WeaponRuntimeData activeWeapon =
    weaponInventory != null
        ? weaponInventory.ActiveWeapon
        : MountedWeaponRuntimeData;

        UnmountCurrentWeaponVisual();

        /*
         * 真正执行 ClearHoundWeapon 时，
         * 从当前槽位移除武器。
         */
        if (weaponInventory != null &&
            activeWeapon != null)
        {
            weaponInventory.RemoveWeapon(
                weaponInventory.ActiveSlotId
            );
        }

        if (characterRoot != null &&
            characterRoot.characterDataBus != null)
        {
            CharacterDataBus dataBus =
                characterRoot.characterDataBus;

            dataBus.currentWeaponRuntimeData = null;
            dataBus.weaponEquipState =
                WeaponEquipState.Cancelled;

            ResetCombatFlagsAfterWeaponChanged();
        }

        GunSystemController gunSystemController =
            characterRoot != null
                ? characterRoot.gunSystemController
                : null;

        if (gunSystemController != null)
        {
            gunSystemController.ClearWeaponRuntimeData();
        }

        RefreshAfterWeaponChanged();










    }

    private void RefreshAfterWeaponChanged()
    {
        if (characterRoot != null && characterRoot.characterStatsController != null)
        {
            characterRoot.characterStatsController.RecalculateStats();
        }

        if (characterRoot != null && characterRoot.characterAnimationConfigManager != null)
        {
            characterRoot.characterAnimationConfigManager.RefreshWeaponAnimationOverride();
        }
    }






    public bool MountWeaponRuntimeData(WeaponRuntimeData runtimeData) // 负责实例化武器模型
    {
        if (characterRoot == null ||
            characterRoot.characterDataBus == null)
        {
            Debug.LogError(
                "[CharacterEquipmentController] " +
                "CharacterRoot 或 DataBus 为空。",
                this
            );

            return false;
        }

        if (houndWeaponMount == null)
        {
            Debug.LogError(
                "[CharacterEquipmentController] " +
                "HoundWeaponMount 为空。",
                this
            );

            return false;
        }

        if (runtimeData == null ||
            runtimeData.config == null)
        {
            Debug.LogWarning(
                "[CharacterEquipmentController] " +
                "无法挂载空的 WeaponRuntimeData。",
                this
            );

            return false;
        }

        WeaponConfigSO weaponConfig =
            runtimeData.config;

        if (weaponConfig.weaponPrefab == null)
        {
            Debug.LogError(
                $"[CharacterEquipmentController] 武器 " +
                $"{weaponConfig.weaponName} 没有配置 weaponPrefab。",
                weaponConfig
            );

            return false;
        }

        /*
         * 只卸载旧模型，不删除旧槽位中的 RuntimeData。
         */
        UnmountCurrentWeaponVisual();

        WeaponRoot weaponRoot =
            houndWeaponMount.SetWeaponPrefab(
                weaponConfig.weaponPrefab
            );

        if (weaponRoot == null)
        {
            Debug.LogError(
                "[CharacterEquipmentController] 武器实例化失败。",
                this
            );

            return false;
        }

        weaponRoot.Initialize(
            weaponConfig,
            characterRoot
        );

        runtimeData.weaponRoot = weaponRoot;

        MountedWeaponRuntimeData = runtimeData;
        currentWeaponConfig = weaponConfig;

        CharacterDataBus dataBus =
            characterRoot.characterDataBus;

        /*
         * 迁移期兼容镜像。
         * Inventory 仍然是 ActiveWeapon 的真实所有者。
         */
        dataBus.currentWeaponRuntimeData =
            runtimeData;

        GunSystemController gunSystemController =
            characterRoot.gunSystemController;

        if (gunSystemController == null)
        {
            gunSystemController =
                characterRoot.GetComponent<GunSystemController>();
        }

        if (gunSystemController != null)
        {
            gunSystemController.ApplyWeaponRuntimeData(
                runtimeData
            );
        }

        ResetCombatFlagsAfterWeaponChanged();
        RefreshAfterWeaponChanged();

        Debug.Log(
            $"[CharacterEquipmentController] 挂载武器完成。" +
            $" Weapon={weaponConfig.weaponName}",
            this
        );

        return true;
    }


    public void UnmountCurrentWeaponVisual()
    {
        if (MountedWeaponRuntimeData != null)
        {
            MountedWeaponRuntimeData.weaponRoot = null;
        }

        if (houndWeaponMount != null)
        {
            houndWeaponMount.ClearWeapon();
        }

        MountedWeaponRuntimeData = null;
        currentWeaponConfig = null;
    }


    public bool MountActiveInventoryWeapon()
    {
        if (weaponInventory == null &&
            characterRoot != null)
        {
            weaponInventory =
                characterRoot.characterWeaponInventory;
        }

        if (weaponInventory == null)
        {
            Debug.LogError(
                "[CharacterEquipmentController] " +
                "CharacterWeaponInventory 为空。",
                this
            );

            return false;
        }

        WeaponRuntimeData activeWeapon =
            weaponInventory.ActiveWeapon;

        if (activeWeapon == null)
        {
            UnmountCurrentWeaponVisual();

            if (characterRoot != null &&
                characterRoot.characterDataBus != null)
            {
                characterRoot.characterDataBus
                    .currentWeaponRuntimeData = null;
            }

            RefreshAfterWeaponChanged();
            return false;
        }

        return MountWeaponRuntimeData(activeWeapon);
    }

    private void ResetCombatFlagsAfterWeaponChanged()
    {
        if (characterRoot == null ||
            characterRoot.characterDataBus == null)
        {
            return;
        }

        CharacterDataBus dataBus =
            characterRoot.characterDataBus;

        dataBus.isAiming = false;
        dataBus.isReloading = false;
        dataBus.isHoundAttacking = false;
        dataBus.currentHoundCombatAction =
            HoundCombatActionType.None;
    }



}
```

## WeaponEquipStateMachine.cs

```csharp

//负责：
//武器是否装备到手上
//长按 R 切换装备/收起
//安全区自动收起
//自动收武器
//播放 Equip / Cancel 动画
//决定武器是否可用
//提交动画意图：
//提交语义：
//EAnimSemantic.Weapon_Equip
//EAnimSemantic.Weapon_Cancel
using UnityEngine;

public class WeaponEquipStateMachine : IParallelCharacterStateMachine
{
    public string StateMachineName => "WeaponEquipStateMachine";

    // 要在 HoundCombatActionStateMachine 前执行
    public int Priority => 14;

    public string CurrentStateName => dataBus != null
        ? dataBus.weaponEquipState.ToString()
        : "None";

    public bool IsActive
    {
        //get
        //{
        //    return dataBus != null
        //        && dataBus.isAlive
        //        && dataBus.currentWeaponRuntimeData != null;
        //}

        get
        {
            /*
             * 切换期间 ActiveWeapon 可能暂时改变，
             * 因此还要考虑 pending switch。
             */
            return dataBus != null &&
                   dataBus.isAlive &&
                   (HasActiveWeapon ||
                    hasPendingSlotSwitch);
        }

    }

    private CharacterDataBus dataBus;
    private CharacterRoot characterRoot;
    private CharacterAnimationComposer animationComposer;

    private CharacterWeaponInventory inventory;
    private CharacterEquipmentController equipmentController;
    private ExternalWeaponMount weaponMount;

    private float transitionTimer;
    private int replayId;

    // 第一版先写死，后面可以从 WeaponCombatProfileSO 或 WeaponVisualProfileSO 读取
    private const float DefaultEquipDuration = 0.35f;
    private const float DefaultCancelDuration = 0.35f;

    /*
 * 待执行的切槽请求。
 *
 * 请求只存放在装备状态机中，
 * DataBus 不需要保存槽位切换算法和生命周期。
 */
    private bool hasPendingSlotSwitch;
    private WeaponSlotId pendingSlotId;

    private bool equipAfterSwitch;

    private bool HasActiveWeapon =>
        inventory != null &&
        inventory.ActiveWeapon != null;




    public void Initialize(CharacterDataBus bus, Rigidbody2D rb, Transform tf, CharacterRoot root)
    {
        dataBus = bus;
        characterRoot = root;

        if (characterRoot != null)
        {
            animationComposer =
                characterRoot.characterAnimationComposer;

            inventory =
                characterRoot.characterWeaponInventory;

            if (inventory == null)
            {
                inventory =
                    characterRoot.GetComponent<
                        CharacterWeaponInventory>();
            }

            equipmentController =
                characterRoot.GetComponent<
                    CharacterEquipmentController>();

            weaponMount =
                characterRoot.GetComponentInChildren<
                    ExternalWeaponMount>(true);
        }
        if (dataBus == null)
            return;

        dataBus.weaponEquipState =
            WeaponEquipState.Cancelled;

        ApplyWeaponVisibility(false);
    }

    public void Enter()
    {
    }

    public void Update(float deltaTime)
    {

        /*
 * 武器切换输入在任何装备过渡阶段都可以接收。
 *
 * 如果正在 Cancelling，新的数字键请求会更新
 * pendingSlotId，最终使用最后一次请求。
 */
        ConsumeWeaponSwitchInput();
        /*
 * 先处理过渡。
 * 过渡完成时可能执行槽位替换并开始下一段 Equip。
 */
        UpdateTransition(deltaTime);
        /*
 * 只有当前不在过渡中时才接受普通请求。
 * 切槽请求可以通过 RequestSwitchSlot() 在任意时刻覆盖 pending。
 */
        if (!IsTransitioning())
        {
            HandleRequests();
        }

        /*
 * 没有武器且没有待切槽时，保持彻底收起。
 */
        if (!HasActiveWeapon &&
            !hasPendingSlotSwitch)
        {
            RemoveEquipIntent();
            ForceCancelled();
            return;
        }
        SyncWeaponVisualState();
    }

    public void FixedUpdate(float fixedDeltaTime)
    {
    }

    public bool RequestState(object stateData)
    {
        /*
           * 允许以后通过统一状态请求传入 WeaponSlotId。
           */
        if (stateData is WeaponSlotId slotId)
        {
            return RequestSwitchSlot(slotId);
        }

        return false;
    }

    /// <summary>
    /// 请求切换到指定武器槽。
    ///
    /// 当前阶段可由调试按钮直接调用。
    /// 下一阶段再由 InputHandler 的 1/2/3 输入调用。
    /// </summary>
    public bool RequestSwitchSlot(
        WeaponSlotId slotId)
    {
        if (dataBus == null ||
            inventory == null ||
            equipmentController == null)
        {
            return false;
        }

        if (!inventory.CanSelectSlot(slotId))
        {
            Debug.LogWarning(
                $"[WeaponEquip] 槽位不可选择: {slotId}",
                characterRoot
            );

            return false;
        }

        /*
         * 已经是当前槽，而且没有正在切换，不重复执行。
         */
        if (slotId == inventory.ActiveSlotId &&
            !hasPendingSlotSwitch)
        {
            switch (dataBus.weaponEquipState)
            {
                case WeaponEquipState.Cancelled:
                    {
                        /*
                         * 当前槽位正确，只是武器处于收起状态。
                         * 数字键应直接拿出当前武器。
                         */
                        if (!dataBus.isInSafeZone)
                        {
                            StartEquip();
                        }
                        Debug.Log( $"[WeaponEquip] {slotId} 已经是当前槽位，" +$"执行当前武器拿出。",characterRoot);
                        return true;
                    }
                case WeaponEquipState.Equipped:
                    {
                        Debug.Log( $"[WeaponEquip] 忽略切换请求：" +$"{slotId} 已经装备并处于使用状态。", characterRoot);
                        return true;
                    }
                case WeaponEquipState.Equipping:
                    {
                        Debug.Log($"[WeaponEquip] 忽略切换请求：" +$"{slotId} 正在装备。",characterRoot);
                        return true;
                    }

                case WeaponEquipState.Cancelling:
                    {
                        /*
                         * 当前武器正在收起，但玩家又按了当前槽位。
                         * 让 Cancel 完成后重新装备当前槽位。
                         */
                        pendingSlotId = slotId;
                        hasPendingSlotSwitch = true;
                        equipAfterSwitch = true;
                        Debug.Log($"[WeaponEquip] 当前槽位正在收起，" +$"完成后重新装备 {slotId}。",characterRoot);
                        return true;
                    }
            }
        }

        /*
         * 如果切换过程中再次请求其他数字键，
         * 使用最后一次请求。
         */
        pendingSlotId = slotId;
        hasPendingSlotSwitch = true;

        /*
         * 数字键切换的目标是拿出新武器。
         */
        equipAfterSwitch = true;

        Debug.Log($"[WeaponEquip] 收到切槽请求。" +$" Current={inventory.ActiveSlotId}," + $" Target={pendingSlotId}," + $" EquipState={dataBus.weaponEquipState}",characterRoot);

        switch (dataBus.weaponEquipState)
        {
            case WeaponEquipState.Equipped:
            case WeaponEquipState.Equipping:
                StartCancel();
                break;

            case WeaponEquipState.Cancelled:
                CommitPendingSlotSwitch();
                break;

            case WeaponEquipState.Cancelling:
                /*
                 * 正在收起时只更新 pendingSlotId。
                 * Cancel 完成后使用最新请求。
                 */
                break;
        }

        return true;
    }

    public bool RequestSwitchToNextSlot()
    {
        if (inventory == null)
            return false;
        /*
 * 如果已经有一个待切换目标，
 * 下一次轮换应从待切换目标继续往后查。
 */
        WeaponSlotId cycleStartSlot =
            hasPendingSlotSwitch
                ? pendingSlotId
                : inventory.ActiveSlotId;
        bool found =inventory.TryGetNextOccupiedSlotFrom(cycleStartSlot,out WeaponSlotId nextSlotId);
        if (!found)
        {
            Debug.Log("[WeaponEquip] 没有找到下一个可用武器槽。",characterRoot );
            return false;
        }
        return RequestSwitchSlot(nextSlotId);
    }

    private void HandleRequests()
    {
        if (dataBus == null)
            return;

        if (dataBus.isInSafeZone)
        {
            if (dataBus.weaponEquipState ==
                    WeaponEquipState.Equipped ||
                dataBus.weaponEquipState ==
                    WeaponEquipState.Equipping)
            {
                /*
                 * 安全区收起不是切槽。
                 */
                hasPendingSlotSwitch = false;
                equipAfterSwitch = false;
                StartCancel();
            }

            dataBus.requestCancelWeapon = false;
            return;
        }

        if (dataBus.requestCancelWeapon)
        {
            dataBus.requestCancelWeapon = false;

            if (dataBus.weaponEquipState ==
                    WeaponEquipState.Equipped ||
                dataBus.weaponEquipState ==
                    WeaponEquipState.Equipping)
            {
                hasPendingSlotSwitch = false;
                equipAfterSwitch = false;
                StartCancel();
            }

            return;
        }

        if (!dataBus.weaponToggleLongPressedThisFrame)
            return;

        if (dataBus.weaponEquipState ==
            WeaponEquipState.Cancelled)
        {
            StartEquip();
            return;
        }

        if (dataBus.weaponEquipState ==
            WeaponEquipState.Equipped)
        {
            hasPendingSlotSwitch = false;
            equipAfterSwitch = false;
            StartCancel();
        }
    }









    public bool CanBeInterruptedBy(ICharacterStateMachine other)
    {
        return true;
    }

    public bool CanInterrupt(ICharacterStateMachine other)
    {
        return false;
    }


    private void StartEquip()
    {
        if (dataBus == null ||
            !HasActiveWeapon)
        {
            return;
        }

        if (dataBus.weaponEquipState ==
                WeaponEquipState.Equipping ||
            dataBus.weaponEquipState ==
                WeaponEquipState.Equipped)
        {
            return;
        }

        replayId++;

        dataBus.weaponEquipState =
            WeaponEquipState.Equipping;

        /*
         * 装备动画开始时武器出现。
         */
        ApplyWeaponVisibility(true);

        transitionTimer = GetEquipDuration();

        SubmitEquipIntent(
            EAnimSemantic.Weapon_Equip,
            transitionTimer,
            replayId
        );
    }


    private void StartCancel()
    {
        if (dataBus == null)
            return;

        if (dataBus.weaponEquipState ==
                WeaponEquipState.Cancelled ||
            dataBus.weaponEquipState ==
                WeaponEquipState.Cancelling)
        {
            return;
        }

        replayId++;

        dataBus.weaponEquipState =
            WeaponEquipState.Cancelling;

        transitionTimer = GetCancelDuration();

        SubmitEquipIntent(
            EAnimSemantic.Weapon_Cancel,
            transitionTimer,
            replayId
        );

        if (characterRoot != null &&
            characterRoot.stateArbitrator != null &&
            characterRoot.stateArbitrator
                .houndCombatActionSM != null)
        {
            characterRoot.stateArbitrator
                .houndCombatActionSM
                .HardClearWeaponAnimation();
        }
    }

    private void UpdateTransition(float deltaTime)
    {
        if (dataBus == null ||
            !IsTransitioning())
        {
            return;
        }

        transitionTimer -= deltaTime;

        if (transitionTimer > 0f)
            return;

        if (dataBus.weaponEquipState ==
            WeaponEquipState.Equipping)
        {
            dataBus.weaponEquipState =
                WeaponEquipState.Equipped;

            RemoveEquipIntent();
            return;
        }

        if (dataBus.weaponEquipState ==
            WeaponEquipState.Cancelling)
        {
            dataBus.weaponEquipState =
                WeaponEquipState.Cancelled;

            RemoveEquipIntent();
            ApplyWeaponVisibility(false);

            /*
             * 旧武器已经完全收起，
             * 现在才能替换模型和 ActiveSlot。
             */
            if (hasPendingSlotSwitch)
            {
                CommitPendingSlotSwitch();
            }
        }
    }


    private void CommitPendingSlotSwitch()
    {
        if (!hasPendingSlotSwitch ||
            inventory == null ||
            equipmentController == null)
        {
            return;
        }

        WeaponSlotId targetSlot =
            pendingSlotId;

        bool shouldEquip =
            equipAfterSwitch;

        /*
         * 先清 pending，避免回调或失败分支重入。
         */
        hasPendingSlotSwitch = false;
        equipAfterSwitch = false;
        ApplyWeaponVisibility(false);
        bool slotChanged =inventory.TrySetActiveSlot(targetSlot);
        if (!slotChanged)
        {
            Debug.LogWarning(
                $"[WeaponEquip] ActiveSlot 切换失败: " +
                $"{targetSlot}",
                characterRoot
            );

            return;
        }

        bool mounted =
            equipmentController
                .MountActiveInventoryWeapon();

        if (!mounted)
        {
            Debug.LogError(
                $"[WeaponEquip] 新武器挂载失败: " +
                $"Slot={targetSlot}",
                characterRoot
            );

            ForceCancelled();
            return;
        }

        /*
         * MountWeaponRuntimeData 会清战斗标记，
         * 这里明确保持装备状态为 Cancelled，
         * 然后开始新武器 Equip。
         */
        dataBus.weaponEquipState =
            WeaponEquipState.Cancelled;

        Debug.Log(
            $"[WeaponEquip] 已切换槽位。" +
            $" Active={inventory.ActiveSlotId}," +
            $" Weapon={GetActiveWeaponName()}",
            characterRoot
        );

        if (shouldEquip &&
            !dataBus.isInSafeZone)
        {
            StartEquip();
        }
    }


    private bool IsTransitioning()
    {
        return dataBus != null &&
               (dataBus.weaponEquipState ==
                    WeaponEquipState.Equipping ||
                dataBus.weaponEquipState ==
                    WeaponEquipState.Cancelling);
    }

    private void ForceCancelled()
    {
        if (dataBus == null)
            return;

        dataBus.weaponEquipState =
            WeaponEquipState.Cancelled;

        transitionTimer = 0f;

        RemoveEquipIntent();
        ApplyWeaponVisibility(false);
    }

    private float GetEquipDuration()
    {
        return DefaultEquipDuration;
    }

    private float GetCancelDuration()
    {
        return DefaultCancelDuration;
    }

    private void SubmitEquipIntent(
        EAnimSemantic semantic,
        float duration,
        int currentReplayId)
    {
        if (animationComposer == null)
            return;

        animationComposer.SubmitIntent(
            new AnimationIntent
            {
                root = characterRoot,
                intentSourceStateMatine =
                    AnimationIntentSourceStateMachine
                        .WeaponEquip,
                semantic = semantic,
                layer = AnimationLayer.UpperBody,
                priority = 70,
                exclusiveFullBody = false,
                oneShot = true,
                duration = duration,
                replayId = currentReplayId
            }
        );
    }

    private void RemoveEquipIntent()
    {
        if (animationComposer == null)
            return;

        animationComposer.RemoveIntent(
            AnimationIntentSourceStateMachine
                .WeaponEquip
        );
    }








    public void ApplyWeaponVisibility(bool visible)
    {
        if (weaponMount == null && characterRoot != null)
            weaponMount = characterRoot.GetComponentInChildren<ExternalWeaponMount>(true);

        if (weaponMount == null)
            return;

        weaponMount.SetWeaponVisible(visible);
    }

    private void SyncWeaponVisualState()
    {
        if (dataBus == null)
            return;

        /*
         * Equipping：已经拿出，显示。
         * Equipped：显示。
         * Cancelling：收起动画还没结束，继续显示。
         * Cancelled：隐藏。
         */
        bool shouldBeVisible =
            dataBus.weaponEquipState !=
            WeaponEquipState.Cancelled;

        ApplyWeaponVisibility(shouldBeVisible);
    }

    private string GetActiveWeaponName()
    {
        WeaponRuntimeData weapon =
            inventory != null
                ? inventory.ActiveWeapon
                : null;

        if (weapon == null ||
            weapon.config == null)
        {
            return "NULL";
        }

        return weapon.config.weaponName;
    }

    private void ConsumeWeaponSwitchInput()
    {
        if (dataBus == null)
            return;

        bool hasRequest = dataBus.TryConsumeWeaponSwitchInput(out WeaponSwitchInputRequestType requestType, out WeaponSlotId requestedSlot );

        if (!hasRequest)
            return;

        switch (requestType)
        {
            case WeaponSwitchInputRequestType.SelectSlot:
                {
                    bool accepted = RequestSwitchSlot(requestedSlot);
                    Debug.Log($"[WeaponEquip] Consume SelectSlot." +$" Slot={requestedSlot}," +$" Accepted={accepted}",characterRoot );
                    break;
                }

            case WeaponSwitchInputRequestType.CycleNext:
                {
                    bool accepted = RequestSwitchToNextSlot();
                    Debug.Log( $"[WeaponEquip] Consume CycleNext." +$" Accepted={accepted}", characterRoot);
                    break;
                }
        }
    }





    public void Exit()
    {
        /*
         * StateArbitrator 当前会对不活跃的并行状态机
         * 每帧调用 Exit，所以这里必须可重复执行。
         */
        RemoveEquipIntent();

        if (dataBus != null &&
            !hasPendingSlotSwitch)
        {
            dataBus.weaponEquipState =
                WeaponEquipState.Cancelled;
        }

        if (!hasPendingSlotSwitch)
        {
            ApplyWeaponVisibility(false);
        }
    }





}
```

## CharacterDataBus.cs

```csharp
using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;
/// <summary>
/// 瑙掕壊鐘舵€佹暟鎹€荤嚎锛岃繍琛屾椂鏁版嵁瀹瑰櫒
/// </summary>
public class CharacterDataBus
{
    //==============杈撳叆璁惧鎺ュ彛=============
    public enum InputDevice_AimType//鐬勫噯璁惧绫诲瀷鏋氫妇
    {
        Mouse,
        Gamepad
    }
    public enum RequestedFacingSource
    {
        None,
        Move,
        Aim,
        Attack,
        Target
    }

    //=============瑙掕壊鍩虹鏁版嵁==========
    public string characterName;
    public CharacterSO characterSO; // 璁板綍瑙掕壊鐨勫熀纭€SO


    // ============ 鐬勫噯杈撳叆 ============
    [Header("Aim Cursor")]
    public Vector3 aimCursorWorldPosition;
    public Vector3 aimDirection;
    public bool aimInputActiveThisFrame;

    [Header("Attack Input")]
    public bool hunterAttackHeld;
    public bool hunterAttackPressedThisFrame;
    public bool houndAttackHeld;
    public bool houndAttackPressedThisFrame;
    [Header("Combat Input")]
    public bool aimHeld;
    public bool aimPressedThisFrame;
    public bool aimReleasedThisFrame;
    public bool reloadPressedThisFrame;
    public bool attackPressed
    {
        get
        {
            return hunterAttackHeld || houndAttackHeld;
        }
    }
    public Vector2 moveInput { get; set; }//鍐欏叆锛欼nputHandler, 璇诲彇 缁欐墍鏈夐渶瑕佺Щ鍔ㄧ殑鑴氭湰
    /// <summary>
    /// 绉诲姩杈撳叆寮哄害銆?    /// 閿洏閫氬父杈撳嚭 0 鎴?1銆?    /// 鎽囨潌鍙互杈撳嚭 0~1銆?    /// 杩欎笉鏄鑹茬殑瀹為檯閫熷害銆?    /// </summary>
    public float moveInputAmount
    {
        get
        {
            return Mathf.Clamp01(moveInput.magnitude);
        }
    }
    /// <summary>
    /// 鏄惁鏈夌Щ鍔ㄨ緭鍏ャ€?    /// </summary>
    public bool hasMoveInput
    {
        get
        {
            return moveInput.sqrMagnitude > 0.01f;
        }
    }
    /// <summary>
    /// 褰撳墠绉诲姩绯荤粺鍒氶噰闆嗗埌鐨勭Щ鍔ㄩ€熷害銆?    /// 鐢?MovementState 鍐欏叆銆?    /// 渚嬪 Walk=2, Jog=4, Dash=6銆?    /// </summary>
    public float currentMoveSpeed { get; set; }

    /// <summary>
    /// 褰撳墠鏄惁澶勪簬娲昏穬鐨勭Щ鍔ㄧ姸鎬併€?    /// 娉ㄦ剰锛氳繖閲屽彧琛ㄧず绉诲姩鐘舵€侀€昏緫灞傛槸鍚︽椿璺冿紝涓嶇瓑浜庢槸鍚︽湁杈撳叆銆?    /// 澶栭儴鐩存帴璋冪敤鍗冲彲锛岄鐣欑粰浠ュ悗鍙兘鏈夌殑鏀瑰姩銆?    /// </summary>
    public bool isMovementActive
    {
        get
        {
            return currentMoveSpeed > 0.01f;
        }
    }





    public bool mouseMoving { get; set; } = false;//榧犳爣鏄惁鍦ㄧЩ鍔?//鍐欏叆锛欼nputHandler
    public InputDevice_AimType currentInputDevice { get; set; }//褰撳墠杈撳叆璁惧锛岄粯璁や负榧犳爣//鍐欏叆锛欼nputHandler
    public bool jumpPressed { get; set; }
    public bool jumpHeld { get; set; }
    public bool jumpPressedThisFrame { get; set; }
    public bool jumpReleasedThisFrame { get; set; }
    public float lastGroundedTime { get; set; }
    public float lastJumpPressedTime { get; set; }
    public bool dashPressed { get; set; }//鍐欏叆锛欼nputHandler
    public bool avoidPressed { get; set; }//鍐欏叆锛欼nputHandler
    public float gamepadMouseControlSensitivity { get; private set; } = 3000f; // 鎵嬫焺鐏垫晱搴︼紝璇诲彇鐢?    public bool aimInputTypeIsTriger { get; set; } = false;//鐬勫噯杈撳叆绫诲瀷鏋氫妇锛屾墜鍔ㄩ€夋嫨Trigger/Press)锛岄粯璁や负Press
    public bool crouchInputTypeIsTriger { get; set; } = false;//韫蹭笅杈撳叆绫诲瀷鏋氫妇锛屾墜鍔ㄩ€夋嫨Trigger/Press)锛岄粯璁や负Press
    public bool forceSlowWalkInputTypeIsTriger { get; set; } = true;//寮哄埗鎱㈣蛋杈撳叆绫诲瀷鏋氫妇锛屾墜鍔ㄩ€夋嫨Trigger/Press)锛岄粯璁や负Press
    //public bool isReloadKeyLongPress {  get; set; } = false;//鍐欏叆锛欼nputHandler
    public bool isForceWalkSlow {  get; set; } = false;////鍐欏叆锛欼nputHandler




    // ============ 鎴樻枟浣嶇疆 ============
    public Vector3 characterPosition { get; set; }//鍐欏叆锛欼CharacterStateMachine
    public Vector3 mousePositionOnScreen { get; set; }//鍐欏叆锛欼nputHandler

    public bool isGrounded { get; set; } = true;
    public Vector3 facingDirection{ get; set; }//鏈濆悜 //鍐欏叆锛歄rientationSystem


    public Facing currentFacing;
    public Facing targetFacing;
    public bool isFacingChanging;
    public int facingChangeId;

    public float moveTurnMovementLockUntilTime;
    public int moveTurnMovementLockId;

    public bool isMoveTurnMovementLocked
    {
        get
        {
            return Time.time < moveTurnMovementLockUntilTime;
        }
    }

    public Vector3 requestedFacingDirection { get; set; }

    public RequestedFacingSource requestedFacingSource = RequestedFacingSource.None;
    public BattleCamp battleCamp { get; set; }//瑙掕壊鎵€灞為樀钀ワ紝闇€瑕佺敱SO鏉ユ寚瀹?

    // ============ 鍏抽敭瑙掕壊鐘舵€?鏁板€煎瓨鍌ㄧ敤 float) ============


    //[Header("鏁板€肩姸鎬佸瓧娈?)]
    // 鍑虹敓鏈濆悜鐢遍粯璁ゅ嚭鍦?闈欐鐘舵€佹ā鍨嬫柟鍚戝弽锛岄渶瑕丼O鎻愪緵
    public Facing assetDefaultFacing;
    ///鎵€灞為樀钀ュ喅瀹氬師鏈湞鍚戯細鍥犱负鍘熺敾涓鑹?Skeleton 搴旇鐢?ScaleX 涓?1 杩樻槸 -1锛岄渶瑕佸湪瑙掕壊鍒濆鍖栨椂鐢卞閮ㄨ瀹氥€?    public float FinalVisualScaleX => (battleCamp == BattleCamp.MateSide)
        ? (assetDefaultFacing == Facing.Right ? 1f : -1f) // 鍙嬫柟闃佃惀涓庡師鍥炬湞鍚戜竴鑷村垯涓嶇炕杞?1)锛屽惁鍒欑炕杞?-1)
        : (assetDefaultFacing == Facing.Right ? -1f : 1f); // 鏁屾柟闃佃惀涓庡師鍥炬湞鍚戠浉鍙嶅垯缈昏浆(-1)锛屽惁鍒欎笉缈昏浆(1)

    //public float maxHp = 100f; // 鐢盨O鎻愪緵
    public float currentHp;    // 鎴樻枟涓慨鏀?    public float maxRemainHp;    //   娈嬫満鐢辨晫鏂归樀钀ヤ娇鐢紝鐢盨O鎻愪緵
    public float currentRemainHp;    //  娈嬫満鐢辨晫鏂归樀钀ヤ娇鐢紝鎴樻枟涓慨鏀?    public float stamina = 100f;

    //[Header("瀹炴椂鐘舵€佸瓧娈?)]
    // 娉ㄦ剰锛氳繖浜涘瓧娈佃BattleDirector鍒ゆ柇鏄惁闇€瑕佸嚭鍦?    public bool isChangeToRemain;  // 娈嬫満鐢辨晫鏂归樀钀ヤ娇鐢紝瑙掕壊琛€閲忓綊闆讹紝妯″瀷鏇挎崲涓烘畫鏈?    public bool isCompleteDisappear = false;   // 娈嬫満琛€閲忓綊闆讹紝瀹屽叏閫€鍦?    public bool isSlotForceChange;   // 浣嶇疆琚己鍒舵敼鍙橈紙濡傝鍑婚鍒板叾浠栦綅缃級
    public bool hasNewStatus; // 鍒氬彈鍒扮姸鎬侊紙濡傚嚮椋炵瓑锛?

    //new 鎴樻枟
    [Header("瑙掕壊鏁板€煎瓧娈?)]
    public CharacterStatProfileSO statProfile;

    [Header("瑙掕壊绛夌骇")]
    public int level = 1;


    /// <summary>
    /// 杩佺Щ鏈熼棿涓存椂瀛楁銆?    ///
    /// 褰撳墠瀛樺湪鐨勪富瑕佸敮涓€鐨勪复鏃跺啓鍏ヨ€呮槸 CharacterWeaponInventory銆?    /// 璇ュ瓧娈电Щ闄ゆ椂锛屽皢 Inventory 鍚屾杩佺Щ鍒版垬鏂楃郴缁熺殑鏂版灙姊拌澶囩郴缁熷彇浠ｃ€?    ///
    /// 浠ｇ爜涓嶈鐩存帴鍐欏叆姝や复鏃跺瓧娈点€?    /// </summary>
    [Header("褰撳墠姝﹀櫒")]
    [System.NonSerialized]
    public WeaponRuntimeData currentWeaponRuntimeData;

    public CharacterWeaponInventory weaponInventory;


    [Header("鑷姩閿佸畾")]
    public bool hasAttackTarget = false;
    public LockTargetPoint lockedAttackTarget;
    public EnemyRoot lockedAttackTargetRoot;
    public Vector3 attackTargetWorldPosition;

    //鍩虹SO
    public AnimationPlanMappingSO basicAnimationPlan;
    public List<AnimationPlanOverrideRuleSetSO> overrideAnimationPlan;


    // ============ 閫昏緫/鏄剧ず杞崲 (鍙鍙栫敤) ============

    /// <summary>
    /// 鐢ㄤ簬 UI 灞曠ず鐨勬暣鏁拌閲忥紝鍗充娇涓嶈冻0.1琛€涔熸樉绀轰负1銆?    /// </summary>
    public int displayHp => Mathf.CeilToInt(currentHp);

    /// <summary>
    /// 鏈€澶ц閲忔暣鏁板寲銆?    /// </summary>
    //public int displayMaxHp => Mathf.RoundToInt(maxHp);

    /// <summary>
    /// 鍒ゅ畾閫昏緫灞傦紝鍙湁褰撹閲忓皬浜?0 鏃讹紝鎵嶆妸isCompleteDisappear涓簍rue锛屾墠鍒ゅ畾姝讳骸
    /// </summary>

    public bool isAlive = true;

    /// <summary>
    /// 涓婁竴甯х殑鍓╀綑琛€閲忕櫨鍒嗘瘮锛岀敤浜庢洿鏂拌鏉?Image.fillAmount銆?    /// </summary>
    //public float hpPercentage => Mathf.Clamp01(currentHp / maxHp);



    // ============ 杩愬姩瑙掕壊鐘舵€?寮€鍏冲瀷) ============
    


    public bool isMoving { get; set; } = false;//瑙掕壊鏄惁鍦ㄧЩ鍔?/鍐欏叆锛欼CharacterStateMachine
    public bool isMovingBackward {  get; set; } = false;//瑙掕壊鏄惁鍦ㄥ悜鍚庣Щ鍔?/鍐欏叆锛欱ackWardMovement
    //public bool isAim { get; set; } = true;//瑙掕壊鏄惁澶勪簬鐬勫噯鐘舵€?//鍐欏叆锛欼nputHandler
    public bool isCrouch {  get; set; } = false;//瑙掕壊鏄惁澶勪簬鐬勫噯鐘舵€?//鍐欏叆锛欼nputHandler
    //public bool isHoldWeapon { get; set; } = false;//瑙掕壊鏄惁澶勪簬鎸佹灙瑁呭鐘舵€?
    [Header("Combat Runtime")]
    public bool isAiming;
    public bool isReloading;
    public bool isHoundAttacking;
    public bool isHunterAttacking;

    public bool isCharacterAttacking => isHoundAttacking || isHunterAttacking;

    public float lastCombatIntentTime;
    public float autoHolsterDelay = 2.5f;

    public bool canMove { get; set; } = true;
    public bool canJump { get; set; } = true;

    public HoundCombatActionType currentHoundCombatAction = HoundCombatActionType.None;










    // ============ 瑙掕壊鐘舵€?鐘舵€佹満鐢? ============
    public EState currentMovementState { get; set; } = EState.Idle;
    public EState currentJumpState { get; set; } = EState.Jump_None;

    /// <summary>
    /// 璺宠穬鏈熼棿鏄惁闃绘 Move 鏈濆悜銆?    /// 瑕嗙洊 Jump_Start / Jump_Rising / Jump_Fall / Jump_Land銆?    /// CompleteJump 鍚庡彉涓?false锛屽厑璁稿湴闈?Move 鎺ョ銆?    /// </summary>
    public bool blocksMoveFacingByJump
    {
        get
        {
            return currentJumpState == EState.Jump_Start
                || currentJumpState == EState.Jump_Rising
                || currentJumpState == EState.Jump_Fall
                || currentJumpState == EState.Jump_Land;
        }
    }




    //===============姝﹀櫒瑁呭鐘舵€?===============
    [Header("Weapon Equip Runtime")]
    public WeaponEquipState weaponEquipState = WeaponEquipState.Cancelled;
    public WeaponPostureState weaponPostureState = WeaponPostureState.None;

    public WeaponSwitchInputRequestType weaponSwitchInputRequestType =WeaponSwitchInputRequestType.None;
    public WeaponSlotId requestedWeaponSlot =WeaponSlotId.Slot1;


    /// <summary>
    /// 鏄惁姝ｅ浜庤澶?鍗镐笅杩囩▼涓€?    /// </summary>
    public bool isWeaponEquipTransitioning
    {
        get
        {
            return weaponEquipState == WeaponEquipState.Equipping
                || weaponEquipState == WeaponEquipState.Cancelling;
        }
    }

    /// <summary>
    /// 鍒ゅ畾姝﹀櫒鏄惁鍙互浣跨敤銆?    /// 濡傛灉姝ｅ湪鐬勫噯銆佹敾鍑荤瓑琛屼负搴旇鍙敤銆?    /// </summary>
    public bool canUseWeapon
    {
        get
        {
            return isAlive
                && currentWeaponRuntimeData != null
                && weaponEquipState == WeaponEquipState.Equipped
                && !isInSafeZone
                && !isWeaponEquipTransitioning;
        }
    }

    /// <summary>
    /// 鏄惁鍦ㄥ畨鍏ㄥ尯鍩熴€?    /// 瀹夊叏鍖哄煙鍐呬細鑷姩瑙﹀彂 CancelWeapon銆?    /// </summary>
    public bool isInSafeZone;

    /// <summary>
    /// 鎸変笅 R 閿Е鍙戯紝鐢?InputHandler 鍐欏叆銆?    /// </summary>
    public bool weaponToggleLongPressedThisFrame;
    /// <summary>
    /// 澶栭儴绯荤粺瑙﹀彂鐨勮嚜鍔ㄥ彇娑堟鍣ㄨ姹傘€?    /// 姣斿瀹夊叏鍖哄煙鍏ュ満銆佽嚜鍔ㄦ敹鍥炴鍣ㄧ瓑銆?    /// </summary>
    public bool requestCancelWeapon;









    // ============ 鎴樻枟鏁版嵁 ============
    public int comboCount { get; set; }
    public float lastAttackTime { get; set; }

    public bool blockMoveFacingByWeapon;
    public float blockMoveFacingExpireTime;
    public float lastWeaponFacingActivityTime;

    /// <summary>
    /// 鏈€鍚庝竴娆℃湁鏁堟鍣ㄦ湞鍚戞柟鍚戙€?    /// 褰撳厜鏍囪繘鍏ユ鍖烘垨 Aim/Fire 鐘舵€佸垏鎹㈡椂锛屼繚鎸佹鏂瑰悜銆?    /// </summary>
    public Vector3 lastWeaponFacingDirection { get; set; } = Vector3.left;
    public bool hasLastWeaponFacingDirection { get; set; } = false;


    // ============ 杈呭姪瀛楁 ============



    //璇诲彇鏁版嵁鐨勭壒娈婃柟娉曪紝闇€琚獴attleManager璋冪敤鏉ユ牴鎹鑹蹭笓灞炴潵璁剧疆
    public void LoadFromSO(CharacterSO so)
    {
        characterSO = so;
        this.statProfile = so.characterStatProfileSO;
        characterName = so.characterName;
        this.assetDefaultFacing = so.assetDefaultFacing;
        battleCamp = so.battleCamp;
        //maxHp = so.maxHp;
        maxRemainHp = so.maxRemainHp;

        basicAnimationPlan = so.basicAnimationPlanMapping;
        overrideAnimationPlan = so.animationPlanOverrideRuleSet;
        // 鍏朵粬鏁版嵁鍚屾...




    }


    #region 姝﹀櫒鍒囨崲璇锋眰杈呭姪鏂规硶
    /// <summary>
    /// 杈撳叆绯荤粺鐩存帴閫夋嫨涓€涓鍣ㄦЫ浣嶃€?    /// DataBus 鍙瓨鍌ㄨ姹傦紝涓嶈礋璐ｆ墽琛屽垏鎹€?    /// </summary>
    public void RequestWeaponSlotSwitch(
        WeaponSlotId slotId)
    {
        requestedWeaponSlot = slotId;

        weaponSwitchInputRequestType =
            WeaponSwitchInputRequestType.SelectSlot;
    }

    /// <summary>
    /// 杈撳叆绯荤粺寰幆鍒囨崲鍒颁笅涓€涓鍣ㄦЫ浣嶃€?    /// </summary>
    public void RequestWeaponSlotCycleNext()
    {
        weaponSwitchInputRequestType =
            WeaponSwitchInputRequestType.CycleNext;
    }

    /// <summary>
    /// 瑁呭鐘舵€佹満娑堣垂褰撳墠姝﹀櫒鍒囨崲璇锋眰銆?    /// 娑堣垂鍚庤姹傛竻绌恒€?    /// </summary>
    public bool TryConsumeWeaponSwitchInput(
        out WeaponSwitchInputRequestType requestType,
        out WeaponSlotId requestedSlot)
    {
        requestType = weaponSwitchInputRequestType;
        requestedSlot = requestedWeaponSlot;

        if (requestType ==
            WeaponSwitchInputRequestType.None)
        {
            return false;
        }

        weaponSwitchInputRequestType =
            WeaponSwitchInputRequestType.None;

        return true;
    }
    #endregion


}
```

## ECharacter.cs

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum Facing { Left = -1, Right = 1 }

public enum WeaponDefaultFacing
{
    Right,
    Left
}

public enum WeaponSwitchInputRequestType
{
    None = 0,

    /// <summary>
    /// 鎵嬫焺閫夋嫨鎸囧畾姝﹀櫒妲戒綅
    /// </summary>
    SelectSlot = 1,

    /// <summary>
    /// 鍒囨崲鍒颁笅涓€涓鍣ㄦЫ浣?    /// </summary>
    CycleNext = 2
}

public enum CombatDamageValueMode
{
    /// <summary>
    /// 瀹屽叏浣跨敤鏁堟灉璧勪骇涓殑鍥哄畾浼ゅ鍊笺€?    /// 閫傚悎鏋韪㈠嚮绛夌嫭绔嬪姩浣溿€?    /// </summary>
    Flat,

    /// <summary>
    /// 浣跨敤姝﹀櫒 CommonCoreStats.damage 涔樺€嶇巼銆?    /// 閫傚悎鏅€氭垬鏂椾激瀹崇粨绠椼€?    /// </summary>
    WeaponCoreMultiplier,

    /// <summary>
    /// 鍥哄畾鍊煎姞涓婃鍣ㄤ激瀹充箻鍊嶇巼銆?    /// </summary>
    FlatPlusWeaponCoreMultiplier
}

public enum AnimationLayer
{
    /// <summary>
    /// 鍩虹鍏ㄨ韩灞傘€?    /// 鐢ㄤ簬 Idle / Walk / Jog / Dash 绛夈€?    /// 绗竴闃舵绉诲姩鐘舵€佹満涓昏浣跨敤姝ゅ眰銆?    /// </summary>
    BaseFullBody = 0,

    /// <summary>
    /// 涓嬪崐韬眰銆?    /// 绗簩闃舵濡?Jog_LowerOnly / Walk_LowerOnly銆?    /// </summary>
    LowerBody = 10,

    /// <summary>
    /// 涓婂崐韬眰銆?    /// 绗簩闃舵濡?Aim / Fire / Reload / UpperOnly Attack銆?    /// </summary>
    UpperBody = 20,

    /// <summary>
    /// 楂樹紭鍏堢骇鍏ㄨ韩鍔ㄤ綔灞傘€?    /// 鐢ㄤ簬鎵╁睍濡傛妧鑳姐€佸彈鍑汇€佸鍐虫妧銆?    /// 绗竴闃舵 AutoAttack 浣跨敤姝ゅ眰銆?    /// </summary>
    FullBodyAction = 30,

    /// <summary>
    /// 鍙犲姞灞傘€?    /// 濡傞】甯с€佸彈鍑汇€侀棯鍥炵瓑涓嶅彇浠ｈ〃鎯呫€?    /// </summary>
    Additive = 40
}

public enum AnimationIntentSourceStateMachine
{
    None = 0,

    Movement = 10,       // 绉诲姩鐘舵€佹満
    HoundAttack = 15,    // 杩戞垬鏀诲嚮鎰忓浘鏉ユ簮
    HoundWeaponPose = 16,// 鏋棰勫 pose 鎰忓浘鏉ユ簮锛堢瀯鍑嗙瓑锛?    HoundWeaponAction = 17,// 鏋瀹為檯琛屼负鎰忓浘鏉ユ簮锛堝皠鍑汇€佹崲寮癸級

    Jump = 25,           // 璺宠穬鐘舵€佹満鎰忓浘鏉ユ簮

    Skill = 40,
    Hurt = 50,
    Status = 60,
    Debug = 90,

    WeaponEquip,   // 姝﹀櫒瑁呭鐘舵€佹満鎻愪氦瑁呭/鍗镐笅鍔ㄧ敾鎰忓浘锛屽浐瀹氫紭鍏堢骇鍚庣紑
    WeaponPosture, // 姝﹀櫒濮挎€佺姸鎬佹満鎻愪氦鏀舵灙/鎸佹灙鍔ㄧ敾鎰忓浘锛屼綆浜庤涓轰紭鍏堢骇
}

public enum WeaponPostureState // 姝﹀櫒濮挎€?{
    None,
    LowReady
}

public enum EAnimSemantic // 鍔ㄧ敾閫昏緫 ID锛岀姸鎬佹満鐢ㄤ簬鏍囪鐘舵€?{
    None,

    // ================= 绉诲姩鐩稿叧锛堟彁浜よ繖浜涳級 =================
    Idle,
    Walk,
    Jog,
    JogFast,
    Dash,
    Walk_Back,
    Jog_Back,

    TurnAround,
    MoveStart,
    MoveStop,
    AimEnter,
    AimExit,

    // ================= 璺宠穬鐩稿叧 =================
    Jump_Start,
    Jump_Rising,
    Jump_Falling,
    Jump_Landing,

    // ================= 姝﹀櫒鐩稿叧鍏ㄨ韩鍔ㄤ綔 =================
    Gun_AimPose,
    Gun_HipFirePose,    // 鑵板皠鎸佹灙濮挎€?    Gun_Fire,           // 鐬勫噯灏勫嚮
    Gun_Reload,

    Weapon_LowReady,
    Weapon_Equip,
    Weapon_Cancel,

    Attack_Normal_1,
    Attack_Normal_2,
    Attack_Normal_3,
}

public enum BattleCamp
{
    MateSide,    // 鍙嬫柟闃佃惀
    EnemySide,   // 鏁屾柟闃佃惀
}

public enum EnemyRank
{
    Minion,
    Elite,
    Boss
}

public enum WeaponEquipState
{
    Cancelled,      // 琚腑鏂?鍙栨秷锛屼笉鎴愬姛涔熶笉澶辫触
    Equipping,      // 姝ｅ湪鎷垮嚭姝﹀櫒
    Equipped,       // 姝﹀櫒瑁呭鎴愬姛
    Cancelling      // 姝ｅ湪鏀跺洖姝﹀櫒
}

public enum EAnimationLogicState // 鍔ㄧ敾閫昏緫 ID锛屽搴旇鑹?Spine 涓寘鍚殑鍔ㄧ敾鍚嶇О
{
    #region 杩戞垬鏀诲嚮绫伙紙绗?浣嶅ご閮級
    Attack_Normal_1 = 11,
    Attack_Normal_2 = 12,
    Attack_Normal_3 = 13,
    #endregion

    // ================= 鍩虹绉诲姩 =================
    #region 绉诲姩绫伙紙绗?浣嶅ご閮級
    Walk = 21,
    WalkBack = 22,
    Jog = 23,
    JogBack = 24,
    JogFast = 25,
    #endregion

    Dash,
    Idle,
    Crouch,

    // ================= 鏋绯荤粺 =================
    #region 鏋绫伙紙绗?浣嶅ご閮級
    Aim = 31,
    Fire = 32,
    Reload = 33,
    Weapon_LowReady = 34,
    NotAimShootReady = 35,

    Avoid_Full,

    // ================= 璺宠穬 =================
    Jump_PreStart_Full,
    Jump_Start_Full,
    Jump_Rising_Full,
    Jump_Peak_Full,
    JumpSecond__Air_Full,
    Jump_Fall_Full,
    Jump_Land_Full,
    #endregion

    // ================= 杩囨浮鍔ㄧ敾 =================
    #region 杩囨浮绫伙紙绗?00浣嶅ご閮級
    Jog_End_Full,    // 鍋滄
    TurnAround,      // 杞韩

    AimToGundown = 501,
    GundownToAim = 502,
    GundownToNotAimShoot = 503,
    NotAimShootToGundown = 504,

    IdleToJogFast = 505,
    IdleToJogSlow = 506,
    IdleToWalk = 507,
    JogFastToIdle = 508,
    JogSlowToIdle = 509,
    WalkToIdle = 510,
    #endregion

    CombatIdle,   // 鎴樻枟寰呮満锛岃鑹蹭細鏍规嵁褰撳墠鑱屼笟鐨勬寔鏋Э鎬佹挱鏀?    SkillNotice,   // 杩涘叆鎶€鑳介€夋嫨锛屼絾鍊兼湭璁╄鑹查€夋嫨鏃讹紝瑙掕壊浼氭湁涓€涓彁绀哄姩浣?    SkillReady,    // 閫夋嫨浜嗘妧鑳藉苟閫夋嫨鐩爣锛岃鎶€鑳借璁板綍锛屾挱鏀句竴涓妧鑳藉緟鏈洪澶囧姩浣?
    // =============== 鎶€鑳?=============== 鎵ц闃舵锛屾瘡涓槸涓€涓彧鏈変竴甯х殑鎶€鑳藉姩浣?    Skill_1, Skill_2, Skill_3, Skill_4, Skill_5, Skill_6, Skill_7, Skill_8,
    Skill_9, Skill_10, Skill_11, Skill_12, Skill_13, Skill_14, Skill_15, Skill_16,

    None
}

public enum EState
{
    // ============== 绉诲姩 ==============
    Idle,
    Walk,
    Jog,
    JogFast,
    Dash,
    Walk_Back,
    Jog_Back,
    Ceouch,

    // ============== 璺宠穬 ==============
    Jump_None,
    Jump_PreStart,
    Jump_Start,
    Jump_Rising,
    Jump_Peak,
    JumpSecond__Air,
    Jump_Fall,
    Jump_Land,
}

public enum CharacterStateType // 鏈夊灏戜釜鐘舵€佸氨鍐欏灏戜釜鏋氫妇锛岄渶瑕佸拰瀹為檯鐨勭姸鎬佽剼鏈繚鎸佷竴鑷?{
    // ============ 鍩虹绉诲姩 ============
    Idle,
    Walk,
    Run,
    Crouch,

    // ============ 鐗规畩鍔ㄤ綔 ============
    TurnAround,
    BrakeStop,
    Dash,

    // ============ 璺宠穬绯诲垪 ============
    Jump_PreStart,
    Jump_Start,
    Jump_Rising,
    Jump_Peak,
    Jump_Fall,
    Jump_Land,

    // ============ 鎴樻枟绯荤粺 ============
    CombatIdleState,
    SkillSelect,
    SkillExecute,
    SkillReact,

    // ============ 绯荤粺鐘舵€?============
    Hurt,
    Stun,
    Death,
    Interact,

    // ============ 鑷畾涔夌姸鎬侊紙鎵╁睍鐢級 ============
    Custom
}

public enum CharacterStats
{
    // 鍩虹
    MaxHP,
    Defense,
    Shield,
    MaxStamina,

    // 绉诲姩
    MoveSpeed,
    WalkSpeed,
    JogSpeed,
    JogFastSpeed,
    WalkBackSpeed,
    JogBackSpeed,
    DashCooldown,

    // 閫氱敤鎴樻枟鍔犳垚
    DamageBonus,
    CritChanceBonus,
    CritDamageBonus,

    // 鏋鍔犳垚
    FirearmDamageBonus,
    FireRateBonus,
    ReloadSpeedBonus,
    RecoilControl,
    AccuracyBonus,
    ProjectileSpeedBonus,

    // 杩戞垬鍔犳垚
    MeleeDamageBonus,
    MeleeAttackSpeedBonus,
    MeleeStaminaCostReduction,

    // 鎰熺煡 / 杈呭姪
    SearchRange,
    AimSpeedBonus
}

public enum StatModifierMode
{
    Add,            // 鍔犳硶锛?10 绛夛級
    PercentAdd,     // 鐧惧垎姣斿姞娉曪紙+20%锛?    Multiply,       // 涔樻硶锛?1.2锛?    Override        // 瑕嗙洊锛堝瀹氭椂 MoveSpeed = 0锛?}

public enum ActionEffectType
{
    Damage,
    Heal,
    SlotMove,
    Buff
}

public enum MoveType
{
    None,
    Swap,
    Push
}

public enum WeaponType
{
    Firearm,
    Melee
}

public enum FirearmCategory
{
    None = 0,

    Pistol,         // 鎵嬫灙
    SubmachineGun,  // 鍐查攱鏋?    Rifle,          // 姝ユ灙
    Shotgun,        // 闇板脊鏋?    Sniper,         // 鐙欏嚮鏋?    Energy,         // 鑳介噺姝﹀櫒
    Special         // 鐗规畩姝﹀櫒
}

public enum GunFireModeType
{
    SemiAuto,       // 鍗婅嚜鍔細姣忔鎸夐敭鍙皠鍑讳竴娆?    Burst,          // 鐐瑰皠锛氳繛缁皠鍑诲浐瀹氬彂鏁?    FullAuto,       // 鍏ㄨ嚜鍔細鎸変綇鎸佺画灏勫嚮
    Charge,         // 钃勫姏锛氭寜浣忓悗閲婃斁鎵嶅紑鐏?    Beam,           // 鍏夋潫锛氭寜浣忔湡闂存寔缁紑鐏?    Special         // 鐗规畩
}

public enum GunHitType
{
    Hitscan,
    Projectile,
}

public enum HoundCombatActionType
{
    None,
    Aim,
    Fire,
    Reload,
    /// <summary>
    /// 鏋鏈瀯鍑嗘椂鐨勮繎鎴樻敾鍑伙紙韪㈠嚮绛夛級銆?    /// 褰撳墠瀹為檯鎾斁 Attack_Normal_1 / Attack_Normal_2 搴忓垪銆?    /// </summary>
    FirearmMelee,
    /// <summary>
    /// 鐙珛瑁呭浜嗚繎鎴樻鍣ㄦ椂鐨勮繎鎴樻敾鍑汇€?    /// 褰撳墠鍙湁璁捐锛屾殏鏈疄鐜般€?    /// </summary>
    Melee,
    /// <summary>
    /// 杩戞垬鏀诲嚮鐨勬妧鑳藉寲鍔熻兘銆?    /// 褰撳墠鍙湁璁捐锛屾殏鏈疄鐜般€?    /// </summary>
    MeleeSpecial
}

public enum TransitionSuppressReason
{
    None,
    MovementInputUnstable,
    MovementDirectionFlip,
}

public enum WeaponSlotId
{
    Slot1 = 0,
    Slot2 = 1,
    Slot3 = 2
}
```

