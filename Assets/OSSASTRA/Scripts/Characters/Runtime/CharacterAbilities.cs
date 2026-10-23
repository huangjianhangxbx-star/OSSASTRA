/* 脚本：CharacterAbilities
 * 功能：按装备定义绑定语义操作，统一编译/校验/接受并调度每次能力执行与离手投递。
 * 边界：不按盾/炸弹/技能名字分派，不新增HP/体力/Current；身体动作仍由根原Runtime管理。
 * 协作：Root按输入→原动作事实→效果顺序调用；执行器只读冻结上下文，来源端口分别提交耐久/数量。
 */
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Ossastra.CharacterCore;
using Ossastra.CharacterCore.Abilities;
using Ossastra.Combat;
namespace Ossastra.Characters
{
    [DisallowMultipleComponent]
    public sealed class CharacterAbilities:MonoBehaviour,ICharacterAbilityController,ICombatGuard,ICharacterOffhandStatus,ICharacterMotionOverride,ICombatEvasion,ICharacterLocomotionScale
    {
        private sealed class EquipmentSource:IAbilitySource,IAbilityStateSource
        {
            private readonly CharacterEquipment equipment;
            private readonly bool main,wear;
            private readonly string bindingInstance;
            public string OwnerId=>equipment?.State?.OwnerId;
            public EquipmentAbilityState RuntimeState=>equipment.State.RuntimeFor(InstanceId);
            public string InstanceId {get;}
            public EquipmentSource(CharacterEquipment equipment,bool main,bool wear,string bindingInstance=null)
            {this.equipment=equipment;this.main=main;this.wear=wear;this.bindingInstance=bindingInstance;InstanceId=main?equipment.State.RightInstanceId:equipment.State.LeftInstanceId;}
            public bool IsAvailable=>equipment!=null&&equipment.isActiveAndEnabled&&equipment.State!=null&&
                (bindingInstance==null||equipment.State.LeftInstanceId==bindingInstance)&&
                (main?equipment.RightDefinition!=null&&equipment.State.RightInstanceId==InstanceId:equipment.State.LeftInstanceId==InstanceId&&equipment.State.Durability>0);
            public bool CommitRelease(Guid id)=>IsAvailable&&(!wear||equipment.State.WearInstance(InstanceId,id));
            public bool TryReserve(Guid id)=>id!=Guid.Empty&&IsAvailable;
            public void CancelReservation() { }
        }
        private sealed class BoundGesture
        {
            public AbilityBinding binding;
            public GestureRecognizer gesture;
            public bool main;
            public bool repeatWhileHeld,repeatArmed;
            public CompiledAbilityExecutor executor;
            public IAbilitySource source;
        }
        private readonly List<BoundGesture> bindings=new List<BoundGesture>();
        private readonly List<AbilityRun> runs=new List<AbilityRun>();
        // 即时策略可以早于身体动画结束；直到原Runtime终结都必须抑制模板近战释放。
        private readonly HashSet<Guid> ownedBodyReleases=new HashSet<Guid>();
        private readonly CombatDeliveryQueue deliveries=new CombatDeliveryQueue();
        private CombatIceWorld iceWorld;
        private string iceInstance;
        private int iceGeneration;
        private sealed class RocketMark {public CombatVitals target;public AbilityRunContext context;public double expires;}
        private readonly Dictionary<string,RocketMark> rocketMarks=new Dictionary<string,RocketMark>();
        private readonly Dictionary<string,double> rocketDirectReadyAt=new Dictionary<string,double>();
        public CombatIceWorld IceWorld=>iceWorld;
        public event Action<Guid,string,Vector3> Feedback;
        public void NotifyResourceFeedback(string kind)=>Feedback?.Invoke(Guid.Empty,kind,transform.position);
        private CharacterRoot owner;
        private CharacterEquipment equipment;
        private CharacterStamina stamina;
        private CharacterInputFrame input;
        private string bindingIdentity;
        private int bindingEpoch;
        private bool previouslyAllowed;
        private bool previouslySuspended;
        private EffectSnapshot liveEffects;
        private EffectSourceRow[] liveSourceRows=Array.Empty<EffectSourceRow>();
        private int liveSourceRevision=-1;
        public string LastResult {get;private set;}="能力未初始化";
        public int AcceptedCount {get;private set;}
        public int EmittedCount {get;private set;}
        public int ResolvedCount=>deliveries.ResolvedCount;
        public int InFlightCount=>deliveries.InFlightCount;
        public bool IsBusy=>runs.Any(r=>!r.Ended);
        public float LocomotionScale=>runs.Where(r=>!r.Ended).OfType<ICharacterLocomotionScale>().Select(r=>r.LocomotionScale).DefaultIfEmpty(1).Min();
        private ICharacterMotionOverride ActiveMotion=>runs.Where(r=>!r.Ended).OfType<ICharacterMotionOverride>().FirstOrDefault(r=>r.IsActive);
        public bool IsActive=>isActiveAndEnabled&&ActiveMotion!=null;
        public Vector3 MotionVelocity=>ActiveMotion?.MotionVelocity??Vector3.zero;
        public bool IsEvading=>isActiveAndEnabled&&runs.Where(r=>!r.Ended).OfType<ICombatEvasion>().Any(r=>r.IsEvading);
        public bool IsAccepting=>runs.Any(r=>!r.Ended&&(r.Context.definition.plan.Occupancy&AbilityOccupancy.OffHand)!=0);
        private ICombatGuard ActiveGuard=>runs.OfType<ICombatGuard>().FirstOrDefault(g=>g.IsGuarding);
        public bool IsGuarding=>isActiveAndEnabled&&ActiveGuard!=null;
        public Vector3 GuardDirection=>ActiveGuard?.GuardDirection??Vector3.zero;
        public double MinimumDot=>ActiveGuard?.MinimumDot??1;
        public double PostureScale=>ActiveGuard?.PostureScale??1;
        public int GuardedCount {get;private set;}
        public void OnGuarded(HitOutcome outcome){if(ActiveGuard!=null){GuardedCount++;LastResult="已格挡";ActiveGuard.OnGuarded(outcome);}}

