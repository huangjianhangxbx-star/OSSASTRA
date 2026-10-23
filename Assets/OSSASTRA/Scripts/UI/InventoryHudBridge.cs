/* 脚本：InventoryHudBridge
 * 功能：把既有六区HUD的背包钮、十个引用格及伙伴优先位连接真实库存；保留原锚点和素材。
 * 边界：数字键只绑定/选择，不直接施放；缺货显示不补库存，清引用不删物品。
 */
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Ossastra.Inventory;
namespace Ossastra.UserInterface
{
    [DefaultExecutionOrder(250)]
    public sealed class InventoryHudBridge:MonoBehaviour
    {
        [SerializeField] private InventorySession session;
        [SerializeField] private InventoryView view;
        [SerializeField] private CharacterHudView hud;
        public InputAction[] digitActions=Enumerable.Range(0,10).Select(i=>new InputAction("InventoryReference"+i,InputActionType.Button,"<Keyboard>/"+((i+1)%10))).ToArray();
        private Text[] labels;
        private Text priorityText;
        private bool dirty=true;
        private void OnInventoryChanged(){dirty=true;}
        private void OnEnable(){foreach(var action in digitActions)action?.Enable();}
        private void Start()
        {
            if(session==null||view==null||hud==null){Debug.LogError("InventoryHudBridge missing explicit references",this);enabled=false;return;}
            var quick=hud.GetComponentsInChildren<RectTransform>(true).SingleOrDefault(t=>t.name=="QuickCluster");if(quick==null){Debug.LogError("Existing QuickCluster missing",this);enabled=false;return;}
            var bag=quick.Find("Backpack");if(bag==null){enabled=false;return;}var label=bag.GetComponentInChildren<Text>();if(label!=null)label.text="Tab\n背包";var button=bag.gameObject.AddComponent<Button>();button.targetGraphic=bag.GetComponent<Image>();button.onClick.AddListener(()=>view.SetOpen(!view.IsOpen));
            labels=new Text[10];
            for(var i=0;i<10;i++)
            {
                var root=quick.Find("QuickSlot"+(i+1));if(root==null)throw new InvalidOperationException("HUD quick slot missing "+i);
                labels[i]=root.GetComponentsInChildren<Text>().FirstOrDefault(t=>t.name=="Unavailable");var graphic=root.gameObject.AddComponent<Image>();graphic.color=Color.clear;graphic.raycastTarget=true;
                var target=root.gameObject.AddComponent<InventoryPointerTarget>();target.View=view;target.Hotbar=i;
            }
            var free=hud.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t=>t.name=="FreeItem");
            if(free!=null){priorityText=free.GetComponentInChildren<Text>();var graphic=free.GetComponent<Image>();if(graphic==null)graphic=free.gameObject.AddComponent<Image>();graphic.raycastTarget=true;var target=free.gameObject.AddComponent<InventoryPointerTarget>();target.View=view;target.Priority=true;}
            session.Changed+=OnInventoryChanged;
        }
        private void Update()
        {
            if(labels==null||!session.Ready)return;
            var editing=EventSystem.current?.currentSelectedGameObject?.GetComponent<InputField>()?.isFocused==true;
            for(var i=0;i<10;i++)
            {
                if(!editing&&i<digitActions.Length&&digitActions[i]?.WasPressedThisFrame()==true)
                {if(view.IsOpen){var q=session.BindHotbar(i,view.SelectedItemId);view.Feedback(q.Ok?"已绑定引用":q.Reason);}else {session.SelectHotbar(i);view.Feedback(session.LastResult);}}
            }
            // 数量/引用改变才重画；十个格共享一次只读快照，不逐帧复制整本库存十一次。
            if(!dirty)return;dirty=false;var hotbar=session.Hotbar;var items=session.Ledger.Items;
            for(var i=0;i<10;i++)
            {
                var id=hotbar[i];var d=session.Definition(id);var quantity=items.Where(item=>item.DefinitionId==id&&item.Location.ContainerId=="team").Sum(item=>item.Quantity);
                if(labels[i]!=null)labels[i].text=d==null?"—":d.displayName+"\n"+(quantity>0?"×"+quantity:"缺货");
            }
            if(priorityText!=null){var d=session.Definition(session.PriorityDefinitionId);var count=items.Where(item=>item.Location.ContainerId=="reserve"&&item.DefinitionId==session.PriorityDefinitionId).Sum(item=>item.Quantity);priorityText.text=d==null?"优先物品\n未指定":d.displayName+"\n"+(count>0?"×"+count:"缺货 · 请交付");}
        }
        private void OnDisable(){foreach(var action in digitActions)action?.Disable();}
        private void OnDestroy(){if(session!=null)session.Changed-=OnInventoryChanged;foreach(var action in digitActions)action?.Dispose();}
    }
}
