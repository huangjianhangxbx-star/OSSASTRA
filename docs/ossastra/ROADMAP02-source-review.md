# ROADMAP02 变化源码集中审查
基准9e10cb3；以下为最终实际源码，不代表人工架构批准。

## Assets/OSSASTRA/Scripts/Animation/Core/ClipFallback.cs
SHA256: 95BBB98B4AD356CD46ABB657FB182142A2D66F939CE5214C4080E1F5BA94338E

```csharp
   1 | /* 片段选择纯函数：校验实际资源，缺失时只回退已存在Idle，不编造播放成功。 */
   2 | using System;
   3 | namespace Ossastra.AnimationCore
   4 | {
   5 |     public static class ClipFallback
   6 |     {
   7 |         public static string Resolve(string requested, string idle, Func<string,bool> exists)
   8 |         {
   9 |             if(!string.IsNullOrEmpty(requested) && exists(requested)) return requested;
  10 |             return !string.IsNullOrEmpty(idle) && exists(idle) ? idle : null;
  11 |         }
  12 |     }
  13 | }
```

## Assets/OSSASTRA/Scripts/Animation/Runtime/EntitySpinePresenter.cs
SHA256: 46C046D9C01E79F46381FE6C1DB11472E88DF188DA1D799046A3731F9132DCDD

```csharp
   1 | /* 中立Spine播放口：只消费语义、身份、代际和朝向；不引用角色根、输入、HP或动作运行时。
   2 |  * 单轨按ActionId去重，映射不存在则明确Idle回退；暂停仅控制自身播放速度。
   3 |  */
   4 | using System;
   5 | using Ossastra.AnimationCore;
   6 | using SemanticIntent=Ossastra.AnimationCore.AnimationIntent;
   7 | using PlaybackIdentity=Ossastra.AnimationCore.AnimationPlaybackIdentity;
   8 | using UnityEngine;
   9 | using Spine.Unity;
  10 | namespace Ossastra.AnimationPresentation
  11 | {
  12 |     public interface IEntityPresentation { void Present(); }
  13 |     [DisallowMultipleComponent]
  14 |     public sealed class EntitySpinePresenter : MonoBehaviour
  15 |     {
  16 |         [SerializeField] private SkeletonAnimation skeleton;
  17 |         [SerializeField] private string idle="idle1_1";
  18 |         [SerializeField] private AnimationClipMap[] catalog=Array.Empty<AnimationClipMap>();
  19 |         private readonly PlaybackIdentity identity=new PlaybackIdentity();
  20 |         private bool initialized;
  21 |         private float originalScale=1,originalSpeed=1;
  22 |         public string CurrentClip {get;private set;}
  23 |         public string FallbackReason {get;private set;}
  24 |         public AnimationSemantic Semantic {get;private set;}
  25 |         public int CommitCount {get;private set;}
  26 |         public void Present(SemanticIntent intent,float facing,bool paused)
  27 |         {
  28 |             if(!isActiveAndEnabled || skeleton==null || skeleton.skeletonDataAsset==null)return;
  29 |             if(!initialized)
  30 |             {
  31 |                 skeleton.Initialize(false);if(skeleton.Skeleton==null)return;
  32 |                 originalScale=Mathf.Abs(skeleton.Skeleton.ScaleX);originalSpeed=skeleton.timeScale;initialized=true;
  33 |             }
  34 |             AnimationClipMap chosen=null;
  35 |             foreach(var entry in catalog)if(entry!=null&&entry.semantic==intent.Semantic){chosen=entry;break;}
  36 |             var requested=chosen==null?(intent.Semantic==AnimationSemantic.Idle?idle:null):chosen.clip;
  37 |             var clip=ClipFallback.Resolve(requested,idle,s=>skeleton.Skeleton.Data.FindAnimation(s)!=null);
  38 |             FallbackReason=clip==null?"无可用片段":clip!=requested?"缺少 "+intent.Semantic+"，回退Idle":"";
  39 |             if(clip==null)return;
  40 |             skeleton.timeScale=paused?0:originalSpeed;
  41 |             skeleton.Skeleton.ScaleX=originalScale*(facing<0?-1:1);
  42 |             if(identity.ShouldCommit(clip,intent))
  43 |             {var track=skeleton.AnimationState.SetAnimation(0,clip,chosen==null||clip!=requested||chosen.loop);track.MixDuration=chosen==null?.08f:chosen.mixDuration;CommitCount++;}
  44 |             CurrentClip=clip;Semantic=intent.Semantic;
  45 |         }
  46 |         private void OnDisable(){if(skeleton!=null&&initialized)skeleton.timeScale=originalSpeed;}
  47 |     }
  48 | }
```

## Assets/OSSASTRA/Scripts/Characters/Core/CompanionCombatPolicy.cs
SHA256: 6144CB52FBD59696829EEF81D0D97DF78525637C36899093C2E82F339020EBA9

```csharp
   1 | /* 伙伴战斗纯决策门：只返回候选，不持有动作时钟、生命、输入或移动写入权。 */
   2 | namespace Ossastra.CharacterCore
   3 | {
   4 |     public enum CompanionCombatDecision { Inactive, Follow, Approach, Wait, Attack }
   5 |     public static class CompanionCombatPolicy
   6 |     {
   7 |         public static CompanionCombatDecision Decide(bool session, bool current, bool engaged, bool capable,
   8 |             bool target, bool insideAnchor, bool inRange, bool ready, bool blocked)
   9 |         {
  10 |             if (!session || current || !engaged || !capable) return CompanionCombatDecision.Inactive;
  11 |             if (!target || !insideAnchor || blocked) return CompanionCombatDecision.Follow;
  12 |             if (!inRange) return CompanionCombatDecision.Approach;
  13 |             return ready ? CompanionCombatDecision.Attack : CompanionCombatDecision.Wait;
  14 |         }
  15 |     }
  16 | }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterIntentSelector.cs
SHA256: F72493EF267ED795D4C9895AA2771466AEE0213E20C5AA1000DFF32A2C74681A

```csharp
   1 | /*
   2 |  * 脚本：CharacterIntentSelector
   3 |  * 功能：在Root惰性采样时，只从玩家、伙伴、空意图中选择一个有效Frame。
   4 |  * 边界：不决定距离、不采InputAction、不写运动/动作；未绑定跟随时委托原玩家输入。
   5 |  * 协作：Root与Bus共用同一选择器；会话代际或当帧变化立刻使旧快照失效。
   6 |  */
   7 | using Ossastra.CharacterCore;
   8 | using UnityEngine;
   9 |
  10 | namespace Ossastra.Characters
  11 | {
  12 |     public sealed class CharacterIntentSelector : ICharacterInputSource
  13 |     {
  14 |         private readonly CharacterRoot character;
  15 |         private readonly ICharacterInputSource player;
  16 |         private ICharacterControlSession session;
  17 |         private ICompanionIntentSource companion;
  18 |         private bool managed;
  19 |         private CharacterInputFrame frame;
  20 |         private CharacterIntentKind kind;
  21 |         private int sampledFrame = -1, sampledRevision;
  22 |
  23 |         public CharacterIntentSelector(CharacterRoot character, ICharacterInputSource player)
  24 |         { this.character=character; this.player=player; }
  25 |         public bool IsConfigured => player != null && player.IsConfigured;
  26 |         private bool ValidSample => sampledFrame == Time.frameCount && character != null && character.IsAlive && character.isActiveAndEnabled &&
  27 |             (!managed || (Alive(session) && session.IsIntentSamplingAllowed && session.ControlRevision == sampledRevision)) &&
  28 |             // 即使旧场未绑定Director，玩家适配器被撤权也必须立即使外层帧归零。
  29 |             (kind != CharacterIntentKind.Player || !(player is ICharacterInputAvailability availability) || availability.HasPlayerInput) &&
  30 |             (kind != CharacterIntentKind.Companion || (Alive(companion) && companion.IsAvailable));
  31 |         public CharacterInputFrame CurrentFrame => ValidSample ? frame : default;
  32 |         public CharacterIntentKind EffectiveKind => ValidSample ? kind : CharacterIntentKind.Neutral;
  33 |
  34 |         public bool CanBind(ICharacterControlSession authority, ICompanionIntentSource source)
  35 |             => Alive(authority) && Alive(source) && (!managed || !Alive(companion) || ReferenceEquals(companion,source));
  36 |
  37 |         /// <summary>只允许同一伙伴提供者重绑或已销毁提供者被替代；不抢占活owner。</summary>
  38 |         public bool Bind(ICharacterControlSession authority, ICompanionIntentSource source)
  39 |         {
  40 |             if (!CanBind(authority,source)) return false;
  41 |             session=authority; companion=source; managed=true; Clear(); return true;
  42 |         }
  43 |
  44 |         public void Clear(ICompanionIntentSource owner = null)
  45 |         {
  46 |             if (owner != null && !ReferenceEquals(owner,companion)) return;
  47 |             frame=default; kind=CharacterIntentKind.Neutral; sampledFrame=-1;
  48 |         }
  49 |
  50 |         public CharacterInputFrame Read()
  51 |         {
  52 |             Clear();
  53 |             if (character == null || !character.IsAlive || !character.isActiveAndEnabled) return default;
  54 |             var selectedKind=CharacterIntentPolicy.Select(managed, Alive(session) && session.IsIntentSamplingAllowed,
  55 |                 Alive(session) && session.Current == character, Alive(companion) && companion.IsAvailable);
  56 |             if (selectedKind == CharacterIntentKind.Player) frame=player.Read();
  57 |             else if (selectedKind == CharacterIntentKind.Companion)
  58 |             {
  59 |                 // 伙伴即使给了其它字段，也只准传递move；不能借AI帧自动攻击、取消或暂停。
  60 |                 var candidate=character.CombatMove != null && character.CombatMove.TryGetCombatMove(out var battleMove)
  61 |                     ? new CharacterInputFrame { move=battleMove } : companion.ReadCompanionIntent(character,Time.deltaTime);
  62 |                 frame=new CharacterInputFrame { move=candidate.move };
  63 |             }
  64 |             // 提供者发现新目标会先撤回旧帧；只有本次读取完成后才发布新来源身份。
  65 |             kind=selectedKind;
  66 |             sampledFrame=Time.frameCount;
  67 |             sampledRevision=Alive(session) ? session.ControlRevision : 0;
  68 |             return CurrentFrame;
  69 |         }
  70 |
  71 |         private static bool Alive(object value) => value != null && (!(value is Object unityObject) || unityObject != null);
  72 |     }
  73 | }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterRoot.cs
