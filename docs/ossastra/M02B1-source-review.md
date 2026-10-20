# M02-B1 变化源码审查

仅本轮12份变化源码；全部待用户审查。权威/风险与阅读顺序见M02B1-review.md。文件头中文总览与关键所有权/生命周期注释已逐文件核对；未动的原运动/跟随数学/动作核心不重复全文。

## CombatTargetPolicy

Assets/OSSASTRA/Scripts/Characters/Core/CombatTargetPolicy.cs

1–18；SHA256 62FFC89DC94CFF861878192C21CBAE1379BCB6120EAA6A4105BA175BA0870634

```csharp
001 | /*
002 |  * 脚本：CombatTargetPolicy
003 |  * 功能：在真实扣血前判断来源/目标身份、死活与工程阵营资格。
004 |  * 边界：不查物理、不写HP；无友伤是本轮验证选项，不是永久玩法平衡。
005 |  * 协作：Melee空间筛选后消费；Neutral保留原开发靶兼容语义。
006 |  */
007 | namespace Ossastra.CharacterCore
008 | {
009 |     public enum CombatFaction { Neutral, Allied, Enemy }
010 |     public static class CombatTargetPolicy
011 |     {
012 |         public static bool CanHit(string source, CombatFaction sourceFaction, bool sourceAlive,
013 |             string target, CombatFaction targetFaction, bool targetAlive)
014 |             => !string.IsNullOrWhiteSpace(source) && !string.IsNullOrWhiteSpace(target) &&
015 |                 sourceAlive && targetAlive && source != target &&
016 |                 (sourceFaction==CombatFaction.Neutral || targetFaction==CombatFaction.Neutral || sourceFaction!=targetFaction);
017 |     }
018 | }
```

## Health

Assets/OSSASTRA/Scripts/Characters/Core/Health.cs

1–36；SHA256 F57FAFB0EE5635D2E0F7B638432FD8177F1743EFE80EF662832E71B0748D5BB6

```csharp
001 | /*
002 |  * 脚本：Health
003 |  * 功能：保存目标身份与唯一可写的当前HP，限制伤害结果不低于零。
004 |  * 边界：不管理动画、控制权、敌人AI或正式死亡流程。
005 |  * 协作：AttackRelease经内部ApplyDamage写入；真实目标和开发重置保留同一个Health引用。
006 |  */
007 | using System;
008 |
009 | namespace Ossastra.CharacterCore
010 | {
011 |     // HP只有本对象能够写；测试目标将来引用同一个Health，不能维护另一份真实HP。
012 |     public sealed class Health
013 |     {
014 |         public string Id { get; }
015 |         public int Maximum { get; }
016 |         public int Current { get; private set; }
017 |
018 |         public Health(string id, int maximum)
019 |         {
020 |             if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Target identity required", nameof(id));
021 |             if (maximum <= 0) throw new ArgumentOutOfRangeException(nameof(maximum));
022 |             Id = id;
023 |             Maximum = maximum;
024 |             Current = maximum;
025 |         }
026 |
027 |         internal void ApplyDamage(int damage)
028 |         {
029 |             if (damage <= 0) throw new ArgumentOutOfRangeException(nameof(damage));
030 |             Current = Math.Max(0, Current - damage);
031 |         }
032 |
033 |         /// <summary>显式开发回放入口；不代表生产自动返场，不更换目标身份或Health引用。</summary>
034 |         public void RestoreForDevelopment() { Current=Maximum; }
035 |     }
036 | }
```

## AttackRelease

Assets/OSSASTRA/Scripts/Characters/Core/AttackRelease.cs

1–79；SHA256 D89605C78E0AD8A32F95B10D1736820802338D1E5E73BA5561390CC7B1747F3A

```csharp
001 | /*
002 |  * 脚本：AttackRelease
003 |  * 功能：结算一次动作释放的目标批次，并返回命中结果。
004 |  * 边界：同一释放只结算一次；按目标身份去重；HP只经Health写入。
005 |  * 协作：世界适配器先做空间筛选，再调用Resolve；空挥同样关闭该批次。
006 |  */
007 | using System;
008 | using System.Collections.Generic;
009 | using System.Linq;
010 |
011 | namespace Ossastra.CharacterCore
012 | {
013 |     public enum HitReason { Applied, Duplicate, Dead, Self }
014 |
015 |     public sealed class HitOutcome
016 |     {
017 |         public HitReason Reason { get; }
018 |         public int HpBefore { get; }
019 |         public int HpAfter { get; }
020 |         public Guid ActionId { get; }
021 |         public string ActorId { get; }
022 |         public string TargetId { get; }
023 |
024 |         internal HitOutcome(HitReason reason, int before, Health target, Guid actionId, string actorId)
025 |         {
026 |             Reason = reason;
027 |             HpBefore = before;
028 |             HpAfter = target.Current;
029 |             TargetId = target.Id;
030 |             ActionId = actionId;
031 |             ActorId = actorId;
032 |         }
033 |     }
034 |
035 |     // 每次释放只能提交一个瞬时近战候选批次；空挥也关闭，避免永久开启命中窗口。
036 |     public sealed class AttackRelease
037 |     {
038 |         private readonly Guid actionId;
039 |         private readonly string actorId;
040 |         private readonly int damage;
041 |         private bool resolved;
042 |         public string ActorId => actorId;
043 |         public Guid ActionId => actionId;
044 |
045 |         internal AttackRelease(Guid actionId, string actorId, int damage)
046 |         {
047 |             this.actionId = actionId;
048 |             this.actorId = actorId;
049 |             this.damage = damage;
050 |         }
051 |
052 |         // 调用方必须先完成实际空间/阵营过滤；本方法不谎称已做物理检测。
053 |         public IReadOnlyList<HitOutcome> Resolve(IEnumerable<Health> targets)
054 |         {
055 |             if (resolved) throw new InvalidOperationException("Attack release already resolved");
056 |             if (targets == null) throw new ArgumentNullException(nameof(targets));
057 |             var batch = targets.ToArray();
058 |             if (batch.Any(t => t == null)) throw new ArgumentException("Null candidate target", nameof(targets));
059 |             resolved = true;
060 |             var seen = new HashSet<string>(StringComparer.Ordinal);
061 |             var outcomes = new List<HitOutcome>();
062 |             foreach (var target in batch)
063 |             {
064 |                 var before = target.Current;
065 |                 HitReason reason;
066 |                 if (!seen.Add(target.Id)) reason = HitReason.Duplicate;
067 |                 else if (target.Id == actorId) reason = HitReason.Self;
068 |                 else if (target.Current == 0) reason = HitReason.Dead;
069 |                 else
070 |                 {
071 |                     target.ApplyDamage(damage);
072 |                     reason = HitReason.Applied;
073 |                 }
074 |                 outcomes.Add(new HitOutcome(reason, before, target, actionId, actorId));
075 |             }
076 |             return outcomes;
077 |         }
078 |     }
079 | }
```

## CharacterVitals

Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterVitals.cs

1–88；SHA256 62BFCDDEEA6AA76783FCE9334BD86F64F676C43DCA939BB6AD0E7D611B5B52B6

