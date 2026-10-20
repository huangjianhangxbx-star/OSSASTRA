/*
 * 脚本：NpcInteraction
 * 功能：NPC自身持有的最小交互资格和回执状态。
 * 边界：无HP/攻击/玩家根；合法请求每次计一次，拒绝不修改成功次数。
 * 协作：NpcRoot提供位置距离/可用状态，实际消费者订阅回应；销毁显式Dispose。
 */
using System;
namespace Ossastra.Npcs
{
    public readonly struct NpcReply
    {
        public readonly bool accepted;
        public readonly string npcId, actorId, reason;
        public readonly int count;
        public NpcReply(bool accepted,string npcId,string actorId,string reason,int count)
        { this.accepted=accepted;this.npcId=npcId;this.actorId=actorId;this.reason=reason;this.count=count; }
    }
    public sealed class NpcInteraction : IDisposable
    {
        private readonly string npcId;
        private readonly float rangeSquared;
        private bool disposed;
        public int Count { get; private set; }
        public event Action<NpcReply> Replied;
        public NpcInteraction(string npcId,float range)
        {
            if(string.IsNullOrWhiteSpace(npcId)||float.IsNaN(range)||float.IsInfinity(range)||range<=0)throw new ArgumentException("Invalid NPC identity/range");
            this.npcId=npcId;rangeSquared=range*range;
        }
        public NpcReply TryInteract(string actorId,float distanceSquared,bool available)
        {
            var reason=disposed?"disposed":!available?"unavailable":
                string.IsNullOrWhiteSpace(actorId)?"invalid-actor":
                float.IsNaN(distanceSquared)||float.IsInfinity(distanceSquared)||distanceSquared<0?"invalid-position":
                distanceSquared>rangeSquared?"out-of-range":"NPC已回应";
            var accepted=reason=="NPC已回应";
            if(accepted) Count++;
            var reply=new NpcReply(accepted,npcId,actorId,reason,Count);
            if(accepted) Replied?.Invoke(reply);
            return reply;
        }
        public void Dispose(){disposed=true;Replied=null;}
    }
}
