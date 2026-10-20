# ARCH-01 变化源码审查与SHA
仅列本轮12份实际变更/迁移/新增自有源码。全部区间待用户审查，既有历史审查不代签。行号对应文件内容，SHA-256对应实际UTF-8文件字节。

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterContracts.cs

范围 1–75；SHA-256：25B0D31AB6CDAF50738EB662495660A6AE619EE7BAD94520C517BC7B40AFAE1D

```csharp
   1 | /*
   2 |  * 脚本：CharacterContracts
   3 |  * 功能：集中声明角色领域的窄职责契约，供组合根和DataBus依赖。
   4 |  * 边界：只定义能力；实际实现与调用顺序不在本文件。
   5 |  * 协作：Inspector仍保存具体组件；Root运行时按接口连接，只有必要模块接受Bus初始化。
   6 |  */
   7 | using Ossastra.CharacterCore;
   8 | using UnityEngine;
   9 | using System.Collections.Generic;
  10 |
  11 | namespace Ossastra.Characters
  12 | {
  13 |     /// <summary>队伍只分配合法目标；角色能力自行计算移动，不从此契约取得方向或策略状态。</summary>
  14 |     public interface ICharacterFollowTargetSource
  15 |     {
  16 |         bool TryGetFollowTarget(CharacterRoot follower, out CharacterRoot target);
  17 |     }
  18 |     /// <summary>场景唯一控制权的只读视图；代际和共享屏障同时约束玩家与伙伴来源。</summary>
  19 |     public interface ICharacterControlSession
  20 |     {
  21 |         CharacterRoot Current { get; }
  22 |         IReadOnlyList<CharacterRoot> Characters { get; }
  23 |         bool IsIntentSamplingAllowed { get; }
  24 |         int ControlRevision { get; }
  25 |     }
  26 |     /// <summary>伙伴策略只提供移动候选帧；选择器剥离攻击、暂停、取消和开发命令。</summary>
  27 |     public interface ICompanionIntentSource
  28 |     {
  29 |         bool IsAvailable { get; }
  30 |         CharacterInputFrame ReadCompanionIntent(CharacterRoot character, float delta);
  31 |     }
  32 |     /// <summary>玩家源的实时许可；外层已采样帧在许可撤回时同样失效，兼容未挂跟随模块的旧场。</summary>
  33 |     public interface ICharacterInputAvailability
  34 |     {
  35 |         bool HasPlayerInput { get; }
  36 |     }
  37 |     /// <summary>由场景实控权威回答谁可以采样玩家输入；输入源不保存第二份选中状态。</summary>
  38 |     public interface ICharacterPlayerControlSource
  39 |     {
  40 |         bool IsPlayerInputAllowed(ICharacterInputSource source);
  41 |     }
  42 |     /// <summary>仅为确需角色Bus的模块提供显式初始化；失败阻止角色启动。</summary>
  43 |     public interface ICharacterSubsystem { bool Initialize(CharacterDataBus dataBus); }
  44 |     /// <summary>提供单角色唯一有效输入快照，不判定动作是否成功。</summary>
  45 |     public interface ICharacterInputSource
  46 |     {
  47 |         CharacterInputFrame CurrentFrame { get; }
  48 |         bool IsConfigured { get; }
  49 |         CharacterInputFrame Read();
  50 |     }
  51 |     /// <summary>执行实际运动并报告结果；不决定攻击朝向。</summary>
  52 |     public interface ICharacterMovement : ICharacterSubsystem
  53 |     {
  54 |         bool IsMoving { get; }
  55 |         Vector3 Velocity { get; }
  56 |         void Tick(bool allowed, float delta);
  57 |         void Teleport(Vector3 position);
  58 |     }
  59 |     /// <summary>分别维护攻击方向与可见翻转，由Root显式调度。</summary>
  60 |     public interface ICharacterOrientation
  61 |     {
  62 |         Vector3 AttackDirection { get; }
  63 |         float VisualFacingX { get; }
  64 |         void ResolveDirections(Vector2 moveInput, Vector3? pointerDirection, Vector2 stickDirection, bool movementFacing);
  65 |         void OrientTowards(Vector3 worldPosition);
  66 |         void FaceAttackDirection();
  67 |     }
  68 |     /// <summary>从角色状态更新表现与暂停，不反向产生命中。</summary>
  69 |     public interface ICharacterPresentation : ICharacterSubsystem
  70 |     {
  71 |         string CurrentClip { get; }
  72 |         void SetPaused(bool paused);
  73 |         void Present();
  74 |     }
  75 | }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterRoot.cs

范围 1–207；SHA-256：888D2A0B0B1C73BA8EBBC984FE40A6841BE1B702F65AE0D4E6209109D8515473

```csharp
   1 | /*
   2 |  * 脚本：CharacterRoot
   3 |  * 功能：组织单角色的接口接线、输入、动作事实、运动和表现。
   4 |  * 边界：持有本角色运行时；只读生命资格，不实现HP写入、设备绑定或开发GUI。
   5 |  * 协作：初始化可选本体Follow，场景只分配目标；单意图入口与原动作/运动时序保持。
   6 |  */
   7 | using System;
   8 | using Ossastra.CharacterCore;
   9 | using Ossastra.Combat;
  10 | using UnityEngine;
  11 | using UnityEngine.Serialization;
  12 |
  13 | namespace Ossastra.Characters
  14 | {
  15 |     [DefaultExecutionOrder(-100)]
  16 |     [RequireComponent(typeof(CharacterMovement), typeof(CombatMeleeHit), typeof(CharacterInputHandler))]
  17 |     [RequireComponent(typeof(CharacterOrientation))]
  18 |     public sealed class CharacterRoot : MonoBehaviour
  19 |     {
  20 |         [SerializeField] private string actorId = "player";
  21 |         [FormerlySerializedAs("motor"), SerializeField] private CharacterMovement movement;
  22 |         [SerializeField] private CharacterOrientation orientation;
  23 |         [SerializeField] private CharacterSpineView view;
  24 |         [SerializeField] private CombatMeleeHit melee;
  25 |         [SerializeField] private CharacterInputHandler input;
  26 |         [SerializeField] private Camera viewCamera;
  27 |         [SerializeField] private CharacterFollow follow;
  28 |         [SerializeField] private CombatVitals vitals;
  29 |         private ICombatLifeState lifeState;
  30 |         [Header("Prototype timing — not final balance")]
  31 |         [SerializeField] private float releaseAt = .3f, moveReadyAt = .5f, attackReadyAt = .8f, finishAt = 1.1f;
  32 |         [SerializeField, Min(1)] private int damage = 12;
  33 |         [SerializeField] private bool verboseTrace;
  34 |         private readonly ControlAuthority authority = new ControlAuthority();
  35 |         private readonly AttackIntent intent = new AttackIntent();
  36 |         private ICharacterInputSource inputSource;
  37 |         private CharacterIntentSelector intentSelector;
  38 |         private ICharacterMovement mover;
  39 |         private ICharacterOrientation orienter;
  40 |         private ICharacterPresentation presentation;
  41 |         private ICombatHitResolver hitResolver;
  42 |         private BasicActionRuntime runtime;
  43 |         private BasicAttackDefinition definition;
  44 |         private bool ready, focused = true, paused, applicationPaused;
  45 |         private string lastFact = "Idle";
  46 |         public CharacterDataBus DataBus { get; private set; }
  47 |         public string ActorId => actorId;
  48 |         public string LastFact => lastFact;
  49 |         public bool IsPaused => paused || applicationPaused;
  50 |         public bool ActionActive => DataBus != null && DataBus.ActionActive;
  51 |         public bool IsAlive => lifeState==null || lifeState.IsAlive;
  52 |         public bool CanSimulateIntent => ready && IsAlive && isActiveAndEnabled && focused && !IsPaused;
  53 |         public CombatVitals Vitals => vitals;
  54 |         public event Action<CharacterRoot> Died;
  55 |         public CharacterFollow Follow => follow;
  56 |         public CharacterIntentKind EffectiveIntentKind => intentSelector == null ? CharacterIntentKind.Neutral : intentSelector.EffectiveKind;
  57 |
  58 |         public bool CanBindCompanionIntent(ICharacterControlSession session, ICompanionIntentSource source)
  59 |             => ready && follow != null && ReferenceEquals(source,follow) && intentSelector != null && intentSelector.CanBind(session,source);
  60 |         public bool BindCompanionIntent(ICharacterControlSession session, ICompanionIntentSource source)
  61 |             => CanBindCompanionIntent(session,source) && intentSelector.Bind(session,source);
  62 |         public void ClearCompanionIntent(ICompanionIntentSource owner)
  63 |         { if (intentSelector != null) intentSelector.Clear(owner); }
  64 |
  65 |         private void Awake()
  66 |         {
  67 |             if (viewCamera == null) viewCamera = Camera.main;
  68 |             if (vitals!=null)
  69 |             {
  70 |                 if (!vitals.Initialize(actorId)) { Debug.LogError("Character vitals identity/configuration invalid",this); enabled=false; return; }
  71 |                 lifeState=vitals; vitals.Died+=OnDied;
  72 |             }
  73 |             if (movement == null || orientation == null || view == null || melee == null || input == null || viewCamera == null)
  74 |             { Debug.LogError("Character requires registered input/movement/orientation/view/melee/camera", this); enabled = false; return; }
  75 |             // Inspector保留具体组件槽；运行期通过窄接口消费，不反向依赖开发GUI或测试靶。
  76 |             // 每角色只有这一条有效Frame入口；没有Director绑定时保持原玩家适配器行为。
  77 |             intentSelector = new CharacterIntentSelector(this,input);
  78 |             inputSource = intentSelector;
  79 |             mover = movement;
  80 |             orienter = orientation;
  81 |             presentation = view;
  82 |             hitResolver = melee;
  83 |             if (!inputSource.IsConfigured) { Debug.LogError("Character actions are not configured", this); enabled = false; return; }
  84 |             try
  85 |             {
  86 |                 definition = new BasicAttackDefinition(releaseAt, moveReadyAt, attackReadyAt, finishAt, damage);
  87 |                 runtime = new BasicActionRuntime(actorId);
  88 |             }
  89 |             catch (ArgumentException error) { Debug.LogError("Character configuration: " + error.Message, this); enabled = false; return; }
  90 |             // 每角色独立Bus只读取原所有者；只有确需Bus的运动和表现执行初始化。
  91 |             DataBus = new CharacterDataBus(actorId, inputSource, mover, orienter, runtime, authority);
  92 |             ready = InitializeSubsystems(DataBus, mover, presentation);
  93 |             if (!ready) { Debug.LogError("Character subsystem initialization failed", this); enabled = false; }
  94 |             // 可选能力只接受本角色初始化；无组件/无assignment的旧场保持普通玩家行为。
  95 |             if (follow != null && !follow.Initialize(this)) Debug.LogWarning("Character follow unavailable: configuration or owner invalid", this);
  96 |         }
  97 |
  98 |         private static bool InitializeSubsystems(CharacterDataBus dataBus, params ICharacterSubsystem[] subsystems)
  99 |         {
 100 |             foreach (var subsystem in subsystems) if (subsystem == null || !subsystem.Initialize(dataBus)) return false;
 101 |             return true;
 102 |         }
 103 |
 104 |         private void Update()
 105 |         {
 106 |             if (!ready || !IsAlive) return;
 107 |             // 选择器只采一个源；伙伴只能提供移动，切人屏障为空帧，动作时钟仍继续。
 108 |             inputSource.Read();
 109 |             var frame = DataBus.Input;
 110 |             if (frame.pause) SetPaused(!paused);
 111 |             if (frame.cancel) CancelAction("player-cancel");
 112 |             var canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
 113 |             if (focused && !IsPaused)
 114 |                 orienter.ResolveDirections(frame.move, PointerDirection(frame), frame.hasDirectionalAim ? frame.aimDirection : Vector2.zero, canMove);
 115 |             // 输入边沿不等于动作成功：焦点、UI和Runtime的暂停/权限/Ready资格分别把关。
 116 |             if (intent.Observe(frame.attackHeld) && focused && !frame.attackPointerBlocked)
 117 |             {
 118 |                 var request = runtime.Request(definition, "player-input", authority, IsPaused);
 119 |                 if (request.PreviousTerminal != null) Observe(request.PreviousTerminal);
 120 |                 if (request.Accepted) { Observe(request.Fact); orienter.FaceAttackDirection(); }
 121 |                 else lastFact = "Rejected: " + request.Reason;
 122 |             }
 123 |             // Accepted只建立动作身份；只有Released带一次性命中凭据。切人不会重发事实。
 124 |             foreach (var fact in runtime.Advance(Time.deltaTime, IsPaused || !focused))
 125 |             {
 126 |                 Observe(fact);
 127 |                 if (fact.Release != null)
 128 |                 {
 129 |                     // 在释放当帧按值采样方向；不是接受动作时锁定，前摇中仍可重新瞄准。
 130 |                     var releaseDirection = orienter.AttackDirection;
 131 |                     orienter.FaceAttackDirection();
 132 |                     hitResolver.Resolve(fact.Release, releaseDirection);
 133 |                 }
 134 |             }
 135 |             // 本帧可能跨过MoveReady；推进事实后重新查询许可，Run依据真实位移。
 136 |             canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
 137 |             mover.Tick(canMove, IsPaused || !focused ? 0 : Time.deltaTime);
 138 |             presentation.SetPaused(IsPaused || !focused);
 139 |             presentation.Present();
 140 |         }
 141 |
 142 |         private Vector3? PointerDirection(CharacterInputFrame frame)
 143 |         {
 144 |             if (!frame.hasPointer) return null;
 145 |             var plane = new Plane(Vector3.up, transform.position);
 146 |             var ray = viewCamera.ScreenPointToRay(frame.pointer);
 147 |             if (!plane.Raycast(ray, out var distance)) return null;
 148 |             var direction = ray.GetPoint(distance) - transform.position;
 149 |             direction.y = 0;
 150 |             return direction.sqrMagnitude < .0001f ? (Vector3?)null : direction;
 151 |         }
 152 |
 153 |         private void Observe(ActionFact fact)
 154 |         {
 155 |             lastFact = fact.Kind + " @" + fact.At.ToString("F3") + " " + fact.ActionId.ToString("N").Substring(0, 8);
 156 |             if (fact.Reason != null) lastFact += " " + fact.Reason;
 157 |             if (verboseTrace) Debug.Log("Character " + lastFact, this);
 158 |         }
 159 |
 160 |         public void CancelAction(string reason)
 161 |         {
 162 |             // 取消必须具名；抑制仍按住的输入，不能把下一帧误判为新请求。
 163 |             intent.SuppressUntilRelease();
 164 |             var terminal = runtime == null ? null : runtime.Cancel(reason);
 165 |             if (terminal != null) Observe(terminal);
 166 |         }
 167 |
 168 |         private void OnDied(HitOutcome outcome)
 169 |         {
 170 |             // HP已由结算归零；这里仅终止本体未释放动作/有效帧，再交唯一会话处理实控。
 171 |             CancelAction("death:"+outcome.ActorId+":"+outcome.ActionId.ToString("N"));
 172 |             if (follow!=null) follow.ResetIntent();
 173 |             if (intentSelector!=null) intentSelector.Clear();
 174 |             Died?.Invoke(this);
 175 |         }
 176 |
 177 |         public void SetPaused(bool value) { paused = value; intent.SuppressUntilRelease(); if (follow != null) follow.ResetIntent(); }
 178 |         public void SetAttackRestriction(object owner, bool restricted, string reason = "attack-restricted")
 179 |         {
 180 |             if (restricted) { authority.Block(owner, ControlChannel.Attack); CancelAction(reason); }
 181 |             else authority.Release(owner);
 182 |         }
 183 |
 184 |         private void OnApplicationFocus(bool hasFocus)
 185 |         { focused = hasFocus; if (follow != null) follow.ResetIntent(); if (!hasFocus) CancelAction("focus-lost"); else intent.SuppressUntilRelease(); }
 186 |         private void OnApplicationPause(bool value)
 187 |         { applicationPaused = value; if (follow != null) follow.ResetIntent(); if (value) intent.SuppressUntilRelease(); }
 188 |         private void OnEnable() { intent.SuppressUntilRelease(); if (intentSelector != null) intentSelector.Clear(); }
 189 |         private void OnDisable() { CancelAction("disabled"); if (follow != null) follow.ResetIntent(); if (intentSelector != null) intentSelector.Clear(); if (ready && presentation != null) presentation.SetPaused(false); }
 190 |         private void OnDestroy()
 191 |         {
 192 |             if (vitals!=null) vitals.Died-=OnDied;
 193 |             // 终止本角色动作后解除本对象引用；不清其它角色或场景控制权威。
 194 |             CancelAction("destroyed");
 195 |             ready = false;
 196 |             DataBus = null;
 197 |             runtime = null;
 198 |             inputSource = null;
 199 |             if (intentSelector != null) intentSelector.Clear();
 200 |             intentSelector = null;
 201 |             mover = null;
 202 |             orienter = null;
 203 |             presentation = null;
 204 |             hitResolver = null;
 205 |         }
 206 |     }
 207 | }
