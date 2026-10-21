/*
 * 脚本：AnimationRouting
 * 功能：只读动画语义、同帧优先级与播放身份；可由三类实体分别产出。
 * 边界：没有Unity、伤害、输入或动画完成回调；代际由本体根失效处理推进。
 */
using System;
using System.Collections.Generic;
namespace Ossastra.AnimationCore
{
    public enum AnimationSemantic { Idle, Move, BasicAttack, HitReaction, PostureBroken, Death, BasicAttackSecond, Dodge, Guard }
    public readonly struct AnimationIntent
    {
        public string ActorId { get; }
        public AnimationSemantic Semantic { get; }
        public string Source { get; }
        public Guid ReplayId { get; }
        public int Generation { get; }
        public AnimationIntent(string actorId, AnimationSemantic semantic, string source, Guid replayId, int generation)
        { ActorId=actorId; Semantic=semantic; Source=source; ReplayId=replayId; Generation=generation; }
    }
    public sealed class AnimationResolvePolicy
    {
        private readonly string actorId;
        public AnimationResolvePolicy(string actorId) { this.actorId=actorId; }
        public AnimationIntent? Resolve(IEnumerable<AnimationIntent> candidates, int generation)
        {
            AnimationIntent? best=null;
            foreach(var intent in candidates)
            {
                if(intent.ActorId!=actorId || intent.Generation!=generation) continue;
                if(!best.HasValue || Priority(intent.Semantic)>Priority(best.Value.Semantic)) best=intent;
            }
            return best;
        }
        // 明确主轨优先级：死亡>破势>受击>动作>移动>待机，与具体Spine片段名称无关。
        private static int Priority(AnimationSemantic semantic) => semantic==AnimationSemantic.Death?6:semantic==AnimationSemantic.PostureBroken?5:semantic==AnimationSemantic.HitReaction?4:semantic==AnimationSemantic.Dodge?3:semantic==AnimationSemantic.Guard||semantic==AnimationSemantic.BasicAttack||semantic==AnimationSemantic.BasicAttackSecond?2:(int)semantic;
    }
    public sealed class AnimationPlaybackIdentity
    {
        private string clip;
        private AnimationIntent last;
        private bool hasLast;
        public bool ShouldCommit(string nextClip, AnimationIntent intent)
        {
            var changed=!hasLast || clip!=nextClip || last.ActorId!=intent.ActorId || last.Semantic!=intent.Semantic ||
                last.Generation!=intent.Generation || last.ReplayId!=intent.ReplayId;
            clip=nextClip; last=intent; hasLast=true; return changed;
        }
    }
}
