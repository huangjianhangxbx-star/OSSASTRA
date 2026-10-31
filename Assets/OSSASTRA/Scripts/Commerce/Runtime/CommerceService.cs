/* 脚本：CommerceService
 * 功能：本地图唯一交易与共同保存组装根；接入账本、uGUI及显式领域值载荷，同修订串行保存。
 * 时序：Awake注入库存启动适配器；库存建立定义后先恢复，只有Missing才播种；Ready后才开放交互。
 * 退出：禁用先在主线程冻结最新快照并排空；销毁在解绑/释放领域前等待关闭，不能只排空旧请求。
 * 边界：不更新角色子模块、动画或音声，不暂停世界。后台只接收主线程冻结的字节。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Ossastra.Inventory;
using Ossastra.UserInterface;
using Ossastra.Characters;
using Ossastra.Combat;
using Ossastra.Commerce.Persistence;
using Ossastra.Commerce.Presentation;
namespace Ossastra.Commerce.Runtime
{
    using CharacterRoot=Ossastra.Characters.CharacterRoot;
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class CommerceService:MonoBehaviour,IInventoryStartupAdapter,ICommerceAccessPolicy,ICommercePresentationHost
    {
        public InventorySession inventory;
        public InventoryView inventoryView;
        public CommerceUIRoot userInterface;
        public MerchantShop[] merchants=Array.Empty<MerchantShop>();
        public MerchantDefinitionSO[] merchantCatalog=Array.Empty<MerchantDefinitionSO>();
        public CommerceWorldContainer[] worldContainers=Array.Empty<CommerceWorldContainer>();
        public string mapId="commerce-map",profileId="commerce-main";
        [Tooltip("留空使用persistentDataPath/Commerce；验证档与正式档分别配置，不自动删除档案。")]
        public string saveDirectory;
        [Min(.25f)] public float checkpointSeconds=3;
        public CommerceEconomy Economy {get;private set;}
        public CommerceSaveStatus SaveStatus=>saves?.Status;
        public bool Ready=>isActiveAndEnabled&&initialized&&inventory!=null&&inventory.Ready;
        public string Diagnostic {get;private set;}="尚未初始化";
        public string SavePath=>store?.PathForProfile(profileId);
        public long Revision=>revision;
        private readonly CommerceSnapshotParticipants snapshotParticipants=new CommerceSnapshotParticipants();
        private CommerceFileSaveStore store;
        private CommerceSaveCoordinator saves;
        private readonly Dictionary<string,MerchantShop> targets=new Dictionary<string,MerchantShop>(StringComparer.Ordinal);
        private readonly Dictionary<string,TradeQuote> quotes=new Dictionary<string,TradeQuote>(StringComparer.Ordinal);
        private readonly HashSet<string> dialogueFlags=new HashSet<string>(StringComparer.Ordinal);
        private CommerceDialogue dialogue;
        private string currentTarget,currentActor;
        private long revision,sessionGeneration,loadedSequence;
        private double lastSavedSimulation;
        private float nextCheckpoint;
        private bool initialized,uiReady,restoring,closing;
        private string sessionId;
        private bool finalizingPersistence;
        private byte[] lifecyclePayload;
        private string lifecycleFailure;
        /// <summary>Awake组装期登记；库存开始恢复后封口，不能绕过已验证的共同快照边界。</summary>
        public void RegisterSnapshotParticipant(ICommerceSnapshotParticipant participant)
        {
            if(initialized||restoring||closing)throw new InvalidOperationException("领域必须在库存恢复前登记");
            snapshotParticipants.Register(participant);
        }
        private void Awake()
        {
            if(inventory==null){Diagnostic="CommerceService缺少InventorySession";enabled=false;return;}
            inventory.ConfigureStartupAdapter(this);
            // 禁用交易只撤下交易入口；普通背包继续可用。保存故障仍须守住同一编辑门。
            inventory.ConfigureEditGate(()=>saves!=null&&saves.Status.CanEdit);
        }
        /// <summary>不把坏档当空档。所有配置及身份先校验，恢复成功之前没有初始物品授予。</summary>
        public bool RestoreOrSeed(InventorySession session,out bool seed,out string error)
        {
            seed=false;error=null;snapshotParticipants.FreezeRegistration();restoring=true;
            try
            {
                sessionId=Guid.NewGuid().ToString("N");
                var actors=session.Coordinator.Characters.Select(c=>c.ActorId).ToArray();
                if(actors.Any(string.IsNullOrWhiteSpace)||actors.Distinct().Count()!=actors.Length)throw new InvalidOperationException("角色稳定ID缺失或重复");
                if(merchants==null||merchants.Length<1)throw new InvalidOperationException("尚未配置商人实例");
                if(worldContainers==null||worldContainers.Any(c=>c==null||string.IsNullOrWhiteSpace(c.instanceId))||worldContainers.Select(c=>c.instanceId).Distinct().Count()!=worldContainers.Length)
                    throw new InvalidOperationException("箱子代理缺失或稳定ID重复；没有自动改写ID");
                Economy=new CommerceEconomy(session.Ledger,this);
                foreach(var catalog in merchantCatalog??Array.Empty<MerchantDefinitionSO>())
                {if(catalog==null)throw new InvalidOperationException("全局商店目录缺失");Economy.RegisterDefinition(catalog.Freeze());}
                foreach(var merchant in merchants)
                {
                    if(merchant==null||merchant.definition==null||string.IsNullOrWhiteSpace(merchant.Id)||targets.ContainsKey(merchant.Id))throw new InvalidOperationException("商人ID重复/缺失或模板未接入；没有自动改写ID");
                    targets.Add(merchant.Id,merchant);Economy.RegisterMerchant(merchant.definition.Freeze(),merchant.Id);
                }
                session.SetActiveMap(mapId);
                store=new CommerceFileSaveStore(string.IsNullOrWhiteSpace(saveDirectory)?System.IO.Path.Combine(Application.persistentDataPath,"Commerce"):saveDirectory);
                CommerceRuntimeSnapshot decoded=null;
                var loaded=store.Load(profileId,snapshot=>
                {
                    try
                    {
                        if(snapshot.PayloadSchema!=2||snapshot.DefinitionFingerprint!=Economy.DefinitionFingerprint)return "保存格式或商品配置指纹不匹配";
                        decoded=CommerceSnapshotCodec.Decode(snapshot.Payload);
                        return ValidateSnapshot(decoded,actors);
                    }
                    catch(Exception ex){return "经济载荷损坏："+ex.Message;}
                });
                if(loaded.Status==CommerceSaveLoadStatus.Invalid)throw new InvalidOperationException("存档未启用，原文件已保留："+loaded.Error);
                if(loaded.Status==CommerceSaveLoadStatus.Loaded)
                {
                    var runtimeCandidate=session.PrepareRuntimeState(decoded.inventoryRuntime,decoded.economy.inventory,out error);
                    if(runtimeCandidate==null)throw new InvalidOperationException(error);
                    if(!snapshotParticipants.TryPrepareRestore(decoded,out var sectionsCandidate,out error))throw new InvalidOperationException(error);
                    var restored=Economy.Restore(decoded.economy);if(!restored.Ok)throw new InvalidOperationException(restored.Reason);
                    // Start仍在同步启动门内，尚无消费/界面订阅；这之后只发布预分配宿主引用。
                    session.PublishRuntimeState(runtimeCandidate);
                    snapshotParticipants.PublishRestore(sectionsCandidate);
                    session.SetActiveMap(mapId);
                    foreach(var target in targets.Values)
                        if(!Economy.HasMerchant(target.Id))Economy.RegisterMerchant(target.definition.Freeze(),target.Id);
                    foreach(var flag in decoded.dialogueFlags)dialogueFlags.Add(flag);
                    revision=decoded.revision;loadedSequence=loaded.Snapshot.SaveSequence;
                    Diagnostic="已恢复经济档案（尚未开放交互）";
                }
                else{seed=true;revision=1;Diagnostic="首次档案，初始化一次";}
                // 恢复已有逻辑箱时此口只重连；新地图第一次见到的箱子才原子创建并授予整箱。
                foreach(var container in worldContainers)
                {
                    if(container==null)throw new InvalidOperationException("箱子代理缺失");
                    var registered=session.RegisterWorldContainer(container.instanceId,container.mapId,container.transform.position,container.width,container.height,container.displayName,container.initialContents);
                    if(!registered.Ok)throw new InvalidOperationException(registered.Reason);
                }
                saves=new CommerceSaveCoordinator(store,profileId,sessionId,revision,loadedSequence);
                return true;
            }
            catch(Exception ex){error=Diagnostic=ex.Message;Debug.LogError("[COM-01] "+error,this);return false;}
            finally{restoring=false;}
        }
        private string ValidateSnapshot(CommerceRuntimeSnapshot value,string[] actors)
        {
            if(value==null||value.schema!=2||value.revision<0||value.revision==long.MaxValue||value.economy==null||value.inventoryRuntime==null||value.dialogueFlags==null||value.merchantIds==null||value.ownerIds==null)return "经济快照字段或版本损坏";
            if(!value.ownerIds.OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(actors.OrderBy(x=>x,StringComparer.Ordinal)))return "存档角色稳定ID与当前队伍不匹配";
            if(value.merchantIds.Any(string.IsNullOrWhiteSpace)||value.merchantIds.Distinct().Count()!=value.merchantIds.Length||!value.merchantIds.OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(value.economy.merchants.Select(m=>m.instanceId).OrderBy(x=>x,StringComparer.Ordinal)))return "商人实例索引缺失或稳定ID重复";
            foreach(var target in targets.Values)
            {var saved=value.economy.merchants.FirstOrDefault(m=>m.instanceId==target.Id);if(saved!=null&&saved.definitionId!=target.definition.definitionId)return "已保存商人ID与当前目录冲突";}
            if(value.dialogueFlags.Any(string.IsNullOrWhiteSpace)||value.dialogueFlags.Distinct().Count()!=value.dialogueFlags.Length)return "对话标记重复或缺失";
            // 先在脱离活状态的候选账本验证领域，再验证宿主字段，避免半本账被启用。
            var probe=new InventoryLedger();foreach(var definition in inventory.InventoryDefinitions)probe.Register(definition);
            using(var economy=new CommerceEconomy(probe,this))
            {
                foreach(var catalog in merchantCatalog??Array.Empty<MerchantDefinitionSO>())economy.RegisterDefinition(catalog.Freeze());
                foreach(var target in targets.Values)economy.RegisterMerchant(target.definition.Freeze(),target.Id);
                var result=economy.Restore(value.economy);if(!result.Ok)return result.Reason;
            }
            var runtimeError=inventory.ValidateRuntimeState(value.inventoryRuntime,value.economy.inventory);
            if(runtimeError!=null)return runtimeError;
            return snapshotParticipants.TryPrepareRestore(value,out var prepared,out var sectionError)?null:sectionError;
        }
        public void OnInventoryReady(InventorySession session)
        {
            if(saves==null||Economy==null)return;
            initialized=true;inventory.RuntimeChanged+=MarkDirty;Economy.Changed+=MarkDirty;snapshotParticipants.Changed+=MarkDirty;
            MarkDirty();RequestSave();nextCheckpoint=Time.unscaledTime+checkpointSeconds;
        }
        private void MarkDirty()
        {
            if(restoring||!initialized||saves==null)return;
            revision=checked(revision+1);saves.MarkDirty(revision);
        }
        private void Update()
        {
            if(!uiReady&&inventoryView!=null&&inventoryView.Skin!=null&&userInterface!=null)
            {userInterface.Initialize(inventoryView,inventoryView.Skin,this);uiReady=true;}
            if(!Ready)
            {
                if(uiReady)userInterface.SetPersistenceStatus("经济模块未启用；没有重新授予物品。\n"+Diagnostic,false);
                return;
            }
            Economy.Advance(Time.deltaTime);
            if(currentActor!=null&&inventory.Coordinator.Current?.ActorId!=currentActor){userInterface?.Close();CloseSession();}
            if(Time.unscaledTime>=nextCheckpoint)
            {
                nextCheckpoint=Time.unscaledTime+checkpointSeconds;
                if(Economy.SimulationTime>lastSavedSimulation){MarkDirty();RequestSave();}
            }
            if(userInterface!=null)
            {
                var s=saves.Status;
                userInterface.SetPersistenceStatus(s.HasFailure?"内存变更尚未保存；交易/背包编辑暂锁定，战斗继续。\n"+s.LastError:s.IsSaving?"经济资料保存中…":null,s.HasFailure);
            }
        }
        public CommerceRuntimeSnapshot Capture()
        {
            var economy=Economy.Capture();
            economy.dialogueFlags=dialogueFlags.OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            economy.ownerKeys=inventory.Coordinator.Characters.Select(c=>c.ActorId).ToArray();
            return new CommerceRuntimeSnapshot{revision=revision,economy=economy,inventoryRuntime=inventory.CaptureRuntimeState(),dialogueFlags=economy.dialogueFlags,
                merchantIds=economy.merchants.Select(m=>m.instanceId).OrderBy(x=>x,StringComparer.Ordinal).ToArray(),ownerIds=economy.ownerKeys,sections=snapshotParticipants.Capture()};
        }
        public void RequestSave()
        {
            if(!Ready||closing||saves==null)return;
            var snapshot=Capture();var bytes=CommerceSnapshotCodec.Encode(snapshot);
            saves.RequestSave(revision,bytes,2,Economy.DefinitionFingerprint);lastSavedSimulation=Economy.SimulationTime;
        }
        /// <summary>正常地图加载器必须先等待此屏障；卸载仅清表现，当前经济档落盘成功才允许切图。</summary>
        public System.Threading.Tasks.Task<bool> FlushBeforeSceneChangeAsync()
        {
            if(!Ready)return System.Threading.Tasks.Task.FromResult(false);
            userInterface?.Close();CloseSession();RequestSave();return saves.FlushAsync();
        }
        public void RetrySave(){if(!Ready)return;MarkDirty();RequestSave();_ = saves.RetryAsync();}
        public CommerceAccess Check(string actorId,string merchantId,long generation)
        {
            var actor=inventory==null?null:inventory.Coordinator.Current;
            var allowed=Ready&&saves!=null&&saves.Status.CanEdit&&generation==sessionGeneration&&actor!=null&&actor.ActorId==actorId&&
                actor.IsAlive&&!actor.IsSimulationSuspended&&targets.TryGetValue(merchantId??"",out var target)&&TargetAccessible(target,actor);
            var payment=new List<string>{"team","safe"};if(inventory!=null&&inventory.CanAccess("reserve"))payment.Add("reserve");
            return new CommerceAccess(allowed,allowed?null:"商人、距离/视线、控制会话或保存状态已失效",inventory?.Ledger?.AccessGeneration??0,payment.ToArray(),"team","team",payment.ToArray());
        }
        private bool TargetAccessible(MerchantShop target,CharacterRoot actor)
        {
            if(target==null||!target.isActiveAndEnabled||actor==null||target.gameObject==actor.gameObject)return false;
            var other=target.GetComponent<CharacterRoot>();if(other!=null&&!other.IsAlive)return false;
            var delta=target.transform.position-actor.transform.position;
            return Mathf.Abs(delta.y)<=.5f&&new Vector2(delta.x,delta.z).magnitude<=target.interactionRange&&MerchantLineClear(actor,target);
        }
        private static bool MerchantLineClear(CharacterRoot actor,MerchantShop target)
        {
            var origin=actor.transform.position+Vector3.up*.8f;var end=target.transform.position+Vector3.up*.8f;var delta=end-origin;
            if(delta.sqrMagnitude<.00001f)return true;
            // NPC不属于战斗SpatialTarget；只排除交互双方自己的碰撞体，墙/其他世界遮挡仍然拒绝访问。
            return !Physics.RaycastAll(origin,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore).Any(hit=>
                !hit.collider.transform.IsChildOf(actor.transform)&&!hit.collider.transform.IsChildOf(target.transform)&&CombatSpatialTarget.Find(hit.collider)==null);
        }
        public MerchantShop NearestTarget()
        {
            if(!Ready||!uiReady||!saves.Status.CanEdit)return null;
            var actor=inventory.Coordinator.Current;return targets.Values.Where(t=>TargetAccessible(t,actor)).OrderBy(t=>(t.transform.position-actor.transform.position).sqrMagnitude).ThenBy(t=>t.Id,StringComparer.Ordinal).FirstOrDefault();
        }
        public bool OpenRole(MerchantShop target)
        {
            if(target==null||!uiReady)return false;
            userInterface.Close();
            CloseSession();currentActor=inventory.Coordinator.Current.ActorId;currentTarget=target.Id;sessionGeneration++;
            return userInterface.OpenRoleMenu(target.Id,target.displayName,TargetScreenPosition(target.Id),target.personalShop);
        }
        public bool IsTargetValid(string targetId)=>Ready&&targets.TryGetValue(targetId??"",out var target)&&TargetAccessible(target,inventory.Coordinator.Current)&&
            (currentActor==null||currentActor==inventory.Coordinator.Current.ActorId);
        public Vector2 TargetScreenPosition(string targetId)
        {
            var camera=Camera.main;return camera!=null&&targets.TryGetValue(targetId??"",out var t)?(Vector2)camera.WorldToScreenPoint(t.transform.position):new Vector2(Screen.width/2f,Screen.height/2f);
        }
        public CommerceShopPresentation OpenShop(string targetId)
        {
            if(!IsTargetValid(targetId))return new CommerceShopPresentation{available=false,reason="交互目标已经失效"};
            currentTarget=targetId;currentActor=inventory.Coordinator.Current.ActorId;quotes.Clear();return ReadShop();
        }
        public CommerceShopPresentation ReadShop()
        {
            if(currentTarget==null||!targets.TryGetValue(currentTarget,out var target)||!IsTargetValid(currentTarget))return new CommerceShopPresentation{reason="商店不可访问"};
            var state=Economy.Merchant(currentTarget);var definition=target.definition.Freeze();var status=saves.Status;
            var balance=status.CanEdit?Economy.Balance(currentActor,currentTarget,sessionGeneration):inventory.Ledger.Items.Where(i=>i.DefinitionId=="trade-coin"&&new[]{"team","safe","reserve"}.Contains(i.Location.ContainerId)&&inventory.CanAccess(i.Location.ContainerId)).Sum(i=>(long)i.Quantity);
            return new CommerceShopPresentation{available=true,merchantId=currentTarget,displayName=target.displayName,reason=status.CanEdit?null:status.LastError,
                revision=state.revision,refreshSeconds=Math.Max(0,state.nextRefresh-Economy.SimulationTime),affordableCurrency=balance,
                currencySources="实体钱币：团队包 → 安全箱 → 当前可访问备用包",saveStatus=status.HasFailure?"保存失败：当前内存变更尚未落盘\n"+status.LastError:status.IsSaving?"保存中…":status.IsSaved?"已保存":"待保存",canRetrySave=status.HasFailure,
                items=definition.Goods.Where(g=>g.BuyPrice>=0).Select(g=>{var d=inventory.Definition(g.DefinitionId);var stock=state.stock.Single(s=>s.definitionId==g.DefinitionId).quantity;
                    return new CommerceItemPresentation{definitionId=g.DefinitionId,displayName=d.displayName,description=d.description,category=d.category,rarity=d.rarity.ToString(),icon=d.icon,
                        stock=stock,width=d.width,height=d.height,maxStack=d.maxStack,unitPrice=g.BuyPrice,unitWeightGrams=d.unitWeightGrams,canBuy=stock>0,reason=stock>0?null:"暂时售罄"};}).ToArray()};
        }
        public CommerceQuotePresentation QuotePurchase(string definitionId,int quantity)=>Quote(true,definitionId,quantity);
        public CommerceQuotePresentation QuoteSale(string itemId,int quantity)=>Quote(false,itemId,quantity);
        private CommerceQuotePresentation Quote(bool buying,string id,int quantity)
        {
            if(currentActor==null||currentTarget==null)return new CommerceQuotePresentation{reason="未打开商店"};
            var result=buying?Economy.QuotePurchase(currentActor,currentTarget,id,quantity,sessionGeneration):Economy.QuoteSale(currentActor,currentTarget,id,quantity,sessionGeneration);
            if(!result.Ok)return new CommerceQuotePresentation{reason=result.Reason};
            var q=result.Quote;var token=q.QuoteId.ToString("N");quotes.Clear();quotes[token]=q;
            var max=buying?Economy.Merchant(currentTarget).stock.Single(s=>s.definitionId==id).quantity:inventory.Ledger.Find(id)?.Quantity??0;
            return new CommerceQuotePresentation{ok=true,buying=buying,token=token,definitionId=q.DefinitionId,itemId=q.ItemId,quantity=q.Quantity,maximumQuantity=max,unitPrice=q.UnitPrice,totalPrice=q.TotalPrice};
        }
        public CommerceUiResult Confirm(string quoteToken)
        {
            if(!quotes.TryGetValue(quoteToken??"",out var quote))return new CommerceUiResult(false,"报价失效，请重新选择");
            var result=Economy.Execute(new TradeCommand(quote.QuoteId,quote));
            if(result.Ok){quotes.Remove(quoteToken);RequestSave();}
            return new CommerceUiResult(result.Ok,result.Ok?$"{(quote.Direction==TradeDirection.Purchase?"购入":"售出")} {quote.Quantity} 件，合计 {quote.TotalPrice} 钱币":result.Reason);
        }
        public CommerceDialoguePresentation StartDialogue(string targetId)
        {
            if(!IsTargetValid(targetId)||!targets[targetId].personalShop)return new CommerceDialoguePresentation{reason="该目标没有角色对话"};
            if(targets[targetId].dialogue==null)return new CommerceDialoguePresentation{reason="对话内容池未配置"};
            currentTarget=targetId;currentActor=inventory.Coordinator.Current.ActorId;
            dialogue=new CommerceDialogue(targets[targetId].dialogue.Freeze(),dialogueFlags,(key,value)=>{MarkDirty();RequestSave();});
            return PresentDialogue(dialogue.Start(targetId));
        }
        public CommerceDialoguePresentation ContinueDialogue(int choiceIndex)
        {
            if(!IsTargetValid(currentTarget))return new CommerceDialoguePresentation{reason="角色离开了交互范围"};
            if(dialogue==null)return new CommerceDialoguePresentation{reason="当前没有对话会话"};
            return PresentDialogue(dialogue.Continue(Math.Max(0,choiceIndex)));
        }
        private CommerceDialoguePresentation PresentDialogue(CommerceDialogueFrame frame)
            =>new CommerceDialoguePresentation{available=frame.Ok,completed=frame.Completed,speaker=currentTarget!=null&&targets.ContainsKey(currentTarget)?targets[currentTarget].displayName:frame.SpeakerKey,text=frame.Text,choices=frame.Choices,reason=frame.Reason};
        public void CloseSession(){dialogue?.Cancel();dialogue=null;currentTarget=null;currentActor=null;quotes.Clear();sessionGeneration++;}
        /// <summary>Unity已将enabled置false时也必须保存；只在主线程捕获，后台仍只处理不可变字节。</summary>
        private void FinalizePersistence(bool closeWriter)
        {
            if(saves==null||finalizingPersistence)return;
            if(closeWriter)closing=true;
            finalizingPersistence=true;
            string failure=null;
            try
            {
                if(initialized&&!saves.Status.IsClosed)
                {
                    try
                    {
                        // 同一宿主先禁用Inventory也可只读捕获；若它已销毁，仅可沿用此前已排空的最终字节。
                        if(inventory==null||inventory.Ledger==null||Economy==null)
                        {
                            if(lifecyclePayload==null||!saves.Status.IsSaved)throw new InvalidOperationException("最后快照的库存/经济依赖已释放");
                        }
                        else
                        {
                            var bytes=CommerceSnapshotCodec.Encode(Capture());
                            // Quit、Disable、Destroy可连续到来；内容未变不重复排队，内容变了即使修订相同也保存新序号。
                            if(lifecyclePayload==null||!lifecyclePayload.SequenceEqual(bytes))
                            {
                                saves.RequestSave(revision,bytes,2,Economy.DefinitionFingerprint);
                                lifecyclePayload=bytes;lastSavedSimulation=Economy.SimulationTime;
                            }
                        }
                    }
                    catch(Exception error){failure="最后快照捕获失败："+error.Message;}
                }
                // 捕获失败也排空已冻结的旧请求；Close不是fire-and-forget，不能把尚未写入伪报为已关闭。
                try
                {
                    var success=(closeWriter?saves.CloseAsync():saves.FlushAsync()).GetAwaiter().GetResult();
                    if(!success)failure=(failure==null?"":failure+"；")+"经济档写入失败："+(saves.Status.LastError??"最新修订尚未落盘");
                }
                catch(Exception error){failure=(failure==null?"":failure+"；")+"保存收口异常："+error.Message;}
                if(failure!=null)
                {
                    Diagnostic="退出保存未完成，旧主档保留："+failure;
                    if(lifecycleFailure!=Diagnostic){lifecycleFailure=Diagnostic;Debug.LogError("[COM-01] "+Diagnostic,this);}
                }
                else lifecycleFailure=null;
            }
            finally{finalizingPersistence=false;}
        }
        private void OnApplicationQuit(){FinalizePersistence(false);}
        private void OnDisable()
        {
            try
            {
                if(userInterface!=null){userInterface.Close();userInterface.SetPersistenceStatus(null,false);}
                CloseSession();
            }
            finally{FinalizePersistence(false);}
        }
        private void OnDestroy()
        {
            // 此时领域和订阅仍完整，先捕获最后变化再等待串行writer真正关闭。
            FinalizePersistence(true);
            if(inventory!=null){inventory.RuntimeChanged-=MarkDirty;inventory.ConfigureEditGate(null);}
            snapshotParticipants.Changed-=MarkDirty;snapshotParticipants.Dispose();
            if(Economy!=null){Economy.Changed-=MarkDirty;Economy.Dispose();}
        }
    }
}
