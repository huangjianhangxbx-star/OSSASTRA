/*
 * 功能：以一份缺口账本表达行动暴露、持续防御、有效冲击和自然恢复。
 * 边界：不拥有 HP、动作准入或破防受迫运动；由角色唯一模拟步推进。
 */
using System;
using System.Collections.Generic;

namespace Ossastra.CombatCore
{
    public enum PostureActionGroup { Locomotion, VerticalTraversal, MainAttack, Dodge, OffhandAction }

    public sealed class ActionPostureLedger
    {
        private const int Guard = (int)PostureActionGroup.OffhandAction + 1;
        private const int Hit = Guard + 1;
        private const int BucketCount = Hit + 1;
        private readonly double recoveryDelay;
        private readonly double recoveryRate;
        private readonly double guardRate;
        private readonly double[] deficits = new double[BucketCount];
        private readonly double[] readyAt = new double[BucketCount];
        private readonly int[] recoveryLocks = new int[Guard];
        private readonly Dictionary<Guid, RecoveryOwner> owners = new Dictionary<Guid, RecoveryOwner>();
        // 终结后的异步事件仍可能到达。动作身份只在显式生命／会话重置时失效。
        private readonly HashSet<Guid> finishedOwners = new HashSet<Guid>();
        private double now;

        public double Maximum { get; }
        public double Current => Math.Max(0, Maximum - TotalDeficit());
        public double GuardDeficit => deficits[Guard];
        public double HitDeficit => deficits[Hit];
        public bool GuardHeld { get; private set; }
        public int RecoveryBlockers => owners.Count;
        public int GlobalRecoveryBlockers { get; private set; }
        public double RecoveryMultiplier { get; private set; } = 1;
        public double RecoveryPerSecond => recoveryRate * RecoveryMultiplier;
        public double WaitingSeconds
        {
            get
            {
                double longest = 0;
                for (int bucket = 0; bucket < BucketCount; bucket++)
                    if (deficits[bucket] > 0 && !IsGroupLocked(bucket))
                        longest = Math.Max(longest, readyAt[bucket] - now);
                return longest;
            }
        }

        public ActionPostureLedger(double maximum, double recoveryDelay, double recoveryRate, double guardRate)
        {
            if (!Finite(maximum) || maximum <= 0 || !NonNegative(recoveryDelay) ||
                !NonNegative(recoveryRate) || !NonNegative(guardRate))
                throw new ArgumentOutOfRangeException(nameof(maximum), "Invalid action posture configuration");
            Maximum = maximum;
            this.recoveryDelay = recoveryDelay;
            this.recoveryRate = recoveryRate;
            this.guardRate = guardRate;
        }

        public double GetDeficit(PostureActionGroup group) => IsActionGroup(group) ? deficits[(int)group] : 0;
        public bool HasRecoveryOwner(Guid id) => owners.ContainsKey(id);
        public Guid[] GetRecoveryOwners()
        {
            var result = new Guid[owners.Count];
            owners.Keys.CopyTo(result, 0);
            return result;
        }

        /// <summary>同组仅补足目标。相同动作身份的重复提交没有重新付款的资格。</summary>
        public double Begin(Guid id, PostureActionGroup group, double exposureTarget, bool blocksAllRecovery)
        {
            if (id == Guid.Empty || !IsActionGroup(group) || !NonNegative(exposureTarget) || finishedOwners.Contains(id))
                return 0;
            if (!owners.TryGetValue(id, out var owner))
            {
                owner = new RecoveryOwner();
                owners.Add(id, owner);
            }

            int bucket = (int)group;
            int bit = 1 << bucket;
            bool newGroup = (owner.GroupMask & bit) == 0;
            double target = Math.Min(exposureTarget, Maximum);
            if (!newGroup && target <= owner.Targets[bucket]) return 0;
            if (newGroup)
            {
                owner.GroupMask |= bit;
                recoveryLocks[bucket]++;
            }
            UpdateGlobalLock(owner, bit, blocksAllRecovery);
            owner.Targets[bucket] = target;
            return AddDeficit(bucket, Math.Max(0, target - deficits[bucket]));
        }

        public void End(Guid id)
        {
            if (!owners.TryGetValue(id, out var owner)) return;
            owners.Remove(id);
            finishedOwners.Add(id);
            if (owner.GlobalMask != 0) GlobalRecoveryBlockers--;
            for (int bucket = 0; bucket < Guard; bucket++)
                if ((owner.GroupMask & (1 << bucket)) != 0 && --recoveryLocks[bucket] == 0)
                    readyAt[bucket] = now + recoveryDelay;
        }