```csharp
001 | /*
002 |  * 脚本：CharacterVitals
003 |  * 功能：持有本体唯一Health/阵营，接收正式结算反馈并单次通知死亡、关闭自身表现和碰撞。
004 |  * 边界：IsAlive只读Health；不另存isDead，不选Current、不计算攻击、不自动复活。
005 |  * 协作：Root/敌人显式Initialize；Melee回调只反馈已Applied结果，Development显式恢复同一Health。
006 |  */
007 | using System;
008 | using Ossastra.CharacterCore;
009 | using UnityEngine;
010 |
011 | namespace Ossastra.Characters
012 | {
013 |     [DisallowMultipleComponent]
014 |     public sealed class CharacterVitals : MonoBehaviour, ICombatHitTarget
015 |     {
016 |         [SerializeField, Min(1)] private int maximumHp=100;
017 |         [SerializeField] private CombatFaction faction=CombatFaction.Allied;
018 |         private Renderer[] renderers;
019 |         private Collider[] colliders;
020 |         private bool[] rendererEnabled, colliderEnabled;
021 |         private MaterialPropertyBlock[] originalBlocks;
022 |         private MaterialPropertyBlock flashBlock;
023 |         private bool deathNotified, flashing;
024 |         private float flashUntil;
025 |         public Health Health { get; private set; }
026 |         public bool IsAlive => Health != null && Health.Current>0;
027 |         public CombatFaction Faction => faction;
028 |         public int DeathCount { get; private set; }
029 |         public string LastResult { get; private set; }="尚未受击";
030 |         public event Action<HitOutcome> Died;
031 |
032 |         public bool Initialize(string actorId)
033 |         {
034 |             if (Health != null) return Health.Id==actorId;
035 |             if (string.IsNullOrWhiteSpace(actorId) || maximumHp<=0) return false;
036 |             Health=new Health(actorId,maximumHp);
037 |             // Unity原生对象必须在显式主线程初始化中创建，不能依赖MonoBehaviour字段构造时机。
038 |             flashBlock=new MaterialPropertyBlock();
039 |             renderers=GetComponentsInChildren<Renderer>(true);
040 |             colliders=GetComponentsInChildren<Collider>(true);
041 |             rendererEnabled=new bool[renderers.Length]; colliderEnabled=new bool[colliders.Length];
042 |             originalBlocks=new MaterialPropertyBlock[renderers.Length];
043 |             for (var i=0;i<renderers.Length;i++)
044 |             { rendererEnabled[i]=renderers[i].enabled; originalBlocks[i]=new MaterialPropertyBlock(); renderers[i].GetPropertyBlock(originalBlocks[i]); }
045 |             for (var i=0;i<colliders.Length;i++) colliderEnabled[i]=colliders[i].enabled;
046 |             return true;
047 |         }
048 |
049 |         public void OnHitResolved(HitOutcome outcome)
050 |         {
051 |             if (Health==null || outcome==null || outcome.TargetId!=Health.Id || outcome.Reason!=HitReason.Applied) return;
052 |             LastResult=outcome.ActorId+" → "+outcome.TargetId+" "+outcome.HpBefore+"→"+outcome.HpAfter+" #"+outcome.ActionId.ToString("N");
053 |             if (IsAlive) { ShowHit(); return; }
054 |             if (deathNotified) return;
055 |             deathNotified=true; DeathCount++; flashing=false;
056 |             // 只退本体表现/碰撞，保留Root/身份/Health供会话与调试读取；死亡不靠禁用全组Root。
057 |             foreach (var r in renderers) if (r!=null) r.enabled=false;
058 |             foreach (var c in colliders) if (c!=null) c.enabled=false;
059 |             Died?.Invoke(outcome);
060 |         }
061 |
062 |         public void ShowHit()
063 |         {
064 |             if (!IsAlive || renderers==null) return;
065 |             flashUntil=Time.time+.15f; flashing=true;
066 |             flashBlock.SetColor("_Color",Color.white); flashBlock.SetColor("_BaseColor",Color.white);
067 |             foreach (var r in renderers) if (r!=null) r.SetPropertyBlock(flashBlock);
068 |         }
069 |
070 |         private void Update()
071 |         {
072 |             if (!flashing || Time.time<flashUntil) return;
073 |             flashing=false; RestoreBlocks();
074 |         }
075 |         private void RestoreBlocks()
076 |         { for (var i=0;i<renderers.Length;i++) if (renderers[i]!=null) renderers[i].SetPropertyBlock(originalBlocks[i]); }
077 |
078 |         /// <summary>开发回放恢复同一身份/Health，仅还原本组件关掉的Renderer/Collider；无生产计时返场。</summary>
079 |         public void RestoreForDevelopment()
080 |         {
081 |             if (Health==null) return;
082 |             Health.RestoreForDevelopment(); deathNotified=false; flashing=false; LastResult="Development重置";
083 |             for (var i=0;i<renderers.Length;i++) if (renderers[i]!=null) renderers[i].enabled=rendererEnabled[i];
084 |             for (var i=0;i<colliders.Length;i++) if (colliders[i]!=null) colliders[i].enabled=colliderEnabled[i];
085 |             RestoreBlocks();
086 |         }
087 |     }
088 | }
```

## CharacterContracts

Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterContracts.cs

1–95；SHA256 E321A0D908AA06D9FE1A6EEA304742AB010E3DB95DE47B65C482C4FB1D9D3032

```csharp
001 | /*
002 |  * 脚本：CharacterContracts
003 |  * 功能：集中声明角色领域的窄职责契约，供组合根和DataBus依赖。
004 |  * 边界：只定义能力；实际实现与调用顺序不在本文件。
005 |  * 协作：Inspector仍保存具体组件；Root运行时按接口连接，只有必要模块接受Bus初始化。
006 |  */
007 | using Ossastra.CharacterCore;
008 | using UnityEngine;
009 | using System.Collections.Generic;
010 |
011 | namespace Ossastra.Characters
012 | {
013 |     /// <summary>Root只读本体生命资格；唯一HP仍由Vitals内的Health持有。</summary>
014 |     public interface ICharacterLifeState { bool IsAlive { get; } }
015 |     /// <summary>真实战斗目标的阵营与结算通知；通知反馈/死亡，不再次扣血。</summary>
016 |     public interface ICombatHitTarget : ICharacterHitTarget, ICharacterLifeState
017 |     {
018 |         CombatFaction Faction { get; }
019 |         void OnHitResolved(HitOutcome outcome);
020 |     }
021 |     /// <summary>队伍只分配合法目标；角色能力自行计算移动，不从此契约取得方向或策略状态。</summary>
022 |     public interface ICharacterFollowTargetSource
023 |     {
024 |         bool TryGetFollowTarget(CharacterRoot follower, out CharacterRoot target);
025 |     }
026 |     /// <summary>场景唯一控制权的只读视图；代际和共享屏障同时约束玩家与伙伴来源。</summary>
027 |     public interface ICharacterControlSession
028 |     {
029 |         CharacterRoot Current { get; }
030 |         IReadOnlyList<CharacterRoot> Characters { get; }
031 |         bool IsIntentSamplingAllowed { get; }
032 |         int ControlRevision { get; }
033 |     }
034 |     /// <summary>伙伴策略只提供移动候选帧；选择器剥离攻击、暂停、取消和开发命令。</summary>
035 |     public interface ICompanionIntentSource
036 |     {
037 |         bool IsAvailable { get; }
038 |         CharacterInputFrame ReadCompanionIntent(CharacterRoot character, float delta);
039 |     }
040 |     /// <summary>玩家源的实时许可；外层已采样帧在许可撤回时同样失效，兼容未挂跟随模块的旧场。</summary>
041 |     public interface ICharacterInputAvailability
042 |     {
043 |         bool HasPlayerInput { get; }
044 |     }
045 |     /// <summary>由场景实控权威回答谁可以采样玩家输入；输入源不保存第二份选中状态。</summary>
046 |     public interface ICharacterPlayerControlSource
047 |     {
048 |         bool IsPlayerInputAllowed(ICharacterInputSource source);
049 |     }
050 |     /// <summary>仅为确需角色Bus的模块提供显式初始化；失败阻止角色启动。</summary>
051 |     public interface ICharacterSubsystem { bool Initialize(CharacterDataBus dataBus); }
052 |     /// <summary>提供单角色唯一有效输入快照，不判定动作是否成功。</summary>
053 |     public interface ICharacterInputSource
054 |     {
055 |         CharacterInputFrame CurrentFrame { get; }
056 |         bool IsConfigured { get; }
057 |         CharacterInputFrame Read();
058 |     }
059 |     /// <summary>执行实际运动并报告结果；不决定攻击朝向。</summary>
060 |     public interface ICharacterMovement : ICharacterSubsystem
061 |     {
062 |         bool IsMoving { get; }
063 |         Vector3 Velocity { get; }
064 |         void Tick(bool allowed, float delta);
065 |         void Teleport(Vector3 position);
066 |     }
067 |     /// <summary>分别维护攻击方向与可见翻转，由Root显式调度。</summary>
068 |     public interface ICharacterOrientation
069 |     {
070 |         Vector3 AttackDirection { get; }
071 |         float VisualFacingX { get; }
072 |         void ResolveDirections(Vector2 moveInput, Vector3? pointerDirection, Vector2 stickDirection, bool movementFacing);
073 |         void OrientTowards(Vector3 worldPosition);
074 |         void FaceAttackDirection();
075 |     }
076 |     /// <summary>从角色状态更新表现与暂停，不反向产生命中。</summary>
077 |     public interface ICharacterPresentation : ICharacterSubsystem
078 |     {
079 |         string CurrentClip { get; }
080 |         void SetPaused(bool paused);
081 |         void Present();
082 |     }
083 |     /// <summary>消费一次释放与方向快照，执行世界目标筛选。</summary>
084 |     public interface ICharacterHitResolver
085 |     {
086 |         string LastResult { get; }
087 |         void Resolve(AttackRelease release, Vector3 releaseDirection);
088 |     }
089 |     /// <summary>提供同一Health和命中反馈；生产命中模块不依赖开发靶类型。</summary>
090 |     public interface ICharacterHitTarget
091 |     {
092 |         Health Health { get; }
093 |         void ShowHit();
094 |     }
095 | }
```

