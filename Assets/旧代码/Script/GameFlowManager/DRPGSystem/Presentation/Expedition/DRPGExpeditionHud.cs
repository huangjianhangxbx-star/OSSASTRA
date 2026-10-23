using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Map/bag/card pages share the existing battle Canvas and card hand.</summary>
public sealed class DRPGExpeditionHud : MonoBehaviour
{
    public DRPGDungeonRuntime runtime;
    public RectTransform hudRoot, cardArea;
    public TMP_FontAsset font;
    RectTransform pages, map, bag, tabs, eventWindow, controls, explorationInfo;
    TMP_Text message, location;
    int revision=-1, mapRevision=-1, inventoryRevision=-1;
    string mode="";
    bool wasExplore;
    readonly Color ink=new Color(.045f,.06f,.055f,.97f), gold=new Color(.78f,.69f,.43f);
    public RectTransform BagRect => bag;
    public RectTransform EventRect => eventWindow;
    public DRPGDungeonRuntime Runtime => runtime;
    public int SelectedSlot=-1;
    void Start()
    {
        if(runtime==null||hudRoot==null||cardArea==null){enabled=false;Debug.LogError("Expedition HUD missing runtime or existing HUD/card anchors.");return;}
        var rail=hudRoot.Find("ActorRail") as RectTransform;var skills=hudRoot.Find("SkillArea") as RectTransform;var recovery=hudRoot.Find("PostureRecovery") as RectTransform;
        if(rail!=null)Anchor(rail,.01f,.06f,.15f,.93f);
        if(skills!=null)Anchor(skills,.16f,.06f,.45f,.93f);
        if(recovery!=null)Anchor(recovery,.455f,.10f,.495f,.90f);
        Anchor(cardArea,.51f,.05f,.95f,.92f);
        pages=Panel("ExpeditionPages",hudRoot,.51f,.05f,.95f,.92f);
        map=Panel("MapPage",pages,0,0,1,1);bag=Panel("BagPage",pages,0,0,1,1);
        var bagDrop=bag.gameObject.AddComponent<DRPGExpeditionDropTarget>();bagDrop.hud=this;
        tabs=Panel("ExpeditionTabs",hudRoot,.955f,.05f,.995f,.92f);
        string[] names={"地图","行囊","卡牌"};
        for(int i=0;i<3;i++){string tab=names[i];Button(tabs,tab,0,1-(i+1)*.23f,1,1-i*.23f,()=>runtime.SelectTab(tab));}
        var canvas=hudRoot.GetComponentInParent<Canvas>().transform;
        controls=Panel("ExpeditionNavigation",canvas,.62f,.35f,.98f,.43f);
        message=Text("ExpeditionMessage",canvas,"",.24f,.71f,.91f,.76f,20);
        location=Text("ExpeditionLocation",canvas,"",.01f,.92f,.45f,.98f,24);
        eventWindow=Panel("ExpeditionEvent",canvas,.53f,.45f,.92f,.70f);eventWindow.gameObject.SetActive(false);
        explorationInfo=Panel("ExplorationCharacterInfo",hudRoot,.01f,.05f,.495f,.93f);
        var drop=eventWindow.gameObject.AddComponent<DRPGExpeditionDropTarget>();drop.hud=this;drop.Event=true;
    }
    void Update()
    {
        if(runtime==null||runtime.State==null||pages==null)return;
        bool explore=runtime.CanExplore;
        bool visible=explore||runtime.InBattle;
        pages.gameObject.SetActive(visible&&runtime.Tab!="卡牌");tabs.gameObject.SetActive(visible);
        cardArea.gameObject.SetActive(visible&&runtime.Tab=="卡牌");
        controls.gameObject.SetActive(explore);explorationInfo.gameObject.SetActive(explore);
        location.gameObject.SetActive(visible);message.gameObject.SetActive(visible);
        eventWindow.gameObject.SetActive(explore&&runtime.EventOpen);
        location.text="无名者的回廊  ·  "+runtime.State.VisitedCount+" / 10 房间";
        message.text=runtime.Message;
        if(revision==runtime.Revision&&mapRevision==runtime.State.Revision&&inventoryRevision==(runtime.Inventory?.Revision??-1)&&mode==runtime.Tab&&wasExplore==explore){UpdateDoor();return;}
        revision=runtime.Revision;mapRevision=runtime.State.Revision;inventoryRevision=runtime.Inventory?.Revision??-1;mode=runtime.Tab;wasExplore=explore;
        map.gameObject.SetActive(mode=="地图");bag.gameObject.SetActive(mode=="行囊");
        DrawMap();DrawBag();DrawEvent();DrawNavigation();DrawParty();
    }
    void UpdateDoor()
    {
        var b=controls.Find("点击门进入")?.GetComponent<Button>();if(b!=null)b.interactable=runtime.State.AtDoor&&!runtime.EventOpen;
    }
    void DrawNavigation()
    {
        Clear(controls);
        if(runtime.State.Edge>=0){Button(controls,"走向入口",0,0,.28f,1,()=>runtime.WalkTo(0));Button(controls,"走向出口",.30f,0,.58f,1,()=>runtime.WalkTo(runtime.State.Length));Button(controls,"点击门进入",.60f,0,1,1,runtime.EnterDoor);UpdateDoor();}
        else {
            Text("SelectRoute",controls,"点击右下地图的金色相邻房间选路",0,0,.72f,1,17);
            Button(controls,runtime.State.VisitedCount>=9?"完成远征":"撤离远征",.74f,0,1,1,()=>runtime.Loop.EndExpedition(runtime.State.VisitedCount>=9?ExpeditionExitReason.Completed:ExpeditionExitReason.Retreated));
        }
        if(runtime.ActiveEvent!=null&&!runtime.EventOpen&&(!runtime.ActiveEvent.Done||!runtime.ActiveEvent.Loot.IsEmpty))Button(controls,"调查 / 战利品",0,1.05f,.42f,1.6f,runtime.OpenEvent);
    }
    void DrawMap()
    {
        Clear(map);var state=runtime.State;var rooms=state.Rooms.Where(r=>r.Discovered).ToArray();
        int minX=rooms.Min(r=>r.X),maxX=rooms.Max(r=>r.X),minY=rooms.Min(r=>r.Y),maxY=rooms.Max(r=>r.Y);
        Func<DRPGDungeonRoom,Vector2> point=r=>new Vector2(.12f+.76f*(r.X-minX)/Mathf.Max(1,maxX-minX),.16f+.68f*(r.Y-minY)/Mathf.Max(1,maxY-minY));
        foreach(var edge in state.Corridors.Where(state.Visible)) {
            var a=point(state.Rooms[edge.A]);var b=point(state.Rooms[edge.B]);
            for(int i=0;i<edge.Cells.Length;i++){var v=Vector2.Lerp(a,b,(i+1f)/(edge.Cells.Length+1));var c=edge.Cells[i];var tile=Panel("CorridorCell",map,v.x-.012f,v.y-.018f,v.x+.012f,v.y+.018f);tile.GetComponent<Image>().color=c.Visited?gold:c.Known?new Color(.3f,.38f,.36f):new Color(.15f,.18f,.17f);if(c.Known&&!c.Done)Text("Event",tile,Symbol(c.Kind),0,0,1,1,12);}
        }
        foreach(var room in rooms){int id=room.Id;var pos=point(room);bool adjacent=state.Edge<0&&state.Corridors.Any(e=>e.A==state.Room&&e.B==id||e.B==state.Room&&e.A==id);var btn=Button(map,(id+1)+(room.Visited?"\n已探索":"\n?"),pos.x-.05f,pos.y-.08f,pos.x+.05f,pos.y+.08f,()=>runtime.Travel(id));btn.interactable=adjacent&&runtime.CanExplore&&!runtime.EventOpen;btn.GetComponent<Image>().color=state.Edge<0&&state.Room==id?new Color(.5f,.39f,.18f):adjacent?new Color(.29f,.27f,.16f):ink;}
        Text("Legend",map,"金色：可前往  ·  ?：未探索  ·  侦察揭示事件",.02f,0,.98f,.08f,12);
    }
    static string Symbol(DRPGDungeonEvent k)=>k==DRPGDungeonEvent.Battle?"敌":k==DRPGDungeonEvent.Treasure?"宝":k==DRPGDungeonEvent.Rubble?"障":k==DRPGDungeonEvent.Trap?"陷":k==DRPGDungeonEvent.Scout?"探":"";
    void DrawBag()
    {
        Clear(bag);var inventory=runtime.Inventory;if(inventory==null)return;
        int columns=8,rows=Mathf.CeilToInt(inventory.Capacity/(float)columns);
        for(int i=0;i<inventory.Capacity;i++){int index=i;var item=inventory.GetSlot(i).Item;float x=(i%columns)/(float)columns,y=1-(i/columns+1f)/rows;
            var button=Button(bag,item==null?"":item.DisplayName+"\n×"+item.Quantity,x+.005f,y+.01f,x+1f/columns-.005f,y+1f/rows-.01f,()=>{SelectedSlot=index;runtime.SelectTab("行囊");});
            var slot=button.gameObject.AddComponent<DRPGExpeditionItemDrag>();slot.hud=this;slot.Slot=i;
            var target=button.gameObject.AddComponent<DRPGExpeditionDropTarget>();target.hud=this;target.Slot=i;
            if(item?.Icon!=null){var icon=Panel("Icon",button.transform,.2f,.35f,.8f,.9f);icon.GetComponent<Image>().sprite=item.Icon;icon.GetComponent<Image>().preserveAspect=true;icon.GetComponent<Image>().color=Color.white;icon.GetComponent<Image>().raycastTarget=false;}
        }
    }
    void DrawEvent()
    {
        // Keep the drop target on the window root while replacing its content.
        Clear(eventWindow);if(!runtime.EventOpen||runtime.ActiveEvent==null)return;
        var cell=runtime.ActiveEvent;bool loot=!cell.Loot.IsEmpty;
        Text("Title",eventWindow,loot?"战利品":DRPGDungeonRuntime.EventName(cell.Kind),.04f,.76f,.95f,.97f,25);
        if(loot){var stacks=cell.Loot.Stacks.ToArray();for(int i=0;i<stacks.Length;i++){var stack=stacks[i];var b=Button(eventWindow,stack.Definition.DisplayName+" ×"+stack.Quantity,.04f+i*.27f,.29f,.28f+i*.27f,.73f,()=>runtime.Collect(stack.Definition.ItemId));var drag=b.gameObject.AddComponent<DRPGExpeditionItemDrag>();drag.hud=this;drag.LootId=stack.Definition.ItemId;}}
        else {Text("Hint",eventWindow,cell.Kind==DRPGDungeonEvent.Rubble?"将铲子拖到整个窗口，或徒手清理（光照 -20）":cell.Kind==DRPGDungeonEvent.Treasure?"将钥匙拖到窗口以获得额外战利品":"点击调查处理事件",.04f,.39f,.95f,.73f,17);Button(eventWindow,"直接调查",.04f,.07f,.32f,.29f,()=>runtime.ResolveEvent());Button(eventWindow,"使用选中道具",.35f,.07f,.66f,.29f,()=>runtime.Use(SelectedSlot));}
        Button(eventWindow,"暂时离开",.70f,.07f,.96f,.29f,runtime.CloseEvent);
    }
    void DrawParty()
    {
        Clear(explorationInfo);if(runtime.partySetup==null)return;
        var profiles=new[]{runtime.partySetup.Protagonist}.Concat(runtime.partySetup.PartyMembers).Where(p=>p!=null).ToArray();
        for(int i=0;i<profiles.Length;i++){var profile=profiles[i];float x=i/(float)profiles.Length;var p=Panel(profile.DisplayName,explorationInfo,x+.01f,.16f,x+1f/profiles.Length-.01f,.94f);var img=Panel("Portrait",p,.06f,.20f,.94f,.96f).GetComponent<Image>();img.sprite=profile.FullPortrait;img.preserveAspect=true;img.color=Color.white;float hp=runtime.Party.BattleState.TryGetHitPoints(profile.CombatantId,out float value)?value:profile.MaxHp;Text("Vitals",p,profile.DisplayName+"\nHP "+Mathf.CeilToInt(hp)+" / "+Mathf.CeilToInt(profile.MaxHp),0,0,1,.24f,15);}
        Text("Supplies",explorationInfo,"探索中  ·  光照 "+runtime.Torch+"  ·  A / D 行走  ·  拖出行囊遗弃整叠",.02f,.01f,.98f,.14f,14);
    }
    public RectTransform Panel(string name,Transform parent,float x0,float y0,float x1,float y1)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));var r=(RectTransform)go.transform;r.SetParent(parent,false);Anchor(r,x0,y0,x1,y1);go.GetComponent<Image>().color=ink;return r;
    }
    public TMP_Text Text(string name,Transform parent,string text,float x0,float y0,float x1,float y1,int size)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));var r=(RectTransform)go.transform;r.SetParent(parent,false);Anchor(r,x0,y0,x1,y1);var t=go.GetComponent<TextMeshProUGUI>();t.font=font;t.text=text;t.fontSize=size*1.65f;t.enableAutoSizing=true;t.fontSizeMin=14;t.fontSizeMax=size*1.65f;t.color=gold;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;return t;
    }
    Button Button(Transform parent,string title,float x0,float y0,float x1,float y1,Action action)
    {
        var r=Panel(title,parent,x0,y0,x1,y1);r.GetComponent<Image>().color=new Color(.09f,.105f,.09f,1);
        var border=r.gameObject.AddComponent<Outline>();border.effectColor=new Color(.60f,.53f,.32f,.65f);border.effectDistance=new Vector2(1.5f,-1.5f);
        var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();b.onClick.AddListener(()=>action());Text("Label",r,title,.02f,.02f,.98f,.98f,18);return b;
    }
    static void Anchor(RectTransform r,float x0,float y0,float x1,float y1){r.anchorMin=new Vector2(x0,y0);r.anchorMax=new Vector2(x1,y1);r.offsetMin=r.offsetMax=Vector2.zero;}
    static void Clear(Transform p){for(int i=p.childCount-1;i>=0;i--){var child=p.GetChild(i);child.gameObject.SetActive(false);Destroy(child.gameObject);}}
}
