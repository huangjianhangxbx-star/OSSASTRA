/* 脚本：EffectSourceSet
 * 功能：实例化来源、按原型优先级/阶段/互斥点求值，并冻结每次接受动作的效果快照。
 * 边界：不执行伤害、不保存SO或HP；移除来源改变活体登记，不追改已经接受的快照。
 * 协作：装备payload拥有登记；专职能力消费冻结修饰，延迟副作用另查Contains验证来源仍持有。
 */
using System;
using System.Linq;
using System.Collections.Generic;
namespace Ossastra.CharacterCore.Abilities
{
    public enum EffectSourceStatus { Active, Dormant, Superseded, ImportedUnresolved }
    public sealed class EffectModifier
    {
        public string Root {get;}
        public bool GuaranteedCrit {get;}
        public bool PassesTargets {get;}
        public bool InstantReload {get;}
        public bool LastRoundOnly {get;}
        public int AmmoCount {get;}
        public double DamageAdd {get;}
        public double DamagePerFrost {get;}
        public double DamageRatio {get;}
        public double CooldownAdd {get;}
        public double KnockRatio {get;}
        public double StunSeconds {get;}
        public double ReloadShotRate {get;}
        public double ReloadShotDamage {get;}
        public double ShotRateBonus {get;}
        public double ShotRateDuration {get;}
        public EffectModifier(string root,double damageAdd=0,double damagePerFrost=0,double damageRatio=0,double cooldownAdd=0,double knockRatio=0,
            double stunSeconds=0,double reloadShotRate=0,double reloadShotDamage=0,double shotRateBonus=0,double shotRateDuration=0,
            bool guaranteedCrit=false,bool passesTargets=false,bool instantReload=false,bool lastRoundOnly=false,int ammoCount=0)
        {
            if(string.IsNullOrWhiteSpace(root)||new[]{damageAdd,damagePerFrost,damageRatio,cooldownAdd,knockRatio,stunSeconds,reloadShotRate,reloadShotDamage,shotRateBonus,shotRateDuration}.Any(v=>!BasicAttackDefinition.Finite(v))||ammoCount<0||ammoCount>64)
                throw new ArgumentException("Invalid effect modifier");
            Root=root;DamageAdd=damageAdd;DamagePerFrost=damagePerFrost;DamageRatio=damageRatio;CooldownAdd=cooldownAdd;KnockRatio=knockRatio;StunSeconds=stunSeconds;
            ReloadShotRate=reloadShotRate;ReloadShotDamage=reloadShotDamage;ShotRateBonus=shotRateBonus;ShotRateDuration=shotRateDuration;
            GuaranteedCrit=guaranteedCrit;PassesTargets=passesTargets;InstantReload=instantReload;LastRoundOnly=lastRoundOnly;AmmoCount=ammoCount;
        }
    }
    public sealed class EffectSourceDefinition
    {
        public string Id {get;}
        public string Profession {get;}
        public string Effect {get;}
        public string RequiredRoot {get;}
        public string ExclusivePoint {get;}
        public string ComboPrefix {get;}
        public int Phase {get;}
        public int Damage {get;}
        public double HastePerFrost {get;}
        public double FrostCap {get;}
        public double FrostRecovery {get;}
        private readonly EffectModifier[] modifiers;
        public EffectModifier[] Modifiers=>(EffectModifier[])modifiers.Clone();
        public EffectSourceDefinition(string id,string profession,string effect,int phase=0,string requiredRoot=null,string exclusivePoint=null,
            double hastePerFrost=0,double frostCap=0,double frostRecovery=0,int damage=0,string comboPrefix=null,EffectModifier[] modifiers=null)
        {
            if(string.IsNullOrWhiteSpace(id)||string.IsNullOrWhiteSpace(profession)||string.IsNullOrWhiteSpace(effect)||phase<0||damage<0||
                new[]{hastePerFrost,frostCap,frostRecovery}.Any(v=>!BasicAttackDefinition.Finite(v)||v<0)||modifiers!=null&&modifiers.Any(m=>m==null))throw new ArgumentException("Invalid effect definition");
            Id=id;Profession=profession;Effect=effect;Phase=phase;RequiredRoot=string.IsNullOrWhiteSpace(requiredRoot)?null:requiredRoot;
            ExclusivePoint=string.IsNullOrWhiteSpace(exclusivePoint)?null:exclusivePoint;HastePerFrost=hastePerFrost;
            FrostCap=frostCap;FrostRecovery=frostRecovery;Damage=damage;ComboPrefix=comboPrefix;this.modifiers=modifiers==null?Array.Empty<EffectModifier>():(EffectModifier[])modifiers.Clone();
        }
    }
    public sealed class EffectSourceRow
    {
        public string InstanceId {get;}
        public EffectSourceDefinition Definition {get;}
        public int Priority {get;}
        public EffectSourceStatus Status {get;}
        internal EffectSourceRow(string id,EffectSourceDefinition definition,int priority,EffectSourceStatus status)
        {InstanceId=id;Definition=definition;Priority=priority;Status=status;}
    }
    public sealed class EffectSourceSet
    {
        private readonly Dictionary<string,EffectSourceRow> grants=new Dictionary<string,EffectSourceRow>(StringComparer.Ordinal);
        public int Revision {get;private set;}
        public bool Contains(string instanceId)=>instanceId!=null&&grants.ContainsKey(instanceId);
        public bool Grant(string instanceId,EffectSourceDefinition definition,int priority=0)
        {
            if(string.IsNullOrWhiteSpace(instanceId)||grants.ContainsKey(instanceId))return false;
            grants.Add(instanceId,new EffectSourceRow(instanceId,definition,priority,EffectSourceStatus.ImportedUnresolved));Revision++;return true;
        }
        public bool Remove(string instanceId){if(instanceId==null||!grants.Remove(instanceId))return false;Revision++;return true;}
        public EffectSourceRow[] Evaluate(string profession,IEnumerable<string> roots)
        {
            var available=new HashSet<string>(roots??Array.Empty<string>(),StringComparer.Ordinal);var chosen=new HashSet<string>(StringComparer.Ordinal);
            return grants.Values.OrderByDescending(s=>s.Priority).ThenByDescending(s=>s.Definition?.Phase??0).ThenBy(s=>s.InstanceId,StringComparer.Ordinal).Select(s=>
            {
                var d=s.Definition;EffectSourceStatus status;
                if(d==null||d.Profession!=profession||d.Effect=="unresolved")status=EffectSourceStatus.ImportedUnresolved;
                else if(!string.IsNullOrEmpty(d.RequiredRoot)&&(d.RequiredRoot=="active"?available.Count==0:!available.Contains(d.RequiredRoot)))status=EffectSourceStatus.Dormant;
                else if(!chosen.Add(d.ExclusivePoint??(d.Effect=="chisel"?"basic-stage-3":d.Id)))status=EffectSourceStatus.Superseded;
                else status=EffectSourceStatus.Active;
                return new EffectSourceRow(s.InstanceId,d,s.Priority,status);
            }).ToArray();
        }
        public EffectSnapshot Snapshot(string profession,IEnumerable<string> roots,double frost,bool lastRound)
            =>new EffectSnapshot(Revision,Evaluate(profession,roots).Where(r=>r.Status==EffectSourceStatus.Active).ToArray(),frost,lastRound);
    }
    public sealed class EffectSnapshot
    {
        private readonly EffectSourceRow[] sources;
        public int Revision {get;}
        public double Frost {get;}
        public bool LastRound {get;}
        public EffectSourceRow[] Sources=>(EffectSourceRow[])sources.Clone();
        public double HastePerFrost=>sources.Sum(s=>s.Definition.HastePerFrost);
        public double FrostCapacity=>3+sources.Sum(s=>s.Definition.FrostCap);
        public double FrostRecovery=>1+sources.Sum(s=>s.Definition.FrostRecovery);
        public EffectSnapshot(int revision,EffectSourceRow[] sources,double frost,bool lastRound)
        {Revision=revision;this.sources=(EffectSourceRow[])sources.Clone();Frost=frost;LastRound=lastRound;}
        public bool ContainsSource(string id)=>sources.Any(s=>s.InstanceId==id);
        public bool HasEffect(string effect)=>sources.Any(s=>s.Definition.Effect==effect);
        public EffectSourceRow FindEffect(string effect)=>sources.FirstOrDefault(s=>s.Definition.Effect==effect);
        public EffectModifier[] Modifiers(string root)=>sources.SelectMany(s=>s.Definition.Modifiers).Where(m=>m.Root==root&&(!m.LastRoundOnly||LastRound)).ToArray();
        public double Damage(string root,double original)
        {var mods=Modifiers(root);return Math.Max(0,(original+mods.Sum(m=>m.DamageAdd+m.DamagePerFrost*Frost))*(1+mods.Sum(m=>m.DamageRatio)));}
        public double Cooldown(string root,double original)=>Math.Max(0,original+Modifiers(root).Sum(m=>m.CooldownAdd));
    }
}
