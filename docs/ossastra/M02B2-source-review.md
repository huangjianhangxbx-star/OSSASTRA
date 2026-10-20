# M02B2 完整源码审查快照
基准 bc34538 + 本轮工作树；具体版本以同目录source-manifest逐文件SHA为准。全部受影响区间待用户审查，技术通过不代签。
审查顺序：PostureState（状态唯一写入/跨阶段delta）→ CombatPosture（合法命中/驱动生命周期/事件）→ Contracts与Definition/Release/Runtime（冻结载荷和一次HP）→ Melee（死亡优先）→ 两根（显式时序/具名权限）→ Development（只读与显式重置）。

## Assets/OSSASTRA/Scripts/Characters/Core/AttackRelease.cs

SHA256: 350a1043e991cde427889822239c77e7145fd2341ae8b3dde2ceba70f2fac22a；81 行；全文件待审。

```csharp
   1: /*
   2:  * 脚本：AttackRelease
   3:  * 功能：结算一次动作释放的目标批次，并返回命中结果。
   4:  * 边界：同一释放只结算一次；按目标身份去重；HP只经Health写入。
   5:  * 协作：世界适配器先做空间筛选，再调用Resolve；空挥关闭批次。冻结架势载荷由同批存活结果消费。
   6:  */
   7: using System;
   8: using System.Collections.Generic;
   9: using System.Linq;
  10:
  11: namespace Ossastra.CharacterCore
  12: {
  13:     public enum HitReason { Applied, Duplicate, Dead, Self }
  14:
  15:     public sealed class HitOutcome
  16:     {
  17:         public HitReason Reason { get; }
  18:         public int HpBefore { get; }
  19:         public int HpAfter { get; }
  20:         public Guid ActionId { get; }
  21:         public string ActorId { get; }
  22:         public string TargetId { get; }
  23:
  24:         internal HitOutcome(HitReason reason, int before, Health target, Guid actionId, string actorId)
  25:         {
  26:             Reason = reason;
  27:             HpBefore = before;
  28:             HpAfter = target.Current;
  29:             TargetId = target.Id;
  30:             ActionId = actionId;
  31:             ActorId = actorId;
  32:         }
  33:     }
  34:
  35:     // 每次释放只能提交一个瞬时近战候选批次；空挥也关闭，避免永久开启命中窗口。
  36:     public sealed class AttackRelease
  37:     {
  38:         private readonly Guid actionId;
  39:         private readonly string actorId;
  40:         private readonly int damage;
  41:         private bool resolved;
  42:         public string ActorId => actorId;
  43:         public Guid ActionId => actionId;
  44:         public double PostureImpact { get; }
  45:
  46:         internal AttackRelease(Guid actionId, string actorId, int damage, double postureImpact = 0)
  47:         {
  48:             this.actionId = actionId;
  49:             this.actorId = actorId;
  50:             this.damage = damage;
  51:             PostureImpact = postureImpact;
  52:         }
  53:
  54:         // 调用方必须先完成实际空间/阵营过滤；本方法不谎称已做物理检测。
  55:         public IReadOnlyList<HitOutcome> Resolve(IEnumerable<Health> targets)
  56:         {
  57:             if (resolved) throw new InvalidOperationException("Attack release already resolved");
  58:             if (targets == null) throw new ArgumentNullException(nameof(targets));
  59:             var batch = targets.ToArray();
  60:             if (batch.Any(t => t == null)) throw new ArgumentException("Null candidate target", nameof(targets));
  61:             resolved = true;
  62:             var seen = new HashSet<string>(StringComparer.Ordinal);
  63:             var outcomes = new List<HitOutcome>();
  64:             foreach (var target in batch)
  65:             {
  66:                 var before = target.Current;
  67:                 HitReason reason;
  68:                 if (!seen.Add(target.Id)) reason = HitReason.Duplicate;
  69:                 else if (target.Id == actorId) reason = HitReason.Self;
  70:                 else if (target.Current == 0) reason = HitReason.Dead;
  71:                 else
  72:                 {
  73:                     target.ApplyDamage(damage);
  74:                     reason = HitReason.Applied;
  75:                 }
  76:                 outcomes.Add(new HitOutcome(reason, before, target, actionId, actorId));
  77:             }
  78:             return outcomes;
  79:         }
  80:     }
  81: }
```

## Assets/OSSASTRA/Scripts/Characters/Core/BasicActionRuntime.cs

SHA256: 7058e22dbfd2970ab3b31f74d123b027e51d2be299073a9c65ba5946a8bde9f0；144 行；全文件待审。

