# ROADMAP03 源码审查快照（S9—S13）

范围：相对e6c1e9e的本轮自有源码。行号及SHA用于人工复核，不等同于用户已审定架构。

## Assets/OSSASTRA/Scripts/Characters/Core/AttackRelease.cs
SHA256 37E349863698B519FB9128077F839F08BD39491CD3425C40B13121DB86C66D7E
```csharp
   1: /*
   2:  * 脚本：AttackRelease
   3:  * 功能：结算一次动作释放的目标批次，并返回命中结果。
   4:  * 边界：新旧入口共享一次性批次；按目标身份去重；HP只经Health写入，零伤防御不调用扣血。
   5:  * 协作：世界适配器先做空间筛选，再调用Resolve；空挥关闭批次。防御快照决定HP及架势倍率，同批回执供世界适配器消费。
   6:  */
   7: using System;
   8: using System.Collections.Generic;
   9: using System.Linq;
  10: 
  11: 
  12: namespace Ossastra.CharacterCore
  13: {
  14:     public enum HitReason { Applied, Duplicate, Dead, Self, Guarded, Evaded }
  15: 
  16:     /// <summary>空间/身份已合法的候选及同帧只读防御快照；不另持有HP余额。</summary>
  17:     public readonly struct AttackContact
  18:     {
  19:         public Health Health { get; }
  20:         public DefenseSnapshot Defense { get; }
  21:         public AttackContact(Health health, DefenseSnapshot defense)
  22:         { Health=health ?? throw new ArgumentNullException(nameof(health)); Defense=defense; }
  23:     }
  24: 
  25:     public sealed class HitOutcome
  26:     {
  27:         public HitReason Reason { get; }
  28:         public int HpBefore { get; }
  29:         public int HpAfter { get; }
  30:         public Guid ActionId { get; }
  31:         public string ActorId { get; }
  32:         public string TargetId { get; }
  33:         public double PostureScale { get; }
  34: 
  35:         internal HitOutcome(HitReason reason, int before, Health target, Guid actionId, string actorId, double postureScale=1)
  36:         {
  37:             Reason = reason;
  38:             HpBefore = before;
  39:             HpAfter = target.Current;
  40:             TargetId = target.Id;
  41:             ActionId = actionId;
  42:             ActorId = actorId;
  43:             PostureScale = reason==HitReason.Applied || reason==HitReason.Guarded ? postureScale : 0;
  44:         }
  45:     }
  46: 
  47:     // 每次释放只能提交一个瞬时近战候选批次；空挥也关闭，避免永久开启命中窗口。
  48:     public sealed class AttackRelease
  49:     {
  50:         private readonly Guid actionId;
  51:         private readonly string actorId;
  52:         private readonly int damage;
  53:         private bool resolved;
  54:         public string ActorId => actorId;
  55:         public Guid ActionId => actionId;
  56:         public double PostureImpact { get; }
  57: 
  58:         internal AttackRelease(Guid actionId, string actorId, int damage, double postureImpact = 0)
  59:         {
  60:             this.actionId = actionId;
  61:             this.actorId = actorId;
  62:             this.damage = damage;
  63:             PostureImpact = postureImpact;
  64:         }
  65: 
  66:         // 调用方必须先完成实际空间/阵营过滤；本方法不谎称已做物理检测。
  67:         public IReadOnlyList<HitOutcome> Resolve(IEnumerable<Health> targets)
  68:         {
  69:             if(targets==null) throw new ArgumentNullException(nameof(targets));
  70:             return ResolveContacts(targets.Select(t=>new AttackContact(t,DefenseSnapshot.Normal)));
  71:         }
  72: 
  73:         /// <summary>与旧Resolve共享一次性水位；盾挡零伤不调用ApplyDamage，闪避不消费架势。</summary>
  74:         public IReadOnlyList<HitOutcome> ResolveContacts(IEnumerable<AttackContact> contacts)
  75:         {
  76:             if (resolved) throw new InvalidOperationException("Attack release already resolved");
  77:             if (contacts == null) throw new ArgumentNullException(nameof(contacts));
  78:             var batch = contacts.ToArray();
  79:             if (batch.Any(t => t.Health == null)) throw new ArgumentException("Null candidate target", nameof(contacts));
  80:             resolved = true;
  81:             var seen = new HashSet<string>(StringComparer.Ordinal);
  82:             var outcomes = new List<HitOutcome>();
  83:             foreach (var contact in batch)
  84:             {
  85:                 var target=contact.Health;
  86:                 var before = target.Current;
  87:                 HitReason reason;
  88:                 if (!seen.Add(target.Id)) reason = HitReason.Duplicate;
  89:                 else if (target.Id == actorId) reason = HitReason.Self;
  90:                 else if (target.Current == 0) reason = HitReason.Dead;
  91:                 else if (contact.Defense.Evading) reason=HitReason.Evaded;
  92:                 else if (contact.Defense.Guarded) reason=HitReason.Guarded;
  93:                 else
  94:                 {
  95:                     target.ApplyDamage(damage);
  96:                     reason = HitReason.Applied;
  97:                 }
  98:                 outcomes.Add(new HitOutcome(reason, before, target, actionId, actorId,contact.Defense.PostureScale));
  99:             }
 100:             return outcomes;
 101:         }
 102:     }
 103: }
```

## Assets/OSSASTRA/Scripts/Characters/Core/DefenseSnapshot.cs
SHA256 6B508F2FB6B8ACA4E8B2296ECC90B012D7475D88591079A33FFF866C815CE91F
```csharp
   1: /* 脚本：DefenseSnapshot
   2:  * 功能：冻结一次合法接触的防御资格；区分闪避、方向盾挡与普通受击。
   3:  * 边界：纯数值决议，不改HP/架势/装备，不查询世界，不持有角色根。
   4:  * 协作：世界网关采样，AttackRelease唯一批次使用；角度点积由适配器计算。
   5:  */
   6: using System;
   7: namespace Ossastra.CharacterCore
   8: {
   9:     public readonly struct DefenseSnapshot
  10:     {
  11:         public bool Evading { get; }
  12:         public bool Guarded { get; }
  13:         public double PostureScale { get; }
  14:         public DefenseSnapshot(bool evading, bool guarding, double incomingDot, double minimumDot, double guardPostureScale)
  15:         {
  16:             if(double.IsNaN(incomingDot)||double.IsInfinity(incomingDot)||double.IsNaN(minimumDot)||minimumDot < -1||minimumDot>1||
  17:                 double.IsNaN(guardPostureScale)||double.IsInfinity(guardPostureScale)||guardPostureScale<0||guardPostureScale>1)
  18:                 throw new ArgumentOutOfRangeException(nameof(incomingDot));
  19:             Evading=evading; Guarded=!evading && guarding && incomingDot>=minimumDot;
  20:             PostureScale=evading?0:Guarded?guardPostureScale:1;
  21:         }
  22:         public static DefenseSnapshot Normal => new DefenseSnapshot(false,false,0,0,1);
  23:     }
  24: }
  25: 
```

## Assets/OSSASTRA/Scripts/Characters/Core/DodgeState.cs
SHA256 CD2914599F9CF2507AC0BC8449EC1D5D91C216039E3D5C77794D1388243B02CA
```csharp
   1: /* 脚本：DodgeState
   2:  * 功能：只按本体模拟时间保存一次闪避及半开免伤窗口。
   3:  * 边界：不驱动物理、不付款；取消立即撤销窗口，结束不保留免伤。
   4:  */
   5: using System;
   6: namespace Ossastra.CharacterCore
   7: {
   8:     public sealed class DodgeState
   9:     {
  10:         private readonly double duration,from,to;
  11:         private double start,now;
  12:         private bool active;
  13:         public bool IsActive=>active;
  14:         public bool IsInvulnerable=>active&&now-start>=from&&now-start<to;
  15:         public DodgeState(double duration,double from,double to)
  16:         {if(!BasicAttackDefinition.Finite(duration)||!BasicAttackDefinition.Finite(from)||!BasicAttackDefinition.Finite(to)||duration<=0||from<0||to<from||to>duration)throw new ArgumentOutOfRangeException(nameof(duration));this.duration=duration;this.from=from;this.to=to;}
  17:         public bool TryStart(double time){if(active||!BasicAttackDefinition.Finite(time))return false;start=now=time;active=true;return true;}
  18:         public void Tick(double time){if(!BasicAttackDefinition.Finite(time)||time<now)throw new ArgumentOutOfRangeException(nameof(time));now=time;if(active&&now-start>=duration)active=false;}
  19:         public void Cancel(){active=false;}
  20:     }
  21: }
```

## Assets/OSSASTRA/Scripts/Characters/Core/EquipmentState.cs
SHA256 3F64FA88AC01400760EB3FAEA91789245838CC4F675CBAD720345517F25A6DDA
```csharp
   1: /* 脚本：EquipmentState
   2:  * 功能：本体左右手独立实例及左手耐久；切换保留原实例，真实释放/防御按身份去重磨损。
   3:  * 边界：不启动动作、不管理CD或背包；调用方验证空闲后才允许切装。
   4:  */
   5: using System;
   6: using System.Collections.Generic;
   7: namespace Ossastra.CharacterCore
   8: {
   9:     public enum OffhandKind { None, Shield, Bomb }
  10:     public sealed class EquipmentState
  11:     {
  12:         private sealed class Item
  13:         {
  14:             public readonly string id=Guid.NewGuid().ToString("N");
  15:             public int durability;
  16:             public readonly HashSet<Guid> wear=new HashSet<Guid>();
  17:         }
  18:         private readonly Dictionary<OffhandKind,Item> items=new Dictionary<OffhandKind,Item>();
  19:         public string OwnerId {get;}
  20:         public string RightInstanceId {get;}
  21:         public OffhandKind LeftKind {get;private set;}
  22:         public string LeftInstanceId=>LeftKind==OffhandKind.None?"none":OwnerId+":left:"+items[LeftKind].id;
  23:         public int Durability=>LeftKind==OffhandKind.None?0:items[LeftKind].durability;
  24:         public EquipmentState(string actorId,int durability)
  25:         {
  26:             if(string.IsNullOrWhiteSpace(actorId)||durability<=0)throw new ArgumentException("Invalid equipment owner/durability");
  27:             OwnerId=actorId;RightInstanceId=actorId+":right:"+Guid.NewGuid().ToString("N");
  28:             items[OffhandKind.Shield]=new Item{durability=durability};items[OffhandKind.Bomb]=new Item{durability=durability};
  29:         }
  30:         public bool TryEquip(OffhandKind kind,bool idle)
  31:         {if(!idle||!Enum.IsDefined(typeof(OffhandKind),kind))return false;LeftKind=kind;return true;}
  32:         public bool Wear(Guid token)
  33:         {
  34:             if(LeftKind==OffhandKind.None||token==Guid.Empty)return false;
  35:             var item=items[LeftKind];if(item.durability<=0||!item.wear.Add(token))return false;
  36:             item.durability--;return true;
  37:         }
  38:     }
  39: }
```

## Assets/OSSASTRA/Scripts/Characters/Core/HealingBottleState.cs
SHA256 71FCF9F2EAC7B9237A00B28D681156FBE0D4526C4ABB57905AD27C4071155A40
```csharp
   1: /* 脚本：HealingBottleState
   2:  * 功能：一次试验世界的共享瓶余额和同步治疗事务。
   3:  * 边界：不持有角色、输入或返场；一次性补给水位不随成员死亡重置。
   4:  */
   5: using System.Collections.Generic;
   6: using System.Linq;
   7: namespace Ossastra.CharacterCore
   8: {
   9:     public sealed class HealingBottleState
  10:     {
  11:         public int Count {get;private set;}=1;
  12:         public int UseCount {get;private set;}
  13:         public bool Refilled {get;private set;}
  14:         public bool TryUse(IEnumerable<Health> members)
  15:         {
  16:             var living=members.Where(h=>h!=null&&h.Current>0).Distinct().ToArray();
  17:             if(Count<=0||!living.Any(h=>h.Current<h.Maximum))return false;
  18:             Count--;UseCount++;foreach(var health in living)health.Heal(100);return true;
  19:         }
  20:         public bool TryRefill(){if(Refilled||Count>=1)return false;Count=1;Refilled=true;return true;}
  21:     }
  22: }
```

## Assets/OSSASTRA/Scripts/Characters/Core/Health.cs
SHA256 A4E3E1BD663EAB8E8A78155CE6246AAC528F5334D97E8524B89139E96F9E5F12
```csharp
   1: /*
   2:  * 脚本：Health
   3:  * 功能：保存目标身份与唯一可写的当前HP，限制伤害结果不低于零。
   4:  * 边界：不管理动画、控制权、敌人AI或正式死亡流程。
   5:  * 协作：AttackRelease经内部ApplyDamage写入；真实目标和开发重置保留同一个Health引用。
   6:  */
   7: using System;
   8: 
   9: namespace Ossastra.CharacterCore
  10: {
  11:     // HP只有本对象能够写；测试目标将来引用同一个Health，不能维护另一份真实HP。
  12:     public sealed class Health
  13:     {
  14:         public string Id { get; }
  15:         public int Maximum { get; }
  16:         public int Current { get; private set; }
  17: 
  18:         public Health(string id, int maximum)
  19:         {
  20:             if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Target identity required", nameof(id));
  21:             if (maximum <= 0) throw new ArgumentOutOfRangeException(nameof(maximum));
  22:             Id = id;
  23:             Maximum = maximum;
  24:             Current = maximum;
  25:         }
  26: 
  27:         /// <summary>唯一治疗入口：返回实际恢复值；正值、存活且不超过上限。</summary>
  28:         public int Heal(int amount)
  29:         { if(amount<=0)throw new ArgumentOutOfRangeException(nameof(amount)); if(Current<=0)return 0; var healed=Math.Min(amount,Maximum-Current);Current+=healed;return healed; }
  30: 
  31:         internal void ApplyDamage(int damage)
  32:         {
  33:             if (damage <= 0) throw new ArgumentOutOfRangeException(nameof(damage));
  34:             Current = Math.Max(0, Current - damage);
  35:         }
  36: 
  37:         /// <summary>显式开发回放入口；不代表生产自动返场，不更换目标身份或Health引用。</summary>
  38:         public void RestoreForDevelopment() { Current=Maximum; }
  39: 
  40:         /// <summary>生命服务经本体验证后恢复同一Health；不更换身份或重播死亡事件。</summary>
  41:         public bool RestoreAfterDeath()
  42:         { if(Current>0) return false; Current=Maximum; return true; }
  43:     }
  44: }
```