SHA256: 200A534532EFDE5E404D0CCCE7E73E1139294B91BE97D2F9F70E8B1BB7995FFC

```csharp
   1 | /*
   2 |  * 脚本：CharacterRoot
   3 |  * 功能：组织单角色的接口接线、输入、动作事实、运动和表现。
   4 |  * 边界：持有本角色运行时；只读生命资格，不实现HP写入、设备绑定或开发GUI。
   5 |  * 协作：初始化可选本体Follow/Posture；显式按输入、架势、朝向、动作、运动、表现推进。
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
  29 |         [SerializeField] private CombatPosture posture;
  30 |         private ICombatLifeState lifeState;
  31 |         private CompanionCombatBrain combatBrain;
  32 |         private CombatVitals autonomousTarget;
  33 |         private bool autonomousAction;
  34 |         public event Action<ActionFact> ActionObserved;
  35 |         public double SimulationTime => runtime == null ? 0 : runtime.SimulationTime;
  36 |         public ICompanionMoveCandidate CombatMove => combatBrain;
  37 |         [Header("Prototype timing — not final balance")]
  38 |         [SerializeField] private float releaseAt = .3f, moveReadyAt = .5f, attackReadyAt = .8f, finishAt = 1.1f;
  39 |         [SerializeField, Min(1)] private int damage = 12;
  40 |         [SerializeField, Min(0)] private float postureImpact;
  41 |         [SerializeField] private bool verboseTrace;
  42 |         private readonly ControlAuthority authority = new ControlAuthority();
  43 |         private readonly AttackIntent intent = new AttackIntent();
  44 |         private ICharacterInputSource inputSource;
  45 |         private CharacterIntentSelector intentSelector;
  46 |         private ICharacterMovement mover;
  47 |         private ICharacterOrientation orienter;
  48 |         private ICharacterPresentation presentation;
  49 |         private ICombatHitResolver hitResolver;
  50 |         private BasicActionRuntime runtime;
  51 |         private BasicAttackDefinition definition;
  52 |         private BasicAttackDefinition secondDefinition;
  53 |         private BasicChainState chain;
  54 |         private BasicChainProfile chainProfile;
  55 |         private ICombatHitReceiptSource hitReceipts;
  56 |         public int AttackStage { get; private set; }
  57 |         public int ReleaseCount { get; private set; }
  58 |         public bool HasBufferedAttack => chain!=null && chain.HasBuffer;
  59 |         public double ComboWindowUntil => chain==null?-1:chain.WindowUntil;
  60 |         private bool ready, focused = true, paused, applicationPaused;
  61 |         private string lastFact = "Idle";
  62 |         private object respawnOwner;
  63 |         private int lifeGeneration, animationGeneration;
  64 |         public int LifeGeneration => lifeGeneration;
  65 |         public bool IsSimulationSuspended => !focused || IsPaused;
  66 |         public CharacterDataBus DataBus { get; private set; }
  67 |         public string ActorId => actorId;
  68 |         public string LastFact => lastFact;
  69 |         public string LastActionSource { get; private set; } = "none";
  70 |         public bool IsPaused => paused || applicationPaused;
  71 |         public bool ActionActive => DataBus != null && DataBus.ActionActive;
  72 |         public bool IsAlive => lifeState==null || lifeState.IsAlive;
  73 |         public bool CanSimulateIntent => ready && IsAlive && isActiveAndEnabled && focused && !IsPaused && (posture == null || !posture.IsStaggered);
  74 |         public CombatVitals Vitals => vitals;
  75 |         public event Action<CharacterRoot> Died;
  76 |         public CharacterFollow Follow => follow;
  77 |         public CharacterIntentKind EffectiveIntentKind => intentSelector == null ? CharacterIntentKind.Neutral : intentSelector.EffectiveKind;
  78 |
  79 |         public bool CanBindCompanionIntent(ICharacterControlSession session, ICompanionIntentSource source)
  80 |             => ready && follow != null && ReferenceEquals(source,follow) && intentSelector != null && intentSelector.CanBind(session,source);
  81 |         public bool BindCompanionIntent(ICharacterControlSession session, ICompanionIntentSource source)
  82 |             => CanBindCompanionIntent(session,source) && intentSelector.Bind(session,source);
  83 |         public void ClearCompanionIntent(ICompanionIntentSource owner)
  84 |         { if (intentSelector != null) intentSelector.Clear(owner); }
  85 |
  86 |         internal bool BindRespawnOwner(object owner)
  87 |         {
  88 |             if(owner==null || (respawnOwner!=null && !ReferenceEquals(respawnOwner,owner))) return false;
  89 |             respawnOwner=owner; return true;
  90 |         }
  91 |         internal void UnbindRespawnOwner(object owner)
  92 |         { if(ReferenceEquals(respawnOwner,owner)) respawnOwner=null; }
  93 |         internal bool CanRespawn(object owner) => ready && isActiveAndEnabled && !IsAlive &&
  94 |             vitals!=null && vitals.isActiveAndEnabled && ReferenceEquals(respawnOwner,owner);
  95 |         internal bool Respawn(object owner, Vector3 position)
  96 |         {
  97 |             if(!CanRespawn(owner)) return false;
  98 |             CancelAction("scene-respawn");
  99 |             if(follow!=null) follow.ResetIntent();
 100 |             if(intentSelector!=null) intentSelector.Clear();
 101 |             input.ResetPlayerInput(true);
 102 |             orientation.ResetOrientation();
 103 |             movement.Teleport(position);
 104 |             if(posture!=null) posture.ResetState();
 105 |             if(!vitals.RestoreAfterDeath()) return false;
 106 |             lifeGeneration++;
 107 |             return true;
 108 |         }
 109 |
 110 |         private void Awake()
 111 |         {
 112 |             if (viewCamera == null) viewCamera = Camera.main;
 113 |             if (vitals!=null)
 114 |             {
 115 |                 if (!vitals.Initialize(actorId)) { Debug.LogError("Character vitals identity/configuration invalid",this); enabled=false; return; }
 116 |                 lifeState=vitals; vitals.Died+=OnDied;
 117 |             }
 118 |             if (movement == null || orientation == null || view == null || melee == null || input == null || viewCamera == null)
 119 |             { Debug.LogError("Character requires registered input/movement/orientation/view/melee/camera", this); enabled = false; return; }
 120 |             // Inspector保留具体组件槽；运行期通过窄接口消费，不反向依赖开发GUI或测试靶。
 121 |             // 每角色只有这一条有效Frame入口；没有Director绑定时保持原玩家适配器行为。
 122 |             intentSelector = new CharacterIntentSelector(this,input);
 123 |             inputSource = intentSelector;
 124 |             combatBrain=GetComponent<CompanionCombatBrain>();
 125 |             if(combatBrain!=null) combatBrain.Initialize(this);
 126 |             mover = movement;
 127 |             orienter = orientation;
 128 |             presentation = view;
 129 |             hitResolver = melee;
 130 |             if (!inputSource.IsConfigured) { Debug.LogError("Character actions are not configured", this); enabled = false; return; }
 131 |             try
 132 |             {
 133 |                 definition = new BasicAttackDefinition(releaseAt, moveReadyAt, attackReadyAt, finishAt, damage, postureImpact);
 134 |                 runtime = new BasicActionRuntime(actorId);
 135 |                 chainProfile=GetComponent<BasicChainProfile>();
 136 |                 // Root的早期Awake可能先于Profile.OnEnable；此处读取序列化enabled，运行期再查isActiveAndEnabled。
 137 |                 if(chainProfile!=null && chainProfile.enabled)
 138 |                 {
 139 |                     definition=chainProfile.stage1.Freeze(); secondDefinition=chainProfile.stage2.Freeze();
 140 |                     chain=chainProfile.CreateState(); hitReceipts=melee; hitReceipts.Resolved+=OnHitReceipt;
 141 |                 }
 142 |             }
 143 |             catch (ArgumentException error) { Debug.LogError("Character configuration: " + error.Message, this); enabled = false; return; }
 144 |             // 每角色独立Bus只读取原所有者；只有确需Bus的运动和表现执行初始化。
 145 |             DataBus = new CharacterDataBus(actorId, inputSource, mover, orienter, runtime, authority);
 146 |             ready = InitializeSubsystems(DataBus, mover, presentation);
 147 |             if (!ready) { Debug.LogError("Character subsystem initialization failed", this); enabled = false; }
 148 |             // 可选能力只接受本角色初始化；无组件/无assignment的旧场保持普通玩家行为。
 149 |             if (follow != null && !follow.Initialize(this)) Debug.LogWarning("Character follow unavailable: configuration or owner invalid", this);
 150 |             if (posture != null)
 151 |             {
 152 |                 if (!posture.Initialize(vitals, this)) { Debug.LogError("Character posture configuration invalid", this); enabled = false; return; }
 153 |                 posture.Broken += OnPostureBroken;
 154 |                 posture.Recovered += OnPostureRecovered;
 155 |             }
 156 |         }
 157 |
 158 |         private static bool InitializeSubsystems(CharacterDataBus dataBus, params ICharacterSubsystem[] subsystems)
 159 |         {
 160 |             foreach (var subsystem in subsystems) if (subsystem == null || !subsystem.Initialize(dataBus)) return false;
 161 |             return true;
 162 |         }
 163 |
 164 |         private void Update()
 165 |         {
 166 |             if (!ready) return;
 167 |             if (!IsAlive) { CommitPresentation(); return; }
 168 |             // 本体显式调度入口：以下阶段在一次Update中按书写顺序执行。
 169 |             // DefaultExecutionOrder只约束跨组件先后，不能替代这里的模块调用顺序。
 170 |             // 1. 采唯一意图源并处理暂停/取消；不由各子模块另开输入Update。
 171 |             // 选择器只采一个移动源；战斗能力另交窄动作候选，切人屏障为空帧，动作时钟仍继续。
 172 |             combatBrain?.Sample(runtime.SimulationTime, Time.deltaTime);
 173 |             inputSource.Read();
 174 |             var frame = DataBus.Input;
 175 |             if(chain!=null && (!input.HasPlayerInput || IsPaused || !focused || chainProfile==null || !chainProfile.isActiveAndEnabled)) chain.Clear();
 176 |             if (frame.pause) SetPaused(!paused);
 177 |             if (frame.cancel) CancelAction("player-cancel");
 178 |             // 2. 同一根时钟推进架势；主动作暂停自然恢复，暂停/失焦完全停钟。
 179 |             // 在Request前解除到期的本owner禁令；不会回滚其他根已释放的命中。
 180 |             if (posture != null) posture.Tick(IsPaused || !focused ? 0 : Time.deltaTime, runtime.IsActive);
 181 |             // 3. 先解算朝向，攻击释放才能读取本帧鼠标/键盘方向。
 182 |             var canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
 183 |             if (focused && !IsPaused)
 184 |                 orienter.ResolveDirections(frame.move, PointerDirection(frame), frame.hasDirectionalAim ? frame.aimDirection : Vector2.zero, canMove);
 185 |             // 4. 观察按键边沿，再由Runtime的暂停/权限/Ready资格决定是否接受动作。
 186 |             if (intent.Observe(frame.attackHeld) && focused && !frame.attackPointerBlocked)
 187 |             {
 188 |                 var stage=chain==null?1:chain.Request(runtime.SimulationTime,runtime.AttackReady,
 189 |                     input.HasPlayerInput && !IsPaused && authority.Allows(ControlChannel.Attack) && chainProfile!=null && chainProfile.isActiveAndEnabled);
 190 |                 if(stage>0) RequestStage(stage);
 191 |             }
 192 |             // 4b. 窄自治请求与玩家边沿互斥；仅Stage1，不通过输入帧注入攻击/暂停。
 193 |             if(!input.HasPlayerInput && CanSimulateIntent && combatBrain!=null &&
 194 |                 ((ICompanionActionCandidate)combatBrain).TryGetAttackTarget(out var aiTarget))
 195 |                 RequestStage(1,aiTarget);
 196 |             if(autonomousAction && runtime.IsActive)
 197 |             {
 198 |                 if(combatBrain==null || !combatBrain.IsAcceptedTargetValid(autonomousTarget)) CancelAction("ai-target-invalid");
 199 |                 else orienter.OrientTowards(autonomousTarget.transform.position);
 200 |             }
 201 |             // 5. 唯一动作时钟推进与Release命中。Accepted不扣血，切人不重发事实。
 202 |             foreach (var fact in runtime.Advance(Time.deltaTime, IsPaused || !focused))
 203 |             {
 204 |                 Observe(fact);
 205 |                 if (fact.Release != null)
 206 |                 {
 207 |                     ReleaseCount++;
 208 |                     // 在释放当帧按值采样方向；不是接受动作时锁定，前摇中仍可重新瞄准。
 209 |                     var releaseDirection = orienter.AttackDirection;
 210 |                     orienter.FaceAttackDirection();
 211 |                     hitResolver.Resolve(fact.Release, releaseDirection);
 212 |                 }
 213 |             }
 214 |             if(chain!=null && input.HasPlayerInput && !IsPaused && focused && authority.Allows(ControlChannel.Attack))
 215 |             {
 216 |                 var bufferedStage=chain.Poll(runtime.SimulationTime,runtime.AttackReady);
 217 |                 if(bufferedStage>0) RequestStage(bufferedStage);
 218 |             }
 219 |             // 6. 动作/命中可能改变MoveReady或架势权限，重新查询后才执行真实移动。
 220 |             canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
 221 |             mover.Tick(canMove, IsPaused || !focused ? 0 : Time.deltaTime);
 222 |             // 7. 最后提交表现，Run读取已完成的实际位移。
 223 |             // 当前没有本体IK/骨骼后处理，故不虚设LateUpdate；Spine内部播放仍由插件驱动。
 224 |             CommitPresentation();
 225 |         }
 226 |
 227 |         // 7. Animation Intent → One Commit；无子模块Update争抢主轨。
 228 |         private void CommitPresentation()
 229 |         {
 230 |             (presentation as Ossastra.AnimationPresentation.ICharacterAnimationFacts)?.CollectFacts(IsAlive, posture!=null && posture.IsStaggered, vitals!=null && vitals.IsHitReacting, animationGeneration, AttackStage);
 231 |             presentation.SetPaused(IsPaused || !focused);
 232 |             presentation.Present();
 233 |         }
 234 |         private Vector3? PointerDirection(CharacterInputFrame frame)
 235 |         {
 236 |             if (!frame.hasPointer) return null;
 237 |             var plane = new Plane(Vector3.up, transform.position);
 238 |             var ray = viewCamera.ScreenPointToRay(frame.pointer);
 239 |             if (!plane.Raycast(ray, out var distance)) return null;
 240 |             var direction = ray.GetPoint(distance) - transform.position;
 241 |             direction.y = 0;
 242 |             return direction.sqrMagnitude < .0001f ? (Vector3?)null : direction;
 243 |         }
 244 |
 245 |         private void Observe(ActionFact fact)
 246 |         {
 247 |             LastActionSource=fact.Source;
 248 |             lastFact = fact.Kind + " @" + fact.At.ToString("F3") + " " + fact.ActionId.ToString("N").Substring(0, 8);
 249 |             if (fact.Reason != null) lastFact += " " + fact.Reason;
 250 |             if (verboseTrace) Debug.Log("Character " + lastFact, this);
 251 |             ActionObserved?.Invoke(fact);
 252 |         }
 253 |         private void RequestStage(int stage, CombatVitals aiTarget = null)
 254 |         {
 255 |             var selected=stage==2?secondDefinition:definition;
 256 |             var request=runtime.Request(selected,aiTarget==null?"player-input:Stage"+stage:"companion:Stage1",authority,IsPaused);
 257 |             if(request.PreviousTerminal!=null) Observe(request.PreviousTerminal);
 258 |             if(request.Accepted)
 259 |             {
 260 |                 AttackStage=stage; autonomousAction=aiTarget!=null; autonomousTarget=aiTarget;
 261 |                 if(autonomousAction) { chain?.Clear(); combatBrain.OnAccepted(); orienter.OrientTowards(aiTarget.transform.position); }
 262 |                 else chain?.Accepted(stage,request.Fact.ActionId,runtime.SimulationTime,selected.AttackReadyAt);
 263 |                 Observe(request.Fact); orienter.FaceAttackDirection();
 264 |             }
 265 |             else lastFact="Rejected: "+request.Reason;
 266 |         }
 267 |         private void OnHitReceipt(Guid id,bool applied){if(!autonomousAction)chain?.Hit(id,applied);}
 268 |
 269 |         public void CancelAction(string reason)
 270 |         {
 271 |             animationGeneration++; autonomousAction=false; autonomousTarget=null; combatBrain?.ResetCandidate();
 272 |             chain?.Clear();
 273 |             // 取消必须具名；抑制仍按住的输入，不能把下一帧误判为新请求。
 274 |             intent.SuppressUntilRelease();
 275 |             var terminal = runtime == null ? null : runtime.Cancel(reason);
 276 |             if (terminal != null) Observe(terminal);
 277 |         }
 278 |
 279 |         private void OnPostureBroken(string cause)
 280 |         {
 281 |             authority.Block(posture, ControlChannel.Move | ControlChannel.Attack);
 282 |             CancelAction("posture-broken:" + cause);
 283 |             if (follow != null) follow.ResetIntent();
 284 |         }
 285 |         private void OnPostureRecovered()
 286 |         {
 287 |             authority.Release(posture);
 288 |             intent.SuppressUntilRelease();
 289 |             if (follow != null) follow.ResetIntent();
 290 |         }
 291 |
 292 |         private void OnDied(HitOutcome outcome)
 293 |         {
 294 |             // HP已由结算归零；这里仅终止本体未释放动作/有效帧，再交唯一会话处理实控。
 295 |             CancelAction("death:"+outcome.ActorId+":"+outcome.ActionId.ToString("N"));
 296 |             if (follow!=null) follow.ResetIntent();
 297 |             if (intentSelector!=null) intentSelector.Clear();
 298 |             Died?.Invoke(this);
 299 |         }
 300 |
 301 |         public void SetPaused(bool value) { paused = value; chain?.Clear(); intent.SuppressUntilRelease(); if (follow != null) follow.ResetIntent(); }
 302 |         public void SetAttackRestriction(object owner, bool restricted, string reason = "attack-restricted")
 303 |         {
 304 |             if (restricted) { authority.Block(owner, ControlChannel.Attack); CancelAction(reason); }
 305 |             else authority.Release(owner);
 306 |         }
 307 |
 308 |         private void OnApplicationFocus(bool hasFocus)
 309 |         { focused = hasFocus; if (follow != null) follow.ResetIntent(); if (!hasFocus) CancelAction("focus-lost"); else intent.SuppressUntilRelease(); }
 310 |         private void OnApplicationPause(bool value)
 311 |           { applicationPaused = value; if(value)chain?.Clear(); if (follow != null) follow.ResetIntent(); if (value) intent.SuppressUntilRelease(); }
 312 |         private void OnEnable() { intent.SuppressUntilRelease(); if (intentSelector != null) intentSelector.Clear(); }
 313 |         private void OnDisable() { CancelAction("disabled"); if (posture != null) posture.ResetState(); authority.Release(posture); if (follow != null) follow.ResetIntent(); if (intentSelector != null) intentSelector.Clear(); if (ready && presentation != null) presentation.SetPaused(false); }
 314 |         private void OnDestroy()
 315 |         {
 316 |             if(hitReceipts!=null) hitReceipts.Resolved-=OnHitReceipt;
 317 |             if (vitals!=null) vitals.Died-=OnDied;
 318 |             if (posture != null) { posture.Broken -= OnPostureBroken; posture.Recovered -= OnPostureRecovered; }
 319 |             authority.Release(posture);
 320 |             // 终止本角色动作后解除本对象引用；不清其它角色或场景控制权威。
 321 |             CancelAction("destroyed");
 322 |             ready = false;
 323 |             DataBus = null;
 324 |             runtime = null;
 325 |             inputSource = null;
 326 |             if (intentSelector != null) intentSelector.Clear();
 327 |             intentSelector = null;
 328 |             mover = null;
 329 |             orienter = null;
 330 |             presentation = null;
 331 |             hitResolver = null;
 332 |         }
 333 |     }
 334 | }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/CompanionCombatBrain.cs
SHA256: 1E216047F4FA9D7737A32160E527DCAF10DFCA3D50D708FAD312F3DB21D7D9E3

```csharp
   1 | /* 本体可选伙伴战斗能力：Root每帧显式Sample，输出有限移动和一次Stage1候选。
   2 |  * 不读InputAction、不Advance动作、不扣HP、不移动Transform；切控代际屏障撤销旧候选。
   3 |  * 冷却仅在Accepted后开始；受阻退回Follow。只使用明确场景成员，拒绝重复身份。
   4 |  */
   5 | using System.Linq;
   6 | using Ossastra.CharacterCore;
   7 | using Ossastra.Combat;
   8 | using UnityEngine;
   9 | namespace Ossastra.Characters
  10 | {
  11 |     public interface ICompanionMoveCandidate { bool TryGetCombatMove(out Vector2 move); }
  12 |     public interface ICompanionActionCandidate { bool TryGetAttackTarget(out CombatVitals target); void OnAccepted(); }
  13 |     [DisallowMultipleComponent]
  14 |     public sealed class CompanionCombatBrain : MonoBehaviour, ICompanionMoveCandidate, ICompanionActionCandidate
  15 |     {
  16 |         [SerializeField] private CombatEngagementSession engagement;
  17 |         [SerializeField, Min(.1f)] private float anchorRadius = 3, attackInterval = 1.3f;
  18 |         private CharacterRoot owner;
  19 |         private CombatMeleeHit melee;
  20 |         private CombatVitals target;
  21 |         private double nextAttack;
  22 |         private int sampledFrame = -1, revision = -1;
  23 |         private float blockedTime, retryAt;
  24 |         private Vector3 previousPosition;
  25 |         private Vector2 move;
  26 |         private bool requestedMove;
  27 |         public CompanionCombatDecision Decision { get; private set; }
  28 |         public string TargetId => target == null ? "无" : target.Health?.Id;
  29 |         public string Reason { get; private set; } = "未采样";
  30 |         public void Initialize(CharacterRoot root) { owner = root; melee = GetComponent<CombatMeleeHit>(); previousPosition=transform.position; }
  31 |         private bool Context => isActiveAndEnabled && owner != null && owner.CanSimulateIntent &&
  32 |             engagement != null && engagement.CanSample && engagement.Engaged &&
  33 |             engagement.Coordinator.Current != owner && engagement.Coordinator.Characters.Contains(owner);
  34 |         private bool Fresh => Context && sampledFrame == Time.frameCount && revision == engagement.Coordinator.ControlRevision;
  35 |         private bool Valid(CombatVitals candidate)
  36 |         {
  37 |             if(candidate == null || !candidate.IsAvailable || owner.Vitals == null || !owner.Vitals.IsAvailable) return false;
  38 |             var enemies = engagement.Enemies;
  39 |             return enemies.Count(e => e != null && e.Vitals != null && e.Vitals.Health != null && e.Vitals.Health.Id == candidate.Health.Id) == 1 &&
  40 |                 enemies.Any(e => e != null && e.isActiveAndEnabled && e.Vitals == candidate) &&
  41 |                 CombatTargetPolicy.CanHit(owner.ActorId,owner.Vitals.Faction,true,candidate.Health.Id,candidate.Faction,true) &&
  42 |                 Mathf.Abs(candidate.transform.position.y-transform.position.y) < .5f &&
  43 |                 CombatSight.IsClear(transform,candidate.transform,.8f);
  44 |         }
  45 |         public void Sample(double now, float delta)
  46 |         {
  47 |             sampledFrame=Time.frameCount; move=Vector2.zero;
  48 |             if(!Context || melee == null) { ResetCandidate(); return; }
  49 |             var session=engagement.Coordinator;
  50 |             if(revision != session.ControlRevision) { target=null; blockedTime=0; requestedMove=false; }
  51 |             revision=session.ControlRevision;
  52 |             var position=transform.position;
  53 |             if(requestedMove && owner.DataBus.MoveReady && Vector3.Distance(position,previousPosition)<.001f) blockedTime+=delta;
  54 |             else blockedTime=0;
  55 |             previousPosition=position; requestedMove=false;
  56 |             if(blockedTime>.6f) { retryAt=Time.time+1; blockedTime=0; target=null; }
  57 |             var anchor=session.Current.transform.position;
  58 |             bool Inside(CombatVitals c) => Vector3.Distance(c.transform.position,anchor)<=anchorRadius;
  59 |             if(!Valid(target) || !Inside(target)) target=null;
  60 |             if(target == null && Time.time>=retryAt)
  61 |                 target=engagement.Enemies.Where(e=>e!=null).Select(e=>e.Vitals).Where(c=>Valid(c)&&Inside(c))
  62 |                     .OrderBy(c=>Vector3.SqrMagnitude(c.transform.position-position)).FirstOrDefault();
  63 |             var inRange=target!=null && Vector3.Distance(position,target.transform.position)<=melee.Reach+melee.Radius*.7f;
  64 |             Decision=CompanionCombatPolicy.Decide(true,false,true,true,target!=null,
  65 |                 Vector3.Distance(position,anchor)<=anchorRadius,inRange,now>=nextAttack && owner.DataBus.AttackReady,Time.time<retryAt);
  66 |             Reason=Decision.ToString();
  67 |             if(Decision==CompanionCombatDecision.Approach)
  68 |             { var d=target.transform.position-position; move=new Vector2(d.x,d.z).normalized; requestedMove=true; }
  69 |         }
  70 |         public bool TryGetCombatMove(out Vector2 value)
  71 |         { value=move; return Fresh && (Decision==CompanionCombatDecision.Approach || Decision==CompanionCombatDecision.Wait || Decision==CompanionCombatDecision.Attack); }
  72 |         public bool TryGetAttackTarget(out CombatVitals value)
  73 |         { value=target; return Fresh && Decision==CompanionCombatDecision.Attack && Valid(target); }
  74 |         public void OnAccepted() { nextAttack=owner.SimulationTime+attackInterval; Decision=CompanionCombatDecision.Wait; }
  75 |         public bool IsAcceptedTargetValid(CombatVitals value) => engagement != null && Valid(value);
  76 |         public void ResetCandidate() { target=null; move=Vector2.zero; requestedMove=false; Decision=CompanionCombatDecision.Inactive; Reason="无有效自治资格"; }
  77 |         private void OnDisable() { ResetCandidate(); }
  78 |     }
  79 | }
