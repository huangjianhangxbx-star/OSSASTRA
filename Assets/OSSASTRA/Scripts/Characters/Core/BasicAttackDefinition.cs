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

        public BasicAttackDefinition(double release, double move, double ready, double finish, int damage)
        {
            if (!Finite(release) || !Finite(move) || !Finite(ready) || !Finite(finish) ||
                release < 0 || move < release || ready < release || finish < move || finish < ready)
                throw new ArgumentOutOfRangeException(nameof(release), "Invalid action node ordering or time");
            if (damage <= 0) throw new ArgumentOutOfRangeException(nameof(damage));
            ReleaseAt = release;
            MoveReadyAt = move;
            AttackReadyAt = ready;
            FinishAt = finish;
            Damage = damage;
        }

        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