```csharp
   1: /*
   2:  * 脚本：BasicActionRuntime
   3:  * 功能：维护单角色的动作身份、时钟与Accepted／Released／Ready／终结事实。
   4:  * 边界：不采设备、不驱动Spine、不查询碰撞、不直接改HP。
   5:  * 协作：Root请求并推进；Release对象交世界适配器；暂停不推进时钟。
   6:  */
   7: using System;
   8: using System.Collections.Generic;
   9: using System.Linq;
  10:
  11: namespace Ossastra.CharacterCore
  12: {
  13:     public enum ActionFactKind { Accepted, Released, MoveReady, AttackReady, Finished, Cancelled }
  14:
  15:     // 事实是结果，不是任意广播；调用者按返回顺序处理。
  16:     public sealed class ActionFact
  17:     {
  18:         public ActionFactKind Kind { get; }
  19:         public Guid ActionId { get; }
  20:         public string Source { get; }
  21:         public string Reason { get; }
  22:         public double At { get; }
  23:         public AttackRelease Release { get; }
  24:
  25:         internal ActionFact(ActionFactKind kind, Guid id, string source, double at,
  26:             string reason = null, AttackRelease release = null)
  27:         {
  28:             Kind = kind;
  29:             ActionId = id;
  30:             Source = source;
  31:             At = at;
  32:             Reason = reason;
  33:             Release = release;
  34:         }
  35:     }
  36:
  37:     public sealed class ActionRequestResult
  38:     {
  39:         public bool Accepted => Fact != null;
  40:         public string Reason { get; }
  41:         public ActionFact Fact { get; }
  42:         public ActionFact PreviousTerminal { get; }
  43:
  44:         internal ActionRequestResult(string reason, ActionFact fact = null, ActionFact previousTerminal = null)
  45:         {
  46:             Reason = reason;
  47:             Fact = fact;
  48:             PreviousTerminal = previousTerminal;
  49:         }
  50:     }
  51:
  52:     // 单角色单动作权威，不读设备/Spine/世界碰撞，也不直接写HP。
  53:     public sealed class BasicActionRuntime
  54:     {
  55:         private readonly string actorId;
  56:         private BasicAttackDefinition definition;
  57:         private (double At, ActionFactKind Kind)[] nodes;
  58:         private int nextNode;
  59:         private double elapsed;
  60:         private double timeCorrection;
  61:         private Guid actionId;
  62:         private string source;
  63:         private bool moveReady;
  64:         private bool attackReady;
  65:
  66:         public bool IsActive => definition != null;
  67:         public bool MoveReady => !IsActive || moveReady;
  68:         public bool AttackReady => !IsActive || attackReady;
  69:         public Guid ActionId => IsActive ? actionId : Guid.Empty;
  70:
  71:         public BasicActionRuntime(string actorId)
  72:         {
  73:             if (string.IsNullOrWhiteSpace(actorId)) throw new ArgumentException("Actor identity required", nameof(actorId));
  74:             this.actorId = actorId;
  75:         }
  76:
  77:         public ActionRequestResult Request(BasicAttackDefinition attack, string requestSource,
  78:             ControlAuthority gate, bool paused = false)
  79:         {
  80:             if (attack == null) throw new ArgumentNullException(nameof(attack));
  81:             if (gate == null) throw new ArgumentNullException(nameof(gate));
  82:             if (string.IsNullOrWhiteSpace(requestSource)) throw new ArgumentException("Request source required", nameof(requestSource));
  83:             if (paused) return new ActionRequestResult("paused");
  84:             if (!gate.Allows(ControlChannel.Attack)) return new ActionRequestResult("attack-blocked");
  85:             if (IsActive && !attackReady) return new ActionRequestResult("busy");
  86:             // Ready允许一次新按下；显式终结旧身体动作，不遗失其已释放效果。
  87:             var previousTerminal = IsActive ? Cancel("replaced-by-next-attack") : null;
  88:             definition = attack;
  89:             source = requestSource;
  90:             actionId = Guid.NewGuid();
  91:             elapsed = 0;
  92:             timeCorrection = 0;
  93:             nextNode = 0;
  94:             moveReady = false;
  95:             attackReady = false;
  96:             // 两种Ready可互换先后；同一时刻固定Release→Move→Attack→Finish。
  97:             nodes = new[] {
  98:                 (attack.ReleaseAt, ActionFactKind.Released),
  99:                 (attack.MoveReadyAt, ActionFactKind.MoveReady),
 100:                 (attack.AttackReadyAt, ActionFactKind.AttackReady),
 101:                 (attack.FinishAt, ActionFactKind.Finished)
 102:             }.OrderBy(n => n.Item1).ThenBy(n => n.Item2).ToArray();
 103:             return new ActionRequestResult(null, Fact(ActionFactKind.Accepted, 0), previousTerminal);
 104:         }
 105:
 106:         public IReadOnlyList<ActionFact> Advance(double delta, bool paused)
 107:         {
 108:             if (!BasicAttackDefinition.Finite(delta) || delta < 0)
 109:                 throw new ArgumentOutOfRangeException(nameof(delta));
 110:             if (!IsActive || paused) return Array.Empty<ActionFact>();
 111:             // 补偿累计舍入误差；不靠提前Release的任意epsilon修补帧边界。
 112:             var correctedDelta = delta - timeCorrection;
 113:             var sum = elapsed + correctedDelta;
 114:             timeCorrection = (sum - elapsed) - correctedDelta;
 115:             elapsed = Math.Min(definition.FinishAt, sum);
 116:             if (elapsed == definition.FinishAt) timeCorrection = 0;
 117:             var facts = new List<ActionFact>();
 118:             while (nextNode < nodes.Length && elapsed >= nodes[nextNode].At)
 119:             {
 120:                 var node = nodes[nextNode++];
 121:                 AttackRelease release = null;
 122:                 if (node.Kind == ActionFactKind.Released)
 123:                     release = new AttackRelease(actionId, actorId, definition.Damage, definition.PostureImpact);
 124:                 if (node.Kind == ActionFactKind.MoveReady) moveReady = true;
 125:                 if (node.Kind == ActionFactKind.AttackReady) attackReady = true;
 126:                 facts.Add(Fact(node.Kind, node.At, release: release));
 127:                 if (node.Kind == ActionFactKind.Finished) definition = null;
 128:             }
 129:             return facts;
 130:         }
 131:
 132:         public ActionFact Cancel(string reason)
 133:         {
 134:             if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Cancellation cause required", nameof(reason));
 135:             if (!IsActive) return null;
 136:             var terminal = Fact(ActionFactKind.Cancelled, elapsed, reason);
 137:             definition = null;
 138:             return terminal;
 139:         }
 140:
 141:         private ActionFact Fact(ActionFactKind kind, double at, string reason = null, AttackRelease release = null)
 142:             => new ActionFact(kind, actionId, source, at, reason, release);
 143:     }
 144: }
```

## Assets/OSSASTRA/Scripts/Characters/Core/BasicAttackDefinition.cs

SHA256: 5dff2fa0d274d09260259f4501a52fddce6e72e6563d6a3c9fcfae1ec314e3e0；39 行；全文件待审。

