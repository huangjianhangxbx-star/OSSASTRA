/*
 * 脚本：ChasePolicy
 * 功能：有限追击状态选择，输入为世界适配器已验证的距离/视线/阻挡快照。
 * 边界：不移动、不查玩家Current、不写生命，不实现寻路或技能时钟。
 */
namespace Ossastra.EnemyCore
{
    public enum ChasePhase { Idle, Approach, InRangeAttack, ReturnHome, Blocked }
    public static class ChasePolicy
    {
        public static ChasePhase Decide(bool available,bool hasTarget,bool sight,float distance,float homeDistance,
            float stopDistance,float leash,float homeTolerance,bool blocked)
        {
            if(!available)return ChasePhase.Idle;
            if(blocked)return ChasePhase.Blocked;
            if(homeDistance>leash || !hasTarget || !sight)
                return homeDistance>homeTolerance?ChasePhase.ReturnHome:ChasePhase.Idle;
            return distance<=stopDistance?ChasePhase.InRangeAttack:ChasePhase.Approach;
        }
    }
}
