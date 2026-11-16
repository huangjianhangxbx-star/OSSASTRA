/* 功能：共享输入定义；每个消费者克隆独立Action，不共享启用状态。绑定只存在配置，不在运行时补键。 */
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
namespace Ossastra.Characters
{
 [CreateAssetMenu(menuName="OSSASTRA/Input/Player Input Layout")]
 public sealed class PlayerInputLayoutSO:ScriptableObject
 {
  [Tooltip("正式横版输入。由同一个按设备路由解析组合键、向上交互与消费。")]
  public bool postureInputRouting;

  [Header("移动 / 键盘与手柄方向")]
  [Tooltip("读取二维移动向量；可配置2D Vector组合或手柄左摇杆。")]
  public InputAction move=new InputAction("Move",InputActionType.Value);
  [Header("鼠标位置 / 屏幕坐标")]
  public InputAction pointer=new InputAction("Pointer",InputActionType.Value);
  [Header("手柄瞄准方向 / 右摇杆")]
  public InputAction aim=new InputAction("Aim",InputActionType.Value);
  [Header("右手 / 主手操作")]
  [Tooltip("正式横版为鼠标左键／手柄 X；实际行为由当前主手装备定义决定。")]
  public InputAction rightHand=new InputAction("RightHandAction",InputActionType.Button);
  [Header("左手 / 副手操作")]
  [Tooltip("正式横版为鼠标右键／手柄 RT；按下、持续或长按的行为由装备SO绑定决定。")]
  public InputAction leftHand=new InputAction("LeftHandAction",InputActionType.Button);
  [Header("本体闪避")]
  [Tooltip("正式横版为左右 Shift／手柄 B。")]
  public InputAction dodge=new InputAction("Dodge",InputActionType.Button);
  [Header("普通跳跃")]
  [Tooltip("正式横版为空格／手柄 A。")]
  public InputAction jump=new InputAction("Jump",InputActionType.Button);
  [Header("技能1 / 正式横版 Q / Y")]
  public InputAction skillOne=new InputAction("SkillOne",InputActionType.Button);
  [Header("技能2 / 固定E槽 / LB + Y")]
  public InputAction skillTwo=new InputAction("SkillTwo",InputActionType.Button);
  [Header("技能3 / R / LB + B")]
  public InputAction skillThree=new InputAction("SkillThree",InputActionType.Button);
  [Header("场景交互 / 正式横版 W / 左摇杆上")]
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
  [Tooltip("正式横版通过槽位引用请求道具使用。")]
  public InputAction quickSlotOne=new InputAction("QuickSlotOne",InputActionType.Button);
  [Header("道具栏2引用 / 2 / RB + Y")]
  public InputAction quickSlotTwo=new InputAction("QuickSlotTwo",InputActionType.Button);
  [Header("道具栏3引用 / 3 / RB + B")]
  public InputAction quickSlotThree=new InputAction("QuickSlotThree",InputActionType.Button);
  [Header("道具栏4引用 / 4 / RB + A")]
  public InputAction quickSlotFour=new InputAction("QuickSlotFour",InputActionType.Button);
  internal InputAction ActionForPostureCommand(PostureInputCommand command) => command switch
  {
   PostureInputCommand.MainHand => rightHand,
   PostureInputCommand.Offhand => leftHand,
   PostureInputCommand.Jump => jump,
   PostureInputCommand.Dodge => dodge,
   PostureInputCommand.SkillOne => skillOne,
   PostureInputCommand.SkillTwo => skillTwo,
   PostureInputCommand.SkillThree => skillThree,
   PostureInputCommand.WeaponMode => weaponMode,
   PostureInputCommand.SwitchCharacter => switchCharacter,
   PostureInputCommand.Heal => heal,
   PostureInputCommand.Interact => interact,
   PostureInputCommand.QuickSlotOne => quickSlotOne,
   PostureInputCommand.QuickSlotTwo => quickSlotTwo,
   PostureInputCommand.QuickSlotThree => quickSlotThree,
   PostureInputCommand.QuickSlotFour => quickSlotFour,
   _ => null
  };
  // 只枚举作者配置；用于按设备识别已消费的组合键，不启用这些共享SO Action。
  public IEnumerable<InputAction> ConfiguredActions()
  {
   yield return move; yield return pointer; yield return aim; yield return rightHand; yield return leftHand;
   yield return dodge; yield return jump; yield return skillOne; yield return skillTwo; yield return skillThree;
   yield return interact; yield return heal; yield return switchCharacter; yield return inventory;
   yield return weaponMode; yield return menu; yield return quickSlotOne; yield return quickSlotTwo; yield return quickSlotThree; yield return quickSlotFour;
  }
  public static void Replace(ref InputAction target,InputAction source){target?.Disable();target?.Dispose();target=source.Clone();}
  public void Apply(CharacterInputHandler target)
  {Replace(ref target.movementAction,move);Replace(ref target.mouseAction,pointer);Replace(ref target.gamepadAimAction,aim);Replace(ref target.rightHandAction,rightHand);Replace(ref target.leftHandAction,leftHand);Replace(ref target.dodgeAction,dodge);if(jump!=null)Replace(ref target.jumpAction,jump);Replace(ref target.skillOneAction,skillOne);Replace(ref target.skillTwoAction,skillTwo);Replace(ref target.skillThreeAction,skillThree);Replace(ref target.weaponModeAction,weaponMode);}
 }
}



