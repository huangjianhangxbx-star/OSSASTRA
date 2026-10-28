/* 脚本：EquipmentPersistentSnapshot
 * 功能：装备实例必要持久字段的纯DTO与受验证恢复；保留破损耐久、CD、弹序、霜寒和来源效果。
 * 边界：不改变现有能力算法；未完成装填/攻击不在重启后继续，既成弹药与资源不会免费刷新。
 * 协作：各状态partial只导出/恢复自己私有字段，Ledger完整校验后绑定同一恢复payload。
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Ossastra.CharacterCore;
using Ossastra.CharacterCore.Abilities;
namespace Ossastra.Inventory
{
    [Serializable] public sealed class EquipmentPersistentSnapshot
    {
        public string id,definitionId;
        public int kind,durability;
        public string[] wear=Array.Empty<string>();
        public EquipmentAbilityPersistentSnapshot runtime;
        public static EquipmentItemState Restore(EquipmentPersistentSnapshot snapshot)=>EquipmentItemState.RestorePersistent(snapshot);
        internal string Signature()
        {
            var text=new StringBuilder();Append(text,this);return text.ToString();
        }
        private static void Append(StringBuilder output,object value)
        {
            if(value==null){output.Append("null;");return;}
            if(value is string text){output.Append(text.Length).Append(':').Append(text).Append(';');return;}
            if(value is Array array){output.Append('[');foreach(var item in array)Append(output,item);output.Append(']');return;}
            if(value is IFormattable number){output.Append(number.ToString(null,CultureInfo.InvariantCulture)).Append(';');return;}
            if(value is bool flag){output.Append(flag?'1':'0');return;}
            foreach(var field in value.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance).OrderBy(f=>f.Name,StringComparer.Ordinal)){output.Append(field.Name);Append(output,field.GetValue(value));}
        }
    }
    [Serializable] public sealed class EquipmentAbilityPersistentSnapshot
    {
        public string mainActionProfileId;
        public double equippedTime,controlledTime,cooldownTime,controlledCooldownTime;
        public SkillSelectionPersistentSnapshot[] skillSelections=Array.Empty<SkillSelectionPersistentSnapshot>();
        public CooldownPersistentSnapshot cooldowns;
        public MagazinePersistentSnapshot magazine;
        public FrostPersistentSnapshot frost;
        public EffectSetPersistentSnapshot sources;
        public ShotPersistentSnapshot shotEnhancements;
    }
    [Serializable] public sealed class SkillSelectionPersistentSnapshot { public int slot;public string definitionId; }
    [Serializable] public sealed class CooldownPersistentSnapshot
    { public CooldownDeadlinePersistentSnapshot[] deadlines=Array.Empty<CooldownDeadlinePersistentSnapshot>();public string[] accepted=Array.Empty<string>(); }
    [Serializable] public sealed class CooldownDeadlinePersistentSnapshot {public string id;public double deadline;}
    [Serializable] public sealed class MagazinePersistentSnapshot
    {public int[] rounds=Array.Empty<int>();public int capacity;public double lastShotAt;public string[] shots=Array.Empty<string>(),reloads=Array.Empty<string>();}
    [Serializable] public sealed class FrostPersistentSnapshot
    {public double current,capacity,recoveryMultiplier,lastTime,paidAt;public bool hasPaid;}
    [Serializable] public sealed class EffectSetPersistentSnapshot
    {public int revision;public EffectRowPersistentSnapshot[] grants=Array.Empty<EffectRowPersistentSnapshot>();}
    [Serializable] public sealed class EffectRowPersistentSnapshot
    {public string instanceId;public int priority;public EffectDefinitionPersistentSnapshot definition;}
    [Serializable] public sealed class EffectDefinitionPersistentSnapshot
    {
        public string id,profession,effect,requiredRoot,exclusivePoint,comboPrefix;
        public int phase,damage;
        public double hastePerFrost,frostCap,frostRecovery;
        public ModifierPersistentSnapshot[] modifiers=Array.Empty<ModifierPersistentSnapshot>();
    }
    [Serializable] public sealed class ModifierPersistentSnapshot
    {
        public string root;public bool guaranteedCrit,passesTargets,instantReload,lastRoundOnly;public int ammoCount;
        public double damageAdd,damagePerFrost,damageRatio,cooldownAdd,knockRatio,stunSeconds,reloadShotRate,reloadShotDamage,shotRateBonus,shotRateDuration;
    }
    [Serializable] public sealed class ShotPersistentSnapshot
    {public string critical;public ReloadBonusPersistentSnapshot[] reload=Array.Empty<ReloadBonusPersistentSnapshot>();public TimedBonusPersistentSnapshot[] timed=Array.Empty<TimedBonusPersistentSnapshot>();}
    [Serializable] public sealed class ReloadBonusPersistentSnapshot {public string source;public bool speed;public int phase;public double value,at;}
    [Serializable] public sealed class TimedBonusPersistentSnapshot {public string source;public double value,until;}
    internal static class PersistentValidation
    {
        internal static bool Time(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value)&&value>=0;
        internal static void Guids(IEnumerable<string> values,HashSet<Guid> target)
        {if(values==null)throw new ArgumentException("装备去重记录缺失");foreach(var text in values){if(!Guid.TryParseExact(text,"N",out var id)||id==Guid.Empty||!target.Add(id))throw new ArgumentException("装备去重记录无效");}}
    }
}
namespace Ossastra.CharacterCore
{
    using Ossastra.Inventory;
    public sealed partial class EquipmentItemState
    {
        public EquipmentPersistentSnapshot CapturePersistent()=>new EquipmentPersistentSnapshot
        {id=Id,definitionId=DefinitionId,kind=(int)Kind,durability=Durability,wear=wear.Select(x=>x.ToString("N")).OrderBy(x=>x,StringComparer.Ordinal).ToArray(),runtime=Runtime.CapturePersistent()};
        internal string PersistentSignature()=>CapturePersistent().Signature();
        internal static EquipmentItemState RestorePersistent(EquipmentPersistentSnapshot snapshot)
        {
            if(snapshot==null||snapshot.durability<0||!Enum.IsDefined(typeof(OffhandKind),snapshot.kind))throw new ArgumentException("装备快照无效");
            var runtime=EquipmentAbilityState.RestorePersistent(snapshot.runtime);
            // 旧构造器拒绝初始破损值；恢复经校验后精确保留0，不改任何耐久消费算法。
            var state=new EquipmentItemState(snapshot.id,snapshot.definitionId,(OffhandKind)snapshot.kind,Math.Max(1,snapshot.durability),runtime){Durability=snapshot.durability};
            PersistentValidation.Guids(snapshot.wear,state.wear);return state;
        }
    }
    public sealed partial class SkillCooldownBook
    {
        internal CooldownPersistentSnapshot CapturePersistent()=>new CooldownPersistentSnapshot
        {deadlines=deadlines.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>new CooldownDeadlinePersistentSnapshot{id=p.Key,deadline=p.Value}).ToArray(),accepted=accepted.Select(x=>x.ToString("N")).OrderBy(x=>x,StringComparer.Ordinal).ToArray()};
        internal void RestorePersistent(CooldownPersistentSnapshot snapshot)
        {
            if(snapshot==null||snapshot.deadlines==null)throw new ArgumentException("冷却快照缺失");
            foreach(var row in snapshot.deadlines){if(row==null||string.IsNullOrWhiteSpace(row.id)||!PersistentValidation.Time(row.deadline))throw new ArgumentException("冷却快照无效");deadlines.Add(row.id,row.deadline);}
            PersistentValidation.Guids(snapshot.accepted,accepted);
        }
    }
}
namespace Ossastra.CharacterCore.Abilities
{
    using Ossastra.Inventory;
    public sealed partial class EquipmentAbilityState
    {
        internal EquipmentAbilityPersistentSnapshot CapturePersistent()=>new EquipmentAbilityPersistentSnapshot
        {
            mainActionProfileId=MainActionProfileId,equippedTime=EquippedTime,controlledTime=ControlledTime,cooldownTime=cooldownTime,controlledCooldownTime=controlledCooldownTime,
            skillSelections=SkillSelections.OrderBy(p=>p.Key).Select(p=>new SkillSelectionPersistentSnapshot{slot=p.Key,definitionId=p.Value}).ToArray(),
            cooldowns=Cooldowns.CapturePersistent(),magazine=Magazine.CapturePersistent(),frost=Frost.CapturePersistent(),sources=Sources.CapturePersistent(),shotEnhancements=ShotEnhancements.CapturePersistent()
        };
        internal static EquipmentAbilityState RestorePersistent(EquipmentAbilityPersistentSnapshot snapshot)
        {
            if(snapshot==null||snapshot.mainActionProfileId==null||snapshot.skillSelections==null||new[]{snapshot.equippedTime,snapshot.controlledTime,snapshot.cooldownTime,snapshot.controlledCooldownTime}.Any(v=>!PersistentValidation.Time(v))||snapshot.controlledTime>snapshot.equippedTime||snapshot.controlledCooldownTime>snapshot.cooldownTime)throw new ArgumentException("装备时钟/动作映射快照损坏");
            var state=new EquipmentAbilityState{MainActionProfileId=snapshot.mainActionProfileId,EquippedTime=snapshot.equippedTime,ControlledTime=snapshot.controlledTime,cooldownTime=snapshot.cooldownTime,controlledCooldownTime=snapshot.controlledCooldownTime};
            foreach(var row in snapshot.skillSelections){if(row==null||row.slot<0||row.definitionId==null)throw new ArgumentException("技能选择快照无效");state.SkillSelections.Add(row.slot,row.definitionId);}
            state.Cooldowns.RestorePersistent(snapshot.cooldowns);state.Magazine.RestorePersistent(snapshot.magazine);state.Frost.RestorePersistent(snapshot.frost);state.Sources.RestorePersistent(snapshot.sources);state.ShotEnhancements.RestorePersistent(snapshot.shotEnhancements,state.Sources);return state;
        }
    }
    public sealed partial class MagazineState
    {
        internal MagazinePersistentSnapshot CapturePersistent()=>new MagazinePersistentSnapshot
        {rounds=rounds.Select(x=>(int)x).ToArray(),capacity=Capacity,lastShotAt=lastShotAt,shots=shots.Select(x=>x.ToString("N")).OrderBy(x=>x,StringComparer.Ordinal).ToArray(),reloads=reloads.Select(x=>x.ToString("N")).OrderBy(x=>x,StringComparer.Ordinal).ToArray()};
        internal void RestorePersistent(MagazinePersistentSnapshot snapshot)
        {
            if(snapshot==null||snapshot.rounds==null||snapshot.capacity<1||snapshot.capacity>64||snapshot.rounds.Length>snapshot.capacity||snapshot.rounds.Any(x=>!Enum.IsDefined(typeof(AmmoKind),x))||!PersistentValidation.Time(snapshot.lastShotAt))throw new ArgumentException("弹仓快照无效");
            rounds.Clear();foreach(var kind in snapshot.rounds)rounds.Enqueue((AmmoKind)kind);Capacity=snapshot.capacity;lastShotAt=snapshot.lastShotAt;
            PersistentValidation.Guids(snapshot.shots,shots);PersistentValidation.Guids(snapshot.reloads,reloads);
            // 不恢复未完成装填的执行；旧弹序保持，重启不自动完成/补满。
            pending=Guid.Empty;automaticReload=Guid.Empty;
        }
    }
    public sealed partial class FrostReserveState
    {
        internal FrostPersistentSnapshot CapturePersistent()=>new FrostPersistentSnapshot
        {current=Current,capacity=Capacity,recoveryMultiplier=recoveryMultiplier,lastTime=lastTime,hasPaid=!double.IsNegativeInfinity(paidAt),paidAt=double.IsNegativeInfinity(paidAt)?0:paidAt};
        internal void RestorePersistent(FrostPersistentSnapshot snapshot)
        {
            if(snapshot==null||new[]{snapshot.current,snapshot.capacity,snapshot.recoveryMultiplier,snapshot.lastTime,snapshot.paidAt}.Any(v=>!PersistentValidation.Time(v))||snapshot.capacity<=0||snapshot.current>snapshot.capacity)throw new ArgumentException("霜寒快照无效");
            Current=snapshot.current;Capacity=snapshot.capacity;recoveryMultiplier=snapshot.recoveryMultiplier;lastTime=snapshot.lastTime;paidAt=snapshot.hasPaid?snapshot.paidAt:double.NegativeInfinity;
        }
    }
    public sealed partial class EffectSourceSet
    {
        internal EffectSetPersistentSnapshot CapturePersistent()=>new EffectSetPersistentSnapshot
        {revision=Revision,grants=grants.Values.OrderBy(r=>r.InstanceId,StringComparer.Ordinal).Select(r=>new EffectRowPersistentSnapshot{instanceId=r.InstanceId,priority=r.Priority,definition=CaptureDefinition(r.Definition)}).ToArray()};
        private static EffectDefinitionPersistentSnapshot CaptureDefinition(EffectSourceDefinition d)=>d==null?null:new EffectDefinitionPersistentSnapshot
        {
            id=d.Id,profession=d.Profession,effect=d.Effect,requiredRoot=d.RequiredRoot,exclusivePoint=d.ExclusivePoint,comboPrefix=d.ComboPrefix,phase=d.Phase,damage=d.Damage,
            hastePerFrost=d.HastePerFrost,frostCap=d.FrostCap,frostRecovery=d.FrostRecovery,
            modifiers=d.Modifiers.Select(m=>new ModifierPersistentSnapshot{root=m.Root,guaranteedCrit=m.GuaranteedCrit,passesTargets=m.PassesTargets,instantReload=m.InstantReload,lastRoundOnly=m.LastRoundOnly,ammoCount=m.AmmoCount,damageAdd=m.DamageAdd,damagePerFrost=m.DamagePerFrost,damageRatio=m.DamageRatio,cooldownAdd=m.CooldownAdd,knockRatio=m.KnockRatio,stunSeconds=m.StunSeconds,reloadShotRate=m.ReloadShotRate,reloadShotDamage=m.ReloadShotDamage,shotRateBonus=m.ShotRateBonus,shotRateDuration=m.ShotRateDuration}).ToArray()
        };
        internal void RestorePersistent(EffectSetPersistentSnapshot snapshot)
        {
            if(snapshot==null||snapshot.revision<0||snapshot.grants==null)throw new ArgumentException("来源快照无效");
            foreach(var row in snapshot.grants)
            {
                if(row==null||string.IsNullOrWhiteSpace(row.instanceId))throw new ArgumentException("来源身份无效");var d=row.definition;EffectSourceDefinition definition=null;
                if(d!=null)
                {
                    if(d.modifiers==null||d.modifiers.Any(m=>m==null))throw new ArgumentException("修饰快照无效");
                    var modifiers=d.modifiers.Select(m=>new EffectModifier(m.root,m.damageAdd,m.damagePerFrost,m.damageRatio,m.cooldownAdd,m.knockRatio,m.stunSeconds,m.reloadShotRate,m.reloadShotDamage,m.shotRateBonus,m.shotRateDuration,m.guaranteedCrit,m.passesTargets,m.instantReload,m.lastRoundOnly,m.ammoCount)).ToArray();
                    definition=new EffectSourceDefinition(d.id,d.profession,d.effect,d.phase,d.requiredRoot,d.exclusivePoint,d.hastePerFrost,d.frostCap,d.frostRecovery,d.damage,d.comboPrefix,modifiers);
                }
                grants.Add(row.instanceId,new EffectSourceRow(row.instanceId,definition,row.priority,EffectSourceStatus.ImportedUnresolved));
            }
            Revision=snapshot.revision;
        }
    }
    public sealed partial class ShotEnhancementState
    {
        internal ShotPersistentSnapshot CapturePersistent()=>new ShotPersistentSnapshot
        {critical=critical,reload=reload.Select(b=>new ReloadBonusPersistentSnapshot{source=b.source,speed=b.speed,phase=b.phase,value=b.value,at=b.at}).ToArray(),timed=timed.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>new TimedBonusPersistentSnapshot{source=p.Key,value=p.Value.value,until=p.Value.until}).ToArray()};
        internal void RestorePersistent(ShotPersistentSnapshot snapshot,EffectSourceSet sources)
        {
            if(snapshot==null||snapshot.reload==null||snapshot.timed==null||snapshot.critical!=null&&!sources.Contains(snapshot.critical))throw new ArgumentException("射击增益快照无效");critical=snapshot.critical;
            foreach(var row in snapshot.reload)
            {
                if(row==null||!sources.Contains(row.source)||row.phase<0||row.phase>2||!BasicAttackDefinition.Finite(row.value)||!PersistentValidation.Time(row.at))throw new ArgumentException("装填增益快照无效");
                reload.Add(new Bonus{source=row.source,speed=row.speed,phase=row.phase,value=row.value,at=row.at});
            }
            foreach(var row in snapshot.timed)
            {if(row==null||!sources.Contains(row.source)||!PersistentValidation.Time(row.value)||!PersistentValidation.Time(row.until))throw new ArgumentException("限时射击增益快照无效");timed.Add(row.source,(row.value,row.until));}
        }
    }
}