```csharp
   1: /*
   2:  * 脚本：BasicAttackDefinition
   3:  * 功能：验证并保存一次基础攻击的不可变时间节点、HP伤害和架势冲击配置。
   4:  * 边界：不内置正式平衡默认值；拒绝无效时间顺序和非正伤害。
   5:  * 协作：由Root构造，BasicActionRuntime读取。
   6:  */
   7: using System;
   8:
   9: namespace Ossastra.CharacterCore
  10: {
  11:     // 全部配置不可变；本类没有游戏平衡默认值。
  12:     public sealed class BasicAttackDefinition
  13:     {
  14:         public double ReleaseAt { get; }
  15:         public double MoveReadyAt { get; }
  16:         public double AttackReadyAt { get; }
  17:         public double FinishAt { get; }
  18:         public int Damage { get; }
  19:         public double PostureImpact { get; }
  20:
  21:         public BasicAttackDefinition(double release, double move, double ready, double finish, int damage, double postureImpact = 0)
  22:         {
  23:             if (!Finite(release) || !Finite(move) || !Finite(ready) || !Finite(finish) ||
  24:                 release < 0 || move < release || ready < release || finish < move || finish < ready)
  25:                 throw new ArgumentOutOfRangeException(nameof(release), "Invalid action node ordering or time");
  26:             if (damage <= 0) throw new ArgumentOutOfRangeException(nameof(damage));
  27:             if (!Finite(postureImpact) || postureImpact < 0) throw new ArgumentOutOfRangeException(nameof(postureImpact));
  28:             ReleaseAt = release;
  29:             MoveReadyAt = move;
  30:             AttackReadyAt = ready;
  31:             FinishAt = finish;
  32:             Damage = damage;
  33:             // 与伤害一起冻结；默认零使未启用架势的旧场保持原行为。
  34:             PostureImpact = postureImpact;
  35:         }
  36:
  37:         internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
  38:     }
  39: }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterRoot.cs

SHA256: 8fac2632a771f0e484054acdd5ec3aaa7e83739da5a6b04e7dcf9852b92f62f2；239 行；全文件待审。

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
  31:         [Header("Prototype timing — not final balance")]
  32:         [SerializeField] private float releaseAt = .3f, moveReadyAt = .5f, attackReadyAt = .8f, finishAt = 1.1f;
  33:         [SerializeField, Min(1)] private int damage = 12;
  34:         [SerializeField, Min(0)] private float postureImpact;
  35:         [SerializeField] private bool verboseTrace;
  36:         private readonly ControlAuthority authority = new ControlAuthority();
  37:         private readonly AttackIntent intent = new AttackIntent();
  38:         private ICharacterInputSource inputSource;
  39:         private CharacterIntentSelector intentSelector;
  40:         private ICharacterMovement mover;
  41:         private ICharacterOrientation orienter;
  42:         private ICharacterPresentation presentation;
  43:         private ICombatHitResolver hitResolver;
  44:         private BasicActionRuntime runtime;
  45:         private BasicAttackDefinition definition;
  46:         private bool ready, focused = true, paused, applicationPaused;
  47:         private string lastFact = "Idle";
  48:         public CharacterDataBus DataBus { get; private set; }
  49:         public string ActorId => actorId;
  50:         public string LastFact => lastFact;
  51:         public bool IsPaused => paused || applicationPaused;
  52:         public bool ActionActive => DataBus != null && DataBus.ActionActive;
  53:         public bool IsAlive => lifeState==null || lifeState.IsAlive;
  54:         public bool CanSimulateIntent => ready && IsAlive && isActiveAndEnabled && focused && !IsPaused && (posture == null || !posture.IsStaggered);
  55:         public CombatVitals Vitals => vitals;
  56:         public event Action<CharacterRoot> Died;
  57:         public CharacterFollow Follow => follow;
  58:         public CharacterIntentKind EffectiveIntentKind => intentSelector == null ? CharacterIntentKind.Neutral : intentSelector.EffectiveKind;
  59:
  60:         public bool CanBindCompanionIntent(ICharacterControlSession session, ICompanionIntentSource source)
  61:             => ready && follow != null && ReferenceEquals(source,follow) && intentSelector != null && intentSelector.CanBind(session,source);
  62:         public bool BindCompanionIntent(ICharacterControlSession session, ICompanionIntentSource source)
  63:             => CanBindCompanionIntent(session,source) && intentSelector.Bind(session,source);
  64:         public void ClearCompanionIntent(ICompanionIntentSource owner)
  65:         { if (intentSelector != null) intentSelector.Clear(owner); }
  66:
  67:         private void Awake()
  68:         {
  69:             if (viewCamera == null) viewCamera = Camera.main;
  70:             if (vitals!=null)
  71:             {
  72:                 if (!vitals.Initialize(actorId)) { Debug.LogError("Character vitals identity/configuration invalid",this); enabled=false; return; }
  73:                 lifeState=vitals; vitals.Died+=OnDied;
  74:             }
  75:             if (movement == null || orientation == null || view == null || melee == null || input == null || viewCamera == null)
  76:             { Debug.LogError("Character requires registered input/movement/orientation/view/melee/camera", this); enabled = false; return; }
  77:             // Inspector保留具体组件槽；运行期通过窄接口消费，不反向依赖开发GUI或测试靶。
  78:             // 每角色只有这一条有效Frame入口；没有Director绑定时保持原玩家适配器行为。
  79:             intentSelector = new CharacterIntentSelector(this,input);
  80:             inputSource = intentSelector;
  81:             mover = movement;
  82:             orienter = orientation;
  83:             presentation = view;
  84:             hitResolver = melee;
  85:             if (!inputSource.IsConfigured) { Debug.LogError("Character actions are not configured", this); enabled = false; return; }
  86:             try
  87:             {
  88:                 definition = new BasicAttackDefinition(releaseAt, moveReadyAt, attackReadyAt, finishAt, damage, postureImpact);
  89:                 runtime = new BasicActionRuntime(actorId);
  90:             }
  91:             catch (ArgumentException error) { Debug.LogError("Character configuration: " + error.Message, this); enabled = false; return; }
  92:             // 每角色独立Bus只读取原所有者；只有确需Bus的运动和表现执行初始化。
  93:             DataBus = new CharacterDataBus(actorId, inputSource, mover, orienter, runtime, authority);
  94:             ready = InitializeSubsystems(DataBus, mover, presentation);
  95:             if (!ready) { Debug.LogError("Character subsystem initialization failed", this); enabled = false; }
  96:             // 可选能力只接受本角色初始化；无组件/无assignment的旧场保持普通玩家行为。
  97:             if (follow != null && !follow.Initialize(this)) Debug.LogWarning("Character follow unavailable: configuration or owner invalid", this);
  98:             if (posture != null)
  99:             {
 100:                 if (!posture.Initialize(vitals, this)) { Debug.LogError("Character posture configuration invalid", this); enabled = false; return; }
 101:                 posture.Broken += OnPostureBroken;
 102:                 posture.Recovered += OnPostureRecovered;
 103:             }
 104:         }
 105:
 106:         private static bool InitializeSubsystems(CharacterDataBus dataBus, params ICharacterSubsystem[] subsystems)
 107:         {
 108:             foreach (var subsystem in subsystems) if (subsystem == null || !subsystem.Initialize(dataBus)) return false;
 109:             return true;
 110:         }
 111:
 112:         private void Update()
 113:         {
 114:             if (!ready || !IsAlive) return;
 115:             // 本体显式调度入口：以下阶段在一次Update中按书写顺序执行。
 116:             // DefaultExecutionOrder只约束跨组件先后，不能替代这里的模块调用顺序。
 117:             // 1. 采唯一意图源并处理暂停/取消；不由各子模块另开输入Update。
 118:             // 选择器只采一个源；伙伴只能提供移动，切人屏障为空帧，动作时钟仍继续。
 119:             inputSource.Read();
 120:             var frame = DataBus.Input;
 121:             if (frame.pause) SetPaused(!paused);
 122:             if (frame.cancel) CancelAction("player-cancel");
 123:             // 2. 同一根时钟推进架势；主动作暂停自然恢复，暂停/失焦完全停钟。
 124:             // 在Request前解除到期的本owner禁令；不会回滚其他根已释放的命中。
 125:             if (posture != null) posture.Tick(IsPaused || !focused ? 0 : Time.deltaTime, runtime.IsActive);
 126:             // 3. 先解算朝向，攻击释放才能读取本帧鼠标/键盘方向。
 127:             var canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
 128:             if (focused && !IsPaused)
 129:                 orienter.ResolveDirections(frame.move, PointerDirection(frame), frame.hasDirectionalAim ? frame.aimDirection : Vector2.zero, canMove);
 130:             // 4. 观察按键边沿，再由Runtime的暂停/权限/Ready资格决定是否接受动作。
 131:             if (intent.Observe(frame.attackHeld) && focused && !frame.attackPointerBlocked)
 132:             {
 133:                 var request = runtime.Request(definition, "player-input", authority, IsPaused);
 134:                 if (request.PreviousTerminal != null) Observe(request.PreviousTerminal);
 135:                 if (request.Accepted) { Observe(request.Fact); orienter.FaceAttackDirection(); }
 136:                 else lastFact = "Rejected: " + request.Reason;
 137:             }
 138:             // 5. 唯一动作时钟推进与Release命中。Accepted不扣血，切人不重发事实。
 139:             foreach (var fact in runtime.Advance(Time.deltaTime, IsPaused || !focused))
 140:             {
 141:                 Observe(fact);
 142:                 if (fact.Release != null)
 143:                 {
 144:                     // 在释放当帧按值采样方向；不是接受动作时锁定，前摇中仍可重新瞄准。
 145:                     var releaseDirection = orienter.AttackDirection;
 146:                     orienter.FaceAttackDirection();
 147:                     hitResolver.Resolve(fact.Release, releaseDirection);
 148:                 }
 149:             }
 150:             // 6. 动作/命中可能改变MoveReady或架势权限，重新查询后才执行真实移动。
 151:             canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
 152:             mover.Tick(canMove, IsPaused || !focused ? 0 : Time.deltaTime);
 153:             // 7. 最后提交表现，Run读取已完成的实际位移。
 154:             // 当前没有本体IK/骨骼后处理，故不虚设LateUpdate；Spine内部播放仍由插件驱动。
 155:             presentation.SetPaused(IsPaused || !focused);
 156:             presentation.Present();
 157:         }
 158:
 159:         private Vector3? PointerDirection(CharacterInputFrame frame)
 160:         {
 161:             if (!frame.hasPointer) return null;
 162:             var plane = new Plane(Vector3.up, transform.position);
 163:             var ray = viewCamera.ScreenPointToRay(frame.pointer);
 164:             if (!plane.Raycast(ray, out var distance)) return null;
 165:             var direction = ray.GetPoint(distance) - transform.position;
 166:             direction.y = 0;
 167:             return direction.sqrMagnitude < .0001f ? (Vector3?)null : direction;
 168:         }
 169:
 170:         private void Observe(ActionFact fact)
 171:         {
 172:             lastFact = fact.Kind + " @" + fact.At.ToString("F3") + " " + fact.ActionId.ToString("N").Substring(0, 8);
 173:             if (fact.Reason != null) lastFact += " " + fact.Reason;
 174:             if (verboseTrace) Debug.Log("Character " + lastFact, this);
 175:         }
 176:
 177:         public void CancelAction(string reason)
 178:         {
 179:             // 取消必须具名；抑制仍按住的输入，不能把下一帧误判为新请求。
 180:             intent.SuppressUntilRelease();
 181:             var terminal = runtime == null ? null : runtime.Cancel(reason);
 182:             if (terminal != null) Observe(terminal);
 183:         }
 184:
 185:         private void OnPostureBroken(string cause)
 186:         {
 187:             authority.Block(posture, ControlChannel.Move | ControlChannel.Attack);
 188:             CancelAction("posture-broken:" + cause);
 189:             if (follow != null) follow.ResetIntent();
 190:         }
 191:         private void OnPostureRecovered()
 192:         {
 193:             authority.Release(posture);
 194:             intent.SuppressUntilRelease();
 195:             if (follow != null) follow.ResetIntent();
 196:         }
 197:
 198:         private void OnDied(HitOutcome outcome)
 199:         {
 200:             // HP已由结算归零；这里仅终止本体未释放动作/有效帧，再交唯一会话处理实控。
 201:             CancelAction("death:"+outcome.ActorId+":"+outcome.ActionId.ToString("N"));
 202:             if (follow!=null) follow.ResetIntent();
 203:             if (intentSelector!=null) intentSelector.Clear();
 204:             Died?.Invoke(this);
 205:         }
 206:
 207:         public void SetPaused(bool value) { paused = value; intent.SuppressUntilRelease(); if (follow != null) follow.ResetIntent(); }
 208:         public void SetAttackRestriction(object owner, bool restricted, string reason = "attack-restricted")
 209:         {
 210:             if (restricted) { authority.Block(owner, ControlChannel.Attack); CancelAction(reason); }
 211:             else authority.Release(owner);
 212:         }
 213:
 214:         private void OnApplicationFocus(bool hasFocus)
 215:         { focused = hasFocus; if (follow != null) follow.ResetIntent(); if (!hasFocus) CancelAction("focus-lost"); else intent.SuppressUntilRelease(); }
 216:         private void OnApplicationPause(bool value)
 217:         { applicationPaused = value; if (follow != null) follow.ResetIntent(); if (value) intent.SuppressUntilRelease(); }
 218:         private void OnEnable() { intent.SuppressUntilRelease(); if (intentSelector != null) intentSelector.Clear(); }
 219:         private void OnDisable() { CancelAction("disabled"); if (posture != null) posture.ResetState(); authority.Release(posture); if (follow != null) follow.ResetIntent(); if (intentSelector != null) intentSelector.Clear(); if (ready && presentation != null) presentation.SetPaused(false); }
 220:         private void OnDestroy()
 221:         {
 222:             if (vitals!=null) vitals.Died-=OnDied;
 223:             if (posture != null) { posture.Broken -= OnPostureBroken; posture.Recovered -= OnPostureRecovered; }
 224:             authority.Release(posture);
 225:             // 终止本角色动作后解除本对象引用；不清其它角色或场景控制权威。
 226:             CancelAction("destroyed");
 227:             ready = false;
 228:             DataBus = null;
 229:             runtime = null;
 230:             inputSource = null;
 231:             if (intentSelector != null) intentSelector.Clear();
 232:             intentSelector = null;
 233:             mover = null;
 234:             orienter = null;
 235:             presentation = null;
 236:             hitResolver = null;
 237:         }
 238:     }
 239: }
```

