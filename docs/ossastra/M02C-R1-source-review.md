# M02-C-R1 变化源码完整审查
仅7份变化源码；全部待审。角色/队伍权威、迁移与生命周期说明见M02C-R1-review.md。Core策略、Coordinator、Bus、Movement和动作核心本轮保持不变，不机械重抄历史源码。

## CharacterFollow

Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterFollow.cs

行数 135；SHA256 6DCFBE7B3640A3188A63649FA3B3749E17AF5E675C5C64DADE5B4A6D3AD13022

本体能力。Initialize只构造自己的策略，Assign/Unassign验证分配owner；TryContext统一检查目标/权限。IsAvailable撤回失效帧但不推进时间，Read只产move，Observe纯读。禁启/销毁只清自己。

```csharp
001 | /*
002 |  * 脚本：CharacterFollow
003 |  * 功能：角色自身的可选跟随能力，拥有独立阈值、策略、位移历史与只读观察。
004 |  * 边界：仅按外部分配目标产出move；不选择Current、不读设备、不执行运动/动作/HP/Spine。
005 |  * 协作：Root显式初始化，Director分配目标源；Selector在Root原更新中惰性读取。
006 |  */
007 | using System;
008 | using Ossastra.CharacterCore;
009 | using UnityEngine;
010 |
011 | namespace Ossastra.Characters
012 | {
013 |     [DisallowMultipleComponent]
014 |     public sealed class CharacterFollow : MonoBehaviour, ICompanionIntentSource
015 |     {
016 |         [Header("跟随候选参数 / 世界单位，不是正式玩法平衡")]
017 |         [SerializeField, Min(.01f)] private float followStartDistance=2.4f, followStopDistance=1.6f;
018 |         [SerializeField, Min(.01f)] private float blockedCheckWindow=.5f, retryTargetDelta=.5f;
019 |         private CharacterRoot owner, sampledTarget;
020 |         private ICharacterControlSession session;
021 |         private ICharacterFollowTargetSource targetSource;
022 |         private CompanionFollowPolicy policy;
023 |         private Vector3 lastPosition;
024 |         private int revision=-1;
025 |         private CompanionFollowState state;
026 |         private string reason="未分配目标";
027 |         public float StartDistance => followStartDistance;
028 |         public float StopDistance => followStopDistance;
029 |         public float BlockedCheckWindow => blockedCheckWindow;
030 |         public float RetryTargetDelta => retryTargetDelta;
031 |         public CharacterRoot Owner => owner;
032 |
033 |         /// <summary>许可检查也撤回已失效历史/帧；不推进策略时钟。观察UI使用Observe，无此副作用。</summary>
034 |         public bool IsAvailable
035 |         {
036 |             get
037 |             {
038 |                 if (!TryContext(out var target)) { ResetIntent(); return false; }
039 |                 if (sampledTarget != target || revision != session.ControlRevision) ResetIntent();
040 |                 return true;
041 |             }
042 |         }
043 |
044 |         public bool Initialize(CharacterRoot character)
045 |         {
046 |             if (character == null || character.gameObject != gameObject || (owner != null && owner != character)) return false;
047 |             owner=character;
048 |             // 参数只在显式初始化时构造策略；无效组件不会让本来可单人操作的Root启动失败。
049 |             try { policy=new CompanionFollowPolicy(followStartDistance,followStopDistance,blockedCheckWindow,retryTargetDelta); }
050 |             catch (ArgumentException) { policy=null; reason="跟随参数无效"; return false; }
051 |             ResetIntent(); return true;
052 |         }
053 |
054 |         public bool CanAssign(ICharacterControlSession authority, ICharacterFollowTargetSource source)
055 |             => owner != null && policy != null && Alive(authority) && Alive(source) &&
056 |                 (!Alive(targetSource) || ReferenceEquals(targetSource,source));
057 |
058 |         /// <summary>只能同一分配者重绑，或接替已销毁/明确归还的分配；不抢活owner。</summary>
059 |         public bool Assign(ICharacterControlSession authority, ICharacterFollowTargetSource source)
060 |         {
061 |             if (!CanAssign(authority,source)) return false;
062 |             session=authority; targetSource=source; ResetIntent(); return true;
063 |         }
064 |
065 |         public void Unassign(ICharacterFollowTargetSource source)
066 |         {
067 |             if (!ReferenceEquals(source,targetSource)) return;
068 |             ResetIntent(); session=null; targetSource=null; reason="未分配目标";
069 |         }
070 |
071 |         public CharacterInputFrame ReadCompanionIntent(CharacterRoot character, float delta)
072 |         {
073 |             // 外来角色不能借本角色的组件读取移动，更不能清掉合法owner正在使用的策略。
074 |             if (character != owner) return default;
075 |             if (!IsAvailable || !TryContext(out var target)) return default;
076 |             var position=owner.transform.position;
077 |             var travel=position-lastPosition; travel.y=0;
078 |             if (sampledTarget != target || revision != session.ControlRevision) travel=Vector3.zero;
079 |             sampledTarget=target; revision=session.ControlRevision; lastPosition=position;
080 |             var targetPosition=target.transform.position;
081 |             var offset=targetPosition-position;
082 |             var decision=policy.Sample(offset.x,offset.z,targetPosition.x,targetPosition.z,travel.magnitude,delta,true);
083 |             state=decision.State;
084 |             reason=state==CompanionFollowState.Blocked ? "受阻等待目标移动；无绕墙" :
085 |                 state==CompanionFollowState.CloseEnough ? "停止区或启动阈值内" : "朝分配目标移动";
086 |             return new CharacterInputFrame { move=new Vector2((float)decision.MoveX,(float)decision.MoveZ) };
087 |         }
088 |
089 |         private bool TryContext(out CharacterRoot target)
090 |         {
091 |             target=null;
092 |             if (!isActiveAndEnabled || owner == null || !owner.CanSimulateIntent || policy == null ||
093 |                 !Alive(session) || !session.IsIntentSamplingAllowed || !Alive(targetSource) ||
094 |                 (targetSource is Behaviour provider && !provider.isActiveAndEnabled) ||
095 |                 !targetSource.TryGetFollowTarget(owner,out target) || target == null || target == owner ||
096 |                 !target.CanSimulateIntent || owner.DataBus == null || !owner.DataBus.AllowsMovement || !owner.DataBus.MoveReady)
097 |                 return false;
098 |             return true;
099 |         }
100 |
101 |         /// <summary>仅重置本角色能力；Root在暂停/失焦/禁用时调用，旧动作生命周期不变。</summary>
102 |         public void ResetIntent()
103 |         {
104 |             if (policy != null) policy.Reset();
105 |             sampledTarget=null; revision=-1; state=CompanionFollowState.Inactive;
106 |             reason=policy == null ? "跟随参数无效或未初始化" : "等待合法目标重新采样";
107 |             if (owner != null) { lastPosition=owner.transform.position; owner.ClearCompanionIntent(this); }
108 |         }
109 |
110 |         /// <summary>纯观察，不Sample、不清历史；无效条件或新目标尚未采样时不展示旧Following。</summary>
111 |         public CompanionFollowObservation Observe()
112 |         {
113 |             var valid=TryContext(out var target);
114 |             var distance=target == null || owner == null ? 0 : Vector2.Distance(
115 |                 new Vector2(target.transform.position.x,target.transform.position.z),new Vector2(owner.transform.position.x,owner.transform.position.z));
116 |             var current=valid && sampledTarget==target && revision==session.ControlRevision;
117 |             return new CompanionFollowObservation(current ? state : CompanionFollowState.Inactive,
118 |                 current ? reason : "未分配／不可行动／等待新目标采样",distance,target == null ? "无" : target.ActorId);
119 |         }
120 |
121 |         private void OnDisable() { ResetIntent(); }
122 |         private void OnDestroy() { ResetIntent(); targetSource=null; session=null; owner=null; }
123 |         private static bool Alive(object value) => value != null && (!(value is UnityEngine.Object unityObject) || unityObject != null);
124 |     }
125 |
126 |     public readonly struct CompanionFollowObservation
127 |     {
128 |         public CompanionFollowState State { get; }
129 |         public string Reason { get; }
130 |         public float Distance { get; }
131 |         public string TargetId { get; }
132 |         public CompanionFollowObservation(CompanionFollowState state, string reason, float distance, string targetId)
133 |         { State=state; Reason=reason; Distance=distance; TargetId=targetId; }
134 |     }
135 | }
```

