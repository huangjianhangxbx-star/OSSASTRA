/* 脚本：CharacterMainAction
 * 功能：独立主手动作组选择及生效来源、接受快照、阶段突进及有效扇区投递。
 * 边界：不采设备、不拥有第二动作钟、不直接Move/写HP；选择保存在装备原实例。
 * 协作：Root显式刷新/接受/推进；实际投递结果用于连携出手判断；DataBus运动端口与CombatDeliveryQueue复用原权威。
 */
using System;
using System.Linq;
using UnityEngine;
using Ossastra.CharacterCore;
using Ossastra.CharacterCore.Abilities;
using Ossastra.Combat;
namespace Ossastra.Characters
{
    [DisallowMultipleComponent]
    public sealed class CharacterMainAction:MonoBehaviour,ICharacterMotionOverride
    {
        public sealed class PreparedStage
        {
            public readonly MainActionStage Stage;
            public readonly ConfiguredActionSnapshot Phase;
            public readonly ActionDefinitionSO Definition;
            public readonly int BasicAttackStage;
            public readonly float Cost,ContactDuration;
            public readonly bool Last;
            public readonly double FullRecovery;
            internal PreparedStage(MainActionStage stage,ConfiguredActionSnapshot phase,ActionDefinitionSO definition,int basicAttackStage,float contactDuration,bool last,double recovery)
            {Stage=stage;Phase=phase;Definition=definition;BasicAttackStage=basicAttackStage;Cost=definition.staminaCost;ContactDuration=contactDuration;Last=last;FullRecovery=recovery;}
        }
        private CharacterRoot owner;
        private CharacterEquipment equipment;
        private CharacterAbilities abilities;
        private CharacterInputHandler input;
        private CombatEngagementSession engagement;
        private FrozenMainActionProfile profile;
        private MainActionProfileSO profileDefinition;
        private int sourceRevision=-1;
        private string cachedSelection,cachedWeapon,cachedQ,cachedR;
        private PreparedStage accepted;
        private Guid actionId;
        private double acceptedAt,previousMotionAt,finalRecoveryUntil;
        private Vector3 motionDirection;
        private int generation;
        private string weaponInstance;
        private readonly CombatDeliveryQueue deliveries=new CombatDeliveryQueue();
        public event Action<Guid,bool> Resolved;
        public string LastResult {get;private set;}="无";
        public bool IsActive=>MotionVelocity.sqrMagnitude>.000001f;
        public Vector3 MotionVelocity {get;private set;}
        public MainActionSequence Sequence=>profile?.Sequence;
        public MainActionProfileSO ComboSource { get; private set; }
        internal BasicComboSettings? AuthoredComboSettings => profileDefinition == null ? (BasicComboSettings?)null : profileDefinition.ComboSettings;
        public string ComboFallbackReason { get; private set; }
        public string SelectedId=>equipment?.State?.RightRuntime.MainActionProfileId??"";
        public string FormLabel=>profileDefinition==null?"I":profileDefinition.formLabel;
        public MainActionProfileSO[] Choices=>equipment?.RightDefinition?.mainActionProfiles??Array.Empty<MainActionProfileSO>();
        public bool AllowsBasicRequest=>owner==null||owner.SimulationTime>=finalRecoveryUntil;
        public Guid ExecutionId=>actionId;
        public string AcceptedStageId=>accepted?.Stage.Id;
        public int InFlightCount=>deliveries.InFlightCount;
#if UNITY_EDITOR
        public void InvalidateLaboratoryProfile() => cachedSelection = null;
#endif
        public void Initialize(CharacterRoot root)
        {owner=root;equipment=GetComponent<CharacterEquipment>();abilities=GetComponent<CharacterAbilities>();input=GetComponent<CharacterInputHandler>();engagement=FindObjectOfType<CombatEngagementSession>();}
        public bool Configure(string id)
        {
            if(!isActiveAndEnabled||owner==null||input==null||!input.HasPlayerControl||!owner.CanSimulateIntent||owner.ActionActive||
                abilities!=null&&abilities.IsBusy||engagement!=null&&engagement.Engaged||equipment?.State==null)return Reject("仅实控角色脱战空闲时可配置主手动作组");
            id=id??"";var choice=Choices.FirstOrDefault(p=>p!=null&&p.definitionId==id);
            if(id.Length>0&&choice==null)return Reject("当前装备未登记此动作组");
            try{choice?.Freeze(abilities?.SnapshotEffects());}catch(ArgumentException e){return Reject("动作组无效："+e.Message);}
            equipment.State.RightRuntime.MainActionProfileId = id;
            LastResult = "已选择" + (choice == null ? "角色默认" : choice.displayName);
            // 同帧还可能收到普攻边沿，先更新实际选择并丢弃旧组缓存。
            owner.StateArbitrator?.Combat?.RefreshMainProfile();
            return true;
        }
        /// <summary>只在身体空闲时重建候选；来源/槽位改变不追改已经接受的动作快照。</summary>
        public bool RefreshWhenIdle()
        {
            if(owner==null||owner.ActionActive||equipment?.State==null)return false;
            if (!isActiveAndEnabled)
            {
                bool hadProfile = profile != null;
                profile = null;
                profileDefinition = null;
                cachedSelection = null;
                ComboSource = null;
                ComboFallbackReason = null;
                return hadProfile;
            }
            var state=equipment.State.RightRuntime;state.SkillSelections.TryGetValue(0,out var q);state.SkillSelections.TryGetValue(2,out var r);
            if(cachedSelection==SelectedId&&cachedWeapon==equipment.State.RightInstanceId&&sourceRevision==state.Sources.Revision&&cachedQ==q&&cachedR==r)return false;
            cachedSelection=SelectedId;cachedWeapon=equipment.State.RightInstanceId;sourceRevision=state.Sources.Revision;cachedQ=q;cachedR=r;
            var source = Choices.FirstOrDefault(p => p != null && p.definitionId == SelectedId);
            profileDefinition = source;
            profile = null;
            ComboSource = null;
            ComboFallbackReason = source == null && !string.IsNullOrEmpty(SelectedId)
                ? "当前主手未登记选中的动作组：" + SelectedId : null;
#if UNITY_EDITOR
            profileDefinition = AttackLabBindings.Resolve(owner, profileDefinition);
#endif
            if(profileDefinition!=null)
            {
                try
                {
                    profile = profileDefinition.Freeze(abilities?.SnapshotEffects(), owner.Definition?.eventCalibration);
                    // 调试候选可替换运行值，配置来源仍指向可定位的作者资产。
                    ComboSource = source;
                }
                catch (ArgumentException e)
                {
                    ComboFallbackReason = e.Message;
                    LastResult = "拒绝：动作组无效，使用角色默认：" + ComboFallbackReason;
                    profileDefinition = null;
                }
            }
            return true;
        }
        public bool TryPrepare(int index,out PreparedStage prepared)
        {
            prepared=null;if(profile==null||index<0||index>=profile.Sequence.Count)return false;
            try
            {
                var stage=profile.Sequence.Stage(index);var definition=profile.AnimationDefinition(index);
                var animation = owner.FreezeAbilityPhase(definition,
                    new CharacterAnimationPreparation(true, index + 1, useAuthoredLogic: true));
                var t = stage.Timeline;
                var impact = t.PostureImpact * profile.Sequence.PostureScale;
                // 真正选中的 Mapping 冻结开放与释放时序。装备组合仍只提供伤害和架势强度。
                var timing = animation.Timeline;
                if (!timing.EventDriven && stage.DashDistance > 0 && stage.DashAt + stage.DashDuration > timing.FinishAt)
                    return Reject("突进超过动作结束：" + stage.Id);
                var timeline = timing.EventDriven
                    ? BasicAttackDefinition.FromEvents(t.Damage, impact)
                    : new BasicAttackDefinition(timing.ReleaseAt, timing.MoveReadyAt, timing.AttackReadyAt,
                        timing.FinishAt, t.Damage, impact);
                var phase=animation.WithTimeline(timeline);
                prepared=new PreparedStage(stage,phase,definition,index+1,profileDefinition.contactDuration,index==profile.Sequence.Count-1,profile.Sequence.FullComboRecovery);return true;
            }
            catch(ArgumentException e){return Reject("动作映射无效："+e.Message);}
        }
        public void Accept(Guid id,PreparedStage stage,Vector3 direction)
        {
            accepted=stage;actionId=id;acceptedAt=previousMotionAt=owner.SimulationTime;motionDirection=direction.normalized;
            generation=owner.LifeGeneration;weaponInstance=equipment.State.RightInstanceId;MotionVelocity=Vector3.zero;deliveries.RegisterParent(id);
        }
        public void ObserveFact(ActionFact fact)
        {
            if(fact.ActionId!=actionId)return;
            if(fact.Kind==ActionFactKind.Finished && accepted?.Last==true && accepted.Phase.Timeline.EventDriven)
                finalRecoveryUntil=owner.SimulationTime+accepted.FullRecovery;
            if(fact.Kind==ActionFactKind.Finished||fact.Kind==ActionFactKind.Cancelled)
            {deliveries.CompleteParent(actionId);accepted=null;actionId=Guid.Empty;if(fact.Kind==ActionFactKind.Cancelled)MotionVelocity=Vector3.zero;}
        }
        public bool TryRelease(ActionFact fact,Vector3 direction,out bool delivered)
        {
            delivered = false;
            if(accepted==null||fact.ActionId!=actionId)return false;
            var stage=accepted;var id=actionId;var state=equipment.State.RightRuntime;
            if(stage.Last&&!stage.Phase.Timeline.EventDriven)finalRecoveryUntil=acceptedAt+stage.Phase.Timeline.FinishAt+stage.FullRecovery;
            if(!string.IsNullOrEmpty(stage.Stage.SourceId)&&!state.Frost.Spend(1,state.EquippedTime))
            {LastResult="拒绝：凿冰释放需要 1 霜寒";return true;}
            var life=generation;var instance=weaponInstance;var t=stage.Phase.Timeline;
            var impactRun = owner.StateArbitrator.Combat.ImpactFor(id);
            delivered = deliveries.Emit(owner.Vitals,id,fact.Release.EmissionIndex,new DeliveryParameters{mode=DeliveryMode.Sector,range=(float)stage.Stage.Range,arc=(float)stage.Stage.ArcDegrees,
                activeDuration=stage.ContactDuration,followSource=true},t.Damage,(float)t.PostureImpact,transform.position,direction,transform.position,
                ()=>this!=null&&isActiveAndEnabled&&owner!=null&&owner.IsAlive&&owner.LifeGeneration==life&&equipment.State.RightInstanceId==instance,
                receipts=>Resolved?.Invoke(id,receipts.Any(r=>r.Reason==HitReason.Applied)),basicAttackStage:stage.BasicAttackStage,hitStop:fact.Release.HitStop,bindRelease:impactRun == null ? null : impactRun.Bind);
            if(!string.IsNullOrEmpty(stage.Stage.SourceId))abilities?.ScheduleIceColumn(id,transform.position,transform.position+direction.normalized*1.5f,stage.Stage.SourceId);
            LastResult="已释放 "+stage.Stage.Id;return true;
        }
        /// <summary>积分本根时钟区间与突进窗口的交集，避免低帧跨过短窗口丢位移；仅输出速度。</summary>
        public void PrepareMotion(double endTime,float delta)
        {
            MotionVelocity=Vector3.zero;if(accepted==null||delta<=0)return;
            var distance=accepted.Stage.TravelBetween(previousMotionAt-acceptedAt,endTime-acceptedAt);previousMotionAt=endTime;
            if(distance>0)MotionVelocity=motionDirection*(float)(distance/delta);
        }
        internal void CloseAttachedContacts(Guid id) => deliveries.CloseAttachedContacts(id);
        public void TickEffects(float delta, bool detachedOnly = false){if(delta>0)deliveries.Tick(delta, detachedOnly);}
        public void ResetState(){deliveries.Clear();accepted=null;actionId=Guid.Empty;MotionVelocity=Vector3.zero;finalRecoveryUntil=0;}
        private bool Reject(string reason){LastResult="拒绝："+reason;return false;}
        private void OnDisable(){if(actionId!=Guid.Empty&&owner!=null)owner.CancelOwnedMainAction(actionId,"main-action-disabled");ResetState();}
    }
}