```

## Assets/OSSASTRA/Scripts/Combat/Runtime/CombatContracts.cs

范围 1–36；SHA-256：D61747C8721DFB9CDA4976CDB8E6099D52A2FFF5D37D129C7F3072A1FD10C0D7

```csharp
   1 | /*
   2 |  * 脚本：CombatContracts
   3 |  * 功能：中立命中、生命及世界目标契约；玩家输入和敌方决策共用。
   4 |  * 边界：Health为唯一HP权威；接口通知不重复扣血，不保存玩家Current。
   5 |  * 协作：玩家根/敌人根/物理解析/感知真实消费；旧开发靶只需IHitTarget。
   6 |  */
   7 | using Ossastra.CharacterCore;
   8 | using UnityEngine;
   9 | namespace Ossastra.Combat
  10 | {
  11 |     public interface ICombatLifeState { bool IsAlive { get; } }
  12 |     public interface IHitTarget { Health Health { get; } void ShowHit(); }
  13 |     public interface ICombatTarget : IHitTarget, ICombatLifeState
  14 |     {
  15 |         bool IsAvailable { get; }
  16 |         CombatFaction Faction { get; }
  17 |         Vector3 WorldPosition { get; }
  18 |         Transform TargetTransform { get; }
  19 |         void OnHitResolved(HitOutcome outcome);
  20 |     }
  21 |     public interface ICombatHitResolver
  22 |     {
  23 |         string LastResult { get; }
  24 |         void Resolve(AttackRelease release, Vector3 releaseDirection);
  25 |     }
  26 |     /// <summary>接口非null不代表Unity宿主有效；销毁/禁用/无生命立即失效。</summary>
  27 |     public static class CombatTargetValidity
  28 |     {
  29 |         public static bool IsAvailable(IHitTarget target)
  30 |         {
  31 |             var component=target as MonoBehaviour;
  32 |             return component!=null && component.isActiveAndEnabled && target.Health!=null &&
  33 |                 target.Health.Current>0 && (!(target is ICombatTarget combat) || combat.IsAvailable);
  34 |         }
  35 |     }
  36 | }
