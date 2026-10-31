/* 脚本：CraftingRoot
 * 功能：配方、工位会话、只读预览与原库存转换的组装根，登记原经济共同快照。
 * 边界：不拥有第二库存/Current/时钟；只有服务签发的当前票据可进入提交，旧回执只读返回。
 * 协作：唯一F路由打开/确认，uGUI持有自有capture；发布段只切换预分配回执和票据引用。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using Ossastra.Characters;
using Ossastra.CharacterCore;
using Ossastra.Inventory;
using Ossastra.Commerce.Runtime;
using Ossastra.UserInterface;
using Ossastra.Crafting.Core;
using Ossastra.Crafting.Presentation;
namespace Ossastra.Crafting
{
    using CharacterRoot=Ossastra.Characters.CharacterRoot;
    [DefaultExecutionOrder(-145),DisallowMultipleComponent]
    public sealed partial class CraftingRoot:MonoBehaviour,ICraftingPresentationHost,ICommerceSnapshotParticipant
    {
        [Header("唯一原宿主 / 不另建库存或存档")]
        public InventorySession inventory;
        public CommerceService commerce;
        public InventoryView inventoryView;
        public CraftingUIRoot userInterface;
        public Ossastra.MapResearch.MapResearchRoot mapView;
        public Camera gameplayCamera;
        [Header("全局目录 / 主场与中转场必须一致")]
        public CraftingRecipeSO[] recipes=Array.Empty<CraftingRecipeSO>();
        public CraftingStationDefinitionSO[] stationCatalog=Array.Empty<CraftingStationDefinitionSO>();
        [Header("本场工位 / 实例ID只显式登记")]
        public CraftingStation[] stations=Array.Empty<CraftingStation>();
        public string mapId="crafting-main";
        [Header("统一音声事实 / 空值表示安静")]
        public string openSoundEvent,successSoundEvent,rejectSoundEvent;
        public bool Ready=>registered&&initialized&&isActiveAndEnabled&&inventory!=null&&inventory.Ready&&commerce!=null&&commerce.Ready;
        public string Diagnostic {get;private set;}="尚未初始化";
        public string SnapshotKey=>"ossastra.crafting";
        public event Action Changed;
        public int ObserverFailureCount {get;private set;}
        public string StationName=>session==null?"装备台":session.station.definition.displayName;
        public long RefreshRevision=>refreshRevision;
        public bool IsSessionValid=>SessionError()==null;
        public IReadOnlyList<CraftingRecipePresentation> Recipes=>rows;
        public int ReceiptCount=>journal.Records.Count;
        CraftCatalog catalog;
        Dictionary<string,CraftingRecipeSO> authors;
        Dictionary<CraftingStation,string> stationStamps;
        IReadOnlyList<CraftingRecipePresentation> rows=Array.Empty<CraftingRecipePresentation>();
        CraftReceiptJournal journal=new CraftReceiptJournal();
        string catalogFingerprint;
        bool registered,initialized,focused=true;
        long refreshRevision,observedRevision=-1,observedAccess=-1;
        int observedControl=-1;
        Ossastra.Audio.SfxSourceHandle sound;
        float billboardYaw=float.NaN;
        sealed class Session
        {
            internal string id,actorId,stationDefinitionId,stationInstanceId;
            internal int controlRevision;
            internal CharacterRoot actor;
            internal CraftingStation station;
            internal Vector3 anchor;
        }
        sealed class Ticket
        {
            internal string id,fingerprint;
            internal CraftRecipe recipe;
            internal int quantity;
            internal long revision,access;
            internal PreparedInventoryConversion conversion;
            internal CraftMaterialConsumption[] consumes;
        }
        Session session;
        Ticket ticket;

        void Awake()
        {
            try
            {
                if(inventory==null||commerce==null||inventoryView==null||userInterface==null||commerce.inventory!=inventory||mapId!=commerce.mapId||recipes==null||recipes.Length<1||recipes.Length>256||stationCatalog==null||stations==null||stations.Length>128)
                    throw new ArgumentException("合成根依赖/地图/目录无效");
                catalog=new CraftCatalog(recipes.Select(r=>r==null?throw new ArgumentException("缺配方"):r.Freeze()));
                authors=recipes.ToDictionary(r=>r.recipeId,StringComparer.Ordinal);
                if(stationCatalog.Any(s=>s==null)||stationCatalog.Select(s=>s.definitionId).Distinct(StringComparer.Ordinal).Count()!=stationCatalog.Length)
                    throw new ArgumentException("工位类型目录缺失或重复");
                foreach(var d in stationCatalog)d.Stamp();
                if(catalog.Recipes.Any(r=>!stationCatalog.Any(s=>s.supported&&s.capabilities.Contains(r.StationCapability))))throw new ArgumentException("配方没有支持其能力的工位定义");
                if(stations.Any(s=>s==null||s.definition==null||!stationCatalog.Contains(s.definition)||string.IsNullOrWhiteSpace(s.instanceId)||s.mapId!=mapId||s.interactionAnchor==null||s.ownedColliders==null||s.ownedColliders.Any(c=>c==null||!c.transform.IsChildOf(s.transform)))||stations.Select(s=>s.instanceId).Distinct(StringComparer.Ordinal).Count()!=stations.Length)
                    throw new ArgumentException("工位实例/地图/自有Collider缺失或重复");
                stationStamps=stations.ToDictionary(s=>s,Stamp);
                catalogFingerprint=ConfigurationFingerprint();
                commerce.RegisterSnapshotParticipant(this);registered=true;Diagnostic="等待原库存同快照恢复";
            }
            catch(Exception ex){Diagnostic=ex.Message;Debug.LogError("[Crafting] "+Diagnostic,this);enabled=false;}
        }
        void Update()
        {
            if(!registered)return;
            if(!initialized)
            {
                if(!inventory.Ready||!commerce.Ready)return;
                try
                {
                    ValidateItemCatalog();userInterface.Initialize(inventoryView,inventoryView.Skin,this);
                    if(Ossastra.Audio.GameAudioRoot.Instance!=null)sound=Ossastra.Audio.GameAudioRoot.Instance.RegisterSfxSource("crafting:"+mapId);
                    initialized=true;Diagnostic="装备台与原库存已就绪";
                }
                catch(Exception ex){Diagnostic=ex.Message;Debug.LogError("[Crafting] "+Diagnostic,this);enabled=false;return;}
            }
            var control=inventory.Coordinator.ControlRevision;
            if(observedRevision!=inventory.Ledger.MutationRevision||observedAccess!=inventory.Ledger.AccessGeneration||observedControl!=control)
            {
                observedRevision=inventory.Ledger.MutationRevision;observedAccess=inventory.Ledger.AccessGeneration;observedControl=control;
                ticket=null;refreshRevision++;
            }
            if(session!=null&&SessionError()!=null)userInterface.Close();
            if(gameplayCamera!=null&&!Mathf.Approximately(billboardYaw,gameplayCamera.transform.eulerAngles.y))
            {
                billboardYaw=gameplayCamera.transform.eulerAngles.y;
                foreach(var station in stations)if(station!=null&&station.billboardVisual!=null)station.billboardVisual.rotation=Quaternion.Euler(0,billboardYaw,0);
            }
        }
        void ValidateItemCatalog()
        {
            foreach(var r in recipes)
            foreach(var item in r.ingredients.Select(x=>x.item).Concat(r.outputs.Select(x=>x.item)))
                if(inventory.Definition(item.definitionId)!=item)throw new ArgumentException("配方物品不是当前原库存目录中的同一定义："+item.definitionId);
        }
        static string Stamp(CraftingStation s)=>s.instanceId+"|"+s.mapId+"|"+s.definition.Stamp();
        string ConfigurationFingerprint()
        {
            var value=new{recipes=recipes.OrderBy(r=>r.recipeId,StringComparer.Ordinal).Select(r=>new{
                id=r.recipeId,version=r.version,capability=r.stationCapability,maxBatch=r.maxBatch,
                ingredients=r.ingredients.Select(x=>new{id=x.item.definitionId,q=x.quantity}).ToArray(),
                outputs=r.outputs.Select(x=>new{id=x.item.definitionId,q=x.quantity,durability=x.initialDurability,w=x.item.width,h=x.item.height,stack=x.item.maxStack,
                    hand=x.item.equipment==null?"none":x.item.equipment.mainHand?"right":"left",kind=x.item.equipment==null?0:(int)x.item.equipment.offhandKind}).ToArray()}).ToArray(),
                stations=stationCatalog.OrderBy(s=>s.definitionId,StringComparer.Ordinal).Select(s=>s.Stamp()).ToArray()};
            using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value)))).Replace("-","").ToLowerInvariant();
        }
        bool ConfigurationMatches()
        {
            try{return ConfigurationFingerprint()==catalogFingerprint;}
            catch(Exception ex) when(ex is ArgumentException||ex is InvalidOperationException||ex is NullReferenceException){return false;}
        }
        public CraftingStation NearestTarget()
        {
            if(!Ready||!focused||inventory.Coordinator.Current==null)return null;
            var actor=inventory.Coordinator.Current;
            return stations.Where(s=>s!=null&&stationStamps.ContainsKey(s)&&s.CheckAccess(actor)==null)
                .OrderBy(s=>(s.Anchor-actor.transform.position).sqrMagnitude).ThenBy(s=>s.instanceId,StringComparer.Ordinal).FirstOrDefault();
        }
        public bool Open(CraftingStation station)
        {
            if(!Ready||station==null||!stationStamps.TryGetValue(station,out var stamp)||stamp!=Stamp(station)||station.mapId!=mapId||station.CheckAccess(inventory.Coordinator.Current)!=null)return false;
            if(userInterface.IsOpen)userInterface.Close();
            var actor=inventory.Coordinator.Current;
            session=new Session{id=Guid.NewGuid().ToString("N"),actor=actor,actorId=actor.ActorId,controlRevision=inventory.Coordinator.ControlRevision,
                station=station,stationDefinitionId=station.definition.definitionId,stationInstanceId=station.instanceId,anchor=station.Anchor};
            ticket=null;
            rows=recipes.Where(r=>station.definition.supported&&station.definition.capabilities.Contains(r.stationCapability))
                .OrderBy(r=>r.category,StringComparer.Ordinal).ThenBy(r=>r.recipeId,StringComparer.Ordinal)
                .Select(r=>new CraftingRecipePresentation(r.recipeId,r.displayName,r.category,r.icon,r.description)).ToArray();
            if(!userInterface.Open()){CloseSession();return false;}
            if(mapView!=null)mapView.SetUiCameraCapture(this,true);
            // UI只增添自身capture；其合法控制修订变化属于本次开窗，不能让首份预览立即失效。
            session.controlRevision=inventory.Coordinator.ControlRevision;ticket=null;refreshRevision++;
            observedControl=session.controlRevision;
            PlaySound(openSoundEvent,"open:"+session.id,station.Anchor);return true;
        }
        string SessionError()
        {
            try{return SessionErrorUnchecked();}
            catch(Exception ex) when(ex is ArgumentException||ex is InvalidOperationException||ex is NullReferenceException){return "工位作者配置已失效";}
        }
        string SessionErrorUnchecked()
        {
            if(!Ready||!focused||session==null)return "装备台会话已关闭";
            var s=session;var actor=inventory.Coordinator.Current;
            if(actor==null||actor!=s.actor||actor.ActorId!=s.actorId||inventory.Coordinator.ControlRevision!=s.controlRevision||!actor.CanSimulateIntent)return "操作者、控制修订或状态已变化";
            if(inventory.CurrentMapId!=mapId||commerce.mapId!=mapId||s.station==null||s.station.instanceId!=s.stationInstanceId||s.station.mapId!=mapId||s.station.definition==null||s.station.definition.definitionId!=s.stationDefinitionId||!stationStamps.TryGetValue(s.station,out var stamp)||stamp!=Stamp(s.station)||(s.station.Anchor-s.anchor).sqrMagnitude>.000001f)return "工位身份、地图、定义或锚点已变化";
            return s.station.CheckAccess(actor);
        }
        public void CloseSession(){if(mapView!=null)mapView.SetUiCameraCapture(this,false);session=null;ticket=null;rows=Array.Empty<CraftingRecipePresentation>();refreshRevision++;}
        void OnApplicationFocus(bool value){focused=value;if(!value&&userInterface!=null)userInterface.Close();}
        void OnDisable(){if(userInterface!=null)userInterface.Close();CloseSession();}
        void OnDestroy(){sound?.Dispose();sound=null;}
        void PlaySound(string soundEvent,string fact,Vector3 at){if(!string.IsNullOrWhiteSpace(soundEvent))sound?.Submit(soundEvent,fact,at);}
        void Notify()
        {
            var listeners=Changed;if(listeners==null)return;
            foreach(Action callback in listeners.GetInvocationList())try{callback();}catch{ObserverFailureCount++;}
        }
    }
}
