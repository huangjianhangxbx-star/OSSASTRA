/* 脚本：DefenseSnapshot
 * 功能：冻结一次合法接触的防御资格；区分闪避、方向盾挡与普通受击。
 * 边界：纯数值决议，不改HP/架势/装备，不查询世界，不持有角色根。
 * 协作：世界网关采样，AttackRelease唯一批次使用；角度点积由适配器计算。
 */
using System;
namespace Ossastra.CharacterCore
{
    public readonly struct DefenseSnapshot
    {
        public bool Evading { get; }
        public bool Guarded { get; }
        public double PostureScale { get; }
        public DefenseSnapshot(bool evading, bool guarding, double incomingDot, double minimumDot, double guardPostureScale)
        {
            if(double.IsNaN(incomingDot)||double.IsInfinity(incomingDot)||double.IsNaN(minimumDot)||minimumDot < -1||minimumDot>1||
                double.IsNaN(guardPostureScale)||double.IsInfinity(guardPostureScale)||guardPostureScale<0||guardPostureScale>1)
                throw new ArgumentOutOfRangeException(nameof(incomingDot));
            Evading=evading; Guarded=!evading && guarding && incomingDot>=minimumDot;
            PostureScale=evading?0:Guarded?guardPostureScale:1;
        }
        public static DefenseSnapshot Normal => new DefenseSnapshot(false,false,0,0,1);
    }
}

