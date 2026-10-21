/* 脚本：HealingBottleState
 * 功能：一次试验世界的共享瓶余额和同步治疗事务。
 * 边界：不持有角色、输入或返场；一次性补给水位不随成员死亡重置。
 */
using System.Collections.Generic;
using System.Linq;
namespace Ossastra.CharacterCore
{
    public sealed class HealingBottleState
    {
        public int Count {get;private set;}=1;
        public int UseCount {get;private set;}
        public bool Refilled {get;private set;}
        public bool TryUse(IEnumerable<Health> members)
        {
            var living=members.Where(h=>h!=null&&h.Current>0).Distinct().ToArray();
            if(Count<=0||!living.Any(h=>h.Current<h.Maximum))return false;
            Count--;UseCount++;foreach(var health in living)health.Heal(100);return true;
        }
        public bool TryRefill(){if(Refilled||Count>=1)return false;Count=1;Refilled=true;return true;}
    }
}