## Assets/OSSASTRA/Scripts/Combat/Core/PostureState.cs

SHA256: 933bc62d0c40ba3c7f3d3f471d7b7f2c4d90fbef79590a935e1fb4a76c4860b6；78 行；全文件待审。

```csharp
   1: /*
   2:  * 脚本：PostureState
   3:  * 功能：独占单实体架势、硬直及恢复计时；纯模型，不拥有HP或动作身份。
   4:  * 协作：CombatPosture接收合法命中，各自根只推进一次Tick。
   5:  * 边界：归零当次硬直、不续期；阶段跨越消费剩余delta，不重复计算时间。
   6:  */
   7: using System;
   8:
   9: namespace Ossastra.CombatCore
  10: {
  11:     public enum PosturePhase { Normal, Recovering, Staggered }
  12:
  13:     public sealed class PostureState
  14:     {
  15:         private readonly double staggerDuration, recoveryFraction, recoveryDelay, recoveryRate;
  16:         public double Maximum { get; }
  17:         public double Current { get; private set; }
  18:         public double StaggerRemaining { get; private set; }
  19:         public double RegenDelayRemaining { get; private set; }
  20:         public bool IsStaggered => StaggerRemaining > 0;
  21:         public PosturePhase Phase => IsStaggered ? PosturePhase.Staggered :
  22:             Current < Maximum ? PosturePhase.Recovering : PosturePhase.Normal;
  23:
  24:         public PostureState(double maximum, double duration, double fraction, double delay, double rate)
  25:         {
  26:             if (!Finite(maximum) || maximum <= 0 || !Finite(duration) || duration <= 0 ||
  27:                 !Finite(fraction) || fraction <= 0 || fraction > 1 ||
  28:                 !Finite(delay) || delay < 0 || !Finite(rate) || rate < 0)
  29:                 throw new ArgumentOutOfRangeException(nameof(maximum), "Invalid posture configuration");
  30:             Maximum = maximum;
  31:             staggerDuration = duration;
  32:             recoveryFraction = fraction;
  33:             recoveryDelay = delay;
  34:             recoveryRate = rate;
  35:             Reset();
  36:         }
  37:
  38:         /// <summary>返回是否本次新破势；受控中的冲击不续期，也不重置等待。</summary>
  39:         public bool ApplyImpact(double impact)
  40:         {
  41:             if (!Finite(impact) || impact <= 0 || IsStaggered) return false;
  42:             Current = Math.Max(0, Current - impact);
  43:             RegenDelayRemaining = recoveryDelay;
  44:             if (Current > 0) return false;
  45:             StaggerRemaining = staggerDuration;
  46:             return true;
  47:         }
  48:
  49:         /// <summary>暂停由根传入零delta；主动作仅暂停自然恢复，不能冻结硬直。</summary>
  50:         public void Tick(double delta, bool primaryActionActive)
  51:         {
  52:             if (!Finite(delta) || delta <= 0) return;
  53:             if (IsStaggered)
  54:             {
  55:                 var used = Math.Min(delta, StaggerRemaining);
  56:                 StaggerRemaining = Math.Max(0, StaggerRemaining - used);
  57:                 delta -= used;
  58:                 if (IsStaggered) return;
  59:                 Current = Maximum * recoveryFraction;
  60:                 RegenDelayRemaining = recoveryDelay;
  61:             }
  62:             if (primaryActionActive) return;
  63:             var wait = Math.Min(delta, RegenDelayRemaining);
  64:             RegenDelayRemaining -= wait;
  65:             delta -= wait;
  66:             Current = Math.Min(Maximum, Current + delta * Maximum * recoveryRate);
  67:         }
  68:
  69:         public void Reset()
  70:         {
  71:             Current = Maximum;
  72:             StaggerRemaining = 0;
  73:             RegenDelayRemaining = 0;
  74:         }
  75:
  76:         private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
  77:     }
  78: }
```

