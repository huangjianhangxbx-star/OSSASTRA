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

        public static bool HoldsCommit(bool originalTargetIsAlive, bool released)
            => released || originalTargetIsAlive;
    }
}