## CharacterRoot

Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterRoot.cs

1–206；SHA256 FA6406F2B82CA9E697DF6233DE182C5BDA6C0F0D4B3E729987E932F02BCC3CC6

```csharp
001 | /*
002 |  * 脚本：CharacterRoot
003 |  * 功能：组织单角色的接口接线、输入、动作事实、运动和表现。
004 |  * 边界：持有本角色运行时；只读生命资格，不实现HP写入、设备绑定或开发GUI。
005 |  * 协作：初始化可选本体Follow，场景只分配目标；单意图入口与原动作/运动时序保持。
006 |  */
007 | using System;
008 | using Ossastra.CharacterCore;
009 | using UnityEngine;
010 | using UnityEngine.Serialization;
011 |
012 | namespace Ossastra.Characters
013 | {
014 |     [DefaultExecutionOrder(-100)]
015 |     [RequireComponent(typeof(CharacterMovement), typeof(CharacterMeleeHit), typeof(CharacterInputHandler))]
016 |     [RequireComponent(typeof(CharacterOrientation))]
017 |     public sealed class CharacterRoot : MonoBehaviour
018 |     {
019 |         [SerializeField] private string actorId = "player";
020 |         [FormerlySerializedAs("motor"), SerializeField] private CharacterMovement movement;
021 |         [SerializeField] private CharacterOrientation orientation;
022 |         [SerializeField] private CharacterSpineView view;
023 |         [SerializeField] private CharacterMeleeHit melee;
024 |         [SerializeField] private CharacterInputHandler input;
025 |         [SerializeField] private Camera viewCamera;
026 |         [SerializeField] private CharacterFollow follow;
027 |         [SerializeField] private CharacterVitals vitals;
028 |         private ICharacterLifeState lifeState;
029 |         [Header("Prototype timing — not final balance")]
030 |         [SerializeField] private float releaseAt = .3f, moveReadyAt = .5f, attackReadyAt = .8f, finishAt = 1.1f;
031 |         [SerializeField, Min(1)] private int damage = 12;
032 |         [SerializeField] private bool verboseTrace;
033 |         private readonly ControlAuthority authority = new ControlAuthority();
034 |         private readonly AttackIntent intent = new AttackIntent();
035 |         private ICharacterInputSource inputSource;
036 |         private CharacterIntentSelector intentSelector;
037 |         private ICharacterMovement mover;
038 |         private ICharacterOrientation orienter;
039 |         private ICharacterPresentation presentation;
040 |         private ICharacterHitResolver hitResolver;
041 |         private BasicActionRuntime runtime;
042 |         private BasicAttackDefinition definition;
043 |         private bool ready, focused = true, paused, applicationPaused;
044 |         private string lastFact = "Idle";
045 |         public CharacterDataBus DataBus { get; private set; }
046 |         public string ActorId => actorId;
047 |         public string LastFact => lastFact;
048 |         public bool IsPaused => paused || applicationPaused;
049 |         public bool ActionActive => DataBus != null && DataBus.ActionActive;
050 |         public bool IsAlive => lifeState==null || lifeState.IsAlive;
051 |         public bool CanSimulateIntent => ready && IsAlive && isActiveAndEnabled && focused && !IsPaused;
052 |         public CharacterVitals Vitals => vitals;
053 |         public event Action<CharacterRoot> Died;
054 |         public CharacterFollow Follow => follow;
055 |         public CharacterIntentKind EffectiveIntentKind => intentSelector == null ? CharacterIntentKind.Neutral : intentSelector.EffectiveKind;
056 |
057 |         public bool CanBindCompanionIntent(ICharacterControlSession session, ICompanionIntentSource source)
058 |             => ready && follow != null && ReferenceEquals(source,follow) && intentSelector != null && intentSelector.CanBind(session,source);
059 |         public bool BindCompanionIntent(ICharacterControlSession session, ICompanionIntentSource source)
060 |             => CanBindCompanionIntent(session,source) && intentSelector.Bind(session,source);
061 |         public void ClearCompanionIntent(ICompanionIntentSource owner)
062 |         { if (intentSelector != null) intentSelector.Clear(owner); }
063 |
064 |         private void Awake()
065 |         {
066 |             if (viewCamera == null) viewCamera = Camera.main;
067 |             if (vitals!=null)
068 |             {
069 |                 if (!vitals.Initialize(actorId)) { Debug.LogError("Character vitals identity/configuration invalid",this); enabled=false; return; }
070 |                 lifeState=vitals; vitals.Died+=OnDied;
071 |             }
072 |             if (movement == null || orientation == null || view == null || melee == null || input == null || viewCamera == null)
073 |             { Debug.LogError("Character requires registered input/movement/orientation/view/melee/camera", this); enabled = false; return; }
074 |             // Inspector保留具体组件槽；运行期通过窄接口消费，不反向依赖开发GUI或测试靶。
075 |             // 每角色只有这一条有效Frame入口；没有Director绑定时保持原玩家适配器行为。
076 |             intentSelector = new CharacterIntentSelector(this,input);
077 |             inputSource = intentSelector;
078 |             mover = movement;
079 |             orienter = orientation;
080 |             presentation = view;
081 |             hitResolver = melee;
082 |             if (!inputSource.IsConfigured) { Debug.LogError("Character actions are not configured", this); enabled = false; return; }
083 |             try
084 |             {
085 |                 definition = new BasicAttackDefinition(releaseAt, moveReadyAt, attackReadyAt, finishAt, damage);
086 |                 runtime = new BasicActionRuntime(actorId);
087 |             }
088 |             catch (ArgumentException error) { Debug.LogError("Character configuration: " + error.Message, this); enabled = false; return; }
089 |             // 每角色独立Bus只读取原所有者；只有确需Bus的运动和表现执行初始化。
090 |             DataBus = new CharacterDataBus(actorId, inputSource, mover, orienter, runtime, authority);
091 |             ready = InitializeSubsystems(DataBus, mover, presentation);
092 |             if (!ready) { Debug.LogError("Character subsystem initialization failed", this); enabled = false; }
093 |             // 可选能力只接受本角色初始化；无组件/无assignment的旧场保持普通玩家行为。
094 |             if (follow != null && !follow.Initialize(this)) Debug.LogWarning("Character follow unavailable: configuration or owner invalid", this);
095 |         }
096 |
097 |         private static bool InitializeSubsystems(CharacterDataBus dataBus, params ICharacterSubsystem[] subsystems)
098 |         {
099 |             foreach (var subsystem in subsystems) if (subsystem == null || !subsystem.Initialize(dataBus)) return false;
100 |             return true;
101 |         }
102 |
103 |         private void Update()
104 |         {
105 |             if (!ready || !IsAlive) return;
106 |             // 选择器只采一个源；伙伴只能提供移动，切人屏障为空帧，动作时钟仍继续。
107 |             inputSource.Read();
108 |             var frame = DataBus.Input;
109 |             if (frame.pause) SetPaused(!paused);
110 |             if (frame.cancel) CancelAction("player-cancel");
111 |             var canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
112 |             if (focused && !IsPaused)
113 |                 orienter.ResolveDirections(frame.move, PointerDirection(frame), frame.hasDirectionalAim ? frame.aimDirection : Vector2.zero, canMove);
114 |             // 输入边沿不等于动作成功：焦点、UI和Runtime的暂停/权限/Ready资格分别把关。
115 |             if (intent.Observe(frame.attackHeld) && focused && !frame.attackPointerBlocked)
116 |             {
117 |                 var request = runtime.Request(definition, "player-input", authority, IsPaused);
118 |                 if (request.PreviousTerminal != null) Observe(request.PreviousTerminal);
119 |                 if (request.Accepted) { Observe(request.Fact); orienter.FaceAttackDirection(); }
120 |                 else lastFact = "Rejected: " + request.Reason;
121 |             }
122 |             // Accepted只建立动作身份；只有Released带一次性命中凭据。切人不会重发事实。
123 |             foreach (var fact in runtime.Advance(Time.deltaTime, IsPaused || !focused))
124 |             {
125 |                 Observe(fact);
126 |                 if (fact.Release != null)
127 |                 {
128 |                     // 在释放当帧按值采样方向；不是接受动作时锁定，前摇中仍可重新瞄准。
129 |                     var releaseDirection = orienter.AttackDirection;
130 |                     orienter.FaceAttackDirection();
131 |                     hitResolver.Resolve(fact.Release, releaseDirection);
132 |                 }
133 |             }
134 |             // 本帧可能跨过MoveReady；推进事实后重新查询许可，Run依据真实位移。
135 |             canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
136 |             mover.Tick(canMove, IsPaused || !focused ? 0 : Time.deltaTime);
137 |             presentation.SetPaused(IsPaused || !focused);
138 |             presentation.Present();
139 |         }
140 |
141 |         private Vector3? PointerDirection(CharacterInputFrame frame)
142 |         {
143 |             if (!frame.hasPointer) return null;
144 |             var plane = new Plane(Vector3.up, transform.position);
145 |             var ray = viewCamera.ScreenPointToRay(frame.pointer);
146 |             if (!plane.Raycast(ray, out var distance)) return null;
147 |             var direction = ray.GetPoint(distance) - transform.position;
148 |             direction.y = 0;
149 |             return direction.sqrMagnitude < .0001f ? (Vector3?)null : direction;
150 |         }
151 |
152 |         private void Observe(ActionFact fact)
153 |         {
154 |             lastFact = fact.Kind + " @" + fact.At.ToString("F3") + " " + fact.ActionId.ToString("N").Substring(0, 8);
155 |             if (fact.Reason != null) lastFact += " " + fact.Reason;
156 |             if (verboseTrace) Debug.Log("Character " + lastFact, this);
157 |         }
158 |
159 |         public void CancelAction(string reason)
160 |         {
161 |             // 取消必须具名；抑制仍按住的输入，不能把下一帧误判为新请求。
162 |             intent.SuppressUntilRelease();
163 |             var terminal = runtime == null ? null : runtime.Cancel(reason);
164 |             if (terminal != null) Observe(terminal);
165 |         }
166 |
167 |         private void OnDied(HitOutcome outcome)
168 |         {
169 |             // HP已由结算归零；这里仅终止本体未释放动作/有效帧，再交唯一会话处理实控。
170 |             CancelAction("death:"+outcome.ActorId+":"+outcome.ActionId.ToString("N"));
171 |             if (follow!=null) follow.ResetIntent();
172 |             if (intentSelector!=null) intentSelector.Clear();
173 |             Died?.Invoke(this);
174 |         }
175 |
176 |         public void SetPaused(bool value) { paused = value; intent.SuppressUntilRelease(); if (follow != null) follow.ResetIntent(); }
177 |         public void SetAttackRestriction(object owner, bool restricted, string reason = "attack-restricted")
178 |         {
179 |             if (restricted) { authority.Block(owner, ControlChannel.Attack); CancelAction(reason); }
180 |             else authority.Release(owner);
181 |         }
182 |
183 |         private void OnApplicationFocus(bool hasFocus)
184 |         { focused = hasFocus; if (follow != null) follow.ResetIntent(); if (!hasFocus) CancelAction("focus-lost"); else intent.SuppressUntilRelease(); }
185 |         private void OnApplicationPause(bool value)
186 |         { applicationPaused = value; if (follow != null) follow.ResetIntent(); if (value) intent.SuppressUntilRelease(); }
187 |         private void OnEnable() { intent.SuppressUntilRelease(); if (intentSelector != null) intentSelector.Clear(); }
188 |         private void OnDisable() { CancelAction("disabled"); if (follow != null) follow.ResetIntent(); if (intentSelector != null) intentSelector.Clear(); if (ready && presentation != null) presentation.SetPaused(false); }
189 |         private void OnDestroy()
190 |         {
191 |             if (vitals!=null) vitals.Died-=OnDied;
192 |             // 终止本角色动作后解除本对象引用；不清其它角色或场景控制权威。
193 |             CancelAction("destroyed");
194 |             ready = false;
195 |             DataBus = null;
196 |             runtime = null;
197 |             inputSource = null;
198 |             if (intentSelector != null) intentSelector.Clear();
199 |             intentSelector = null;
200 |             mover = null;
201 |             orienter = null;
202 |             presentation = null;
203 |             hitResolver = null;
204 |         }
205 |     }
206 | }
```

