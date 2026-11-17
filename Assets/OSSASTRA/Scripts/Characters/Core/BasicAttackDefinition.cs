/*
 * 脚本：BasicAttackDefinition
 * 功能：验证并保存不可变的动作节点、伤害、架势冲击和外部维持阶段的可选权限。
 * 边界：不内置正式平衡默认值；拒绝无效时间顺序和非正伤害。
 * 协作：由Root构造，BasicActionRuntime读取。
 */
using System;

namespace Ossastra.CharacterCore
{
    // 全部配置不可变；本类没有游戏平衡默认值。
    public sealed class BasicAttackDefinition
    {
        public double ReleaseAt { get; }
        public double MoveReadyAt { get; }
        public double AttackReadyAt { get; }
        public double FinishAt { get; }
        public int Damage { get; }
        public double PostureImpact { get; }
        public bool WaitsForPhaseChange { get; }
        public bool HasReleaseNode { get; }
        public bool AllowsMovementWhileWaiting { get; }
        public bool RetainsPoseWhileMoving { get; }

        public BasicAttackDefinition(double release, double move, double ready, double finish, int damage, double postureImpact = 0)
            : this(release, move, ready, finish, damage, postureImpact, false)
        {
        }

        private BasicAttackDefinition(double release, double move, double ready, double finish, int damage,
            double postureImpact, bool waitsForPhaseChange, bool allowsMovementWhileWaiting = false,
            bool retainsPoseWhileMoving = false, bool hasReleaseNode = true)
        {
            if (!Finite(release) || !Finite(move) || !Finite(ready) || !Finite(finish) ||
                release < 0 || move < release || ready < release || finish < move || finish < ready)
                throw new ArgumentOutOfRangeException(nameof(release), "Invalid action node ordering or time");
            if (damage <= 0) throw new ArgumentOutOfRangeException(nameof(damage));
            if (!Finite(postureImpact) || postureImpact < 0) throw new ArgumentOutOfRangeException(nameof(postureImpact));
            ReleaseAt = release;
            MoveReadyAt = move;
            AttackReadyAt = ready;
            FinishAt = finish;
            Damage = damage;
            // 与伤害一起冻结；默认零使未启用架势的旧场保持原行为。
            PostureImpact = postureImpact;
            WaitsForPhaseChange = waitsForPhaseChange;
            HasReleaseNode = !waitsForPhaseChange && hasReleaseNode;
            AllowsMovementWhileWaiting = allowsMovementWhileWaiting;
            RetainsPoseWhileMoving = retainsPoseWhileMoving;
        }

        /// <summary>保留原阶段时长，由所属动作显式切相或取消。移动与姿势需分别启用，后续主攻击继续关闭。</summary>
        public BasicAttackDefinition WithoutTimedNodes(bool allowMovement = false, bool retainPoseWhileMoving = false)
            => new BasicAttackDefinition(ReleaseAt, MoveReadyAt, AttackReadyAt, FinishAt, Damage, PostureImpact,
                true, allowMovement, retainPoseWhileMoving);

        /// <summary>保留定时权限与结束节点，仅移除没有伤害释放的阶段对应的出手节点。</summary>
        public BasicAttackDefinition WithoutReleaseNode()
            => new BasicAttackDefinition(ReleaseAt, MoveReadyAt, AttackReadyAt, FinishAt, Damage, PostureImpact,
                WaitsForPhaseChange, AllowsMovementWhileWaiting, RetainsPoseWhileMoving, hasReleaseNode: false);

        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
