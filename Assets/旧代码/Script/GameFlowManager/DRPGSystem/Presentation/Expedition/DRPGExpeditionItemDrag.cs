using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class DRPGExpeditionItemDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public DRPGExpeditionHud hud;
    public int Slot=-1;
    public string LootId;
    public bool Handled;
    GameObject ghost;
    public void OnBeginDrag(PointerEventData e)
    {
        if(!hud.Runtime.CanExplore||(LootId==null&&hud.Runtime.Inventory.GetSlot(Slot).IsEmpty))return;
        Handled=false;var canvas=hud.GetComponentInParent<Canvas>();if(canvas==null)canvas=hud.hudRoot.GetComponentInParent<Canvas>();
        var rect=hud.Panel("ItemDragGhost",canvas.transform,0,0,0,0);rect.sizeDelta=new Vector2(210,55);rect.GetComponent<Image>().raycastTarget=false;
        hud.Text("Hint",rect,LootId??hud.Runtime.Inventory.GetSlot(Slot).Item.DisplayName,0,0,1,1,16);ghost=rect.gameObject;OnDrag(e);
    }
    public void OnDrag(PointerEventData e)
    {
        if(ghost==null)return;ghost.transform.position=e.position+new Vector2(35,35);
        var target=e.pointerCurrentRaycast.gameObject!=null?e.pointerCurrentRaycast.gameObject.GetComponentInParent<DRPGExpeditionDropTarget>():null;
        ghost.GetComponentInChildren<TMP_Text>().text=target!=null?(target.Event?(LootId==null&&hud.Runtime.CanUse(Slot)?"适用 · 松手使用":"不适用 · 不会消耗"):"松手放入行囊"):(LootId==null?"松手遗弃整叠":"松手取消收取");
    }
    public void OnEndDrag(PointerEventData e)
    {
        if(ghost==null)return;
        Destroy(ghost);ghost=null;
        if(!Handled&&LootId==null)hud.Runtime.Discard(Slot);
    }
    void OnDisable(){if(ghost!=null)Destroy(ghost);ghost=null;}
}
public sealed class DRPGExpeditionDropTarget : MonoBehaviour, IDropHandler
{
    public DRPGExpeditionHud hud;
    public int Slot=-1;
    public bool Event;
    public void OnDrop(PointerEventData e)
    {
        var drag=e.pointerDrag!=null?e.pointerDrag.GetComponent<DRPGExpeditionItemDrag>():null;
        if(drag==null||drag.hud!=hud)return;drag.Handled=true;
        if(Event){if(drag.LootId==null)hud.Runtime.Use(drag.Slot);return;}
        if(drag.LootId!=null)hud.Runtime.Collect(drag.LootId,Slot);else if(Slot>=0)hud.Runtime.MoveItem(drag.Slot,Slot);
    }
}