## Assets/OSSASTRA/Scripts/Characters/Core/OffhandActionState.cs
SHA256 B17C46F981638580029F817D28D48E82F0E6B182DC46890FC436A5FA2B0D0FD9
```csharp
   1: /* 脚本：OffhandActionState
   2:  * 功能：副手独立接受、单次释放和 SAMPLE 冷却；保留来源身份。
   3:  * 边界：不触碰主手时钟、设备、体力或物理；非实控只冻结冷却，暂停冻结所有推进。
   4:  */
   5: using System;
   6: namespace Ossastra.CharacterCore
   7: {
   8:     public sealed class OffhandActionState
   9:     {
  10:         private readonly string ownerId;
  11:         private double elapsed;
  12:         public Guid ActionId { get; private set; }
  13:         public string EquipmentId { get; private set; }
  14:         public int LifeGeneration { get; private set; }
  15:         public bool IsAccepting { get; private set; }
  16:         public double CooldownRemaining { get; private set; }
  17:         public OffhandActionState(string ownerId) { this.ownerId=ownerId; }
  18:         public bool TryAccept(Guid id,string equipmentId,int lifeGeneration)
  19:         {
  20:             if(id==Guid.Empty||string.IsNullOrEmpty(equipmentId)||IsAccepting||CooldownRemaining>0)return false;
  21:             ActionId=id;EquipmentId=equipmentId;LifeGeneration=lifeGeneration;elapsed=0;IsAccepting=true;CooldownRemaining=5;return true;
  22:         }
  23:         public AttackRelease Advance(double delta,bool controlled,bool paused)
  24:         {
  25:             if(double.IsNaN(delta)||double.IsInfinity(delta)||delta<0)throw new ArgumentOutOfRangeException(nameof(delta));
  26:             if(paused)return null;
  27:             if(controlled)CooldownRemaining=Math.Max(0,CooldownRemaining-delta);
  28:             if(!IsAccepting)return null;
  29:             elapsed+=delta;if(elapsed<.05)return null;
  30:             IsAccepting=false;return new AttackRelease(ActionId,ownerId,45,25);
  31:         }
  32:         public void CancelPending(){IsAccepting=false;}
  33:     }
  34: }
```

## Assets/OSSASTRA/Scripts/Characters/Core/StaminaState.cs
SHA256 B7F72E544AEB61EDD918A3383CE556D1725242FEB3C4AF7B097DEF8DD62774B1
```csharp
   1: /* 脚本：StaminaState
   2:  * 功能：每本体唯一体力余额、按接受身份去重支付、模拟时钟恢复。
   3:  * 边界：不接受动作、不读输入；调用者先验证动作资格再同步付款；暂停不推进时间。
   4:  */
   5: using System;
   6: using System.Collections.Generic;
   7: namespace Ossastra.CharacterCore
   8: {
   9:     public sealed class StaminaState
  10:     {
  11:         private readonly HashSet<Guid> spent=new HashSet<Guid>();
  12:         private readonly double delay,rate;
  13:         private double time,recoverAt;
  14:         public double Current { get; private set; }
  15:         public double Maximum { get; }
  16:         public Guid LastToken { get; private set; }
  17:         public StaminaState(double maximum,double delay,double rate)
  18:         {
  19:             if(!BasicAttackDefinition.Finite(maximum)||maximum<=0||!BasicAttackDefinition.Finite(delay)||delay<0||!BasicAttackDefinition.Finite(rate)||rate<0)throw new ArgumentOutOfRangeException(nameof(maximum));
  20:             Maximum=Current=maximum;this.delay=delay;this.rate=rate;
  21:         }
  22:         public bool CanSpend(double amount)=>BasicAttackDefinition.Finite(amount)&&amount>=0&&Current>=amount;
  23:         public bool TrySpend(Guid token,double amount,double now)
  24:         {
  25:             if(token==Guid.Empty||!CanSpend(amount)||!BasicAttackDefinition.Finite(now)||now<time||spent.Contains(token))return false;
  26:             Current-=amount;spent.Add(token);LastToken=token;recoverAt=now+delay;return true;
  27:         }
  28:         public void Tick(double now,bool canRecover)
  29:         {
  30:             if(!BasicAttackDefinition.Finite(now)||now<time)throw new ArgumentOutOfRangeException(nameof(now));
  31:             if(canRecover)Current=Math.Min(Maximum,Current+Math.Max(0,now-Math.Max(time,recoverAt))*rate);
  32:             time=now;
  33:         }
  34:         public void Restore(){Current=Maximum;recoverAt=time+delay;}
  35:         /// <summary>连续举盾消耗不制造每帧动作token；余额耗尽立即返回false且不变负数。</summary>
  36:         public bool Drain(double amount,double now)
  37:         {
  38:             if(!BasicAttackDefinition.Finite(amount)||amount<0||!BasicAttackDefinition.Finite(now)||now<time)throw new ArgumentOutOfRangeException(nameof(amount));
  39:             Current=Math.Max(0,Current-amount);if(amount>0)recoverAt=now+delay;return Current>0;
  40:         }
  41:     }
  42: }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterContracts.cs
SHA256 FD591B162654E08771B0275022955EB97115ACC6486B47E8F0413FE4306D8426
```csharp
   1: /*
   2:  * 脚本：CharacterContracts
   3:  * 功能：集中声明角色领域的窄职责契约，供组合根和DataBus依赖。
   4:  * 边界：只定义能力；实际实现与调用顺序不在本文件。
   5:  * 协作：Inspector仍保存具体组件；Root运行时按接口连接，只有必要模块接受Bus初始化。
   6:  */
   7: using Ossastra.CharacterCore;
   8: using Ossastra.Combat;
   9: using UnityEngine;
  10: using System.Collections.Generic;
  11: 
  12: namespace Ossastra.Characters
  13: {
  14:     /// <summary>副手未释放前的空闲资格；在途派生不占据主手动作时钟。</summary>
  15:     public interface ICharacterOffhandStatus { bool IsAccepting {get;} }
  16:     /// <summary>本体体力只读视图；付款仅经本体资源模块，不在DataBus复制余额。</summary>
  17:     public interface ICharacterStamina
  18:     {
  19:         double Current { get; }
  20:         double Maximum { get; }
  21:     }
  22:     /// <summary>原运动执行器读取机动速度；能力不直接调用Move或修改位置。</summary>
  23:     public interface ICharacterMotionOverride
  24:     {
  25:         bool IsActive { get; }
  26:         Vector3 MotionVelocity { get; }
  27:     }
  28:     /// <summary>伙伴战斗策略提供移动候选；选择器与Root保留有效输入及实际运动执行权。</summary>
  29:     public interface ICompanionMoveCandidate
  30:     {
  31:         bool TryGetCombatMove(out Vector2 move);
  32:     }
  33:     /// <summary>伙伴战斗策略提供攻击目标候选；Root决定是否受理并执行动作。</summary>
  34:     public interface ICompanionActionCandidate
  35:     {
  36:         bool TryGetAttackTarget(out CombatVitals target);
  37:         /// <summary>Root受理当前候选后通知策略开始冷却；候选本身不启动动作。</summary>
  38:         void OnAccepted();
  39:     }
  40:     /// <summary>队伍只分配合法目标；角色能力自行计算移动，不从此契约取得方向或策略状态。</summary>
  41:     public interface ICharacterFollowTargetSource
  42:     {
  43:         bool TryGetFollowTarget(CharacterRoot follower, out CharacterRoot target);
  44:     }
  45:     /// <summary>场景唯一控制权的只读视图；代际和共享屏障同时约束玩家与伙伴来源。</summary>
  46:     public interface ICharacterControlSession
  47:     {
  48:         CharacterRoot Current { get; }
  49:         IReadOnlyList<CharacterRoot> Characters { get; }
  50:         bool IsIntentSamplingAllowed { get; }
  51:         int ControlRevision { get; }
  52:     }
  53:     /// <summary>伙伴策略只提供移动候选帧；选择器剥离攻击、暂停、取消和开发命令。</summary>
  54:     public interface ICompanionIntentSource
  55:     {
  56:         bool IsAvailable { get; }
  57:         CharacterInputFrame ReadCompanionIntent(CharacterRoot character, float delta);
  58:     }
  59:     /// <summary>玩家源的实时许可；外层已采样帧在许可撤回时同样失效，兼容未挂跟随模块的旧场。</summary>
  60:     public interface ICharacterInputAvailability
  61:     {
  62:         bool HasPlayerInput { get; }
  63:     }
  64:     /// <summary>由场景实控权威回答谁可以采样玩家输入；输入源不保存第二份选中状态。</summary>
  65:     public interface ICharacterPlayerControlSource
  66:     {
  67:         bool IsPlayerInputAllowed(ICharacterInputSource source);
  68:     }
  69:     /// <summary>仅为确需角色Bus的模块提供显式初始化；失败阻止角色启动。</summary>
  70:     public interface ICharacterSubsystem { bool Initialize(CharacterDataBus dataBus); }
  71:     /// <summary>提供单角色唯一有效输入快照，不判定动作是否成功。</summary>
  72:     public interface ICharacterInputSource
  73:     {
  74:         CharacterInputFrame CurrentFrame { get; }
  75:         bool IsConfigured { get; }
  76:         CharacterInputFrame Read();
  77:     }
  78:     /// <summary>执行实际运动并报告结果；不决定攻击朝向。</summary>
  79:     public interface ICharacterMovement : ICharacterSubsystem
  80:     {
  81:         bool IsMoving { get; }
  82:         Vector3 Velocity { get; }
  83:         void Tick(bool allowed, float delta);
  84:         void Teleport(Vector3 position);
  85:     }
  86:     /// <summary>分别维护攻击方向与可见翻转，由Root显式调度。</summary>
  87:     public interface ICharacterOrientation
  88:     {
  89:         Vector3 AttackDirection { get; }
  90:         float VisualFacingX { get; }
  91:         void ResolveDirections(Vector2 moveInput, Vector3? pointerDirection, Vector2 stickDirection, bool movementFacing);
  92:         void OrientTowards(Vector3 worldPosition);
  93:         void FaceAttackDirection();
  94:     }
  95:     /// <summary>从角色状态更新表现与暂停，不反向产生命中。</summary>
  96:     public interface ICharacterPresentation : ICharacterSubsystem
  97:     {
  98:         string CurrentClip { get; }
  99:         void SetPaused(bool paused);
 100:         void Present();
 101:     }
 102: }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterDataBus.cs