        public void SetGuardHold(bool held)
        {
            if (held == GuardHeld) return;
            GuardHeld = held;
            if (!held) readyAt[Guard] = now + recoveryDelay;
        }

        public void SetRecoveryMultiplier(double multiplier)
        {
            if (NonNegative(multiplier) && Finite(multiplier * recoveryRate))
                RecoveryMultiplier = multiplier;
        }

        /// <summary>返回一次新破防事实。反应存续和运动由唯一外部反应状态机拥有。</summary>
        public bool ApplyImpact(double impact, bool healthLost, bool canBreak, bool reacting)
        {
            if (!canBreak || reacting || !NonNegative(impact) || (impact == 0 && !healthLost)) return false;
            AddDeficit(Hit, impact);
            readyAt[Hit] = now + recoveryDelay;
            if (Current > Maximum * 1e-12) return false;

            Suspend();
            Array.Clear(readyAt, 0, readyAt.Length);
            return true;
        }

        public void Tick(double delta)
        {
            double end = now + delta;
            if (delta <= 0 || !Finite(end)) return;

            if (GuardHeld)
                AddDeficit(Guard, guardRate * delta);
            else if (GlobalRecoveryBlockers == 0 && RecoveryPerSecond > 0)
                RecoverUntil(end);
            now = end;
        }

        // 以缺口恢复资格的变化切分步长。每个时间片只分配一次全体预算。
        private void RecoverUntil(double end)
        {
            while (now < end)
            {
                double next = end;
                double eligible = 0;
                for (int bucket = 0; bucket < BucketCount; bucket++)
                {
                    if (deficits[bucket] <= 0 || IsGroupLocked(bucket)) continue;
                    if (readyAt[bucket] <= now) eligible += deficits[bucket];
                    else next = Math.Min(next, readyAt[bucket]);
                }
                if (eligible > 0)
                {
                    double remainder = Math.Max(0, 1 - (next - now) * RecoveryPerSecond / eligible);
                    for (int bucket = 0; bucket < BucketCount; bucket++)
                        if (!IsGroupLocked(bucket) && readyAt[bucket] <= now)
                            deficits[bucket] *= remainder;
                }
                now = next;
            }
        }

        /// <summary>清理动作和防御生命周期，保留同一生命的实际缺口。</summary>
        public void Suspend()
        {
            foreach (var id in owners.Keys) finishedOwners.Add(id);
            owners.Clear();
            GlobalRecoveryBlockers = 0;
            for (int bucket = 0; bucket < Guard; bucket++)
                if (recoveryLocks[bucket] != 0)
                {
                    recoveryLocks[bucket] = 0;
                    readyAt[bucket] = now + recoveryDelay;
                }
            SetGuardHold(false);
        }

        public void Reset()
        {
            owners.Clear();
            finishedOwners.Clear();
            Array.Clear(deficits, 0, deficits.Length);
            Array.Clear(readyAt, 0, readyAt.Length);
            Array.Clear(recoveryLocks, 0, recoveryLocks.Length);
            GlobalRecoveryBlockers = 0;
            GuardHeld = false;
            now = 0;
        }

        private void UpdateGlobalLock(RecoveryOwner owner, int bit, bool blocksAllRecovery)
        {
            bool wasGlobal = owner.GlobalMask != 0;
            owner.GlobalMask = blocksAllRecovery ? owner.GlobalMask | bit : owner.GlobalMask & ~bit;
            if (wasGlobal != (owner.GlobalMask != 0)) GlobalRecoveryBlockers += wasGlobal ? -1 : 1;
        }

        private double AddDeficit(int bucket, double requested)
        {
            double charged = Math.Min(requested, Current);
            deficits[bucket] += charged;
            return charged;
        }

        private double TotalDeficit()
        {
            double total = 0;
            foreach (var deficit in deficits) total += deficit;
            return total;
        }

        private bool IsGroupLocked(int bucket) => bucket < Guard && recoveryLocks[bucket] > 0;
        private static bool IsActionGroup(PostureActionGroup group) => (uint)group < Guard;
        private static bool NonNegative(double value) => value >= 0 && Finite(value);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private sealed class RecoveryOwner
        {
            public int GroupMask;
            public int GlobalMask;
            public readonly double[] Targets = new double[Guard];
        }
    }
}