```

## Assets/OSSASTRA/Scripts/Combat/Runtime/CombatEngagementSession.cs
SHA256: BD6B142975EF91015541088BEA41018ABC690D3AFF2120A6E4A1CA2462732CBD

```csharp
   1 | /* 场景交战资格：仅观察真实Accepted及存活威胁；不拥有Current、HP、动作或角色目标。
   2 |  * Update只推进平静脱战窗口。双向根直接发布事实，无全局事件总线。参数为SAMPLE。
   3 |  */
   4 | using System;
   5 | using System.Linq;
   6 | using Ossastra.CharacterCore;
   7 | using Ossastra.Characters;
   8 | using Ossastra.Enemies;
   9 | using UnityEngine;
  10 | namespace Ossastra.Combat
  11 | {
  12 |     [DefaultExecutionOrder(-140), DisallowMultipleComponent]
  13 |     public sealed class CombatEngagementSession : MonoBehaviour
  14 |     {
  15 |         [SerializeField] private CharacterControlCoordinator coordinator;
  16 |         [SerializeField] private Ossastra.Enemies.EnemyRoot[] enemies = Array.Empty<Ossastra.Enemies.EnemyRoot>();
  17 |         [SerializeField, Min(.1f)] private float quietDuration = 3;
  18 |         private float quietTime;
  19 |         public bool Engaged { get; private set; }
  20 |         public CharacterControlCoordinator Coordinator => coordinator;
  21 |         public Ossastra.Enemies.EnemyRoot[] Enemies => (Ossastra.Enemies.EnemyRoot[])enemies.Clone();
  22 |         public bool IsAvailable => isActiveAndEnabled && coordinator != null && coordinator.isActiveAndEnabled;
  23 |         public bool CanSample => IsAvailable && coordinator.IsIntentSamplingAllowed &&
  24 |             !coordinator.Characters.Any(c => c != null && c.IsSimulationSuspended);
  25 |         public float QuietTime => quietTime;
  26 |         private void OnEnable()
  27 |         {
  28 |             if (coordinator == null) return;
  29 |             // 同一控制会话只能有一个资格观察者；重复配置显式拒绝，不争夺Current。
  30 |             if (FindObjectsOfType<CombatEngagementSession>().Any(other => other != this && other.isActiveAndEnabled && other.coordinator == coordinator))
  31 |             { enabled=false; return; }
  32 |             foreach (var c in coordinator.Characters) if(c != null) c.ActionObserved += OnFact;
  33 |             foreach (var e in enemies) if(e != null) e.ActionObserved += OnFact;
  34 |         }
  35 |         private void OnFact(ActionFact fact)
  36 |         {
  37 |             if (!IsAvailable || fact.Kind != ActionFactKind.Accepted) return;
  38 |             Engaged = true; quietTime = 0;
  39 |         }
  40 |         private void Update()
  41 |         {
  42 |             if (!CanSample || !Engaged) return;
  43 |             var threat = enemies.Any(e => e != null && e.isActiveAndEnabled && e.Vitals != null &&
  44 |                 e.Vitals.IsAvailable && (e.ActionActive || e.HasCombatTarget));
  45 |             if (threat) quietTime = 0;
  46 |             else { quietTime += Time.deltaTime; if(quietTime >= quietDuration) Engaged = false; }
  47 |         }
  48 |         private void OnDisable()
  49 |         {
  50 |             if (coordinator != null) foreach(var c in coordinator.Characters) if(c != null) c.ActionObserved -= OnFact;
  51 |             foreach(var e in enemies) if(e != null) e.ActionObserved -= OnFact;
  52 |             Engaged = false; quietTime = 0;
  53 |         }
  54 |     }
  55 | }
