/* 脚本：AbilityPlan
 * 功能：验证并冻结一次能力的阶段与有限释放节点，支持同一次执行的多次释放。
 * 边界：不持有SO/设备/资源/目标；节点数组拷贝进出，不能被作者后改配置污染。
 * 协作：定义编译器构建；AbilityExecution按原本体模拟时钟读取，不另创场景时钟。
 */
using System;
using System.Linq;
namespace Ossastra.CharacterCore.Abilities
{
    [Flags] public enum AbilityOccupancy { None=0, Body=1, MainHand=2, OffHand=4, Motion=8 }
    public readonly struct AbilityEmission
    {
        public double At {get;}
        public int Damage {get;}
        public int Index {get;}
        public AbilityEmission(double at,int damage,int index){At=at;Damage=damage;Index=index;}
    }
    public sealed class AbilityPlan
    {
        private readonly AbilityEmission[] emissions;
        public string Id {get;}
        public double FinishAt {get;}
        public double MoveReadyAt {get;}
        public double AttackReadyAt {get;}
        public AbilityOccupancy Occupancy {get;}
        public AbilityEmission[] Emissions => (AbilityEmission[])emissions.Clone();
        public AbilityPlan(string id,double finish,double moveReady,double attackReady,AbilityEmission[] emissions,AbilityOccupancy occupancy)
        {
            if(string.IsNullOrWhiteSpace(id)||!Valid(finish)||finish<=0||!Valid(moveReady)||!Valid(attackReady)||moveReady>finish||attackReady>finish)
                throw new ArgumentException("Ability identity/stage times invalid");
            if(emissions==null||emissions.Length>128||emissions.Any(n=>!Valid(n.At)||n.At>finish||n.Damage<0||n.Index<0)||emissions.Select(n=>n.Index).Distinct().Count()!=emissions.Length)
                throw new ArgumentException("Ability emission time/index invalid");
            if((occupancy&~(AbilityOccupancy.Body|AbilityOccupancy.MainHand|AbilityOccupancy.OffHand|AbilityOccupancy.Motion))!=0)
                throw new ArgumentException("Ability occupancy invalid");
            Id=id;FinishAt=finish;MoveReadyAt=moveReady;AttackReadyAt=attackReady;Occupancy=occupancy;
            this.emissions=emissions.OrderBy(n=>n.At).ThenBy(n=>n.Index).ToArray();
        }
        private static bool Valid(double value)=>BasicAttackDefinition.Finite(value)&&value>=0;
    }
}
