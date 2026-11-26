/* 冻结逐轨事件响应，按实际播放时间、循环和发生序位，派发单一逻辑源及各独立响应用途。 */
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
            internal bool NeedsSourceCallback => (Purpose == AnimationSignalPurpose.Marker || Purpose == AnimationSignalPurpose.Source) &&
                Binding.offsetFrames == 0;
            internal Node(AnimationCalibrationBinding binding, AnimationSignalPurpose purpose, float time)
            {
                Binding = binding;
                Purpose = purpose;
                Time = time;
            }
        }
        private readonly Node[] nodes;
        private int cursor,cycle;
        public bool HasFootstep {get;}
        private readonly Dictionary<(string name, int occurrence), int> collectedCycles =
            new Dictionary<(string, int), int>();
        private float previousTrackTime;
        private float recoveredThrough=-1;
        public bool Collect(Spine.Event marker, TrackEntry entry)
        {
            int ordinal = AnimationClipEventConfiguration.Occurrence(entry.Animation, marker);
            var key = (marker.Data.Name, ordinal);
            if (!collectedCycles.ContainsKey(key)) return false;

            float duration = entry.Animation.Duration;
            // 回调可在循环跨越后的同一帧到达，需由源时刻还原它所属循环。
            int sourceCycle = entry.Loop && duration > 0
                ? Math.Max(0, (int)Math.Floor((entry.TrackTime - marker.Time + 0.000001f) / duration))
                : 0;
            collectedCycles[key] = sourceCycle;
            return true;
        }
        public AnimationMarkerPlayback(AnimationCalibrationBinding[] bindings,int fps,float duration)
        {
            float Bounded(float time)=>Math.Min(duration,Math.Max(0f,time));
            var list=new List<Node>();
            var bySource = bindings.GroupBy(binding => (binding.eventName, binding.occurrence));
            foreach (var group in bySource)
            {
                collectedCycles.Add(group.Key, -1);
                var sourceBinding = group.OrderBy(binding => binding.offsetFrames).First();
                list.Add(new Node(sourceBinding, AnimationSignalPurpose.Source,
                    Bounded(sourceBinding.sourceTime + (float)sourceBinding.offsetFrames / fps)));

                foreach (var binding in group)
                {
                    if (binding.semantic == AnimationEventMeaning.Ignore) continue;
                    float time = binding.sourceTime + (float)binding.offsetFrames / fps;
                    list.Add(new Node(binding, AnimationSignalPurpose.Marker, Bounded(time)));
                    if (binding.sound)
                        list.Add(new Node(binding, AnimationSignalPurpose.Sound, Bounded(time + (float)binding.soundOffsetFrames / fps)));
                    if (binding.effect)
                        list.Add(new Node(binding, AnimationSignalPurpose.Effect, Bounded(time + (float)binding.effectOffsetFrames / fps)));
                }
            }
            // 同帧先处理源与其他响应，Finish 最后释放动作身份。
            nodes = list.OrderBy(node => node.Time).ThenBy(node => node.Purpose == AnimationSignalPurpose.Source ? 0 :
                node.Purpose == AnimationSignalPurpose.Marker && node.Binding.semantic == AnimationEventMeaning.Finish ? 2 : 1).ToArray();
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
                if (node.NeedsSourceCallback && absolute > recoveredThrough &&
                    collectedCycles[(node.Binding.eventName, node.Binding.occurrence)] < cycle) return;
                cursor++;
                emit(node,cycle);
            }
        }
    }
}