```

## Assets/OSSASTRA/Scripts/Combat/Runtime/CombatMeleeHit.cs
SHA256: 10871090E82892936267C205923F73718C68B5A44613D260C7899CF1F116D44F

```csharp
   1 | /*
   2 |  * 脚本：CombatMeleeHit
   3 |  * 功能：将一次释放转换为物理候选、身份/阵营资格和唯一伤害批次。
   4 |  * 边界：中立能力，无玩家Root依赖，不建动作时钟或HP；空挥也关闭Release。
   5 |  * 协作：两类战斗根各自释放；Applied先反馈/死亡，存活者再接可选架势，不重复扣血。
   6 |  */
   7 | using System;
   8 | using System.Linq;
   9 | using Ossastra.CharacterCore;
  10 | using UnityEngine;
  11 | namespace Ossastra.Combat
  12 | {
  13 |     public sealed class CombatMeleeHit : MonoBehaviour, ICombatHitResolver, ICombatHitReceiptSource
  14 |     {
  15 |         [SerializeField, Min(0)] private float reach=.8f;
  16 |         [SerializeField, Min(.01f)] private float radius=.65f;
  17 |         [SerializeField] private float height=.8f;
  18 |         [SerializeField] private LayerMask targetLayers=~0;
  19 |         public string LastResult { get; private set; }="未释放";
  20 |         public event Action<Guid, bool> Resolved;
  21 |         public float Reach => reach;
  22 |         public float Radius => radius;
  23 |         public float Height => height;
  24 |         public void Resolve(AttackRelease release, Vector3 releaseDirection)
  25 |         {
  26 |             if(release==null) throw new ArgumentNullException(nameof(release));
  27 |             var source=GetComponent<CombatVitals>();
  28 |             if(!isActiveAndEnabled || (source!=null && (!source.IsAvailable || source.Health.Id!=release.ActorId)))
  29 |             { release.Resolve(Array.Empty<Health>()); LastResult="来源失效或释放身份不符"; Resolved?.Invoke(release.ActionId,false); return; }
  30 |             var center=transform.position+Vector3.up*height+releaseDirection*reach;
  31 |             var candidates=Physics.OverlapSphere(center,radius,targetLayers,QueryTriggerInteraction.Collide)
  32 |                 .Select(c=>c.GetComponentsInParent<MonoBehaviour>().OfType<IHitTarget>().FirstOrDefault())
  33 |                 .Where(CombatTargetValidity.IsAvailable).Distinct()
  34 |                 .Where(t=>t is MonoBehaviour body && CombatSight.IsClear(transform,body.transform,height)).ToArray();
  35 |             // 同一接收者多个Collider合并；不同接收者冒用同ID整组拒绝，不能任取一个扣HP。
  36 |             var targets=candidates.GroupBy(t=>t.Health.Id,StringComparer.Ordinal)
  37 |                 .Where(g=>g.Count()==1).Select(g=>g.Single())
  38 |                 .Where(t=>CombatTargetPolicy.CanHit(release.ActorId,
  39 |                     source==null?CombatFaction.Neutral:source.Faction,source==null||source.IsAlive,
  40 |                     t.Health.Id,t is ICombatTarget combat?combat.Faction:CombatFaction.Neutral,true)).ToArray();
  41 |             var outcomes=release.Resolve(targets.Select(t=>t.Health));
  42 |             foreach(var outcome in outcomes)
  43 |                 if(outcome.Reason==HitReason.Applied)
  44 |                 {
  45 |                     var target=targets.First(t=>t.Health.Id==outcome.TargetId);
  46 |                     if(target is ICombatTarget combat) combat.OnHitResolved(outcome); else target.ShowHit();
  47 |                     // HP/死亡通知已完成；同批存活者才消费架势。不会再做空间查询或伤害结算。
  48 |                     if (outcome.HpAfter > 0 && target is MonoBehaviour component && component != null)
  49 |                         foreach (var receiver in component.GetComponents<MonoBehaviour>().OfType<ICombatPostureReceiver>())
  50 |                             receiver.ReceiveImpact(outcome, release.PostureImpact);
  51 |                 }
  52 |             LastResult=outcomes.Count==0?"空挥／无合法敌对目标":string.Join("; ",outcomes.Select(o=>
  53 |                 o.ActorId+" → "+o.TargetId+" "+o.HpBefore+"→"+o.HpAfter+" "+o.Reason+" #"+o.ActionId.ToString("N")));
  54 |             Resolved?.Invoke(release.ActionId,outcomes.Any(o=>o.Reason==HitReason.Applied));
  55 |         }
  56 |     }
  57 | }
