/* 迁自原Handle的逐轨播放/排队/去重职责；本地派发替代全局EventCenter。
 * 一个目标一个实例。只写计划，不Update Skeleton；合法结构轨离散清旧链，不影响身体动作身份或时钟。
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Spine;
using Spine.Unity;
using UnityEngine;
namespace Ossastra.CharacterAnimation
{
    [DisallowMultipleComponent]
    public sealed class AnimationHandleSystem : MonoBehaviour
    {
        private SkeletonAnimation skeletonAnimation;
        private Spine.AnimationState state;
        private Ossastra.Characters.CharacterRoot owner;
        private string target;
        private bool boundToActor;
        private readonly Dictionary<int,string> trackCommands = new Dictionary<int,string>();
        private readonly Dictionary<TrackEntry,CharacterAnimationClip> entries = new Dictionary<TrackEntry,CharacterAnimationClip>();
        private readonly HashSet<string> diagnostics = new HashSet<string>();
        private CharacterSpineEventSystem signals;
        public CharacterAnimationPlan currentPlan { get; private set; }
        public int CommitCount { get; private set; }
        public int QueueCount { get; private set; }
        public int PresentationGeneration { get; private set; }
        public string LastResult { get; private set; } = "未绑定";
        public SkeletonAnimation Skeleton => skeletonAnimation;
        public event Action<AnimationSignal> Signal;
        public bool Bind(Ossastra.Characters.CharacterRoot root, SkeletonAnimation skeleton, string targetId)
        {
            if (skeleton == null || skeleton.skeletonDataAsset == null) return false;
            skeleton.Initialize(false);
            if (skeleton.AnimationState == null) return false;
            if (owner == root && skeletonAnimation == skeleton && ReferenceEquals(state,skeleton.AnimationState) && target == targetId)
            { signals?.Bind(this,state); return true; }
            ClearAll(); ResetBinding(); owner=root; boundToActor=!ReferenceEquals(root,null); skeletonAnimation=skeleton; target=targetId; state=skeleton.AnimationState;
            signals=new CharacterSpineEventSystem(); signals.Signal += OnSignal; signals.Bind(this,state);
            LastResult="已绑定 "+targetId; return true;
        }
        private void OnSignal(AnimationSignal signal) => Signal?.Invoke(signal);
        public void FlushSignals() => signals?.Flush();
        public bool IsCurrent(TrackEntry entry, out CharacterAnimationClip clip)
        {
            clip=null;
            return isActiveAndEnabled && state != null && entry != null && entries.TryGetValue(entry,out clip)
                && ReferenceEquals(state.GetTrack(entry.TrackIndex),entry)
                && clip.runtimeTarget==target && clip.runtimeGeneration==PresentationGeneration
                && (!boundToActor || owner!=null && owner.IsAlive && owner.isActiveAndEnabled && owner.ActorId==clip.runtimeActor && owner.LifeGeneration==clip.runtimeLifeGeneration);
        }
        private bool ValidClip(CharacterAnimationClip clip)
        {
            if (clip==null || clip.track<0 || string.IsNullOrEmpty(clip.animaName)) return false;
            if(boundToActor&&(owner==null||clip.runtimeActor!=owner.ActorId||clip.runtimeLifeGeneration!=owner.LifeGeneration||clip.runtimeTarget!=target))return Reject("失效的角色/生命/目标播放身份");
            if (skeletonAnimation.Skeleton.Data.FindAnimation(clip.animaName)==null) return Reject("缺少片段："+clip.animaName);
            if (float.IsNaN(clip.playbackRate)||float.IsInfinity(clip.playbackRate)||clip.playbackRate<=0||float.IsNaN(clip.mixDuration)||float.IsInfinity(clip.mixDuration)||clip.mixDuration<0)
                return Reject("片段速率/混合非法："+clip.animaName);
            return true;
        }
        private bool Reject(string message)
        { LastResult=message; if(diagnostics.Add(message))Debug.LogWarning("[AnimationHandle] "+message,this); return false; }
        private static string Identity(CharacterAnimationClip c)
            => c.runtimeActor+"|"+c.runtimeTarget+"|"+c.runtimeSourceStateMachine+"|"+c.runtimeActionId+"|"+c.runtimePhaseId+"|"+c.runtimeReplayId+"|"+c.runtimeGeneration+"|"+c.runtimeLifeGeneration;
        private static void Append(StringBuilder b, CharacterAnimationClip c)
        {
            b.Append(Identity(c)).Append('|').Append(c.animaName).Append('|').Append(c.loop).Append('|').Append(c.isAdditive)
                .Append('|').Append(c.runtimeStructural)
                .Append('|').Append(c.playbackRate.ToString("R",CultureInfo.InvariantCulture)).Append('|').Append(c.mixDuration.ToString("R",CultureInfo.InvariantCulture));
        }
        public void ApplyPlan(CharacterAnimationPlan plan,int generation)
        {
            if (!isActiveAndEnabled || skeletonAnimation==null || !Bind(owner,skeletonAnimation,target)) return;
            PresentationGeneration=generation; currentPlan=plan;
            if(plan==null){ClearAll();return;}
            var plays=new Dictionary<int,CharacterAnimationClip>();
            foreach(var clip in plan.clips) if(ValidClip(clip)&&!plays.ContainsKey(clip.track))plays.Add(clip.track,clip);
            var queued=plan.queuedClips.Where(q=>q!=null&&ValidClip(q.clip)).GroupBy(q=>q.clip.track).ToDictionary(g=>g.Key,g=>g.ToList());
            var used=new HashSet<int>(plays.Keys); used.UnionWith(queued.Keys);
            foreach(var stale in trackCommands.Keys.Where(t=>!used.Contains(t)).ToArray()) ClearTrack(stale);
            foreach(var track in plan.clearTracks) if(track>=0&&!plays.ContainsKey(track)&&!queued.ContainsKey(track))ClearTrack(track);
            foreach(var track in used.OrderBy(t=>t))
            {
                plays.TryGetValue(track,out var clip); queued.TryGetValue(track,out var queue);
                var command=new StringBuilder(); if(clip!=null)Append(command,clip);
                if(queue!=null)foreach(var q in queue){command.Append(";Q:");Append(command,q.clip);command.Append(':').Append(q.delay.ToString("R",CultureInfo.InvariantCulture)).Append(':').Append(q.runtimeAfterPreviousEnd);}
                var signature=command.ToString();
                // 同一完整序列下发时，当前可能已进入队列目标，不能重播过渡头部。
                if(trackCommands.TryGetValue(track,out var previous)&&previous==signature&&state.GetTrack(track)!=null)continue;
                TrackEntry entry;
                if(clip!=null)
                {
                    // 仅Composer授权的结构方向轨。先退掉旧面mix链，主轨与动作事件均不重播。
                    if(clip.runtimeStructural)ClearTrack(track);
                    RemoveEntryRecords(track);
                    entry=state.SetAnimation(track,clip.animaName,clip.loop); Configure(entry,clip); CommitCount++;
                }
                else
                {
                    entry=state.GetTrack(track);
                    if(entry==null||!entries.TryGetValue(entry,out var context)||queue==null||Identity(context)!=Identity(queue[0].clip))
                    { Reject("queue-only缺少同来源/阶段上下文：track "+track); ClearTrack(track); continue; }
                    state.ClearNext(entry); RemoveEntryRecords(track,entry);
                }
                if(queue!=null)foreach(var q in queue)
                {
                    var prior=entry;
                    entry=state.AddAnimation(track,q.clip.animaName,q.clip.loop,1); Configure(entry,q.clip);
                    // Spine 4.3源码：Delay以Previous.TrackTime计量，TimeScale由Update转换。
                    // 过渡先到TrackComplete才开始混合；普通<=0保留“混合结束于前片结束”的原约定。
                    entry.Delay=q.runtimeAfterPreviousEnd?prior.TrackComplete+Mathf.Max(0,q.delay)*prior.TimeScale:
                        q.delay>0?q.delay:Mathf.Max(0,prior.TrackComplete-entry.MixDuration+q.delay);
                    QueueCount++;
                }
                trackCommands[track]=signature; LastResult="已提交 "+target+" track "+track;
            }
        }
        private void Configure(TrackEntry entry, CharacterAnimationClip clip)
        {
            entry.MixDuration=clip.mixDuration; entry.TimeScale=clip.playbackRate; entry.Additive=clip.isAdditive;
            entries[entry]=clip.Clone();
        }
        private void RemoveEntryRecords(int track,TrackEntry keep=null)
        { foreach(var entry in entries.Where(p=>p.Value.track==track&&!ReferenceEquals(p.Key,keep)).Select(p=>p.Key).ToArray()){signals?.Forget(entry);entries.Remove(entry);} }
        private void ClearTrack(int track)
        { if(state!=null)state.ClearTrack(track);trackCommands.Remove(track);RemoveEntryRecords(track); }
        public void ClearAll()
        { foreach(var track in trackCommands.Keys.ToArray())ClearTrack(track);entries.Clear();signals?.Clear();currentPlan=null; }
        private void ResetBinding()
        { if(signals!=null){signals.Signal-=OnSignal;signals.Dispose();}signals=null;trackCommands.Clear();entries.Clear();currentPlan=null;state=null; }
        private void OnDisable(){ClearAll();signals?.Dispose();}
        private void OnDestroy(){ResetBinding();owner=null;skeletonAnimation=null;}
    }
}
