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
        public float Time;
    }
    public sealed class CharacterSpineEventSystem : IDisposable
    {
        private Spine.AnimationState state;
        private AnimationHandleSystem handle;
        private readonly Queue<(TrackEntry entry, AnimationSignal signal)> pending=new Queue<(TrackEntry,AnimationSignal)>();
        private readonly Dictionary<TrackEntry,Dictionary<string,int>> consumed=new Dictionary<TrackEntry,Dictionary<string,int>>();
        public event Action<AnimationSignal> Signal;
        public void Bind(AnimationHandleSystem owner,Spine.AnimationState next)
        {
            if(handle==owner&&ReferenceEquals(state,next))return;
            Dispose();handle=owner;state=next;if(state!=null)state.Event+=OnEvent;
        }
        private void OnEvent(TrackEntry entry,Spine.Event e)
        {
            if(handle==null||!handle.IsCurrent(entry,out var clip))return;
            var cycle=entry.Loop&&entry.Animation.Duration>0?(int)(entry.TrackTime/entry.Animation.Duration):0;
            var key=e.Data.Name+"|"+e.Time;
            if(!consumed.TryGetValue(entry,out var nodes)){nodes=new Dictionary<string,int>();consumed.Add(entry,nodes);}
            if(nodes.TryGetValue(key,out var previous)&&previous==cycle)return;
            nodes[key]=cycle;
            pending.Enqueue((entry,new AnimationSignal{ActorId=clip.runtimeActor,Target=clip.runtimeTarget,ActionId=clip.runtimeActionId,PhaseId=clip.runtimePhaseId,Generation=clip.runtimeGeneration,LifeGeneration=clip.runtimeLifeGeneration,Source=clip.runtimeSourceStateMachine,ReplayId=clip.runtimeReplayId,Track=entry.TrackIndex,Name=e.Data.Name,Time=e.Time}));
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
