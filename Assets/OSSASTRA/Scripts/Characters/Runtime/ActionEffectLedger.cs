/* 功能：按接受的动作、条目、真实释放和目标分别去重，使用发动者的模拟钟限制间隔。
 * 边界：纯 C# 台账，不采样输入、场景或动画；动作结束只关闭自身窗口，离手命中仍可影响存活目标。
 */
using System;
using System.Collections.Generic;
using Ossastra.CharacterCore;

namespace Ossastra.Characters
{
    public enum ActionEffectFrequency { Action, Release, TargetPerRelease, Interval }

    internal sealed class ActionEffectLedger
    {
        private sealed class Counter
        {
            public int count;
            public double nextTime;
            public readonly HashSet<(Guid release, string target)> consumed = new HashSet<(Guid, string)>();
        }

        private readonly Counter[] counters;
        internal Guid Id { get; }
        internal bool Ended { get; private set; }
        internal ActionEffectLedger(Guid actionId, int entryCount)
        {
            Id = actionId;
            counters = new Counter[entryCount];
        }
        internal bool Owns(AttackRelease release) => release != null && release.ParentExecutionId == Id;
        internal void End() => Ended = true;
        internal bool AllowsContact(HitReason reason, bool onGuard, bool targetEffect, bool targetAlive)
        {
            if (reason != HitReason.Applied && !(reason == HitReason.Guarded && onGuard)) return false;
            return targetEffect ? targetAlive : !Ended;
        }

        internal bool Claim(int index, ActionEffectFrequency frequency, int maximum, float interval, double now, Guid releaseId, string targetId)
        {
            var counter = counters[index] ?? (counters[index] = new Counter());
            if (maximum > 0 && counter.count >= maximum) return false;
            switch (frequency)
            {
                case ActionEffectFrequency.Action: if (counter.count > 0) return false; break;
                case ActionEffectFrequency.Release: if (!counter.consumed.Add((releaseId, ""))) return false; break;
                case ActionEffectFrequency.TargetPerRelease: if (!counter.consumed.Add((releaseId, targetId))) return false; break;
                case ActionEffectFrequency.Interval:
                    if (now < counter.nextTime) return false;
                    counter.nextTime = now + interval;
                    break;
                default: return false;
            }
            counter.count++;
            return true;
        }
    }
}
