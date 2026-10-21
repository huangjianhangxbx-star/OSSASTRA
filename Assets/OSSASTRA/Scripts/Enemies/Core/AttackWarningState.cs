/* 攻击预警身份投影：只记已观察事实，不推进时钟，不产生攻击或命中。 */
using System;
namespace Ossastra.EnemyCore
{
    public sealed class AttackWarningState
    {
        public Guid ActionId { get; private set; }
        public bool Tracking => ActionId != Guid.Empty && !Released;
        public bool Released { get; private set; }
        public void Accept(Guid id) { if(id==Guid.Empty || id==ActionId) return; ActionId=id; Released=false; }
        public void Release(Guid id) { if(id==ActionId && id!=Guid.Empty) Released=true; }
        public void Clear(Guid id) { if(id!=ActionId)return; ActionId=Guid.Empty; Released=false; }
    }
}