SHA256 7DCEFC1720CF9C98580B25E2E447018E5B38F3FBF10959D3621D6A6061B8240D
```csharp
   1: /*
   2:  * 脚本：CharacterDataBus
   3:  * 功能：为单个角色提供输入、运动、朝向、动作和权限的只读访问门面。
   4:  * 边界：状态仍由原模块唯一写入；不复制可写HP、动作时钟或全局控制状态。
   5:  * 协作：Root创建；Movement／Spine及消费者实时读取接口拥有者。
   6:  */
   7: using System;
   8: using Ossastra.CharacterCore;
   9: using UnityEngine;
  10: 
  11: namespace Ossastra.Characters
  12: {
  13:     // Per-character read facade; referenced modules remain the sole mutable-state owners.
  14:     public sealed class CharacterDataBus
  15:     {
  16:         private readonly ICharacterInputSource input;
  17:         private readonly ICharacterMovement movement;
  18:         private readonly ICharacterOrientation orientation;
  19:         private readonly BasicActionRuntime actionRuntime;
  20:         private readonly ControlAuthority controlAuthority;
  21: 
  22:         public string ActorId { get; }
  23:         public CharacterInputFrame Input => input.CurrentFrame;
  24:         public bool IsMoving => movement.IsMoving;
  25:         public Vector3 Velocity => movement.Velocity;
  26:         public Vector3 AttackDirection => orientation.AttackDirection;
  27:         public float VisualFacingX => orientation.VisualFacingX;
  28:         public bool ActionActive => actionRuntime.IsActive;
  29:         public Guid ActionId => actionRuntime.ActionId;
  30:         public bool MoveReady => actionRuntime.MoveReady;
  31:         public bool AttackReady => actionRuntime.AttackReady;
  32:         public bool AllowsMovement => controlAuthority.Allows(ControlChannel.Move);
  33:         public bool AllowsAttack => controlAuthority.Allows(ControlChannel.Attack);
  34:         public ICharacterStamina Stamina {get;private set;}
  35:         public ICharacterMotionOverride MotionOverride {get;private set;}
  36:         internal void BindResources(ICharacterStamina stamina,ICharacterMotionOverride motion){Stamina=stamina;MotionOverride=motion;}
  37: 
  38:         public CharacterDataBus(string actorId, ICharacterInputSource input, ICharacterMovement movement,
  39:             ICharacterOrientation orientation, BasicActionRuntime actionRuntime, ControlAuthority controlAuthority)
  40:         {
  41:             if (string.IsNullOrWhiteSpace(actorId)) throw new ArgumentException("Actor identity required", nameof(actorId));
  42:             if (input == null) throw new ArgumentNullException(nameof(input));
  43:             if (movement == null) throw new ArgumentNullException(nameof(movement));
  44:             if (orientation == null) throw new ArgumentNullException(nameof(orientation));
  45:             if (actionRuntime == null) throw new ArgumentNullException(nameof(actionRuntime));
  46:             if (controlAuthority == null) throw new ArgumentNullException(nameof(controlAuthority));
  47:             ActorId = actorId;
  48:             this.input = input;
  49:             this.movement = movement;
  50:             this.orientation = orientation;
  51:             this.actionRuntime = actionRuntime;
  52:             this.controlAuthority = controlAuthority;
  53:         }
  54:     }
  55: }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterDodge.cs
SHA256 FBCFB1130FD7523EF353F66CB5DAD091C28F07AE604F87F8E724D54607670F1C
```csharp
   1: /* 脚本：CharacterDodge
   2:  * 功能：Root调用的有限闪避请求、方向快照与机动速度；无敌只在配置窗口内。
   3:  * 边界：不自行Update/Move，不重置主手时钟；所有参数为SAMPLE。
   4:  */
   5: using System;
   6: using Ossastra.CharacterCore;
   7: using Ossastra.Combat;
   8: using UnityEngine;
   9: namespace Ossastra.Characters
  10: {
  11:     [DisallowMultipleComponent]
  12:     public sealed class CharacterDodge:MonoBehaviour,ICharacterMotionOverride,ICombatEvasion
  13:     {
  14:         [SerializeField,Min(.01f)] private float duration=.45f,distance=1.5f;
  15:         [SerializeField,Min(0)] private float invulnerableFrom=.12f,invulnerableTo=.32f,cost=22;
  16:         private DodgeState state;
  17:         private CharacterRoot owner;
  18:         private CharacterStamina stamina;
  19:         private Vector3 direction;
  20:         public Guid ActionId {get;private set;}
  21:         public string LastResult {get;private set;}="无";
  22:         public bool IsActive=>isActiveAndEnabled&&state!=null&&state.IsActive;
  23:         public bool IsEvading=>IsActive&&owner!=null&&owner.CanSimulateIntent&&state.IsInvulnerable;
  24:         public Vector3 MotionVelocity=>IsActive?direction*(distance/duration):Vector3.zero;
  25:         public void Initialize(CharacterRoot root,CharacterStamina resource){owner=root;stamina=resource;state=new DodgeState(duration,invulnerableFrom,invulnerableTo);}
  26:         public bool Request(Vector2 move,bool playerInput)
  27:         {
  28:             if(!isActiveAndEnabled||state==null||owner==null||!owner.CanSimulateIntent||!playerInput||IsActive||
  29:                 !owner.DataBus.AllowsMovement||!owner.DataBus.MoveReady||stamina==null||!stamina.CanSpend(cost))
  30:             {LastResult="拒绝：控制/动作/体力资格";return false;}
  31:             direction=move.sqrMagnitude>.0001f?new Vector3(move.x,0,move.y).normalized:owner.DataBus.AttackDirection;
  32:             direction.y=0;if(direction.sqrMagnitude<.0001f){LastResult="无可靠方向";return false;}
  33:             direction.Normalize();var token=Guid.NewGuid();
  34:             if(!stamina.Spend(token,cost,owner.SimulationTime,"dodge"))return false;
  35:             if(!state.TryStart(owner.SimulationTime))throw new InvalidOperationException("Validated dodge acceptance failed");
  36:             ActionId=token;LastResult="Accepted SAMPLE，未配置专用动画";return true;
  37:         }
  38:         public void Tick(double time){state?.Tick(time);}
  39:         public void Cancel(){state?.Cancel();}
  40:         private void OnDisable(){Cancel();}
  41:     }
  42: }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterEquipment.cs
SHA256 DBB89014C4074455C721CFD58C8EAA154C0B1D2B9A939D3F03746D811BF8B30A
```csharp
   1: /* 脚本：CharacterEquipment
   2:  * 功能：显式初始化本体独立装备；Inspector初始左手仅用于新场样板。
   3:  * 边界：不控制主手资格，不重置耐久/CD；开发切装也必须本体存活空闲。
   4:  */
   5: using Ossastra.CharacterCore;
   6: using UnityEngine;
   7: namespace Ossastra.Characters
   8: {
   9:     [DisallowMultipleComponent]
  10:     public sealed class CharacterEquipment:MonoBehaviour
  11:     {
  12:         [SerializeField] private OffhandKind initialOffhand=OffhandKind.Shield;
  13:         [SerializeField,Min(1)] private int initialDurability=100;
  14:         private CharacterRoot owner;
  15:         public EquipmentState State {get;private set;}
  16:         public string LastResult {get;private set;}="无";
  17:         public void Initialize(CharacterRoot root)
  18:         {owner=root;if(State==null){State=new EquipmentState(root.ActorId,initialDurability);State.TryEquip(initialOffhand,true);}}
  19:         public bool TryEquip(OffhandKind kind)
  20:         {
  21:             var guard=GetComponent<ShieldGuard>();
  22:             var idle=isActiveAndEnabled&&owner!=null&&owner.CanSimulateIntent&&!owner.ActionActive&&
  23:                 (owner.DataBus.MotionOverride==null||!owner.DataBus.MotionOverride.IsActive)&&(guard==null||!guard.IsGuarding);
  24:             // 副手提供独立空闲资格；不认识/修改主手时钟。
  25:             foreach(var candidate in GetComponents<MonoBehaviour>())if(candidate is ICharacterOffhandStatus offhand && offhand.IsAccepting)idle=false;
  26:             var ok=State!=null&&State.TryEquip(kind,idle);LastResult=ok?"装备 "+kind:"拒绝：非空闲";return ok;
  27:         }
  28:     }
  29: }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterInputHandler.cs
SHA256 96CC82E6241E8923AC0585649F26F465A569ACEB0585E4498D0A692F4EA3137F
```csharp
   1: /*
   2:  * 脚本：CharacterInputHandler
   3:  * 功能：从Inspector配置的InputAction采样单角色输入快照。
   4:  * 边界：只采输入；不接受动作、不驱动物理或写HP；每实例管理Action生命周期。
   5:  * 协作：选择器经接口读取；死亡交接额外等待移动中立，普通Tab仍保留原移动语义。
   6:  */
   7: using UnityEngine.EventSystems;
   8: using System.Collections.Generic;
   9: using UnityEngine;
  10: using UnityEngine.InputSystem;
  11: 
  12: namespace Ossastra.Characters
  13: {
  14:     public struct CharacterInputFrame
  15:     {
  16:         public Vector2 move, pointer, aimDirection;
  17:         public bool attackHeld, dodgePressed, offhandHeld, offhandPressed, cancel, pause, hasPointer, hasDirectionalAim, attackPointerBlocked;
  18:         public int debugCommand;
  19:     }
  20: 
  21:     // Each character owns its serialized actions. Input does not accept attacks or write gameplay state.
  22:     [DisallowMultipleComponent]
  23:     public sealed class CharacterInputHandler : MonoBehaviour, ICharacterInputSource, ICharacterInputAvailability
  24:     {
  25:         [Header("移动 / VELGITA movementAction")]
  26:         public InputAction movementAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
  27:         [Header("鼠标位置 / VELGITA mouseAction")]
  28:         public InputAction mouseAction = new InputAction("Pointer", InputActionType.Value, expectedControlType: "Vector2");
  29:         [Header("手柄瞄准方向 / VELGITA gamePad_MouseAction")]
  30:         public InputAction gamepadAimAction = new InputAction("GamepadAim", InputActionType.Value, expectedControlType: "Vector2");
  31:         [Header("当前普攻 / VELGITA HoundWeaponAttack_Action")]
  32:         public InputAction attackAction = new InputAction("Attack", InputActionType.Button);
  33:         [Header("可选本体闪避 / 只在新验证场绑定")]
  34:         public InputAction dodgeAction = new InputAction("Dodge", InputActionType.Button);
  35:         public InputAction offhandAction = new InputAction("Offhand", InputActionType.Button);
  36:         [Header("取消与暂停")]
  37:         public InputAction cancelAction = new InputAction("Cancel", InputActionType.Button);
  38:         public InputAction pauseAction = new InputAction("Pause", InputActionType.Button);
  39:         [Header("开发检查入口")]
  40:         public InputAction debugValidAction = new InputAction("DebugValid", InputActionType.Button);
  41:         public InputAction debugBlockedAction = new InputAction("DebugBlocked", InputActionType.Button);
  42:         public InputAction debugResetAction = new InputAction("DebugReset", InputActionType.Button);
  43:         public InputAction debugInspectAction = new InputAction("DebugInspect", InputActionType.Button);
  44: 
  45:         private readonly Dictionary<object, Rect> pointerRegions = new Dictionary<object, Rect>();
  46:         private ICharacterPlayerControlSource playerControl;
  47:         private bool managedPlayerInput, attackArmed = true;
  48:         private bool movementArmed=true;
  49:         private bool dodgeArmed;
  50:         private bool offhandArmed;
  51: 
  52:         public bool HasPlayerInput => !managedPlayerInput || (ControlSourceAlive && playerControl.IsPlayerInputAllowed(this));
  53:         private bool ControlSourceAlive => playerControl != null &&
  54:             (!(playerControl is Object unityObject) || unityObject != null);
  55: 
  56:         public bool CanBindPlayerControl(ICharacterPlayerControlSource source)
  57:             => source != null && (!managedPlayerInput || !ControlSourceAlive || ReferenceEquals(playerControl, source));
  58: 
  59:         /// <summary>绑定唯一场景权威；拒绝另一存活协调者抢占同一输入。</summary>
  60:         public bool BindPlayerControl(ICharacterPlayerControlSource source)
  61:         {
  62:             if (!CanBindPlayerControl(source)) return false;
  63:             playerControl = source;
  64:             managedPlayerInput = true;
  65:             ResetPlayerInput();
  66:             return true;
  67:         }
  68: 
  69:         /// <summary>移交时清掉旧快照；不假装一次松键，必须真正观察释放后才重武装攻击。</summary>
  70:         public void ResetPlayerInput(bool requireMovementRelease=false)
  71:         { CurrentFrame=default; attackArmed=false; dodgeArmed=false; offhandArmed=false; if (requireMovementRelease) movementArmed=false; }
  72:         public void RegisterPointerRegion(object owner, Rect region) { pointerRegions[owner] = region; }
  73:         public void RemovePointerRegion(object owner) { pointerRegions.Remove(owner); }
  74: 
  75:         private bool IsPointerBlocked(Vector2 pointer)
  76:         {
  77:             if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return true;
  78:             var guiPoint = new Vector2(pointer.x, Screen.height - pointer.y);
  79:             foreach (var region in pointerRegions.Values) if (region.Contains(guiPoint)) return true;
  80:             return false;
  81:         }
  82: 
  83:         public CharacterInputFrame CurrentFrame { get; private set; }
  84: 
  85:         public bool IsConfigured => movementAction != null && movementAction.bindings.Count > 0 &&
  86:             attackAction != null && attackAction.bindings.Count > 0 &&
  87:             ((mouseAction != null && mouseAction.bindings.Count > 0) ||
  88:              (gamepadAimAction != null && gamepadAimAction.bindings.Count > 0));
  89: 
  90:         private IEnumerable<InputAction> Actions()
  91:         {
  92:             // 同步枚举仅统一启用/禁用/释放清单；不是Unity协程，没有跨帧等待。
  93:             yield return movementAction;
  94:             yield return mouseAction;
  95:             yield return gamepadAimAction;
  96:             yield return attackAction;
  97:             yield return dodgeAction;
  98:             yield return offhandAction;
  99:             yield return cancelAction;
 100:             yield return pauseAction;
 101:             yield return debugValidAction;
 102:             yield return debugBlockedAction;
 103:             yield return debugResetAction;
 104:             yield return debugInspectAction;
 105:         }
 106: 
 107:         private void OnEnable() { foreach (var action in Actions()) if (action != null) action.Enable(); }
 108:         private void OnDisable() { ResetPlayerInput(); foreach (var action in Actions()) if (action != null) action.Disable(); }
 109:         private void OnDestroy() { foreach (var action in Actions()) if (action != null) action.Dispose(); }
 110: 
 111:         public CharacterInputFrame Read()
 112:         {
 113:             var frame = new CharacterInputFrame();
 114:             // 非实控在读取设备前返回空帧；Root仍推进自己已接受的动作，不会偷读另一角色输入。
 115:             if (!HasPlayerInput || !isActiveAndEnabled || movementAction == null || !movementAction.enabled)
 116:             { CurrentFrame = frame; return frame; }
 117:             frame.move = movementAction.ReadValue<Vector2>();
 118:             if (!movementArmed)
 119:             {
 120:                 // 死亡交接不能把旧WASD/摇杆位移转发给幸存者；必须真实回中才重新采移动。
 121:                 if (frame.move.sqrMagnitude<.0001f) movementArmed=true;
 122:                 frame.move=Vector2.zero;
 123:             }
 124:             if (mouseAction != null && mouseAction.enabled)
 125:             {
 126:                 frame.pointer = mouseAction.ReadValue<Vector2>();
 127:                 frame.hasPointer = mouseAction.controls.Count > 0 && frame.pointer.x >= 0 && frame.pointer.x <= Screen.width &&
 128:                     frame.pointer.y >= 0 && frame.pointer.y <= Screen.height && !IsPointerBlocked(frame.pointer);
 129:             }
 130:             if (gamepadAimAction != null && gamepadAimAction.enabled)
 131:             {
 132:                 frame.aimDirection = gamepadAimAction.ReadValue<Vector2>();
 133:                 frame.hasDirectionalAim = frame.aimDirection.sqrMagnitude > .01f;
 134:             }
 135:             var held = attackAction != null && attackAction.enabled && attackAction.IsPressed();
 136:             if (!held) attackArmed = true;
 137:             frame.attackHeld = held && attackArmed;
 138:             var dodgeHeld=dodgeAction!=null&&dodgeAction.enabled&&dodgeAction.IsPressed();
 139:             if(!dodgeHeld)dodgeArmed=true;
 140:             frame.dodgePressed=dodgeHeld&&dodgeArmed&&Pressed(dodgeAction);
 141:             if(frame.dodgePressed)dodgeArmed=false;
 142:             var offhandHeld=offhandAction!=null&&offhandAction.enabled&&offhandAction.IsPressed();
 143:             if(!offhandHeld)offhandArmed=true;
 144:             frame.offhandHeld=offhandHeld&&offhandArmed;
 145:             frame.offhandPressed=frame.offhandHeld&&Pressed(offhandAction);
 146:             if(offhandAction!=null&&offhandAction.activeControl!=null&&offhandAction.activeControl.device is Pointer&&!frame.hasPointer)
 147:             {frame.offhandHeld=false;frame.offhandPressed=false;}
 148:             if (attackAction != null && attackAction.activeControl != null && attackAction.activeControl.device is Pointer &&
 149:                 !frame.hasPointer) frame.attackPointerBlocked = true;
 150:             frame.cancel = Pressed(cancelAction);
 151:             frame.pause = Pressed(pauseAction);
 152:             frame.debugCommand = Pressed(debugValidAction) ? 1 : Pressed(debugBlockedAction) ? 2 :
 153:                 Pressed(debugResetAction) ? 3 : Pressed(debugInspectAction) ? 4 : 0;
 154:             CurrentFrame = frame;
 155:             return frame;
 156:         }
 157: 
 158:         private static bool Pressed(InputAction action) => action != null && action.enabled && action.WasPressedThisFrame();
 159:     }
 160: }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterMovement.cs