## Assets/OSSASTRA/Scripts/Combat/Runtime/CombatContracts.cs

SHA256: debcad978dca39b008ae3427b468f88df2f33f8273b0a8619ce8a70e34a3f104；41 行；全文件待审。

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
  11:     /// <summary>世界命中器向同一合法存活目标递交架势冲击，不拥有第二次HP写入。</summary>
  12:     public interface ICombatPostureReceiver
  13:     {
  14:         void ReceiveImpact(HitOutcome outcome, double impact);
  15:     }
  16:     public interface ICombatLifeState { bool IsAlive { get; } }
  17:     public interface IHitTarget { Health Health { get; } void ShowHit(); }
  18:     public interface ICombatTarget : IHitTarget, ICombatLifeState
  19:     {
  20:         bool IsAvailable { get; }
  21:         CombatFaction Faction { get; }
  22:         Vector3 WorldPosition { get; }
  23:         Transform TargetTransform { get; }
  24:         void OnHitResolved(HitOutcome outcome);
  25:     }
  26:     public interface ICombatHitResolver
  27:     {
  28:         string LastResult { get; }
  29:         void Resolve(AttackRelease release, Vector3 releaseDirection);
  30:     }
  31:     /// <summary>接口非null不代表Unity宿主有效；销毁/禁用/无生命立即失效。</summary>
  32:     public static class CombatTargetValidity
  33:     {
  34:         public static bool IsAvailable(IHitTarget target)
  35:         {
  36:             var component=target as MonoBehaviour;
  37:             return component!=null && component.isActiveAndEnabled && target.Health!=null &&
  38:                 target.Health.Current>0 && (!(target is ICombatTarget combat) || combat.IsAvailable);
  39:         }
  40:     }
  41: }
