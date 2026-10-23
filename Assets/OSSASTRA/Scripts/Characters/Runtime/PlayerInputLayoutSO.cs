/* 功能：共享输入定义；每个消费者克隆独立Action，不共享启用状态。绑定只存在配置，不在运行时补键。 */
using UnityEngine;
using UnityEngine.InputSystem;
namespace Ossastra.Characters
{
 [CreateAssetMenu(menuName="OSSASTRA/Input/Player Input Layout")]
 public sealed class PlayerInputLayoutSO:ScriptableObject
 {

  public InputAction move=new InputAction("Move",InputActionType.Value),pointer=new InputAction("Pointer",InputActionType.Value),aim=new InputAction("Aim",InputActionType.Value);
  public InputAction rightHand=new InputAction("RightHandAction",InputActionType.Button),leftHand=new InputAction("LeftHandAction",InputActionType.Button),dodge=new InputAction("Dodge",InputActionType.Button);
  public InputAction skillOne=new InputAction("SkillOne",InputActionType.Button),skillTwo=new InputAction("SkillTwo",InputActionType.Button),skillThree=new InputAction("SkillThree",InputActionType.Button);
  public InputAction interact=new InputAction("Interact",InputActionType.Button),heal=new InputAction("Heal",InputActionType.Button),switchCharacter=new InputAction("SwitchCharacter",InputActionType.Button),inventory=new InputAction("Inventory",InputActionType.Button),weaponMode=new InputAction("WeaponMode",InputActionType.Button),menu=new InputAction("Menu",InputActionType.Button);
  public InputAction quickSlotOne=new InputAction("QuickSlotOne",InputActionType.Button),quickSlotTwo=new InputAction("QuickSlotTwo",InputActionType.Button),quickSlotThree=new InputAction("QuickSlotThree",InputActionType.Button),quickSlotFour=new InputAction("QuickSlotFour",InputActionType.Button);
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



