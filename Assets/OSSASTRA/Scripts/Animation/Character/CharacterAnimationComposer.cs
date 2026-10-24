/* 原Composer合成职责迁移：保留来源意图、路由、覆盖、逐轨Play/Clear及过渡。
 * Submit/Remove只修改候选；运动完成后Commit一次。只有角色Profile声明的零时长结构方向层可与全身动作共存。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
namespace Ossastra.CharacterAnimation
{
    [DisallowMultipleComponent]
    public sealed class CharacterAnimationComposer : MonoBehaviour
    {
        private Ossastra.Characters.CharacterRoot characterRoot;
        private CharacterAnimationConfigManager configManager;
        private AnimationHandleSystem handle;
        private string target="body";
        private readonly Dictionary<AnimationIntentSourceStateMachine,AnimationIntent> activeIntents=new Dictionary<AnimationIntentSourceStateMachine,AnimationIntent>();
        private readonly Dictionary<AnimationIntentSourceStateMachine,EAnimationLogicState> history=new Dictionary<AnimationIntentSourceStateMachine,EAnimationLogicState>();
        private readonly Dictionary<AnimationIntentSourceStateMachine,(string key,CharacterAnimationPlan plan)> cache=new Dictionary<AnimationIntentSourceStateMachine,(string,CharacterAnimationPlan)>();
        private readonly AnimationTransitionRouter transitionRouter=new AnimationTransitionRouter();
        private bool dirty=true,committing;
        private int previousVersion;
        private string previousFacts;
        private Func<AnimationIntent,Ossastra.Characters.EquipmentDefinitionSO> equipmentSource;
        public CharacterAnimationPlan CurrentOutputPlan {get;private set;}
        public AnimationIntent CurrentBodyIntent {get;private set;}
        public int CommitCount {get;private set;}
        public string LastResult {get;private set;}="未初始化";
        private sealed class ResolvedAnimationPlan
        {
            public AnimationIntent intent;
            public EAnimationLogicState logic;
            public CharacterAnimationPlan plan;
        }
        private sealed class TrackOperation
        {
            public ResolvedAnimationPlan owner;
            public CharacterAnimationClip clip;
            public bool play;
        }
        public void Initialize(Ossastra.Characters.CharacterRoot root,CharacterAnimationConfigManager config,AnimationHandleSystem player,
            string targetId="body",Func<AnimationIntent,Ossastra.Characters.EquipmentDefinitionSO> source=null)
        {
            if(characterRoot==root&&configManager==config&&handle==player&&target==targetId)return;
            if(characterRoot!=null&&characterRoot!=root)throw new InvalidOperationException("Composer owner cannot change");
            characterRoot=root;configManager=config;handle=player;target=targetId;equipmentSource=source;dirty=true;
        }
        private static bool SameIdentity(AnimationIntent a,AnimationIntent b)
            =>a.actorId==b.actorId&&a.target==b.target&&a.actionId==b.actionId&&a.phaseId==b.phaseId&&a.lifeGeneration==b.lifeGeneration
                &&a.presentationGeneration==b.presentationGeneration&&a.replayId==b.replayId;
        public void SubmitIntent(AnimationIntent intent)
        {
            if(intent==null||!intent.IsValid(characterRoot,target)||intent.intentSourceStateMatine==AnimationIntentSourceStateMachine.None)return;
            if(activeIntents.TryGetValue(intent.intentSourceStateMatine,out var current)&&SameIdentity(current,intent)
                &&current.semantic==intent.semantic&&current.logicId==intent.logicId&&current.priority==intent.priority&&current.layer==intent.layer
                &&current.exclusiveFullBody==intent.exclusiveFullBody&&current.oneShot==intent.oneShot
                &&current.structureProfile==intent.structureProfile
                &&(intent.oneShot||current.configurationVersion==intent.configurationVersion))return;
            var copy=intent.Clone();
            // 重复提交同一动作不延长寿命；自动到期使用角色唯一模拟时钟。
            copy.expireTime=copy.oneShot&&copy.duration>0?(float)characterRoot.SimulationTime+copy.duration:0;
            if(current!=null&&SameIdentity(current,copy))copy.expireTime=current.expireTime;
            activeIntents[copy.intentSourceStateMatine]=copy;dirty=true;
        }
        public bool RemoveIntent(AnimationIntentSourceStateMachine source,Guid? actionId=null,Guid? phaseId=null)
        {
            if(!activeIntents.TryGetValue(source,out var current)||actionId.HasValue&&current.actionId!=actionId.Value||phaseId.HasValue&&current.phaseId!=phaseId.Value)return false;
            activeIntents.Remove(source);cache.Remove(source);history.Remove(source);dirty=true;return true;
        }
        public AnimationIntent GetIntent(AnimationIntentSourceStateMachine source) => activeIntents.TryGetValue(source,out var value)?value.Clone():null;
        public IReadOnlyDictionary<AnimationIntentSourceStateMachine,AnimationIntent> SnapshotIntents()
            =>activeIntents.ToDictionary(pair=>pair.Key,pair=>pair.Value.Clone());
        public void ClearAllIntents()
        {activeIntents.Clear();cache.Clear();history.Clear();CurrentOutputPlan=null;CurrentBodyIntent=null;dirty=true;handle?.ClearAll();}
        private string FactsKey()
        {
            var bus=characterRoot.DataBus;
            return characterRoot.IsAlive+"|"+bus.IsMoving+"|"+bus.Velocity.magnitude.ToString("F2")+"|"+bus.ActionActive+"|"+bus.MoveReady+"|"+bus.AttackReady
                +"|"+bus.AllowsAttack+"|"+bus.AllowsMovement+"|"+bus.VisualFacingX+"|"+characterRoot.HasPlayerInput+"|"+characterRoot.AttackStage
                +"|"+characterRoot.Vitals?.Health.Current+"|"+bus.Stamina?.Current;
        }
        private static string PlanKey(CharacterAnimationPlan plan)
        {
            var b=new StringBuilder();
            foreach(var c in plan.clips)if(c!=null)b.Append(c.track).Append(':').Append(c.animaName).Append(':').Append(c.loop).Append(':').Append(c.mixDuration).Append(':').Append(c.playbackRate).Append(':').Append(c.isAdditive).Append(';');
            foreach(var q in plan.queuedClips)if(q?.clip!=null)b.Append("Q").Append(q.clip.track).Append(':').Append(q.clip.animaName).Append(':').Append(q.delay).Append(':').Append(q.clip.mixDuration).Append(':').Append(q.clip.playbackRate).Append(';');
            foreach(var t in plan.clearTracks)b.Append("C").Append(t).Append(';');return b.ToString();
        }
        public void Commit(int generation)
        {
            if(committing||characterRoot==null||configManager==null||handle==null)return;
            committing=true;
            try
            {
                foreach(var pair in activeIntents.ToArray())if(!pair.Value.IsValid(characterRoot,target)
                    ||pair.Value.oneShot&&pair.Value.duration>0&&characterRoot.SimulationTime>=pair.Value.expireTime)RemoveIntent(pair.Key,pair.Value.actionId,pair.Value.phaseId);
                var facts=FactsKey();var version=configManager.ConfigurationVersion;
                foreach(var intent in activeIntents.Values.OrderBy(i=>(int)i.intentSourceStateMatine))
                {
                    var equipment=equipmentSource?.Invoke(intent);
                    version=unchecked(version*31+(equipment==null?0:equipment.GetInstanceID()));
                    var slots=characterRoot.GetComponent<Ossastra.Characters.CharacterEquipment>()?.State;
                    var instance=intent.intentSourceStateMatine==AnimationIntentSourceStateMachine.OffHand?slots?.LeftInstanceId:slots?.RightInstanceId;
                    version=unchecked(version*31+(instance?.GetHashCode()??0));
                    var overrides=equipment?.bodyAnimationOverride;if(overrides!=null)version=unchecked(version*31+overrides.GetInstanceID()+overrides.Revision);
                }
                if(previousVersion!=version||previousFacts!=facts){dirty=true;previousVersion=version;previousFacts=facts;}
                if(dirty){CurrentOutputPlan=ComposeFinalPlan();dirty=false;}
                handle.ApplyPlan(CurrentOutputPlan,generation);CommitCount++;
            }
            finally{committing=false;}
        }
        private CharacterAnimationPlan ComposeFinalPlan()
        {
            var resolved=new List<ResolvedAnimationPlan>();
            foreach(var intent in activeIntents.Values.OrderBy(i=>(int)i.intentSourceStateMatine))
            {
                var context=new AnimationComposeContext{root=characterRoot,dataBus=characterRoot.DataBus,activeIntents=activeIntents,target=target,
                    sourceEquipment=equipmentSource?.Invoke(intent),configurationVersion=configManager.ConfigurationVersion};
                var logic=intent.logicId!=EAnimationLogicState.None?intent.logicId:configManager.RouteIntent(intent,context);
                if(logic==EAnimationLogicState.None){LastResult="缺少语义路由："+intent.semantic;continue;}
                var raw=intent.frozenPlan?.CloneFor(characterRoot)??configManager.ResolvePlan(logic,context);
                if(raw==null||!raw.HasOperations){LastResult="缺少有效映射："+logic;continue;}
                var key=intent.actionId+"|"+intent.phaseId+"|"+intent.presentationGeneration+"|"+logic+"|"+PlanKey(raw);
                CharacterAnimationPlan plan;
                if(cache.TryGetValue(intent.intentSourceStateMatine,out var cached)&&cached.key==key)plan=cached.plan.CloneFor(characterRoot);
                else
                {
                    var previous=history.TryGetValue(intent.intentSourceStateMatine,out var state)?state:EAnimationLogicState.None;
                    plan=transitionRouter.BuildTransitionedPlan(intent,previous,logic,raw,context,configManager);
                    cache[intent.intentSourceStateMatine]=(key,plan.CloneFor(characterRoot));
                }
                resolved.Add(new ResolvedAnimationPlan{intent=intent,logic=logic,plan=plan});
            }
            return MergePlans(resolved);
        }
        private static bool Better(TrackOperation old,TrackOperation candidate)
        {
            if(old==null)return true;
            if(candidate.owner.intent.priority!=old.owner.intent.priority)return candidate.owner.intent.priority>old.owner.intent.priority;
            if(candidate.play!=old.play)return candidate.play;
            return false; // 同类型同优先级，保持已按显式来源数值排序的先出现者。
        }
        private CharacterAnimationPlan MergePlans(List<ResolvedAnimationPlan> plans)
        {
            CurrentBodyIntent=null;
            var operations=new Dictionary<int,TrackOperation>();
            void Offer(int track,ResolvedAnimationPlan owner,CharacterAnimationClip clip)
            {
                if(track<0)return;
                if(owner.intent.intentSourceStateMatine==AnimationIntentSourceStateMachine.StructuralDirection&&!LegalStructure(owner.intent,clip))
                {LastResult="结构层不属于当前Profile或映射非法";return;}
                if(clip!=null&&(handle.Skeleton?.Skeleton?.Data.FindAnimation(clip.animaName)==null||clip.playbackRate<=0||float.IsNaN(clip.playbackRate)||float.IsInfinity(clip.playbackRate)))
                {LastResult="无效轨道片段："+clip.animaName;return;}
                var candidate=new TrackOperation{owner=owner,clip=clip,play=clip!=null};
                operations.TryGetValue(track,out var previous);if(Better(previous,candidate))operations[track]=candidate;
            }
            foreach(var resolved in plans)
            {
                foreach(var track in resolved.plan.clearTracks)Offer(track,resolved,null);
                foreach(var clip in resolved.plan.clips)if(clip!=null&&!string.IsNullOrEmpty(clip.animaName))Offer(clip.track,resolved,clip);
                // queue-only必须在Handle复核同来源/阶段上下文；它仍需参加该轨竞争。
                foreach(var queue in resolved.plan.queuedClips)if(queue?.clip!=null&&!resolved.plan.clips.Any(c=>c!=null&&c.track==queue.clip.track))Offer(queue.clip.track,resolved,queue.clip);
            }
            ResolvedAnimationPlan exclusive=null;
            if(target=="body"&&operations.TryGetValue(0,out var zero)&&zero.play&&zero.owner.intent.exclusiveFullBody
                &&zero.owner.plan.clips.Any(c=>c!=null&&c.track==0&&!string.IsNullOrEmpty(c.animaName)))exclusive=zero.owner;
            var output=new CharacterAnimationPlan{characterRoot=characterRoot};var winners=new HashSet<AnimationIntentSourceStateMachine>();
            foreach(var pair in operations.OrderBy(p=>p.Key))
            {
                var op=pair.Value;
                if(!op.play||exclusive!=null&&op.owner!=exclusive&&!LegalStructure(op.owner.intent,op.clip)){output.clearTracks.Add(pair.Key);continue;}
                if(pair.Key==0)CurrentBodyIntent=op.owner.intent.Clone();
                var plan=op.owner.plan;var immediate=plan.clips.FirstOrDefault(c=>c!=null&&c.track==pair.Key);
                if(immediate!=null)output.clips.Add(Stamp(immediate,op.owner.intent));
                foreach(var queued in plan.queuedClips.Where(q=>q?.clip!=null&&q.clip.track==pair.Key))
                {var copy=queued.Clone();copy.clip=Stamp(copy.clip,op.owner.intent);output.queuedClips.Add(copy);}
                winners.Add(op.owner.intent.intentSourceStateMatine);history[op.owner.intent.intentSourceStateMatine]=op.owner.logic;
            }
            // 落败者的接续及过渡历史立即失效；恢复时重算当前目标，不补播积压。
            foreach(var source in cache.Keys.Where(s=>!winners.Contains(s)).ToArray()){cache.Remove(source);history.Remove(source);}
            LastResult="最终轨道 "+output.clips.Count+"；队列 "+output.queuedClips.Count+"；清轨 "+output.clearTracks.Count;
            return output;
        }
        private CharacterAnimationClip Stamp(CharacterAnimationClip clip,AnimationIntent intent)
        {
            var c=clip.Clone();c.characterRoot=characterRoot;c.runtimeActor=characterRoot.ActorId;c.runtimeTarget=target;
            c.runtimeActionId=intent.actionId;c.runtimePhaseId=intent.phaseId;c.runtimeSourceStateMachine=intent.intentSourceStateMatine;
            c.runtimeReplayId=intent.replayId;c.runtimeGeneration=intent.presentationGeneration;c.runtimeLifeGeneration=intent.lifeGeneration;
            c.runtimeStructural=LegalStructure(intent,clip);return c;
        }
        private bool LegalStructure(AnimationIntent intent,CharacterAnimationClip clip)
        {
            var profile=characterRoot?.Definition?.directionProfile;
            return target=="body"&&clip!=null&&profile!=null&&intent.structureProfile==profile
                &&intent.intentSourceStateMatine==AnimationIntentSourceStateMachine.StructuralDirection&&intent.layer==AnimationLayer.StructuralModel
                &&profile.Authorizes(intent.logicId,clip.track)&&!clip.scalesWithAction&&clip.mixDuration==0
                &&handle?.Skeleton?.Skeleton?.Data.FindAnimation(clip.animaName)?.Duration==0;
        }
        private void OnDisable(){ClearAllIntents();}
    }
}
