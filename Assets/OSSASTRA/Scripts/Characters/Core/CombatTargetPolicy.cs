/*
 * 脚本：CombatTargetPolicy
 * 功能：在真实扣血前判断来源/目标身份、死活与工程阵营资格。
 * 边界：不查物理、不写HP；无友伤是本轮验证选项，不是永久玩法平衡。
 * 协作：Melee空间筛选后消费；Neutral保留原开发靶兼容语义。
 */
namespace Ossastra.CharacterCore
{
    public enum CombatFaction { Neutral, Allied, Enemy }
    public static class CombatTargetPolicy
    {
        public static bool CanHit(string source, CombatFaction sourceFaction, bool sourceAlive,
            string target, CombatFaction targetFaction, bool targetAlive)
            => !string.IsNullOrWhiteSpace(source) && !string.IsNullOrWhiteSpace(target) &&
                sourceAlive && targetAlive && source != target &&
                (sourceFaction==CombatFaction.Neutral || targetFaction==CombatFaction.Neutral || sourceFaction!=targetFaction);
    }
}
