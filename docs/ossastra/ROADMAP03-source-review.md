# ROADMAP03 源码审查（S9已发布，S10本机候选）

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

## Assets/OSSASTRA/Scripts/Characters/Core/StaminaState.cs
SHA256 A4C21023DD5C6ED4E5B3148A78C2104CBC667A0930056148D6E3F7B77F73323E
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
  35:     }
  36: }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterContracts.cs
SHA256 B9A864D189D4CF2C5B538D5FB600A3858B8689B8F7531299DF65D51749609CD8
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
  14:     /// <summary>本体体力只读视图；付款仅经本体资源模块，不在DataBus复制余额。</summary>
  15:     public interface ICharacterStamina
  16:     {
  17:         double Current { get; }
  18:         double Maximum { get; }
  19:     }
  20:     /// <summary>原运动执行器读取机动速度；能力不直接调用Move或修改位置。</summary>
  21:     public interface ICharacterMotionOverride
  22:     {
  23:         bool IsActive { get; }
  24:         Vector3 MotionVelocity { get; }
  25:     }
  26:     /// <summary>伙伴战斗策略提供移动候选；选择器与Root保留有效输入及实际运动执行权。</summary>
  27:     public interface ICompanionMoveCandidate
  28:     {
  29:         bool TryGetCombatMove(out Vector2 move);
  30:     }
  31:     /// <summary>伙伴战斗策略提供攻击目标候选；Root决定是否受理并执行动作。</summary>
  32:     public interface ICompanionActionCandidate
  33:     {
  34:         bool TryGetAttackTarget(out CombatVitals target);
  35:         /// <summary>Root受理当前候选后通知策略开始冷却；候选本身不启动动作。</summary>
  36:         void OnAccepted();
  37:     }
  38:     /// <summary>队伍只分配合法目标；角色能力自行计算移动，不从此契约取得方向或策略状态。</summary>
  39:     public interface ICharacterFollowTargetSource
  40:     {
  41:         bool TryGetFollowTarget(CharacterRoot follower, out CharacterRoot target);
  42:     }
  43:     /// <summary>场景唯一控制权的只读视图；代际和共享屏障同时约束玩家与伙伴来源。</summary>
  44:     public interface ICharacterControlSession
  45:     {
  46:         CharacterRoot Current { get; }
  47:         IReadOnlyList<CharacterRoot> Characters { get; }
  48:         bool IsIntentSamplingAllowed { get; }
  49:         int ControlRevision { get; }
  50:     }
  51:     /// <summary>伙伴策略只提供移动候选帧；选择器剥离攻击、暂停、取消和开发命令。</summary>
  52:     public interface ICompanionIntentSource
  53:     {
  54:         bool IsAvailable { get; }
  55:         CharacterInputFrame ReadCompanionIntent(CharacterRoot character, float delta);
  56:     }
  57:     /// <summary>玩家源的实时许可；外层已采样帧在许可撤回时同样失效，兼容未挂跟随模块的旧场。</summary>
  58:     public interface ICharacterInputAvailability
  59:     {
  60:         bool HasPlayerInput { get; }
  61:     }
  62:     /// <summary>由场景实控权威回答谁可以采样玩家输入；输入源不保存第二份选中状态。</summary>
  63:     public interface ICharacterPlayerControlSource
  64:     {
  65:         bool IsPlayerInputAllowed(ICharacterInputSource source);
  66:     }
  67:     /// <summary>仅为确需角色Bus的模块提供显式初始化；失败阻止角色启动。</summary>
  68:     public interface ICharacterSubsystem { bool Initialize(CharacterDataBus dataBus); }
  69:     /// <summary>提供单角色唯一有效输入快照，不判定动作是否成功。</summary>
  70:     public interface ICharacterInputSource
  71:     {
  72:         CharacterInputFrame CurrentFrame { get; }
  73:         bool IsConfigured { get; }
  74:         CharacterInputFrame Read();
  75:     }
  76:     /// <summary>执行实际运动并报告结果；不决定攻击朝向。</summary>
  77:     public interface ICharacterMovement : ICharacterSubsystem
  78:     {
  79:         bool IsMoving { get; }
  80:         Vector3 Velocity { get; }
  81:         void Tick(bool allowed, float delta);
  82:         void Teleport(Vector3 position);
  83:     }
  84:     /// <summary>分别维护攻击方向与可见翻转，由Root显式调度。</summary>
  85:     public interface ICharacterOrientation
  86:     {
  87:         Vector3 AttackDirection { get; }
  88:         float VisualFacingX { get; }
  89:         void ResolveDirections(Vector2 moveInput, Vector3? pointerDirection, Vector2 stickDirection, bool movementFacing);
  90:         void OrientTowards(Vector3 worldPosition);
  91:         void FaceAttackDirection();
  92:     }
  93:     /// <summary>从角色状态更新表现与暂停，不反向产生命中。</summary>
  94:     public interface ICharacterPresentation : ICharacterSubsystem
  95:     {
  96:         string CurrentClip { get; }
  97:         void SetPaused(bool paused);
  98:         void Present();
  99:     }
 100: }
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

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterInputHandler.cs
SHA256 841B3E00CD8B0D8FEAFD093A28C68CF2E72436867C8CFBA15D876D22E3EDC71F
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
  17:         public bool attackHeld, dodgePressed, cancel, pause, hasPointer, hasDirectionalAim, attackPointerBlocked;
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
  35:         [Header("取消与暂停")]
  36:         public InputAction cancelAction = new InputAction("Cancel", InputActionType.Button);
  37:         public InputAction pauseAction = new InputAction("Pause", InputActionType.Button);
  38:         [Header("开发检查入口")]
  39:         public InputAction debugValidAction = new InputAction("DebugValid", InputActionType.Button);
  40:         public InputAction debugBlockedAction = new InputAction("DebugBlocked", InputActionType.Button);
  41:         public InputAction debugResetAction = new InputAction("DebugReset", InputActionType.Button);
  42:         public InputAction debugInspectAction = new InputAction("DebugInspect", InputActionType.Button);
  43: 
  44:         private readonly Dictionary<object, Rect> pointerRegions = new Dictionary<object, Rect>();
  45:         private ICharacterPlayerControlSource playerControl;
  46:         private bool managedPlayerInput, attackArmed = true;
  47:         private bool movementArmed=true;
  48:         private bool dodgeArmed;
  49: 
  50:         public bool HasPlayerInput => !managedPlayerInput || (ControlSourceAlive && playerControl.IsPlayerInputAllowed(this));
  51:         private bool ControlSourceAlive => playerControl != null &&
  52:             (!(playerControl is Object unityObject) || unityObject != null);
  53: 
  54:         public bool CanBindPlayerControl(ICharacterPlayerControlSource source)
  55:             => source != null && (!managedPlayerInput || !ControlSourceAlive || ReferenceEquals(playerControl, source));
  56: 
  57:         /// <summary>绑定唯一场景权威；拒绝另一存活协调者抢占同一输入。</summary>
  58:         public bool BindPlayerControl(ICharacterPlayerControlSource source)
  59:         {
  60:             if (!CanBindPlayerControl(source)) return false;
  61:             playerControl = source;
  62:             managedPlayerInput = true;
  63:             ResetPlayerInput();
  64:             return true;
  65:         }
  66: 
  67:         /// <summary>移交时清掉旧快照；不假装一次松键，必须真正观察释放后才重武装攻击。</summary>
  68:         public void ResetPlayerInput(bool requireMovementRelease=false)
  69:         { CurrentFrame=default; attackArmed=false; dodgeArmed=false; if (requireMovementRelease) movementArmed=false; }
  70:         public void RegisterPointerRegion(object owner, Rect region) { pointerRegions[owner] = region; }
  71:         public void RemovePointerRegion(object owner) { pointerRegions.Remove(owner); }
  72: 
  73:         private bool IsPointerBlocked(Vector2 pointer)
  74:         {
  75:             if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return true;
  76:             var guiPoint = new Vector2(pointer.x, Screen.height - pointer.y);
  77:             foreach (var region in pointerRegions.Values) if (region.Contains(guiPoint)) return true;
  78:             return false;
  79:         }
  80: 
  81:         public CharacterInputFrame CurrentFrame { get; private set; }
  82: 
  83:         public bool IsConfigured => movementAction != null && movementAction.bindings.Count > 0 &&
  84:             attackAction != null && attackAction.bindings.Count > 0 &&
  85:             ((mouseAction != null && mouseAction.bindings.Count > 0) ||
  86:              (gamepadAimAction != null && gamepadAimAction.bindings.Count > 0));
  87: 
  88:         private IEnumerable<InputAction> Actions()
  89:         {
  90:             // 同步枚举仅统一启用/禁用/释放清单；不是Unity协程，没有跨帧等待。
  91:             yield return movementAction;
  92:             yield return mouseAction;
  93:             yield return gamepadAimAction;
  94:             yield return attackAction;
  95:             yield return dodgeAction;
  96:             yield return cancelAction;
  97:             yield return pauseAction;
  98:             yield return debugValidAction;
  99:             yield return debugBlockedAction;
 100:             yield return debugResetAction;
 101:             yield return debugInspectAction;
 102:         }
 103: 
 104:         private void OnEnable() { foreach (var action in Actions()) if (action != null) action.Enable(); }
 105:         private void OnDisable() { ResetPlayerInput(); foreach (var action in Actions()) if (action != null) action.Disable(); }
 106:         private void OnDestroy() { foreach (var action in Actions()) if (action != null) action.Dispose(); }
 107: 
 108:         public CharacterInputFrame Read()
 109:         {
 110:             var frame = new CharacterInputFrame();
 111:             // 非实控在读取设备前返回空帧；Root仍推进自己已接受的动作，不会偷读另一角色输入。
 112:             if (!HasPlayerInput || !isActiveAndEnabled || movementAction == null || !movementAction.enabled)
 113:             { CurrentFrame = frame; return frame; }
 114:             frame.move = movementAction.ReadValue<Vector2>();
 115:             if (!movementArmed)
 116:             {
 117:                 // 死亡交接不能把旧WASD/摇杆位移转发给幸存者；必须真实回中才重新采移动。
 118:                 if (frame.move.sqrMagnitude<.0001f) movementArmed=true;
 119:                 frame.move=Vector2.zero;
 120:             }
 121:             if (mouseAction != null && mouseAction.enabled)
 122:             {
 123:                 frame.pointer = mouseAction.ReadValue<Vector2>();
 124:                 frame.hasPointer = mouseAction.controls.Count > 0 && frame.pointer.x >= 0 && frame.pointer.x <= Screen.width &&
 125:                     frame.pointer.y >= 0 && frame.pointer.y <= Screen.height && !IsPointerBlocked(frame.pointer);
 126:             }
 127:             if (gamepadAimAction != null && gamepadAimAction.enabled)
 128:             {
 129:                 frame.aimDirection = gamepadAimAction.ReadValue<Vector2>();
 130:                 frame.hasDirectionalAim = frame.aimDirection.sqrMagnitude > .01f;
 131:             }
 132:             var held = attackAction != null && attackAction.enabled && attackAction.IsPressed();
 133:             if (!held) attackArmed = true;
 134:             frame.attackHeld = held && attackArmed;
 135:             var dodgeHeld=dodgeAction!=null&&dodgeAction.enabled&&dodgeAction.IsPressed();
 136:             if(!dodgeHeld)dodgeArmed=true;
 137:             frame.dodgePressed=dodgeHeld&&dodgeArmed&&Pressed(dodgeAction);
 138:             if(frame.dodgePressed)dodgeArmed=false;
 139:             if (attackAction != null && attackAction.activeControl != null && attackAction.activeControl.device is Pointer &&
 140:                 !frame.hasPointer) frame.attackPointerBlocked = true;
 141:             frame.cancel = Pressed(cancelAction);
 142:             frame.pause = Pressed(pauseAction);
 143:             frame.debugCommand = Pressed(debugValidAction) ? 1 : Pressed(debugBlockedAction) ? 2 :
 144:                 Pressed(debugResetAction) ? 3 : Pressed(debugInspectAction) ? 4 : 0;
 145:             CurrentFrame = frame;
 146:             return frame;
 147:         }
 148: 
 149:         private static bool Pressed(InputAction action) => action != null && action.enabled && action.WasPressedThisFrame();
 150:     }
 151: }
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

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterRoot.cs
SHA256 A04DF0F0592F2A699CD7CEAC146E55323080B8284B0D2959FB4F741B4F6AAD61
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
  34:         private CombatVitals autonomousTarget;
  35:         private bool autonomousAction;
  36:         public event Action<ActionFact> ActionObserved;
  37:         public double SimulationTime => runtime == null ? 0 : runtime.SimulationTime;
  38:         public ICompanionMoveCandidate CombatMove => combatBrain;
  39:         [Header("Prototype timing — not final balance")]
  40:         [SerializeField] private float releaseAt = .3f, moveReadyAt = .5f, attackReadyAt = .8f, finishAt = 1.1f;
  41:         [SerializeField, Min(1)] private int damage = 12;
  42:         [SerializeField, Min(0)] private float postureImpact;
  43:         [SerializeField] private bool verboseTrace;
  44:         private readonly ControlAuthority authority = new ControlAuthority();
  45:         private readonly AttackIntent intent = new AttackIntent();
  46:         private ICharacterInputSource inputSource;
  47:         private CharacterIntentSelector intentSelector;
  48:         private ICharacterMovement mover;
  49:         private ICharacterOrientation orienter;
  50:         private ICharacterPresentation presentation;
  51:         private ICombatHitResolver hitResolver;
  52:         private BasicActionRuntime runtime;
  53:         private BasicAttackDefinition definition;
  54:         private BasicAttackDefinition secondDefinition;
  55:         private BasicChainState chain;
  56:         private BasicChainProfile chainProfile;
  57:         private ICombatHitReceiptSource hitReceipts;
  58:         public int AttackStage { get; private set; }
  59:         public int ReleaseCount { get; private set; }
  60:         public bool HasBufferedAttack => chain!=null && chain.HasBuffer;
  61:         public double ComboWindowUntil => chain==null?-1:chain.WindowUntil;
  62:         private bool ready, focused = true, paused, applicationPaused;
  63:         private string lastFact = "Idle";
  64:         private object respawnOwner;
  65:         private int lifeGeneration, animationGeneration;
  66:         public int LifeGeneration => lifeGeneration;
  67:         public bool IsSimulationSuspended => !focused || IsPaused;
  68:         public CharacterDataBus DataBus { get; private set; }
  69:         public string ActorId => actorId;
  70:         public string LastFact => lastFact;
  71:         public string LastActionSource { get; private set; } = "none";
  72:         public bool IsPaused => paused || applicationPaused;
  73:         public bool ActionActive => DataBus != null && DataBus.ActionActive;
  74:         public bool IsAlive => lifeState==null || lifeState.IsAlive;
  75:         public bool CanSimulateIntent => ready && IsAlive && isActiveAndEnabled && focused && !IsPaused && (posture == null || !posture.IsStaggered);
  76:         public CombatVitals Vitals => vitals;
  77:         public event Action<CharacterRoot> Died;
  78:         public CharacterFollow Follow => follow;
  79:         public CharacterIntentKind EffectiveIntentKind => intentSelector == null ? CharacterIntentKind.Neutral : intentSelector.EffectiveKind;
  80: 
  81:         public bool CanBindCompanionIntent(ICharacterControlSession session, ICompanionIntentSource source)
  82:             => ready && follow != null && ReferenceEquals(source,follow) && intentSelector != null && intentSelector.CanBind(session,source);
  83:         public bool BindCompanionIntent(ICharacterControlSession session, ICompanionIntentSource source)
  84:             => CanBindCompanionIntent(session,source) && intentSelector.Bind(session,source);
  85:         public void ClearCompanionIntent(ICompanionIntentSource owner)
  86:         { if (intentSelector != null) intentSelector.Clear(owner); }
  87: 
  88:         internal bool BindRespawnOwner(object owner)
  89:         {
  90:             if(owner==null || (respawnOwner!=null && !ReferenceEquals(respawnOwner,owner))) return false;
  91:             respawnOwner=owner; return true;
  92:         }
  93:         internal void UnbindRespawnOwner(object owner)
  94:         { if(ReferenceEquals(respawnOwner,owner)) respawnOwner=null; }
  95:         internal bool CanRespawn(object owner) => ready && isActiveAndEnabled && !IsAlive &&
  96:             vitals!=null && vitals.isActiveAndEnabled && ReferenceEquals(respawnOwner,owner);
  97:         internal bool Respawn(object owner, Vector3 position)
  98:         {
  99:             if(!CanRespawn(owner)) return false;
 100:             CancelAction("scene-respawn");
 101:             if(follow!=null) follow.ResetIntent();
 102:             if(intentSelector!=null) intentSelector.Clear();
 103:             input.ResetPlayerInput(true);
 104:             orientation.ResetOrientation();
 105:             movement.Teleport(position);
 106:             if(posture!=null) posture.ResetState();
 107:             if(!vitals.RestoreAfterDeath()) return false;
 108:             stamina?.Restore();
 109:             lifeGeneration++;
 110:             return true;
 111:         }
 112: 
 113:         private void Awake()
 114:         {
 115:             if (viewCamera == null) viewCamera = Camera.main;
 116:             if (vitals!=null)
 117:             {
 118:                 if (!vitals.Initialize(actorId)) { Debug.LogError("Character vitals identity/configuration invalid",this); enabled=false; return; }
 119:                 lifeState=vitals; vitals.Died+=OnDied;
 120:             }
 121:             if (movement == null || orientation == null || view == null || melee == null || input == null || viewCamera == null)
 122:             { Debug.LogError("Character requires registered input/movement/orientation/view/melee/camera", this); enabled = false; return; }
 123:             // Inspector保留具体组件槽；运行期通过窄接口消费，不反向依赖开发GUI或测试靶。
 124:             // 每角色只有这一条有效Frame入口；没有Director绑定时保持原玩家适配器行为。
 125:             intentSelector = new CharacterIntentSelector(this,input);
 126:             inputSource = intentSelector;
 127:             combatBrain=GetComponent<CompanionCombatBrain>();
 128:             if(combatBrain!=null) combatBrain.Initialize(this);
 129:             mover = movement;
 130:             orienter = orientation;
 131:             presentation = view;
 132:             hitResolver = melee;
 133:             if (!inputSource.IsConfigured) { Debug.LogError("Character actions are not configured", this); enabled = false; return; }
 134:             try
 135:             {
 136:                 definition = new BasicAttackDefinition(releaseAt, moveReadyAt, attackReadyAt, finishAt, damage, postureImpact);
 137:                 runtime = new BasicActionRuntime(actorId);
 138:                 chainProfile=GetComponent<BasicChainProfile>();
 139:                 // Root的早期Awake可能先于Profile.OnEnable；此处读取序列化enabled，运行期再查isActiveAndEnabled。
 140:                 if(chainProfile!=null && chainProfile.enabled)
 141:                 {
 142:                     definition=chainProfile.stage1.Freeze(); secondDefinition=chainProfile.stage2.Freeze();
 143:                     chain=chainProfile.CreateState(); hitReceipts=melee; hitReceipts.Resolved+=OnHitReceipt;
 144:                 }
 145:             }
 146:             catch (ArgumentException error) { Debug.LogError("Character configuration: " + error.Message, this); enabled = false; return; }
 147:             // 每角色独立Bus只读取原所有者；只有确需Bus的运动和表现执行初始化。
 148:             DataBus = new CharacterDataBus(actorId, inputSource, mover, orienter, runtime, authority);
 149:             stamina=GetComponent<CharacterStamina>();if(stamina!=null)stamina.Initialize();
 150:             dodge=GetComponent<CharacterDodge>();if(dodge!=null)dodge.Initialize(this,stamina);
 151:             DataBus.BindResources(stamina,dodge);
 152:             ready = InitializeSubsystems(DataBus, mover, presentation);
 153:             if (!ready) { Debug.LogError("Character subsystem initialization failed", this); enabled = false; }
 154:             // 可选能力只接受本角色初始化；无组件/无assignment的旧场保持普通玩家行为。
 155:             if (follow != null && !follow.Initialize(this)) Debug.LogWarning("Character follow unavailable: configuration or owner invalid", this);
 156:             if (posture != null)
 157:             {
 158:                 if (!posture.Initialize(vitals, this)) { Debug.LogError("Character posture configuration invalid", this); enabled = false; return; }
 159:                 posture.Broken += OnPostureBroken;
 160:                 posture.Recovered += OnPostureRecovered;
 161:             }
 162:         }
 163: 
 164:         private static bool InitializeSubsystems(CharacterDataBus dataBus, params ICharacterSubsystem[] subsystems)
 165:         {
 166:             foreach (var subsystem in subsystems) if (subsystem == null || !subsystem.Initialize(dataBus)) return false;
 167:             return true;
 168:         }
 169: 
 170:         private void Update()
 171:         {
 172:             if (!ready) return;
 173:             if (!IsAlive) { CommitPresentation(); return; }
 174:             // 本体显式调度入口：以下阶段在一次Update中按书写顺序执行。
 175:             // DefaultExecutionOrder只约束跨组件先后，不能替代这里的模块调用顺序。
 176:             // 1. 采唯一意图源并处理暂停/取消；不由各子模块另开输入Update。
 177:             // 选择器只采一个移动源；战斗能力另交窄动作候选，切人屏障为空帧，动作时钟仍继续。
 178:             combatBrain?.Sample(runtime.SimulationTime, Time.deltaTime);
 179:             inputSource.Read();
 180:             var frame = DataBus.Input;
 181:             if(chain!=null && (!input.HasPlayerInput || IsPaused || !focused || chainProfile==null || !chainProfile.isActiveAndEnabled)) chain.Clear();
 182:             if (frame.pause) SetPaused(!paused);
 183:             if (frame.cancel) CancelAction("player-cancel");
 184:             // 2. 同一根时钟推进架势；主动作暂停自然恢复，暂停/失焦完全停钟。
 185:             // 在Request前解除到期的本owner禁令；不会回滚其他根已释放的命中。
 186:             if (posture != null) posture.Tick(IsPaused || !focused ? 0 : Time.deltaTime, runtime.IsActive);
 187:             // 3. 先解算朝向，攻击释放才能读取本帧鼠标/键盘方向。
 188:             var canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
 189:             if (focused && !IsPaused)
 190:                 orienter.ResolveDirections(frame.move, PointerDirection(frame), frame.hasDirectionalAim ? frame.aimDirection : Vector2.zero, canMove);
 191:             // 3b. 可选本体机动在同一根时钟采样/推进；失控、暂停、破势立即撤销免伤资格。
 192:             if(dodge!=null)
 193:             {
 194:                 if(!input.HasPlayerInput||!CanSimulateIntent)dodge.Cancel();
 195:                 dodge.Tick(runtime.SimulationTime);
 196:                 if(frame.dodgePressed)dodge.Request(frame.move,input.HasPlayerInput);
 197:             }
 198:             // 4. 观察按键边沿，再由Runtime的暂停/权限/Ready资格决定是否接受动作。
 199:             if (intent.Observe(frame.attackHeld) && focused && !frame.attackPointerBlocked)
 200:             {
 201:                 var stage=chain==null?1:chain.Request(runtime.SimulationTime,runtime.AttackReady,
 202:                     input.HasPlayerInput && !IsPaused && authority.Allows(ControlChannel.Attack) && chainProfile!=null && chainProfile.isActiveAndEnabled);
 203:                 if(stage>0) RequestStage(stage);
 204:             }
 205:             // 4b. 窄自治请求与玩家边沿互斥；仅Stage1，不通过输入帧注入攻击/暂停。
 206:             if(!input.HasPlayerInput && CanSimulateIntent && combatBrain!=null &&
 207:                 ((ICompanionActionCandidate)combatBrain).TryGetAttackTarget(out var aiTarget))
 208:                 RequestStage(1,aiTarget);
 209:             if(autonomousAction && runtime.IsActive)
 210:             {
 211:                 if(combatBrain==null || !combatBrain.IsAcceptedTargetValid(autonomousTarget)) CancelAction("ai-target-invalid");
 212:                 else orienter.OrientTowards(autonomousTarget.transform.position);
 213:             }
 214:             // 5. 唯一动作时钟推进与Release命中。Accepted不扣血，切人不重发事实。
 215:             foreach (var fact in runtime.Advance(Time.deltaTime, IsPaused || !focused))
 216:             {
 217:                 Observe(fact);
 218:                 if (fact.Release != null)
 219:                 {
 220:                     ReleaseCount++;
 221:                     // 在释放当帧按值采样方向；不是接受动作时锁定，前摇中仍可重新瞄准。
 222:                     var releaseDirection = orienter.AttackDirection;
 223:                     orienter.FaceAttackDirection();
 224:                     hitResolver.Resolve(fact.Release, releaseDirection);
 225:                 }
 226:             }
 227:             if(chain!=null && input.HasPlayerInput && !IsPaused && focused && authority.Allows(ControlChannel.Attack))
 228:             {
 229:                 var bufferedStage=chain.Poll(runtime.SimulationTime,runtime.AttackReady);
 230:                 if(bufferedStage>0) RequestStage(bufferedStage);
 231:             }
 232:             // 6. 动作/命中可能改变MoveReady或架势权限，重新查询后才执行真实移动。
 233:             dodge?.Tick(runtime.SimulationTime);
 234:             canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
 235:             mover.Tick(canMove, IsPaused || !focused ? 0 : Time.deltaTime);
 236:             stamina?.Tick(runtime.SimulationTime,!runtime.IsActive&&(dodge==null||!dodge.IsActive)&&CanSimulateIntent);
 237:             // 7. 最后提交表现，Run读取已完成的实际位移。
 238:             // 当前没有本体IK/骨骼后处理，故不虚设LateUpdate；Spine内部播放仍由插件驱动。
 239:             CommitPresentation();
 240:         }
 241: 
 242:         // 7. Animation Intent → One Commit；无子模块Update争抢主轨。
 243:         private void CommitPresentation()
 244:         {
 245:             (presentation as Ossastra.AnimationPresentation.ICharacterAnimationFacts)?.CollectFacts(IsAlive, posture!=null && posture.IsStaggered, vitals!=null && vitals.IsHitReacting, animationGeneration, AttackStage);
 246:             presentation.SetPaused(IsPaused || !focused);
 247:             presentation.Present();
 248:         }
 249:         private Vector3? PointerDirection(CharacterInputFrame frame)
 250:         {
 251:             if (!frame.hasPointer) return null;
 252:             var plane = new Plane(Vector3.up, transform.position);
 253:             var ray = viewCamera.ScreenPointToRay(frame.pointer);
 254:             if (!plane.Raycast(ray, out var distance)) return null;
 255:             var direction = ray.GetPoint(distance) - transform.position;
 256:             direction.y = 0;
 257:             return direction.sqrMagnitude < .0001f ? (Vector3?)null : direction;
 258:         }
 259: 
 260:         private void Observe(ActionFact fact)
 261:         {
 262:             LastActionSource=fact.Source;
 263:             lastFact = fact.Kind + " @" + fact.At.ToString("F3") + " " + fact.ActionId.ToString("N").Substring(0, 8);
 264:             if (fact.Reason != null) lastFact += " " + fact.Reason;
 265:             if (verboseTrace) Debug.Log("Character " + lastFact, this);
 266:             ActionObserved?.Invoke(fact);
 267:         }
 268:         private void RequestStage(int stage, CombatVitals aiTarget = null)
 269:         {
 270:             if((dodge!=null&&dodge.IsActive)||(stamina!=null&&!stamina.CanSpend(stamina.AttackCost)))
 271:             {lastFact="Rejected: dodge-active/stamina";return;}
 272:             var selected=stage==2?secondDefinition:definition;
 273:             var request=runtime.Request(selected,aiTarget==null?"player-input:Stage"+stage:"companion:Stage1",authority,IsPaused);
 274:             if(request.PreviousTerminal!=null) Observe(request.PreviousTerminal);
 275:             if(request.Accepted)
 276:             {
 277:                 // 同步核准后才能付款；Request没有外部回调，余额不会在检查和此处之间被异步改写。
 278:                 if(stamina!=null&&!stamina.Spend(request.Fact.ActionId,stamina.AttackCost,runtime.SimulationTime,request.Fact.Source))
 279:                 {runtime.Cancel("stamina-acceptance-conflict");throw new InvalidOperationException("Accepted attack payment failed");}
 280:                 AttackStage=stage; autonomousAction=aiTarget!=null; autonomousTarget=aiTarget;
 281:                 if(autonomousAction) { chain?.Clear(); combatBrain.OnAccepted(); orienter.OrientTowards(aiTarget.transform.position); }
 282:                 else chain?.Accepted(stage,request.Fact.ActionId,runtime.SimulationTime,selected.AttackReadyAt);
 283:                 Observe(request.Fact); orienter.FaceAttackDirection();
 284:             }
 285:             else lastFact="Rejected: "+request.Reason;
 286:         }
 287:         private void OnHitReceipt(Guid id,bool applied){if(!autonomousAction)chain?.Hit(id,applied);}
 288: 
 289:         public void CancelAction(string reason)
 290:         {
 291:             dodge?.Cancel();
 292:             animationGeneration++; autonomousAction=false; autonomousTarget=null; combatBrain?.ResetCandidate();
 293:             chain?.Clear();
 294:             // 取消必须具名；抑制仍按住的输入，不能把下一帧误判为新请求。
 295:             intent.SuppressUntilRelease();
 296:             var terminal = runtime == null ? null : runtime.Cancel(reason);
 297:             if (terminal != null) Observe(terminal);
 298:         }
 299: 
 300:         private void OnPostureBroken(string cause)
 301:         {
 302:             authority.Block(posture, ControlChannel.Move | ControlChannel.Attack);
 303:             CancelAction("posture-broken:" + cause);
 304:             if (follow != null) follow.ResetIntent();
 305:         }
 306:         private void OnPostureRecovered()
 307:         {
 308:             authority.Release(posture);
 309:             intent.SuppressUntilRelease();
 310:             if (follow != null) follow.ResetIntent();
 311:         }
 312: 
 313:         private void OnDied(HitOutcome outcome)
 314:         {
 315:             // HP已由结算归零；这里仅终止本体未释放动作/有效帧，再交唯一会话处理实控。
 316:             CancelAction("death:"+outcome.ActorId+":"+outcome.ActionId.ToString("N"));
 317:             if (follow!=null) follow.ResetIntent();
 318:             if (intentSelector!=null) intentSelector.Clear();
 319:             Died?.Invoke(this);
 320:         }
 321: 
 322:         public void SetPaused(bool value) { paused = value; if(value)dodge?.Cancel(); chain?.Clear(); intent.SuppressUntilRelease(); if (follow != null) follow.ResetIntent(); }
 323:         public void SetAttackRestriction(object owner, bool restricted, string reason = "attack-restricted")
 324:         {
 325:             if (restricted) { authority.Block(owner, ControlChannel.Attack); CancelAction(reason); }
 326:             else authority.Release(owner);
 327:         }
 328: 
 329:         private void OnApplicationFocus(bool hasFocus)
 330:         { focused = hasFocus; if (follow != null) follow.ResetIntent(); if (!hasFocus) CancelAction("focus-lost"); else intent.SuppressUntilRelease(); }
 331:         private void OnApplicationPause(bool value)
 332:           { applicationPaused = value; if(value)chain?.Clear(); if (follow != null) follow.ResetIntent(); if (value) intent.SuppressUntilRelease(); }
 333:         private void OnEnable() { intent.SuppressUntilRelease(); if (intentSelector != null) intentSelector.Clear(); }
 334:         private void OnDisable() { CancelAction("disabled"); if (posture != null) posture.ResetState(); authority.Release(posture); if (follow != null) follow.ResetIntent(); if (intentSelector != null) intentSelector.Clear(); if (ready && presentation != null) presentation.SetPaused(false); }
 335:         private void OnDestroy()
 336:         {
 337:             if(hitReceipts!=null) hitReceipts.Resolved-=OnHitReceipt;
 338:             if (vitals!=null) vitals.Died-=OnDied;
 339:             if (posture != null) { posture.Broken -= OnPostureBroken; posture.Recovered -= OnPostureRecovered; }
 340:             authority.Release(posture);
 341:             // 终止本角色动作后解除本对象引用；不清其它角色或场景控制权威。
 342:             CancelAction("destroyed");
 343:             ready = false;
 344:             DataBus = null;
 345:             runtime = null;
 346:             inputSource = null;
 347:             if (intentSelector != null) intentSelector.Clear();
 348:             intentSelector = null;
 349:             mover = null;
 350:             orienter = null;
 351:             presentation = null;
 352:             hitResolver = null;
 353:         }
 354:     }
 355: }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterStamina.cs