```

## Assets/OSSASTRA/Scripts/Development/Combat/TeamCombatDebugView.cs
SHA256: 26D40E8AFEF61DED791C6980CCC07631CA530418730D3BFA873925C9C4C9AF8D

```csharp
   1 | /* 双人战斗开发观察：只读交战、候选、动作来源、敌预警及二维fallback。
   2 |  * AI开关仅启停本体能力，重测使用既有开发入口；不写HP或伪造入战成功。
   3 |  * 头顶只给敌/NPC测试身份，绝不添加友方头顶血条。正式美术缺失明确标记。
   4 |  */
   5 | using Ossastra.Characters;
   6 | using Ossastra.Combat;
   7 | using Ossastra.AnimationPresentation;
   8 | using UnityEngine;
   9 | namespace Ossastra.Development
  10 | {
  11 |     public sealed class TeamCombatDebugView : MonoBehaviour
  12 |     {
  13 |         [SerializeField] private CombatEngagementSession engagement;
  14 |         [SerializeField] private Ossastra.Npcs.NpcRoot npc;
  15 |         private readonly Rect panel=new Rect(1570,12,410,340);
  16 |         private void OnEnable()
  17 |         {
  18 |             if(engagement!=null&&engagement.Coordinator!=null)
  19 |                 foreach(var c in engagement.Coordinator.Characters)if(c!=null)c.GetComponent<CharacterInputHandler>()?.RegisterPointerRegion(this,panel);
  20 |         }
  21 |         private void OnDisable()
  22 |         {
  23 |             if(engagement!=null&&engagement.Coordinator!=null)
  24 |                 foreach(var c in engagement.Coordinator.Characters)if(c!=null)c.GetComponent<CharacterInputHandler>()?.RemovePointerRegion(this);
  25 |         }
  26 |         private void OnGUI()
  27 |         {
  28 |             if(engagement==null||engagement.Coordinator==null)return;
  29 |             GUILayout.BeginArea(panel,GUI.skin.box);
  30 |             GUILayout.Label("ROADMAP02 / 技术试玩 / SAMPLE");
  31 |             GUILayout.Label("交战="+engagement.Engaged+" 平静="+engagement.QuietTime.ToString("F1"));
  32 |             foreach(var c in engagement.Coordinator.Characters)
  33 |             {
  34 |                 if(c==null)continue;var brain=c.GetComponent<CompanionCombatBrain>();
  35 |                 GUILayout.Label(c.ActorId+" 来源="+c.LastActionSource);
  36 |                 if(brain!=null)
  37 |                 {
  38 |                     GUILayout.Label("目标="+brain.TargetId+" / "+brain.Reason);
  39 |                     if(GUILayout.Button(new GUIContent(brain.enabled?"停用该角色自治":"启用该角色自治","仅启停本体候选；不改变Current或生命")))brain.enabled=!brain.enabled;
  40 |                 }
  41 |             }
  42 |             foreach(var e in engagement.Enemies)
  43 |             {
  44 |                 if(e==null)continue;var p=e.GetComponentInChildren<EntitySpinePresenter>();
  45 |                 GUILayout.Label("敌预警="+e.WarningVisible+" 已释放="+e.AttackReleased);
  46 |                 if(p!=null)GUILayout.Label(p.Semantic+" → "+p.CurrentClip+" "+p.FallbackReason);
  47 |             }
  48 |             GUILayout.Label("2D为本地现有Spine替身；正式敌人美术待补");
  49 |             GUILayout.EndArea();
  50 |             var camera=Camera.main;if(camera==null)return;
  51 |             foreach(var e in engagement.Enemies)if(e!=null&&e.Vitals!=null&&e.Vitals.IsAlive)
  52 |                 Label(camera,e.transform.position+Vector3.up*1.8f,"敌方 TEMP · HP "+e.Vitals.Health.Current,new Color(1,.65f,.5f));
  53 |             if(npc!=null)Label(camera,npc.transform.position+Vector3.up*1.9f,"NPC TEMP · 非战斗",Color.white);
  54 |         }
  55 |         private static void Label(Camera camera,Vector3 world,string text,Color color)
  56 |         {
  57 |             var p=camera.WorldToScreenPoint(world);if(p.z<=0)return;
  58 |             var old=GUI.color;GUI.color=color;GUI.Label(new Rect(p.x-90,Screen.height-p.y-15,220,30),text);GUI.color=old;
  59 |         }
  60 |     }
  61 | }