```

## Assets/OSSASTRA/Scripts/Combat/Runtime/CombatMeleeHit.cs

SHA256: 34d36ebd738b1d604ad45afdc51d74851d230e08752fd97aac63807550310082；51 行；全文件待审。

```csharp
   1: /*
   2:  * 脚本：CombatMeleeHit
   3:  * 功能：将一次释放转换为物理候选、身份/阵营资格和唯一伤害批次。
   4:  * 边界：中立能力，无玩家Root依赖，不建动作时钟或HP；空挥也关闭Release。
   5:  * 协作：两类战斗根各自释放；Applied先反馈/死亡，存活者再接可选架势，不重复扣血。
   6:  */
   7: using System;
   8: using System.Linq;
   9: using Ossastra.CharacterCore;
  10: using UnityEngine;
  11: namespace Ossastra.Combat
  12: {
  13:     public sealed class CombatMeleeHit : MonoBehaviour, ICombatHitResolver
  14:     {
  15:         [SerializeField, Min(0)] private float reach=.8f;
  16:         [SerializeField, Min(.01f)] private float radius=.65f;
  17:         [SerializeField] private float height=.8f;
  18:         [SerializeField] private LayerMask targetLayers=~0;
  19:         public string LastResult { get; private set; }="未释放";
  20:         public void Resolve(AttackRelease release, Vector3 releaseDirection)
  21:         {
  22:             if(release==null) throw new ArgumentNullException(nameof(release));
  23:             var source=GetComponent<CombatVitals>();
  24:             if(!isActiveAndEnabled || (source!=null && (!source.IsAvailable || source.Health.Id!=release.ActorId)))
  25:             { release.Resolve(Array.Empty<Health>()); LastResult="来源失效或释放身份不符"; return; }
  26:             var center=transform.position+Vector3.up*height+releaseDirection*reach;
  27:             var candidates=Physics.OverlapSphere(center,radius,targetLayers,QueryTriggerInteraction.Collide)
  28:                 .Select(c=>c.GetComponentsInParent<MonoBehaviour>().OfType<IHitTarget>().FirstOrDefault())
  29:                 .Where(CombatTargetValidity.IsAvailable).Distinct().ToArray();
  30:             // 同一接收者多个Collider合并；不同接收者冒用同ID整组拒绝，不能任取一个扣HP。
  31:             var targets=candidates.GroupBy(t=>t.Health.Id,StringComparer.Ordinal)
  32:                 .Where(g=>g.Count()==1).Select(g=>g.Single())
  33:                 .Where(t=>CombatTargetPolicy.CanHit(release.ActorId,
  34:                     source==null?CombatFaction.Neutral:source.Faction,source==null||source.IsAlive,
  35:                     t.Health.Id,t is ICombatTarget combat?combat.Faction:CombatFaction.Neutral,true)).ToArray();
  36:             var outcomes=release.Resolve(targets.Select(t=>t.Health));
  37:             foreach(var outcome in outcomes)
  38:                 if(outcome.Reason==HitReason.Applied)
  39:                 {
  40:                     var target=targets.First(t=>t.Health.Id==outcome.TargetId);
  41:                     if(target is ICombatTarget combat) combat.OnHitResolved(outcome); else target.ShowHit();
  42:                     // HP/死亡通知已完成；同批存活者才消费架势。不会再做空间查询或伤害结算。
  43:                     if (outcome.HpAfter > 0 && target is MonoBehaviour component && component != null)
  44:                         foreach (var receiver in component.GetComponents<MonoBehaviour>().OfType<ICombatPostureReceiver>())
  45:                             receiver.ReceiveImpact(outcome, release.PostureImpact);
  46:                 }
  47:             LastResult=outcomes.Count==0?"空挥／无合法敌对目标":string.Join("; ",outcomes.Select(o=>
  48:                 o.ActorId+" → "+o.TargetId+" "+o.HpBefore+"→"+o.HpAfter+" "+o.Reason+" #"+o.ActionId.ToString("N")));
  49:         }
  50:     }
  51: }