SHA256 1D4D8206FD1B671EBCFE2A251C0A7A1D9B16B5C83A92DC00E7B308A504789012
```csharp
   1: /*
   2:  * 脚本：CharacterMovement
   3:  * 功能：依据角色输入与移动许可执行XZ位移、碰撞及贴地。
   4:  * 边界：只管理运动；不决定攻击方向、不结算伤害。
   5:  * 协作：Root初始化Bus并调用Tick；CharacterController实际位移决定速度和Run资格。
   6:  */
   7: using System;
   8: using UnityEngine;
   9: 
  10: namespace Ossastra.Characters
  11: {
  12:     [DisallowMultipleComponent]
  13:     [RequireComponent(typeof(CharacterController))]
  14:     public sealed class CharacterMovement : MonoBehaviour, ICharacterMovement
  15:     {
  16:         [SerializeField, Min(.01f)] private float speed = 3;
  17:         [SerializeField, Min(.01f)] private float groundStick = 2;
  18:         private CharacterController body;
  19:         private CharacterDataBus dataBus;
  20:         public bool IsMoving { get; private set; }
  21:         public Vector3 Velocity { get; private set; }
  22: 
  23:         public bool Initialize(CharacterDataBus dataBus)
  24:         {
  25:             if (dataBus == null) throw new ArgumentNullException(nameof(dataBus));
  26:             this.dataBus = dataBus;
  27:             body = GetComponent<CharacterController>();
  28:             return body != null;
  29:         }
  30: 
  31:         public void Tick(bool allowed, float delta)
  32:         {
  33:             IsMoving = false;
  34:             Velocity = Vector3.zero;
  35:             if (dataBus == null || body == null || !body.enabled || delta <= 0) return;
  36:             var move = allowed ? Vector2.ClampMagnitude(dataBus.Input.move, 1) : Vector2.zero;
  37:             var before = transform.position;
  38:             // 平时与闪避共同走这一处Move；覆盖速度仍接受本体移动权限和真实墙体碰撞。
  39:             var velocity=allowed&&dataBus.MotionOverride!=null&&dataBus.MotionOverride.IsActive
  40:                 ?dataBus.MotionOverride.MotionVelocity:new Vector3(move.x,0,move.y)*speed;
  41:             body.Move((velocity + Vector3.down * groundStick) * delta);
  42:             // 用碰撞处理后的实际位移报告运动，而不是把“按住移动键”当成真的跑动。
  43:             var displacement = transform.position - before;
  44:             displacement.y = 0;
  45:             Velocity = displacement / delta;
  46:             IsMoving = displacement.sqrMagnitude > .000001f;
  47:         }
  48: 
  49:         public void Teleport(Vector3 position)
  50:         {
  51:             if (body == null) return;
  52:             var wasEnabled = body.enabled;
  53:             body.enabled = false;
  54:             transform.position = position;
  55:             body.enabled = wasEnabled;
  56:             IsMoving = false;
  57:             Velocity = Vector3.zero;
  58:         }
  59:     }
  60: }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterOffhand.cs
SHA256 0238347A436B81617592EDAB608B67917814F65A59BBFEEC5DB9ADC8C76D9187
```csharp
   1: /* 脚本：CharacterOffhand
   2:  * 功能：本体副手炸弹请求、费用、冻结来源与演示投掷体生命周期。
   3:  * 边界：Root显式推进；主手Runtime完全独立；伤害只进入CombatContactGateway。
   4:  * 协作：Equipment持有实例与耐久，Stamina付款；几何球仅技术演示，非正式美术。
   5:  */
   6: using System;
   7: using System.Linq;
   8: using UnityEngine;
   9: using Ossastra.CharacterCore;
  10: using Ossastra.Combat;
  11: namespace Ossastra.Characters
  12: {
  13:     [DisallowMultipleComponent]
  14:     public sealed class CharacterOffhand:MonoBehaviour,ICharacterOffhandStatus
  15:     {
  16:         private CharacterRoot owner;
  17:         private CharacterEquipment gear;
  18:         private CharacterStamina stamina;
  19:         private OffhandActionState state;
  20:         private Vector3 origin,landing;
  21:         private AttackRelease flight;
  22:         private GameObject visual;
  23:         private float flightTime;
  24:         private int flightGeneration;
  25:         public bool IsAccepting=>state!=null&&state.IsAccepting;
  26:         public double CooldownRemaining=>state==null?0:state.CooldownRemaining;
  27:         public bool InFlight=>flight!=null;
  28:         public int AcceptedCount {get;private set;}
  29:         public int ReleasedCount {get;private set;}
  30:         public int LandedCount {get;private set;}
  31:         public string LastResult {get;private set;}="未投掷";
  32:         public string FrozenEquipmentId {get;private set;}
  33:         public Guid LastActionId {get;private set;}
  34:         public void Initialize(CharacterRoot root,CharacterEquipment equipment,CharacterStamina resource)
  35:         {owner=root;gear=equipment;stamina=resource;state=new OffhandActionState(root.ActorId);}
  36:         public bool Request(Vector3? point,bool controlled)
  37:         {
  38:             if(!isActiveAndEnabled||owner==null||!owner.CanSimulateIntent||!controlled||gear==null||!gear.isActiveAndEnabled||gear.State.LeftKind!=OffhandKind.Bomb||gear.State.Durability<=0)
  39:                 return Reject("副手装备/控制资格不足");
  40:             if(IsAccepting||InFlight||CooldownRemaining>0)return Reject("副手冷却中");
  41:             if(point==null)return Reject("鼠标不是世界落点");
  42:             var p=point.Value;var delta=p-owner.transform.position;delta.y=0;
  43:             if(float.IsNaN(p.x)||float.IsInfinity(p.x)||float.IsNaN(p.z)||float.IsInfinity(p.z)||delta.magnitude>4.5f)return Reject("超出4.5范围");
  44:             // 脚下平面投影之后必须再证实地面；不把墙后或无地面的点自动修正为合法点。
  45:             var floor=Physics.RaycastAll(p+Vector3.up*.4f,Vector3.down,.8f,~0,QueryTriggerInteraction.Ignore)
  46:                 .Where(h=>h.normal.y>.7f&&h.collider.GetComponentInParent<CombatVitals>()==null).OrderBy(h=>h.distance).ToArray();
  47:             if(floor.Length==0)return Reject("落点无可用地面");
  48:             p=floor[0].point;
  49:             if(!ClearPath(owner.transform.position,p))return Reject("落点被墙遮挡");
  50:             if(stamina==null||!stamina.CanSpend(20))return Reject("体力不足");
  51:             var id=Guid.NewGuid();if(!state.TryAccept(id,gear.State.LeftInstanceId,owner.LifeGeneration))return Reject("副手拒绝请求");
  52:             if(!stamina.Spend(id,20,owner.SimulationTime,"offhand-bomb")){state.CancelPending();throw new InvalidOperationException("Accepted offhand payment failed");}
  53:             origin=owner.transform.position;landing=p;FrozenEquipmentId=state.EquipmentId;LastActionId=id;AcceptedCount++;LastResult="Accepted 炸弹 SAMPLE";return true;
  54:         }
  55:         private bool Reject(string reason){LastResult="拒绝："+reason;return false;}
  56:         public void Tick(float delta,bool controlled)
  57:         {
  58:             if(owner==null||state==null)return;
  59:             if(!isActiveAndEnabled||!owner.IsAlive){CancelAll();return;}
  60:             if(owner.IsSimulationSuspended)return;
  61:             if(!owner.CanSimulateIntent||!controlled)state.CancelPending();
  62:             var released=state.Advance(delta,controlled,false);
  63:             if(released!=null)
  64:             {
  65:                 if(gear.State.LeftInstanceId!=state.EquipmentId||owner.LifeGeneration!=state.LifeGeneration){LastResult="释放身份失效";return;}
  66:                 gear.State.Wear(released.ActionId);flight=released;flightGeneration=owner.LifeGeneration;flightTime=0;ReleasedCount++;
  67:                 visual=GameObject.CreatePrimitive(PrimitiveType.Sphere);visual.name="SAMPLE_Bomb_"+released.ActionId.ToString("N");visual.transform.localScale=Vector3.one*.16f;
  68:                 var collider=visual.GetComponent<Collider>();collider.enabled=false;Destroy(collider);visual.transform.position=origin+Vector3.up*.3f;
  69:                 LastResult="Released #"+released.ActionId.ToString("N");return;
  70:             }
  71:             if(flight==null)return;
  72:             if(owner.LifeGeneration!=flightGeneration){CancelAll();return;}
  73:             flightTime+=delta;var t=Mathf.Clamp01(flightTime/.45f);
  74:             if(visual!=null)visual.transform.position=Vector3.Lerp(origin,landing,t)+Vector3.up*(.15f+Mathf.Sin(t*Mathf.PI));
  75:             if(t<1)return;
  76:             // 再查发射至落点及爆点至目标的直线遮挡。并非连续扫掠式物理弹道。
  77:             var candidates=ClearPath(origin,landing)?Physics.OverlapSphere(landing,1.25f,~0,QueryTriggerInteraction.Ignore)
  78:                 .Select(c=>c.GetComponentInParent<CombatVitals>()).Where(v=>v!=null&&ClearPath(landing,v.transform.position)).Distinct().Cast<IHitTarget>().ToArray():Array.Empty<IHitTarget>();
  79:             var receipts=CombatContactGateway.Resolve(flight,owner.Vitals,candidates,landing);
  80:             LandedCount++;LastResult="落地 #"+flight.ActionId.ToString("N")+" 接触 "+receipts.Count;ClearFlight();
  81:         }
  82:         private static bool ClearPath(Vector3 a,Vector3 b)
  83:         {
  84:             a+=Vector3.up*.25f;b+=Vector3.up*.25f;var d=b-a;
  85:             foreach(var hit in Physics.RaycastAll(a,d,d.magnitude,~0,QueryTriggerInteraction.Ignore))
  86:                 if(hit.collider.GetComponentInParent<CombatVitals>()==null)return false;
  87:             return true;
  88:         }
  89:         public void CancelPending(){state?.CancelPending();}
  90:         public void CancelAll(){state?.CancelPending();ClearFlight();}
  91:         private void ClearFlight(){flight=null;if(visual!=null)Destroy(visual);visual=null;}
  92:         private void OnDisable(){CancelAll();}
  93:     }
  94: }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterRoot.cs