```

## Assets/OSSASTRA/Scripts/Enemies/Core/AttackWarningState.cs
SHA256: 10FD1871E089696886C93320C7605DC6E043E205B392CB7BD247BD70D508203F

```csharp
   1 | /* 攻击预警身份投影：只记已观察事实，不推进时钟，不产生攻击或命中。 */
   2 | using System;
   3 | namespace Ossastra.EnemyCore
   4 | {
   5 |     public sealed class AttackWarningState
   6 |     {
   7 |         public Guid ActionId { get; private set; }
   8 |         public bool Tracking => ActionId != Guid.Empty && !Released;
   9 |         public bool Released { get; private set; }
  10 |         public void Accept(Guid id) { if(id==Guid.Empty || id==ActionId) return; ActionId=id; Released=false; }
  11 |         public void Release(Guid id) { if(id==ActionId && id!=Guid.Empty) Released=true; }
  12 |         public void Clear(Guid id) { if(id!=ActionId)return; ActionId=Guid.Empty; Released=false; }
  13 |     }
  14 | }
```

## Assets/OSSASTRA/Scripts/Enemies/Runtime/EnemyAttackWarning.cs
SHA256: FD2AE5D6EB2700E7FF6C3847267B452ADAE23DD3C40DAC05A3E9481495591FB0

```csharp
   1 | /* 世界预警适配：LateUpdate只读取敌根真实动作投影与近战空间参数。
   2 |  * 使用两个复用LineRenderer，无动作时钟/伤害写入；生命周期显式销毁自有材质和对象。
   3 |  * SAMPLE：地面圆为当前追踪预计范围，不承诺锁定落点或命中；无单向光照。
   4 |  */
   5 | using UnityEngine;
   6 | namespace Ossastra.Enemies
   7 | {
   8 |     [DisallowMultipleComponent, RequireComponent(typeof(EnemyRoot))]
   9 |     public sealed class EnemyAttackWarning : MonoBehaviour
  10 |     {
  11 |         private EnemyRoot root;
  12 |         private LineRenderer ring, direction;
  13 |         private Material material;
  14 |         public bool Visible => ring != null && ring.enabled;
  15 |         private void Awake()
  16 |         {
  17 |             root=GetComponent<EnemyRoot>();
  18 |             var shader=Shader.Find("Sprites/Default");
  19 |             if(shader==null) { enabled=false; return; }
  20 |             material=new Material(shader){name="SAMPLE_AttackWarning_Runtime"};
  21 |             ring=CreateLine("TrackingRange",49,true); direction=CreateLine("TrackingDirection",2,false);
  22 |         }
  23 |         private LineRenderer CreateLine(string label,int count,bool loop)
  24 |         {
  25 |             var go=new GameObject(label);go.transform.SetParent(transform,false);
  26 |             var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;
  27 |             line.positionCount=count;line.loop=loop;line.useWorldSpace=true;line.widthMultiplier=.035f;
  28 |             line.startColor=line.endColor=new Color(1,.38f,.2f,.8f);line.enabled=false;return line;
  29 |         }
  30 |         private void LateUpdate()
  31 |         {
  32 |             if(ring==null)return;
  33 |             var show=root!=null && root.WarningVisible;
  34 |             ring.enabled=direction.enabled=show;
  35 |             if(!show)return;
  36 |             var center=root.WarningCenter;center.y=transform.position.y+.04f;
  37 |             for(int i=0;i<ring.positionCount;i++)
  38 |             {float angle=i*Mathf.PI*2/(ring.positionCount-1);ring.SetPosition(i,center+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*root.WarningRadius);}
  39 |             direction.SetPosition(0,new Vector3(transform.position.x,center.y,transform.position.z));direction.SetPosition(1,center);
  40 |         }
  41 |         private void OnDisable(){if(ring!=null)ring.enabled=false;if(direction!=null)direction.enabled=false;}
  42 |         private void OnDestroy()
  43 |         {
  44 |             if(ring!=null)Destroy(ring.gameObject);if(direction!=null)Destroy(direction.gameObject);
  45 |             if(material!=null)Destroy(material);
  46 |         }
  47 |     }
  48 | }