```

## Assets/OSSASTRA/Scripts/Combat/Runtime/CombatMeleeHit.cs

范围 1–47；SHA-256：4A121FDBAF48CACF3ABCBA5F6E4320316DEA7C17C7F2E715682AB0AA95F8CD33

```csharp
   1 | /*
   2 |  * 脚本：CombatMeleeHit
   3 |  * 功能：将一次释放转换为物理候选、身份/阵营资格和唯一伤害批次。
   4 |  * 边界：中立能力，无玩家Root依赖，不建动作时钟或HP；空挥也关闭Release。
   5 |  * 协作：两类战斗根各自释放，Applied交回目标只做反馈/死亡，不重复扣血。
   6 |  */
   7 | using System;
   8 | using System.Linq;
   9 | using Ossastra.CharacterCore;
  10 | using UnityEngine;
  11 | namespace Ossastra.Combat
  12 | {
  13 |     public sealed class CombatMeleeHit : MonoBehaviour, ICombatHitResolver
  14 |     {
  15 |         [SerializeField, Min(0)] private float reach=.8f;
  16 |         [SerializeField, Min(.01f)] private float radius=.65f;
  17 |         [SerializeField] private float height=.8f;
  18 |         [SerializeField] private LayerMask targetLayers=~0;
  19 |         public string LastResult { get; private set; }="未释放";
  20 |         public void Resolve(AttackRelease release, Vector3 releaseDirection)
  21 |         {
  22 |             if(release==null) throw new ArgumentNullException(nameof(release));
  23 |             var source=GetComponent<CombatVitals>();
  24 |             if(!isActiveAndEnabled || (source!=null && (!source.IsAvailable || source.Health.Id!=release.ActorId)))
  25 |             { release.Resolve(Array.Empty<Health>()); LastResult="来源失效或释放身份不符"; return; }
  26 |             var center=transform.position+Vector3.up*height+releaseDirection*reach;
  27 |             var candidates=Physics.OverlapSphere(center,radius,targetLayers,QueryTriggerInteraction.Collide)
  28 |                 .Select(c=>c.GetComponentsInParent<MonoBehaviour>().OfType<IHitTarget>().FirstOrDefault())
  29 |                 .Where(CombatTargetValidity.IsAvailable).Distinct().ToArray();
  30 |             // 同一接收者多个Collider合并；不同接收者冒用同ID整组拒绝，不能任取一个扣HP。
  31 |             var targets=candidates.GroupBy(t=>t.Health.Id,StringComparer.Ordinal)
  32 |                 .Where(g=>g.Count()==1).Select(g=>g.Single())
  33 |                 .Where(t=>CombatTargetPolicy.CanHit(release.ActorId,
  34 |                     source==null?CombatFaction.Neutral:source.Faction,source==null||source.IsAlive,
  35 |                     t.Health.Id,t is ICombatTarget combat?combat.Faction:CombatFaction.Neutral,true)).ToArray();
  36 |             var outcomes=release.Resolve(targets.Select(t=>t.Health));
  37 |             foreach(var outcome in outcomes)
  38 |                 if(outcome.Reason==HitReason.Applied)
  39 |                 {
  40 |                     var target=targets.First(t=>t.Health.Id==outcome.TargetId);
  41 |                     if(target is ICombatTarget combat) combat.OnHitResolved(outcome); else target.ShowHit();
  42 |                 }
  43 |             LastResult=outcomes.Count==0?"空挥／无合法敌对目标":string.Join("; ",outcomes.Select(o=>
  44 |                 o.ActorId+" → "+o.TargetId+" "+o.HpBefore+"→"+o.HpAfter+" "+o.Reason+" #"+o.ActionId.ToString("N")));
  45 |         }
  46 |     }
  47 | }
