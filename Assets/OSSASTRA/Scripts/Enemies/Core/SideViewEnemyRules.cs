/*
 * 脚本：SideViewEnemyRules
 * 功能：纯数值的横版感知、目标稳定性和个别招式门槛。
 * 职责：提供可独立验证的判定，与敌根的动作时钟和世界物理查询分开。
 */
using System;

namespace Ossastra.EnemyCore
{
    public static class SideViewEnemyRules
    {
        public static bool Detects(float x, float y, float z, float reach, float height, float depth)
            => Math.Abs(x) <= reach && Math.Abs(y) <= height && Math.Abs(z) <= depth;

        public static bool Beats(string candidate, float distance, string selected, float selectedDistance)
            => selected == null || distance < selectedDistance ||
                (distance == selectedDistance && string.CompareOrdinal(candidate, selected) < 0);

        public static int Facing(float horizontal, int fallback)
            => horizontal > 0 ? 1 : horizontal < 0 ? -1 : fallback < 0 ? -1 : 1;

        public static bool ShouldSwitchTarget(float currentSquared, float candidateSquared, float distanceMargin)
            => Math.Sqrt(candidateSquared) + distanceMargin < Math.Sqrt(currentSquared);

        // 钻击以偏下的矩形区域触发，提交后不随目标横移。
        public static bool CanFlyDrill(float x, float y, float reach)
            => Math.Abs(x) <= .8f && y <= -.2f && y >= -reach;

        // 原场单 Boss 的前段高血量选择只发飞扑。
        public static bool MotherMaySlam(int currentHp, int maximumHp)
            => currentHp <= (long)maximumHp * 95 / 100;

        public static bool HoldsCommit(bool originalTargetIsAlive, bool released)
            => released || originalTargetIsAlive;
    }
}
