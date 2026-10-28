/* 脚本：InventoryCommerceView（InventoryView的贸易接线部分）
 * 功能：在同一库存表面切换贸易布局并公开选物/取消/捕获端口；不创建第二库存或监听第二个交互键。
 * 边界：商品、报价、成交与存档由Commerce宿主管理；退出贸易恢复原四窗口和已有操作。
 * 协作：CommerceUIRoot只订阅本View选中实例，捕获按owner成对释放，不触及声音菜单。
 */
using System;
using UnityEngine;

namespace Ossastra.UserInterface
{
    public sealed partial class InventoryView
    {
        private bool commerceMode,commerceEditingQuantity;
        private readonly Ossastra.Commerce.Presentation.CommerceMenuGate commerceMenuGate=new Ossastra.Commerce.Presentation.CommerceMenuGate();
        private int commerceCancelFrame=-1;
        public bool IsCommerceOpen=>commerceMode&&IsOpen;
        public bool HasCommerceCapture=>commerceMenuGate.HasCapture;
        public bool IsPresentationReady=>canvas!=null&&session!=null&&session.Ready;
        public bool HasInventoryDrag=>drag!=null;
        public InventorySkinSO Skin=>skin;
        public event Action<string> CommerceSelectionChanged;
        public event Action CommerceInventoryClosed;

        /// <summary>保留团队网格与实例身份，只改变显示上下文；失败时不留下半开的贸易模式。</summary>
        public bool BeginCommerce()
        {
            if(!IsPresentationReady)return false;
            CloseSystemMenu();commerceMode=true;SetOpen(true);
            if(!IsOpen){commerceMode=false;return false;}
            openedExternal=null;session.SetActiveExternal(null);Select(null);dirty=true;ApplyCommerceLayout();return true;
        }
        public void EndCommerce()
        {
            commerceMode=false;commerceEditingQuantity=false;
            RestoreInventoryLayout();SetOpen(false);
        }
        private void NotifyCommerceInventoryClosing()
        {
            if(!commerceMode&&!HasCommerceCapture)return;
            commerceMode=false;commerceEditingQuantity=false;RestoreInventoryLayout();
            CommerceInventoryClosed?.Invoke();
        }
        /// <summary>只消费已配置的背包取消Action；同一按下只返回一次，root无需硬读Escape。</summary>
        public bool ConsumeCommerceCancel()
        {
            if(cancelAction==null||!cancelAction.enabled||!cancelAction.WasPressedThisFrame()||commerceCancelFrame==Time.frameCount)return false;
            commerceCancelFrame=Time.frameCount;commerceMenuGate.ConsumeCancel(Time.frameCount);return true;
        }
        public void SetCommerceQuantityFocus(bool editing){commerceEditingQuantity=editing;}
        public void SetCommerceInputCapture(object owner,bool captured,bool blockMovement)
        {
            if(owner==null)return;
            commerceMenuGate.Capture(owner,captured,Time.frameCount);
            if(session==null||session.Coordinator==null)return;
            session.Coordinator.SetInventoryCapture(owner,captured);
            SetCharacterCapture(owner,captured,blockMovement);
        }
        private void CommerceRect(RectTransform node,Rect rect)
        {if(node==null)return;node.anchoredPosition=new Vector2(rect.x,-rect.y);node.sizeDelta=rect.size;}
        private void ApplyCommerceLayout()
        {
            if(player==null)return;
            CommerceRect(player,new Rect(18,25,435,740));
            var frame=player.Find("Frame") as RectTransform;if(frame!=null)frame.sizeDelta=player.sizeDelta;
            safe.gameObject.SetActive(false);detail.gameObject.SetActive(false);external.gameObject.SetActive(false);reserve.gameObject.SetActive(false);
            foreach(Transform child in player)
                if(child.name.StartsWith("Owner",StringComparison.Ordinal)||child.name.StartsWith("hand:",StringComparison.Ordinal)||child.name=="ReserveToggle")child.gameObject.SetActive(false);
            CommerceRect(player.Find("TeamHeading") as RectTransform,new Rect(12,55,411,36));
            CommerceRect(teamGrid,new Rect(12,95,414,165.6f));
            CommerceRect(player.Find("Weight") as RectTransform,new Rect(12,270,411,30));
            feedback.rectTransform.anchoredPosition=new Vector2(12,-310);
        }
        private void RestoreInventoryLayout()
        {
            if(player==null)return;
            CommerceRect(player,new Rect(12,20,440,505));safe.gameObject.SetActive(true);
            foreach(Transform child in player)
                if(child.name.StartsWith("Owner",StringComparison.Ordinal)||child.name.StartsWith("hand:",StringComparison.Ordinal)||child.name=="ReserveToggle")child.gameObject.SetActive(true);
            CommerceRect(player.Find("TeamHeading") as RectTransform,new Rect(12,223,416,36));
            CommerceRect(teamGrid,new Rect(12,263,414,165.6f));
            CommerceRect(player.Find("Weight") as RectTransform,new Rect(12,437.6f,416,30));dirty=true;
        }
    }
}