```

## Assets/OSSASTRA/Scripts/Combat/Runtime/CombatVitals.cs

范围 1–92；SHA-256：70A040A8FC7CB9836A2BB8DADEE55C18F2574B6C3BA24A861D11CCCAC6D23ABB

```csharp
   1 | /*
   2 |  * 脚本：CombatVitals
   3 |  * 功能：持有本体唯一Health/阵营，接收正式结算反馈并单次通知死亡、关闭自身表现和碰撞。
   4 |  * 边界：IsAlive只读Health；不另存isDead，不选Current、不计算攻击、不自动复活。
   5 |  * 协作：Root/敌人显式Initialize；Melee回调只反馈已Applied结果，Development显式恢复同一Health。
   6 |  */
   7 | using System;
   8 | using Ossastra.CharacterCore;
   9 | using Ossastra.Combat;
  10 | using UnityEngine;
  11 |
  12 | namespace Ossastra.Combat
  13 | {
  14 |     [DisallowMultipleComponent]
  15 |     public sealed class CombatVitals : MonoBehaviour, ICombatTarget
  16 |     {
  17 |         [SerializeField, Min(1)] private int maximumHp=100;
  18 |         [SerializeField] private CombatFaction faction=CombatFaction.Allied;
  19 |         private Renderer[] renderers;
  20 |         private Collider[] colliders;
  21 |         private bool[] rendererEnabled, colliderEnabled;
  22 |         private MaterialPropertyBlock[] originalBlocks;
  23 |         private MaterialPropertyBlock flashBlock;
  24 |         private bool deathNotified, flashing;
  25 |         private float flashUntil;
  26 |         public Health Health { get; private set; }
  27 |         public bool IsAlive => Health != null && Health.Current>0;
  28 |         public bool IsAvailable => isActiveAndEnabled && IsAlive;
  29 |         public Vector3 WorldPosition => transform.position;
  30 |         public Transform TargetTransform => transform;
  31 |         public CombatFaction Faction => faction;
  32 |         public int DeathCount { get; private set; }
  33 |         public string LastResult { get; private set; }="尚未受击";
  34 |         public event Action<HitOutcome> Died;
  35 |
  36 |         public bool Initialize(string actorId)
  37 |         {
  38 |             if (Health != null) return Health.Id==actorId;
  39 |             if (string.IsNullOrWhiteSpace(actorId) || maximumHp<=0) return false;
  40 |             Health=new Health(actorId,maximumHp);
  41 |             // Unity原生对象必须在显式主线程初始化中创建，不能依赖MonoBehaviour字段构造时机。
  42 |             flashBlock=new MaterialPropertyBlock();
  43 |             renderers=GetComponentsInChildren<Renderer>(true);
  44 |             colliders=GetComponentsInChildren<Collider>(true);
  45 |             rendererEnabled=new bool[renderers.Length]; colliderEnabled=new bool[colliders.Length];
  46 |             originalBlocks=new MaterialPropertyBlock[renderers.Length];
  47 |             for (var i=0;i<renderers.Length;i++)
  48 |             { rendererEnabled[i]=renderers[i].enabled; originalBlocks[i]=new MaterialPropertyBlock(); renderers[i].GetPropertyBlock(originalBlocks[i]); }
  49 |             for (var i=0;i<colliders.Length;i++) colliderEnabled[i]=colliders[i].enabled;
  50 |             return true;
  51 |         }
  52 |
  53 |         public void OnHitResolved(HitOutcome outcome)
  54 |         {
  55 |             if (Health==null || outcome==null || outcome.TargetId!=Health.Id || outcome.Reason!=HitReason.Applied) return;
  56 |             LastResult=outcome.ActorId+" → "+outcome.TargetId+" "+outcome.HpBefore+"→"+outcome.HpAfter+" #"+outcome.ActionId.ToString("N");
  57 |             if (IsAlive) { ShowHit(); return; }
  58 |             if (deathNotified) return;
  59 |             deathNotified=true; DeathCount++; flashing=false;
  60 |             // 只退本体表现/碰撞，保留Root/身份/Health供会话与调试读取；死亡不靠禁用全组Root。
  61 |             foreach (var r in renderers) if (r!=null) r.enabled=false;
  62 |             foreach (var c in colliders) if (c!=null) c.enabled=false;
  63 |             Died?.Invoke(outcome);
  64 |         }
  65 |
  66 |         public void ShowHit()
  67 |         {
  68 |             if (!IsAlive || renderers==null) return;
  69 |             flashUntil=Time.time+.15f; flashing=true;
  70 |             flashBlock.SetColor("_Color",Color.white); flashBlock.SetColor("_BaseColor",Color.white);
  71 |             foreach (var r in renderers) if (r!=null) r.SetPropertyBlock(flashBlock);
  72 |         }
  73 |
  74 |         private void Update()
  75 |         {
  76 |             if (!flashing || Time.time<flashUntil) return;
  77 |             flashing=false; RestoreBlocks();
  78 |         }
  79 |         private void RestoreBlocks()
  80 |         { for (var i=0;i<renderers.Length;i++) if (renderers[i]!=null) renderers[i].SetPropertyBlock(originalBlocks[i]); }
  81 |
  82 |         /// <summary>开发回放恢复同一身份/Health，仅还原本组件关掉的Renderer/Collider；无生产计时返场。</summary>
  83 |         public void RestoreForDevelopment()
  84 |         {
  85 |             if (Health==null) return;
  86 |             Health.RestoreForDevelopment(); deathNotified=false; flashing=false; LastResult="Development重置";
  87 |             for (var i=0;i<renderers.Length;i++) if (renderers[i]!=null) renderers[i].enabled=rendererEnabled[i];
  88 |             for (var i=0;i<colliders.Length;i++) if (colliders[i]!=null) colliders[i].enabled=colliderEnabled[i];
  89 |             RestoreBlocks();
  90 |         }
  91 |     }
  92 | }