```

## Assets/OSSASTRA/Scripts/Combat/Runtime/CombatPosture.cs

SHA256: 2b4760b645a6609e768c7588f8507e0dfe532d35eacfab9f6007557a3a27d843；95 行；全文件待审。

```csharp
   1: /*
   2:  * 脚本：CombatPosture
   3:  * 功能：把本体合法HP命中交给唯一PostureState；报告破势与解除。
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
  49:         /// <summary>仅消费同一HP批次存活者的Applied结果；不再次Resolve或寻找目标。</summary>
  50:         public void ReceiveImpact(HitOutcome outcome, double impact)
  51:         {
  52:             // 根关闭后不能再留下无人推进的硬直；只依赖显式注入的生命周期，不反查具体根类型。
  53:             if (!isActiveAndEnabled || driver == null || !driver.isActiveAndEnabled ||
  54:                 state == null || vitals == null || !vitals.IsAvailable ||
  55:                 outcome == null || outcome.Reason != HitReason.Applied || outcome.HpAfter <= 0 ||
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

## Assets/OSSASTRA/Scripts/Development/Combat/CombatDeathDebugView.cs

SHA256: 3a88beede9f7f69d6237732c4b63c51c3843b9725c73294ba688a208d8a33b87；86 行；全文件待审。

```csharp
   1: /*
   2:  * 脚本：CombatDeathDebugView
   3:  * 功能：开发HP/可选架势读数、正式结算入口注入受击和同实体重置，便于复验生命周期。
   4:  * 边界：Development Only，不代表自动返场；不直接写HP私有字段、不伪造UI成功。
   5:  * 协作：注入独立开发来源的Released→Health→目标通知；恢复顺序为取消/恢复原对象/重选实控。
   6:  */
   7: using System.Linq;
   8: using Ossastra.CharacterCore;
   9: using Ossastra.Combat;
  10: using Ossastra.Characters;
  11: using Ossastra.Enemies;
  12: using UnityEngine;
  13: namespace Ossastra.Development
  14: {
  15:     public sealed class CombatDeathDebugView : MonoBehaviour
  16:     {
  17:         [SerializeField] private CharacterControlCoordinator coordinator;
  18:         [SerializeField] private CompanionFollowDirector director;
  19:         [SerializeField] private Ossastra.Enemies.EnemyRoot enemy;
  20:         private Vector3[] spawns;
  21:         private Vector2 postureScroll;
  22:         private static readonly Rect panel=new Rect(490,12,650,370);
  23:         private void Start() { spawns=coordinator.Characters.Select(c=>c.transform.position).ToArray(); Register(true); }
  24:         private void Register(bool add)
  25:         {
  26:             if (coordinator==null) return;
  27:             foreach (var c in coordinator.Characters)
  28:             { var input=c==null?null:c.GetComponent<CharacterInputHandler>(); if (input==null) continue; if (add) input.RegisterPointerRegion(this,panel); else input.RemovePointerRegion(this); }
  29:         }
  30:         private void OnDisable() { Register(false); }
  31:         private void OnEnable() { if (spawns!=null) Register(true); }
  32:
  33:         public static void ApplyDevelopmentHit(CombatVitals target, int damage)
  34:         {
  35:             if (target==null || !target.IsAlive || damage<=0) return;
  36:             var runtime=new BasicActionRuntime("development-hit-probe");
  37:             runtime.Request(new BasicAttackDefinition(0,0,0,.01,damage),"development-only",new ControlAuthority());
  38:             var release=runtime.Advance(.001,false).Single(f=>f.Release!=null).Release;
  39:             foreach (var outcome in release.Resolve(new[]{target.Health})) target.OnHitResolved(outcome);
  40:         }
  41:         public void RestartForDevelopment()
  42:         {
  43:             if (spawns==null) return;
  44:             director.enabled=false;
  45:             for (var i=0;i<coordinator.Characters.Count;i++)
  46:             {
  47:                 var c=coordinator.Characters[i]; c.CancelAction("development-reset"); c.SetPaused(false);
  48:                 c.Vitals.RestoreForDevelopment();
  49:                 var posture=c.GetComponent<CombatPosture>();
  50:                 if(posture!=null) posture.ResetState();
  51:                 c.GetComponent<CharacterMovement>().Teleport(spawns[i]);
  52:             }
  53:             enemy.RestoreForDevelopment(); coordinator.RestoreControlForDevelopment(); director.enabled=true;
  54:         }
  55:         private void OnGUI()
  56:         {
  57:             if (coordinator==null || enemy==null) return;
  58:             GUILayout.BeginArea(panel,GUI.skin.box);
  59:             var showPosture=enemy.GetComponent<CombatPosture>()!=null;
  60:             if(showPosture) postureScroll=GUILayout.BeginScrollView(postureScroll);
  61:             GUILayout.Label("Development Only · 双向攻防 / 真死亡 · 无正式返场");
  62:             GUILayout.Label("Current: "+(coordinator.Current==null?"无可控制角色":coordinator.Current.ActorId));
  63:             foreach (var c in coordinator.Characters)
  64:             {
  65:                 var v=c.Vitals; GUILayout.Label(c.ActorId+" HP "+v.Health.Current+"/"+v.Health.Maximum+" "+(v.IsAlive?"Alive":"Dead")+" "+c.EffectiveIntentKind+" | "+c.LastFact);
  66:                 DrawPosture(c.GetComponent<CombatPosture>(),v.LastResult);
  67:                 if (GUILayout.Button("合法受击检查："+c.ActorId+" -20HP")) ApplyDevelopmentHit(v,20);
  68:                 if (GUILayout.Button("死亡边界："+c.ActorId+" 致死一击")) ApplyDevelopmentHit(v,v.Health.Maximum);
  69:             }
  70:             GUILayout.Label("Enemy HP "+enemy.Vitals.Health.Current+"/"+enemy.Vitals.Health.Maximum+" | "+enemy.LastFact);
  71:             DrawPosture(enemy.GetComponent<CombatPosture>(),enemy.LastHit);
  72:             if (GUILayout.Button("敌人攻击："+(enemy.AttacksEnabled?"开启（点击关闭）":"关闭（点击开启）"))) enemy.AttacksEnabled=!enemy.AttacksEnabled;
  73:             if (GUILayout.Button("Development Only：同实体重新开始测试")) RestartForDevelopment();
  74:             if(showPosture) GUILayout.EndScrollView();
  75:             GUILayout.EndArea();
  76:         }
  77:         // 只读真实能力；旧场没挂架势时仍显示原命中信息，不伪造一份UI状态。
  78:         private static void DrawPosture(CombatPosture posture,string fallback)
  79:         {
  80:             if(posture==null) { GUILayout.Label(fallback); return; }
  81:             GUILayout.Label("架势 "+posture.Current.ToString("F1")+"/"+posture.Maximum+" · "+posture.Phase+
  82:                 " · 硬直 "+posture.StaggerRemaining.ToString("F2")+"s · 等待 "+posture.RegenDelayRemaining.ToString("F2")+"s");
  83:             GUILayout.Label(new GUIContent("来源："+posture.LastImpact,posture.LastImpact));
  84:         }
  85:     }
  86: }