## CompanionFollowDirector

Assets/OSSASTRA/Scripts/Characters/Runtime/CompanionFollowDirector.cs

行数 71；SHA256 3BD51C0841EF6832FEE116EC75A4D108BF1CD710470DFA252A22BE601203CE62

队伍分配。Start在Root后统一预检，bindings只为归还自己取得的绑定；TryGetFollowTarget读取唯一会话，不计算方向或保存策略。旧隐藏字段仅兼容，Observe只转发。

```csharp
001 | /*
002 |  * 脚本：CompanionFollowDirector
003 |  * 功能：场景级目标分配与绑定，读取唯一会话为成员分配Current目标。
004 |  * 边界：不保存角色策略/距离/受阻/位移历史，不执行运动或动作。
005 |  * 协作：Start在Root初始化后预检全组；将各角色自己的Follow接至其Selector，归还仅清自己绑定。
006 |  */
007 | using System.Collections.Generic;
008 | using UnityEngine;
009 |
010 | namespace Ossastra.Characters
011 | {
012 |     public sealed class CompanionFollowDirector : MonoBehaviour, ICharacterFollowTargetSource
013 |     {
014 |         [SerializeField] private CharacterControlCoordinator coordinator;
015 |         // 仅保留旧场序列化迁移证据，不再参与运行时计算；权威参数在每角色CharacterFollow。
016 |         [SerializeField, HideInInspector] private float followStartDistance=2.4f, followStopDistance=1.6f;
017 |         [SerializeField, HideInInspector] private float blockedCheckWindow=.5f, retryTargetDelta=.5f;
018 |         private readonly List<CharacterFollow> bindings = new List<CharacterFollow>();
019 |         private bool started, configured;
020 |         public bool IsAvailable => configured && isActiveAndEnabled && coordinator != null && coordinator.isActiveAndEnabled;
021 |         public string Status { get; private set; } = "未绑定";
022 |         public CharacterControlCoordinator Coordinator => coordinator;
023 |         private void Start() { started=true; BindMembers(); }
024 |         private void OnEnable() { if (started) BindMembers(); }
025 |
026 |         private void BindMembers()
027 |         {
028 |             configured=false;
029 |             if (coordinator == null || !coordinator.isActiveAndEnabled) { Status="控制会话缺失或未启用"; return; }
030 |             var ids=new HashSet<string>();
031 |             // 先检查全部成员；列表只记录待归还的绑定，不成为第二成员/Current权威。
032 |             foreach (var character in coordinator.Characters)
033 |                 if (character == null || !ids.Add(character.ActorId) || character.Follow == null ||
034 |                     !character.Follow.CanAssign(coordinator,this) || !character.CanBindCompanionIntent(coordinator,character.Follow))
035 |                 { Status="角色能力未就绪或分配已被占用"; return; }
036 |             foreach (var character in coordinator.Characters)
037 |             {
038 |                 var follow=character.Follow;
039 |                 if (!follow.Assign(coordinator,this)) { ReleaseBindings(); Status="分配失败，已归还自身绑定"; return; }
040 |                 bindings.Add(follow);
041 |                 if (!character.BindCompanionIntent(coordinator,follow))
042 |                 { ReleaseBindings(); Status="接线失败，已归还自身绑定"; return; }
043 |             }
044 |             configured=true; Status="队伍目标分配 / 角色自身计算跟随";
045 |         }
046 |
047 |         public bool TryGetFollowTarget(CharacterRoot follower, out CharacterRoot target)
048 |         {
049 |             target=null;
050 |             if (!IsAvailable || !coordinator.IsIntentSamplingAllowed || follower == null ||
051 |                 coordinator.Current == null || follower==coordinator.Current) return false;
052 |             foreach (var character in coordinator.Characters)
053 |                 if (character==follower) { target=coordinator.Current; return true; }
054 |             return false;
055 |         }
056 |
057 |         // 历史开发调用的只读转发；本体观察才是唯一状态来源，Director没有镜像缓存。
058 |         public CompanionFollowObservation Observe(CharacterRoot character)
059 |             => character != null && character.Follow != null ? character.Follow.Observe() :
060 |                 new CompanionFollowObservation(CharacterCore.CompanionFollowState.Inactive,"缺少角色跟随能力",0,"无");
061 |
062 |         private void ReleaseBindings()
063 |         {
064 |             configured=false;
065 |             foreach (var follow in bindings) if (follow != null) follow.Unassign(this);
066 |             bindings.Clear();
067 |         }
068 |         private void OnDisable() { ReleaseBindings(); Status="目标分配关闭，玩家仍由原会话管理"; }
069 |         private void OnDestroy() { ReleaseBindings(); }
070 |     }
071 | }
```

