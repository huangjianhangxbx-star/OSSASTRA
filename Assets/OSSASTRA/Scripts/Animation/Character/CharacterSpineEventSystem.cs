/* Spine信号只读转发：核对真实TrackEntry和完整播放身份；回调中只排队，显式Flush后交表现订阅者。 */
using System;
using System.Collections.Generic;
using Spine;
namespace Ossastra.CharacterAnimation
{
    public sealed class AnimationSignal
    {
        public string ActorId, Target, Name;
        public Guid ActionId, PhaseId;
        public int Generation, LifeGeneration, Track, ReplayId;
        public AnimationIntentSourceStateMachine Source;
        public float Time, EffectiveTime, CollectedTime, ConsumedTime;
        public string Animation;
        public int Cycle,Occurrence;
        public long Sequence;
        public bool Calibrated;
        public bool MappingConfigured;
        public bool CanDriveActionTiming;
        public AnimationEventMeaning Meaning;
        public AnimationSignalPurpose Purpose;
        public AnimationCalibrationBinding Binding;
    }
    public sealed class CharacterSpineEventSystem : IDisposable
    {
        private Spine.AnimationState state;
        private AnimationHandleSystem handle;
        private readonly Queue<(TrackEntry entry, AnimationSignal signal)> pending=new Queue<(TrackEntry,AnimationSignal)>();
        private readonly Dictionary<TrackEntry, Dictionary<Spine.Event, int>> consumed =
            new Dictionary<TrackEntry, Dictionary<Spine.Event, int>>();
        public event Action<AnimationSignal> Signal;
        public void Bind(AnimationHandleSystem owner,Spine.AnimationState next)
        {
            if(handle==owner&&ReferenceEquals(state,next))return;
            Dispose();handle=owner;state=next;if(state!=null)state.Event+=OnEvent;
        }
        private void OnEvent(TrackEntry entry,Spine.Event e)
        {
            if(handle==null||!handle.IsCurrent(entry,out var clip)||handle.CollectMarker(entry,e))return;
            float duration = entry.Animation.Duration;
            var cycle = entry.Loop && duration > 0 ? Math.Max(0, (int)Math.Floor((entry.TrackTime - e.Time + 0.000001f) / duration)) : 0;
            if (!consumed.TryGetValue(entry, out var nodes))
            {
                nodes = new Dictionary<Spine.Event, int>();
                consumed.Add(entry, nodes);
            }
            // 同时同名的两次 Spine 事件仍有不同源对象，不折叠成一次。
            if (nodes.TryGetValue(e, out var previous) && previous == cycle) return;
            nodes[e] = cycle;
            pending.Enqueue((entry, new AnimationSignal
            {
                ActorId = clip.runtimeActor, Target = clip.runtimeTarget, ActionId = clip.runtimeActionId,
                PhaseId = clip.runtimePhaseId, Generation = clip.runtimeGeneration, LifeGeneration = clip.runtimeLifeGeneration,
                Source = clip.runtimeSourceStateMachine, ReplayId = clip.runtimeReplayId, Track = entry.TrackIndex,
                Name = e.Data.Name, Time = e.Time, Animation = entry.Animation.Name, EffectiveTime = e.Time, Cycle = cycle,
                Occurrence = AnimationClipEventConfiguration.Occurrence(entry.Animation, e),
                MappingConfigured = clip.runtimeMappingConfigured
            }));
        }
        public void Flush()
        {
            int count=pending.Count;
            while(count-->0){var item=pending.Dequeue();if(handle!=null&&handle.IsCurrent(item.entry,out var clip)&&clip.runtimeActionId==item.signal.ActionId&&clip.runtimePhaseId==item.signal.PhaseId
                &&clip.runtimeGeneration==item.signal.Generation&&clip.runtimeLifeGeneration==item.signal.LifeGeneration&&clip.runtimeSourceStateMachine==item.signal.Source&&clip.runtimeReplayId==item.signal.ReplayId)Signal?.Invoke(item.signal);}
        }
        public void Forget(TrackEntry entry){consumed.Remove(entry);}
        public void Clear(){pending.Clear();consumed.Clear();}
        public void Dispose(){if(state!=null)state.Event-=OnEvent;state=null;handle=null;Clear();}
    }
}