```

## Assets/OSSASTRA/Scripts/Enemies/Runtime/EnemyRoot.cs
SHA256: 3FCB917728F1AD3FB55A3D99E0FEBE547ECE699504782D844D7AB46E5CCED691

```csharp
   1 | /*
   2 |  * 脚本：EnemyRoot
   3 |  * 功能：敌方唯一组合根，组织身份、生命、决策与单一动作推进/释放。
   4 |  * 边界：无玩家Root/Input/Bus/Current，不选最近目标、不直接扣HP。
   5 |  * 协作：Brain提交并验证目标；根独占动作和可选架势推进，破势/生命周期撤销未释放动作。
   6 |  */
   7 | using System;
   8 | using Ossastra.CharacterCore;
   9 | using Ossastra.Combat;
  10 | using UnityEngine;
  11 | namespace Ossastra.Enemies
  12 | {
  13 |     [RequireComponent(typeof(CombatVitals),typeof(CombatMeleeHit))]
  14 |     public sealed class EnemyRoot : MonoBehaviour
  15 |     {
  16 |         [SerializeField] private string actorId="enemy-stationary-01";
  17 |         [SerializeField] private float releaseAt=.5f, moveReadyAt=.6f, attackReadyAt=.9f, finishAt=1.1f;
  18 |         [SerializeField, Min(1)] private int damage=20;
  19 |         [SerializeField, Min(0)] private float postureImpact;
  20 |         [SerializeField] private CombatPosture posture;
  21 |         [SerializeField] private bool attacksEnabled=true;
  22 |         private CombatVitals vitals;
  23 |         private ICombatHitResolver melee;
  24 |         private CombatMeleeHit hitShape;
  25 |         private readonly Ossastra.EnemyCore.AttackWarningState warning = new Ossastra.EnemyCore.AttackWarningState();
  26 |         public Guid ActionId => runtime == null ? Guid.Empty : runtime.ActionId;
  27 |         public bool WarningVisible => isActiveAndEnabled && focused && !applicationPaused && !paused && warning.Tracking && vitals!=null && vitals.IsAvailable && target!=null && target.IsAvailable;
  28 |         public bool AttackReleased => warning.Released;
  29 |         public Vector3 AttackDirection { get { var d=target==null?Vector3.forward:target.WorldPosition-transform.position; d.y=0; return d.normalized; } }
  30 |         public Vector3 WarningCenter => transform.position+AttackDirection*(hitShape==null?0:hitShape.Reach);
  31 |         public float WarningRadius => hitShape==null?0:hitShape.Radius;
  32 |         private IEnemyBrain brain;
  33 |         private IEnemyMovement movement;
  34 |         private BasicActionRuntime runtime;
  35 |         private BasicAttackDefinition attack;
  36 |         private readonly ControlAuthority authority=new ControlAuthority();
  37 |         private ICombatTarget target;
  38 |         private Health targetHealth;
  39 |         private bool focused=true, applicationPaused, paused;
  40 |         public event Action<ActionFact> ActionObserved;
  41 |         public bool HasCombatTarget => brain is ChaseEnemyBrain chase && chase.TargetId!="none";
  42 |         public string ActorId => actorId;
  43 |         public CombatVitals Vitals => vitals;
  44 |         public string LastFact { get; private set; }="Idle";
  45 |         public string LastHit => melee==null?"未初始化":melee.LastResult;
  46 |         public bool ActionActive => runtime!=null && runtime.IsActive;
  47 |         public bool AttacksEnabled { get => attacksEnabled; set { attacksEnabled=value; if(!value) Cancel("development-attacks-disabled"); } }
  48 |         public int PresentationGeneration { get; private set; }
  49 |         public bool IsSimulationSuspended => !focused || applicationPaused || paused;
  50 |         private Ossastra.AnimationPresentation.IEntityPresentation presentation;
  51 |         private void LateUpdate() { presentation?.Present(); }
  52 |         private void Awake()
  53 |         {
  54 |             presentation=GetComponent<EnemySpineView>();
  55 |             vitals=GetComponent<CombatVitals>(); melee=GetComponent<CombatMeleeHit>(); hitShape=GetComponent<CombatMeleeHit>();
  56 |             var chase=GetComponent<ChaseEnemyBrain>();
  57 |             brain=chase!=null&&chase.enabled?(IEnemyBrain)chase:GetComponent<StationaryEnemyBrain>();
  58 |             movement=GetComponent<EnemyMovement>();
  59 |             try { attack=new BasicAttackDefinition(releaseAt,moveReadyAt,attackReadyAt,finishAt,damage,postureImpact); runtime=new BasicActionRuntime(actorId); }
  60 |             catch(ArgumentException e) { Debug.LogError("Enemy configuration: "+e.Message,this); enabled=false; return; }
  61 |             if(!vitals.Initialize(actorId) || brain==null || !brain.Initialize(vitals))
  62 |             { Debug.LogError("Enemy identity/brain invalid",this); enabled=false; return; }
  63 |             vitals.Died+=OnDied;
  64 |             if (posture != null)
  65 |             {
  66 |                 if (!posture.Initialize(vitals, this)) { Debug.LogError("Enemy posture configuration invalid", this); enabled=false; return; }
  67 |                 posture.Broken += OnPostureBroken;
  68 |                 posture.Recovered += OnPostureRecovered;
  69 |             }
  70 |         }
  71 |         private void Update()
  72 |         {
  73 |             if(runtime==null) return;
  74 |             if(vitals==null || !vitals.IsAvailable) { Cancel("source-unavailable"); return; }
  75 |             // 1. 根独占架势时钟，禁用开发攻击不冻结恢复；失焦/暂停停钟。
  76 |             if (posture != null) posture.Tick(!focused || applicationPaused || paused ? 0 : Time.deltaTime, runtime.IsActive);
  77 |             if(!attacksEnabled || !focused || applicationPaused || paused || !authority.Allows(ControlChannel.Attack)) {movement?.Stop();return;}
  78 |             // 2. 决策资格先于动作请求；Brain不掌握架势数值或第二时钟。
  79 |             if(brain==null || !brain.IsAvailable) { Cancel("brain-disabled"); return; }
  80 |             brain.Tick(Time.time,Time.deltaTime);
  81 |             if(runtime.IsActive && (!brain.IsEligible(target) || !ReferenceEquals(target.Health,targetHealth)))
  82 |                 Cancel("target-dead-out-of-range-or-occluded-or-stale");
  83 |             if(!runtime.IsActive && brain.TrySelect(Time.time,out var selected))
  84 |             {
  85 |                 var request=runtime.Request(attack,"enemy-brain",authority);
  86 |                 if(request.Accepted)
  87 |                 {
  88 |                     target=selected; targetHealth=selected.Health; brain.OnAccepted(Time.time);
  89 |                     LastFact="Accepted #"+request.Fact.ActionId.ToString("N");
  90 |                     warning.Accept(request.Fact.ActionId);
  91 |                     ActionObserved?.Invoke(request.Fact);
  92 |                 }
  93 |             }
  94 |             // 3. 唯一动作推进/命中阶段；破势回调立即Cancel，不保留冻结的待释放动作。
  95 |             foreach(var fact in runtime.Advance(Time.deltaTime,false))
  96 |             {
  97 |                 LastFact=fact.Kind+" #"+fact.ActionId.ToString("N");
  98 |                 if(fact.Kind==ActionFactKind.Released)warning.Release(fact.ActionId);
  99 |                 if(fact.Kind==ActionFactKind.Finished || fact.Kind==ActionFactKind.Cancelled)warning.Clear(fact.ActionId);
 100 |                 ActionObserved?.Invoke(fact);
 101 |                 if(fact.Release!=null)
 102 |                 {
 103 |                     var direction=target.WorldPosition-transform.position; direction.y=0;
 104 |                     melee.Resolve(fact.Release,direction.normalized);
 105 |                 }
 106 |             }
 107 |             // 根的最后物理阶段：只消费Brain方向；已接受攻击全段停步，避免贴人重叠攻击。
 108 |             movement?.Tick(runtime.IsActive?Vector3.zero:brain.MoveDirection,Time.deltaTime);
 109 |         }
 110 |         public void Cancel(string reason)
 111 |         {
 112 |             var fact=runtime==null?null:runtime.Cancel(reason);
 113 |             if(fact!=null) { PresentationGeneration++; warning.Clear(fact.ActionId);
 114 |                 ActionObserved?.Invoke(fact); }
 115 |             if(fact!=null) LastFact="Cancelled "+reason+" #"+fact.ActionId.ToString("N");
 116 |             target=null; targetHealth=null;
 117 |             movement?.Stop();
 118 |             brain?.ResetDecision(Time.time);
 119 |         }
 120 |         public void SetPaused(bool value) { paused=value; if(value) Cancel("enemy-paused"); }
 121 |         private void OnPostureBroken(string cause)
 122 |         {
 123 |             authority.Block(posture, ControlChannel.Move | ControlChannel.Attack);
 124 |             Cancel("posture-broken:" + cause);
 125 |         }
 126 |         private void OnPostureRecovered() { authority.Release(posture); }
 127 |         private void OnDied(HitOutcome outcome) { Cancel("death:"+outcome.ActorId); }
 128 |         private void OnDisable() { Cancel("enemy-disabled"); if (posture != null) posture.ResetState(); authority.Release(posture); }
 129 |         private void OnApplicationFocus(bool value) { focused=value; if(!value) Cancel("focus-lost"); }
 130 |         private void OnApplicationPause(bool value) { applicationPaused=value; if(value) Cancel("application-paused"); }
 131 |         private void OnDestroy()
 132 |         {
 133 |             if(vitals!=null) vitals.Died-=OnDied;
 134 |             if(posture!=null) { posture.Broken-=OnPostureBroken; posture.Recovered-=OnPostureRecovered; }
 135 |             authority.Release(posture);
 136 |             Cancel("destroyed");
 137 |         }
 138 |         public void RestoreForDevelopment()
 139 |         { PresentationGeneration++; Cancel("development-reset"); vitals.RestoreForDevelopment(); if(posture!=null) posture.ResetState(); brain.ResetDecision(Time.time); }
 140 |     }
 141 | }