```

## Assets/OSSASTRA/Scripts/Development/Combat/CombatDeathDebugView.cs

范围 1–71；SHA-256：2F154B7510C008C7C9E55D34375F5D38575CC15158A3BF3ED648C2561DBBF4CB

```csharp
   1 | /*
   2 |  * 脚本：CombatDeathDebugView
   3 |  * 功能：开发战斗读数、正式结算入口注入受击和显式同实体重置，便于复验死亡边界。
   4 |  * 边界：Development Only，不代表自动返场；不直接写HP私有字段、不伪造UI成功。
   5 |  * 协作：注入独立开发来源的Released→Health→目标通知；恢复顺序为取消/恢复原对象/重选实控。
   6 |  */
   7 | using System.Linq;
   8 | using Ossastra.CharacterCore;
   9 | using Ossastra.Combat;
  10 | using Ossastra.Characters;
  11 | using Ossastra.Enemies;
  12 | using UnityEngine;
  13 | namespace Ossastra.Development
  14 | {
  15 |     public sealed class CombatDeathDebugView : MonoBehaviour
  16 |     {
  17 |         [SerializeField] private CharacterControlCoordinator coordinator;
  18 |         [SerializeField] private CompanionFollowDirector director;
  19 |         [SerializeField] private Ossastra.Enemies.EnemyRoot enemy;
  20 |         private Vector3[] spawns;
  21 |         private static readonly Rect panel=new Rect(490,12,650,370);
  22 |         private void Start() { spawns=coordinator.Characters.Select(c=>c.transform.position).ToArray(); Register(true); }
  23 |         private void Register(bool add)
  24 |         {
  25 |             if (coordinator==null) return;
  26 |             foreach (var c in coordinator.Characters)
  27 |             { var input=c==null?null:c.GetComponent<CharacterInputHandler>(); if (input==null) continue; if (add) input.RegisterPointerRegion(this,panel); else input.RemovePointerRegion(this); }
  28 |         }
  29 |         private void OnDisable() { Register(false); }
  30 |         private void OnEnable() { if (spawns!=null) Register(true); }
  31 |
  32 |         public static void ApplyDevelopmentHit(CombatVitals target, int damage)
  33 |         {
  34 |             if (target==null || !target.IsAlive || damage<=0) return;
  35 |             var runtime=new BasicActionRuntime("development-hit-probe");
  36 |             runtime.Request(new BasicAttackDefinition(0,0,0,.01,damage),"development-only",new ControlAuthority());
  37 |             var release=runtime.Advance(.001,false).Single(f=>f.Release!=null).Release;
  38 |             foreach (var outcome in release.Resolve(new[]{target.Health})) target.OnHitResolved(outcome);
  39 |         }
  40 |         public void RestartForDevelopment()
  41 |         {
  42 |             if (spawns==null) return;
  43 |             director.enabled=false;
  44 |             for (var i=0;i<coordinator.Characters.Count;i++)
  45 |             {
  46 |                 var c=coordinator.Characters[i]; c.CancelAction("development-reset"); c.SetPaused(false);
  47 |                 c.Vitals.RestoreForDevelopment(); c.GetComponent<CharacterMovement>().Teleport(spawns[i]);
  48 |             }
  49 |             enemy.RestoreForDevelopment(); coordinator.RestoreControlForDevelopment(); director.enabled=true;
  50 |         }
  51 |         private void OnGUI()
  52 |         {
  53 |             if (coordinator==null || enemy==null) return;
  54 |             GUILayout.BeginArea(panel,GUI.skin.box);
  55 |             GUILayout.Label("Development Only · 双向攻防 / 真死亡 · 无正式返场");
  56 |             GUILayout.Label("Current: "+(coordinator.Current==null?"无可控制角色":coordinator.Current.ActorId));
  57 |             foreach (var c in coordinator.Characters)
  58 |             {
  59 |                 var v=c.Vitals; GUILayout.Label(c.ActorId+" HP "+v.Health.Current+"/"+v.Health.Maximum+" "+(v.IsAlive?"Alive":"Dead")+" "+c.EffectiveIntentKind+" | "+c.LastFact);
  60 |                 GUILayout.Label(v.LastResult);
  61 |                 if (GUILayout.Button("合法受击检查："+c.ActorId+" -20HP")) ApplyDevelopmentHit(v,20);
  62 |                 if (GUILayout.Button("死亡边界："+c.ActorId+" 致死一击")) ApplyDevelopmentHit(v,v.Health.Maximum);
  63 |             }
  64 |             GUILayout.Label("Enemy HP "+enemy.Vitals.Health.Current+"/"+enemy.Vitals.Health.Maximum+" | "+enemy.LastFact);
  65 |             GUILayout.Label(enemy.LastHit);
  66 |             if (GUILayout.Button("敌人攻击："+(enemy.AttacksEnabled?"开启（点击关闭）":"关闭（点击开启）"))) enemy.AttacksEnabled=!enemy.AttacksEnabled;
  67 |             if (GUILayout.Button("Development Only：同实体重新开始测试")) RestartForDevelopment();
  68 |             GUILayout.EndArea();
  69 |         }
  70 |     }
  71 | }
