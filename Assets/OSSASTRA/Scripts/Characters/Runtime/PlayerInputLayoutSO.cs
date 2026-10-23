/* 功能：共享输入定义；每个消费者克隆独立Action，不共享启用状态。绑定只存在配置，不在运行时补键。 */
using UnityEngine;
using UnityEngine.InputSystem;
namespace Ossastra.Characters
{
 [CreateAssetMenu(menuName="OSSASTRA/Input/Player Input Layout")]
 public sealed class PlayerInputLayoutSO:ScriptableObject
 {

  [Header("移动 / 键盘与手柄方向")]
  [Tooltip("读取二维移动向量；可配置2D Vector组合或手柄左摇杆。")]
  public InputAction move=new InputAction("Move",InputActionType.Value);
  [Header("鼠标位置 / 屏幕坐标")]
  public InputAction pointer=new InputAction("Pointer",InputActionType.Value);
  [Header("手柄瞄准方向 / 右摇杆")]
  public InputAction aim=new InputAction("Aim",InputActionType.Value);
  [Header("右手 / 主手操作")]
  [Tooltip("当前约定：鼠标左键、手柄RT；实际行为由当前主手装备定义决定。")]
  public InputAction rightHand=new InputAction("RightHandAction",InputActionType.Button);
  [Header("左手 / 副手操作")]
  [Tooltip("当前约定：鼠标右键、手柄LT；按下、持续或长按的行为由装备SO绑定决定。")]
  public InputAction leftHand=new InputAction("LeftHandAction",InputActionType.Button);
  [Header("本体闪避")]
  [Tooltip("当前约定：空格、手柄B（Button East）。")]
  public InputAction dodge=new InputAction("Dodge",InputActionType.Button);
  [Header("技能1 / Q / LB + X")]
  public InputAction skillOne=new InputAction("SkillOne",InputActionType.Button);
  [Header("技能2 / 固定E槽 / LB + Y")]
  public InputAction skillTwo=new InputAction("SkillTwo",InputActionType.Button);
  [Header("技能3 / R / LB + B")]
  public InputAction skillThree=new InputAction("SkillThree",InputActionType.Button);
  [Header("场景交互 / F / 手柄A")]
  [Tooltip("请求当前合法上下文，例如拾取、容器、篝火；不是每个对象各自响应一次。")]
  public InputAction interact=new InputAction("Interact",InputActionType.Button);
  [Header("共享血瓶 / Y / LB + A")]
  public InputAction heal=new InputAction("Heal",InputActionType.Button);
  [Header("切换实控角色 / Z / LB + RB")]
  public InputAction switchCharacter=new InputAction("SwitchCharacter",InputActionType.Button);
  [Header("展开背包 / Tab / Start")]
  public InputAction inventory=new InputAction("Inventory",InputActionType.Button);
  [Header("主手动作模式切换 / LT + RT")]
  [Tooltip("切换装备已登记的动作组；仍须满足现有换模式资格。")]
  public InputAction weaponMode=new InputAction("WeaponMode",InputActionType.Button);
  [Header("非暂停菜单 / Select")]
  public InputAction menu=new InputAction("Menu",InputActionType.Button);
  [Header("道具栏1引用 / 1 / RB + X")]
  [Tooltip("目前为引用选择；连续使用、投掷瞄准流程尚未接入。")]
  public InputAction quickSlotOne=new InputAction("QuickSlotOne",InputActionType.Button);
  [Header("道具栏2引用 / 2 / RB + Y")]
  public InputAction quickSlotTwo=new InputAction("QuickSlotTwo",InputActionType.Button);
  [Header("道具栏3引用 / 3 / RB + B")]
  public InputAction quickSlotThree=new InputAction("QuickSlotThree",InputActionType.Button);
  [Header("道具栏4引用 / 4 / RB + A")]
  public InputAction quickSlotFour=new InputAction("QuickSlotFour",InputActionType.Button);
  public static void Replace(ref InputAction target,InputAction source){target?.Disable();target?.Dispose();target=source.Clone();}
  public void Apply(CharacterInputHandler target)
  {Replace(ref target.movementAction,move);Replace(ref target.mouseAction,pointer);Replace(ref target.gamepadAimAction,aim);Replace(ref target.rightHandAction,rightHand);Replace(ref target.leftHandAction,leftHand);Replace(ref target.dodgeAction,dodge);Replace(ref target.skillOneAction,skillOne);Replace(ref target.skillTwoAction,skillTwo);Replace(ref target.skillThreeAction,skillThree);Replace(ref target.weaponModeAction,weaponMode);}
 }
 /* 明确的手柄修饰键规则：组合期间基础面键不透传；键鼠不受影响，多手柄取实际Action设备。 */
 public static class GamepadInputPolicy
 {
  public static bool Modified(InputAction action)=>action?.activeControl?.device is Gamepad pad&&(pad.leftShoulder.isPressed||pad.rightShoulder.isPressed);
  public static bool WeaponChord(InputAction action)=>action?.activeControl?.device is Gamepad pad&&pad.leftTrigger.isPressed&&pad.rightTrigger.isPressed;
 }
}



