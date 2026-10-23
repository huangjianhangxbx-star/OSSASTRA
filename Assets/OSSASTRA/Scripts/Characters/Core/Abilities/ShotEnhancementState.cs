/* 脚本：ShotEnhancementState
 * 功能：装备实例的装填增益、一次命中暴击和落地限时射速。
 * 边界：不发弹、不扣HP；接受时冻结射击数值，命中回执才消费暴击。
 * 协作：来源撤回清理尚在实例上的效果，已接受的射击快照保持不变。
 */
using System;
using System.Linq;
using System.Collections.Generic;
namespace Ossastra.CharacterCore.Abilities
{
    public readonly struct ShotEnhancementSnapshot
    {
        public readonly string CriticalSource;
        public readonly double DamageMultiplier,Rate;
        public ShotEnhancementSnapshot(string source,double damage,double rate){CriticalSource=source;DamageMultiplier=damage;Rate=rate;}
    }
    public sealed class ShotEnhancementState
    {
        private sealed class Bonus {public string source;public bool speed;public int phase;public double value,at;}
        private readonly List<Bonus> reload=new List<Bonus>();
        private readonly Dictionary<string,(double value,double until)> timed=new Dictionary<string,(double,double)>();
        private string critical;
        public void PrecisionAccepted(EffectSnapshot effects,double now)
        {
            foreach(var source in effects.Sources)foreach(var modifier in source.Definition.Modifiers.Where(m=>m.Root=="precision"))
            {
                if(modifier.GuaranteedCrit)critical=source.InstanceId;
                if(modifier.ReloadShotRate!=0)reload.Add(new Bonus{source=source.InstanceId,speed=true,value=modifier.ReloadShotRate,at=now+.05});
                if(modifier.ReloadShotDamage!=0)reload.Add(new Bonus{source=source.InstanceId,value=modifier.ReloadShotDamage,at=now+.05});
            }
        }
        public void Tick(double now,EffectSourceSet sources)
        {
            reload.RemoveAll(b=>!sources.Contains(b.source));
            foreach(var b in reload)if(b.phase==0&&now>=b.at)b.phase=b.speed?2:1;
            foreach(var id in timed.Keys.ToArray())if(!sources.Contains(id)||now>=timed[id].until)timed.Remove(id);
            if(critical!=null&&!sources.Contains(critical))critical=null;
        }
        public void ReloadStarted()=>reload.RemoveAll(b=>b.speed&&b.phase==2);
        public void ReloadCompleted(){reload.RemoveAll(b=>!b.speed&&b.phase==2);foreach(var b in reload)if(!b.speed&&b.phase==1)b.phase=2;}
        public void ReloadCancelled()=>reload.Clear();
        public void RocketLanded(EffectSnapshot effects,double now,EffectSourceSet sources)
        {
            foreach(var row in effects.Sources.Where(s=>sources.Contains(s.InstanceId)))
                foreach(var m in row.Definition.Modifiers.Where(m=>m.Root=="rocket-land"&&m.ShotRateBonus>0&&m.ShotRateDuration>0))
                    timed[row.InstanceId]=(m.ShotRateBonus,now+m.ShotRateDuration);
        }
        public ShotEnhancementSnapshot Snapshot()=>new ShotEnhancementSnapshot(critical,
            (1+reload.Where(b=>!b.speed&&b.phase==2).Sum(b=>b.value))*(critical==null?1:2.25),
            1+reload.Where(b=>b.speed&&b.phase==2).Sum(b=>b.value)+timed.Values.Sum(b=>b.value));
        public void Applied(ShotEnhancementSnapshot shot){if(shot.CriticalSource!=null&&critical==shot.CriticalSource)critical=null;}
        public void Clear(){critical=null;reload.Clear();timed.Clear();}
    }
}