```

## Assets/OSSASTRA/Scripts/Development/Combat/CombatTestTarget.cs

范围 1–65；SHA-256：9192B20790AE2C079F2738249EDF8931DF16914022AAAE9AF6AE4B7657F0A44A

```csharp
   1 | /*
   2 |  * 脚本：CombatTestTarget
   3 |  * 功能：提供开发验证靶、独立Health和短暂命中闪白／数值显示。
   4 |  * 边界：仅验证辅助，不代表正式敌人、受击或死亡系统已经迁移。
   5 |  * 协作：通过IHitTarget被命中模块发现；Health保持唯一写入。
   6 |  */
   7 | using Ossastra.Characters;
   8 | using Ossastra.CharacterCore;
   9 | using Ossastra.Combat;
  10 | using UnityEngine;
  11 |
  12 | namespace Ossastra.Development
  13 | {
  14 |     public sealed class CombatTestTarget : MonoBehaviour, IHitTarget
  15 |     {
  16 |         [SerializeField] private string targetId = "target-a";
  17 |         [SerializeField, Min(1)] private int maximumHp = 100;
  18 |         [SerializeField, Min(.01f)] private float flashSeconds = .15f;
  19 |         private Renderer[] renderers;
  20 |         private MaterialPropertyBlock block;
  21 |         private float flashUntil;
  22 |         private bool flashing;
  23 |         public Health Health { get; private set; }
  24 |
  25 |         private void Awake()
  26 |         {
  27 |             Health = new Health(targetId, maximumHp);
  28 |             renderers = GetComponentsInChildren<Renderer>();
  29 |             block = new MaterialPropertyBlock();
  30 |         }
  31 |
  32 |         public void ShowHit() { flashing = true; flashUntil = Time.time + flashSeconds; Paint(Color.white); }
  33 |         public void RestoreHealth()
  34 |         {
  35 |             Health = new Health(targetId, maximumHp);
  36 |             flashing = false;
  37 |             Paint(new Color(.15f, .7f, .7f));
  38 |         }
  39 |
  40 |         private void Update()
  41 |         {
  42 |             if (!flashing || Time.time < flashUntil) return;
  43 |             flashing = false;
  44 |             Paint(Health.Current == 0 ? Color.gray : new Color(.15f, .7f, .7f));
  45 |         }
  46 |
  47 |         private void Paint(Color color)
  48 |         {
  49 |             if (block == null) return;
  50 |             block.SetColor("_BaseColor", color);
  51 |             block.SetColor("_Color", color);
  52 |             foreach (var r in renderers) if (r != null) r.SetPropertyBlock(block);
  53 |         }
  54 |
  55 |         private void OnGUI()
  56 |         {
  57 |             var camera = Camera.main;
  58 |             if (camera == null || Health == null) return;
  59 |             var point = camera.WorldToScreenPoint(transform.position + Vector3.up * 1.8f);
  60 |             if (point.z <= 0) return;
  61 |             GUI.Box(new Rect(point.x - 70, Screen.height - point.y, 140, 26),
  62 |                 targetId + "  HP " + Health.Current + "/" + Health.Maximum);
  63 |         }
  64 |     }
  65 | }
```

## Assets/OSSASTRA/Scripts/Development/Combat/EntityRootsDebugView.cs

范围 1–60；SHA-256：8B681F6E7C5610BC6A6AAED3747F00F1FECC97FB9897ABD44E2765E578F3A275

```csharp
   1 | /*
   2 |  * 脚本：EntityRootsDebugView
   3 |  * 功能：三根验证场的真实NPC交互消费者，展示成功回执和明确拒绝原因。
   4 |  * 边界：仅Development，不修改NPC计数，不构造正式对话/商店，不增加玩家输入映射。
   5 |  * 协作：当前玩家只作为请求来源；NpcRoot公开入口完成资格判断，事件反馈可观察。
   6 |  */
   7 | using Ossastra.Characters;
   8 | using Ossastra.Npcs;
   9 | using UnityEngine;
  10 | namespace Ossastra.Development
  11 | {
  12 |     public sealed class EntityRootsDebugView : MonoBehaviour
  13 |     {
  14 |         [SerializeField] private CharacterControlCoordinator coordinator;
  15 |         [SerializeField] private NpcRoot npc;
  16 |         private readonly Rect panel=new Rect(490,390,650,120);
  17 |         private string lastReply="靠近独立圆柱NPC后点击交互；范围2单位";
  18 |         public int ObservedReplies { get; private set; }
  19 |         private void OnEnable()
  20 |         {
  21 |             if(npc!=null) npc.Replied+=OnReplied;
  22 |             Register(true);
  23 |         }
  24 |         private void Start() { Register(true); }
  25 |         private void OnDisable()
  26 |         {
  27 |             if(npc!=null) npc.Replied-=OnReplied;
  28 |             Register(false);
  29 |         }
  30 |         private void Register(bool add)
  31 |         {
  32 |             if(coordinator==null) return;
  33 |             foreach(var character in coordinator.Characters)
  34 |             {
  35 |                 if(character==null) continue;
  36 |                 var input=character.GetComponent<CharacterInputHandler>();
  37 |                 if(input==null) continue;
  38 |                 if(add) input.RegisterPointerRegion(this,panel); else input.RemovePointerRegion(this);
  39 |             }
  40 |         }
  41 |         private void OnReplied(NpcReply reply) { ObservedReplies++; lastReply=reply.npcId+"：NPC已回应 #"+reply.count; }
  42 |         public NpcReply RequestInteraction()
  43 |         {
  44 |             var current=coordinator==null?null:coordinator.Current;
  45 |             if(npc==null) return new NpcReply(false,"","","npc-missing",0);
  46 |             var reply=npc.TryInteract(current==null?"":current.ActorId,current==null?Vector3.zero:current.transform.position,
  47 |                 current!=null && current.IsAlive && current.isActiveAndEnabled);
  48 |             if(!reply.accepted) lastReply="交互拒绝："+reply.reason;
  49 |             return reply;
  50 |         }
  51 |         private void OnGUI()
  52 |         {
  53 |             GUILayout.BeginArea(panel,GUI.skin.box);
  54 |             GUILayout.Label("Development Only · 独立NPC交互 / 不参与战斗");
  55 |             if(GUILayout.Button("向NPC发起交互请求")) RequestInteraction();
  56 |             GUILayout.Label(lastReply+" · 已观察事件 "+ObservedReplies);
  57 |             GUILayout.EndArea();
  58 |         }
  59 |     }
  60 | }