        public void Initialize(CharacterRoot root)
        {owner=root;equipment=GetComponent<CharacterEquipment>();stamina=GetComponent<CharacterStamina>();Rebind();}
        public AbilityDefinitionSO SkillDefinition(int slot)
        {
            var profile=equipment?.RightDefinition?.skillLoadout;if(profile==null||slot<0||slot>2)return null;
            if(slot==1)return profile.nativeAbility;
            if(equipment.State.RightRuntime.SkillSelections.TryGetValue(slot,out var id))return SkillChoices.FirstOrDefault(d=>d!=null&&d.definitionId==id);
            return slot==0?profile.defaultQ:profile.defaultR;
        }
        public AbilityDefinitionSO[] SkillChoices
        {
            get
            {
                var profile=equipment?.RightDefinition?.skillLoadout;
                return profile==null?Array.Empty<AbilityDefinitionSO>():profile.regularChoices.Concat(profile.experimentalChoices).Distinct().ToArray();
            }
        }
        private string[] EquippedEffectRoots=>Enumerable.Range(0,3).Select(i=>SkillDefinition(i)?.effectRoot).Where(r=>!string.IsNullOrEmpty(r)).Distinct().ToArray();
        public EffectSnapshot SnapshotEffects(bool lastRound=false)
        {
            var live=ReadLiveEffects();
            return new EffectSnapshot(live.Revision,live.Sources,equipment?.State?.RightRuntime.Frost.Current??0,lastRound);
        }
        private EffectSnapshot ReadLiveEffects()
        {
            var state=equipment?.State?.RightRuntime;
            if(liveEffects==null||liveSourceRevision!=(state?.Sources.Revision??0))
            {
                liveSourceRevision=state?.Sources.Revision??0;
                liveSourceRows=state?.Sources.Evaluate(equipment.RightDefinition?.skillLoadout?.effectProfession,EquippedEffectRoots)??Array.Empty<EffectSourceRow>();
                liveEffects=new EffectSnapshot(liveSourceRevision,liveSourceRows.Where(r=>r.Status==EffectSourceStatus.Active).ToArray(),0,false);
            }
            return liveEffects;
        }
        public EffectSourceRow[] EvaluateSources(){ReadLiveEffects();return (EffectSourceRow[])liveSourceRows.Clone();}
        public EffectSourceDefinitionSO[] SourceChoices=>equipment?.RightDefinition?.skillLoadout?.sourceCatalog.Where(d=>d!=null&&d.profession==equipment.RightDefinition.skillLoadout.effectProfession).ToArray()??Array.Empty<EffectSourceDefinitionSO>();
        public bool ToggleExperimentalSource(string definitionId)
        {
            var engagement=FindObjectOfType<CombatEngagementSession>();
            if(owner==null||!owner.GetComponent<CharacterInputHandler>().HasPlayerControl||!owner.CanSimulateIntent||owner.ActionActive||IsBusy||engagement!=null&&engagement.Engaged)return Reject("只能实控角色脱战空闲时试验授予/撤回");
            var definition=SourceChoices.FirstOrDefault(d=>d.definitionId==definitionId);if(definition==null)return Reject("来源未登记");
            var sources=equipment.State.RightRuntime.Sources;var instance=equipment.State.RightInstanceId+":trial:"+definitionId;
            if(!sources.Remove(instance))sources.Grant(instance,definition.Freeze());LastResult="试验来源已更新（非正式掉落/成长）";return true;
        }
        public bool ConfigureSkill(int slot,string definitionId)
        {
            var engagement=FindObjectOfType<CombatEngagementSession>();
            if(slot!=0&&slot!=2||owner==null||!owner.GetComponent<CharacterInputHandler>().HasPlayerControl||!owner.CanSimulateIntent||owner.ActionActive||IsBusy||
                equipment?.RightDefinition?.skillLoadout==null||engagement!=null&&engagement.Engaged)return Reject("仅实控角色脱战空闲时可修改Q/R，E固定");
            if(!string.IsNullOrEmpty(definitionId)&&!SkillChoices.Any(d=>d.definitionId==definitionId))return Reject("不是当前主手职业技能");
            equipment.State.RightRuntime.SkillSelections[slot]=definitionId??"";Rebind();LastResult="已更新技能配置";return true;
        }
        public double SkillCooldown(int slot)
        {
            var d=SkillDefinition(slot);if(d==null||equipment?.State==null)return 0;
            var state=equipment.State.RightRuntime;
            return state.Cooldowns.Remaining(equipment.State.RightInstanceId+":"+(string.IsNullOrWhiteSpace(d.cooldownIdentity)?d.definitionId:d.cooldownIdentity),state.Clock(d.cooldownPolicy));
        }
        public string ResourceSummary
        {
            get
            {
                if(equipment?.State==null)return "";var state=equipment.State.RightRuntime;var parts=new List<string>();
                if(bindings.Any(b=>b.executor!=null&&b.executor.UsesMagazine&&b.source.IsAvailable))parts.Add("弹药 "+state.Magazine.Count+" · "+(state.Magazine.IsReloading?"装填中":state.Magazine.Next==AmmoKind.Mortar?"炮弹":"普通弹"));
                if(equipment.RightDefinition?.skillLoadout?.effectProfession=="blue")parts.Add("霜寒 "+state.Frost.Current.ToString("0.0")+"/"+state.Frost.Capacity.ToString("0"));
                return string.Join(" · ",parts);
            }
        }
        public string SkillAvailability(int slot)
        {
            var definition=SkillDefinition(slot);if(definition==null)return "空槽";
            var binding=bindings.FirstOrDefault(b=>b.main&&b.binding.operation==(AbilityOperation)((int)AbilityOperation.SkillOne+slot)&&b.binding.ability==definition);
            if(binding==null||!binding.source.IsAvailable)return "来源不可用";
            if(binding.executor==null||!binding.executor.CanUseSource(binding.source))return "资源未就绪";
            var request=owner.Actions.CreateRequest(binding.executor.ActionKind,CharacterActionSource.UserInterface,"availability");
            return owner.Actions.CanAccept(request,definition.acceptCost+definition.releaseCost,out var reason)?"可用":reason=="stamina"?"体力不足":"动作/控制未就绪";
        }
        public bool RequestSkill(int slot,CharacterActionSource source)
            =>slot>=0&&slot<3&&TryRequest(SkillDefinition(slot),true,(AbilityOperation)((int)AbilityOperation.SkillOne+slot),source);
        private bool EnsureIceWorld()
        {
            if(owner==null||owner.Vitals==null||!owner.IsAlive||equipment?.State==null)return false;
            if(iceWorld!=null&&(iceInstance!=equipment.State.RightInstanceId||iceGeneration!=owner.LifeGeneration)){iceWorld.Dispose();iceWorld=null;}
            if(iceWorld==null)
            {
                iceInstance=equipment.State.RightInstanceId;iceGeneration=owner.LifeGeneration;
                var instance=iceInstance;var generation=iceGeneration;
                iceWorld=new CombatIceWorld(owner.Vitals,()=>this!=null&&isActiveAndEnabled&&owner.IsAlive&&owner.LifeGeneration==generation&&equipment.State.RightInstanceId==instance);
            }
            return true;
        }
        public void StartIceTrail(Guid execution,Func<bool> active)
        {if(EnsureIceWorld())iceWorld.StartTrail(execution,active);}
        public bool ScheduleIceColumn(Guid execution,Vector3 from,Vector3 position,string sourceId)
        {
            if(!EnsureIceWorld())return false;var state=equipment.State.RightRuntime;
            return iceWorld.ScheduleColumn(execution,from,position,()=>state.Sources.Contains(sourceId));
        }
        private bool ValidRocketContext(AbilityRunContext context)=>context.owner==owner&&owner.IsAlive&&owner.LifeGeneration==context.lifeGeneration&&context.source.IsAvailable;
        private void RefillRocket(AbilityRunContext context)
        {
            if(!ValidRocketContext(context))return;var state=context.source.RuntimeState;
            state.Cooldowns.MakeReady(context.source.InstanceId+":"+context.definition.cooldownIdentity,state.Clock(context.definition.cooldownPolicy));
        }
        /// <summary>落地与起跳共用原火箭身份；直接击杀节流与一秒死亡标记不依赖是否仍由玩家控制。</summary>
        public void TrackRocketContacts(AbilityRunContext context,IReadOnlyList<HitOutcome> receipts)
        {
            if(!context.effects.HasEffect("rocket-reset")||!ValidRocketContext(context))return;
            foreach(var receipt in receipts.Where(r=>r.Reason==HitReason.Applied))
            {
                var target=FindObjectsOfType<CombatVitals>().FirstOrDefault(v=>v.Health?.Id==receipt.TargetId&&v.Faction!=owner.Vitals.Faction);
                if(target==null)continue;
                if(receipt.HpAfter==0)
                {
                    if(!rocketDirectReadyAt.TryGetValue(context.source.InstanceId,out var ready)||owner.SimulationTime>=ready)
                    {rocketDirectReadyAt[context.source.InstanceId]=owner.SimulationTime+.5;RefillRocket(context);}
                }
                else rocketMarks[receipt.TargetId]=new RocketMark{target=target,context=context,expires=owner.SimulationTime+1};
            }
        }
        private void TickRocketMarks()
        {
            foreach(var pair in rocketMarks.ToArray())
            {
                var mark=pair.Value;
                if(mark.target==null||owner.SimulationTime>mark.expires||!ValidRocketContext(mark.context)){rocketMarks.Remove(pair.Key);continue;}
                if(!mark.target.IsAlive){RefillRocket(mark.context);rocketMarks.Remove(pair.Key);}
            }
        }
        private IEnumerable<AbilityBinding> MainBindings(EquipmentDefinitionSO definition)
        {
            if(definition.skillLoadout==null)return definition.abilityBindings??Array.Empty<AbilityBinding>();
            definition.skillLoadout.Validate();
            return (definition.abilityBindings??Array.Empty<AbilityBinding>()).Concat(Enumerable.Range(0,3).Where(i=>SkillDefinition(i)!=null)
                .Select(i=>new AbilityBinding{operation=(AbilityOperation)((int)AbilityOperation.SkillOne+i),gesture=GestureMode.Press,ability=SkillDefinition(i)}));
        }
        private void Rebind()
        {
            foreach(var old in bindings)old.executor?.StopSource(old.source);
            bindings.Clear();bindingEpoch++;
            liveEffects=null;
            if(equipment?.State==null)return;
            var used=new HashSet<AbilityOperation>();
            foreach(var pair in new[]{(definition:equipment.RightDefinition,main:true),(definition:equipment.LeftDefinition,main:false)})
            {
                if(pair.definition==null)continue;
                foreach(var binding in pair.main?MainBindings(pair.definition):pair.definition.abilityBindings??Array.Empty<AbilityBinding>())
                {
                    if(binding==null||binding.ability==null||!Enum.IsDefined(typeof(AbilityOperation),binding.operation)||
                        !Enum.IsDefined(typeof(GestureMode),binding.gesture)||float.IsNaN(binding.longPressThreshold)||float.IsInfinity(binding.longPressThreshold)||binding.longPressThreshold<=0||!used.Add(binding.operation))
                    {LastResult="拒绝："+pair.definition.name+" binding missing/duplicate operation";bindings.Clear();return;}
                    var frozen=new AbilityBinding{operation=binding.operation,gesture=binding.gesture,resourceOwner=binding.resourceOwner,longPressThreshold=binding.longPressThreshold,ability=binding.ability,longPressAbility=binding.longPressAbility};
                    var executor=frozen.ability.executor?.CompileExecutor();
                    var mainPayload=frozen.resourceOwner==AbilityResourceOwner.MainEquipment;
                    bindings.Add(new BoundGesture{binding=frozen,main=pair.main,gesture=new GestureRecognizer(frozen.gesture,frozen.longPressThreshold),repeatWhileHeld=executor!=null&&executor.RepeatWhileHeld,executor=executor,
                        source=new EquipmentSource(equipment,pair.main||mainPayload,false,!pair.main&&mainPayload?equipment.State.LeftInstanceId:null)});
                }
            }
            bindingIdentity=equipment.State.RightInstanceId+"/"+equipment.State.LeftInstanceId;
        }
        public void TickInput(CharacterInputFrame frame)
        {
            if(owner==null||!isActiveAndEnabled)return;input=frame;
            var identity=equipment?.State==null?null:equipment.State.RightInstanceId+"/"+equipment.State.LeftInstanceId;
            if(identity!=bindingIdentity)Rebind();
            if(previouslyAllowed!=owner.HasPlayerInput){bindingEpoch++;previouslyAllowed=owner.HasPlayerInput;}
            if(previouslySuspended!=owner.IsSimulationSuspended){bindingEpoch++;previouslySuspended=owner.IsSimulationSuspended;}
            foreach(var binding in bindings)
            {
                var signal=binding.gesture.Sample(Held(binding.binding.operation),owner.SimulationTime,unchecked(bindingEpoch*397^owner.ActionControlRevision));
                if(signal==GestureSignal.Cancel||signal==GestureSignal.End)
                {binding.repeatArmed=false;foreach(var run in runs.Where(r=>r.Context.operation==binding.binding.operation&&r.RequiresHeld))run.Cancel();continue;}
                if(signal==GestureSignal.Trigger||signal==GestureSignal.Begin)binding.repeatArmed=true;
                if(signal==GestureSignal.None&&(!Held(binding.binding.operation)||!owner.HasPlayerInput||input.attackHeld||
                    !binding.repeatArmed||!binding.repeatWhileHeld))continue;
                var definition=signal==GestureSignal.LongPress?binding.binding.longPressAbility:binding.binding.ability;
                TryRequest(definition,binding.main,binding.binding.operation,CharacterActionSource.Player);
            }
        }
        private bool Held(AbilityOperation operation)=>operation==AbilityOperation.Secondary?input.offhandHeld:
            operation==AbilityOperation.SkillOne?input.skillOneHeld:operation==AbilityOperation.SkillTwo?input.skillTwoHeld:input.skillThreeHeld;