SHA256 1876F79C5C0B9042C60D46B6B203570615DD9DB3E189DAB75A9F705002137CFC
```csharp
   1: /*
   2:  * 脚本：CharacterRoot
   3:  * 功能：组织单角色的接口接线、输入、动作事实、运动和表现。
   4:  * 边界：持有本角色运行时；只读生命资格，不实现HP写入、设备绑定或开发GUI。
   5:  * 协作：初始化可选本体Follow/Posture；显式按输入、架势、朝向、动作、运动、表现推进。
   6:  */
   7: using System;
   8: using Ossastra.CharacterCore;
   9: using Ossastra.Combat;
  10: using UnityEngine;
  11: using UnityEngine.Serialization;
  12: 
  13: namespace Ossastra.Characters
  14: {
  15:     [DefaultExecutionOrder(-100)]
  16:     [RequireComponent(typeof(CharacterMovement), typeof(CombatMeleeHit), typeof(CharacterInputHandler))]
  17:     [RequireComponent(typeof(CharacterOrientation))]
  18:     public sealed class CharacterRoot : MonoBehaviour
  19:     {
  20:         [SerializeField] private string actorId = "player";
  21:         [FormerlySerializedAs("motor"), SerializeField] private CharacterMovement movement;
  22:         [SerializeField] private CharacterOrientation orientation;
  23:         [SerializeField] private CharacterSpineView view;
  24:         [SerializeField] private CombatMeleeHit melee;
  25:         [SerializeField] private CharacterInputHandler input;
  26:         [SerializeField] private Camera viewCamera;
  27:         [SerializeField] private CharacterFollow follow;
  28:         [SerializeField] private CombatVitals vitals;
  29:         [SerializeField] private CombatPosture posture;
  30:         private ICombatLifeState lifeState;
  31:         private CompanionCombatBrain combatBrain;
  32:         private CharacterStamina stamina;
  33:         private CharacterDodge dodge;
  34:         private CharacterEquipment equipment;
  35:         private ShieldGuard guard;
  36:         private CharacterOffhand offhand;
  37:         private CombatVitals autonomousTarget;
  38:         private bool autonomousAction;
  39:         public event Action<ActionFact> ActionObserved;
  40:         public double SimulationTime => runtime == null ? 0 : runtime.SimulationTime;
  41:         public ICompanionMoveCandidate CombatMove => combatBrain;
  42:         [Header("Prototype timing — not final balance")]
  43:         [SerializeField] private float releaseAt = .3f, moveReadyAt = .5f, attackReadyAt = .8f, finishAt = 1.1f;
  44:         [SerializeField, Min(1)] private int damage = 12;
  45:         [SerializeField, Min(0)] private float postureImpact;
  46:         [SerializeField] private bool verboseTrace;
  47:         private readonly ControlAuthority authority = new ControlAuthority();
  48:         private readonly AttackIntent intent = new AttackIntent();
  49:         private ICharacterInputSource inputSource;
  50:         private CharacterIntentSelector intentSelector;
  51:         private ICharacterMovement mover;
  52:         private ICharacterOrientation orienter;
  53:         private ICharacterPresentation presentation;
  54:         private ICombatHitResolver hitResolver;
  55:         private BasicActionRuntime runtime;
  56:         private BasicAttackDefinition definition;
  57:         private BasicAttackDefinition secondDefinition;
  58:         private BasicChainState chain;
  59:         private BasicChainProfile chainProfile;
  60:         private ICombatHitReceiptSource hitReceipts;
  61:         public int AttackStage { get; private set; }
  62:         public int ReleaseCount { get; private set; }
  63:         public bool HasBufferedAttack => chain!=null && chain.HasBuffer;
  64:         public double ComboWindowUntil => chain==null?-1:chain.WindowUntil;
  65:         private bool ready, focused = true, paused, applicationPaused;
  66:         private string lastFact = "Idle";
  67:         private object respawnOwner;
  68:         private int lifeGeneration, animationGeneration;
  69:         public int LifeGeneration => lifeGeneration;
  70:         public bool IsSimulationSuspended => !focused || IsPaused;
  71:         public CharacterDataBus DataBus { get; private set; }
  72:         public string ActorId => actorId;
  73:         public string LastFact => lastFact;
  74:         public string LastActionSource { get; private set; } = "none";
  75:         public bool IsPaused => paused || applicationPaused;
  76:         public bool ActionActive => DataBus != null && DataBus.ActionActive;
  77:         public bool IsAlive => lifeState==null || lifeState.IsAlive;
  78:         public bool CanSimulateIntent => ready && IsAlive && isActiveAndEnabled && focused && !IsPaused && (posture == null || !posture.IsStaggered);
  79:         public CombatVitals Vitals => vitals;
  80:         public event Action<CharacterRoot> Died;
  81:         public CharacterFollow Follow => follow;
  82:         public CharacterIntentKind EffectiveIntentKind => intentSelector == null ? CharacterIntentKind.Neutral : intentSelector.EffectiveKind;
  83: 
  84:         public bool CanBindCompanionIntent(ICharacterControlSession session, ICompanionIntentSource source)
  85:             => ready && follow != null && ReferenceEquals(source,follow) && intentSelector != null && intentSelector.CanBind(session,source);
  86:         public bool BindCompanionIntent(ICharacterControlSession session, ICompanionIntentSource source)
  87:             => CanBindCompanionIntent(session,source) && intentSelector.Bind(session,source);
  88:         public void ClearCompanionIntent(ICompanionIntentSource owner)
  89:         { if (intentSelector != null) intentSelector.Clear(owner); }
  90: 
  91:         internal bool BindRespawnOwner(object owner)
  92:         {
  93:             if(owner==null || (respawnOwner!=null && !ReferenceEquals(respawnOwner,owner))) return false;
  94:             respawnOwner=owner; return true;
  95:         }
  96:         internal void UnbindRespawnOwner(object owner)
  97:         { if(ReferenceEquals(respawnOwner,owner)) respawnOwner=null; }
  98:         internal bool CanRespawn(object owner) => ready && isActiveAndEnabled && !IsAlive &&
  99:             vitals!=null && vitals.isActiveAndEnabled && ReferenceEquals(respawnOwner,owner);
 100:         internal bool Respawn(object owner, Vector3 position)
 101:         {
 102:             if(!CanRespawn(owner)) return false;
 103:             CancelAction("scene-respawn");
 104:             if(follow!=null) follow.ResetIntent();
 105:             if(intentSelector!=null) intentSelector.Clear();
 106:             input.ResetPlayerInput(true);
 107:             orientation.ResetOrientation();
 108:             movement.Teleport(position);
 109:             if(posture!=null) posture.ResetState();
 110:             if(!vitals.RestoreAfterDeath()) return false;
 111:             stamina?.Restore();
 112:             lifeGeneration++;
 113:             return true;
 114:         }
 115: 
 116:         private void Awake()
 117:         {
 118:             if (viewCamera == null) viewCamera = Camera.main;
 119:             if (vitals!=null)
 120:             {
 121:                 if (!vitals.Initialize(actorId)) { Debug.LogError("Character vitals identity/configuration invalid",this); enabled=false; return; }
 122:                 lifeState=vitals; vitals.Died+=OnDied;
 123:             }
 124:             if (movement == null || orientation == null || view == null || melee == null || input == null || viewCamera == null)
 125:             { Debug.LogError("Character requires registered input/movement/orientation/view/melee/camera", this); enabled = false; return; }
 126:             // Inspector保留具体组件槽；运行期通过窄接口消费，不反向依赖开发GUI或测试靶。
 127:             // 每角色只有这一条有效Frame入口；没有Director绑定时保持原玩家适配器行为。
 128:             intentSelector = new CharacterIntentSelector(this,input);
 129:             inputSource = intentSelector;
 130:             combatBrain=GetComponent<CompanionCombatBrain>();
 131:             if(combatBrain!=null) combatBrain.Initialize(this);
 132:             mover = movement;
 133:             orienter = orientation;
 134:             presentation = view;
 135:             hitResolver = melee;
 136:             if (!inputSource.IsConfigured) { Debug.LogError("Character actions are not configured", this); enabled = false; return; }
 137:             try
 138:             {
 139:                 definition = new BasicAttackDefinition(releaseAt, moveReadyAt, attackReadyAt, finishAt, damage, postureImpact);
 140:                 runtime = new BasicActionRuntime(actorId);
 141:                 chainProfile=GetComponent<BasicChainProfile>();
 142:                 // Root的早期Awake可能先于Profile.OnEnable；此处读取序列化enabled，运行期再查isActiveAndEnabled。
 143:                 if(chainProfile!=null && chainProfile.enabled)
 144:                 {
 145:                     definition=chainProfile.stage1.Freeze(); secondDefinition=chainProfile.stage2.Freeze();
 146:                     chain=chainProfile.CreateState(); hitReceipts=melee; hitReceipts.Resolved+=OnHitReceipt;
 147:                 }
 148:             }
 149:             catch (ArgumentException error) { Debug.LogError("Character configuration: " + error.Message, this); enabled = false; return; }
 150:             // 每角色独立Bus只读取原所有者；只有确需Bus的运动和表现执行初始化。
 151:             DataBus = new CharacterDataBus(actorId, inputSource, mover, orienter, runtime, authority);
 152:             stamina=GetComponent<CharacterStamina>();if(stamina!=null)stamina.Initialize();
 153:             dodge=GetComponent<CharacterDodge>();if(dodge!=null)dodge.Initialize(this,stamina);
 154:             equipment=GetComponent<CharacterEquipment>();if(equipment!=null)equipment.Initialize(this);
 155:             guard=GetComponent<ShieldGuard>();if(guard!=null)guard.Initialize(this,stamina,equipment);
 156:             DataBus.BindResources(stamina,dodge);
 157:             offhand=GetComponent<CharacterOffhand>(); offhand?.Initialize(this,equipment,stamina);
 158:             ready = InitializeSubsystems(DataBus, mover, presentation);
 159:             if (!ready) { Debug.LogError("Character subsystem initialization failed", this); enabled = false; }
 160:             // 可选能力只接受本角色初始化；无组件/无assignment的旧场保持普通玩家行为。
 161:             if (follow != null && !follow.Initialize(this)) Debug.LogWarning("Character follow unavailable: configuration or owner invalid", this);
 162:             if (posture != null)
 163:             {
 164:                 if (!posture.Initialize(vitals, this)) { Debug.LogError("Character posture configuration invalid", this); enabled = false; return; }
 165:                 posture.Broken += OnPostureBroken;
 166:                 posture.Recovered += OnPostureRecovered;
 167:             }
 168:         }
 169: 
 170:         private static bool InitializeSubsystems(CharacterDataBus dataBus, params ICharacterSubsystem[] subsystems)
 171:         {
 172:             foreach (var subsystem in subsystems) if (subsystem == null || !subsystem.Initialize(dataBus)) return false;
 173:             return true;
 174:         }
 175: 
 176:         private void Update()
 177:         {
 178:             if (!ready) return;
 179:             if (!IsAlive) { offhand?.CancelAll(); CommitPresentation(); return; }
 180:             // 本体显式调度入口：以下阶段在一次Update中按书写顺序执行。
 181:             // DefaultExecutionOrder只约束跨组件先后，不能替代这里的模块调用顺序。
 182:             // 1. 采唯一意图源并处理暂停/取消；不由各子模块另开输入Update。
 183:             // 选择器只采一个移动源；战斗能力另交窄动作候选，切人屏障为空帧，动作时钟仍继续。
 184:             combatBrain?.Sample(runtime.SimulationTime, Time.deltaTime);
 185:             inputSource.Read();
 186:             var frame = DataBus.Input;
 187:             if(chain!=null && (!input.HasPlayerInput || IsPaused || !focused || chainProfile==null || !chainProfile.isActiveAndEnabled)) chain.Clear();
 188:             if (frame.pause) SetPaused(!paused);
 189:             if (frame.cancel) CancelAction("player-cancel");
 190:             // 2. 同一根时钟推进架势；主动作暂停自然恢复，暂停/失焦完全停钟。
 191:             // 在Request前解除到期的本owner禁令；不会回滚其他根已释放的命中。
 192:             if (posture != null) posture.Tick(IsPaused || !focused ? 0 : Time.deltaTime, runtime.IsActive);
 193:             // 3. 先解算朝向，攻击释放才能读取本帧鼠标/键盘方向。
 194:             var canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
 195:             if (focused && !IsPaused)
 196:                 orienter.ResolveDirections(frame.move, PointerDirection(frame), frame.hasDirectionalAim ? frame.aimDirection : Vector2.zero, canMove);
 197:             // 3b. 可选本体机动在同一根时钟采样/推进；失控、暂停、破势立即撤销免伤资格。
 198:             if(dodge!=null)
 199:             {
 200:                 if(!input.HasPlayerInput||!CanSimulateIntent)dodge.Cancel();
 201:                 dodge.Tick(runtime.SimulationTime);
 202:                 if(frame.dodgePressed)dodge.Request(frame.move,input.HasPlayerInput);
 203:             }
 204:             // 4. 观察按键边沿，再由Runtime的暂停/权限/Ready资格决定是否接受动作。
 205:             guard?.Tick(frame.offhandHeld,input.HasPlayerInput,IsSimulationSuspended?0:Time.deltaTime);
 206:             if (intent.Observe(frame.attackHeld) && focused && !frame.attackPointerBlocked)
 207:             {
 208:                 var stage=chain==null?1:chain.Request(runtime.SimulationTime,runtime.AttackReady,
 209:                     input.HasPlayerInput && !IsPaused && authority.Allows(ControlChannel.Attack) && chainProfile!=null && chainProfile.isActiveAndEnabled);
 210:                 if(stage>0) RequestStage(stage);
 211:             }
 212:             // 4a. 副手独立请求/时钟；同帧主手已按自身资格处理，二者不共享动作身份。
 213:             if(frame.offhandPressed)offhand?.Request(PointerGroundPoint(frame),input.HasPlayerInput);
 214:             offhand?.Tick(Time.deltaTime,input.HasPlayerInput);
 215:             // 4b. 窄自治请求与玩家边沿互斥；仅Stage1，不通过输入帧注入攻击/暂停。
 216:             if(!input.HasPlayerInput && CanSimulateIntent && combatBrain!=null &&
 217:                 ((ICompanionActionCandidate)combatBrain).TryGetAttackTarget(out var aiTarget))
 218:                 RequestStage(1,aiTarget);
 219:             if(autonomousAction && runtime.IsActive)
 220:             {
 221:                 if(combatBrain==null || !combatBrain.IsAcceptedTargetValid(autonomousTarget)) CancelAction("ai-target-invalid");
 222:                 else orienter.OrientTowards(autonomousTarget.transform.position);
 223:             }
 224:             // 5. 唯一动作时钟推进与Release命中。Accepted不扣血，切人不重发事实。
 225:             foreach (var fact in runtime.Advance(Time.deltaTime, IsPaused || !focused))
 226:             {
 227:                 Observe(fact);
 228:                 if (fact.Release != null)
 229:                 {
 230:                     ReleaseCount++;
 231:                     // 在释放当帧按值采样方向；不是接受动作时锁定，前摇中仍可重新瞄准。
 232:                     var releaseDirection = orienter.AttackDirection;
 233:                     orienter.FaceAttackDirection();
 234:                     hitResolver.Resolve(fact.Release, releaseDirection);
 235:                 }
 236:             }
 237:             if(chain!=null && input.HasPlayerInput && !IsPaused && focused && authority.Allows(ControlChannel.Attack))
 238:             {
 239:                 var bufferedStage=chain.Poll(runtime.SimulationTime,runtime.AttackReady);
 240:                 if(bufferedStage>0) RequestStage(bufferedStage);
 241:             }
 242:             // 6. 动作/命中可能改变MoveReady或架势权限，重新查询后才执行真实移动。
 243:             dodge?.Tick(runtime.SimulationTime);
 244:             canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
 245:             mover.Tick(canMove, IsPaused || !focused ? 0 : Time.deltaTime);
 246:             stamina?.Tick(runtime.SimulationTime,!runtime.IsActive&&(dodge==null||!dodge.IsActive)&&(guard==null||!guard.IsGuarding)&&CanSimulateIntent);
 247:             // 7. 最后提交表现，Run读取已完成的实际位移。
 248:             // 当前没有本体IK/骨骼后处理，故不虚设LateUpdate；Spine内部播放仍由插件驱动。
 249:             CommitPresentation();
 250:         }
 251: 
 252:         // 7. Animation Intent → One Commit；无子模块Update争抢主轨。
 253:         private void CommitPresentation()
 254:         {
 255:             (presentation as Ossastra.AnimationPresentation.ICharacterAnimationFacts)?.CollectFacts(IsAlive, posture!=null && posture.IsStaggered, vitals!=null && vitals.IsHitReacting, animationGeneration, AttackStage);
 256:             presentation.SetPaused(IsPaused || !focused);
 257:             presentation.Present();
 258:         }
 259:         private Vector3? PointerGroundPoint(CharacterInputFrame frame)
 260:         {
 261:             if(!frame.hasPointer)return null; var plane=new Plane(Vector3.up,transform.position);
 262:             var ray=viewCamera.ScreenPointToRay(frame.pointer); return plane.Raycast(ray,out var distance)?ray.GetPoint(distance):(Vector3?)null;
 263:         }
 264:         private Vector3? PointerDirection(CharacterInputFrame frame)
 265:         {
 266:             if (!frame.hasPointer) return null;
 267:             var plane = new Plane(Vector3.up, transform.position);
 268:             var ray = viewCamera.ScreenPointToRay(frame.pointer);
 269:             if (!plane.Raycast(ray, out var distance)) return null;
 270:             var direction = ray.GetPoint(distance) - transform.position;
 271:             direction.y = 0;
 272:             return direction.sqrMagnitude < .0001f ? (Vector3?)null : direction;
 273:         }
 274: 
 275:         private void Observe(ActionFact fact)
 276:         {
 277:             LastActionSource=fact.Source;
 278:             lastFact = fact.Kind + " @" + fact.At.ToString("F3") + " " + fact.ActionId.ToString("N").Substring(0, 8);
 279:             if (fact.Reason != null) lastFact += " " + fact.Reason;
 280:             if (verboseTrace) Debug.Log("Character " + lastFact, this);
 281:             ActionObserved?.Invoke(fact);
 282:         }
 283:         private void RequestStage(int stage, CombatVitals aiTarget = null)
 284:         {
 285:             if((dodge!=null&&dodge.IsActive)||(stamina!=null&&!stamina.CanSpend(stamina.AttackCost)))
 286:             {lastFact="Rejected: dodge-active/stamina";return;}
 287:             var selected=stage==2?secondDefinition:definition;
 288:             var request=runtime.Request(selected,aiTarget==null?"player-input:Stage"+stage:"companion:Stage1",authority,IsPaused);
 289:             if(request.PreviousTerminal!=null) Observe(request.PreviousTerminal);
 290:             if(request.Accepted)
 291:             {
 292:                 // 同步核准后才能付款；Request没有外部回调，余额不会在检查和此处之间被异步改写。
 293:                 if(stamina!=null&&!stamina.Spend(request.Fact.ActionId,stamina.AttackCost,runtime.SimulationTime,request.Fact.Source))
 294:                 {runtime.Cancel("stamina-acceptance-conflict");throw new InvalidOperationException("Accepted attack payment failed");}
 295:                 AttackStage=stage; autonomousAction=aiTarget!=null; autonomousTarget=aiTarget;
 296:                 if(autonomousAction) { chain?.Clear(); combatBrain.OnAccepted(); orienter.OrientTowards(aiTarget.transform.position); }
 297:                 else chain?.Accepted(stage,request.Fact.ActionId,runtime.SimulationTime,selected.AttackReadyAt);
 298:                 Observe(request.Fact); orienter.FaceAttackDirection();
 299:             }
 300:             else lastFact="Rejected: "+request.Reason;
 301:         }
 302:         private void OnHitReceipt(Guid id,bool applied){if(!autonomousAction)chain?.Hit(id,applied);}
 303: 
 304:         public void CancelAction(string reason)
 305:         {
 306:             dodge?.Cancel();
 307:             guard?.Cancel();
 308:             offhand?.CancelPending();
 309:             animationGeneration++; autonomousAction=false; autonomousTarget=null; combatBrain?.ResetCandidate();
 310:             chain?.Clear();
 311:             // 取消必须具名；抑制仍按住的输入，不能把下一帧误判为新请求。
 312:             intent.SuppressUntilRelease();
 313:             var terminal = runtime == null ? null : runtime.Cancel(reason);
 314:             if (terminal != null) Observe(terminal);
 315:         }
 316: 
 317:         private void OnPostureBroken(string cause)
 318:         {
 319:             authority.Block(posture, ControlChannel.Move | ControlChannel.Attack);
 320:             CancelAction("posture-broken:" + cause);
 321:             if (follow != null) follow.ResetIntent();
 322:         }
 323:         private void OnPostureRecovered()
 324:         {
 325:             authority.Release(posture);
 326:             intent.SuppressUntilRelease();
 327:             if (follow != null) follow.ResetIntent();
 328:         }
 329: 
 330:         private void OnDied(HitOutcome outcome)
 331:         {
 332:             // HP已由结算归零；这里仅终止本体未释放动作/有效帧，再交唯一会话处理实控。
 333:             offhand?.CancelAll();
 334:             CancelAction("death:"+outcome.ActorId+":"+outcome.ActionId.ToString("N"));
 335:             if (follow!=null) follow.ResetIntent();
 336:             if (intentSelector!=null) intentSelector.Clear();
 337:             Died?.Invoke(this);
 338:         }
 339: 
 340:         public void SetPaused(bool value) { paused = value; if(value){dodge?.Cancel();guard?.Cancel();
 341:             offhand?.CancelPending();} chain?.Clear(); intent.SuppressUntilRelease(); if (follow != null) follow.ResetIntent(); }
 342:         public void SetAttackRestriction(object owner, bool restricted, string reason = "attack-restricted")
 343:         {
 344:             if (restricted) { authority.Block(owner, ControlChannel.Attack); CancelAction(reason); }
 345:             else authority.Release(owner);
 346:         }
 347: 
 348:         private void OnApplicationFocus(bool hasFocus)
 349:         { focused = hasFocus; if (follow != null) follow.ResetIntent(); if (!hasFocus) CancelAction("focus-lost"); else intent.SuppressUntilRelease(); }
 350:         private void OnApplicationPause(bool value)
 351:           { applicationPaused = value; if(value)chain?.Clear(); if (follow != null) follow.ResetIntent(); if (value) intent.SuppressUntilRelease(); }
 352:         private void OnEnable() { intent.SuppressUntilRelease(); if (intentSelector != null) intentSelector.Clear(); }
 353:         private void OnDisable() { offhand?.CancelAll(); CancelAction("disabled"); if (posture != null) posture.ResetState(); authority.Release(posture); if (follow != null) follow.ResetIntent(); if (intentSelector != null) intentSelector.Clear(); if (ready && presentation != null) presentation.SetPaused(false); }
 354:         private void OnDestroy()
 355:         {
 356:             if(hitReceipts!=null) hitReceipts.Resolved-=OnHitReceipt;
 357:             if (vitals!=null) vitals.Died-=OnDied;
 358:             if (posture != null) { posture.Broken -= OnPostureBroken; posture.Recovered -= OnPostureRecovered; }
 359:             authority.Release(posture);
 360:             // 终止本角色动作后解除本对象引用；不清其它角色或场景控制权威。
 361:             CancelAction("destroyed");
 362:             ready = false;
 363:             DataBus = null;
 364:             runtime = null;
 365:             inputSource = null;
 366:             if (intentSelector != null) intentSelector.Clear();
 367:             intentSelector = null;
 368:             mover = null;
 369:             orienter = null;
 370:             presentation = null;
 371:             hitResolver = null;
 372:         }
 373:     }
 374: }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterStamina.cs
SHA256 D1C09B30339D925526F38A76740EC68E1FFE2B371D1CBFA2FFA90D27060CEBD5
```csharp
   1: /* 脚本：CharacterStamina
   2:  * 功能：把每本体体力配置与纯余额接入Root；记录最近一次实际付款。
   3:  * 边界：没有独立Update，不创建动作或处理设备输入；只有Root模拟时间推进。
   4:  */
   5: using System;
   6: using Ossastra.CharacterCore;
   7: using UnityEngine;
   8: namespace Ossastra.Characters
   9: {
  10:     [DisallowMultipleComponent]
  11:     public sealed class CharacterStamina:MonoBehaviour,ICharacterStamina
  12:     {
  13:         [SerializeField,Min(1)] private float maximum=100;
  14:         [SerializeField,Min(0)] private float recoveryDelay=.5f,recoveryRate=20,attackCost=10;
  15:         private StaminaState state;
  16:         public double Current=>state?.Current??0;
  17:         public double Maximum=>state?.Maximum??maximum;
  18:         public float AttackCost=>attackCost;
  19:         public Guid LastToken=>state?.LastToken??Guid.Empty;
  20:         public string LastPayment {get;private set;}="无";
  21:         public void Initialize(){if(state==null)state=new StaminaState(maximum,recoveryDelay,recoveryRate);}
  22:         public bool CanSpend(double amount)=>isActiveAndEnabled&&state!=null&&state.CanSpend(amount);
  23:         public bool Spend(Guid token,double amount,double time,string source)
  24:         {var ok=isActiveAndEnabled&&state!=null&&state.TrySpend(token,amount,time);LastPayment=source+" #"+token.ToString("N")+(ok?" 已支付 ":" 拒绝 ")+amount;return ok;}
  25:         public void Tick(double time,bool idle){state?.Tick(time,idle&&isActiveAndEnabled);}
  26:         public void Restore(){state?.Restore();}
  27:         public bool Drain(double amount,double time)=>isActiveAndEnabled&&state!=null&&state.Drain(amount,time);
  28:     }
  29: }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/HealingBottleService.cs
