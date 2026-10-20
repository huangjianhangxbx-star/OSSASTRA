/*
 * 脚本：CharacterSpineView
 * 功能：Root运动后采集只读语义，经公共仲裁和配置映射，单次提交Spine主轨。
 * 边界：不定义战斗时钟、不监听Complete扣血；死亡仍由Vitals立即隐藏。
 * 协作：旧idle/move/attack序列化引用保留；可选catalog覆盖，缺片段明确回退。
 */
using System;
using System.Collections.Generic;
using Ossastra.AnimationCore;
using SemanticIntent = Ossastra.AnimationCore.AnimationIntent;
using PlaybackIdentity = Ossastra.AnimationCore.AnimationPlaybackIdentity;
using Ossastra.AnimationPresentation;
using Spine.Unity;
using UnityEngine;
namespace Ossastra.Characters
{
    public sealed class CharacterSpineView : MonoBehaviour, ICharacterPresentation, ICharacterAnimationFacts
    {
        [SerializeField] private SkeletonAnimation skeleton;
        [SerializeField] private string idle="idle1_1", move="run", attack="atk1";
        [SerializeField] private AnimationClipMap[] catalog=Array.Empty<AnimationClipMap>();
        private CharacterDataBus dataBus;
        private AnimationResolvePolicy policy;
        private readonly PlaybackIdentity identity=new PlaybackIdentity();
        private readonly List<SemanticIntent> intents=new List<SemanticIntent>(6);
        private float originalScaleX, originalTimeScale;
        private bool alive=true, staggered, hitReaction;
        private int generation;
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
            if(data==null||data.FindAnimation(idle)==null||data.FindAnimation(move)==null||data.FindAnimation(attack)==null||skeleton.Skeleton==null)return false;
            originalScaleX=Mathf.Abs(skeleton.Skeleton.ScaleX);originalTimeScale=skeleton.timeScale;return true;
        }
        public void SetPaused(bool paused){if(skeleton!=null)skeleton.timeScale=paused?0:originalTimeScale;}
        public void CollectFacts(bool alive,bool staggered,bool hitReaction,int generation)
        {this.alive=alive;this.staggered=staggered;this.hitReaction=hitReaction;this.generation=generation;}
        private void Add(AnimationSemantic semantic,string source,Guid replay)
        {intents.Add(new SemanticIntent(dataBus.ActorId,semantic,source,replay,generation));}
        public void Present()
        {
            if(dataBus==null||skeleton==null||skeleton.Skeleton==null||skeleton.AnimationState==null)return;
            // 每帧重新采集，不保留过期请求；Root本帧事实是唯一资格来源。
            intents.Clear();Add(AnimationSemantic.Idle,"movement",Guid.Empty);
            if(dataBus.IsMoving)Add(AnimationSemantic.Move,"movement",Guid.Empty);
            if(dataBus.ActionActive)Add(AnimationSemantic.BasicAttack,"action",dataBus.ActionId);
            if(hitReaction)Add(AnimationSemantic.HitReaction,"vitals",Guid.Empty);
            if(staggered)Add(AnimationSemantic.PostureBroken,"posture",Guid.Empty);
            if(!alive)Add(AnimationSemantic.Death,"life",Guid.Empty);
            var selected=policy.Resolve(intents,generation);if(!selected.HasValue)return;
            var intent=selected.Value;var clip=idle;var loop=true;var mix=.08f;FallbackReason="";
            // 旧三片段为兼容默认映射；Inspector catalog可覆盖全部语义，无需改战斗代码。
            if(intent.Semantic==AnimationSemantic.Move)clip=move;
            else if(intent.Semantic==AnimationSemantic.BasicAttack){clip=attack;loop=false;}
            else if(intent.Semantic!=AnimationSemantic.Idle)FallbackReason="缺少 "+intent.Semantic+" 映射，回退Idle（玩法事实不变）";
            foreach(var entry in catalog??Array.Empty<AnimationClipMap>())
                if(entry!=null&&entry.semantic==intent.Semantic){clip=entry.clip;loop=entry.loop;mix=Mathf.Max(0,entry.mixDuration);FallbackReason="";break;}
            if(string.IsNullOrEmpty(clip)||skeleton.Skeleton.Data.FindAnimation(clip)==null)
            {FallbackReason="片段缺失："+clip+"，回退Idle";clip=idle;loop=true;}
            skeleton.Skeleton.ScaleX=originalScaleX*dataBus.VisualFacingX;
            if(identity.ShouldCommit(clip,intent))
            {var track=skeleton.AnimationState.SetAnimation(0,clip,loop);track.MixDuration=mix;CommitCount++;}
            CurrentClip=clip;CurrentSemantic=intent.Semantic;CurrentSource=intent.Source;CurrentReplayId=intent.ReplayId;
        }
    }
}