```

## Assets/OSSASTRA/Scripts/Enemies/Runtime/EnemyRoot.cs

范围 1–87；SHA-256：225753F817C24B31C2786B85E28F745D0713E2712A48287B6DE0E6B5C23977A7

```csharp
   1 | /*
   2 |  * 脚本：EnemyRoot
   3 |  * 功能：敌方唯一组合根，组织身份、生命、决策与单一动作推进/释放。
   4 |  * 边界：无玩家Root/Input/Bus/Current，不选最近目标、不直接扣HP。
   5 |  * 协作：Brain提交并验证目标；根独占BasicActionRuntime，生命周期撤销未释放动作。
   6 |  */
   7 | using System;
   8 | using Ossastra.CharacterCore;
   9 | using Ossastra.Combat;
  10 | using UnityEngine;
  11 | namespace Ossastra.Enemies
  12 | {
  13 |     [RequireComponent(typeof(CombatVitals),typeof(CombatMeleeHit),typeof(StationaryEnemyBrain))]
  14 |     public sealed class EnemyRoot : MonoBehaviour
  15 |     {
  16 |         [SerializeField] private string actorId="enemy-stationary-01";
  17 |         [SerializeField] private float releaseAt=.5f, moveReadyAt=.6f, attackReadyAt=.9f, finishAt=1.1f;
  18 |         [SerializeField, Min(1)] private int damage=20;
  19 |         [SerializeField] private bool attacksEnabled=true;
  20 |         private CombatVitals vitals;
  21 |         private ICombatHitResolver melee;
  22 |         private StationaryEnemyBrain brain;
  23 |         private BasicActionRuntime runtime;
  24 |         private BasicAttackDefinition attack;
  25 |         private readonly ControlAuthority authority=new ControlAuthority();
  26 |         private ICombatTarget target;
  27 |         private Health targetHealth;
  28 |         private bool focused=true, applicationPaused, paused;
  29 |         public string ActorId => actorId;
  30 |         public CombatVitals Vitals => vitals;
  31 |         public string LastFact { get; private set; }="Idle";
  32 |         public string LastHit => melee==null?"未初始化":melee.LastResult;
  33 |         public bool ActionActive => runtime!=null && runtime.IsActive;
  34 |         public bool AttacksEnabled { get => attacksEnabled; set { attacksEnabled=value; if(!value) Cancel("development-attacks-disabled"); } }
  35 |         private void Awake()
  36 |         {
  37 |             vitals=GetComponent<CombatVitals>(); melee=GetComponent<CombatMeleeHit>(); brain=GetComponent<StationaryEnemyBrain>();
  38 |             try { attack=new BasicAttackDefinition(releaseAt,moveReadyAt,attackReadyAt,finishAt,damage); runtime=new BasicActionRuntime(actorId); }
  39 |             catch(ArgumentException e) { Debug.LogError("Enemy configuration: "+e.Message,this); enabled=false; return; }
  40 |             if(!vitals.Initialize(actorId) || brain==null || !brain.Initialize(vitals))
  41 |             { Debug.LogError("Enemy identity/brain invalid",this); enabled=false; return; }
  42 |             vitals.Died+=OnDied;
  43 |         }
  44 |         private void Update()
  45 |         {
  46 |             if(runtime==null) return;
  47 |             if(vitals==null || !vitals.IsAvailable) { Cancel("source-unavailable"); return; }
  48 |             if(!attacksEnabled || !focused || applicationPaused || paused) return;
  49 |             if(brain==null || !brain.isActiveAndEnabled) { Cancel("brain-disabled"); return; }
  50 |             if(runtime.IsActive && (!brain.IsEligible(target) || !ReferenceEquals(target.Health,targetHealth)))
  51 |                 Cancel("target-dead-out-of-range-or-occluded-or-stale");
  52 |             if(!runtime.IsActive && brain.TrySelect(Time.time,out var selected))
  53 |             {
  54 |                 var request=runtime.Request(attack,"stationary-enemy",authority);
  55 |                 if(request.Accepted)
  56 |                 {
  57 |                     target=selected; targetHealth=selected.Health; brain.OnAccepted(Time.time);
  58 |                     LastFact="Accepted #"+request.Fact.ActionId.ToString("N");
  59 |                 }
  60 |             }
  61 |             // 唯一推进者；Brain没有Update/Advance，不生成第二套释放事实。
  62 |             foreach(var fact in runtime.Advance(Time.deltaTime,false))
  63 |             {
  64 |                 LastFact=fact.Kind+" #"+fact.ActionId.ToString("N");
  65 |                 if(fact.Release!=null)
  66 |                 {
  67 |                     var direction=target.WorldPosition-transform.position; direction.y=0;
  68 |                     melee.Resolve(fact.Release,direction.normalized);
  69 |                 }
  70 |             }
  71 |         }
  72 |         public void Cancel(string reason)
  73 |         {
  74 |             var fact=runtime==null?null:runtime.Cancel(reason);
  75 |             if(fact!=null) LastFact="Cancelled "+reason+" #"+fact.ActionId.ToString("N");
  76 |             target=null; targetHealth=null;
  77 |         }
  78 |         public void SetPaused(bool value) { paused=value; if(value) Cancel("enemy-paused"); }
  79 |         private void OnDied(HitOutcome outcome) { Cancel("death:"+outcome.ActorId); }
  80 |         private void OnDisable() { Cancel("enemy-disabled"); }
  81 |         private void OnApplicationFocus(bool value) { focused=value; if(!value) Cancel("focus-lost"); }
  82 |         private void OnApplicationPause(bool value) { applicationPaused=value; if(value) Cancel("application-paused"); }
  83 |         private void OnDestroy() { if(vitals!=null) vitals.Died-=OnDied; Cancel("destroyed"); }
  84 |         public void RestoreForDevelopment()
  85 |         { Cancel("development-reset"); vitals.RestoreForDevelopment(); brain.ResetDecision(Time.time); }
  86 |     }
  87 | }
```

## Assets/OSSASTRA/Scripts/Enemies/Runtime/StationaryEnemyBrain.cs

范围 1–67；SHA-256：2BD32711BF6735C5E115CE3FE35E29BE3EE38F556A6050C0BC4B6DAE3FF19EF7

```csharp
   1 | /*
   2 |  * 脚本：StationaryEnemyBrain
   3 |  * 功能：固定站位有限感知/决策：显式候选、距离、LOS、最近选择及请求间隔。
   4 |  * 边界：无Update/动作时钟/HP写入，无玩家类型、寻路或追击；不执行攻击。
   5 |  * 协作：Inspector保存中立Vitals，逻辑消费ICombatTarget；EnemyRoot单调度。
   6 |  */
   7 | using System;
   8 | using Ossastra.CharacterCore;
   9 | using Ossastra.Combat;
  10 | using UnityEngine;
  11 | namespace Ossastra.Enemies
  12 | {
  13 |     public sealed class StationaryEnemyBrain : MonoBehaviour
  14 |     {
  15 |         [SerializeField] private CombatVitals[] targets=Array.Empty<CombatVitals>();
  16 |         [SerializeField, Min(.1f)] private float triggerDistance=1.7f, attackInterval=1.8f;
  17 |         private CombatVitals owner;
  18 |         private EnemyRoot root;
  19 |         private float nextAttack;
  20 |         public bool Initialize(CombatVitals value)
  21 |         {
  22 |             if(value==null || value.gameObject!=gameObject || value.Health==null) return false;
  23 |             if(owner!=null && owner!=value) return false;
  24 |             owner=value; root=GetComponent<EnemyRoot>(); ResetDecision(Time.time); return root!=null;
  25 |         }
  26 |         public void ResetDecision(float now) { nextAttack=now+attackInterval; }
  27 |         public void OnAccepted(float now) { nextAttack=now+attackInterval; }
  28 |         public bool IsEligible(ICombatTarget candidate)
  29 |         {
  30 |             if(!isActiveAndEnabled || owner==null || !owner.IsAvailable ||
  31 |                 !CombatTargetValidity.IsAvailable(candidate) || candidate.TargetTransform==null) return false;
  32 |             // 必须仍在显式候选表；同ID不同实体拒绝，防止过期身份指向新对象。
  33 |             var found=false;
  34 |             foreach(var configured in targets)
  35 |             {
  36 |                 if(configured==null) continue;
  37 |                 if(ReferenceEquals(configured,candidate)) found=true;
  38 |                 else if(configured.Health!=null && configured.Health.Id==candidate.Health.Id) return false;
  39 |             }
  40 |             if(!found || !CombatTargetPolicy.CanHit(owner.Health.Id,owner.Faction,owner.IsAlive,
  41 |                 candidate.Health.Id,candidate.Faction,candidate.IsAlive)) return false;
  42 |             var offset=candidate.WorldPosition-transform.position; offset.y=0;
  43 |             if(offset.magnitude>triggerDistance) return false;
  44 |             var start=transform.position+Vector3.up*.8f;
  45 |             var end=candidate.WorldPosition+Vector3.up*.8f;
  46 |             foreach(var hit in Physics.RaycastAll(start,end-start,Vector3.Distance(start,end),~0,QueryTriggerInteraction.Ignore))
  47 |                 if(!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(candidate.TargetTransform)) return false;
  48 |             return true;
  49 |         }
  50 |         public bool TrySelect(float now,out ICombatTarget selected)
  51 |         {
  52 |             selected=null;
  53 |             if(now<nextAttack || !isActiveAndEnabled) return false;
  54 |             var nearest=float.MaxValue;
  55 |             foreach(var candidate in targets)
  56 |             {
  57 |                 if(!IsEligible(candidate)) continue;
  58 |                 var distance=(candidate.WorldPosition-transform.position).sqrMagnitude;
  59 |                 if(distance<nearest) { nearest=distance; selected=candidate; }
  60 |             }
  61 |             return selected!=null;
  62 |         }
  63 |         private void OnDisable() { if(root!=null) root.Cancel("brain-disabled"); }
  64 |         private void OnDrawGizmosSelected()
  65 |         { Gizmos.color=Color.red; Gizmos.DrawWireSphere(transform.position+Vector3.up*.8f,triggerDistance); }
  66 |     }
  67 | }