SHA256 882207465440609A8E594019A074A30341AAB6708B9F0F9F1C77CADF5B2F85FB
```csharp
   1: /* 脚本：HealingBottleService
   2:  * 功能：场景唯一共享治疗余额；验证当前实控资格后调用纯事务。
   3:  * 边界：不绑定按键、不拥有角色生命和返场；同场死亡不重建余额。
   4:  */
   5: using System.Linq;
   6: using UnityEngine;
   7: using Ossastra.CharacterCore;
   8: namespace Ossastra.Characters
   9: {
  10:     [DisallowMultipleComponent]
  11:     public sealed class HealingBottleService:MonoBehaviour
  12:     {
  13:         [SerializeField] private CharacterControlCoordinator coordinator;
  14:         private readonly HealingBottleState state=new HealingBottleState();
  15:         public int Count=>state.Count;
  16:         public int UseCount=>state.UseCount;
  17:         public bool Refilled=>state.Refilled;
  18:         public CharacterControlCoordinator Coordinator=>coordinator;
  19:         public string LastResult {get;private set;}="共享Y 1/1";
  20:         public bool CanRequest=>isActiveAndEnabled&&coordinator!=null&&coordinator.IsIntentSamplingAllowed&&coordinator.Current!=null&&coordinator.Current.CanSimulateIntent;
  21:         private void OnEnable(){if(FindObjectsOfType<HealingBottleService>().Any(s=>s!=this&&s.isActiveAndEnabled)){Debug.LogError("Duplicate healing bottle service",this);enabled=false;}}
  22:         public bool TryUse(){if(!CanRequest){LastResult="拒绝：实控资格";return false;}var ok=state.TryUse(coordinator.Characters.Where(c=>c!=null&&c.IsAlive).Select(c=>c.Vitals.Health));LastResult=ok?"Y +100（存活成员，上限截断）":"拒绝：无瓶或活体全满";return ok;}
  23:         public bool TryRefill(PrototypeCampfire campfire){if(!CanRequest||campfire==null||!campfire.CanRefill(this))return false;var ok=state.TryRefill();LastResult=ok?"首次篝火补至1瓶":"拒绝：已经补给或已有瓶";return ok;}
  24:     }
  25: }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/PartyConsumableInput.cs
SHA256 2727DF19EBB0439E426CE3F15EC1364FE754D68A1E63AE336B8D3CABD1253C7C
```csharp
   1: /* 脚本：PartyConsumableInput
   2:  * 功能：场景唯一Y输入；实控转移、暂停与失焦后须重新松键。
   3:  * 边界：只转发真实按键边沿，不保存瓶余额或角色HP。
   4:  */
   5: using UnityEngine;
   6: using UnityEngine.InputSystem;
   7: namespace Ossastra.Characters
   8: {
   9:     [DisallowMultipleComponent]
  10:     public sealed class PartyConsumableInput:MonoBehaviour
  11:     {
  12:         [SerializeField] private HealingBottleService service;
  13:         public InputAction healingAction=new InputAction("SharedHealing",InputActionType.Button);
  14:         private bool armed,focused=true;
  15:         private int revision=-1;
  16:         private void OnEnable(){armed=false;healingAction.Enable();}
  17:         private void Update()
  18:         {
  19:             if(service==null||service.Coordinator==null)return;
  20:             var currentRevision=service.Coordinator.ControlRevision;
  21:             if(revision!=currentRevision){revision=currentRevision;armed=false;return;}
  22:             if(!focused||!service.CanRequest){armed=false;return;}
  23:             if(!healingAction.IsPressed()){armed=true;return;}
  24:             if(armed&&healingAction.WasPressedThisFrame()){armed=false;service.TryUse();}
  25:         }
  26:         private void OnApplicationFocus(bool value){focused=value;armed=false;}
  27:         private void OnApplicationPause(bool value){if(value)armed=false;}
  28:         private void OnDisable(){armed=false;healingAction.Disable();}
  29:         private void OnDestroy(){healingAction.Dispose();}
  30:     }
  31: }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/PrototypeCampfire.cs