## CharacterMeleeHit

Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterMeleeHit.cs

1–44；SHA256 43FC1A21E13AC448ED8CF95B9BF544D40ABD797965BDCCA23E8D7D947D34AF8C

```csharp
001 | /*
002 |  * 脚本：CharacterMeleeHit
003 |  * 功能：将一次释放的方向快照转换为物理候选、敌我资格与唯一伤害批次。
004 |  * 边界：不建动作时钟/HP；来源死亡或身份不符只关闭空批次，不产生新伤害。
005 |  * 协作：Root和敌人共用Released入口；AttackRelease扣血后通知真实目标，开发靶保留原ShowHit。
006 |  */
007 | using System;
008 | using System.Linq;
009 | using Ossastra.CharacterCore;
010 | using UnityEngine;
011 | namespace Ossastra.Characters
012 | {
013 |     public sealed class CharacterMeleeHit : MonoBehaviour, ICharacterHitResolver
014 |     {
015 |         [SerializeField, Min(0)] private float reach=.8f;
016 |         [SerializeField, Min(.01f)] private float radius=.65f;
017 |         [SerializeField] private float height=.8f;
018 |         [SerializeField] private LayerMask targetLayers=~0;
019 |         public string LastResult { get; private set; }="未释放";
020 |         public void Resolve(AttackRelease release, Vector3 releaseDirection)
021 |         {
022 |             if (release==null) throw new ArgumentNullException(nameof(release));
023 |             var source=GetComponent<CharacterVitals>();
024 |             if (source!=null && (!source.IsAlive || source.Health.Id!=release.ActorId))
025 |             { release.Resolve(Array.Empty<Health>()); LastResult="来源死亡或释放身份不符"; return; }
026 |             var center=transform.position+Vector3.up*height+releaseDirection*reach;
027 |             var targets=Physics.OverlapSphere(center,radius,targetLayers,QueryTriggerInteraction.Collide)
028 |                 .Select(c=>c.GetComponentsInParent<MonoBehaviour>().OfType<ICharacterHitTarget>().FirstOrDefault())
029 |                 .Where(t=>t!=null && t.Health!=null && CombatTargetPolicy.CanHit(release.ActorId,
030 |                     source==null?CombatFaction.Neutral:source.Faction,source==null||source.IsAlive,
031 |                     t.Health.Id,t is ICombatHitTarget combat?combat.Faction:CombatFaction.Neutral,t.Health.Current>0))
032 |                 .Distinct().ToArray();
033 |             var outcomes=release.Resolve(targets.Select(t=>t.Health));
034 |             foreach (var outcome in outcomes)
035 |                 if (outcome.Reason==HitReason.Applied)
036 |                 {
037 |                     var target=targets.First(t=>t.Health.Id==outcome.TargetId);
038 |                     if (target is ICombatHitTarget combat) combat.OnHitResolved(outcome); else target.ShowHit();
039 |                 }
040 |             LastResult=outcomes.Count==0?"空挥／无合法敌对目标":string.Join("; ",outcomes.Select(o=>
041 |                 o.ActorId+" → "+o.TargetId+" "+o.HpBefore+"→"+o.HpAfter+" "+o.Reason+" #"+o.ActionId.ToString("N")));
042 |         }
043 |     }
044 | }
```

## CharacterIntentSelector

Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterIntentSelector.cs

1–72；SHA256 162207E0B34361737ACAE6BDE85145694DC3014C9586144B15F2C2813B3BACF3

