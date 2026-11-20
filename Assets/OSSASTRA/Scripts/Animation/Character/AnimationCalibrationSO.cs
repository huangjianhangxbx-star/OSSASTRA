using System;
using System.Collections.Generic;
using System.Linq;
using Spine;
using Spine.Unity;
using UnityEngine;
namespace Ossastra.CharacterAnimation
{
    public enum AnimationEventMeaning { Release, Finish, DodgeCancel, Footstep }
    public enum AnimationSignalPurpose { Marker, Sound, Effect }
    // 一项代表某个真实片段中一次同名事件。标记修正与音画补偿分别使用制作帧。
    [Serializable] public sealed class AnimationCalibrationBinding
    {
        public string behavior, animation, eventName, previousEvent, nextEvent, sourceString, anchor;
        public int track, occurrence, sourceInt, sourceCount, offsetFrames, soundOffsetFrames, effectOffsetFrames;
        public float sourceTime, sourceFloat, volume=1, scale=1, lifetime=1, rotation;
        public bool follow;
        public Vector3 localOffset;
        public AnimationEventMeaning semantic;
        public AudioClip sound;
        public GameObject effect;
        public bool IsSlotAnchor=>anchor!=null && anchor.StartsWith("slot:",StringComparison.Ordinal);
        public string AnchorName=>IsSlotAnchor?anchor.Substring(5):anchor;
        public Bone ResolveAnchor(Skeleton model)=>string.IsNullOrEmpty(anchor) || model==null ? null :
            IsSlotAnchor ? model.FindSlot(AnchorName)?.Bone : model.FindBone(anchor);
        public AnimationCalibrationBinding Copy() => (AnimationCalibrationBinding)MemberwiseClone();
    }

    [CreateAssetMenu(menuName="OSSASTRA/动画/逐动画事件校准")]
    public sealed class AnimationCalibrationSO : ScriptableObject
    {
        public SkeletonDataAsset skeleton;
        public bool MatchesSkeleton(SkeletonDataAsset actual)
        {
            return actual!=null && skeleton!=null && (skeleton==actual ||
                (skeleton.skeletonJSON!=null && skeleton.skeletonJSON==actual.skeletonJSON));
        }
        public string[] migratedBehaviors=Array.Empty<string>();
        public bool Drives(string behavior) => !string.IsNullOrEmpty(behavior) &&
            ((migratedBehaviors!=null && Array.IndexOf(migratedBehaviors,behavior)>=0) ||
             (bindings!=null && bindings.Any(b=>b!=null && b.behavior==behavior && b.semantic!=AnimationEventMeaning.Footstep)));
        [Min(1)] public int framesPerSecond=30;
        public AnimationCalibrationBinding[] bindings=Array.Empty<AnimationCalibrationBinding>();
        public static IEnumerable<Spine.Event> Events(Spine.Animation clip)
            => clip==null ? Enumerable.Empty<Spine.Event>() : clip.Timelines.OfType<EventTimeline>().SelectMany(t=>t.Events).OrderBy(e=>e.Time);
        public bool SourceMatches(AnimationCalibrationBinding b, Spine.Animation clip)
        {
            if(b==null || clip==null || clip.Name!=b.animation) return false;
            var all=Events(clip).ToArray();
            var names=all.Where(e=>e.Data.Name==b.eventName).ToArray();
            if(b.occurrence<0 || b.occurrence>=names.Length || b.sourceCount!=names.Length) return false;
            var e=names[b.occurrence];
            if(Mathf.Abs(e.Time-b.sourceTime)>.0001f || e.Int!=b.sourceInt || e.Float!=b.sourceFloat ||
                (e.String??"")!=(b.sourceString??"")) return false;
            int idx=Array.IndexOf(all,e);
            if((b.previousEvent!=null && b.previousEvent!=(idx==0?"":all[idx-1].Data.Name)) ||
               (b.nextEvent!=null && b.nextEvent!=(idx+1==all.Length?"":all[idx+1].Data.Name)))return false;
            return true;
        }
        public bool Valid(AnimationCalibrationBinding b, Spine.Animation clip)
        {
            if(framesPerSecond<=0 || !SourceMatches(b,clip))return false;
            if(!string.IsNullOrEmpty(b.anchor) && (b.effect||b.sound))
            {
                var data=skeleton?.GetSkeletonData(true);
                if(data==null || (b.IsSlotAnchor?data.FindSlot(b.AnchorName)==null:data.FindBone(b.anchor)==null))return false;
            }
            float mark=b.sourceTime+(float)b.offsetFrames/framesPerSecond;
            bool Inside(float t)=>t>=-0.000001f && t<=clip.Duration+0.000001f;
            return Inside(mark) && (!b.sound || Inside(mark+(float)b.soundOffsetFrames/framesPerSecond))
                && (!b.effect || Inside(mark+(float)b.effectOffsetFrames/framesPerSecond));
        }
    }
}
