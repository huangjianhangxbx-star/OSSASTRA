/* 中立Spine播放口：只消费语义、身份、代际和朝向；不引用角色根、输入、HP或动作运行时。
 * 单轨按ActionId去重，映射不存在则明确Idle回退；暂停仅控制自身播放速度。
 */
using System;
using Ossastra.AnimationCore;
using SemanticIntent=Ossastra.AnimationCore.AnimationIntent;
using PlaybackIdentity=Ossastra.AnimationCore.AnimationPlaybackIdentity;
using UnityEngine;
using Spine.Unity;
namespace Ossastra.AnimationPresentation
{
    public interface IEntityPresentation { void Present(); }
    [DisallowMultipleComponent]
    public sealed class EntitySpinePresenter : MonoBehaviour
    {
        [SerializeField] private SkeletonAnimation skeleton;
        [SerializeField] private string idle="idle1_1";
        [SerializeField] private AnimationClipMap[] catalog=Array.Empty<AnimationClipMap>();
        private readonly PlaybackIdentity identity=new PlaybackIdentity();
        private bool initialized;
        private float originalScale=1,originalSpeed=1;
        public string CurrentClip {get;private set;}
        public string FallbackReason {get;private set;}
        public AnimationSemantic Semantic {get;private set;}
        public int CommitCount {get;private set;}
        public void Present(SemanticIntent intent,float facing,bool paused)
        {
            if(!isActiveAndEnabled || skeleton==null || skeleton.skeletonDataAsset==null)return;
            if(!initialized)
            {
                skeleton.Initialize(false);if(skeleton.Skeleton==null)return;
                originalScale=Mathf.Abs(skeleton.Skeleton.ScaleX);originalSpeed=skeleton.timeScale;initialized=true;
            }
            AnimationClipMap chosen=null;
            foreach(var entry in catalog)if(entry!=null&&entry.semantic==intent.Semantic){chosen=entry;break;}
            var requested=chosen==null?(intent.Semantic==AnimationSemantic.Idle?idle:null):chosen.clip;
            var clip=ClipFallback.Resolve(requested,idle,s=>skeleton.Skeleton.Data.FindAnimation(s)!=null);
            FallbackReason=clip==null?"无可用片段":clip!=requested?"缺少 "+intent.Semantic+"，回退Idle":"";
            if(clip==null)return;
            skeleton.timeScale=paused?0:originalSpeed;
            skeleton.Skeleton.ScaleX=originalScale*(facing<0?-1:1);
            if(identity.ShouldCommit(clip,intent))
            {var track=skeleton.AnimationState.SetAnimation(0,clip,chosen==null||clip!=requested||chosen.loop);track.MixDuration=chosen==null?.08f:chosen.mixDuration;CommitCount++;}
            CurrentClip=clip;Semantic=intent.Semantic;
        }
        private void OnDisable(){if(skeleton!=null&&initialized)skeleton.timeScale=originalSpeed;}
    }
}