```csharp
001 | /*
002 |  * 脚本：CharacterIntentSelector
003 |  * 功能：在Root惰性采样时，只从玩家、伙伴、空意图中选择一个有效Frame。
004 |  * 边界：不决定距离、不采InputAction、不写运动/动作；未绑定跟随时委托原玩家输入。
005 |  * 协作：Root与Bus共用同一选择器；会话代际或当帧变化立刻使旧快照失效。
006 |  */
007 | using Ossastra.CharacterCore;
008 | using UnityEngine;
009 |
010 | namespace Ossastra.Characters
011 | {
012 |     public sealed class CharacterIntentSelector : ICharacterInputSource
013 |     {
014 |         private readonly CharacterRoot character;
015 |         private readonly ICharacterInputSource player;
016 |         private ICharacterControlSession session;
017 |         private ICompanionIntentSource companion;
018 |         private bool managed;
019 |         private CharacterInputFrame frame;
020 |         private CharacterIntentKind kind;
021 |         private int sampledFrame = -1, sampledRevision;
022 |
023 |         public CharacterIntentSelector(CharacterRoot character, ICharacterInputSource player)
024 |         { this.character=character; this.player=player; }
025 |         public bool IsConfigured => player != null && player.IsConfigured;
026 |         private bool ValidSample => sampledFrame == Time.frameCount && character != null && character.IsAlive && character.isActiveAndEnabled &&
027 |             (!managed || (Alive(session) && session.IsIntentSamplingAllowed && session.ControlRevision == sampledRevision)) &&
028 |             // 即使旧场未绑定Director，玩家适配器被撤权也必须立即使外层帧归零。
029 |             (kind != CharacterIntentKind.Player || !(player is ICharacterInputAvailability availability) || availability.HasPlayerInput) &&
030 |             (kind != CharacterIntentKind.Companion || (Alive(companion) && companion.IsAvailable));
031 |         public CharacterInputFrame CurrentFrame => ValidSample ? frame : default;
032 |         public CharacterIntentKind EffectiveKind => ValidSample ? kind : CharacterIntentKind.Neutral;
033 |
034 |         public bool CanBind(ICharacterControlSession authority, ICompanionIntentSource source)
035 |             => Alive(authority) && Alive(source) && (!managed || !Alive(companion) || ReferenceEquals(companion,source));
036 |
037 |         /// <summary>只允许同一伙伴提供者重绑或已销毁提供者被替代；不抢占活owner。</summary>
038 |         public bool Bind(ICharacterControlSession authority, ICompanionIntentSource source)
039 |         {
040 |             if (!CanBind(authority,source)) return false;
041 |             session=authority; companion=source; managed=true; Clear(); return true;
042 |         }
043 |
044 |         public void Clear(ICompanionIntentSource owner = null)
045 |         {
046 |             if (owner != null && !ReferenceEquals(owner,companion)) return;
047 |             frame=default; kind=CharacterIntentKind.Neutral; sampledFrame=-1;
048 |         }
049 |
050 |         public CharacterInputFrame Read()
051 |         {
052 |             Clear();
053 |             if (character == null || !character.IsAlive || !character.isActiveAndEnabled) return default;
054 |             var selectedKind=CharacterIntentPolicy.Select(managed, Alive(session) && session.IsIntentSamplingAllowed,
055 |                 Alive(session) && session.Current == character, Alive(companion) && companion.IsAvailable);
056 |             if (selectedKind == CharacterIntentKind.Player) frame=player.Read();
057 |             else if (selectedKind == CharacterIntentKind.Companion)
058 |             {
059 |                 // 伙伴即使给了其它字段，也只准传递move；不能借AI帧自动攻击、取消或暂停。
060 |                 var candidate=companion.ReadCompanionIntent(character,Time.deltaTime);
061 |                 frame=new CharacterInputFrame { move=candidate.move };
062 |             }
063 |             // 提供者发现新目标会先撤回旧帧；只有本次读取完成后才发布新来源身份。
064 |             kind=selectedKind;
065 |             sampledFrame=Time.frameCount;
066 |             sampledRevision=Alive(session) ? session.ControlRevision : 0;
067 |             return CurrentFrame;
068 |         }
069 |
070 |         private static bool Alive(object value) => value != null && (!(value is Object unityObject) || unityObject != null);
071 |     }
072 | }
```

## CharacterInputHandler

Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterInputHandler.cs

1–143；SHA256 B1938306C67B70A06E85231B7236108E2A864F8C0E595EED0316990D8B6B4D78

```csharp
001 | /*
002 |  * 脚本：CharacterInputHandler
003 |  * 功能：从Inspector配置的InputAction采样单角色输入快照。
004 |  * 边界：只采输入；不接受动作、不驱动物理或写HP；每实例管理Action生命周期。
005 |  * 协作：选择器经接口读取；死亡交接额外等待移动中立，普通Tab仍保留原移动语义。
006 |  */
007 | using UnityEngine.EventSystems;
008 | using System.Collections.Generic;
009 | using UnityEngine;
010 | using UnityEngine.InputSystem;
011 |
012 | namespace Ossastra.Characters
013 | {
014 |     public struct CharacterInputFrame
015 |     {
016 |         public Vector2 move, pointer, aimDirection;
017 |         public bool attackHeld, cancel, pause, hasPointer, hasDirectionalAim, attackPointerBlocked;
018 |         public int debugCommand;
019 |     }
020 |
021 |     // Each character owns its serialized actions. Input does not accept attacks or write gameplay state.
022 |     [DisallowMultipleComponent]
023 |     public sealed class CharacterInputHandler : MonoBehaviour, ICharacterInputSource, ICharacterInputAvailability
024 |     {
025 |         [Header("移动 / VELGITA movementAction")]
026 |         public InputAction movementAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
027 |         [Header("鼠标位置 / VELGITA mouseAction")]
028 |         public InputAction mouseAction = new InputAction("Pointer", InputActionType.Value, expectedControlType: "Vector2");
029 |         [Header("手柄瞄准方向 / VELGITA gamePad_MouseAction")]
030 |         public InputAction gamepadAimAction = new InputAction("GamepadAim", InputActionType.Value, expectedControlType: "Vector2");
031 |         [Header("当前普攻 / VELGITA HoundWeaponAttack_Action")]
032 |         public InputAction attackAction = new InputAction("Attack", InputActionType.Button);
033 |         [Header("取消与暂停")]
034 |         public InputAction cancelAction = new InputAction("Cancel", InputActionType.Button);
035 |         public InputAction pauseAction = new InputAction("Pause", InputActionType.Button);
036 |         [Header("开发检查入口")]
037 |         public InputAction debugValidAction = new InputAction("DebugValid", InputActionType.Button);
038 |         public InputAction debugBlockedAction = new InputAction("DebugBlocked", InputActionType.Button);
039 |         public InputAction debugResetAction = new InputAction("DebugReset", InputActionType.Button);
040 |         public InputAction debugInspectAction = new InputAction("DebugInspect", InputActionType.Button);
041 |
042 |         private readonly Dictionary<object, Rect> pointerRegions = new Dictionary<object, Rect>();
043 |         private ICharacterPlayerControlSource playerControl;
044 |         private bool managedPlayerInput, attackArmed = true;
045 |         private bool movementArmed=true;
046 |
047 |         public bool HasPlayerInput => !managedPlayerInput || (ControlSourceAlive && playerControl.IsPlayerInputAllowed(this));
048 |         private bool ControlSourceAlive => playerControl != null &&
049 |             (!(playerControl is Object unityObject) || unityObject != null);
050 |
051 |         public bool CanBindPlayerControl(ICharacterPlayerControlSource source)
052 |             => source != null && (!managedPlayerInput || !ControlSourceAlive || ReferenceEquals(playerControl, source));
053 |
054 |         /// <summary>绑定唯一场景权威；拒绝另一存活协调者抢占同一输入。</summary>
055 |         public bool BindPlayerControl(ICharacterPlayerControlSource source)
056 |         {
057 |             if (!CanBindPlayerControl(source)) return false;
058 |             playerControl = source;
059 |             managedPlayerInput = true;
060 |             ResetPlayerInput();
061 |             return true;
062 |         }
063 |
064 |         /// <summary>移交时清掉旧快照；不假装一次松键，必须真正观察释放后才重武装攻击。</summary>
065 |         public void ResetPlayerInput(bool requireMovementRelease=false)
066 |         { CurrentFrame=default; attackArmed=false; if (requireMovementRelease) movementArmed=false; }
067 |         public void RegisterPointerRegion(object owner, Rect region) { pointerRegions[owner] = region; }
068 |         public void RemovePointerRegion(object owner) { pointerRegions.Remove(owner); }
069 |
070 |         private bool IsPointerBlocked(Vector2 pointer)
071 |         {
072 |             if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return true;
073 |             var guiPoint = new Vector2(pointer.x, Screen.height - pointer.y);
074 |             foreach (var region in pointerRegions.Values) if (region.Contains(guiPoint)) return true;
075 |             return false;
076 |         }
077 |
078 |         public CharacterInputFrame CurrentFrame { get; private set; }
079 |
080 |         public bool IsConfigured => movementAction != null && movementAction.bindings.Count > 0 &&
081 |             attackAction != null && attackAction.bindings.Count > 0 &&
082 |             ((mouseAction != null && mouseAction.bindings.Count > 0) ||
083 |              (gamepadAimAction != null && gamepadAimAction.bindings.Count > 0));
084 |
085 |         private IEnumerable<InputAction> Actions()
086 |         {
087 |             // 同步枚举仅统一启用/禁用/释放清单；不是Unity协程，没有跨帧等待。
088 |             yield return movementAction;
089 |             yield return mouseAction;
090 |             yield return gamepadAimAction;
091 |             yield return attackAction;
092 |             yield return cancelAction;
093 |             yield return pauseAction;
094 |             yield return debugValidAction;
095 |             yield return debugBlockedAction;
096 |             yield return debugResetAction;
097 |             yield return debugInspectAction;
098 |         }
099 |
100 |         private void OnEnable() { foreach (var action in Actions()) if (action != null) action.Enable(); }
101 |         private void OnDisable() { ResetPlayerInput(); foreach (var action in Actions()) if (action != null) action.Disable(); }
102 |         private void OnDestroy() { foreach (var action in Actions()) if (action != null) action.Dispose(); }
103 |
104 |         public CharacterInputFrame Read()
105 |         {
106 |             var frame = new CharacterInputFrame();
107 |             // 非实控在读取设备前返回空帧；Root仍推进自己已接受的动作，不会偷读另一角色输入。
108 |             if (!HasPlayerInput || !isActiveAndEnabled || movementAction == null || !movementAction.enabled)
109 |             { CurrentFrame = frame; return frame; }
110 |             frame.move = movementAction.ReadValue<Vector2>();
111 |             if (!movementArmed)
112 |             {
113 |                 // 死亡交接不能把旧WASD/摇杆位移转发给幸存者；必须真实回中才重新采移动。
114 |                 if (frame.move.sqrMagnitude<.0001f) movementArmed=true;
115 |                 frame.move=Vector2.zero;
116 |             }
117 |             if (mouseAction != null && mouseAction.enabled)
118 |             {
119 |                 frame.pointer = mouseAction.ReadValue<Vector2>();
120 |                 frame.hasPointer = mouseAction.controls.Count > 0 && frame.pointer.x >= 0 && frame.pointer.x <= Screen.width &&
121 |                     frame.pointer.y >= 0 && frame.pointer.y <= Screen.height && !IsPointerBlocked(frame.pointer);
122 |             }
123 |             if (gamepadAimAction != null && gamepadAimAction.enabled)
124 |             {
125 |                 frame.aimDirection = gamepadAimAction.ReadValue<Vector2>();
126 |                 frame.hasDirectionalAim = frame.aimDirection.sqrMagnitude > .01f;
127 |             }
128 |             var held = attackAction != null && attackAction.enabled && attackAction.IsPressed();
129 |             if (!held) attackArmed = true;
130 |             frame.attackHeld = held && attackArmed;
131 |             if (attackAction != null && attackAction.activeControl != null && attackAction.activeControl.device is Pointer &&
132 |                 !frame.hasPointer) frame.attackPointerBlocked = true;
133 |             frame.cancel = Pressed(cancelAction);
134 |             frame.pause = Pressed(pauseAction);
135 |             frame.debugCommand = Pressed(debugValidAction) ? 1 : Pressed(debugBlockedAction) ? 2 :
136 |                 Pressed(debugResetAction) ? 3 : Pressed(debugInspectAction) ? 4 : 0;
137 |             CurrentFrame = frame;
138 |             return frame;
139 |         }
140 |
141 |         private static bool Pressed(InputAction action) => action != null && action.enabled && action.WasPressedThisFrame();
142 |     }
143 | }
```

