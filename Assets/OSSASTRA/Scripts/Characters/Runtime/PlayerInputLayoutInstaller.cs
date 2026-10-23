/* 功能：场景输入配置接线；在各消费者Awake之前克隆共享定义。菜单不暂停世界。 */
using UnityEngine;
using UnityEngine.InputSystem;
using Ossastra.Inventory;
using Ossastra.UserInterface;
namespace Ossastra.Characters
{
 [DefaultExecutionOrder(-500)]
 public sealed class PlayerInputLayoutInstaller:MonoBehaviour
 {
  public PlayerInputLayoutSO layout;
  public CharacterControlCoordinator control;
  public PartyConsumableInput consumables;
  public InventoryInteraction interaction;
  public InventoryView inventory;
  public InputAction menuAction=new InputAction("Menu",InputActionType.Button);
  private void Awake()
  {
   if(layout==null){Debug.LogError("PlayerInputLayoutInstaller.layout missing",this);enabled=false;return;}
   foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var input in root.GetComponentsInChildren<CharacterInputHandler>(true))input.inputLayout=layout;
   if(control!=null)PlayerInputLayoutSO.Replace(ref control.switchAction,layout.switchCharacter);
   if(consumables!=null)PlayerInputLayoutSO.Replace(ref consumables.healingAction,layout.heal);
   if(interaction!=null)PlayerInputLayoutSO.Replace(ref interaction.interactAction,layout.interact);
   if(inventory!=null)PlayerInputLayoutSO.Replace(ref inventory.toggleAction,layout.inventory);
   var hotbar=GetComponent<InventoryHudBridge>();
   if(hotbar!=null&&hotbar.digitActions.Length>=4){var refs=new[]{layout.quickSlotOne,layout.quickSlotTwo,layout.quickSlotThree,layout.quickSlotFour};for(int i=0;i<4;i++)PlayerInputLayoutSO.Replace(ref hotbar.digitActions[i],refs[i]);}
   PlayerInputLayoutSO.Replace(ref menuAction,layout.menu);
  }
  private void OnEnable(){menuAction.Enable();}
  private void Update(){if(menuAction.WasPressedThisFrame()&&inventory!=null)inventory.ToggleMenu();}
  private void OnDisable(){menuAction.Disable();}
  private void OnDestroy(){menuAction.Dispose();}
 }
}

