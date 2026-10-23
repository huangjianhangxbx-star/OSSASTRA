/* 脚本：InventoryView
 * 功能：冻结原型四个独立背包表面、详情/拆分、真实uGUI拖放与有限整理；所有落点用实际RectTransform。
 * 边界：不保存第二库存；不以UI创建装备/治疗。开包捕获战斗而世界继续，数字栏只引用。
 * 协作：InventorySession复核访问与本体，Ledger原子提交；保持HUD原六区。
 */
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Ossastra.Inventory;
namespace Ossastra.UserInterface
{
    public sealed class InventoryView:MonoBehaviour
    {
        [SerializeField] private InventorySession session;
        [SerializeField] private InventorySkinSO skin;
        public InputAction toggleAction=new InputAction("Inventory",InputActionType.Button,"<Keyboard>/tab");
        public InputAction cancelAction=new InputAction("InventoryCancel",InputActionType.Button,"<Keyboard>/escape");
        private RectTransform menuPanel;
        public void ToggleMenu()
        {
            if(windows==null||session==null)return;
            if(menuPanel==null)
            {
                menuPanel=Window("SystemMenu",new Rect(565,180,350,180));
                Header(menuPanel,"菜单",350,()=>ToggleMenu());
                Button(menuPanel,"Continue",new Rect(20,65,310,36),"继续游戏",()=>ToggleMenu());
                Button(menuPanel,"Inventory",new Rect(20,115,310,36),"背包",()=>{ToggleMenu();SetOpen(true);});
                menuPanel.gameObject.SetActive(false);
            }
            bool open=!menuPanel.gameObject.activeSelf;
            if(open){SetOpen(false);windows.gameObject.SetActive(true);player.gameObject.SetActive(false);safe.gameObject.SetActive(false);detail.gameObject.SetActive(false);external.gameObject.SetActive(false);}else{player.gameObject.SetActive(true);safe.gameObject.SetActive(true);windows.gameObject.SetActive(IsOpen);}
            menuPanel.gameObject.SetActive(open);
            SetCharacterCapture(menuPanel,open,true);
            if(session.Coordinator!=null)session.Coordinator.SetInputCapture(menuPanel,open);
            if(open&&EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(menuPanel.Find("Continue").gameObject);
        }
        private Canvas canvas;
        private RectTransform surface,windows,player,safe,detail,external,teamGrid,externalGrid,reserve,ghost,preview;
        private Text detailName,detailText,detailArt,feedback,weight,externalName,ghostText;
        private Image detailIcon,weightFill;
        private Button unload,give,reclaim,priority,use;
        private InputField quantity;
        private Font font,serif;
        private string selected,openedExternal;
        private bool dirty=true,reserveExpanded;
        private int externalRows=8;
        private readonly Dictionary<string,InventoryPointerTarget> cells=new Dictionary<string,InventoryPointerTarget>();
        private readonly Dictionary<string,InventoryPointerTarget> itemViews=new Dictionary<string,InventoryPointerTarget>();
        private readonly List<(Text text,int size)> typography=new List<(Text,int)>();
        private sealed class DragState
        {public string id,from;public long revision,generation;public Dictionary<string,long> revisions;public Vector2 offset,down;public bool started;public int pointer;}
        private DragState drag;
        public bool IsOpen {get;private set;}
        public string SelectedItemId=>selected;
        public string OpenedExternal=>openedExternal;
        public InventorySession Session=>session;
        public RectTransform TeamGrid=>teamGrid;
        public string Notice=>feedback?.text;
        private static readonly Color ink=new Color32(8,3,5,255),edge=new Color32(157,32,69,255),gold=new Color32(210,190,128,255);
        private void OnEnable(){toggleAction.Enable();cancelAction.Enable();}
        private void Start()
        {
            if(session==null||skin==null||skin.frame==null||skin.slotFill==null||skin.slotEdge==null){Debug.LogError("InventoryView session/skin missing",this);enabled=false;return;}
            font=Font.CreateDynamicFontFromOSFont(skin.fontName,16);
            serif=Font.CreateDynamicFontFromOSFont("SimSun",26);
            var go=new GameObject("InventoryCanvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=150;surface=(RectTransform)go.transform;
            windows=Node(surface,"Windows",new Rect(0,0,1434,804));player=Window("Backpack",new Rect(12,20,440,505));
            Header(player,"背包",440,()=>SetOpen(false));
            for(var n=0;n<2;n++)
            {
                var root=session.Coordinator.Characters[n];Label(player,"Owner"+n,new Rect(12,55.5f+n*80,78,70),root.Definition?.displayName??root.ActorId,14);
                var left=Slot(player,InventorySession.HandId(root.ActorId,InventoryHand.Left),0,new Rect(105,55.5f+n*80,70,70));
                var right=Slot(player,InventorySession.HandId(root.ActorId,InventoryHand.Right),0,new Rect(190,55.5f+n*80,70,70));
                Label(left.transform,"Hand",new Rect(31,1,35,15),"左手",10,gold);Label(right.transform,"Hand",new Rect(31,1,35,15),"右手",10,gold);
            }
            var heading=Image(player,"TeamHeading",new Rect(12,223,416,36),new Color32(40,19,31,255),skin.slotFill,true);
            Label(heading.transform,"Capacity",new Rect(8,0,260,36),"团队背包 · 10格",14);
            Button(heading.transform,"Sort",new Rect(337,4,68,28),"整理",()=>Sort("team"),new Color32(225,237,239,255),new Color32(39,58,66,255));
            teamGrid=Grid(player,"team",new Rect(12,263,414,165.6f),82.8f,5,2);
            var load=Image(player,"Weight",new Rect(12,437.6f,416,30),new Color32(28,13,22,255),skin.slotFill);
            Label(load.transform,"Label",new Rect(5,0,35,30),"负重",11);
            var meter=Image(load.transform,"Meter",new Rect(40,11,230,8),new Color32(48,36,44,255),skin.slotFill);
            weightFill=Image(meter.transform,"Fill",new Rect(0,0,230,8),new Color32(167,182,109,255),skin.slotFill);
            weight=Label(load.transform,"Value",new Rect(275,0,137,30),"",11,gold);
            Button(player,"ReserveToggle",new Rect(12,470.6f,230,22),"▸ 芙蕾茜娅后备",()=>{reserveExpanded=!reserveExpanded;dirty=true;});
            reserve=Node(player,"Reserve",new Rect(12,496.6f,416,57));
            for(var n=0;n<5;n++)Slot(reserve,"reserve",n,new Rect(n*84,0,80,57));
            feedback=Label(player,"Feedback",new Rect(12,485.6f,416,20),"",11,gold);
            safe=Window("Safe",new Rect(456,20,97,208));Image(safe,"Title",new Rect(8,8,81,20),new Color32(155,32,67,255));Label(safe,"Name",new Rect(8,8,81,20),"安全",13,null,TextAnchor.MiddleCenter);
            Slot(safe,"safe",0,new Rect(8,36,79,79));Slot(safe,"safe",1,new Rect(8,119,79,79));
            detail=Window("ItemDetail",new Rect(565,20,448,350));detailName=Header(detail,"",448,()=>Select(null));
            detailIcon=Image(detail,"Art",new Rect(12,49,70,70),Color.white);detailIcon.preserveAspect=true;
            detailArt=Label(detail,"Glyph",new Rect(12,49,70,70),"",36,null,TextAnchor.MiddleCenter);
            detailText=Label(detail,"Description",new Rect(96,47,338,165),"",14);detailText.verticalOverflow=VerticalWrapMode.Truncate;
            unload=Button(detail,"Unequip",new Rect(12,220,92,28),"卸回背包",()=>TransferSelected("team"));
            give=Button(detail,"Give",new Rect(108,220,145,28),"交给芙蕾茜娅 ×1",()=>TransferSelected("reserve",1));
            reclaim=Button(detail,"Reclaim",new Rect(12,220,112,28),"收回背包 ×1",()=>TransferSelected("team",1));
            priority=Button(detail,"Priority",new Rect(130,220,105,28),"设为优先",()=>Notify(session.SetPriority(selected)));
            use=Button(detail,"Use",new Rect(12,255,96,28),"使用 ×1",()=>
            {var port=session.GetComponents<MonoBehaviour>().OfType<IInventoryItemUse>().FirstOrDefault();if(port==null){Feedback("道具消费者未就绪");return;}port.TryUse(selected);Feedback(port.LastResult);});
            Label(detail,"QuantityLabel",new Rect(12,292,70,26),"转移数量",12);quantity=NumberField(detail,new Rect(86,292,86,26));
            external=Window("External",new Rect(1043,92,365,217));externalName=Header(external,"试验物资箱",365,()=>OpenExternal(null));
            externalGrid=ScrollGrid(external,"ExternalScroll",new Rect(12,47,341,132),67.8f);
            Button(external,"SortExternal",new Rect(247,184,105,22),"整理箱子",()=>Sort(openedExternal));
            ghost=Node(surface,"DragGhost",new Rect(0,0,180,34));Image(ghost,"Fill",new Rect(0,0,180,34),new Color32(24,8,16,245));ghostText=Label(ghost,"Name",new Rect(8,0,164,34),"",13,gold);
            preview=Node(surface,"DropPreview",new Rect(0,0,80,80));Image(preview,"Fill",new Rect(0,0,80,80),new Color32(133,183,170,50));
            session.Changed+=OnChanged;windows.gameObject.SetActive(false);ghost.gameObject.SetActive(false);preview.gameObject.SetActive(false);
            if(EventSystem.current!=null)EventSystem.current.pixelDragThreshold=6;
        }
        private void OnChanged(){dirty=true;}
        private RectTransform Node(Transform parent,string name,Rect rect)
        {var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(rect.x,-rect.y);r.sizeDelta=rect.size;return r;}
        private Image Image(Transform parent,string name,Rect rect,Color color,Sprite sprite=null,bool raycast=false)
        {var r=Node(parent,name,rect);var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=color;image.sprite=sprite;if(sprite!=null&&sprite.border.sqrMagnitude>0)image.type=UnityEngine.UI.Image.Type.Sliced;image.raycastTarget=raycast;return image;}
        private Text Label(Transform parent,string name,Rect rect,string value,int size,Color? color=null,TextAnchor align=TextAnchor.UpperLeft)
        {var text=Node(parent,name,rect).gameObject.AddComponent<Text>();text.font=name=="Title"||name=="ItemGlyph"||name=="Glyph"?serif:font;text.fontSize=size;text.color=color??new Color32(238,233,233,255);text.text=value;text.raycastTarget=false;text.alignment=align;text.verticalOverflow=VerticalWrapMode.Truncate;typography.Add((text,size));return text;}
        private Button Button(Transform parent,string name,Rect rect,string text,Action action,Color? fill=null,Color? textColor=null)
        {
            var plainText=name=="ReserveToggle";var image=Image(parent,name,rect,plainText?Color.clear:fill??new Color32(16,5,8,255),fill.HasValue?skin.slotFill:null,true);
            if(!plainText&&!fill.HasValue)
            {
                var border=new Color32(129,26,56,255);
                Image(image.transform,"Top",new Rect(0,0,rect.width,1),border);Image(image.transform,"Bottom",new Rect(0,rect.height-1,rect.width,1),border);
                Image(image.transform,"Left",new Rect(0,0,1,rect.height),border);Image(image.transform,"Right",new Rect(rect.width-1,0,1,rect.height),border);
            }
            var button=image.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;button.onClick.AddListener(()=>action());Label(image.transform,"Label",new Rect(2,1,rect.width-4,rect.height-2),text,13,textColor,plainText?TextAnchor.MiddleLeft:TextAnchor.MiddleCenter);return button;
        }
        private RectTransform Window(string name,Rect rect)
        {var image=Image(windows,name,rect,ink,null,true);var r=image.rectTransform;Image(r,"Frame",new Rect(0,0,rect.width,rect.height),Color.white,skin.frame).fillCenter=false;return r;}
        private Text Header(RectTransform parent,string title,float width,Action close)
        {Image(parent,"Header",new Rect(11,11,width-22,28),new Color32(155,32,67,255));var text=Label(parent,"Title",new Rect(15,11,width-66,28),title,22);Button(parent,"Close",new Rect(width-37,12,25,25),"×",close);return text;}
        private InventoryPointerTarget Target(RectTransform node,string container,int slot=-1,RectTransform grid=null,float pitch=0)
        {var t=node.gameObject.AddComponent<InventoryPointerTarget>();t.View=this;t.ContainerId=container;t.Slot=slot;t.Grid=grid;t.Pitch=pitch;return t;}
        private InventoryPointerTarget Slot(Transform parent,string container,int slot,Rect rect)
        {var fill=Image(parent,container+"_"+slot,rect,new Color32(24,11,19,255),skin.slotFill,true);Image(fill.transform,"Edge",new Rect(0,0,rect.width,rect.height),edge,skin.slotEdge);var t=Target(fill.rectTransform,container,slot);cells.Add(container+":"+slot,t);Label(fill.transform,"Empty",new Rect(0,0,rect.width,rect.height),container=="safe"?"空位":container=="reserve"?"—":"空手",14,null,TextAnchor.MiddleCenter);return t;}
        private RectTransform Grid(Transform parent,string id,Rect rect,float pitch,int columns,int rows)
        {var grid=Image(parent,id+"Grid",rect,Color.clear,null,true).rectTransform;Target(grid,id,-1,grid,pitch);for(var y=0;y<rows;y++)for(var x=0;x<columns;x++)Image(grid,"Cell"+x+"_"+y,new Rect(x*pitch,y*pitch,pitch,pitch),edge,skin.slotEdge);return grid;}
        private RectTransform ScrollGrid(Transform parent,string name,Rect rect,float pitch)
        {
            var root=Node(parent,name,rect);var scroll=root.gameObject.AddComponent<ScrollRect>();var viewport=Image(root,"Viewport",new Rect(0,0,rect.width,rect.height),Color.clear,null,true).rectTransform;viewport.gameObject.AddComponent<RectMask2D>();
            var content=Grid(viewport,"external",new Rect(0,0,339,8*pitch),pitch,5,8);scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=24;return content;
        }
        private InputField NumberField(Transform parent,Rect rect)
        {
            var image=Image(parent,"TransferQuantity",rect,new Color32(16,5,8,255),null,true);var field=image.gameObject.AddComponent<InputField>();field.targetGraphic=image;field.contentType=InputField.ContentType.IntegerNumber;
            var text=Label(image.transform,"Text",new Rect(5,2,rect.width-10,rect.height-4),"",13,null,TextAnchor.MiddleLeft);field.textComponent=text;
            field.placeholder=Label(image.transform,"Placeholder",new Rect(5,2,rect.width-10,rect.height-4),"整堆",13,new Color32(155,145,150,255),TextAnchor.MiddleLeft);return field;
        }
        public void SetOpen(bool value)
        {
            if(value&&(windows==null||session==null||session.Coordinator==null||!session.Ready||session.Coordinator.Current==null||!session.Coordinator.Current.CanSimulateIntent))return;
            CancelDrag();IsOpen=value;if(windows!=null)windows.gameObject.SetActive(value);
            if(session!=null&&session.Coordinator!=null)session.Coordinator.SetInventoryCapture(this,value);
            SetCharacterCapture(this,value);
            if(!value){selected=null;openedExternal=null;}dirty=true;
        }
        private void SetCharacterCapture(object captureOwner,bool captured,bool movement=false)
        {
            // 场景卸载时角色、会话和UI的销毁顺序不固定；只释放仍存活的输入端口。
            if(session==null||session.Coordinator==null)return;
            foreach(var root in session.Coordinator.Characters)
            {
                if(root==null)continue;
                var input=root.GetComponent<Ossastra.Characters.CharacterInputHandler>();
                if(input!=null)input.SetInventoryCapture(captureOwner,captured,movement);
            }
        }
        public void OpenExternal(string id)
        {if(id!=null&&!session.CanAccessContainer(id)){Feedback("靠近同层容器后打开");return;}if(id!=null&&!IsOpen)SetOpen(true);CancelDrag();openedExternal=id;selected=null;dirty=true;}
        public void Select(string id)
        {
            // 同一选中物品的再次按下是拖动开始，必须保留已经填写的拆分数量。
            if(selected!=id)quantity.text="";
            selected=id;dirty=true;
        }
        public void Feedback(string message){if(feedback!=null)feedback.text=message??"";dirty=true;}
        private void Notify(InventoryResult result){Feedback(result.Ok?session.LastResult:result.Reason);}
        private bool Quantity(out int value)
        {value=0;if(string.IsNullOrWhiteSpace(quantity.text))return true;if(int.TryParse(quantity.text,out value)&&value>0)return true;Feedback("转移数量必须是正整数");return false;}
        private void TransferSelected(string to,int amount=0){if(selected==null||amount==0&&!Quantity(out amount))return;Notify(session.Move(session.Ledger.Plan(selected,to,amount)));}
        private void Sort(string id)
        {if(id==null||!session.CanAccessContainer(id)){Feedback("容器不可访问");return;}var plan=session.Ledger.PlanSort(id,4000);var q=session.Ledger.CommitSort(plan);Feedback(q.Ok?"整理完成 · 可行布局，未证明最优":q.Reason);}
        public void PointerDown(InventoryPointerTarget target,PointerEventData data)
        {
            if(target.Hotbar>=0){if(IsOpen)Notify(session.BindHotbar(target.Hotbar,selected));else {session.SelectHotbar(target.Hotbar);Feedback(session.LastResult);}return;}
            if(target.Priority){if(IsOpen&&selected!=null){var preferred=session.Ledger.Find(selected);if(preferred?.Location.ContainerId=="reserve")Notify(session.SetPriority(selected));else {var q=session.Move(session.Ledger.Plan(selected,"reserve",1));Notify(q);if(q.Ok)Notify(session.SetPriority(q.ItemId));}}return;}
            if(!IsOpen){if(target.ContainerId!=null&&session.CanAccessContainer(target.ContainerId))OpenExternal(target.ContainerId);return;}
            if(target.ItemId==null){if(target.Hotbar<0&&!target.Priority)Select(null);return;}
            var item=session.Ledger.Find(target.ItemId);if(item==null||!session.CanAccessContainer(item.Location.ContainerId)){Feedback("靠近道具后操作");return;}
            Select(item.Id);
            if(Keyboard.current?.ctrlKey.isPressed==true){Notify(session.QuickTransfer(item.Id));return;}
            var c=session.Ledger.Container(item.Location.ContainerId);var offset=Vector2.zero;
            if(!c.Slots&&target.Grid!=null&&RectTransformUtility.ScreenPointToLocalPointInRectangle(target.Grid,data.position,null,out var local))offset=new Vector2(Mathf.FloorToInt(local.x/target.Pitch)-item.Location.X,Mathf.FloorToInt(-local.y/target.Pitch)-item.Location.Y);
            drag=new DragState{id=item.Id,from=item.Location.ContainerId,revision=c.Revision,generation=session.Ledger.AccessGeneration,offset=offset,down=data.position,pointer=data.pointerId,revisions=session.Ledger.Containers.ToDictionary(x=>x.Id,x=>x.Revision)};
        }
        public void BeginDrag(InventoryPointerTarget target,PointerEventData data)
        {if(drag==null||drag.id!=target.ItemId||data.button!=PointerEventData.InputButton.Left)return;drag.started=true;ghost.gameObject.SetActive(true);ghostText.text=session.Definition(session.Ledger.Find(drag.id)?.DefinitionId)?.displayName;Drag(data);}
        public void Drag(PointerEventData data)
        {
            if(drag==null||!drag.started)return;ghost.anchoredPosition=new Vector2(data.position.x/canvas.scaleFactor+12,-(Screen.height-data.position.y)/canvas.scaleFactor-12);
            var target=Hit(data.position);preview.gameObject.SetActive(target?.Grid!=null);
            if(target?.Grid!=null&&DropCoordinates(target,data.position,out var x,out var y))
            {
                var corners=new Vector3[4];target.Grid.GetWorldCorners(corners);preview.position=corners[1]+new Vector3(x*target.Pitch*canvas.scaleFactor,-y*target.Pitch*canvas.scaleFactor,0);var d=session.Ledger.Definition(session.Ledger.Find(drag.id)?.DefinitionId);
                preview.sizeDelta=new Vector2(d.Width*target.Pitch,d.Height*target.Pitch);var image=preview.GetComponentInChildren<Image>();image.rectTransform.sizeDelta=preview.sizeDelta;
                var q=session.Ledger.Query(session.Ledger.Plan(drag.id,target.ContainerId,0,x,y));image.color=q.Ok?new Color32(133,183,170,80):new Color32(195,108,100,80);
            }
        }
        private InventoryPointerTarget Hit(Vector2 point)
        {if(EventSystem.current==null)return null;var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);return hits.Select(h=>h.gameObject.GetComponentInParent<InventoryPointerTarget>()).FirstOrDefault(t=>t!=null);}
        private bool DropCoordinates(InventoryPointerTarget target,Vector2 point,out int x,out int y)
        {x=target.Slot<0?0:target.Slot;y=0;if(target.Grid==null)return true;if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(target.Grid,point,null,out var local))return false;x=Mathf.FloorToInt(local.x/target.Pitch)-(int)(drag?.offset.x??0);y=Mathf.FloorToInt(-local.y/target.Pitch)-(int)(drag?.offset.y??0);return true;}
        public void EndDrag(PointerEventData data)
        {
            var current=drag;if(current==null)return;
            var target=Hit(data.position);
            if(!current.started){CancelDrag();return;}
            if(current.generation!=session.Ledger.AccessGeneration||session.Ledger.Container(current.from)?.Revision!=current.revision){CancelDrag();Feedback("物品、控制或访问已变化，取消拖拽");return;}
            if(!Quantity(out var count)){CancelDrag();return;}
            if(target?.Hotbar>=0){Notify(session.BindHotbar(target.Hotbar,current.id));CancelDrag();return;}
            if(target?.Priority==true){var item=session.Ledger.Find(current.id);if(item?.Location.ContainerId=="reserve")Notify(session.SetPriority(current.id));else {var q=session.Move(session.Ledger.Plan(current.id,"reserve",1));Notify(q);if(q.Ok)Notify(session.SetPriority(q.ItemId));}CancelDrag();return;}
            if(target?.ContainerId!=null&&DropCoordinates(target,data.position,out var x,out var y))
            {
                if(!current.revisions.TryGetValue(target.ContainerId,out var revision)||session.Ledger.Container(target.ContainerId)?.Revision!=revision)Feedback("目标内容已变化，请重新拖动");
                else Notify(session.Move(session.Ledger.Plan(current.id,target.ContainerId,count,x,y)));
            }
            else
            {
                var hits=new List<RaycastResult>();EventSystem.current?.RaycastAll(data,hits);
                if(hits.Count==0&&session.TryGroundPoint(data.position,out var point))Notify(session.Drop(current.id,count==0?session.Ledger.Find(current.id).Quantity:count,point,current.generation,current.revision));
                else Feedback("无效目标，已取消");
            }
            CancelDrag();session.ReclaimEmptyGround();
        }
        public void RightClick(InventoryPointerTarget target)
        {if(target.Hotbar>=0)Notify(session.BindHotbar(target.Hotbar,null));else if(target.Priority)Notify(session.SetPriority(null));}
        public void CancelDrag(){drag=null;if(ghost!=null)ghost.gameObject.SetActive(false);if(preview!=null)preview.gameObject.SetActive(false);}
        private void Update()
        {
            if(canvas==null||!session.Ready)return;
            var scale=Mathf.Min(Screen.width/1434f,Screen.height/804f);if(!Mathf.Approximately(scale,canvas.scaleFactor)){CancelDrag();canvas.scaleFactor=scale;dirty=true;}
            external.anchoredPosition=new Vector2(Screen.width/scale-391,-92);
            if(toggleAction.WasPressedThisFrame()&&!(EventSystem.current?.currentSelectedGameObject?.GetComponent<InputField>()?.isFocused??false))SetOpen(!IsOpen);
            if(IsOpen&&cancelAction.WasPressedThisFrame()){if(drag!=null)CancelDrag();else SetOpen(false);}
            if(IsOpen&&(session.Coordinator.Current==null||!session.Coordinator.Current.IsAlive)){SetOpen(false);return;}
            if(IsOpen)
            {
                var editing=quantity.isFocused;SetCharacterCapture(this,true,editing);
                if(openedExternal!=null&&!session.CanAccessContainer(openedExternal)){OpenExternal(null);Feedback("已离开容器访问范围");}
                if(selected!=null&&(session.Ledger.Find(selected)==null||!session.CanAccessContainer(session.Ledger.Find(selected).Location.ContainerId))){Select(null);CancelDrag();}
                if(drag!=null&&drag.generation!=session.Ledger.AccessGeneration)CancelDrag();
                if(Mouse.current?.leftButton.wasPressedThisFrame==true&&!(EventSystem.current?.IsPointerOverGameObject()??false)&&drag==null)Select(null);
            }
            if(dirty){dirty=false;Refresh();}
        }
        private static Color Rarity(Ossastra.Characters.ItemRarity rarity)
            =>rarity==Ossastra.Characters.ItemRarity.Blue?new Color32(74,183,237,255):rarity==Ossastra.Characters.ItemRarity.Purple?new Color32(189,118,238,255):rarity==Ossastra.Characters.ItemRarity.Gold?new Color32(244,204,97,255):rarity==Ossastra.Characters.ItemRarity.RedGold?new Color32(255,100,108,255):new Color32(155,164,173,255);
        private void Paint(InventoryPointerTarget target,InventoryItem item)
        {
            var d=session.Definition(item.DefinitionId);target.ItemId=item.Id;var r=(RectTransform)target.transform;
            var icon=r.Find("ItemIcon")?.GetComponent<Image>();if(icon==null)icon=Image(r,"ItemIcon",new Rect(8,10,r.rect.width-16,Mathf.Max(20,r.rect.height-34)),Color.white);icon.sprite=d.icon;icon.enabled=d.icon!=null;icon.preserveAspect=true;
            Text TextChild(string name,Rect rect,int size,TextAnchor align){var t=r.Find(name)?.GetComponent<Text>();return t!=null?t:Label(r,name,rect,"",size,null,align);}
            var glyph=TextChild("ItemGlyph",new Rect(4,7,r.rect.width-8,Mathf.Max(20,r.rect.height-32)),26,TextAnchor.MiddleCenter);glyph.text=d.icon==null?(string.IsNullOrEmpty(d.inventoryGlyph)?d.displayName.Substring(0,1):d.inventoryGlyph):"";
            var name=TextChild("ItemName",new Rect(4,r.rect.height-25,r.rect.width-8,22),11,TextAnchor.MiddleCenter);name.text=d.displayName;
            var amount=TextChild("Amount",new Rect(4,3,r.rect.width-8,18),11,TextAnchor.UpperRight);amount.text=item.Equipment==null?"×"+item.Quantity:"";amount.color=gold;
            foreach(var child in new[]{"ItemGlyph","ItemName","Amount"})r.Find(child)?.gameObject.SetActive(true);icon.gameObject.SetActive(d.icon!=null);
            var empty=r.Find("Empty");if(empty!=null)empty.gameObject.SetActive(false);
            var border=r.Find("Edge")?.GetComponent<Image>();if(border!=null)border.color=item.Id==selected?new Color32(255,242,164,255):Rarity(d.rarity);
        }
        private void Refresh()
        {
            if(!IsOpen)return;
            reserve.gameObject.SetActive(reserveExpanded);var hasFeedback=!string.IsNullOrEmpty(feedback.text);feedback.gameObject.SetActive(hasFeedback);
            feedback.rectTransform.anchoredPosition=new Vector2(12,-498.6f-(reserveExpanded?63:0));player.sizeDelta=new Vector2(440,504.6f+(reserveExpanded?63:0)+(hasFeedback?22:0));
            var frame=player.Find("Frame") as RectTransform;if(frame!=null)frame.sizeDelta=player.sizeDelta;
            foreach(var target in cells.Values)
            {
                var item=session.Ledger.Items.FirstOrDefault(i=>i.Location.ContainerId==target.ContainerId&&i.Location.X==target.Slot&&i.Location.Y==0);
                if(item!=null)Paint(target,item);else
                {target.ItemId=null;foreach(var child in new[]{"ItemIcon","ItemGlyph","ItemName","Amount"}){var t=target.transform.Find(child);if(t!=null)t.gameObject.SetActive(false);}var empty=target.transform.Find("Empty");if(empty!=null)empty.gameObject.SetActive(true);var border=target.transform.Find("Edge")?.GetComponent<Image>();if(border!=null)border.color=edge;}
            }
            RefreshGrid("team",teamGrid,82.8f);external.gameObject.SetActive(openedExternal!=null);
            if(openedExternal!=null)
            {
                externalName.text=openedExternal=="external"?"试验物资箱":"掉落包";var c=session.Ledger.Container(openedExternal);
                externalGrid.GetComponent<InventoryPointerTarget>().ContainerId=openedExternal;
                for(var y=externalRows;y<c.Rows;y++)for(var x=0;x<c.Columns;x++)
                {var cell=Image(externalGrid,"Cell"+x+"_"+y,new Rect(x*67.8f,y*67.8f,67.8f,67.8f),edge,skin.slotEdge);cell.transform.SetAsFirstSibling();}
                externalRows=Math.Max(externalRows,c.Rows);
                foreach(Transform child in externalGrid)if(child.name.StartsWith("Cell",StringComparison.Ordinal))
                {var row=int.Parse(child.name.Substring(child.name.IndexOf('_')+1));child.gameObject.SetActive(row<c.Rows);}
                externalGrid.sizeDelta=new Vector2(339,Mathf.Max(132,c.Rows*67.8f));RefreshGrid(openedExternal,externalGrid,67.8f);
            }
            var selectedItem=session.Ledger.Find(selected);detail.gameObject.SetActive(selectedItem!=null);
            if(selectedItem!=null)
            {
                var d=session.Definition(selectedItem.DefinitionId);detailName.text=d.displayName;detailIcon.sprite=d.icon;detailIcon.enabled=d.icon!=null;detailArt.text=d.icon==null?(string.IsNullOrEmpty(d.inventoryGlyph)?d.displayName.Substring(0,1):d.inventoryGlyph):"";
                detailText.text=d.description+"\n数量 "+selectedItem.Quantity+" · 占格 "+d.width+"×"+d.height+"\n每堆上限 "+d.maxStack+"\n重量 "+(d.unitWeightGrams/1000f).ToString("0.00")+" kg/件"+(selectedItem.Equipment==null?"":"\n耐久 "+selectedItem.Equipment.Durability);
                unload.gameObject.SetActive(selectedItem.Location.ContainerId.StartsWith("hand:"));give.gameObject.SetActive(selectedItem.Location.ContainerId=="team"&&d.equipment==null&&d.category!="food");reclaim.gameObject.SetActive(selectedItem.Location.ContainerId=="reserve");priority.gameObject.SetActive(selectedItem.Location.ContainerId=="reserve");use.gameObject.SetActive(d.useKind!=Ossastra.Characters.InventoryUseKind.None&&new[]{"team","safe","reserve"}.Contains(selectedItem.Location.ContainerId));
            }
            var ids=new[]{"team","safe","reserve"}.Concat(session.Coordinator.Characters.SelectMany(r=>new[]{InventorySession.HandId(r.ActorId,InventoryHand.Left),InventorySession.HandId(r.ActorId,InventoryHand.Right)})).ToArray();var grams=session.Ledger.WeightGrams(ids);weight.text=(grams/1000f).ToString("0.00")+" / 30.00 kg"+(grams>30000?" · 超重":"");weightFill.rectTransform.sizeDelta=new Vector2(230*Mathf.Clamp01(grams/30000f),8);
        }
        private void RefreshGrid(string id,RectTransform grid,float pitch)
        {
            var actual=session.Ledger.Items.Where(i=>i.Location.ContainerId==id).ToArray();
            foreach(var key in itemViews.Keys.Where(k=>itemViews[k].Grid==grid&&!actual.Any(i=>i.Id==k)).ToArray()){Destroy(itemViews[key].gameObject);itemViews.Remove(key);}
            foreach(var item in actual)
            {
                var d=session.Ledger.Definition(item.DefinitionId);var c=session.Ledger.Container(id);var rect=new Rect(item.Location.X*pitch,item.Location.Y*pitch,(c.Slots?1:d.Width)*pitch,(c.Slots?1:d.Height)*pitch);
                if(itemViews.TryGetValue(item.Id,out var target)&&target.Grid!=grid){Destroy(target.gameObject);itemViews.Remove(item.Id);target=null;}
                if(target==null){var fill=Image(grid,"Item_"+item.Id,rect,new Color32(24,11,19,255),skin.slotFill,true);Image(fill.transform,"Edge",new Rect(0,0,rect.width,rect.height),edge,skin.slotEdge);target=Target(fill.rectTransform,id,-1,grid,pitch);itemViews[item.Id]=target;}
                var rt=(RectTransform)target.transform;rt.anchoredPosition=new Vector2(rect.x,-rect.y);rt.sizeDelta=rect.size;target.ContainerId=id;Paint(target,item);
            }
        }
        private void OnApplicationFocus(bool focused){if(!focused){if(menuPanel!=null&&menuPanel.gameObject.activeSelf)ToggleMenu();SetOpen(false);}}
        private void OnDisable(){if(menuPanel!=null&&menuPanel.gameObject.activeSelf)ToggleMenu();SetOpen(false);toggleAction.Disable();cancelAction.Disable();}
        private void OnDestroy(){if(session!=null)session.Changed-=OnChanged;toggleAction.Dispose();cancelAction.Dispose();if(font!=null)Destroy(font);if(serif!=null)Destroy(serif);}
    }
}