## CharacterControlCoordinator

Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterControlCoordinator.cs

1–171；SHA256 99EA71996E547971757548C2A7EEBAE90F5BC86605DA3208E8AE8A5FDF06DCC8

```csharp
001 | /*
002 |  * 脚本：CharacterControlCoordinator
003 |  * 功能：保存同组角色唯一的玩家实控对象，并读取Inspector切换输入。
004 |  * 边界：不接管各角色动作时钟、HP或AI；不使用全局单例。
005 |  * 协作：在Root之前更新；订阅本体死亡立即交接，玩家/伙伴只读同一许可/代际，不复制Current。
006 |  */
007 | using System;
008 | using System.Collections.Generic;
009 | using Ossastra.CharacterCore;
010 | using UnityEngine;
011 | using UnityEngine.InputSystem;
012 |
013 | namespace Ossastra.Characters
014 | {
015 |     [DefaultExecutionOrder(-200)]
016 |     [DisallowMultipleComponent]
017 |     public sealed class CharacterControlCoordinator : MonoBehaviour, ICharacterPlayerControlSource, ICharacterControlSession
018 |     {
019 |         [SerializeField] private CharacterRoot[] characters = Array.Empty<CharacterRoot>();
020 |         [SerializeField, Min(0)] private int initialIndex;
021 |         [Header("切换实控角色 / 可在Inspector修改")]
022 |         public InputAction switchAction = new InputAction("SwitchCharacter", InputActionType.Button);
023 |         private readonly AttackIntent switchIntent = new AttackIntent();
024 |         private CharacterInputHandler[] inputs;
025 |         private bool configured, bindingsOwned, focused = true, applicationPaused;
026 |         private int handoverFrame = -1;
027 |         public CharacterRoot Current { get; private set; }
028 |         public IReadOnlyList<CharacterRoot> Characters => Array.AsReadOnly(characters);
029 |         public string Status { get; private set; } = "未初始化";
030 |         public int ControlRevision { get; private set; }
031 |         public bool IsIntentSamplingAllowed => configured && isActiveAndEnabled && focused && !applicationPaused &&
032 |             Current != null && Current.IsAlive && Current.isActiveAndEnabled && Current.DataBus != null && Time.frameCount > handoverFrame;
033 |
034 |         private void Awake()
035 |         {
036 |             if (characters == null) characters = Array.Empty<CharacterRoot>();
037 |             inputs = new CharacterInputHandler[characters.Length];
038 |             var identities = new HashSet<string>(StringComparer.Ordinal);
039 |             var sources = new HashSet<CharacterInputHandler>();
040 |             var valid = characters.Length >= 2 && initialIndex < characters.Length && initialIndex >= 0 &&
041 |                 switchAction != null && switchAction.bindings.Count > 0;
042 |             // 先完整校验，再绑定；合法成员在配置错误时也归本协调者关闭，不能退回双控。
043 |             for (var i = 0; i < characters.Length; i++)
044 |             {
045 |                 var character = characters[i];
046 |                 if (character == null) { valid = false; continue; }
047 |                 inputs[i] = character.GetComponent<CharacterInputHandler>();
048 |                 if (inputs[i] == null || !sources.Add(inputs[i]) ||
049 |                     string.IsNullOrWhiteSpace(character.ActorId) || !identities.Add(character.ActorId)) valid = false;
050 |             }
051 |             foreach (var input in inputs)
052 |                 if (input != null && !input.CanBindPlayerControl(this))
053 |                 { Status = "输入已归另一协调者；拒绝抢占"; enabled = false; return; }
054 |             foreach (var input in inputs) if (input != null) input.BindPlayerControl(this);
055 |             bindingsOwned = true;
056 |             foreach (var character in characters) if (character!=null) character.Died+=OnCharacterDied;
057 |             configured = valid;
058 |             if (!valid) { Status = "角色／身份／输入配置无效，输入关闭"; enabled = false; }
059 |         }
060 |
061 |         private void OnEnable()
062 |         {
063 |             switchIntent.SuppressUntilRelease();
064 |             if (!configured) return;
065 |             switchAction.Enable();
066 |             if (characters[initialIndex]!=null && characters[initialIndex].IsAlive) Select(characters[initialIndex]);
067 |             else SelectSurvivor(false);
068 |         }
069 |
070 |         private void Update()
071 |         {
072 |             if (Current != null && !Current.IsAlive) SelectSurvivor(true);
073 |             else if (Current != null && (!Current.isActiveAndEnabled || Current.DataBus == null)) Revoke("当前角色不可用");
074 |             var held = switchAction != null && switchAction.enabled && switchAction.IsPressed();
075 |             if (switchIntent.Observe(held) && focused && !applicationPaused) SwitchNext();
076 |         }
077 |
078 |         /// <summary>只有Current对应的输入源可消费玩家输入；切换当帧新源禁止采样。</summary>
079 |         public bool IsPlayerInputAllowed(ICharacterInputSource source)
080 |         {
081 |             if (!IsIntentSamplingAllowed) return false;
082 |             for (var i = 0; i < characters.Length; i++)
083 |                 if (characters[i] == Current) return ReferenceEquals(inputs[i], source);
084 |             return false;
085 |         }
086 |
087 |         /// <summary>场景级切人入口；不会取消、复制或重建任一角色的已接受动作。</summary>
088 |         public bool SwitchNext()
089 |         {
090 |             if (!configured || !isActiveAndEnabled || !focused || applicationPaused) return false;
091 |             var index = Array.IndexOf(characters, Current);
092 |             for (var step = 1; step <= characters.Length; step++)
093 |             {
094 |                 var candidate = characters[(index + step) % characters.Length];
095 |                 if (candidate != null && candidate.IsAlive && candidate.isActiveAndEnabled && candidate.DataBus != null)
096 |                 { Select(candidate); return true; }
097 |             }
098 |             Revoke("没有可控制角色");
099 |             return false;
100 |         }
101 |
102 |         private void Select(CharacterRoot next, bool requireMovementRelease=false)
103 |         {
104 |             // 先撤回旧权威并清所有快照，再授予新身份。整帧禁入避免外部晚调用导致同帧双控。
105 |             Current = null;
106 |             ControlRevision++;
107 |             foreach (var input in inputs) if (input != null) input.ResetPlayerInput(requireMovementRelease);
108 |             handoverFrame = Time.frameCount;
109 |             Current = next;
110 |             Status = next == null ? "无实控" : "玩家输入 → " + next.ActorId;
111 |         }
112 |
113 |         private void Revoke(string reason) { Select(null); Status = reason; }
114 |         private void OnCharacterDied(CharacterRoot character)
115 |         { if (bindingsOwned && configured && character==Current) SelectSurvivor(true); }
116 |
117 |         private void SelectSurvivor(bool deathTransfer)
118 |         {
119 |             if (!configured || !isActiveAndEnabled) return;
120 |             var index=Array.IndexOf(characters,Current);
121 |             for (var step=1;step<=characters.Length;step++)
122 |             {
123 |                 var candidate=characters[(index+step)%characters.Length];
124 |                 if (candidate!=null && candidate.IsAlive && candidate.isActiveAndEnabled && candidate.DataBus!=null)
125 |                 { Select(candidate,deathTransfer); return; }
126 |             }
127 |             Select(null,deathTransfer); Status="无可控制角色";
128 |         }
129 |
130 |         /// <summary>显式开发回放：由外部先恢复同一组实体，随后重新选合法存活者；无自动返场。</summary>
131 |         public void RestoreControlForDevelopment()
132 |         {
133 |             if (!configured || !isActiveAndEnabled) return;
134 |             foreach (var candidate in characters)
135 |                 if (candidate!=null && candidate.IsAlive && candidate.isActiveAndEnabled && candidate.DataBus!=null)
136 |                 { Select(candidate,true); return; }
137 |             Select(null,true); Status="无可控制角色";
138 |         }
139 |         private void OnDisable()
140 |         {
141 |             if (switchAction != null) switchAction.Disable();
142 |             switchIntent.SuppressUntilRelease();
143 |             // 未取得绑定所有权的失败实例不能清理其它协调者的快照或重武装状态。
144 |             if (bindingsOwned)
145 |             {
146 |                 if (configured) Revoke("协调者禁用，输入关闭");
147 |                 else foreach (var input in inputs) if (input != null) input.ResetPlayerInput();
148 |             }
149 |         }
150 |         private void OnApplicationFocus(bool value)
151 |         {
152 |             focused = value;
153 |             ControlRevision++;
154 |             switchIntent.SuppressUntilRelease();
155 |             if (bindingsOwned) foreach (var input in inputs) if (input != null) input.ResetPlayerInput();
156 |         }
157 |         private void OnApplicationPause(bool value)
158 |         {
159 |             applicationPaused = value;
160 |             ControlRevision++;
161 |             switchIntent.SuppressUntilRelease();
162 |             if (bindingsOwned) foreach (var input in inputs) if (input != null) input.ResetPlayerInput();
163 |         }
164 |         // 输入仍保留“曾受管理”的关系；本对象销毁后查询失败关闭，不能突然恢复多个玩家源。
165 |         private void OnDestroy()
166 |         {
167 |             if (bindingsOwned) foreach (var character in characters) if (character!=null) character.Died-=OnCharacterDied;
168 |             if (switchAction != null) switchAction.Dispose();
169 |         }
170 |     }
171 | }
```

