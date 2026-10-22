/* 脚本：EquipmentAbilityState
 * 功能：同一装备实例持有的能力资源payload：CD账本、弹仓、霜寒及按政策过滤的模拟时间。
 * 边界：不持有HP/体力，不读Time.time；换槽/卸装不重建，源未装备时由调用者停止推进。
 * 协作：CharacterAbilities以Root已推进的delta调用一次；受控时间只在实际玩家控制时增长。
 */
using System;
namespace Ossastra.CharacterCore.Abilities
{
    public enum AbilityCooldownPolicy { WhileEquipped, WhilePlayerControlled }
    public sealed class EquipmentAbilityState
    {
        public SkillCooldownBook Cooldowns {get;}=new SkillCooldownBook();
        public MagazineState Magazine {get;}=new MagazineState();
        public FrostReserveState Frost {get;}=new FrostReserveState();
        // 空字符串表示主动清空；缺键才使用定义默认值，换槽不重建选择。
        public System.Collections.Generic.Dictionary<int,string> SkillSelections {get;}=new System.Collections.Generic.Dictionary<int,string>();
        public double EquippedTime {get;private set;}
        public double ControlledTime {get;private set;}
        public double Clock(AbilityCooldownPolicy policy)=>policy==AbilityCooldownPolicy.WhilePlayerControlled?ControlledTime:EquippedTime;
        public void Tick(double delta,bool controlled,bool frostRecovering=true)
        {
            if(!BasicAttackDefinition.Finite(delta)||delta<0)throw new ArgumentException("Invalid equipment simulation delta");
            EquippedTime+=delta;if(controlled)ControlledTime+=delta;Frost.Tick(EquippedTime,frostRecovering);
        }
    }
}