```

## Assets/OSSASTRA/Scripts/Enemies/Runtime/EnemySpineView.cs
SHA256: E0B152CFAE09FDAC0FF08F238FFE2D3F1C53076090E04B37FD915C08C4C1E504

```csharp
   1 | /* 敌方二维事实适配：敌根LateUpdate显式调用；死亡/破势/受击/动作/移动顺序只决定表现。
   2 |  * 没有CharacterRoot或玩家Bus依赖；生命退场仍由CombatVitals负责。
   3 |  */
   4 | using System;
   5 | using Ossastra.AnimationCore;
   6 | using Ossastra.AnimationPresentation;
   7 | using Ossastra.Combat;
   8 | using UnityEngine;
   9 | namespace Ossastra.Enemies
  10 | {
  11 |     [DisallowMultipleComponent]
  12 |     public sealed class EnemySpineView : MonoBehaviour,IEntityPresentation
  13 |     {
  14 |         [SerializeField] private EntitySpinePresenter presenter;
  15 |         private EnemyRoot root;private EnemyMovement movement;private CombatPosture posture;private float facing=1;
  16 |         private void Awake(){root=GetComponent<EnemyRoot>();movement=GetComponent<EnemyMovement>();posture=GetComponent<CombatPosture>();}
  17 |         public void Present()
  18 |         {
  19 |             if(!isActiveAndEnabled||root==null||root.Vitals==null||presenter==null)return;
  20 |             var velocity=movement==null?Vector3.zero:movement.Velocity;
  21 |             var direction=root.ActionActive?root.AttackDirection:velocity;
  22 |             if(Mathf.Abs(direction.x)>.01f)facing=Mathf.Sign(direction.x);
  23 |             var semantic=!root.Vitals.IsAlive?AnimationSemantic.Death:posture!=null&&posture.IsStaggered?AnimationSemantic.PostureBroken:
  24 |                 root.Vitals.IsHitReacting?AnimationSemantic.HitReaction:root.ActionActive?AnimationSemantic.BasicAttack:
  25 |                 velocity.sqrMagnitude>.001f?AnimationSemantic.Move:AnimationSemantic.Idle;
  26 |             presenter.Present(new Ossastra.AnimationCore.AnimationIntent(root.ActorId,semantic,"enemy-facts",root.ActionId,root.PresentationGeneration),facing,root.IsSimulationSuspended);
  27 |         }
  28 |     }
  29 | }
```

## Assets/OSSASTRA/Scripts/Npcs/Runtime/NpcRoot.cs
SHA256: F95D3EE483060410667F95B588535925D12F647589A6B87EA2633F2BCB6552EB

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
  16 |         private Ossastra.AnimationPresentation.IEntityPresentation presentation;
  17 |         private void LateUpdate() { presentation?.Present(); }
  18 |         public string NpcId => npcId;
  19 |         public int InteractionCount => interaction==null?0:interaction.Count;
  20 |         public event Action<NpcReply> Replied;
  21 |         private void Awake()
  22 |         {
  23 |             presentation=GetComponent<NpcSpineView>();
  24 |             try { interaction=new NpcInteraction(npcId,interactionRange); interaction.Replied+=OnReplied; }
  25 |             catch(ArgumentException error) { Debug.LogError("NPC configuration: "+error.Message,this); enabled=false; }
  26 |         }
  27 |         public NpcReply TryInteract(string actorId,Vector3 actorPosition,bool actorAvailable)
  28 |         {
  29 |             if(interaction==null) return new NpcReply(false,npcId,actorId,"not-initialized",0);
  30 |             return interaction.TryInteract(actorId,(actorPosition-transform.position).sqrMagnitude,
  31 |                 isActiveAndEnabled && actorAvailable);
  32 |         }
  33 |         private void OnReplied(NpcReply reply) { Replied?.Invoke(reply); }
  34 |         private void OnDestroy()
  35 |         {
  36 |             if(interaction!=null) { interaction.Replied-=OnReplied; interaction.Dispose(); }
  37 |             Replied=null;
  38 |         }
  39 |     }
  40 | }
```

## Assets/OSSASTRA/Scripts/Npcs/Runtime/NpcSpineView.cs
SHA256: 6773989D4F2EE5CF4E8C93731DB7E3F704475A8B8777FB0D5189DB6FCFE60AFE

```csharp
   1 | /* NPC独立二维适配：根在LateUpdate提交Idle。无输入、Health、EnemyBrain或动作时钟。 */
   2 | using System;
   3 | using Ossastra.AnimationCore;
   4 | using Ossastra.AnimationPresentation;
   5 | using UnityEngine;
   6 | namespace Ossastra.Npcs
   7 | {
   8 |     [DisallowMultipleComponent]
   9 |     public sealed class NpcSpineView:MonoBehaviour,IEntityPresentation
  10 |     {
  11 |         [SerializeField] private EntitySpinePresenter presenter;
  12 |         private NpcRoot root;
  13 |         private void Awake(){root=GetComponent<NpcRoot>();}
  14 |         public void Present(){if(isActiveAndEnabled&&root!=null&&presenter!=null)presenter.Present(new Ossastra.AnimationCore.AnimationIntent(root.NpcId,AnimationSemantic.Idle,"npc-idle",Guid.Empty,0),1,false);}
  15 |     }
  16 | }
```
