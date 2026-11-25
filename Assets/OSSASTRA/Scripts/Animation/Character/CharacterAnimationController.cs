/* 角色事实适配器：Root的表现端口→Intent/配置/Composer/Handle。
 * 不拥有玩法状态、动作钟或伤害。墙边语义和朝向只读运动事实，动画片段由SO配置。
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
        [SerializeField] private string postureLaunchClip, postureDownClip, postureRiseClip;
        [SerializeField] private bool usePosturePoseFallback;
        [SerializeField] private string postureDodgeFallbackClip;
        private Quaternion authoredSkeletonRotation;
        private bool temporaryPosturePose;
        private CharacterAnimationPlan reactionPlan;
        private OrdinaryJumpPhase presentedJumpPhase;
        private PostureReactionPhase displayedReaction;
        private Guid displayedReactionId;
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
            authoredSkeletonRotation = skeleton.transform.localRotation;
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
        public bool HasCalibratedFootstep=>handle!=null && handle.HasCalibratedFootstep;
        public static event Action<CharacterAnimationController,AnimationSignal> EffectRequested;
        public Guid CustomReleaseSound { get; private set; }
        public Guid CustomReleaseEffect { get; private set; }
        public Vector3 AnchorPosition(AnimationCalibrationBinding binding)
        {
            var bone=binding.ResolveAnchor(skeleton.Skeleton);
            if(bone!=null)
            {
                bone.AppliedPose.LocalToWorld(binding.localOffset.x,binding.localOffset.y,out var x,out var y);
                return skeleton.transform.TransformPoint(new Vector3(x,y,binding.localOffset.z));
            }
            var position=Vector3.zero;
            var offset=binding.localOffset;
            offset.x*=Mathf.Sign(skeleton.Skeleton.ScaleX);
            return skeleton.transform.TransformPoint(position+offset);
        }
        public Quaternion AnchorRotation(AnimationCalibrationBinding binding)
        {
            var bone=binding.ResolveAnchor(skeleton.Skeleton);
            var angle=bone==null?binding.rotation:bone.AppliedPose.LocalToWorldRotation(binding.rotation);
            return skeleton.transform.rotation*Quaternion.Euler(0,0,angle);
        }
        private void OnSignal(AnimationSignal signal)
        {
            if(signal.Calibrated && signal.Purpose==AnimationSignalPurpose.Marker && signal.Meaning==AnimationEventMeaning.Release)
            {
                CustomReleaseSound=signal.Binding.sound?signal.ActionId:Guid.Empty;
                CustomReleaseEffect=signal.Binding.effect?signal.ActionId:Guid.Empty;
            }
            if (signal.ActionId != Guid.Empty && root && signal.LifeGeneration == root.LifeGeneration)
                root.StateArbitrator?.Combat?.OnImpactSpine(signal.ActionId, signal.PhaseId, signal.Name);
            SpineSignal?.Invoke(signal);
            if(signal.Calibrated && signal.Purpose==AnimationSignalPurpose.Effect) EffectRequested?.Invoke(this,signal);
            if(!signal.Calibrated || signal.Purpose!=AnimationSignalPurpose.Marker || signal.ActionId==Guid.Empty)return;
            switch(signal.Meaning)
            {
                case AnimationEventMeaning.Release: root.ConsumeAnimationMarker(signal.ActionId,signal.PhaseId,ActionMarkerKind.Release);break;
                case AnimationEventMeaning.Finish: root.ConsumeAnimationMarker(signal.ActionId,signal.PhaseId,ActionMarkerKind.Finish);break;
                case AnimationEventMeaning.DodgeCancel: root.ConsumeAnimationMarker(signal.ActionId,signal.PhaseId,ActionMarkerKind.DodgeWindow);break;
            }
        }
        private Guid checkedPlaybackPhase;
        public void FlushSignals()
        {
            handle?.FlushSignals();
            if(root==null || root.IsSimulationSuspended || !root.DrivesActionFromEvents ||
                !bus.ActionActive || bus.ActionId!=capturedId || !bus.RetainsMainPose || checkedPlaybackPhase!=phaseId)return;
            var track=skeleton?.AnimationState?.GetTrack(0);
            if(track==null || !handle.IsCurrent(track,out var clip) ||
                clip.runtimeActionId!=capturedId || clip.runtimePhaseId!=phaseId)
                root.CancelOwnedMainAction(capturedId,"calibrated-playback-lost");
        }
        private CharacterAnimationPlan ResolveAction(Ossastra.Characters.ActionDefinitionSO definition,CharacterAnimationPreparation preparation)
        {
            if(definition==null||definition.animationLogicId==EAnimationLogicState.None||!Enum.IsDefined(typeof(EAnimationLogicState),definition.animationLogicId))return null;
            var source=preparation.MainHand.HasValue?(preparation.MainHand.Value?AnimationIntentSourceStateMachine.MainHand:AnimationIntentSourceStateMachine.OffHand):AnimationIntentSourceStateMachine.Skill;
            var index=preparation.Stage;
            if(index==0)index=definition.animationLogicId==EAnimationLogicState.Attack_Normal_1?1:definition.animationLogicId==EAnimationLogicState.Attack_Normal_2?2:definition.animationLogicId==EAnimationLogicState.Attack_Normal_3?3:definition.animationLogicId==EAnimationLogicState.Attack_Normal_4?4:0;
            var semantic=index==1?EAnimSemantic.Attack_Normal_1:index==2?EAnimSemantic.Attack_Normal_2:index==3?EAnimSemantic.Attack_Normal_3:index==4?EAnimSemantic.Attack_Normal_4:EAnimSemantic.Ability;
            return ResolveCandidate(definition.animationLogicId,semantic,source,preparation.MainHand,index,preparation.UseAuthoredLogic);
        }
        private CharacterAnimationPlan ResolveCandidate(EAnimationLogicState actionLogic,EAnimSemantic semantic,
            AnimationIntentSourceStateMachine source,bool? mainHand,int preparedStage=0,bool useAuthoredLogic=false)
        {
            var candidate=new AnimationIntent{root=root,actorId=root.ActorId,target="body",lifeGeneration=root.LifeGeneration,presentationGeneration=generation,
                intentSourceStateMatine=source,semantic=semantic,logicId=actionLogic,phaseId=Guid.NewGuid(),oneShot=true};
            var intents=composer.SnapshotIntents().ToDictionary(pair=>pair.Key,pair=>pair.Value);
            intents[source]=candidate;
            var equipment=root.GetComponent<Ossastra.Characters.CharacterEquipment>();
            var context=new AnimationComposeContext{root=root,dataBus=bus,target="body",activeIntents=intents,preparedStage=preparedStage,
                sourceEquipment=mainHand.HasValue?(mainHand.Value?equipment?.RightDefinition:equipment?.LeftDefinition):null,configurationVersion=config.ConfigurationVersion};
            // 普攻动作资产决定片段逻辑，连段位置继续供语义及装备／模型覆写判断使用。
            var logic = useAuthoredLogic ? actionLogic : config.RouteIntent(candidate, context);
            var plan = config.ResolvePlan(logic, context);
            if(plan!=null){plan.runtimeSource=source;plan.runtimeSemantic=semantic;}return plan;
        }
        public bool CanCaptureAction(Ossastra.Characters.ActionDefinitionSO definition,out string reason)
        {
            reason=null;if(config==null||skeleton?.Skeleton==null){reason="animation-not-initialized";return false;}
            var plan=ResolveAction(definition,new CharacterAnimationPreparation(true));if(plan==null||!plan.HasOperations){reason="missing-original-animation-plan:"+definition?.animationLogicId;return false;}
            return ValidatePlan(plan,out reason,"invalid-original-animation-clip:");
        }
        private static bool FinitePositive(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value)&&value>0;
        public ConfiguredActionSnapshot FreezeAction(Ossastra.Characters.ActionDefinitionSO definition)
            =>PrepareAnimation(definition,new CharacterAnimationPreparation(true));
        public ConfiguredActionSnapshot PrepareAnimation(Ossastra.Characters.ActionDefinitionSO definition,CharacterAnimationPreparation preparation)
        {
            if(definition==null)throw new ArgumentException("missing-action-definition");
            return FreezePlan(ResolveAction(definition,preparation),definition.FreezeTiming(handle.Profile),definition.definitionId);
        }
        private ConfiguredActionSnapshot FreezePlan(CharacterAnimationPlan original,BasicAttackDefinition timeline,string behavior=null)
        {
            if(!ValidatePlan(original,out var reason))throw new ArgumentException(reason);
            var plan=original.CloneFor(root);
            var profile=handle.Profile;
            var rules=profile?.bindings?.Where(b=>b!=null && behavior!=null && b.behavior==behavior).ToArray();
            if(profile!=null && profile.Drives(behavior))
            {
                if(rules==null || rules.Length==0)throw new ArgumentException("已迁移动作没有绑定："+behavior);
                if(!profile.MatchesSkeleton(skeleton.skeletonDataAsset))throw new ArgumentException("校准骨架失配："+behavior);
                var carrier=plan.clips.FirstOrDefault(c=>c.track==0);
                if(carrier==null)throw new ArgumentException("缺主轨："+behavior);
                var rows=rules.Where(b=>b.animation==carrier.animaName && b.track==carrier.track).ToArray();
                var animation=skeleton.Skeleton.Data.FindAnimation(carrier.animaName);
                if(rows.Length==0 || rows.Any(b=>!profile.Valid(b,animation)) ||
                    rows.GroupBy(b=>(b.eventName,b.occurrence,b.semantic)).Any(g=>g.Count()>1) ||
                    rows.Count(b=>b.semantic==AnimationEventMeaning.Finish)!=1 || !rows.Any(b=>b.semantic==AnimationEventMeaning.Release))
                    throw new ArgumentException("源事件或正式收尾失配："+behavior+" / "+carrier.animaName);
                float Effective(AnimationCalibrationBinding b)=>b.sourceTime+(float)b.offsetFrames/profile.framesPerSecond;
                var finish=rows.Single(b=>b.semantic==AnimationEventMeaning.Finish);
                if(rows.Any(b=>b.semantic!=AnimationEventMeaning.Finish && Effective(b)>Effective(finish)))
                    throw new ArgumentException("动作标记位于结束之后："+behavior);
                carrier.runtimeMarkers=rows.Select(b=>b.Copy()).ToArray();
                carrier.runtimeFps=profile.framesPerSecond;
                carrier.runtimeBehavior=behavior;
                timeline=timeline.AsEventDriven();
            }
            foreach(var clip in PlanClips(plan))
                if(clip.scalesWithAction && !timeline.EventDriven)
                {
                    var duration=clip.sourceDuration>0?clip.sourceDuration:skeleton.Skeleton.Data.FindAnimation(clip.animaName).Duration;
                    clip.playbackRate=duration/(float)timeline.FinishAt;
                }
            var primary=plan.clips.FirstOrDefault(c=>c.track==0);
            return new ConfiguredActionSnapshot(timeline,primary?.animaName,primary?.mixDuration??0,primary?.playbackRate??1,primary?.loop??false,()=>plan.CloneFor(root),replacement=>FreezePlan(original,replacement,behavior));
        }
        public void CaptureAction(Guid id,Ossastra.Characters.ActionDefinitionSO definition)=>CapturePhase(id,FreezeAction(definition));
        public void CaptureDefaultAction(Guid id,BasicAttackDefinition timeline,int index)
            =>CapturePhase(id,FreezeDefaultAction(timeline,index));
        public ConfiguredActionSnapshot FreezeDefaultAction(BasicAttackDefinition timeline,int index)
        {
            var logic=index==2?EAnimationLogicState.Attack_Normal_2:index==3?EAnimationLogicState.Attack_Normal_3:index==4?EAnimationLogicState.Attack_Normal_4:EAnimationLogicState.Attack_Normal_1;
            var plan=ResolveCandidate(logic,index==2?EAnimSemantic.Attack_Normal_2:index==3?EAnimSemantic.Attack_Normal_3:index==4?EAnimSemantic.Attack_Normal_4:EAnimSemantic.Attack_Normal_1,AnimationIntentSourceStateMachine.MainHand,true,index);
            if(plan==null)throw new ArgumentException("Default action mapping missing: "+logic);
            return FreezePlan(plan,timeline,logic.ToString());
        }
        public bool CanCapturePhase(ConfiguredActionSnapshot snapshot,out string reason)
            =>ValidatePlan(snapshot?.CopyPresentationPlan() as CharacterAnimationPlan,out reason);
        public bool HasPostureRecoveryEvent(ConfiguredActionSnapshot phase, string marker)
        {
            if (phase == null || string.IsNullOrWhiteSpace(marker)) return false;
            var plan = phase.CopyPresentationPlan() as CharacterAnimationPlan;
            var primary = plan?.clips.FirstOrDefault(clip => clip != null && clip.track == 0);
            if (primary == null) return false;
            var animation = skeleton?.Skeleton?.Data.FindAnimation(primary.animaName);
            return animation != null && animation.Timelines.OfType<Spine.EventTimeline>()
                .Any(timeline => timeline.Events.Any(node => node.Data.Name == marker));
        }

        private bool ValidatePlan(CharacterAnimationPlan plan,out string reason,string invalidClip = "invalid-frozen-animation-clip:")
        {
            reason=plan==null||!plan.HasOperations?"missing-frozen-animation-plan":null;
            if(reason==null)foreach(var clip in PlanClips(plan))
                if(clip==null||clip.track<0||skeleton?.Skeleton?.Data.FindAnimation(clip.animaName)==null||!FinitePositive(clip.playbackRate)||float.IsNaN(clip.mixDuration)||float.IsInfinity(clip.mixDuration)||clip.mixDuration<0)
                {reason=invalidClip+clip?.animaName;break;}
            return reason==null;
        }
        private static System.Collections.Generic.IEnumerable<CharacterAnimationClip> PlanClips(CharacterAnimationPlan plan)
            => plan.clips.Concat(plan.movingLoopClips ?? Array.Empty<CharacterAnimationClip>())
                .Concat(plan.queuedClips.Where(queue => queue != null).Select(queue => queue.clip));
        public ConfiguredActionSnapshot PrepareDodge(float duration)
        {
            var plan = ResolveCandidate(EAnimationLogicState.Dodge, EAnimSemantic.Dodge,
                AnimationIntentSourceStateMachine.Dodge, null);
            if (plan == null && root.UsesActionPosture && !string.IsNullOrEmpty(postureDodgeFallbackClip))
            {
                plan = new CharacterAnimationPlan
                {
                    characterRoot = root, runtimeSource = AnimationIntentSourceStateMachine.Dodge,
                    runtimeSemantic = EAnimSemantic.Dodge, runtimeLogicId = EAnimationLogicState.Dodge
                };
                plan.clips.Add(new CharacterAnimationClip
                {
                    animaName = postureDodgeFallbackClip, track = 0, playbackRate = 1, scalesWithAction = true,
                    characterRoot = root, skeletonDataAsset = skeleton.skeletonDataAsset
                });
            }
            return FreezePlan(plan, new BasicAttackDefinition(0, 0, 0, duration, 1));
        }
        public void CapturePhase(Guid id,ConfiguredActionSnapshot snapshot)
        {
            var plan=snapshot.CopyPresentationPlan() as CharacterAnimationPlan;
            if(plan==null)throw new ArgumentException("Frozen phase has no original animation plan");
            captured=plan;capturedId=id;phaseId=Guid.NewGuid();
        }
        public void CollectFacts(bool isAlive,bool isStaggered,bool reacting,int presentationGeneration,int attackStage=1)
        {alive=isAlive;staggered=isStaggered;hitReaction=reacting;stage=attackStage;if(generation!=presentationGeneration){generation=presentationGeneration;presentedJumpPhase=OrdinaryJumpPhase.Uninitialized;composer?.ClearAllIntents();}}
        public void SetPaused(bool paused){if(skeleton!=null)skeleton.timeScale=paused?0:timeScale;}
        private void Submit(AnimationIntentSourceStateMachine source,EAnimSemantic semantic,int priority,Guid action,Guid phase=default,CharacterAnimationPlan plan=null,bool exclusive=false,bool oneShot=false,
            EAnimationLogicState logic=EAnimationLogicState.None,int replayId=0)
        {
            composer.SubmitIntent(new AnimationIntent{root=root,actorId=root.ActorId,target="body",intentSourceStateMatine=source,semantic=semantic,
                priority=priority,layer=priority>=20?AnimationLayer.FullBodyAction:AnimationLayer.BaseFullBody,exclusiveFullBody=exclusive,oneShot=oneShot,
                actionId=action,phaseId=phase,logicId=plan?.runtimeLogicId??logic,replayId=replayId,lifeGeneration=root.LifeGeneration,presentationGeneration=generation,configurationVersion=config.ConfigurationVersion,frozenPlan=plan});
        }

        private void SubmitMovement()
        {
            // 同一个片段可作临时回退，仍提交有身份的墙边语义，避免零速度被识别为站立。
            var semantic = bus.PresentsLocomotion ? EAnimSemantic.Jog : EAnimSemantic.Idle;
            if (bus.PresentsLocomotion && bus.IsGrounded && bus.IsWalking && config.baseMap.plansMovement.Exists(
                e => e?.logicID == EAnimationLogicState.Walk && e.plan?.HasOperations == true))
                semantic = EAnimSemantic.Walk;
            var logic = EAnimationLogicState.None;
            if (config.sideViewTraversalMap != null)
            {
                switch (bus.TraversalMode)
                {
                    case SideViewTraversalMode.WallSlide:
                        semantic = EAnimSemantic.WallSlide; logic = EAnimationLogicState.WallSlide; break;
                    case SideViewTraversalMode.LedgeHang:
                        semantic = EAnimSemantic.LedgeHang; logic = EAnimationLogicState.LedgeHang; break;
                    case SideViewTraversalMode.LedgeClimb:
                        semantic = EAnimSemantic.LedgeClimb; logic = EAnimationLogicState.LedgeClimb; break;
                }
            }
            bool traversing = logic != EAnimationLogicState.None;
            Submit(AnimationIntentSourceStateMachine.Movement, semantic, traversing ? 12 : bus.PresentsLocomotion ? 10 : 0,
                Guid.Empty, logic:logic, replayId:traversing ? bus.TraversalRevision : 0);
            SubmitJump(traversing);
        }

        private void SubmitJump(bool traversing)
        {
            var previous = presentedJumpPhase;
            presentedJumpPhase = traversing || config.baseMap.plansJump == null || config.baseMap.plansJump.Count == 0
                ? OrdinaryJumpPhase.Uninitialized : bus.JumpPhase;
            EAnimSemantic semantic;
            switch (presentedJumpPhase)
            {
                case OrdinaryJumpPhase.Rising:
                    semantic = EAnimSemantic.Jump_Start;
                    break;
                case OrdinaryJumpPhase.Falling:
                    semantic = EAnimSemantic.Jump_Falling;
                    break;
                case OrdinaryJumpPhase.Grounded when previous == OrdinaryJumpPhase.Rising || previous == OrdinaryJumpPhase.Falling:
                    semantic = EAnimSemantic.Jump_Landing;
                    break;
                case OrdinaryJumpPhase.Grounded when composer.CurrentBodyIntent?.semantic == EAnimSemantic.Jump_Landing
                    && skeleton.AnimationState.GetTrack(0)?.IsComplete == false:
                    semantic = EAnimSemantic.Jump_Landing;
                    break;
                default:
                    composer.RemoveIntent(AnimationIntentSourceStateMachine.Jump);
                    return;
            }
            // 配置中的轨道队列负责起跳接空中姿势、下落起势接循环。碰撞落地可立即截断它们。
            Submit(AnimationIntentSourceStateMachine.Jump, semantic, 11, Guid.Empty, replayId:bus.JumpRevision);
        }
        public void Present()
        {
            if(bus==null||skeleton?.Skeleton==null)return;
            if(!alive||!root.IsAlive){ResetPosturePose();composer.ClearAllIntents();return;}
            SubmitMovement();
            var retain=bus.RetainsMainPose&&capturedId==bus.ActionId&&captured!=null;
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
            if(SubmitPostureReaction()) { }
            else if(staggered)Submit(AnimationIntentSourceStateMachine.Status,EAnimSemantic.Stagger,50,Guid.Empty,exclusive:true);
            else composer.RemoveIntent(AnimationIntentSourceStateMachine.Status);
            int wallSide = bus.TraversalWallSide;
            if(direction!=null&&direction.IsConfigured)
            {
                direction.SubmitDirection(composer,wallSide == 0 ? direction.ReadGameplayDirection() : Vector3.right * wallSide,generation);
                skeleton.Skeleton.ScaleX=scaleX*(direction.Mirrored?-1:1);
            }
            else skeleton.Skeleton.ScaleX=scaleX*(wallSide == 0 ? bus.ViewFacingX : wallSide);
            composer.Commit(generation);
            if(retain)checkedPlaybackPhase=phaseId;
            var selected=composer.CurrentBodyIntent;
            if(selected!=null){CurrentSemantic=AnimationSemanticTranslation.ToShared(selected.semantic);CurrentSource=selected.oneShot&&selected.actionId==bus.ActionId&&selected.priority==20?"action":selected.intentSourceStateMatine.ToString();CurrentReplayId=selected.phaseId!=Guid.Empty?selected.phaseId:selected.actionId;}
        }
        private bool SubmitPostureReaction()
        {
            var reaction = root.StateArbitrator?.Reaction;
            if (!root.UsesActionPosture || reaction?.IsActive != true)
            {
                ResetPosturePose();
                return false;
            }
            if (displayedReaction != reaction.Phase || displayedReactionId != reaction.ActionId)
            {
                displayedReaction = reaction.Phase;
                displayedReactionId = reaction.ActionId;
                reactionPlan = null;
                string clip = displayedReaction == PostureReactionPhase.Launched ? postureLaunchClip :
                    displayedReaction == PostureReactionPhase.Downed ? postureDownClip : postureRiseClip;
                if (!string.IsNullOrEmpty(clip) && skeleton.Skeleton.Data.FindAnimation(clip) is Spine.Animation animation)
                {
                    float speed = displayedReaction == PostureReactionPhase.GettingUp
                        ? animation.Duration / root.StateArbitrator.PostureRiseSeconds : 1;
                    reactionPlan = new CharacterAnimationPlan { characterRoot = root, runtimeSemantic = EAnimSemantic.Stagger };
                    reactionPlan.clips.Add(new CharacterAnimationClip
                    {
                        animaName = clip, track = 0, loop = false, playbackRate = speed, mixDuration = .04f,
                        characterRoot = root, skeletonDataAsset = skeleton.skeletonDataAsset
                    });
                }
            }
            if (reactionPlan == null && usePosturePoseFallback)
            {
                float rotation = displayedReaction == PostureReactionPhase.Downed ? 76 :
                    displayedReaction == PostureReactionPhase.GettingUp ? 32 : 22;
                skeleton.transform.localRotation = authoredSkeletonRotation *
                    Quaternion.Euler(0, 0, -bus.ViewFacingX * rotation);
                temporaryPosturePose = true;
            }
            Submit(AnimationIntentSourceStateMachine.Status, EAnimSemantic.Stagger, 50, reaction.ActionId,
                plan: reactionPlan, exclusive:true, replayId:(int)displayedReaction);
            return true;
        }

        private void ResetPosturePose()
        {
            if (temporaryPosturePose && skeleton != null) skeleton.transform.localRotation = authoredSkeletonRotation;
            temporaryPosturePose = false;
            displayedReaction = PostureReactionPhase.Inactive;
            displayedReactionId = Guid.Empty;
            reactionPlan = null;
        }

        private void OnDisable(){presentedJumpPhase=OrdinaryJumpPhase.Uninitialized;ResetPosturePose();composer?.ClearAllIntents();if(handle!=null)handle.Signal-=OnSignal;}
        private void OnEnable(){if(handle!=null){handle.Signal-=OnSignal;handle.Signal+=OnSignal;}}
        private void OnDestroy(){if(handle!=null)handle.Signal-=OnSignal;}
    }
}
