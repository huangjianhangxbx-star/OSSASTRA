/* 脚本：AbilityExecution
 * 功能：持有一次已接受能力的不可变来源身份与唯一阶段游标。
 * 边界：读取角色原模拟钟，不自行累计Time.time/delta；不创建HP/CD/耐久副本，不付款。
 * 协作：协调事务成功后创建；执行器消费带父执行身份的释放索引，取消后永不迟到释放。
 */
using System;
using System.Collections.Generic;
namespace Ossastra.CharacterCore.Abilities
{
    public sealed class AbilityExecution
    {
        public Guid Id {get;}
        public string ActorId {get;}
        public string SourceInstanceId {get;}
        public int LifeGeneration {get;}
        public int BindingRevision {get;}
        public AbilityPlan Plan {get;}
        public double AcceptedAt {get;}
        public bool Cancelled {get;private set;}
        private readonly AbilityEmission[] emissions;
        private int next;
        public AbilityExecution(Guid id,string actorId,string sourceInstanceId,int life,int revision,AbilityPlan plan,double acceptedAt)
        {
            if(id==Guid.Empty||string.IsNullOrWhiteSpace(actorId)||string.IsNullOrWhiteSpace(sourceInstanceId)||plan==null||!BasicAttackDefinition.Finite(acceptedAt))
                throw new ArgumentException("Accepted ability identity invalid");
            Id=id;ActorId=actorId;SourceInstanceId=sourceInstanceId;LifeGeneration=life;BindingRevision=revision;Plan=plan;AcceptedAt=acceptedAt;emissions=plan.Emissions;
        }
        public double Elapsed(double now)=>Math.Max(0,now-AcceptedAt);
        public bool Finished(double now)=>Cancelled||now>=AcceptedAt+Plan.FinishAt;
        public AbilityEmission[] Advance(double now,bool paused)
        {
            if(!BasicAttackDefinition.Finite(now))throw new ArgumentException("Ability time invalid");
            if(paused||Cancelled)return Array.Empty<AbilityEmission>();
            // 绝对到期时间比较，避免(5.1-5)<0.1的减法舍入使边界释放迟一帧；不引入提前释放epsilon。
            var results=new List<AbilityEmission>();
            while(next<emissions.Length&&now>=AcceptedAt+emissions[next].At)results.Add(emissions[next++]);
            return results.ToArray();
        }
        public void Cancel(){Cancelled=true;}
    }
}
