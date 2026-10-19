/*
 * 脚本：CompanionFollowPolicy
 * 功能：按双距离阈值产生归一化跟随方向，并诊断持续受阻。
 * 边界：无Unity依赖，不执行位移/寻路/攻击；实际位移与目标坐标由场景适配器提供。
 * 协作：FollowDirector每个伙伴持有独立策略；只有前帧确有命令才累计无位移时间。
 */
using System;
namespace Ossastra.CharacterCore
{
    public enum CompanionFollowState { Inactive, Following, CloseEnough, Blocked }
    public readonly struct CompanionFollowDecision
    {
        public CompanionFollowState State { get; }
        public double MoveX { get; }
        public double MoveZ { get; }
        public CompanionFollowDecision(CompanionFollowState state, double x=0, double z=0) { State=state; MoveX=x; MoveZ=z; }
    }
    public sealed class CompanionFollowPolicy
    {
        private readonly double start, stop, blockWindow, retryDelta;
        private bool following, blocked, commanded;
        private double stalled, blockedTargetX, blockedTargetZ;

        public CompanionFollowPolicy(double start, double stop, double blockWindow, double retryDelta)
        {
            if (!Finite(start) || !Finite(stop) || !Finite(blockWindow) || !Finite(retryDelta) ||
                stop <= 0 || start <= stop || blockWindow <= 0 || retryDelta <= 0)
                throw new ArgumentException("Invalid companion follow thresholds");
            this.start=start; this.stop=stop; this.blockWindow=blockWindow; this.retryDelta=retryDelta;
        }

        public void Reset() { following=false; blocked=false; commanded=false; stalled=0; }

        /// <summary>提供水平相对向量、绝对目标坐标、上帧真实位移与模拟步长；不可行动时清策略并输出零。</summary>
        public CompanionFollowDecision Sample(double x,double z,double targetX,double targetZ,double travel,double delta,bool eligible)
        {
            if (!eligible || !Finite(x) || !Finite(z) || !Finite(targetX) || !Finite(targetZ) ||
                !Finite(travel) || !Finite(delta) || delta <= 0 || travel < 0)
            { Reset(); return new CompanionFollowDecision(CompanionFollowState.Inactive); }
            var distance=Math.Sqrt(x*x+z*z);
            if (!Finite(distance)) { Reset(); return new CompanionFollowDecision(CompanionFollowState.Inactive); }
            if (distance <= stop)
            { Reset(); return new CompanionFollowDecision(CompanionFollowState.CloseEnough); }
            if (blocked)
            {
                var dx=targetX-blockedTargetX; var dz=targetZ-blockedTargetZ;
                if (dx*dx+dz*dz < retryDelta*retryDelta)
                    return new CompanionFollowDecision(CompanionFollowState.Blocked);
                // 领队显著移动才恢复尝试；不宣称可自行绕墙或在静止目标下持续寻找新路径。
                blocked=false; stalled=0; commanded=false;
            }
            if (!following && distance < start) return new CompanionFollowDecision(CompanionFollowState.CloseEnough);
            following=true;
            stalled=commanded && travel < .002 ? stalled+delta : 0;
            if (stalled >= blockWindow)
            {
                blocked=true; commanded=false; blockedTargetX=targetX; blockedTargetZ=targetZ;
                return new CompanionFollowDecision(CompanionFollowState.Blocked);
            }
            commanded=true;
            return new CompanionFollowDecision(CompanionFollowState.Following,x/distance,z/distance);
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
