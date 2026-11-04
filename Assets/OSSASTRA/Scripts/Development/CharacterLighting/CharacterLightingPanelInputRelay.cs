/* 功能：把角色光感面板的指针、按下与导航选择转交唯一面板owner，并显示对应中文说明。
 * 边界：不读取设备、不切换主控、不直接修改光感或业务；仅维护本控件的输入租约状态。
 * 生命周期：禁用/销毁撤回本控件登记，面板据实际剩余交互释放自己的协调器与镜头租约。
 */
using UnityEngine;
using UnityEngine.EventSystems;
namespace Ossastra.Development.CharacterLighting
{
 public sealed class CharacterLightingPanelInputRelay:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler,ISelectHandler,IDeselectHandler
 {
  internal CharacterLightingPanel owner;
  [TextArea] public string description;
  public void OnPointerEnter(PointerEventData data){if(owner!=null){owner.NotifyHover(this,true);owner.ShowHint(this,description);}}
  public void OnPointerExit(PointerEventData data){if(owner!=null){owner.NotifyHover(this,false);owner.HideHint(this);}}
  public void OnPointerDown(PointerEventData data){if(owner!=null)owner.NotifyPressed(this,true);}
  public void OnPointerUp(PointerEventData data){if(owner!=null)owner.NotifyPressed(this,false);}
  public void OnSelect(BaseEventData data){if(owner!=null){owner.NotifySelection(this,true);owner.ShowHint(this,description);}}
  public void OnDeselect(BaseEventData data){if(owner!=null){owner.NotifySelection(this,false);owner.HideHint(this);}}
  void OnDisable(){if(owner!=null)owner.ForgetPointer(this);}
  void OnDestroy(){if(owner!=null)owner.ForgetPointer(this);}
 }
}