SHA256 6B72C24F515E01515318059894F709D28713BF7B3201FF27E78A2FCA4FE0BE0D
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
  27:     }
  28: }
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
SHA256 C67B957E50750B50C9512DE55ADD63EEFB7D65851479EEEBEA2D34C1D7EA2634
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
  10:  private void OnGUI(){if(session==null)return;var rect=new Rect(12,12,390,235);foreach(var root in session.Characters)root.GetComponent<CharacterInputHandler>().RegisterPointerRegion(this,rect);
  11:  GUILayout.BeginArea(rect,GUI.skin.box);GUILayout.Label("ROADMAP03 · SAMPLE / 正式敌美术ASSET_BLOCKED");GUILayout.Label("WASD移动 / LMB主手 / Shift闪避 / Tab切人");
  12:  foreach(var root in session.Characters){var s=root.GetComponent<CharacterStamina>();var d=root.GetComponent<CharacterDodge>();GUILayout.Label(root.ActorId+(session.Current==root?" [Current]":"")+" HP "+root.Vitals.Health.Current+" 体力 "+(s==null?"无":s.Current.ToString("F1"))+" 闪避 "+(d!=null&&d.IsActive)+" 免伤 "+(d!=null&&d.IsEvading));}
  13:  var c=session.Current;if(c!=null){var s=c.GetComponent<CharacterStamina>();GUILayout.Label(s?.LastPayment??"无体力能力");GUILayout.BeginHorizontal();if(GUILayout.Button(new GUIContent("体力耗尽","开发：真实资源入口扣除当前体力，验证非法请求"))&&s!=null)s.Spend(Guid.NewGuid(),s.Current,c.SimulationTime,"Development-empty");if(GUILayout.Button(new GUIContent("恢复体力","开发：恢复同一本体资源实例，验证合法请求")))s?.Restore();GUILayout.EndHorizontal();}
  14:  GUILayout.Label("状态只读；调试不代表自然路径已通过。");GUILayout.EndArea();}
  15:  private void OnDisable(){if(session!=null)foreach(var root in session.Characters)if(root!=null)root.GetComponent<CharacterInputHandler>().RemovePointerRegion(this);}
  16: }}
```
