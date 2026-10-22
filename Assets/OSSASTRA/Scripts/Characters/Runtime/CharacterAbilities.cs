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
    public sealed class CharacterAbilities:MonoBehaviour,ICharacterAbilityController,ICombatGuard,ICharacterOffhandStatus,ICharacterMotionOverride,ICombatEvasion
    {
        private sealed class EquipmentSource:IAbilitySource,IAbilityStateSource
        {
            private readonly CharacterEquipment equipment;
            private readonly bool main,wear;
            public string OwnerId=>equipment?.State?.OwnerId;
            public EquipmentAbilityState RuntimeState=>equipment.State.RuntimeFor(InstanceId);
            public string InstanceId {get;}
            public EquipmentSource(CharacterEquipment equipment,bool main,bool wear)
            {this.equipment=equipment;this.main=main;this.wear=wear;InstanceId=main?equipment.State.RightInstanceId:equipment.State.LeftInstanceId;}
            public bool IsAvailable=>equipment!=null&&equipment.isActiveAndEnabled&&equipment.State!=null&&
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
        }
        private readonly List<BoundGesture> bindings=new List<BoundGesture>();
        private readonly List<AbilityRun> runs=new List<AbilityRun>();
        private readonly CombatDeliveryQueue deliveries=new CombatDeliveryQueue();
        private CharacterRoot owner;
        private CharacterEquipment equipment;
        private CharacterStamina stamina;
        private CharacterInputFrame input;
        private string bindingIdentity;
        private int bindingEpoch;
        private bool previouslyAllowed;
        private bool previouslySuspended;
        public string LastResult {get;private set;}="能力未初始化";
        public int AcceptedCount {get;private set;}
        public int EmittedCount {get;private set;}
        public int ResolvedCount=>deliveries.ResolvedCount;
        public int InFlightCount=>deliveries.InFlightCount;
        public bool IsBusy=>runs.Any(r=>!r.Ended);
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
        public void OnGuarded(HitOutcome outcome)=>ActiveGuard?.OnGuarded(outcome);

        public void Initialize(CharacterRoot root)
        {owner=root;equipment=GetComponent<CharacterEquipment>();stamina=GetComponent<CharacterStamina>();Rebind();}
        public AbilityDefinitionSO SkillDefinition(int slot)
        {
            var profile=equipment?.RightDefinition?.skillLoadout;if(profile==null||slot<0||slot>2)return null;
            if(slot==1)return profile.nativeAbility;
            if(equipment.State.RightRuntime.SkillSelections.TryGetValue(slot,out var id))return profile.regularChoices.FirstOrDefault(d=>d!=null&&d.definitionId==id);
            return slot==0?profile.defaultQ:profile.defaultR;
        }
        public AbilityDefinitionSO[] SkillChoices=>equipment?.RightDefinition?.skillLoadout?.regularChoices??Array.Empty<AbilityDefinitionSO>();
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
        public bool RequestSkill(int slot,CharacterActionSource source)
            =>slot>=0&&slot<3&&TryRequest(SkillDefinition(slot),true,(AbilityOperation)((int)AbilityOperation.SkillOne+slot),source);
        private IEnumerable<AbilityBinding> MainBindings(EquipmentDefinitionSO definition)
        {
            if(definition.skillLoadout==null)return definition.abilityBindings??Array.Empty<AbilityBinding>();
            definition.skillLoadout.Validate();
            return (definition.abilityBindings??Array.Empty<AbilityBinding>()).Concat(Enumerable.Range(0,3).Where(i=>SkillDefinition(i)!=null)
                .Select(i=>new AbilityBinding{operation=(AbilityOperation)((int)AbilityOperation.SkillOne+i),gesture=GestureMode.Press,ability=SkillDefinition(i)}));
        }
        private void Rebind()
        {
            bindings.Clear();bindingEpoch++;
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
                    var frozen=new AbilityBinding{operation=binding.operation,gesture=binding.gesture,longPressThreshold=binding.longPressThreshold,ability=binding.ability,longPressAbility=binding.longPressAbility};
                    bindings.Add(new BoundGesture{binding=frozen,main=pair.main,gesture=new GestureRecognizer(frozen.gesture,frozen.longPressThreshold)});
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
                {foreach(var run in runs.Where(r=>r.Context.operation==binding.binding.operation&&r.RequiresHeld))run.Cancel();continue;}
                if(signal==GestureSignal.None)continue;
                var definition=signal==GestureSignal.LongPress?binding.binding.longPressAbility:binding.binding.ability;
                TryRequest(definition,binding.main,binding.binding.operation,CharacterActionSource.Player);
            }
        }
        private bool Held(AbilityOperation operation)=>operation==AbilityOperation.Secondary?input.offhandHeld:
            operation==AbilityOperation.SkillOne?input.skillOneHeld:operation==AbilityOperation.SkillTwo?input.skillTwoHeld:input.skillThreeHeld;

        public bool TryRequest(AbilityDefinitionSO definition,bool main,AbilityOperation operation,CharacterActionSource source)
        {
            if(owner==null||!isActiveAndEnabled||!owner.HasPlayerInput||equipment?.State==null||definition==null)return Reject("来源/控制不可用");
            var directory=main?equipment.RightDefinition?.skillLoadout:null;
            if(directory!=null&&definition!=directory.nativeAbility&&!directory.regularChoices.Contains(definition))return Reject("能力不属于当前主手职业目录");
            CompiledAbility compiled;
            try{compiled=definition.Compile();}catch(ArgumentException e){return Reject(e.Message);}
            var port=new EquipmentSource(equipment,main,compiled.wearOnRelease);
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
            if(owner==null||!isActiveAndEnabled||!owner.HasPlayerInput||port==null||port.OwnerId!=owner.ActorId||string.IsNullOrWhiteSpace(port.InstanceId)||!Enum.IsDefined(typeof(AbilityOperation),operation))return Reject("来源/控制身份无效");
            var resources=port.RuntimeState;
            if(resources==null)return Reject("来源缺少稳定运行payload");
            var key=port.InstanceId+":"+compiled.cooldownIdentity;
            if(!port.IsAvailable||resources.Cooldowns.Remaining(key,resources.Clock(compiled.cooldownPolicy))>0)return Reject("来源失效或冷却中");
            if(runs.Count>=8)return Reject("能力执行预算已满");
            CompiledAbilityExecutor executor;
            try{executor=compiled.executor.Prepare(owner);}catch(ArgumentException error){return Reject(error.Message);}
            if(!CanAccept(executor.ActionKind,out var conflict))return Reject(conflict);
            var direction=owner.DataBus.AttackDirection;
            if(direction.sqrMagnitude<.0001f)return Reject("无效方向");
            var landing=owner.transform.position+direction.normalized*compiled.delivery.range;
            if(executor.AimRange>0)
            {
                var aim=owner.AbilityAimPoint(input);if(!aim.HasValue)return Reject("无合法鼠标落点");
                var offset=aim.Value-owner.transform.position;offset.y=0;landing=owner.transform.position+Vector3.ClampMagnitude(offset,executor.AimRange);
            }
            if(compiled.delivery.mode==DeliveryMode.Parabolic||compiled.delivery.mode==DeliveryMode.RayLandingArc)
            {
                var aim=owner.AbilityAimPoint(input);if(!aim.HasValue)return Reject("无合法鼠标落点");
                var delta=aim.Value-owner.transform.position;delta.y=0;
                if(delta.magnitude>compiled.delivery.range||!CombatDeliveryQueue.Clear(owner.transform.position,aim.Value))return Reject("落点超距或遮挡");
                var floor=Physics.RaycastAll(aim.Value+Vector3.up*.4f,Vector3.down,.8f,compiled.delivery.collisionLayers,QueryTriggerInteraction.Ignore)
                    .Where(h=>h.normal.y>.7f&&h.collider.GetComponentInParent<CombatVitals>()==null).OrderBy(h=>h.distance).ToArray();
                if(floor.Length==0)return Reject("落点无地面");landing=floor[0].point;
            }
            if(stamina==null||!stamina.CanSpend(compiled.cost+compiled.releaseCost))return Reject("体力不足");
            var reservation=Guid.NewGuid();
            Guid id;
            if(compiled.executor.UsesBodyAction)
            {
                if(!owner.TryAbilityBodyAction(compiled.animation,source,"ability:"+compiled.id,compiled.cost,out id,()=>port.TryReserve(reservation),port.CancelReservation))return Reject(owner.LastFact);
            }
            else
            {
                var request=owner.Actions.CreateRequest(executor.ActionKind,source,"ability:"+compiled.id);id=request.RequestId;
                if(!owner.Actions.TryCommit(request,compiled.cost,()=>port.TryReserve(reservation)?request.RequestId:Guid.Empty,out var reason))return Reject(reason);
            }
            var context=new AbilityRunContext(owner,compiled,port,id,direction.normalized,landing,Emit,operation,sprite,scale);
            owner.Vitals?.Recovery?.RetainParent(id);
            deliveries.RegisterParent(id);
            runs.Add(executor.CreateRun(context));
            if(executor.StartsCooldownOnAccept&&!resources.Cooldowns.Commit(key,id,resources.Clock(compiled.cooldownPolicy),compiled.cooldown))throw new InvalidOperationException("Accepted ability cooldown conflict");
            AcceptedCount++;LastResult="接受 "+compiled.displayName;return true;
        }
        public bool CanAccept(CharacterActionKind kind,out string reason)
        {
            reason=null;if(!isActiveAndEnabled)return true;
            foreach(var run in runs.Where(r=>!r.Ended))
            {
                var mask=run.Context.definition.plan.Occupancy;
                if((kind==CharacterActionKind.Offhand||kind==CharacterActionKind.Guard)&&(mask&AbilityOccupancy.OffHand)!=0)
                {reason="offhand-occupied";return false;}
            }
            return true;
        }
        private bool Reject(string reason){LastResult="拒绝："+reason;return false;}
        public bool OwnsRelease(Guid id)=>runs.Any(r=>r.Context.id==id&&r.Context.definition.executor.UsesBodyAction);
        public void ObserveFact(ActionFact fact)
        {
            foreach(var run in runs.Where(r=>r.Context.id==fact.ActionId).ToArray())
            {
                if(fact.Kind==ActionFactKind.Cancelled)run.Cancel();
                else if(fact.Kind==ActionFactKind.Released||fact.Kind==ActionFactKind.Finished)run.Tick(0,Held(run.Context.operation));
            }
        }
        public void TickEffects(float delta)
        {
            if(owner==null)return;
            if(!owner.IsAlive||!isActiveAndEnabled){CancelPending("life/disabled");deliveries.Clear();ReleaseEndedRuns();return;}
            if(owner.IsSimulationSuspended)return;
            var controlled=owner.GetComponent<CharacterInputHandler>().HasPlayerControl;
            equipment?.State?.RightRuntime.Tick(delta,controlled,!IsGuarding);
            equipment?.State?.LeftRuntime?.Tick(delta,controlled);
            foreach(var run in runs)
            {
                if(run.Context.lifeGeneration!=owner.LifeGeneration||!run.Context.source.IsAvailable||!owner.CanSimulateIntent||!owner.GetComponent<CharacterInputHandler>().HasPlayerControl)
                {run.Cancel();if(run.Context.definition.executor.UsesBodyAction)owner.CancelOwnedMainAction(run.Context.id,"ability-source-invalid");continue;}
                run.Tick(delta,Held(run.Context.operation));
            }
            deliveries.Tick(delta);
            ReleaseEndedRuns();
        }
        private void ReleaseEndedRuns()
        {
            foreach(var run in runs.Where(r=>r.Ended))
            {owner.Vitals?.Recovery?.ReleaseParent(run.Context.id);run.Context.source.CancelReservation();deliveries.CompleteParent(run.Context.id);}
            runs.RemoveAll(r=>r.Ended);
        }
        private void Emit(AbilityRunContext context,AbilityEmission emission)
        {
            if(!context.source.IsAvailable)return;
            if(!context.SourceCommitted)
            {
                if(context.definition.releaseCost>0&&!stamina.Spend(context.releasePaymentId,context.definition.releaseCost,owner.SimulationTime,"ability-release"))return;
                if(!context.source.CommitRelease(context.id))return;
                context.MarkSourceCommitted();
            }
            var emitted=deliveries.Emit(owner.Vitals,context.id,emission.Index,context.definition.delivery,emission.Damage,context.definition.posture[emission.Index],
                owner.transform.position,context.direction,context.landing,()=>this!=null&&isActiveAndEnabled&&owner!=null&&owner.IsAlive&&owner.LifeGeneration==context.lifeGeneration,
                receipts=>LastResult="接触 "+receipts.Count+" #"+context.id.ToString("N"),context.projectileSprite,context.projectileScale,context.definition.reclaimBudget,context.definition.reclaimRate);
            if(emitted)EmittedCount++;
        }
        public void CancelPending(string reason){foreach(var run in runs)run.Cancel();}
        private void OnDisable()
        {if(owner!=null)foreach(var run in runs)if(run.Context.definition.executor.UsesBodyAction)owner.CancelOwnedMainAction(run.Context.id,"ability-disabled");CancelPending("disabled");deliveries.Clear();if(owner!=null)ReleaseEndedRuns();}
    }
}