## CharacterRoot

Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterRoot.cs

行数 186；SHA256 67C9D17A4B5D1E493D835134239A4A4F802A19A7049B5486D5E97E4FEE9DF04E

组合根。新增可选Follow槽并在原子系统初始化后接线，仅允许绑定本角色Follow；暂停/失焦/禁用通知自身能力清历史。原Update依旧一次Read、动作推进、运动与表现。

```csharp
001 | /*
002 |  * 脚本：CharacterRoot
003 |  * 功能：组织单角色的接口接线、输入、动作事实、运动和表现。
004 |  * 边界：持有本角色运行时；不实现设备绑定、目标HP写入或开发GUI。
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
027 |         [Header("Prototype timing — not final balance")]
028 |         [SerializeField] private float releaseAt = .3f, moveReadyAt = .5f, attackReadyAt = .8f, finishAt = 1.1f;
029 |         [SerializeField, Min(1)] private int damage = 12;
030 |         [SerializeField] private bool verboseTrace;
031 |         private readonly ControlAuthority authority = new ControlAuthority();
032 |         private readonly AttackIntent intent = new AttackIntent();
033 |         private ICharacterInputSource inputSource;
034 |         private CharacterIntentSelector intentSelector;
035 |         private ICharacterMovement mover;
036 |         private ICharacterOrientation orienter;
037 |         private ICharacterPresentation presentation;
038 |         private ICharacterHitResolver hitResolver;
039 |         private BasicActionRuntime runtime;
040 |         private BasicAttackDefinition definition;
041 |         private bool ready, focused = true, paused, applicationPaused;
042 |         private string lastFact = "Idle";
043 |         public CharacterDataBus DataBus { get; private set; }
044 |         public string ActorId => actorId;
045 |         public string LastFact => lastFact;
046 |         public bool IsPaused => paused || applicationPaused;
047 |         public bool ActionActive => DataBus != null && DataBus.ActionActive;
048 |         public bool CanSimulateIntent => ready && isActiveAndEnabled && focused && !IsPaused;
049 |         public CharacterFollow Follow => follow;
050 |         public CharacterIntentKind EffectiveIntentKind => intentSelector == null ? CharacterIntentKind.Neutral : intentSelector.EffectiveKind;
051 |
052 |         public bool CanBindCompanionIntent(ICharacterControlSession session, ICompanionIntentSource source)
053 |             => ready && follow != null && ReferenceEquals(source,follow) && intentSelector != null && intentSelector.CanBind(session,source);
054 |         public bool BindCompanionIntent(ICharacterControlSession session, ICompanionIntentSource source)
055 |             => CanBindCompanionIntent(session,source) && intentSelector.Bind(session,source);
056 |         public void ClearCompanionIntent(ICompanionIntentSource owner)
057 |         { if (intentSelector != null) intentSelector.Clear(owner); }
058 |
059 |         private void Awake()
060 |         {
061 |             if (viewCamera == null) viewCamera = Camera.main;
062 |             if (movement == null || orientation == null || view == null || melee == null || input == null || viewCamera == null)
063 |             { Debug.LogError("Character requires registered input/movement/orientation/view/melee/camera", this); enabled = false; return; }
064 |             // Inspector保留具体组件槽；运行期通过窄接口消费，不反向依赖开发GUI或测试靶。
065 |             // 每角色只有这一条有效Frame入口；没有Director绑定时保持原玩家适配器行为。
066 |             intentSelector = new CharacterIntentSelector(this,input);
067 |             inputSource = intentSelector;
068 |             mover = movement;
069 |             orienter = orientation;
070 |             presentation = view;
071 |             hitResolver = melee;
072 |             if (!inputSource.IsConfigured) { Debug.LogError("Character actions are not configured", this); enabled = false; return; }
073 |             try
074 |             {
075 |                 definition = new BasicAttackDefinition(releaseAt, moveReadyAt, attackReadyAt, finishAt, damage);
076 |                 runtime = new BasicActionRuntime(actorId);
077 |             }
078 |             catch (ArgumentException error) { Debug.LogError("Character configuration: " + error.Message, this); enabled = false; return; }
079 |             // 每角色独立Bus只读取原所有者；只有确需Bus的运动和表现执行初始化。
080 |             DataBus = new CharacterDataBus(actorId, inputSource, mover, orienter, runtime, authority);
081 |             ready = InitializeSubsystems(DataBus, mover, presentation);
082 |             if (!ready) { Debug.LogError("Character subsystem initialization failed", this); enabled = false; }
083 |             // 可选能力只接受本角色初始化；无组件/无assignment的旧场保持普通玩家行为。
084 |             if (follow != null && !follow.Initialize(this)) Debug.LogWarning("Character follow unavailable: configuration or owner invalid", this);
085 |         }
086 |
087 |         private static bool InitializeSubsystems(CharacterDataBus dataBus, params ICharacterSubsystem[] subsystems)
088 |         {
089 |             foreach (var subsystem in subsystems) if (subsystem == null || !subsystem.Initialize(dataBus)) return false;
090 |             return true;
091 |         }
092 |
093 |         private void Update()
094 |         {
095 |             if (!ready) return;
096 |             // 选择器只采一个源；伙伴只能提供移动，切人屏障为空帧，动作时钟仍继续。
097 |             inputSource.Read();
098 |             var frame = DataBus.Input;
099 |             if (frame.pause) SetPaused(!paused);
100 |             if (frame.cancel) CancelAction("player-cancel");
101 |             var canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
102 |             if (focused && !IsPaused)
103 |                 orienter.ResolveDirections(frame.move, PointerDirection(frame), frame.hasDirectionalAim ? frame.aimDirection : Vector2.zero, canMove);
104 |             // 输入边沿不等于动作成功：焦点、UI和Runtime的暂停/权限/Ready资格分别把关。
105 |             if (intent.Observe(frame.attackHeld) && focused && !frame.attackPointerBlocked)
106 |             {
107 |                 var request = runtime.Request(definition, "player-input", authority, IsPaused);
108 |                 if (request.PreviousTerminal != null) Observe(request.PreviousTerminal);
109 |                 if (request.Accepted) { Observe(request.Fact); orienter.FaceAttackDirection(); }
110 |                 else lastFact = "Rejected: " + request.Reason;
111 |             }
112 |             // Accepted只建立动作身份；只有Released带一次性命中凭据。切人不会重发事实。
113 |             foreach (var fact in runtime.Advance(Time.deltaTime, IsPaused || !focused))
114 |             {
115 |                 Observe(fact);
116 |                 if (fact.Release != null)
117 |                 {
118 |                     // 在释放当帧按值采样方向；不是接受动作时锁定，前摇中仍可重新瞄准。
119 |                     var releaseDirection = orienter.AttackDirection;
120 |                     orienter.FaceAttackDirection();
121 |                     hitResolver.Resolve(fact.Release, releaseDirection);
122 |                 }
123 |             }
124 |             // 本帧可能跨过MoveReady；推进事实后重新查询许可，Run依据真实位移。
125 |             canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
126 |             mover.Tick(canMove, IsPaused || !focused ? 0 : Time.deltaTime);
127 |             presentation.SetPaused(IsPaused || !focused);
128 |             presentation.Present();
129 |         }
130 |
131 |         private Vector3? PointerDirection(CharacterInputFrame frame)
132 |         {
133 |             if (!frame.hasPointer) return null;
134 |             var plane = new Plane(Vector3.up, transform.position);
135 |             var ray = viewCamera.ScreenPointToRay(frame.pointer);
136 |             if (!plane.Raycast(ray, out var distance)) return null;
137 |             var direction = ray.GetPoint(distance) - transform.position;
138 |             direction.y = 0;
139 |             return direction.sqrMagnitude < .0001f ? (Vector3?)null : direction;
140 |         }
141 |
142 |         private void Observe(ActionFact fact)
143 |         {
144 |             lastFact = fact.Kind + " @" + fact.At.ToString("F3") + " " + fact.ActionId.ToString("N").Substring(0, 8);
145 |             if (fact.Reason != null) lastFact += " " + fact.Reason;
146 |             if (verboseTrace) Debug.Log("Character " + lastFact, this);
147 |         }
148 |
149 |         public void CancelAction(string reason)
150 |         {
151 |             // 取消必须具名；抑制仍按住的输入，不能把下一帧误判为新请求。
152 |             intent.SuppressUntilRelease();
153 |             var terminal = runtime == null ? null : runtime.Cancel(reason);
154 |             if (terminal != null) Observe(terminal);
155 |         }
156 |
157 |         public void SetPaused(bool value) { paused = value; intent.SuppressUntilRelease(); if (follow != null) follow.ResetIntent(); }
158 |         public void SetAttackRestriction(object owner, bool restricted, string reason = "attack-restricted")
159 |         {
160 |             if (restricted) { authority.Block(owner, ControlChannel.Attack); CancelAction(reason); }
161 |             else authority.Release(owner);
162 |         }
163 |
164 |         private void OnApplicationFocus(bool hasFocus)
165 |         { focused = hasFocus; if (follow != null) follow.ResetIntent(); if (!hasFocus) CancelAction("focus-lost"); else intent.SuppressUntilRelease(); }
166 |         private void OnApplicationPause(bool value)
167 |         { applicationPaused = value; if (follow != null) follow.ResetIntent(); if (value) intent.SuppressUntilRelease(); }
168 |         private void OnEnable() { intent.SuppressUntilRelease(); if (intentSelector != null) intentSelector.Clear(); }
169 |         private void OnDisable() { CancelAction("disabled"); if (follow != null) follow.ResetIntent(); if (intentSelector != null) intentSelector.Clear(); if (ready && presentation != null) presentation.SetPaused(false); }
170 |         private void OnDestroy()
171 |         {
172 |             // 终止本角色动作后解除本对象引用；不清其它角色或场景控制权威。
173 |             CancelAction("destroyed");
174 |             ready = false;
175 |             DataBus = null;
176 |             runtime = null;
177 |             inputSource = null;
178 |             if (intentSelector != null) intentSelector.Clear();
179 |             intentSelector = null;
180 |             mover = null;
181 |             orienter = null;
182 |             presentation = null;
183 |             hitResolver = null;
184 |         }
185 |     }
186 | }
```

