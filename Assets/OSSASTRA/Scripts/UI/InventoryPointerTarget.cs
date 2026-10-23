/* 脚本：InventoryPointerTarget
 * 功能：把uGUI的真实按下/拖动/释放转给背包控制器；容器表面与条目共用实际RectTransform。
 * 边界：不操作账本、不拥有物品快照；手指/鼠标捕获与玩法付款分离。
 */
using UnityEngine;
using UnityEngine.EventSystems;
namespace Ossastra.UserInterface
{
    public sealed class InventoryPointerTarget:MonoBehaviour,IPointerDownHandler,IBeginDragHandler,IDragHandler,IEndDragHandler,IPointerClickHandler
    {
        public InventoryView View {get;set;}
        public string ItemId {get;set;}
        public string ContainerId {get;set;}
        public int Slot {get;set;}=-1;
        public int Hotbar {get;set;}=-1;
        public bool Priority {get;set;}
        public RectTransform Grid {get;set;}
        public float Pitch {get;set;}
        public void OnPointerDown(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)View?.PointerDown(this,e);}
        public void OnBeginDrag(PointerEventData e)=>View?.BeginDrag(this,e);
        public void OnDrag(PointerEventData e)=>View?.Drag(e);
        public void OnEndDrag(PointerEventData e)=>View?.EndDrag(e);
        public void OnPointerClick(PointerEventData e){if(e.button==PointerEventData.InputButton.Right)View?.RightClick(this);}
    }
}