## StationaryMeleeEnemy

Assets/OSSASTRA/Scripts/Enemies/Runtime/StationaryMeleeEnemy.cs

1–97；SHA256 7C604CE2ACC2760111546FC39F6160B433F4461A36A5C49781E24FDCD3622B2A

```csharp
001 | /*
002 |  * 脚本：StationaryMeleeEnemy
003 |  * 功能：固定站位的真实验证敌人，近距可见目标通过单一动作时钟释放近战。
004 |  * 边界：无寻路/追击/行为树，不直接扣HP、不用开发靶充敌人，不自动返场。
005 |  * 协作：独立Vitals持有生命，复用BasicActionRuntime→CharacterMeleeHit；目标失效/出圈/遮挡具名取消。
006 |  */
007 | using System;
008 | using Ossastra.CharacterCore;
009 | using Ossastra.Characters;
010 | using UnityEngine;
011 | using Root = Ossastra.Characters.CharacterRoot;
012 |
013 | namespace Ossastra.Enemies
014 | {
015 |     [RequireComponent(typeof(CharacterVitals),typeof(CharacterMeleeHit))]
016 |     public sealed class StationaryMeleeEnemy : MonoBehaviour
017 |     {
018 |         [SerializeField] private string actorId="enemy-stationary-01";
019 |         [SerializeField] private Root[] targets=Array.Empty<Root>();
020 |         [SerializeField, Min(.1f)] private float triggerDistance=1.7f, attackInterval=1.8f;
021 |         [SerializeField] private float releaseAt=.5f, moveReadyAt=.6f, attackReadyAt=.9f, finishAt=1.1f;
022 |         [SerializeField, Min(1)] private int damage=20;
023 |         [SerializeField] private bool attacksEnabled=true;
024 |         private CharacterVitals vitals;
025 |         private CharacterMeleeHit melee;
026 |         private BasicActionRuntime runtime;
027 |         private BasicAttackDefinition attack;
028 |         private readonly ControlAuthority authority=new ControlAuthority();
029 |         private Root target;
030 |         private float nextAttack;
031 |         private bool focused=true, applicationPaused;
032 |         public CharacterVitals Vitals => vitals;
033 |         public string LastFact { get; private set; }="Idle";
034 |         public string LastHit => melee==null?"未初始化":melee.LastResult;
035 |         public bool ActionActive => runtime!=null && runtime.IsActive;
036 |         public bool AttacksEnabled { get => attacksEnabled; set { attacksEnabled=value; if (!value) Cancel("development-attacks-disabled"); } }
037 |
038 |         private void Awake()
039 |         {
040 |             vitals=GetComponent<CharacterVitals>(); melee=GetComponent<CharacterMeleeHit>();
041 |             try { attack=new BasicAttackDefinition(releaseAt,moveReadyAt,attackReadyAt,finishAt,damage); runtime=new BasicActionRuntime(actorId); }
042 |             catch (ArgumentException e) { Debug.LogError("Enemy configuration: "+e.Message,this); enabled=false; return; }
043 |             if (!vitals.Initialize(actorId)) { Debug.LogError("Enemy identity invalid",this); enabled=false; return; }
044 |             vitals.Died+=OnDied; nextAttack=Time.time+attackInterval;
045 |         }
046 |
047 |         private bool Eligible(Root candidate)
048 |         {
049 |             if (candidate==null || !candidate.isActiveAndEnabled || !candidate.IsAlive || candidate.Vitals==null || !candidate.Vitals.IsAlive ||
050 |                 !CombatTargetPolicy.CanHit(vitals.Health.Id,vitals.Faction,vitals.IsAlive,candidate.ActorId,candidate.Vitals.Faction,candidate.IsAlive)) return false;
051 |             var offset=candidate.transform.position-transform.position; offset.y=0;
052 |             if (offset.magnitude>triggerDistance) return false;
053 |             var start=transform.position+Vector3.up*.8f;
054 |             var end=candidate.transform.position+Vector3.up*.8f;
055 |             // 不借目标半径隔墙感知；忽略自身/该目标的碰撞体，其余实体为遮挡。
056 |             foreach (var hit in Physics.RaycastAll(start,end-start,Vector3.Distance(start,end),~0,QueryTriggerInteraction.Ignore))
057 |                 if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(candidate.transform)) return false;
058 |             return true;
059 |         }
060 |
061 |         private void Update()
062 |         {
063 |             if (vitals==null || !vitals.IsAlive || runtime==null || !attacksEnabled || !focused || applicationPaused) return;
064 |             if (runtime.IsActive && !Eligible(target)) Cancel("target-dead-out-of-range-or-occluded");
065 |             if (!runtime.IsActive && Time.time>=nextAttack)
066 |             {
067 |                 target=null; var nearest=float.MaxValue;
068 |                 foreach (var candidate in targets)
069 |                     if (Eligible(candidate))
070 |                     { var distance=(candidate.transform.position-transform.position).sqrMagnitude; if (distance<nearest) { nearest=distance; target=candidate; } }
071 |                 if (target!=null)
072 |                 {
073 |                     var request=runtime.Request(attack,"stationary-enemy",authority);
074 |                     if (request.Accepted) { LastFact="Accepted #"+request.Fact.ActionId.ToString("N"); nextAttack=Time.time+attackInterval; }
075 |                 }
076 |             }
077 |             foreach (var fact in runtime.Advance(Time.deltaTime,false))
078 |             {
079 |                 LastFact=fact.Kind+" #"+fact.ActionId.ToString("N");
080 |                 if (fact.Release!=null)
081 |                 {
082 |                     var direction=target.transform.position-transform.position; direction.y=0;
083 |                     melee.Resolve(fact.Release,direction.normalized);
084 |                 }
085 |             }
086 |         }
087 |         private void Cancel(string reason)
088 |         { var fact=runtime==null?null:runtime.Cancel(reason); if (fact!=null) LastFact="Cancelled "+reason+" #"+fact.ActionId.ToString("N"); target=null; }
089 |         private void OnDied(HitOutcome outcome) { Cancel("death:"+outcome.ActorId); }
090 |         private void OnDisable() { Cancel("enemy-disabled"); }
091 |         private void OnApplicationFocus(bool value) { focused=value; if (!value) Cancel("focus-lost"); }
092 |         private void OnApplicationPause(bool value) { applicationPaused=value; if (value) Cancel("application-paused"); }
093 |         private void OnDestroy() { if (vitals!=null) vitals.Died-=OnDied; }
094 |         public void RestoreForDevelopment() { Cancel("development-reset"); vitals.RestoreForDevelopment(); nextAttack=Time.time+attackInterval; }
095 |         private void OnDrawGizmosSelected() { Gizmos.color=Color.red; Gizmos.DrawWireSphere(transform.position+Vector3.up*.8f,triggerDistance); }
096 |     }
097 | }
```