## CharacterIntentSelector

Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterIntentSelector.cs

行数 72；SHA256 534D5533DD9B70021E2166F6D45FC2ED3EF9B97AC032FB0571783825E345D6F4

有效帧唯一入口。Read的selectedKind局部变量保护provider清旧帧回调之后的新来源身份；既有代际/即时许可/owner清帧约束不变。

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
026 |         private bool ValidSample => sampledFrame == Time.frameCount && character != null && character.isActiveAndEnabled &&
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
053 |             if (character == null || !character.isActiveAndEnabled) return default;
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

## CharacterContracts

Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterContracts.cs

行数 87；SHA256 4A74159A600B0BA99036468BAD8BCD52B6F917A197AFD3DE346C3CE2D437884D

新增最小目标分配查询，实际由Director实现、Follow消费；其余契约不扩成万能角色AI。

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
013 |     /// <summary>队伍只分配合法目标；角色能力自行计算移动，不从此契约取得方向或策略状态。</summary>
014 |     public interface ICharacterFollowTargetSource
015 |     {
016 |         bool TryGetFollowTarget(CharacterRoot follower, out CharacterRoot target);
017 |     }
018 |     /// <summary>场景唯一控制权的只读视图；代际和共享屏障同时约束玩家与伙伴来源。</summary>
019 |     public interface ICharacterControlSession
020 |     {
021 |         CharacterRoot Current { get; }
022 |         IReadOnlyList<CharacterRoot> Characters { get; }
023 |         bool IsIntentSamplingAllowed { get; }
024 |         int ControlRevision { get; }
025 |     }
026 |     /// <summary>伙伴策略只提供移动候选帧；选择器剥离攻击、暂停、取消和开发命令。</summary>
027 |     public interface ICompanionIntentSource
028 |     {
029 |         bool IsAvailable { get; }
030 |         CharacterInputFrame ReadCompanionIntent(CharacterRoot character, float delta);
031 |     }
032 |     /// <summary>玩家源的实时许可；外层已采样帧在许可撤回时同样失效，兼容未挂跟随模块的旧场。</summary>
033 |     public interface ICharacterInputAvailability
034 |     {
035 |         bool HasPlayerInput { get; }
036 |     }
037 |     /// <summary>由场景实控权威回答谁可以采样玩家输入；输入源不保存第二份选中状态。</summary>
038 |     public interface ICharacterPlayerControlSource
039 |     {
040 |         bool IsPlayerInputAllowed(ICharacterInputSource source);
041 |     }
042 |     /// <summary>仅为确需角色Bus的模块提供显式初始化；失败阻止角色启动。</summary>
043 |     public interface ICharacterSubsystem { bool Initialize(CharacterDataBus dataBus); }
044 |     /// <summary>提供单角色唯一有效输入快照，不判定动作是否成功。</summary>
045 |     public interface ICharacterInputSource
046 |     {
047 |         CharacterInputFrame CurrentFrame { get; }
048 |         bool IsConfigured { get; }
049 |         CharacterInputFrame Read();
050 |     }
051 |     /// <summary>执行实际运动并报告结果；不决定攻击朝向。</summary>
052 |     public interface ICharacterMovement : ICharacterSubsystem
053 |     {
054 |         bool IsMoving { get; }
055 |         Vector3 Velocity { get; }
056 |         void Tick(bool allowed, float delta);
057 |         void Teleport(Vector3 position);
058 |     }
059 |     /// <summary>分别维护攻击方向与可见翻转，由Root显式调度。</summary>
060 |     public interface ICharacterOrientation
061 |     {
062 |         Vector3 AttackDirection { get; }
063 |         float VisualFacingX { get; }
064 |         void ResolveDirections(Vector2 moveInput, Vector3? pointerDirection, Vector2 stickDirection, bool movementFacing);
065 |         void OrientTowards(Vector3 worldPosition);
066 |         void FaceAttackDirection();
067 |     }
068 |     /// <summary>从角色状态更新表现与暂停，不反向产生命中。</summary>
069 |     public interface ICharacterPresentation : ICharacterSubsystem
070 |     {
071 |         string CurrentClip { get; }
072 |         void SetPaused(bool paused);
073 |         void Present();
074 |     }
075 |     /// <summary>消费一次释放与方向快照，执行世界目标筛选。</summary>
076 |     public interface ICharacterHitResolver
077 |     {
078 |         string LastResult { get; }
079 |         void Resolve(AttackRelease release, Vector3 releaseDirection);
080 |     }
081 |     /// <summary>提供同一Health和命中反馈；生产命中模块不依赖开发靶类型。</summary>
082 |     public interface ICharacterHitTarget
083 |     {
084 |         Health Health { get; }
085 |         void ShowHit();
086 |     }
087 | }
```

## CompanionFollowDebugView

Assets/OSSASTRA/Scripts/Development/Combat/CompanionFollowDebugView.cs

行数 52；SHA256 D4C8074798D99CC00190B3EA3D42C9455D8ACABEBDB56EEE9CBA90B79B9C43E9

运行时开发面板直接读取各Follow，提供单体和整体启停；只登记自己的指针区域，不生成方向或推进策略。

```csharp
001 | /*
002 |  * 脚本：CompanionFollowDebugView
003 |  * 功能：展示跟随来源、目标、距离/阈值、状态与拒绝原因，提供可达开发入口。
004 |  * 边界：只读业务状态；切换/禁启通过真实入口，不伪造GUI成功、不移动角色。
005 |  * 协作：直接观察各角色Follow与分配源；登记自己的GUI阻挡区域，禁用只清理自己。
006 |  */
007 | using Ossastra.Characters;
008 | using UnityEngine;
009 |
010 | namespace Ossastra.Development
011 | {
012 |     public sealed class CompanionFollowDebugView : MonoBehaviour
013 |     {
014 |         [SerializeField] private CompanionFollowDirector director;
015 |         private static readonly Rect panel = new Rect(12,294,465,300);
016 |         private void OnEnable() { Register(true); }
017 |         private void OnDisable() { Register(false); }
018 |         private void Register(bool add)
019 |         {
020 |             if (director == null || director.Coordinator == null) return;
021 |             foreach (var character in director.Coordinator.Characters)
022 |             {
023 |                 var input=character == null ? null : character.GetComponent<CharacterInputHandler>();
024 |                 if (input == null) continue;
025 |                 if (add) input.RegisterPointerRegion(this,panel); else input.RemovePointerRegion(this);
026 |             }
027 |         }
028 |         private void OnGUI()
029 |         {
030 |             if (director == null || director.Coordinator == null) return;
031 |             var coordinator=director.Coordinator;
032 |             GUILayout.BeginArea(panel,GUI.skin.box);
033 |             GUILayout.Label("最小伙伴跟随候选 · 临时同素材 · Tab/Select切换");
034 |             GUILayout.Label("Current: " + (coordinator.Current == null ? "无" : coordinator.Current.ActorId) + " | " + director.Status);
035 |             foreach (var character in coordinator.Characters)
036 |             {
037 |                 if (character == null) { GUILayout.Label("成员引用缺失"); continue; }
038 |                 var follow=character.Follow;
039 |                 if (follow == null) { GUILayout.Label(character.ActorId+" | 无Follow组件"); continue; }
040 |                 var observation=follow.Observe();
041 |                 GUILayout.Label(character.ActorId + " | " + character.EffectiveIntentKind + " | " + observation.State + " | 距离 " + observation.Distance.ToString("F2"));
042 |                 GUILayout.Label("目标 " + observation.TargetId + " | 起停 " + follow.StartDistance.ToString("F2")+"/"+follow.StopDistance.ToString("F2") + " | " + observation.Reason);
043 |                 if (GUILayout.Button(character.ActorId+"：切换本体Follow启用")) follow.enabled=!follow.enabled;
044 |             }
045 |             if (GUILayout.Button("合法路径：切换实控角色")) coordinator.SwitchNext();
046 |             if (GUILayout.Button("非法/恢复：切换跟随启用")) director.enabled=!director.enabled;
047 |             if (GUILayout.Button("清理：禁用后重新启用跟随，重新采样")) { director.enabled=false; director.enabled=true; }
048 |             if (GUILayout.Button("检查：输出当前权威与状态")) Debug.Log(coordinator.Status + " | " + director.Status,this);
049 |             GUILayout.EndArea();
050 |         }
051 |     }
052 | }
```

## CharacterFollowEditor

Assets/OSSASTRA/Scripts/Development/Editor/CharacterFollowEditor.cs

行数 32；SHA256 EE41F2C3504ADD6CAF01406B93F58217CD528B96A157BC024D8F414D416BED7E

仅Editor的Inspector扩展。显示默认配置并纯读Observe；不保存运行状态镜像，不参与游戏决策。

```csharp
001 | /*
002 |  * 脚本：CharacterFollowEditor
003 |  * 功能：在角色Inspector中显示Follow真实只读运行状态，参数仍由原序列化字段配置。
004 |  * 边界：仅编辑器显示；不采样/重置策略，不在角色上新增可写状态镜像。
005 |  * 协作：读取本体Observe，普通游戏运行不依赖本编辑器脚本。
006 |  */
007 | using Ossastra.Characters;
008 | using UnityEditor;
009 | using UnityEngine;
010 |
011 | namespace Ossastra.Development.Editor
012 | {
013 |     [CustomEditor(typeof(CharacterFollow))]
014 |     public sealed class CharacterFollowEditor : UnityEditor.Editor
015 |     {
016 |         public override void OnInspectorGUI()
017 |         {
018 |             DrawDefaultInspector();
019 |             if (!Application.isPlaying) return;
020 |             var follow=target as CharacterFollow;
021 |             if (follow == null) return;
022 |             var observation=follow.Observe();
023 |             EditorGUILayout.Space();
024 |             EditorGUILayout.LabelField("本体运行状态（只读）",EditorStyles.boldLabel);
025 |             EditorGUILayout.LabelField("状态",observation.State.ToString());
026 |             EditorGUILayout.LabelField("目标",observation.TargetId);
027 |             EditorGUILayout.LabelField("距离",observation.Distance.ToString("F2"));
028 |             EditorGUILayout.LabelField("原因",observation.Reason);
029 |         }
030 |         public override bool RequiresConstantRepaint() => Application.isPlaying;
031 |     }
032 | }
```
