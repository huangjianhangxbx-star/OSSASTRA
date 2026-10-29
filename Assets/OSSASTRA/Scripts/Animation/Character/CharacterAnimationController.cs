/* 角色事实适配器：Root的既有表现端口→原式Intent/配置/Composer/Handle。
 * 不拥有玩法状态、动作钟或伤害。正式片段只在SO中配置；可选方向端口提交结构层，观察镜像只读Bus派生值，保持单一Composer/Handle。
 */
using System;
using System.Linq;
using Ossastra.AnimationCore;
using Ossastra.AnimationPresentation;
using Ossastra.CharacterCore;
using Ossastra.Characters;
using Spine.Unity;
using UnityEngine;
namespace Ossastra.CharacterAnimation
{
    [RequireComponent(typeof(CharacterAnimationConfigManager),typeof(CharacterAnimationComposer),typeof(AnimationHandleSystem))]
    public sealed class CharacterAnimationController : MonoBehaviour,ICharacterPresentation,ICharacterAnimationFacts,ICharacterConfiguredAnimation,ICharacterAnimationStateView,ICharacterDefaultActionAnimation,ICharacterPreparedAnimation
    {
        [SerializeField] private SkeletonAnimation skeleton;
        private Ossastra.Characters.CharacterRoot root;
        private Ossastra.Characters.CharacterDataBus bus;
        private CharacterAnimationConfigManager config;
        private CharacterAnimationComposer composer;
        private AnimationHandleSystem handle;
        private CharacterDirectionPresentation direction;
        private Ossastra.Combat.ICombatGuard[] guards;
        private CharacterAnimationPlan captured;
        private Guid capturedId,phaseId;
        private int generation,stage;
        private bool alive=true,staggered,hitReaction;
        private float scaleX=1,timeScale=1;
        public SkeletonAnimation Skeleton=>skeleton;
        public AnimationSemantic CurrentSemantic {get;private set;}
        public string CurrentClip=>skeleton?.AnimationState?.GetTrack(0)?.Animation?.Name;
        public string CurrentSource {get;private set;}
        public Guid CurrentReplayId {get;private set;}
        public Guid CapturedActionId=>capturedId;
        public Guid CapturedPhaseId=>phaseId;
        public AnimationIntentSourceStateMachine CapturedSource=>captured?.runtimeSource??AnimationIntentSourceStateMachine.None;
        public string FallbackReason=>composer?.LastResult;
        public int CommitCount=>handle?.CommitCount??0;
        public event Action<AnimationSignal> SpineSignal;
        public bool Initialize(Ossastra.Characters.CharacterDataBus dataBus)
        {
            bus=dataBus;root=GetComponentInParent<Ossastra.Characters.CharacterRoot>();
            if(skeleton==null)skeleton=GetComponent<SkeletonAnimation>();
            config=GetComponent<CharacterAnimationConfigManager>();composer=GetComponent<CharacterAnimationComposer>();handle=GetComponent<AnimationHandleSystem>();
            if(root==null||bus==null||skeleton==null||config==null||config.baseMap==null||composer==null||handle==null)return false;
            skeleton.Initialize(false);if(skeleton.Skeleton==null||!handle.Bind(root,skeleton,"body"))return false;
            config.Initialize(root);composer.Initialize(root,config,handle,"body",intent=>
            {
                var gear=root.GetComponent<Ossastra.Characters.CharacterEquipment>();
                return intent.intentSourceStateMatine==AnimationIntentSourceStateMachine.OffHand?gear?.LeftDefinition:gear?.RightDefinition;
            });
            guards=root.GetComponents<MonoBehaviour>().OfType<Ossastra.Combat.ICombatGuard>().ToArray();
            scaleX=Mathf.Abs(skeleton.Skeleton.ScaleX);timeScale=skeleton.timeScale;
            direction=GetComponent<CharacterDirectionPresentation>();
            if(root.Definition?.directionProfile?.Enabled==true&&direction==null)return false;
            if(direction!=null&&!direction.Initialize(root,skeleton,config))return false;
            handle.Signal-=OnSignal;handle.Signal+=OnSignal;return true;
        }
        private void OnSignal(AnimationSignal signal)=>SpineSignal?.Invoke(signal);
        public void FlushSignals()=>handle?.FlushSignals();
        private CharacterAnimationPlan ResolveAction(Ossastra.Characters.ActionDefinitionSO definition,CharacterAnimationPreparation preparation)
        {
            if(definition==null||definition.animationLogicId==EAnimationLogicState.None||!Enum.IsDefined(typeof(EAnimationLogicState),definition.animationLogicId))return null;
            var source=preparation.MainHand.HasValue?(preparation.MainHand.Value?AnimationIntentSourceStateMachine.MainHand:AnimationIntentSourceStateMachine.OffHand):AnimationIntentSourceStateMachine.Skill;
            var index=preparation.Stage;
            if(index==0)index=definition.animationLogicId==EAnimationLogicState.Attack_Normal_1?1:definition.animationLogicId==EAnimationLogicState.Attack_Normal_2?2:definition.animationLogicId==EAnimationLogicState.Attack_Normal_3?3:definition.animationLogicId==EAnimationLogicState.Attack_Normal_4?4:0;
            var semantic=index==1?EAnimSemantic.Attack_Normal_1:index==2?EAnimSemantic.Attack_Normal_2:index==3?EAnimSemantic.Attack_Normal_3:index==4?EAnimSemantic.Attack_Normal_4:EAnimSemantic.Ability;
            return ResolveCandidate(definition.animationLogicId,semantic,source,preparation.MainHand,index);
        }
        private CharacterAnimationPlan ResolveCandidate(EAnimationLogicState fallback,EAnimSemantic semantic,AnimationIntentSourceStateMachine source,bool? mainHand,int preparedStage=0)
        {
            var candidate=new AnimationIntent{root=root,actorId=root.ActorId,target="body",lifeGeneration=root.LifeGeneration,presentationGeneration=generation,
                intentSourceStateMatine=source,semantic=semantic,logicId=fallback,phaseId=Guid.NewGuid(),oneShot=true};
            var intents=composer.SnapshotIntents().ToDictionary(pair=>pair.Key,pair=>pair.Value);
            intents[source]=candidate;
            var equipment=root.GetComponent<Ossastra.Characters.CharacterEquipment>();
            var context=new AnimationComposeContext{root=root,dataBus=bus,target="body",activeIntents=intents,preparedStage=preparedStage,
                sourceEquipment=mainHand.HasValue?(mainHand.Value?equipment?.RightDefinition:equipment?.LeftDefinition):null,configurationVersion=config.ConfigurationVersion};
            var logic=config.RouteIntent(candidate,context);var plan=config.ResolvePlan(logic,context);
            if(plan!=null){plan.runtimeSource=source;plan.runtimeSemantic=semantic;}return plan;
        }
        public bool CanCaptureAction(Ossastra.Characters.ActionDefinitionSO definition,out string reason)
        {
            reason=null;if(config==null||skeleton?.Skeleton==null){reason="animation-not-initialized";return false;}
            var plan=ResolveAction(definition,new CharacterAnimationPreparation(true));if(plan==null||!plan.HasOperations){reason="missing-original-animation-plan:"+definition?.animationLogicId;return false;}
            foreach(var clip in plan.clips.Concat(plan.queuedClips.Where(q=>q!=null).Select(q=>q.clip)))
                if(clip==null||clip.track<0||skeleton.Skeleton.Data.FindAnimation(clip.animaName)==null||!FinitePositive(clip.playbackRate)||float.IsNaN(clip.mixDuration)||float.IsInfinity(clip.mixDuration)||clip.mixDuration<0)
                {reason="invalid-original-animation-clip:"+clip?.animaName;return false;}
            return true;
        }
        private static bool FinitePositive(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value)&&value>0;
        public ConfiguredActionSnapshot FreezeAction(Ossastra.Characters.ActionDefinitionSO definition)
            =>PrepareAnimation(definition,new CharacterAnimationPreparation(true));
        public ConfiguredActionSnapshot PrepareAnimation(Ossastra.Characters.ActionDefinitionSO definition,CharacterAnimationPreparation preparation)
        {
            if(definition==null)throw new ArgumentException("missing-action-definition");
            return FreezePlan(ResolveAction(definition,preparation),definition.Freeze());
        }
        private ConfiguredActionSnapshot FreezePlan(CharacterAnimationPlan original,BasicAttackDefinition timeline)
        {
            if(!ValidatePlan(original,out var reason))throw new ArgumentException(reason);
            var plan=original.CloneFor(root);
            foreach(var clip in plan.clips.Concat(plan.queuedClips.Where(q=>q!=null).Select(q=>q.clip)))
                if(clip.scalesWithAction)
                {
                    var duration=clip.sourceDuration>0?clip.sourceDuration:skeleton.Skeleton.Data.FindAnimation(clip.animaName).Duration;
                    clip.playbackRate=duration/(float)timeline.FinishAt;
                }
            var primary=plan.clips.FirstOrDefault(c=>c.track==0);
            return new ConfiguredActionSnapshot(timeline,primary?.animaName,primary?.mixDuration??0,primary?.playbackRate??1,primary?.loop??false,()=>plan.CloneFor(root),replacement=>FreezePlan(original,replacement));
        }
        public void CaptureAction(Guid id,Ossastra.Characters.ActionDefinitionSO definition)=>CapturePhase(id,FreezeAction(definition));
        public void CaptureDefaultAction(Guid id,BasicAttackDefinition timeline,int index)
            =>CapturePhase(id,FreezeDefaultAction(timeline,index));
        public ConfiguredActionSnapshot FreezeDefaultAction(BasicAttackDefinition timeline,int index)
        {
            var logic=index==2?EAnimationLogicState.Attack_Normal_2:index==3?EAnimationLogicState.Attack_Normal_3:index==4?EAnimationLogicState.Attack_Normal_4:EAnimationLogicState.Attack_Normal_1;
            var plan=ResolveCandidate(logic,index==2?EAnimSemantic.Attack_Normal_2:index==3?EAnimSemantic.Attack_Normal_3:index==4?EAnimSemantic.Attack_Normal_4:EAnimSemantic.Attack_Normal_1,AnimationIntentSourceStateMachine.MainHand,true,index);
            if(plan==null)throw new ArgumentException("Default action mapping missing: "+logic);
            return FreezePlan(plan,timeline);
        }
        public bool CanCapturePhase(ConfiguredActionSnapshot snapshot,out string reason)
            =>ValidatePlan(snapshot?.CopyPresentationPlan() as CharacterAnimationPlan,out reason);
        private bool ValidatePlan(CharacterAnimationPlan plan,out string reason)
        {
            reason=plan==null||!plan.HasOperations?"missing-frozen-animation-plan":null;
            if(reason==null)foreach(var clip in plan.clips.Concat(plan.queuedClips.Where(q=>q!=null).Select(q=>q.clip)))
                if(clip==null||clip.track<0||skeleton?.Skeleton?.Data.FindAnimation(clip.animaName)==null||!FinitePositive(clip.playbackRate)||float.IsNaN(clip.mixDuration)||float.IsInfinity(clip.mixDuration)||clip.mixDuration<0)
                {reason="invalid-frozen-animation-clip:"+clip?.animaName;break;}
            return reason==null;
        }
        public ConfiguredActionSnapshot PrepareDodge(float duration)
            =>FreezePlan(ResolveCandidate(EAnimationLogicState.Dodge,EAnimSemantic.Dodge,AnimationIntentSourceStateMachine.Dodge,null),new BasicAttackDefinition(0,0,0,duration,1));
        public void CapturePhase(Guid id,ConfiguredActionSnapshot snapshot)
        {
            var plan=snapshot.CopyPresentationPlan() as CharacterAnimationPlan;
            if(plan==null)throw new ArgumentException("Frozen phase has no original animation plan");
            captured=plan;capturedId=id;phaseId=Guid.NewGuid();
        }
        public void CollectFacts(bool isAlive,bool isStaggered,bool reacting,int presentationGeneration,int attackStage=1)
        {alive=isAlive;staggered=isStaggered;hitReaction=reacting;stage=attackStage;if(generation!=presentationGeneration){generation=presentationGeneration;composer?.ClearAllIntents();}}
        public void SetPaused(bool paused){if(skeleton!=null)skeleton.timeScale=paused?0:timeScale;}
        private void Submit(AnimationIntentSourceStateMachine source,EAnimSemantic semantic,int priority,Guid action,Guid phase=default,CharacterAnimationPlan plan=null,bool exclusive=false,bool oneShot=false)
        {
            composer.SubmitIntent(new AnimationIntent{root=root,actorId=root.ActorId,target="body",intentSourceStateMatine=source,semantic=semantic,
                priority=priority,layer=priority>=20?AnimationLayer.FullBodyAction:AnimationLayer.BaseFullBody,exclusiveFullBody=exclusive,oneShot=oneShot,
                actionId=action,phaseId=phase,logicId=plan?.runtimeLogicId??EAnimationLogicState.None,lifeGeneration=root.LifeGeneration,presentationGeneration=generation,configurationVersion=config.ConfigurationVersion,frozenPlan=plan});
        }
        public void Present()
        {
            if(bus==null||skeleton?.Skeleton==null)return;
            if(!alive||!root.IsAlive){composer.ClearAllIntents();return;}
            Submit(AnimationIntentSourceStateMachine.Movement,bus.IsMoving?EAnimSemantic.Jog:EAnimSemantic.Idle,bus.IsMoving?10:0,Guid.Empty);
            var retain=(ActionPresentationPolicy.RetainMainPose(bus.ActionActive,bus.MoveReady,bus.IsMoving)||bus.AbilityMotionActive)&&capturedId==bus.ActionId&&captured!=null;
            var actionSource=CapturedSource==AnimationIntentSourceStateMachine.None?AnimationIntentSourceStateMachine.MainHand:CapturedSource;
            foreach(var source in new[]{AnimationIntentSourceStateMachine.MainHand,AnimationIntentSourceStateMachine.Skill,AnimationIntentSourceStateMachine.OffHand})
                if((!retain||source!=actionSource)&&composer.GetIntent(source)?.oneShot==true)composer.RemoveIntent(source);
            if(retain)Submit(actionSource,captured.runtimeSemantic,20,capturedId,phaseId,captured,true,true);
            var motion=bus.MotionOverride as ICharacterMotionAnimation;
            if(motion!=null&&motion.IsActive)
            {
                var plan=root.GetComponent<CharacterDodge>()?.PreparedAnimation?.CopyPresentationPlan() as CharacterAnimationPlan;
                if(plan!=null)Submit(AnimationIntentSourceStateMachine.Dodge,EAnimSemantic.Dodge,30,motion.ActionId,motion.ActionId,plan,true,true);
            }
            else composer.RemoveIntent(AnimationIntentSourceStateMachine.Dodge);
            if(!(retain&&actionSource==AnimationIntentSourceStateMachine.OffHand))
            {
                if(guards.Any(g=>g.IsGuarding))
                {
                    var abilities=root.GetComponent<Ossastra.Characters.CharacterAbilities>();Guid guardId=Guid.Empty;
                    if(abilities!=null)abilities.TryGetHandPresentation(false,out guardId,out _,out _);
                    Submit(AnimationIntentSourceStateMachine.OffHand,EAnimSemantic.Guard,20,guardId);
                }
                else Submit(AnimationIntentSourceStateMachine.OffHand,EAnimSemantic.Idle,5,Guid.Empty);
            }
            if(hitReaction)Submit(AnimationIntentSourceStateMachine.Hurt,EAnimSemantic.HitReact,40,Guid.Empty,exclusive:true);
            else composer.RemoveIntent(AnimationIntentSourceStateMachine.Hurt);
            if(staggered)Submit(AnimationIntentSourceStateMachine.Status,EAnimSemantic.Stagger,50,Guid.Empty,exclusive:true);
            else composer.RemoveIntent(AnimationIntentSourceStateMachine.Status);
            if(direction!=null&&direction.IsConfigured)
            {
                direction.SubmitDirection(composer,direction.ReadGameplayDirection(),generation);
                skeleton.Skeleton.ScaleX=scaleX*(direction.Mirrored?-1:1);
            }
            else skeleton.Skeleton.ScaleX=scaleX*bus.ViewFacingX;
            composer.Commit(generation);
            var selected=composer.CurrentBodyIntent;
            if(selected!=null){CurrentSemantic=AnimationSemanticTranslation.ToShared(selected.semantic);CurrentSource=selected.oneShot&&selected.actionId==bus.ActionId&&selected.priority==20?"action":selected.intentSourceStateMatine.ToString();CurrentReplayId=selected.phaseId!=Guid.Empty?selected.phaseId:selected.actionId;}
        }
        private void OnDisable(){composer?.ClearAllIntents();if(handle!=null)handle.Signal-=OnSignal;}
        private void OnEnable(){if(handle!=null){handle.Signal-=OnSignal;handle.Signal+=OnSignal;}}
        private void OnDestroy(){if(handle!=null)handle.Signal-=OnSignal;}
    }
}
