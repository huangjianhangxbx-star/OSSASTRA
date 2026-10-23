using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DisallowMultipleComponent, DefaultExecutionOrder(100)]
public sealed class DRPGDungeonRuntime : MonoBehaviour
{
    public DRPGNodeBattleCoordinator coordinator;
    public DRPGEncounterDefinitionSO encounter;
    public DRPGPartySetupSO partySetup;
    public Sprite roomArt, corridorArt;
    public SpriteRenderer backdrop;
    public DRPGLootItemDefinitionSO shovel, key, gold, gem, food, torch;
    public DRPGDungeonState State { get; private set; }
    public DRPGDungeonCell ActiveEvent { get; private set; }
    public bool EventOpen { get; private set; }
    public bool InBattle { get; private set; }
    public string Message { get; private set; } = "选择相邻房间进入走廊";
    public int Revision { get; private set; }
    public int Torch { get; private set; } = 100;
    public string Tab { get; private set; } = "地图";
    public DRPGPartyInventoryState Inventory => GameFlowRoot.Instance?.drpgEquipment?.Equipment?.PartyInventory;
    public DRPGGameLoopRuntimeRoot Loop => GameFlowRoot.Instance?.drpgGameLoop;
    public DRPGNodePartyRoot Party => coordinator != null ? coordinator.Context?.PartyRoot : null;
    public bool CanExplore => Loop != null && Loop.CanUseExplorationUI && !InBattle;
    public DRPGLootContainerState Loot => ActiveEvent?.Loot;
    float originX, velocity;
    float? destination;
    bool initialized, seededSupplies, expeditionStarted;
    bool MovementPaused => GameFlowRoot.Instance?.huntFlow?.huntFlowDataBus.isPaused == true;
    SpriteRenderer[] scenery;
    DRPGEncounterPoint[] oldPoints;
    bool[] oldPointEnabled;
    WebCryptBattleBackdrop oldBackdrop;
    bool oldBackdropEnabled;
    Vector3 startPosition;
    DRPGDungeonCell battleCell, pendingSettlementCell;
    void Start()
    {
        if(coordinator==null||Party==null||backdrop==null){enabled=false;Debug.LogError("Dungeon integration requires configured coordinator, party and backdrop.");return;}
        startPosition=Party.transform.position;originX=startPosition.x;
        Party.ExternalMovement=true;
        oldPoints=FindObjectsByType<DRPGEncounterPoint>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        oldPointEnabled=oldPoints.Select(p=>p.enabled).ToArray();foreach(var p in oldPoints)p.enabled=false;
        oldBackdrop=backdrop.GetComponent<WebCryptBattleBackdrop>();
        if(oldBackdrop!=null){oldBackdropEnabled=oldBackdrop.enabled;oldBackdrop.enabled=false;}
        scenery=new SpriteRenderer[3];scenery[1]=backdrop;
        for(int i=0;i<3;i++)if(i!=1){var go=new GameObject("ExpeditionBackdrop"+i);go.transform.SetParent(transform,false);var r=go.AddComponent<SpriteRenderer>();r.sharedMaterial=backdrop.sharedMaterial;r.sortingLayerID=backdrop.sortingLayerID;r.sortingOrder=backdrop.sortingOrder;scenery[i]=r;}
        coordinator.BattleBegan+=BattleBegan;coordinator.BattleFinished+=BattleFinished;
        initialized=true;ResetRoute(Environment.TickCount,false);
    }
    public void ResetRoute(int seed,bool test)
    {
        if(InBattle||!initialized)return;
        pendingSettlementCell=null;State=new DRPGDungeonState(seed,test);ActiveEvent=null;EventOpen=false;Torch=100;velocity=0;destination=null;
        Party.transform.position=startPosition;Party.ConfigureForRuntime(4,originX,originX,2.25f);Party.SetExplorationEnabled(true);
        Tab="地图";Message=test?"交互测试路线：空段→石堆→宝箱→侦察→战斗":"点击地图相邻节点选路";BuildScenery();Revision++;
    }
    void Update()
    {
        if(!initialized||State==null)return;
        // Keep the existing reward container alive at the encounter site. No second reward roll.
        if(pendingSettlementCell!=null&&Loop!=null&&Loop.AnkalutePhase==AnkaluteGameLoopPhase.BattleSettlement){
            var loot=Loop.ActiveSettlement?.Loot;var rewardCell=pendingSettlementCell;
            if(Loop.CompleteSettlement()){
                pendingSettlementCell=null;if(loot!=null)rewardCell.Loot=loot;
                ActiveEvent=rewardCell;EventOpen=!rewardCell.Loot.IsEmpty;Tab=EventOpen?"行囊":"地图";
                Message=EventOpen?"战斗胜利，点击或拖拽收取战利品":"战斗胜利，继续原地探索";Revision++;
            }
        }
        if(Loop!=null&&(Loop.AnkalutePhase==AnkaluteGameLoopPhase.Title||Loop.AnkalutePhase==AnkaluteGameLoopPhase.StrategicHub))expeditionStarted=false;
        if(CanExplore&&!expeditionStarted){expeditionStarted=true;ResetRoute(Environment.TickCount,false);}
        if(CanExplore&&!seededSupplies&&Inventory!=null){seededSupplies=true;Inventory.TryAddLoot(shovel,3);Inventory.TryAddLoot(key,2);Inventory.TryAddLoot(food,12);Inventory.TryAddLoot(torch,8);Revision++;}
        bool visible=Loop!=null&&(CanExplore||InBattle||Loop.AnkalutePhase==AnkaluteGameLoopPhase.BattleSettlement);
        foreach(var r in scenery)if(r!=null)r.enabled=visible;
        if(!CanExplore||MovementPaused||EventOpen||State.Edge<0){velocity=0;destination=null;return;}
        var keyboard=Keyboard.current;
        float dir=keyboard==null?0:(keyboard.dKey.isPressed||keyboard.rightArrowKey.isPressed?1:0)-(keyboard.aKey.isPressed||keyboard.leftArrowKey.isPressed?1:0);
        if(dir!=0)destination=null;
        var mouse=Mouse.current;
        if(mouse!=null&&mouse.leftButton.wasPressedThisFrame&&!(EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject())){
            var cam=coordinator.Context.SceneCamera;var pos=mouse.position.ReadValue();
            destination=Mathf.Clamp(State.Distance+(cam.ScreenToWorldPoint(new Vector3(pos.x,pos.y,Mathf.Abs(cam.transform.position.z))).x-Party.transform.position.x),0,State.Length);
        }
        if(dir==0&&destination.HasValue)dir=Mathf.Sign(destination.Value-State.Distance);
        velocity=Mathf.Lerp(velocity,dir*4f,1-Mathf.Exp(-Time.deltaTime*(dir==0?16:9)));
        float next=State.Distance+velocity*Time.deltaTime;
        if(destination.HasValue&&(destination.Value-State.Distance)*(destination.Value-next)<=0){next=destination.Value;destination=null;velocity=0;}
        var cell=State.Move(next);
        var p=Party.transform.position;p.x=originX+State.Distance;Party.transform.position=p;
        if(cell!=null){velocity=0;destination=null;Encounter(cell);}
    }
    public void WalkTo(float distance){if(!CanExplore||EventOpen||State.Edge<0)return;destination=Mathf.Clamp(distance,0,State.Length);}
    public void Travel(int room)
    {
        if(!CanExplore||EventOpen||!State.Travel(room))return;
        ActiveEvent=null;velocity=0;destination=null;Party.transform.position=startPosition;
        Party.ConfigureForRuntime(4,originX,originX+State.Length,2.25f);BuildScenery();Message="A / D 连续行走 · 点击地面移动 · 到门前点击进入";Revision++;
    }
    public void EnterDoor()
    {
        if(!CanExplore||EventOpen||!State.EnterDoor())return;
        ActiveEvent=null;Tab="地图";velocity=0;destination=null;Party.transform.position=startPosition;
        Party.ConfigureForRuntime(4,originX,originX);coordinator.Context.CenterExplorationCamera();BuildScenery();
        Message="进入房间 "+(State.Room+1)+"，已显示相邻道路";Revision++;
    }
    void Encounter(DRPGDungeonCell cell)
    {
        ActiveEvent=cell;
        if(cell.Kind==DRPGDungeonEvent.Battle&&!cell.Done){battleCell=cell;var result=coordinator.TryStart(encounter);if(result!=DRPGBattleStartResult.Started){battleCell=null;EventOpen=true;Message="遇敌未能开始："+coordinator.LastStartError;Revision++;}return;}
        EventOpen=true;Tab="行囊";Message="发现 "+EventName(cell.Kind);Revision++;
    }
    void BattleBegan(){Message="遭遇敌人";InBattle=true;velocity=0;destination=null;Tab="卡牌";Revision++;}
    void BattleFinished(DRPGBattleCompletionContext result)
    {
        InBattle=false;
        if(battleCell!=null&&result.Result.Outcome==DRPGBattleOutcome.Victory){State.Complete(battleCell);pendingSettlementCell=battleCell;}
        battleCell=null;ActiveEvent=null;EventOpen=false;Message="战斗结束，留在原走廊位置";Revision++;
    }
    public static string EventName(DRPGDungeonEvent kind)=>kind==DRPGDungeonEvent.Rubble?"坍塌石堆":kind==DRPGDungeonEvent.Treasure?"遗物宝箱":kind==DRPGDungeonEvent.Trap?"地面机关":kind==DRPGDungeonEvent.Scout?"古老地图台":kind==DRPGDungeonEvent.Battle?"敌人":"空段";
    public void SelectTab(string tab){Tab=tab;Revision++;}
    public void CloseEvent(){EventOpen=false;Message="继续探索，未领取的战利品保留在原处";Revision++;}
    public void OpenEvent(){if(ActiveEvent==null||!CanExplore||(ActiveEvent.Done&&ActiveEvent.Loot.IsEmpty))return;EventOpen=true;Tab="行囊";Revision++;}
    public bool CanUse(int index)
    {
        var item=Inventory?.GetSlot(index).Item;
        return EventOpen&&ActiveEvent!=null&&!ActiveEvent.Done&&item!=null&&item.LootDefinition!=null&&
            (ActiveEvent.Kind==DRPGDungeonEvent.Rubble&&item.LootDefinition==shovel||ActiveEvent.Kind==DRPGDungeonEvent.Treasure&&item.LootDefinition==key);
    }
    public void Use(int index)
    {
        if(!CanExplore||!CanUse(index)){Message="不适用：道具没有消耗";Revision++;return;}
        var need=ActiveEvent.Kind==DRPGDungeonEvent.Rubble?shovel:key;
        if(!DRPGExpeditionItems.Consume(Inventory,index,need.ItemId))return;
        ResolveEvent(true);
    }
    public void ResolveEvent(bool supplied=false)
    {
        if(!CanExplore||ActiveEvent==null||ActiveEvent.Done||ActiveEvent.Kind==DRPGDungeonEvent.Battle)return;
        var c=ActiveEvent;
        if(c.Kind==DRPGDungeonEvent.Treasure){c.Loot.Add(gold,150);c.Loot.Add(food,2);if(supplied)c.Loot.Add(gem,1);Message="点击战利品收取，或拖到行囊";}
        else if(c.Kind==DRPGDungeonEvent.Scout){State.Scout();Message="侦察成功，周围事件已揭示";}
        else if(c.Kind==DRPGDungeonEvent.Rubble){if(!supplied)Torch=Math.Max(0,Torch-20);Message=supplied?"铲子已清除障碍":"徒手清理完成，消耗20光照";}
        else if(c.Kind==DRPGDungeonEvent.Trap){Message="机关已拆除";}
        State.Complete(c);if(c.Loot.IsEmpty)EventOpen=false;Revision++;
    }
    public void Collect(string id,int slot=-1)
    {
        if(!CanExplore||!EventOpen)return;
        int amount=DRPGExpeditionItems.Collect(Loot,Inventory,id,slot);
        Message=amount>0?"收取成功："+amount+" 件":"行囊已满或目标不适用，请先手动遗弃腾位";
        if(Loot!=null&&Loot.IsEmpty)EventOpen=false;Revision++;
    }
    public void Discard(int slot){if(!CanExplore)return;var result=DRPGInventoryTransferService.Discard(Inventory,slot,null);Message=result.Success?"已遗弃整叠物品":result.Message;Revision++;}
    public void MoveItem(int from,int to){if(!CanExplore)return;var result=DRPGInventoryTransferService.Transfer(Inventory,from,Inventory,to);Message=result.Success?"整理成功":result.Message;Revision++;}
    void BuildScenery()
    {
        var cam=coordinator.Context.SceneCamera;var sprite=State.Edge<0?roomArt:corridorArt;if(sprite==null)return;
        float height=cam.orthographicSize*2;float scale=Mathf.Max(height/sprite.bounds.size.y,height*cam.aspect/sprite.bounds.size.x);
        float width=sprite.bounds.size.x*scale;
        for(int i=0;i<3;i++){var r=scenery[i];r.sprite=sprite;r.transform.localScale=new Vector3(scale,scale,1);r.transform.position=new Vector3(originX+height*cam.aspect*.25f+(i-1)*width,cam.transform.position.y+cam.orthographicSize*(State.Edge<0?.35f:.55f),0);}
    }
    public string Inspect()=>State==null?"尚未启动":$"seed={State.Seed}; room={State.Room}; edge={State.Edge}; distance={State.Distance:F2}; event={ActiveEvent?.Kind}; battle={InBattle}; bag={Inventory?.UsedSlots}/{Inventory?.Capacity}";
    void OnDestroy()
    {
        if(coordinator!=null){coordinator.BattleBegan-=BattleBegan;coordinator.BattleFinished-=BattleFinished;}
        if(Party!=null)Party.ExternalMovement=false;
        if(oldPoints!=null)for(int i=0;i<oldPoints.Length;i++)if(oldPoints[i]!=null)oldPoints[i].enabled=oldPointEnabled[i];
        if(oldBackdrop!=null)oldBackdrop.enabled=oldBackdropEnabled;
    }
}
