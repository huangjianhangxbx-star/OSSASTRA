using System;
using System.Collections.Generic;
using System.Linq;
using Spine;
namespace Ossastra.CharacterAnimation
{
    // 绑定随 TrackEntry 冻结。每次 Flush 用实际 TrackTime 补齐本次跨过的所有标记。
    internal sealed class AnimationMarkerPlayback
    {
        internal readonly struct Node
        {
            internal readonly AnimationCalibrationBinding Binding;
            internal readonly AnimationSignalPurpose Purpose;
            internal readonly float Time;
            internal Node(AnimationCalibrationBinding binding, AnimationSignalPurpose purpose,float time)
            {Binding=binding;Purpose=purpose;Time=time;}
        }
        private readonly Node[] nodes;
        private int cursor,cycle;
        public bool HasFootstep {get;}
        private readonly Dictionary<AnimationCalibrationBinding,int> collectedCycles=new Dictionary<AnimationCalibrationBinding,int>();
        private float previousTrackTime;
        private float recoveredThrough=-1;
        public bool Collect(Spine.Event marker, TrackEntry entry)
        {
            int ordinal=AnimationCalibrationSO.Events(entry.Animation).Where(e=>e.Data.Name==marker.Data.Name)
                .TakeWhile(e=>!ReferenceEquals(e,marker)).Count();
            float duration=entry.Animation.Duration;
            int sourceCycle=entry.Loop && duration>0 ? Math.Max(0,(int)Math.Floor((entry.TrackTime-marker.Time+0.000001f)/duration)) : 0;
            bool owns=false;
            foreach(var node in nodes)
            {
                if(node.Purpose!=AnimationSignalPurpose.Marker || node.Binding.eventName!=marker.Data.Name ||
                    node.Binding.occurrence!=ordinal)continue;
                owns=true;
                if(node.Binding.offsetFrames==0)collectedCycles[node.Binding]=sourceCycle;
            }
            return owns;
        }
        public AnimationMarkerPlayback(AnimationCalibrationBinding[] bindings,int fps,float duration)
        {
            float Bounded(float time)=>Math.Min(duration,Math.Max(0f,time));
            var list=new List<Node>();
            foreach(var b in bindings)
            {
                float time=b.sourceTime+(float)b.offsetFrames/fps;
                list.Add(new Node(b,AnimationSignalPurpose.Marker,Bounded(time)));
                if(b.sound)list.Add(new Node(b,AnimationSignalPurpose.Sound,Bounded(time+(float)b.soundOffsetFrames/fps)));
                if(b.effect)list.Add(new Node(b,AnimationSignalPurpose.Effect,Bounded(time+(float)b.effectOffsetFrames/fps)));
            }
            nodes=list.OrderBy(n=>n.Time).ThenBy(n=>n.Purpose==AnimationSignalPurpose.Marker && n.Binding.semantic==AnimationEventMeaning.Finish ? 1:0).ToArray();
            HasFootstep=bindings.Any(b=>b.semantic==AnimationEventMeaning.Footstep);
        }
        public void Advance(TrackEntry entry,Action<Node,int> emit)
        {
            float duration=entry.Animation.Duration;
            // Spine may report only the last loop on a long update. Recover only an actual skipped full cycle.
            if(entry.Loop && duration>0 && entry.TrackTime-previousTrackTime>=duration)
                recoveredThrough=entry.TrackTime;
            previousTrackTime=entry.TrackTime;
            while(nodes.Length!=0)
            {
                if(cursor==nodes.Length)
                {
                    if(!entry.Loop || duration<=0)return;
                    cycle++;cursor=0;
                }
                var node=nodes[cursor];
                double absolute=(double)cycle*duration+node.Time;
                if(absolute>entry.TrackTime)return;
                if(node.Purpose==AnimationSignalPurpose.Marker && node.Binding.offsetFrames==0 &&
                   absolute>recoveredThrough && (!collectedCycles.TryGetValue(node.Binding,out var seen) || seen<cycle))return;
                cursor++;
                emit(node,cycle);
            }
        }
    }
}
