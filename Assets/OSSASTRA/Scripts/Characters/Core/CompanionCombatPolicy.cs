/* 伙伴战斗纯决策门：只返回候选，不持有动作时钟、生命、输入或移动写入权。 */
namespace Ossastra.CharacterCore
{
    public enum CompanionCombatDecision { Inactive, Follow, Approach, Wait, Attack }
    public static class CompanionCombatPolicy
    {
        public static CompanionCombatDecision Decide(bool session, bool current, bool engaged, bool capable,
            bool target, bool insideAnchor, bool inRange, bool ready, bool blocked)
        {
            if (!session || current || !engaged || !capable) return CompanionCombatDecision.Inactive;
            if (!target || !insideAnchor || blocked) return CompanionCombatDecision.Follow;
            if (!inRange) return CompanionCombatDecision.Approach;
            return ready ? CompanionCombatDecision.Attack : CompanionCombatDecision.Wait;
        }
    }
}