        public bool TryRequest(AbilityDefinitionSO definition,bool main,AbilityOperation operation,CharacterActionSource source)
        {
            if(owner==null||!isActiveAndEnabled||!owner.HasPlayerInput||equipment?.State==null||definition==null||owner.GetComponent<CharacterInputHandler>().InventoryCaptured)return Reject("来源/控制不可用");
            var handDefinition=main?equipment.RightDefinition:equipment.LeftDefinition;
            if(handDefinition==null)return Reject("请求手别未装备来源");
            try
            {
                var mounted=main?MainBindings(handDefinition):(handDefinition.abilityBindings??Array.Empty<AbilityBinding>());
                if(!mounted.Any(b=>b!=null&&b.operation==operation&&(b.ability==definition||b.longPressAbility==definition)))return Reject("能力未装配到当前手别/操作槽");
            }
            catch(ArgumentException error){return Reject(error.Message);}
            var directory=main?equipment.RightDefinition?.skillLoadout:null;
            if(directory!=null&&definition!=directory.nativeAbility&&!SkillChoices.Contains(definition)&&
                !(equipment.RightDefinition.abilityBindings??Array.Empty<AbilityBinding>()).Any(b=>b!=null&&(b.ability==definition||b.longPressAbility==definition)))return Reject("能力不属于当前主手职业目录");
            CompiledAbility compiled;
            try{compiled=definition.Compile();}catch(ArgumentException e){return Reject(e.Message);}
            var binding=main?null:equipment.LeftDefinition?.abilityBindings?.FirstOrDefault(b=>b!=null&&b.operation==operation&&(b.ability==definition||b.longPressAbility==definition));
            var mainPayload=binding!=null&&binding.resourceOwner==AbilityResourceOwner.MainEquipment;
            var port=new EquipmentSource(equipment,main||mainPayload,compiled.wearOnRelease,mainPayload?equipment.State.LeftInstanceId:null);
            var visualDefinition=main?equipment.RightDefinition:equipment.LeftDefinition;
            return AcceptCompiled(compiled,port,operation,source,visualDefinition?.worldSprite,visualDefinition?.worldScale??.25f);
        }
        /// <summary>库存/固有能力适配器也进入相同事务；来源必须属于本体，释放消费由来源实现，投递器不认识库存。</summary>
        public bool TryRequestFromSource(AbilityDefinitionSO definition,IAbilitySource port,AbilityOperation operation,CharacterActionSource source,Sprite sprite=null,float scale=.25f)
        {
            if(definition==null)return Reject("能力定义缺失");
            try{return AcceptCompiled(definition.Compile(),port,operation,source,sprite,scale);}
            catch(ArgumentException e){return Reject(e.Message);}
        }
        private bool AcceptCompiled(CompiledAbility compiled,IAbilitySource port,AbilityOperation operation,CharacterActionSource source,Sprite sprite,float scale)
        {
            var companion=source==CharacterActionSource.Companion;
            if(owner==null||!isActiveAndEnabled||port==null||port.OwnerId!=owner.ActorId||string.IsNullOrWhiteSpace(port.InstanceId)||!Enum.IsDefined(typeof(AbilityOperation),operation)||
                (companion?owner.GetComponent<CharacterInputHandler>().HasPlayerControl||!compiled.executor.AllowsCompanionUse:!owner.HasPlayerInput))return Reject("来源/控制身份无效");
            var resources=port.RuntimeState;
            if(equipment!=null&&!equipment.ValidatePresentations(out var presentationError))return Reject(presentationError);
            if(resources==null)return Reject("来源缺少稳定运行payload");
            var key=port.InstanceId+":"+compiled.cooldownIdentity;
            if(!port.IsAvailable||resources.Cooldowns.Remaining(key,resources.Clock(compiled.cooldownPolicy))>0)return Reject("来源失效或冷却中");
            if(runs.Count>=8)return Reject("能力执行预算已满");
            CompiledAbilityExecutor executor;
            try{executor=compiled.executor.Prepare(owner,port);}catch(ArgumentException error){return Reject(error.Message);}
            if(!executor.CanUseSource(port))return Reject("来源资源不允许当前能力");
            if(!CanAccept(executor.ActionKind,out var conflict))return Reject(conflict);
            var direction=owner.DataBus.AttackDirection;
            var explicitAim=(port as IAbilityTargetSource)?.AimPoint;
            if(explicitAim.HasValue){direction=explicitAim.Value-owner.transform.position;direction.y=0;}
            if(direction.sqrMagnitude<.0001f)return Reject("无效方向");
            var acceptanceDelivery=executor.AcceptanceDelivery??compiled.delivery;
            var landing=owner.transform.position+direction.normalized*acceptanceDelivery.range;
            if(executor.AimRange>0)
            {
                var aim=explicitAim??owner.AbilityAimPoint(input);if(!aim.HasValue)return Reject("无合法鼠标落点");
                var offset=aim.Value-owner.transform.position;offset.y=0;landing=owner.transform.position+Vector3.ClampMagnitude(offset,executor.AimRange);
            }
            if(acceptanceDelivery.mode==DeliveryMode.Parabolic||acceptanceDelivery.mode==DeliveryMode.RayLandingArc)
            {
                var aim=explicitAim??owner.AbilityAimPoint(input);if(!aim.HasValue)return Reject("无合法鼠标落点");
                var targetPoint=executor.AimRange>0?landing:aim.Value;
                var delta=targetPoint-owner.transform.position;delta.y=0;
                if(delta.magnitude>acceptanceDelivery.range+.0001f||!CombatDeliveryQueue.Clear(owner.transform.position,targetPoint))return Reject("落点超距或遮挡");
                var floor=Physics.RaycastAll(targetPoint+Vector3.up*.4f,Vector3.down,.8f,acceptanceDelivery.collisionLayers,QueryTriggerInteraction.Ignore)
                    .Where(h=>h.normal.y>.7f&&h.collider.GetComponentInParent<CombatVitals>()==null).OrderBy(h=>h.distance).ToArray();
                if(floor.Length==0)return Reject("落点无地面");landing=floor[0].point;
            }
            if(stamina==null||!stamina.CanSpend(compiled.cost+compiled.releaseCost))return Reject("体力不足");
            var frozenEffects=SnapshotEffects(resources.Magazine.Count==1);
            var releasePayment=Guid.NewGuid();
            if(compiled.releaseCost>0&&!stamina.TryReserve(releasePayment,compiled.releaseCost))return Reject("释放体力无法预留");
            bool RejectReserved(string reason){stamina.CancelReservation(releasePayment);return Reject(reason);}
            var reservation=Guid.NewGuid();
            Guid id;
            if(compiled.executor.UsesBodyAction)
            {
                if(!owner.TryAbilityBodyAction(compiled.animation,source,"ability:"+compiled.id,compiled.cost,out id,()=>port.TryReserve(reservation),port.CancelReservation,executor.InitialPhase))return RejectReserved(owner.LastFact);
            }
            else
            {
                var request=owner.Actions.CreateRequest(executor.ActionKind,source,"ability:"+compiled.id);id=request.RequestId;
                if(!owner.Actions.TryCommit(request,compiled.cost,()=>port.TryReserve(reservation)?request.RequestId:Guid.Empty,out var reason))return RejectReserved(reason);
            }
            var context=new AbilityRunContext(owner,compiled,port,id,direction.normalized,landing,Emit,operation,sprite,scale,frozenEffects,releasePayment,source);
            context.commitSource=CommitSource;
            context.deliveryPort=EmitDelivery;
            context.feedback=(execution,kind,position)=>Feedback?.Invoke(execution,kind,position);
            owner.Vitals?.Recovery?.RetainParent(id);
            deliveries.RegisterParent(id);
            var run=executor.CreateRun(context);context.onResolved=run.OnResolved;runs.Add(run);
            if(executor.UsesBodyAction)ownedBodyReleases.Add(id);
            if(executor.StartsCooldownOnAccept&&!resources.Cooldowns.Commit(key,id,resources.Clock(compiled.cooldownPolicy),context.ModifiedCooldown))throw new InvalidOperationException("Accepted ability cooldown conflict");
            AcceptedCount++;Feedback?.Invoke(id,"accepted",owner.transform.position);LastResult="接受 "+compiled.displayName;return true;
        }
        public bool CanAccept(CharacterActionKind kind,out string reason)
        {
            reason=null;if(!isActiveAndEnabled)return true;
            foreach(var run in runs.Where(r=>!r.Ended))
            {
                var mask=run.Context.definition.plan.Occupancy;
                if((kind==CharacterActionKind.Offhand||kind==CharacterActionKind.Guard||kind==CharacterActionKind.Support)&&(mask&AbilityOccupancy.OffHand)!=0)
                {reason="offhand-occupied";return false;}
            }
            return true;
        }
        private bool Reject(string reason){LastResult="拒绝："+reason;return false;}
        public bool OwnsRelease(Guid id)=>ownedBodyReleases.Contains(id);
        public void ObserveFact(ActionFact fact)
        {
            foreach(var run in runs.Where(r=>r.Context.id==fact.ActionId).ToArray())
            {
                if(fact.Kind==ActionFactKind.Cancelled){if(!run.Ended)run.Context.NotifyFeedback("cancel");run.Cancel();stamina?.CancelReservation(run.Context.releasePaymentId);}
                else if(fact.Kind==ActionFactKind.Released||fact.Kind==ActionFactKind.Finished)run.Tick(0,Held(run.Context.operation));
            }
            if(fact.Kind==ActionFactKind.Cancelled||fact.Kind==ActionFactKind.Finished)ownedBodyReleases.Remove(fact.ActionId);
        }
        public void TickEffects(float delta)
        {
            if(owner==null)return;
            if(!owner.IsAlive||!isActiveAndEnabled){CancelPending("life/disabled");equipment?.State?.RightRuntime.ShotEnhancements.Clear();deliveries.Clear();iceWorld?.Dispose();iceWorld=null;rocketMarks.Clear();rocketDirectReadyAt.Clear();ReleaseEndedRuns();return;}
            if(owner.IsSimulationSuspended)return;
            var controlled=owner.GetComponent<CharacterInputHandler>().HasPlayerControl;
            var live=ReadLiveEffects();
            equipment?.State?.RightRuntime.Frost.SetModifiers(live.FrostCapacity,live.FrostRecovery);
            equipment?.State?.RightRuntime.Tick(delta,controlled,!IsGuarding,1+(equipment?.State?.RightRuntime.Frost.Current??0)*live.HastePerFrost);
            equipment?.State?.LeftRuntime?.Tick(delta,controlled);
            foreach(var run in runs)
            {
                var controlValid=run.Context.requestSource==CharacterActionSource.Companion?!owner.GetComponent<CharacterInputHandler>().HasPlayerControl:owner.GetComponent<CharacterInputHandler>().HasPlayerControl;
                if(run.Context.lifeGeneration!=owner.LifeGeneration||!run.Context.source.IsAvailable||!owner.CanSimulateIntent||!controlValid)
                {run.Cancel();if(run.Context.definition.executor.UsesBodyAction)owner.CancelOwnedMainAction(run.Context.id,"ability-source-invalid");continue;}
                run.Tick(delta,Held(run.Context.operation));
            }
            deliveries.Tick(delta);
            if(iceWorld!=null){EnsureIceWorld();iceWorld?.Tick(delta);}TickRocketMarks();
            ReleaseEndedRuns();
            foreach(var binding in bindings)
            {
                if(binding.source.IsAvailable)binding.executor?.TickSource(owner,binding.source);
                else binding.executor?.StopSource(binding.source);
            }
        }
        private void ReleaseEndedRuns()
        {
            foreach(var run in runs.Where(r=>r.Ended))
            {run.Context.NotifyFeedback("end");owner.Vitals?.Recovery?.ReleaseParent(run.Context.id);run.Context.source.CancelReservation();stamina?.CancelReservation(run.Context.releasePaymentId);deliveries.CompleteParent(run.Context.id);}
            runs.RemoveAll(r=>r.Ended);
        }
        private void Emit(AbilityRunContext context,AbilityEmission emission)
            =>EmitDelivery(context,emission,context.definition.delivery,context.definition.damageChannel,context.definition.posture[emission.Index]);
        private void EmitDelivery(AbilityRunContext context,AbilityEmission emission,DeliveryParameters delivery,string channel,float posture)
        {
            if(!CommitSource(context))return;
            var power=context.effects.Damage(channel,emission.Damage);
            if(channel=="ranged-shot")
            {
                var critical=context.shot.CriticalSource==null?1:2.25;
                power=(power+emission.Damage*(context.shot.DamageMultiplier/critical-1))*critical;
            }
            var damage=Mathf.RoundToInt((float)power);
            var emitted=deliveries.Emit(owner.Vitals,context.id,emission.Index,delivery,damage,posture,
                owner.transform.position,context.direction,context.landing,()=>this!=null&&isActiveAndEnabled&&owner!=null&&owner.IsAlive&&owner.LifeGeneration==context.lifeGeneration,
                receipts=>{LastResult="接触 "+receipts.Count+" #"+context.id.ToString("N");context.onResolved?.Invoke(emission.Index,channel,receipts);Feedback?.Invoke(context.id,receipts.Any(r=>r.Reason==HitReason.Applied)?"hit":"miss",context.landing);},context.projectileSprite,context.projectileScale,context.definition.reclaimBudget,context.definition.reclaimRate);
            if(emitted){EmittedCount++;Feedback?.Invoke(context.id,"release",owner.transform.position);}
        }
        private bool CommitSource(AbilityRunContext context)
        {
            if(!context.source.IsAvailable)return false;
            if(!context.SourceCommitted)
            {
                if(context.definition.releaseCost>0)
                {if(!stamina.CommitReserved(context.releasePaymentId,owner.SimulationTime,()=>context.source.CommitRelease(context.id),"ability-release"))return false;}
                else if(!context.source.CommitRelease(context.id))return false;
                context.MarkSourceCommitted();
            }
            return true;
        }
        public void CancelPending(string reason){foreach(var run in runs){if(!run.Ended)run.Context.NotifyFeedback("cancel");run.Cancel();stamina?.CancelReservation(run.Context.releasePaymentId);}foreach(var binding in bindings)binding.executor?.StopSource(binding.source);}
        private void OnDisable()
        {if(owner!=null)foreach(var run in runs)if(run.Context.definition.executor.UsesBodyAction)owner.CancelOwnedMainAction(run.Context.id,"ability-disabled");CancelPending("disabled");equipment?.State?.RightRuntime.ShotEnhancements.Clear();deliveries.Clear();iceWorld?.Dispose();iceWorld=null;rocketMarks.Clear();rocketDirectReadyAt.Clear();if(owner!=null)ReleaseEndedRuns();}
    }
}
