/* 功能：把本面板的指针进入、按住与释放转交给唯一面板owner，避免拖动滑条时动作穿透。
 * 边界：不读设备键、不暂停世界、不直接操作角色；只登记自己这一份指针状态。
 * 生命周期：禁用或销毁即撤销自身登记，面板按owner归还原角色/镜头输入租约。
 */
using UnityEngine;
using UnityEngine.EventSystems;
namespace Ossastra.Development.Environment
{
 public sealed class EnvironmentPanelInputRelay:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler,ISelectHandler,IDeselectHandler
 {
  internal EnvironmentSimulationPanel owner;
  EnvironmentSimulationPanel Owner{get{if(owner==null)owner=GetComponentInParent<EnvironmentSimulationPanel>();return owner;}}
  public void OnPointerEnter(PointerEventData data){if(Owner!=null)owner.NotifyHover(this,true);}
  public void OnPointerExit(PointerEventData data){if(Owner!=null)owner.NotifyHover(this,false);}
  public void OnPointerDown(PointerEventData data){if(Owner!=null)owner.NotifyPressed(this,true);}
  public void OnPointerUp(PointerEventData data){if(owner!=null)owner.NotifyPressed(this,false);}
  public void OnSelect(BaseEventData data){if(Owner!=null)owner.NotifySelection(this,true);}
  public void OnDeselect(BaseEventData data){if(owner!=null)owner.NotifySelection(this,false);}
  void OnDisable(){if(owner!=null)owner.ForgetPointer(this);}
  void OnDestroy(){if(owner!=null)owner.ForgetPointer(this);}
 }
}
