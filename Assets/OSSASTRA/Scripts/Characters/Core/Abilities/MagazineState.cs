/* 脚本：MagazineState
 * 功能：同一装备实例的实际弹序列与普通/特殊装填提交；释放弹药与完成装填各自去重。
 * 边界：不发弹/扣体力/播放动画；取消不生成弹药，挪槽/换装不重建本对象。
 * 协作：执行器在释放消费当前首弹；装填使用本体模拟到期时间，立即提交与延时提交互斥。
 * 持久化：Inventory/EquipmentPersistentSnapshot中的partial只导出/校验恢复字段，不改变下方能力算法。
 */
using System;
using System.Collections.Generic;
namespace Ossastra.CharacterCore.Abilities
{
    public enum AmmoKind { Normal, Mortar }
    public sealed partial class MagazineState
    {
        private readonly Queue<AmmoKind> rounds=new Queue<AmmoKind>();
        private readonly HashSet<Guid> shots=new HashSet<Guid>(),reloads=new HashSet<Guid>();
        private Guid pending;
        private AmmoKind pendingKind;
        private int pendingCount;
        private double due;
        private Guid automaticReload;
        private double lastShotAt;
        public int Count=>rounds.Count;
        public int Capacity {get;private set;}=4;
        public AmmoKind? Next=>rounds.Count==0?(AmmoKind?)null:rounds.Peek();
        public bool IsReloading=>pending!=Guid.Empty;
        public MagazineState(){Refill(AmmoKind.Normal,4);}
        public bool Consume(Guid emission)
        {if(emission==Guid.Empty||Count==0||!shots.Add(emission))return false;rounds.Dequeue();return true;}
        public bool Consume(Guid emission,double now)
        {
            if(!BasicAttackDefinition.Finite(now)||now<0||IsReloading||!Consume(emission))return false;
            lastShotAt=now;return true;
        }
        /// <summary>只推进本弹仓自己发起的普通装填，不抢精确装填的执行ID。</summary>
        public void TickAutomatic(double now,bool idle,double idleDelay=3,double duration=.8,int capacity=4)
        {
            if(!BasicAttackDefinition.Finite(now)||now<0)throw new ArgumentException("Invalid magazine clock");
            if(!idle){CancelAutomaticReload();return;}
            if(automaticReload!=Guid.Empty)
            {
                if(CompleteReload(automaticReload,now)||!IsReloading)automaticReload=Guid.Empty;
                return;
            }
            if(IsReloading||Count>=Capacity||Count>0&&now-lastShotAt<idleDelay)return;
            var id=Guid.NewGuid();if(BeginReload(id,AmmoKind.Normal,capacity,false,now,duration))automaticReload=id;
        }
        public void CancelAutomaticReload(){if(automaticReload!=Guid.Empty)CancelReload(automaticReload);automaticReload=Guid.Empty;}
        public bool BeginReload(Guid id,AmmoKind kind,int count,bool instant,double now,double duration)
        {
            if(id==Guid.Empty||IsReloading||reloads.Contains(id)||!Enum.IsDefined(typeof(AmmoKind),kind)||count<1||count>64||!BasicAttackDefinition.Finite(now)||!BasicAttackDefinition.Finite(duration)||duration<0)return false;
            pending=id;pendingKind=kind;pendingCount=count;due=now+duration;
            if(instant)CompleteReload(due);return true;
        }
        public bool CompleteReload(double now)
            =>CompleteReload(pending,now);
        public bool CompleteReload(Guid expected,double now)
        {
            if(!BasicAttackDefinition.Finite(now)||!IsReloading||pending!=expected||now<due)return false;
            reloads.Add(pending);Refill(pendingKind,pendingCount);pending=Guid.Empty;return true;
        }
        public void CancelReload(){pending=Guid.Empty;}
        public void CancelReload(Guid expected){if(pending==expected)CancelReload();}
        private void Refill(AmmoKind kind,int count){Capacity=count;rounds.Clear();for(int i=0;i<count;i++)rounds.Enqueue(kind);}
    }
}