```

## Assets/OSSASTRA/Scripts/Enemies/Runtime/EnemyRoot.cs

SHA256: c991cdb027c11858c5188f7784021202d96d68fb9605f32dfa873896c6c2ec49；110 行；全文件待审。

```csharp
   1: /*
   2:  * 脚本：EnemyRoot
   3:  * 功能：敌方唯一组合根，组织身份、生命、决策与单一动作推进/释放。
   4:  * 边界：无玩家Root/Input/Bus/Current，不选最近目标、不直接扣HP。
   5:  * 协作：Brain提交并验证目标；根独占动作和可选架势推进，破势/生命周期撤销未释放动作。
   6:  */
   7: using System;
   8: using Ossastra.CharacterCore;
   9: using Ossastra.Combat;
  10: using UnityEngine;
  11: namespace Ossastra.Enemies
  12: {
  13:     [RequireComponent(typeof(CombatVitals),typeof(CombatMeleeHit),typeof(StationaryEnemyBrain))]
  14:     public sealed class EnemyRoot : MonoBehaviour
  15:     {
  16:         [SerializeField] private string actorId="enemy-stationary-01";
  17:         [SerializeField] private float releaseAt=.5f, moveReadyAt=.6f, attackReadyAt=.9f, finishAt=1.1f;
  18:         [SerializeField, Min(1)] private int damage=20;
  19:         [SerializeField, Min(0)] private float postureImpact;
  20:         [SerializeField] private CombatPosture posture;
  21:         [SerializeField] private bool attacksEnabled=true;
  22:         private CombatVitals vitals;
  23:         private ICombatHitResolver melee;
  24:         private StationaryEnemyBrain brain;
  25:         private BasicActionRuntime runtime;
  26:         private BasicAttackDefinition attack;
  27:         private readonly ControlAuthority authority=new ControlAuthority();
  28:         private ICombatTarget target;
  29:         private Health targetHealth;
  30:         private bool focused=true, applicationPaused, paused;
  31:         public string ActorId => actorId;
  32:         public CombatVitals Vitals => vitals;
  33:         public string LastFact { get; private set; }="Idle";
  34:         public string LastHit => melee==null?"未初始化":melee.LastResult;
  35:         public bool ActionActive => runtime!=null && runtime.IsActive;
  36:         public bool AttacksEnabled { get => attacksEnabled; set { attacksEnabled=value; if(!value) Cancel("development-attacks-disabled"); } }
  37:         private void Awake()
  38:         {
  39:             vitals=GetComponent<CombatVitals>(); melee=GetComponent<CombatMeleeHit>(); brain=GetComponent<StationaryEnemyBrain>();
  40:             try { attack=new BasicAttackDefinition(releaseAt,moveReadyAt,attackReadyAt,finishAt,damage,postureImpact); runtime=new BasicActionRuntime(actorId); }
  41:             catch(ArgumentException e) { Debug.LogError("Enemy configuration: "+e.Message,this); enabled=false; return; }
  42:             if(!vitals.Initialize(actorId) || brain==null || !brain.Initialize(vitals))
  43:             { Debug.LogError("Enemy identity/brain invalid",this); enabled=false; return; }
  44:             vitals.Died+=OnDied;
  45:             if (posture != null)
  46:             {
  47:                 if (!posture.Initialize(vitals, this)) { Debug.LogError("Enemy posture configuration invalid", this); enabled=false; return; }
  48:                 posture.Broken += OnPostureBroken;
  49:                 posture.Recovered += OnPostureRecovered;
  50:             }
  51:         }
  52:         private void Update()
  53:         {
  54:             if(runtime==null) return;
  55:             if(vitals==null || !vitals.IsAvailable) { Cancel("source-unavailable"); return; }
  56:             // 1. 根独占架势时钟，禁用开发攻击不冻结恢复；失焦/暂停停钟。
  57:             if (posture != null) posture.Tick(!focused || applicationPaused || paused ? 0 : Time.deltaTime, runtime.IsActive);
  58:             if(!attacksEnabled || !focused || applicationPaused || paused || !authority.Allows(ControlChannel.Attack)) return;
  59:             // 2. 决策资格先于动作请求；Brain不掌握架势数值或第二时钟。
  60:             if(brain==null || !brain.isActiveAndEnabled) { Cancel("brain-disabled"); return; }
  61:             if(runtime.IsActive && (!brain.IsEligible(target) || !ReferenceEquals(target.Health,targetHealth)))
  62:                 Cancel("target-dead-out-of-range-or-occluded-or-stale");
  63:             if(!runtime.IsActive && brain.TrySelect(Time.time,out var selected))
  64:             {
  65:                 var request=runtime.Request(attack,"stationary-enemy",authority);
  66:                 if(request.Accepted)
  67:                 {
  68:                     target=selected; targetHealth=selected.Health; brain.OnAccepted(Time.time);
  69:                     LastFact="Accepted #"+request.Fact.ActionId.ToString("N");
  70:                 }
  71:             }
  72:             // 3. 唯一动作推进/命中阶段；破势回调立即Cancel，不保留冻结的待释放动作。
  73:             foreach(var fact in runtime.Advance(Time.deltaTime,false))
  74:             {
  75:                 LastFact=fact.Kind+" #"+fact.ActionId.ToString("N");
  76:                 if(fact.Release!=null)
  77:                 {
  78:                     var direction=target.WorldPosition-transform.position; direction.y=0;
  79:                     melee.Resolve(fact.Release,direction.normalized);
  80:                 }
  81:             }
  82:         }
  83:         public void Cancel(string reason)
  84:         {
  85:             var fact=runtime==null?null:runtime.Cancel(reason);
  86:             if(fact!=null) LastFact="Cancelled "+reason+" #"+fact.ActionId.ToString("N");
  87:             target=null; targetHealth=null;
  88:         }
  89:         public void SetPaused(bool value) { paused=value; if(value) Cancel("enemy-paused"); }
  90:         private void OnPostureBroken(string cause)
  91:         {
  92:             authority.Block(posture, ControlChannel.Move | ControlChannel.Attack);
  93:             Cancel("posture-broken:" + cause);
  94:         }
  95:         private void OnPostureRecovered() { authority.Release(posture); }
  96:         private void OnDied(HitOutcome outcome) { Cancel("death:"+outcome.ActorId); }
  97:         private void OnDisable() { Cancel("enemy-disabled"); if (posture != null) posture.ResetState(); authority.Release(posture); }
  98:         private void OnApplicationFocus(bool value) { focused=value; if(!value) Cancel("focus-lost"); }
  99:         private void OnApplicationPause(bool value) { applicationPaused=value; if(value) Cancel("application-paused"); }
 100:         private void OnDestroy()
 101:         {
 102:             if(vitals!=null) vitals.Died-=OnDied;
 103:             if(posture!=null) { posture.Broken-=OnPostureBroken; posture.Recovered-=OnPostureRecovered; }
 104:             authority.Release(posture);
 105:             Cancel("destroyed");
 106:         }
 107:         public void RestoreForDevelopment()
 108:         { Cancel("development-reset"); vitals.RestoreForDevelopment(); if(posture!=null) posture.ResetState(); brain.ResetDecision(Time.time); }
 109:     }
 110: }
```
