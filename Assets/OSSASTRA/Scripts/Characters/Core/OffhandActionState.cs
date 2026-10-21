/* 脚本：OffhandActionState
 * 功能：副手独立接受、单次释放和 SAMPLE 冷却；保留来源身份。
 * 边界：不触碰主手时钟、设备、体力或物理；非实控只冻结冷却，暂停冻结所有推进。
 */
using System;
namespace Ossastra.CharacterCore
{
    public sealed class OffhandActionState
    {
        private readonly string ownerId;
        private double elapsed;
        public Guid ActionId { get; private set; }
        public string EquipmentId { get; private set; }
        public int LifeGeneration { get; private set; }
        public bool IsAccepting { get; private set; }
        public double CooldownRemaining { get; private set; }
        public OffhandActionState(string ownerId) { this.ownerId=ownerId; }
        public bool TryAccept(Guid id,string equipmentId,int lifeGeneration)
        {
            if(id==Guid.Empty||string.IsNullOrEmpty(equipmentId)||IsAccepting||CooldownRemaining>0)return false;
            ActionId=id;EquipmentId=equipmentId;LifeGeneration=lifeGeneration;elapsed=0;IsAccepting=true;CooldownRemaining=5;return true;
        }
        public AttackRelease Advance(double delta,bool controlled,bool paused)
        {
            if(double.IsNaN(delta)||double.IsInfinity(delta)||delta<0)throw new ArgumentOutOfRangeException(nameof(delta));
            if(paused)return null;
            if(controlled)CooldownRemaining=Math.Max(0,CooldownRemaining-delta);
            if(!IsAccepting)return null;
            elapsed+=delta;if(elapsed<.05)return null;
            IsAccepting=false;return new AttackRelease(ActionId,ownerId,45,25);
        }
        public void CancelPending(){IsAccepting=false;}
    }
}