## CombatDeathDebugView

Assets/OSSASTRA/Scripts/Development/Combat/CombatDeathDebugView.cs

1–70；SHA256 32EA3317B36D1B04E6C8857E493E39629CAFA8B11CAD9EDB0D5C3236EA6AAD0D

```csharp
001 | /*
002 |  * 脚本：CombatDeathDebugView
003 |  * 功能：开发战斗读数、正式结算入口注入受击和显式同实体重置，便于复验死亡边界。
004 |  * 边界：Development Only，不代表自动返场；不直接写HP私有字段、不伪造UI成功。
005 |  * 协作：注入独立开发来源的Released→Health→目标通知；恢复顺序为取消/恢复原对象/重选实控。
006 |  */
007 | using System.Linq;
008 | using Ossastra.CharacterCore;
009 | using Ossastra.Characters;
010 | using Ossastra.Enemies;
011 | using UnityEngine;
012 | namespace Ossastra.Development
013 | {
014 |     public sealed class CombatDeathDebugView : MonoBehaviour
015 |     {
016 |         [SerializeField] private CharacterControlCoordinator coordinator;
017 |         [SerializeField] private CompanionFollowDirector director;
018 |         [SerializeField] private StationaryMeleeEnemy enemy;
019 |         private Vector3[] spawns;
020 |         private static readonly Rect panel=new Rect(490,12,650,370);
021 |         private void Start() { spawns=coordinator.Characters.Select(c=>c.transform.position).ToArray(); Register(true); }
022 |         private void Register(bool add)
023 |         {
024 |             if (coordinator==null) return;
025 |             foreach (var c in coordinator.Characters)
026 |             { var input=c==null?null:c.GetComponent<CharacterInputHandler>(); if (input==null) continue; if (add) input.RegisterPointerRegion(this,panel); else input.RemovePointerRegion(this); }
027 |         }
028 |         private void OnDisable() { Register(false); }
029 |         private void OnEnable() { if (spawns!=null) Register(true); }
030 |
031 |         public static void ApplyDevelopmentHit(CharacterVitals target, int damage)
032 |         {
033 |             if (target==null || !target.IsAlive || damage<=0) return;
034 |             var runtime=new BasicActionRuntime("development-hit-probe");
035 |             runtime.Request(new BasicAttackDefinition(0,0,0,.01,damage),"development-only",new ControlAuthority());
036 |             var release=runtime.Advance(.001,false).Single(f=>f.Release!=null).Release;
037 |             foreach (var outcome in release.Resolve(new[]{target.Health})) target.OnHitResolved(outcome);
038 |         }
039 |         public void RestartForDevelopment()
040 |         {
041 |             if (spawns==null) return;
042 |             director.enabled=false;
043 |             for (var i=0;i<coordinator.Characters.Count;i++)
044 |             {
045 |                 var c=coordinator.Characters[i]; c.CancelAction("development-reset"); c.SetPaused(false);
046 |                 c.Vitals.RestoreForDevelopment(); c.GetComponent<CharacterMovement>().Teleport(spawns[i]);
047 |             }
048 |             enemy.RestoreForDevelopment(); coordinator.RestoreControlForDevelopment(); director.enabled=true;
049 |         }
050 |         private void OnGUI()
051 |         {
052 |             if (coordinator==null || enemy==null) return;
053 |             GUILayout.BeginArea(panel,GUI.skin.box);
054 |             GUILayout.Label("Development Only · 双向攻防 / 真死亡 · 无正式返场");
055 |             GUILayout.Label("Current: "+(coordinator.Current==null?"无可控制角色":coordinator.Current.ActorId));
056 |             foreach (var c in coordinator.Characters)
057 |             {
058 |                 var v=c.Vitals; GUILayout.Label(c.ActorId+" HP "+v.Health.Current+"/"+v.Health.Maximum+" "+(v.IsAlive?"Alive":"Dead")+" "+c.EffectiveIntentKind+" | "+c.LastFact);
059 |                 GUILayout.Label(v.LastResult);
060 |                 if (GUILayout.Button("合法受击检查："+c.ActorId+" -20HP")) ApplyDevelopmentHit(v,20);
061 |                 if (GUILayout.Button("死亡边界："+c.ActorId+" 致死一击")) ApplyDevelopmentHit(v,v.Health.Maximum);
062 |             }
063 |             GUILayout.Label("Enemy HP "+enemy.Vitals.Health.Current+"/"+enemy.Vitals.Health.Maximum+" | "+enemy.LastFact);
064 |             GUILayout.Label(enemy.LastHit);
065 |             if (GUILayout.Button("敌人攻击："+(enemy.AttacksEnabled?"开启（点击关闭）":"关闭（点击开启）"))) enemy.AttacksEnabled=!enemy.AttacksEnabled;
066 |             if (GUILayout.Button("Development Only：同实体重新开始测试")) RestartForDevelopment();
067 |             GUILayout.EndArea();
068 |         }
069 |     }
070 | }
```