```

## Assets/OSSASTRA/Scripts/Npcs/Core/NpcInteraction.cs

范围 1–44；SHA-256：EF7CC9F7605FAA7BA6F3FCFFE006D7C6136E962303FC01E49DFE08A5066823F6

```csharp
   1 | /*
   2 |  * 脚本：NpcInteraction
   3 |  * 功能：NPC自身持有的最小交互资格和回执状态。
   4 |  * 边界：无HP/攻击/玩家根；合法请求每次计一次，拒绝不修改成功次数。
   5 |  * 协作：NpcRoot提供位置距离/可用状态，实际消费者订阅回应；销毁显式Dispose。
   6 |  */
   7 | using System;
   8 | namespace Ossastra.Npcs
   9 | {
  10 |     public readonly struct NpcReply
  11 |     {
  12 |         public readonly bool accepted;
  13 |         public readonly string npcId, actorId, reason;
  14 |         public readonly int count;
  15 |         public NpcReply(bool accepted,string npcId,string actorId,string reason,int count)
  16 |         { this.accepted=accepted;this.npcId=npcId;this.actorId=actorId;this.reason=reason;this.count=count; }
  17 |     }
  18 |     public sealed class NpcInteraction : IDisposable
  19 |     {
  20 |         private readonly string npcId;
  21 |         private readonly float rangeSquared;
  22 |         private bool disposed;
  23 |         public int Count { get; private set; }
  24 |         public event Action<NpcReply> Replied;
  25 |         public NpcInteraction(string npcId,float range)
  26 |         {
  27 |             if(string.IsNullOrWhiteSpace(npcId)||float.IsNaN(range)||float.IsInfinity(range)||range<=0)throw new ArgumentException("Invalid NPC identity/range");
  28 |             this.npcId=npcId;rangeSquared=range*range;
  29 |         }
  30 |         public NpcReply TryInteract(string actorId,float distanceSquared,bool available)
  31 |         {
  32 |             var reason=disposed?"disposed":!available?"unavailable":
  33 |                 string.IsNullOrWhiteSpace(actorId)?"invalid-actor":
  34 |                 float.IsNaN(distanceSquared)||float.IsInfinity(distanceSquared)||distanceSquared<0?"invalid-position":
  35 |                 distanceSquared>rangeSquared?"out-of-range":"NPC已回应";
  36 |             var accepted=reason=="NPC已回应";
  37 |             if(accepted) Count++;
  38 |             var reply=new NpcReply(accepted,npcId,actorId,reason,Count);
  39 |             if(accepted) Replied?.Invoke(reply);
  40 |             return reply;
  41 |         }
  42 |         public void Dispose(){disposed=true;Replied=null;}
  43 |     }
  44 | }
```

## Assets/OSSASTRA/Scripts/Npcs/Runtime/NpcRoot.cs

范围 1–37；SHA-256：6372E6B607D29CD29CDA29F62FEE6D63428130D78241AE3D2F1EE34326B6DDEF

```csharp
   1 | /*
   2 |  * 脚本：NpcRoot
   3 |  * 功能：NPC独立组合根，持有唯一身份和自身交互模块，提供公开请求/回应事件。
   4 |  * 边界：无玩家Root/敌人根/Health/InputAction/动作时钟，不实现生活或商店系统。
   5 |  * 协作：外部请求到根检查空间/可用性，再由NpcInteraction产生真实回执；销毁撤订阅。
   6 |  */
   7 | using System;
   8 | using UnityEngine;
   9 | namespace Ossastra.Npcs
  10 | {
  11 |     public sealed class NpcRoot : MonoBehaviour
  12 |     {
  13 |         [SerializeField] private string npcId="npc-structure-sample";
  14 |         [SerializeField, Min(.1f)] private float interactionRange=2f;
  15 |         private NpcInteraction interaction;
  16 |         public string NpcId => npcId;
  17 |         public int InteractionCount => interaction==null?0:interaction.Count;
  18 |         public event Action<NpcReply> Replied;
  19 |         private void Awake()
  20 |         {
  21 |             try { interaction=new NpcInteraction(npcId,interactionRange); interaction.Replied+=OnReplied; }
  22 |             catch(ArgumentException error) { Debug.LogError("NPC configuration: "+error.Message,this); enabled=false; }
  23 |         }
  24 |         public NpcReply TryInteract(string actorId,Vector3 actorPosition,bool actorAvailable)
  25 |         {
  26 |             if(interaction==null) return new NpcReply(false,npcId,actorId,"not-initialized",0);
  27 |             return interaction.TryInteract(actorId,(actorPosition-transform.position).sqrMagnitude,
  28 |                 isActiveAndEnabled && actorAvailable);
  29 |         }
  30 |         private void OnReplied(NpcReply reply) { Replied?.Invoke(reply); }
  31 |         private void OnDestroy()
  32 |         {
  33 |             if(interaction!=null) { interaction.Replied-=OnReplied; interaction.Dispose(); }
  34 |             Replied=null;
  35 |         }
  36 |     }
  37 | }
```