SHA256 5FBC0B7A3298CB90C6A0C674BAD711832631B385036127B4873537AADF47A2EA
```csharp
   1: /* 脚本：PrototypeCampfire
   2:  * 功能：新试验场的近距离、存活、脱战首次补瓶入口。
   3:  * 边界：G为本场独立样板键，不订阅/消费NpcRoot交互，不代表正式篝火或世界存档。
   4:  */
   5: using UnityEngine;
   6: using UnityEngine.InputSystem;
   7: using Ossastra.Combat;
   8: namespace Ossastra.Characters
   9: {
  10:     public sealed class PrototypeCampfire:MonoBehaviour
  11:     {
  12:         [SerializeField] private HealingBottleService service;
  13:         [SerializeField] private CombatEngagementSession engagement;
  14:         [SerializeField,Min(.1f)] private float radius=1.5f;
  15:         public InputAction interactAction=new InputAction("SampleCampfire",InputActionType.Button);
  16:         private bool armed;
  17:         public bool CanRefill(HealingBottleService requester)=>isActiveAndEnabled&&requester==service&&service!=null&&service.CanRequest&&
  18:             engagement!=null&&engagement.IsAvailable&&!engagement.Engaged&&Vector3.Distance(service.Coordinator.Current.transform.position,transform.position)<=radius;
  19:         private void OnEnable(){armed=false;interactAction.Enable();}
  20:         private void Update(){if(!CanRefill(service)){armed=false;return;}if(!interactAction.IsPressed()){armed=true;return;}if(armed&&interactAction.WasPressedThisFrame()){armed=false;service.TryRefill(this);}}
  21:         private void OnDisable(){armed=false;interactAction.Disable();}
  22:         private void OnDestroy(){interactAction.Dispose();}
  23:     }
  24: }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/ShieldGuard.cs
SHA256 E38307999EAF01287C59ECEFFCD676A80C2B7947BABF133CD559CAC17CF2C0C3
```csharp
   1: /* 脚本：ShieldGuard
   2:  * 功能：本体方向盾挡、首次/持续费用和真实盾挡磨损；只由Root Tick。
   3:  * 边界：不读设备、不写HP/架势，不改变主手资格；耗尽必须松键才可再次举盾。
   4:  */
   5: using System;
   6: using Ossastra.CharacterCore;
   7: using Ossastra.Combat;
   8: using UnityEngine;
   9: namespace Ossastra.Characters
  10: {
  11:     [DisallowMultipleComponent]
  12:     public sealed class ShieldGuard:MonoBehaviour,ICombatGuard
  13:     {
  14:         [SerializeField,Min(0)] private float startCost=8,holdCostPerSecond=6;
  15:         [SerializeField,Range(0,180)] private float frontAngle=120;
  16:         [SerializeField,Range(0,1)] private float postureScale=.5f;
  17:         private CharacterRoot owner;
  18:         private CharacterStamina stamina;
  19:         private CharacterEquipment equipment;
  20:         private bool guarding,armed;
  21:         public bool IsGuarding=>guarding&&isActiveAndEnabled&&owner!=null&&owner.CanSimulateIntent&&
  22:             equipment!=null&&equipment.isActiveAndEnabled&&equipment.State.LeftKind==OffhandKind.Shield&&equipment.State.Durability>0;
  23:         public Vector3 GuardDirection=>owner==null||owner.DataBus==null?Vector3.zero:owner.DataBus.AttackDirection;
  24:         public double MinimumDot=>Mathf.Cos(frontAngle*.5f*Mathf.Deg2Rad);
  25:         public double PostureScale=>postureScale;
  26:         public string LastResult {get;private set;}="未举盾";
  27:         public void Initialize(CharacterRoot root,CharacterStamina resource,CharacterEquipment gear){owner=root;stamina=resource;equipment=gear;}
  28:         public void Tick(bool held,bool playerAllowed,float delta)
  29:         {
  30:             if(!held){guarding=false;armed=true;return;}
  31:             var valid=isActiveAndEnabled&&owner!=null&&owner.CanSimulateIntent&&playerAllowed&&equipment!=null&&equipment.isActiveAndEnabled&&
  32:                 equipment.State.LeftKind==OffhandKind.Shield&&equipment.State.Durability>0&&stamina!=null&&
  33:                 (owner.DataBus.MotionOverride==null||!owner.DataBus.MotionOverride.IsActive);
  34:             if(!valid){Cancel();return;}
  35:             if(guarding)
  36:             {if(!stamina.Drain(holdCostPerSecond*delta,owner.SimulationTime)){Cancel();LastResult="体力耗尽，需松键";}return;}
  37:             if(!armed)return;armed=false;
  38:             if(stamina.Spend(Guid.NewGuid(),startCost,owner.SimulationTime,"guard-start")){guarding=true;LastResult="举盾 SAMPLE";}
  39:             else LastResult="体力不足";
  40:         }
  41:         public void OnGuarded(HitOutcome outcome)
  42:         {if(IsGuarding&&outcome.TargetId==owner.ActorId&&equipment.State.Wear(outcome.ActionId)){LastResult="Guarded #"+outcome.ActionId.ToString("N");if(equipment.State.Durability==0)Cancel();}}
  43:         public void Cancel(){guarding=false;armed=false;}
  44:         private void OnDisable(){Cancel();}
  45:     }
  46: }
```

## Assets/OSSASTRA/Scripts/Combat/Runtime/CombatContactGateway.cs
SHA256 7DF107521F529946853E08182618A6387AECE398F0A5F157D6D8C54291FBA0BD
```csharp
   1: /* 脚本：CombatContactGateway
   2:  * 功能：把空间候选收敛为一个身份合法、防御已采样的Release批次，再分发同批回执。
   3:  * 边界：HP只经AttackRelease；多碰撞体合并、冒名整组拒绝；不拥有动作时钟。
   4:  * 协作：近战/范围适配器先查空间与LOS；网关复核来源和目标，HP反馈后处理存活者架势。
   5:  */
   6: using System;
   7: using System.Collections.Generic;
   8: using System.Linq;
   9: using Ossastra.CharacterCore;
  10: using Ossastra.CombatCore;
  11: using UnityEngine;
  12: namespace Ossastra.Combat
  13: {
  14:     public static class CombatContactGateway
  15:     {
  16:         public static IReadOnlyList<HitOutcome> Resolve(AttackRelease release, CombatVitals source, IEnumerable<IHitTarget> candidates, Vector3 origin)
  17:         {
  18:             if(release==null)throw new ArgumentNullException(nameof(release));
  19:             if(source!=null && (!source.IsAvailable || source.Health.Id!=release.ActorId))return release.Resolve(Array.Empty<Health>());
  20:             var targets=candidates.Where(CombatTargetValidity.IsAvailable).Distinct()
  21:                 .GroupBy(t=>t.Health.Id,StringComparer.Ordinal).Where(g=>g.Count()==1).Select(g=>g.Single())
  22:                 .Where(t=>CombatTargetPolicy.CanHit(release.ActorId,source==null?CombatFaction.Neutral:source.Faction,
  23:                     source==null||source.IsAlive,t.Health.Id,t is ICombatTarget combat?combat.Faction:CombatFaction.Neutral,true)).ToArray();
  24:             var defenses=targets.ToDictionary(t=>t,t=>(t as MonoBehaviour)?.GetComponents<MonoBehaviour>().OfType<ICombatDefense>().FirstOrDefault(x=>(x as Behaviour).isActiveAndEnabled));
  25:             var outcomes=release.ResolveContacts(targets.Select(t=>new AttackContact(t.Health,defenses[t]?.CaptureDefense(origin)??DefenseSnapshot.Normal)));
  26:             foreach(var outcome in outcomes)
  27:             {
  28:                 var target=targets.First(t=>t.Health.Id==outcome.TargetId);
  29:                 if(outcome.Reason==HitReason.Applied)
  30:                 {if(target is ICombatTarget combat)combat.OnHitResolved(outcome);else target.ShowHit();}
  31:                 if(outcome.Reason==HitReason.Guarded||outcome.Reason==HitReason.Evaded)defenses[target]?.OnDefenseResolved(outcome);
  32:                 if(outcome.HpAfter>0 && outcome.PostureScale>0 && target is MonoBehaviour component && component!=null)
  33:                     foreach(var receiver in component.GetComponents<MonoBehaviour>().OfType<ICombatPostureReceiver>())
  34:                         receiver.ReceiveImpact(outcome,release.PostureImpact*outcome.PostureScale);
  35:             }
  36:             return outcomes;
  37:         }
  38:     }
  39: }
```

## Assets/OSSASTRA/Scripts/Combat/Runtime/CombatContracts.cs
SHA256 5F7D48271ECB2DB698877FBE472E4606D198C92A9ABD2FAAB2047DD55A3D8AE5
```csharp
   1: /*
   2:  * 脚本：CombatContracts
   3:  * 功能：中立命中、生命及世界目标契约；玩家输入和敌方决策共用。
   4:  * 边界：Health为唯一HP权威；接口通知不重复扣血，不保存玩家Current。
   5:  * 协作：玩家根/敌人根/物理解析/感知真实消费；旧开发靶只需IHitTarget。
   6:  */
   7: using Ossastra.CharacterCore;
   8: using UnityEngine;
   9: namespace Ossastra.Combat
  10: {
  11:     public interface ICombatEvasion { bool IsEvading { get; } }
  12:     public interface ICombatGuard
  13:     {
  14:         bool IsGuarding { get; }
  15:         Vector3 GuardDirection { get; }
  16:         double MinimumDot { get; }
  17:         double PostureScale { get; }
  18:         void OnGuarded(HitOutcome outcome);
  19:     }
  20:     /// <summary>只采样防御资格；回执后才允许更新盾耐久，不直接写生命或架势。</summary>
  21:     public interface ICombatDefense
  22:     {
  23:         DefenseSnapshot CaptureDefense(Vector3 incomingOrigin);
  24:         void OnDefenseResolved(HitOutcome outcome);
  25:     }
  26:     /// <summary>世界命中器向同一合法存活目标递交架势冲击，不拥有第二次HP写入。</summary>
  27:     public interface ICombatPostureReceiver
  28:     {
  29:         void ReceiveImpact(HitOutcome outcome, double impact);
  30:     }
  31:     public interface ICombatLifeState { bool IsAlive { get; } }
  32:     public interface IHitTarget { Health Health { get; } void ShowHit(); }
  33:     public interface ICombatTarget : IHitTarget, ICombatLifeState
  34:     {
  35:         bool IsAvailable { get; }
  36:         CombatFaction Faction { get; }
  37:         Vector3 WorldPosition { get; }
  38:         Transform TargetTransform { get; }
  39:         void OnHitResolved(HitOutcome outcome);
  40:     }
  41:     public interface ICombatHitResolver
  42:     {
  43:         string LastResult { get; }
  44:         void Resolve(AttackRelease release, Vector3 releaseDirection);
  45:     }
  46:     /// <summary>原物理结算的只读回执；禁止消费者再查物理或再扣血。</summary>
  47:     public interface ICombatHitReceiptSource
  48:     {
  49:         event System.Action<System.Guid, bool> Resolved;
  50:     }
  51:     /// <summary>接口非null不代表Unity宿主有效；销毁/禁用/无生命立即失效。</summary>
  52:     public static class CombatTargetValidity
  53:     {
  54:         public static bool IsAvailable(IHitTarget target)
  55:         {
  56:             var component=target as MonoBehaviour;
  57:             return component!=null && component.isActiveAndEnabled && target.Health!=null &&
  58:                 target.Health.Current>0 && (!(target is ICombatTarget combat) || combat.IsAvailable);
  59:         }
  60:     }
  61: }
