/*
 * 脚本：CharacterSpineView
 * 功能：Root运动后采集只读语义，经公共仲裁和配置映射，单次提交Spine主轨；合法恢复移动后让出攻击姿势。
 * 边界：不定义战斗时钟、不监听Complete扣血；死亡仍由Vitals立即隐藏。
 * 协作：旧idle/move/attack序列化引用保留；SO配置与接受时冻结方案优先；catalog和旧三片段仅为旧资产兼容。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.AnimationCore;
using Ossastra.CharacterCore;
using SemanticIntent = Ossastra.AnimationCore.AnimationIntent;
using PlaybackIdentity = Ossastra.AnimationCore.AnimationPlaybackIdentity;
using Ossastra.AnimationPresentation;
using Spine.Unity;
using UnityEngine;
namespace Ossastra.Characters
{
    public sealed class CharacterSpineView : MonoBehaviour, ICharacterPresentation, ICharacterAnimationFacts, ICharacterConfiguredAnimation
    {
        [SerializeField] private SkeletonAnimation skeleton;
        [SerializeField] private CharacterAnimationProfileSO profile;
        private Guid capturedAction;
        private string capturedClip;
        private float capturedMix,capturedRate=1;
        private bool capturedLoop;
        private Guid phaseReplay;
        private Ossastra.Combat.ICombatGuard[] guards=Array.Empty<Ossastra.Combat.ICombatGuard>();
        [SerializeField] private string idle="idle1_1", move="run", attack="atk1";
        [SerializeField] private AnimationClipMap[] catalog=Array.Empty<AnimationClipMap>();
        private CharacterDataBus dataBus;
        private AnimationResolvePolicy policy;
        private readonly PlaybackIdentity identity=new PlaybackIdentity();
        private readonly List<SemanticIntent> intents=new List<SemanticIntent>(6);
        private float originalScaleX, originalTimeScale;
        private bool alive=true, staggered, hitReaction;
        private int generation, attackStage;
        public string CurrentClip { get; private set; }
        public AnimationSemantic CurrentSemantic { get; private set; }
        public string CurrentSource { get; private set; }
        public string FallbackReason { get; private set; }
        public Guid CurrentReplayId { get; private set; }
        public int CommitCount { get; private set; }
        public bool Initialize(CharacterDataBus dataBus)
        {
            if(dataBus==null)return false;this.dataBus=dataBus;policy=new AnimationResolvePolicy(dataBus.ActorId);
            if(skeleton==null)skeleton=GetComponent<SkeletonAnimation>();
            if(skeleton==null||skeleton.skeletonDataAsset==null)return false;
            skeleton.Initialize(false);var data=skeleton.skeletonDataAsset.GetSkeletonData(false);
            if(data==null||skeleton.Skeleton==null)return false;
            var root=GetComponentInParent<CharacterRoot>();
            if(profile==null&&root!=null&&root.Definition!=null)profile=root.Definition.animationProfile;
            if(profile!=null)
            {
                foreach(var binding in profile.bindings)
                    if(binding==null||string.IsNullOrEmpty(binding.clip)||data.FindAnimation(binding.clip)==null)
                    {Debug.LogError("Animation profile contains missing clip",this);return false;}
                var idleBinding=profile.FindSemantic(AnimationSemantic.Idle);
                var moveBinding=profile.FindSemantic(AnimationSemantic.Move);
                var attackBinding=profile.FindSemantic(AnimationSemantic.BasicAttack);
                if(idleBinding==null||moveBinding==null||attackBinding==null)return false;
                idle=idleBinding.clip;move=moveBinding.clip;attack=attackBinding.clip;
                if(root!=null&&root.Definition!=null)
                    foreach(var action in root.Definition.basicAttacks)
                        if(action==null||profile.FindKey(action.animationKey)==null)return false;
            }
            if(data.FindAnimation(idle)==null||data.FindAnimation(move)==null||data.FindAnimation(attack)==null)return false;
            if(root!=null)guards=root.GetComponents<MonoBehaviour>().OfType<Ossastra.Combat.ICombatGuard>().ToArray();
            originalScaleX=Mathf.Abs(skeleton.Skeleton.ScaleX);originalTimeScale=skeleton.timeScale;return true;
        }
        public bool CanCaptureAction(ActionDefinitionSO definition,out string reason)
        {
            var binding=profile==null||definition==null?null:profile.FindKey(definition.animationKey);
            reason=binding==null||skeleton==null||skeleton.Skeleton?.Data.FindAnimation(binding.clip)==null?"missing-animation-mapping":
                float.IsNaN(binding.sourceDuration)||float.IsInfinity(binding.sourceDuration)||binding.sourceDuration<=0?"invalid-animation-duration":null;
            return reason==null;
        }        /// <summary>接受时冻结片段/速率；SO后续变化不改写本次正在执行的播放方案。</summary>
        public void CaptureAction(Guid actionId,ActionDefinitionSO definition)
        {
            CapturePhase(actionId,FreezeAction(definition));
        }
        public ConfiguredActionSnapshot FreezeAction(ActionDefinitionSO definition)
        {
            if(!CanCaptureAction(definition,out var reason))throw new ArgumentException(reason);
            var binding=profile.FindKey(definition.animationKey);
            var timeline=definition.Freeze();
            return new ConfiguredActionSnapshot(timeline,binding.clip,binding.mixDuration,binding.sourceDuration/(float)timeline.FinishAt,binding.loop);
        }
        public void CapturePhase(Guid actionId,ConfiguredActionSnapshot phase)
        {
            capturedAction=actionId;capturedClip=phase.Clip;capturedMix=phase.Mix;capturedRate=phase.Rate;capturedLoop=phase.Loop;
            // 表现重播身份与战斗支付身份分离；同片段的下一阶段也须真实重播。
            phaseReplay=Guid.NewGuid();
        }
        public void SetPaused(bool paused){if(skeleton!=null)skeleton.timeScale=paused?0:originalTimeScale;}
        public void CollectFacts(bool alive,bool staggered,bool hitReaction,int generation,int attackStage=1)
        {this.alive=alive;this.staggered=staggered;this.hitReaction=hitReaction;this.generation=generation;this.attackStage=attackStage;}
        private void Add(AnimationSemantic semantic,string source,Guid replay)
        {intents.Add(new SemanticIntent(dataBus.ActorId,semantic,source,replay,generation));}
        public void Present()
        {
            if(dataBus==null||skeleton==null||skeleton.Skeleton==null||skeleton.AnimationState==null)return;
            // 每帧重新采集，不保留过期请求；Root本帧事实是唯一资格来源。
            intents.Clear();Add(AnimationSemantic.Idle,"movement",Guid.Empty);
            if(dataBus.IsMoving)Add(AnimationSemantic.Move,"movement",Guid.Empty);
            // 恢复门与呈现占用分离；跑步交接不取消主动作或提前开放AttackReady。
            if(ActionPresentationPolicy.RetainMainPose(dataBus.ActionActive,dataBus.MoveReady,dataBus.IsMoving)||dataBus.AbilityMotionActive)Add(attackStage==2?AnimationSemantic.BasicAttackSecond:AnimationSemantic.BasicAttack,"action",dataBus.ActionId);
            var motion=dataBus.MotionOverride as ICharacterMotionAnimation;
            if(motion!=null&&motion.IsActive)Add(AnimationSemantic.Dodge,"dodge",motion.ActionId);
            if(guards.Any(g=>g.IsGuarding))Add(AnimationSemantic.Guard,"guard",Guid.Empty);
            if(hitReaction)Add(AnimationSemantic.HitReaction,"vitals",Guid.Empty);
            if(staggered)Add(AnimationSemantic.PostureBroken,"posture",Guid.Empty);
            if(!alive)Add(AnimationSemantic.Death,"life",Guid.Empty);
            var selected=policy.Resolve(intents,generation);if(!selected.HasValue)return;
            var intent=selected.Value;var clip=idle;var loop=true;var mix=.08f;var rate=1f;FallbackReason="";
            // 旧三片段为兼容默认映射；Inspector catalog可覆盖全部语义，无需改战斗代码。
            if(intent.Semantic==AnimationSemantic.Move)clip=move;
            else if(intent.Semantic==AnimationSemantic.BasicAttack || intent.Semantic==AnimationSemantic.BasicAttackSecond){clip=attack;loop=false;if(intent.Semantic==AnimationSemantic.BasicAttackSecond)FallbackReason="Stage2无独立片段，复用atk1并按新ActionId重播";}
            else if(intent.Semantic!=AnimationSemantic.Idle)FallbackReason="缺少 "+intent.Semantic+" 映射，回退Idle（玩法事实不变）";
            foreach(var entry in catalog??Array.Empty<AnimationClipMap>())
                if(entry!=null&&entry.semantic==intent.Semantic){clip=entry.clip;loop=entry.loop;mix=Mathf.Max(0,entry.mixDuration);FallbackReason="";break;}
            if(profile!=null)
            {
                var binding=profile.FindSemantic(intent.Semantic);
                if(binding!=null){clip=binding.clip;loop=binding.loop;mix=binding.mixDuration;FallbackReason="";}
                if(intent.Source=="action"&&capturedAction==intent.ReplayId&&capturedClip!=null)
                {clip=capturedClip;loop=capturedLoop;mix=capturedMix;rate=capturedRate;FallbackReason="";intent=new SemanticIntent(intent.ActorId,intent.Semantic,intent.Source,phaseReplay,generation);}
                else if(intent.Semantic==AnimationSemantic.Dodge&&binding!=null&&motion!=null)
                    rate=binding.sourceDuration/motion.Duration;
            }            if(string.IsNullOrEmpty(clip)||skeleton.Skeleton.Data.FindAnimation(clip)==null)
            {FallbackReason="片段缺失："+clip+"，回退Idle";clip=idle;loop=true;}
            skeleton.Skeleton.ScaleX=originalScaleX*dataBus.VisualFacingX;
            if(identity.ShouldCommit(clip,intent))
            {var track=skeleton.AnimationState.SetAnimation(0,clip,loop);track.MixDuration=mix;track.TimeScale=rate;CommitCount++;}
            CurrentClip=clip;CurrentSemantic=intent.Semantic;CurrentSource=intent.Source;CurrentReplayId=intent.ReplayId;
        }
    }
}