```

## Assets/OSSASTRA/Scripts/Combat/Runtime/CombatDefense.cs
SHA256 5BE5A308F5AE204937C1B97BEEAE1F61797CFF91BD22050335639C3B9128B517
```csharp
   1: /* 脚本：CombatDefense
   2:  * 功能：组合本体可选闪避/盾资格，冻结来袭XZ方向的防御快照。
   3:  * 边界：无HP或时钟权威，不认识CharacterRoot；合法回执后才通知盾磨损。
   4:  */
   5: using System.Linq;
   6: using Ossastra.CharacterCore;
   7: using UnityEngine;
   8: namespace Ossastra.Combat
   9: {
  10:     [DisallowMultipleComponent]
  11:     public sealed class CombatDefense:MonoBehaviour,ICombatDefense
  12:     {
  13:         private ICombatEvasion evasion;
  14:         private ICombatGuard guard;
  15:         private void Awake(){evasion=GetComponents<MonoBehaviour>().OfType<ICombatEvasion>().FirstOrDefault();guard=GetComponents<MonoBehaviour>().OfType<ICombatGuard>().FirstOrDefault();}
  16:         public DefenseSnapshot CaptureDefense(Vector3 incomingOrigin)
  17:         {
  18:             var d=incomingOrigin-transform.position;d.y=0;
  19:             var dot=d.sqrMagnitude>.0001f&&guard!=null?Vector3.Dot(guard.GuardDirection.normalized,d.normalized):-1;
  20:             return new DefenseSnapshot(evasion!=null&&evasion.IsEvading,guard!=null&&guard.IsGuarding,dot,guard?.MinimumDot??1,guard?.PostureScale??1);
  21:         }
  22:         public void OnDefenseResolved(HitOutcome outcome){if(outcome.Reason==HitReason.Guarded)guard?.OnGuarded(outcome);}
  23:     }
  24: }
```

## Assets/OSSASTRA/Scripts/Combat/Runtime/CombatMeleeHit.cs
SHA256 32252E408FF7F4FAF4F0DF32F089FBE4034FE30B85550F67AC08EAEFFF9D0AA7
```csharp
   1: /*
   2:  * 脚本：CombatMeleeHit
   3:  * 功能：将一次释放转换为物理候选、身份/阵营资格和唯一伤害批次。
   4:  * 边界：中立能力，无玩家Root依赖，不建动作时钟或HP；空挥也关闭Release。
   5:  * 协作：两类战斗根各自释放；空间/LOS通过后交统一防御网关；仅Applied通知连段命中，格挡不自动进段。
   6:  */
   7: using System;
   8: using System.Linq;
   9: using Ossastra.CharacterCore;
  10: using UnityEngine;
  11: namespace Ossastra.Combat
  12: {
  13:     public sealed class CombatMeleeHit : MonoBehaviour, ICombatHitResolver, ICombatHitReceiptSource
  14:     {
  15:         [SerializeField, Min(0)] private float reach=.8f;
  16:         [SerializeField, Min(.01f)] private float radius=.65f;
  17:         [SerializeField] private float height=.8f;
  18:         [SerializeField] private LayerMask targetLayers=~0;
  19:         public string LastResult { get; private set; }="未释放";
  20:         public event Action<Guid, bool> Resolved;
  21:         public float Reach => reach;
  22:         public float Radius => radius;
  23:         public float Height => height;
  24:         public void Resolve(AttackRelease release, Vector3 releaseDirection)
  25:         {
  26:             if(release==null) throw new ArgumentNullException(nameof(release));
  27:             var source=GetComponent<CombatVitals>();
  28:             if(!isActiveAndEnabled || (source!=null && (!source.IsAvailable || source.Health.Id!=release.ActorId)))
  29:             { release.Resolve(Array.Empty<Health>()); LastResult="来源失效或释放身份不符"; Resolved?.Invoke(release.ActionId,false); return; }
  30:             var center=transform.position+Vector3.up*height+releaseDirection*reach;
  31:             var candidates=Physics.OverlapSphere(center,radius,targetLayers,QueryTriggerInteraction.Collide)
  32:                 .Select(c=>c.GetComponentsInParent<MonoBehaviour>().OfType<IHitTarget>().FirstOrDefault())
  33:                 .Where(CombatTargetValidity.IsAvailable).Distinct()
  34:                 .Where(t=>t is MonoBehaviour body && CombatSight.IsClear(transform,body.transform,height)).ToArray();
  35:             var outcomes=CombatContactGateway.Resolve(release,source,candidates,transform.position);
  36:             LastResult=outcomes.Count==0?"空挥／无合法敌对目标":string.Join("; ",outcomes.Select(o=>
  37:                 o.ActorId+" → "+o.TargetId+" "+o.HpBefore+"→"+o.HpAfter+" "+o.Reason+" #"+o.ActionId.ToString("N")));
  38:             Resolved?.Invoke(release.ActionId,outcomes.Any(o=>o.Reason==HitReason.Applied));
  39:         }
  40:     }
  41: }
```

## Assets/OSSASTRA/Scripts/Combat/Runtime/CombatPosture.cs
SHA256 4C5C611FD875E8D7DEE890DF9C1D2DCB175EDBA5189328AB3B55EBD991425BA9
```csharp
   1: /*
   2:  * 脚本：CombatPosture
   3:  * 功能：把本体合法普通命中或盾挡接触交给唯一PostureState；报告破势与解除。
   4:  * 边界：不扣HP、不查世界目标、不找玩家根，不自带Update；无组件时旧场不变。
   5:  * 协作：CharacterRoot/EnemyRoot初始化、Tick、监听并管理自己的权限owner。
   6:  */
   7: using System;
   8: using Ossastra.CharacterCore;
   9: using Ossastra.CombatCore;
  10: using UnityEngine;
  11: 
  12: namespace Ossastra.Combat
  13: {
  14:     [DisallowMultipleComponent]
  15:     public sealed class CombatPosture : MonoBehaviour, ICombatPostureReceiver
  16:     {
  17:         [SerializeField] private float maximum = 100, impactMultiplier = 1;
  18:         [SerializeField] private float staggerDuration = .6f, recoveryFraction = .5f;
  19:         [SerializeField] private float recoveryDelay = 1.5f, recoveryRate = .25f;
  20:         private CombatVitals vitals;
  21:         private Behaviour driver;
  22:         private PostureState state;
  23:         private Guid lastActionId;
  24:         private string lastActorId;
  25:         public double Current => state == null ? 0 : state.Current;
  26:         public double Maximum => state == null ? 0 : state.Maximum;
  27:         public double StaggerRemaining => state == null ? 0 : state.StaggerRemaining;
  28:         public double RegenDelayRemaining => state == null ? 0 : state.RegenDelayRemaining;
  29:         public PosturePhase Phase => state == null ? PosturePhase.Normal : state.Phase;
  30:         public bool IsStaggered => state != null && state.IsStaggered;
  31:         public string LastImpact { get; private set; } = "无";
  32:         public event Action<string> Broken;
  33:         public event Action Recovered;
  34: 
  35:         public bool Initialize(CombatVitals owner, Behaviour clockOwner)
  36:         {
  37:             if (owner == null || owner.gameObject != gameObject || owner.Health == null ||
  38:                 clockOwner == null || clockOwner.gameObject != gameObject) return false;
  39:             if (state != null) return ReferenceEquals(vitals, owner) && ReferenceEquals(driver, clockOwner);
  40:             if (float.IsNaN(impactMultiplier) || float.IsInfinity(impactMultiplier) || impactMultiplier < 0) return false;
  41:             try { state = new PostureState(maximum, staggerDuration, recoveryFraction, recoveryDelay, recoveryRate); }
  42:             catch (ArgumentException) { return false; }
  43:             vitals = owner;
  44:             driver = clockOwner;
  45:             vitals.Died += OnDied;
  46:             return true;
  47:         }
  48: 
  49:         /// <summary>仅消费同一接触批次存活者的Applied/Guarded结果，倍率由网关计算；不再次Resolve或寻找目标。</summary>
  50:         public void ReceiveImpact(HitOutcome outcome, double impact)
  51:         {
  52:             // 根关闭后不能再留下无人推进的硬直；只依赖显式注入的生命周期，不反查具体根类型。
  53:             if (!isActiveAndEnabled || driver == null || !driver.isActiveAndEnabled ||
  54:                 state == null || vitals == null || !vitals.IsAvailable ||
  55:                 outcome == null || (outcome.Reason != HitReason.Applied && outcome.Reason != HitReason.Guarded) || outcome.HpAfter <= 0 ||
  56:                 outcome.TargetId != vitals.Health.Id || outcome.HpAfter != vitals.Health.Current ||
  57:                 outcome.ActionId == Guid.Empty || string.IsNullOrWhiteSpace(outcome.ActorId) ||
  58:                 outcome.ActorId == outcome.TargetId ||
  59:                 (lastActionId == outcome.ActionId && lastActorId == outcome.ActorId) ||
  60:                 double.IsNaN(impact) || double.IsInfinity(impact) || impact <= 0) return;
  61:             lastActionId = outcome.ActionId;
  62:             lastActorId = outcome.ActorId;
  63:             LastImpact = outcome.ActorId + " #" + outcome.ActionId.ToString("N");
  64:             if (state.ApplyImpact(impact * impactMultiplier)) Broken?.Invoke(LastImpact);
  65:         }
  66: 
  67:         /// <summary>只有本体根调用；失焦/暂停由根传零，硬直结束只通知自身owner。</summary>
  68:         public void Tick(double delta, bool primaryActionActive)
  69:         {
  70:             if (!isActiveAndEnabled || driver == null || !driver.isActiveAndEnabled ||
  71:                 state == null || vitals == null || !vitals.IsAvailable) return;
  72:             var wasStaggered = state.IsStaggered;
  73:             state.Tick(delta, primaryActionActive);
  74:             if (wasStaggered && !state.IsStaggered) Recovered?.Invoke();
  75:         }
  76: 
  77:         public void ResetState()
  78:         {
  79:             if (state == null) return;
  80:             var wasStaggered = state.IsStaggered;
  81:             state.Reset();
  82:             lastActionId = Guid.Empty;
  83:             lastActorId = null;
  84:             LastImpact = "重置";
  85:             if (wasStaggered) Recovered?.Invoke();
  86:         }
  87:         private void OnDied(HitOutcome outcome) { ResetState(); }
  88:         private void OnDisable() { ResetState(); }
  89:         private void OnDestroy()
  90:         {
  91:             ResetState();
  92:             if (vitals != null) vitals.Died -= OnDied;
  93:         }
  94:     }
  95: }
```

## Assets/OSSASTRA/Scripts/Development/Defense/DefenseLoadoutDebugView.cs
SHA256 DF5561199049EAB035251226F8710E0D27333B336CA8C5A5B0B05C9579CB10D0
```csharp
   1: /* 脚本：DefenseLoadoutDebugView
   2:  * 功能：新试玩场读取真实HP/架势/体力/机动；提供显式资源边界与恢复调试。
   3:  * 边界：仅开发场装配，不存玩法余额，不强行宣告试炼成功；按钮走原资源入口。
   4:  */
   5: using System;using UnityEngine;using Ossastra.Characters;using Ossastra.Combat;
   6: namespace Ossastra.Development {
   7: public sealed class DefenseLoadoutDebugView:MonoBehaviour {
   8:  private CharacterControlCoordinator session;
   9:  private void Start(){session=FindObjectOfType<CharacterControlCoordinator>();}
  10:  private void OnGUI(){if(session==null)return;var rect=new Rect(12,12,460,410);foreach(var root in session.Characters)root.GetComponent<CharacterInputHandler>().RegisterPointerRegion(this,rect);
  11:  GUILayout.BeginArea(rect,GUI.skin.box);GUILayout.Label("ROADMAP03 · SAMPLE / 正式敌美术ASSET_BLOCKED");GUILayout.Label("WASD移动 / LMB主手 / Shift闪避 / RMB副手 / Tab切人");
  12:  foreach(var root in session.Characters){var s=root.GetComponent<CharacterStamina>();var d=root.GetComponent<CharacterDodge>();GUILayout.Label(root.ActorId+(session.Current==root?" [Current]":"")+" HP "+root.Vitals.Health.Current+" 体力 "+(s==null?"无":s.Current.ToString("F1"))+" 闪避 "+(d!=null&&d.IsActive)+" 免伤 "+(d!=null&&d.IsEvading));}
  13:  var c=session.Current;if(c!=null){var s=c.GetComponent<CharacterStamina>();GUILayout.Label(s?.LastPayment??"无体力能力");GUILayout.BeginHorizontal();if(GUILayout.Button(new GUIContent("体力耗尽","开发：真实资源入口扣除当前体力，验证非法请求"))&&s!=null)s.Spend(Guid.NewGuid(),s.Current,c.SimulationTime,"Development-empty");if(GUILayout.Button(new GUIContent("恢复体力","开发：恢复同一本体资源实例，验证合法请求")))s?.Restore();GUILayout.EndHorizontal();}
  14:  if(c!=null){var gear=c.GetComponent<CharacterEquipment>();var guard=c.GetComponent<ShieldGuard>();if(gear!=null){GUILayout.Label("左手 "+gear.State.LeftKind+" 耐久 "+gear.State.Durability+" 盾挡 "+(guard!=null&&guard.IsGuarding));GUILayout.BeginHorizontal();foreach(Ossastra.CharacterCore.OffhandKind kind in Enum.GetValues(typeof(Ossastra.CharacterCore.OffhandKind)))if(GUILayout.Button(kind.ToString()))gear.TryEquip(kind);GUILayout.EndHorizontal();GUILayout.Label(gear.LastResult);var bomb=c.GetComponent<CharacterOffhand>();if(bomb!=null){GUILayout.Label("炸弹 CD "+bomb.CooldownRemaining.ToString("F1")+" 在途 "+bomb.InFlight);GUILayout.Label(bomb.LastResult);}}}var bottle=FindObjectOfType<HealingBottleService>();if(bottle!=null){GUILayout.Label("Y共享回血 "+bottle.Count+"/1 · 使用 "+bottle.UseCount+" · 篝火已补 "+bottle.Refilled);GUILayout.Label(bottle.LastResult);GUILayout.Label("脱战靠近左下圆台按G：仅一次补瓶");}GUILayout.Label("状态只读；调试不代表自然路径已通过。");GUILayout.EndArea();}
  15:  private void OnDisable(){if(session!=null)foreach(var root in session.Characters)if(root!=null)root.GetComponent<CharacterInputHandler>().RemovePointerRegion(this);}
  16: }}
```
