# M02-C 本轮源码审查

仅本轮新增/修改9份自有源码，完整当前行号与语句块解释。M02A历史待审记录不改。所有范围均待用户审查；测试通过不是架构签字。

## Assets/OSSASTRA/Scripts/Characters/Core/CharacterIntentPolicy.cs

SHA256：EA9814A24E7CAABEBE38CC53E2C54002A59D51857E7025CD09A40C736E592CA9

```text
001 | /*
002 |  * 脚本：CharacterIntentPolicy
003 |  * 功能：从会话许可和来源可用性中选唯一Player／Companion／Neutral。
004 |  * 边界：无设备、场景或动作状态；只返回选择结果，不拥有第二个Current。
005 |  * 协作：运行时CharacterIntentSelector调用；纯合同可独立验证权威边界。
006 |  */
007 | namespace Ossastra.CharacterCore
008 | {
009 |     public enum CharacterIntentKind { Neutral, Player, Companion }
010 |     public static class CharacterIntentPolicy
011 |     {
012 |         public static CharacterIntentKind Select(bool hasSession, bool sessionAllows, bool isCurrent, bool hasCompanion)
013 |         {
014 |             if (!hasSession) return CharacterIntentKind.Player;
015 |             // 无合法Current或处于移交屏障时，两条来源都不生效。
016 |             if (!sessionAllows) return CharacterIntentKind.Neutral;
017 |             if (isCurrent) return CharacterIntentKind.Player;
018 |             return hasCompanion ? CharacterIntentKind.Companion : CharacterIntentKind.Neutral;
019 |         }
020 |     }
021 | }
```

| 行区间 | 实际调用、状态与边界 |
|---|---|
| 1–6 | 中文功能/边界/协作文件头，与当前实现同步；不执行业务。 |
| 7–11 | 来源枚举只是Player/Companion/Neutral，不是角色身份或第二个Current。 |
| 12–21 | 选择器传入会话存在/许可/本角色是否Current/伙伴源可用性。无会话沿用玩家适配器；有会话但屏障或无Current时Neutral；Current只Player，否则可用伙伴才Companion。无输入读取、状态写入或Unity依赖。 |

用户结论：待审。变更后重审受影响范围。

## Assets/OSSASTRA/Scripts/Characters/Core/CompanionFollowPolicy.cs

SHA256：A45C38E2457A71D18AB52E0D8A3FD7AB0393E33870DB02DC4991D72465F98009

```text
001 | /*
002 |  * 脚本：CompanionFollowPolicy
003 |  * 功能：按双距离阈值产生归一化跟随方向，并诊断持续受阻。
004 |  * 边界：无Unity依赖，不执行位移/寻路/攻击；实际位移与目标坐标由场景适配器提供。
005 |  * 协作：FollowDirector每个伙伴持有独立策略；只有前帧确有命令才累计无位移时间。
006 |  */
007 | using System;
008 | namespace Ossastra.CharacterCore
009 | {
010 |     public enum CompanionFollowState { Inactive, Following, CloseEnough, Blocked }
011 |     public readonly struct CompanionFollowDecision
012 |     {
013 |         public CompanionFollowState State { get; }
014 |         public double MoveX { get; }
015 |         public double MoveZ { get; }
016 |         public CompanionFollowDecision(CompanionFollowState state, double x=0, double z=0) { State=state; MoveX=x; MoveZ=z; }
017 |     }
018 |     public sealed class CompanionFollowPolicy
019 |     {
020 |         private readonly double start, stop, blockWindow, retryDelta;
021 |         private bool following, blocked, commanded;
022 |         private double stalled, blockedTargetX, blockedTargetZ;
023 |
024 |         public CompanionFollowPolicy(double start, double stop, double blockWindow, double retryDelta)
025 |         {
026 |             if (!Finite(start) || !Finite(stop) || !Finite(blockWindow) || !Finite(retryDelta) ||
027 |                 stop <= 0 || start <= stop || blockWindow <= 0 || retryDelta <= 0)
028 |                 throw new ArgumentException("Invalid companion follow thresholds");
029 |             this.start=start; this.stop=stop; this.blockWindow=blockWindow; this.retryDelta=retryDelta;
030 |         }
031 |
032 |         public void Reset() { following=false; blocked=false; commanded=false; stalled=0; }
033 |
034 |         /// <summary>提供水平相对向量、绝对目标坐标、上帧真实位移与模拟步长；不可行动时清策略并输出零。</summary>
035 |         public CompanionFollowDecision Sample(double x,double z,double targetX,double targetZ,double travel,double delta,bool eligible)
036 |         {
037 |             if (!eligible || !Finite(x) || !Finite(z) || !Finite(targetX) || !Finite(targetZ) ||
038 |                 !Finite(travel) || !Finite(delta) || delta <= 0 || travel < 0)
039 |             { Reset(); return new CompanionFollowDecision(CompanionFollowState.Inactive); }
040 |             var distance=Math.Sqrt(x*x+z*z);
041 |             if (!Finite(distance)) { Reset(); return new CompanionFollowDecision(CompanionFollowState.Inactive); }
042 |             if (distance <= stop)
043 |             { Reset(); return new CompanionFollowDecision(CompanionFollowState.CloseEnough); }
044 |             if (blocked)
045 |             {
046 |                 var dx=targetX-blockedTargetX; var dz=targetZ-blockedTargetZ;
047 |                 if (dx*dx+dz*dz < retryDelta*retryDelta)
048 |                     return new CompanionFollowDecision(CompanionFollowState.Blocked);
049 |                 // 领队显著移动才恢复尝试；不宣称可自行绕墙或在静止目标下持续寻找新路径。
050 |                 blocked=false; stalled=0; commanded=false;
051 |             }
052 |             if (!following && distance < start) return new CompanionFollowDecision(CompanionFollowState.CloseEnough);
053 |             following=true;
054 |             stalled=commanded && travel < .002 ? stalled+delta : 0;
055 |             if (stalled >= blockWindow)
056 |             {
057 |                 blocked=true; commanded=false; blockedTargetX=targetX; blockedTargetZ=targetZ;
058 |                 return new CompanionFollowDecision(CompanionFollowState.Blocked);
059 |             }
060 |             commanded=true;
061 |             return new CompanionFollowDecision(CompanionFollowState.Following,x/distance,z/distance);
062 |         }
063 |
064 |         private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
065 |     }
066 | }
```

| 行区间 | 实际调用、状态与边界 |
|---|---|
| 1–6 | 中文功能/边界/协作文件头，与当前实现同步；不执行业务。 |
| 7–19 | 纯策略依赖System；状态/决策只表达跟随意图与诊断，不是角色动作时钟。readonly决策保存方向，不执行运动。 |
| 20–23 | 每个角色独立阈值/滞回/受阻计时。commanded标前次确有跟随命令，避免把空闲当卡住；blockedTarget保存受阻时目标坐标而非第二领队对象。 |
| 24–31 | 构造前验证有限正值、start严格大于stop，非法配置拒绝；阈值来自Inspector候选，非正式玩法规则。 |
| 32–34 | Reset清跟随/受阻/前帧命令/无位移累计，不改角色Runtime/HP。 |
| 35–39 | 输入是XZ相对向量、目标绝对坐标、前帧水平实际路程、步长与资格；非法数值/不可行动/非正步长输出Inactive并清策略。 |
| 40–43 | 距离有限性检查；进入停止区立即清策略返回CloseEnough。停止和启动阈值不同，停止后在两者之间不反复启停。 |
| 44–51 | 受阻状态不输出运动；目标移动未到retryDelta时保持。目标显著移动才解除受阻并清前帧命令，重新尝试；不宣称能绕障或静止目标下自动发现新路线。 |
| 52–63 | 尚未启动且距离不到start保持CloseEnough；已启动则在stop前继续。仅前帧发过命令且实际位移小于.002才累计delta，正常实际移动清计时。达到窗口保存目标坐标、Blocked零方向；否则归一输出方向。 |
| 64–66 | 拒绝NaN/Infinity；辅助验证不维护额外游戏状态。 |

用户结论：待审。变更后重审受影响范围。

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterContracts.cs

SHA256：2AB3866A56D5DF6CC5626DCA6B01FEA77129CE72F679CBB35B7E56F3443EB6FF

```text
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
013 |     /// <summary>场景唯一控制权的只读视图；代际和共享屏障同时约束玩家与伙伴来源。</summary>
014 |     public interface ICharacterControlSession
015 |     {
016 |         CharacterRoot Current { get; }
017 |         IReadOnlyList<CharacterRoot> Characters { get; }
018 |         bool IsIntentSamplingAllowed { get; }
019 |         int ControlRevision { get; }
020 |     }
021 |     /// <summary>伙伴策略只提供移动候选帧；选择器剥离攻击、暂停、取消和开发命令。</summary>
022 |     public interface ICompanionIntentSource
023 |     {
024 |         bool IsAvailable { get; }
025 |         CharacterInputFrame ReadCompanionIntent(CharacterRoot character, float delta);
026 |     }
027 |     /// <summary>玩家源的实时许可；外层已采样帧在许可撤回时同样失效，兼容未挂跟随模块的旧场。</summary>
028 |     public interface ICharacterInputAvailability
029 |     {
030 |         bool HasPlayerInput { get; }
031 |     }
032 |     /// <summary>由场景实控权威回答谁可以采样玩家输入；输入源不保存第二份选中状态。</summary>
033 |     public interface ICharacterPlayerControlSource
034 |     {
035 |         bool IsPlayerInputAllowed(ICharacterInputSource source);
036 |     }
037 |     /// <summary>仅为确需角色Bus的模块提供显式初始化；失败阻止角色启动。</summary>
038 |     public interface ICharacterSubsystem { bool Initialize(CharacterDataBus dataBus); }
039 |     /// <summary>提供单角色唯一有效输入快照，不判定动作是否成功。</summary>
040 |     public interface ICharacterInputSource
041 |     {
042 |         CharacterInputFrame CurrentFrame { get; }
043 |         bool IsConfigured { get; }
044 |         CharacterInputFrame Read();
045 |     }
046 |     /// <summary>执行实际运动并报告结果；不决定攻击朝向。</summary>
047 |     public interface ICharacterMovement : ICharacterSubsystem
048 |     {
049 |         bool IsMoving { get; }
050 |         Vector3 Velocity { get; }
051 |         void Tick(bool allowed, float delta);
052 |         void Teleport(Vector3 position);
053 |     }
054 |     /// <summary>分别维护攻击方向与可见翻转，由Root显式调度。</summary>
055 |     public interface ICharacterOrientation
056 |     {
057 |         Vector3 AttackDirection { get; }
058 |         float VisualFacingX { get; }
059 |         void ResolveDirections(Vector2 moveInput, Vector3? pointerDirection, Vector2 stickDirection, bool movementFacing);
060 |         void OrientTowards(Vector3 worldPosition);
061 |         void FaceAttackDirection();
062 |     }
063 |     /// <summary>从角色状态更新表现与暂停，不反向产生命中。</summary>
064 |     public interface ICharacterPresentation : ICharacterSubsystem
065 |     {
066 |         string CurrentClip { get; }
067 |         void SetPaused(bool paused);
068 |         void Present();
069 |     }
070 |     /// <summary>消费一次释放与方向快照，执行世界目标筛选。</summary>
071 |     public interface ICharacterHitResolver
072 |     {
073 |         string LastResult { get; }
074 |         void Resolve(AttackRelease release, Vector3 releaseDirection);
075 |     }
076 |     /// <summary>提供同一Health和命中反馈；生产命中模块不依赖开发靶类型。</summary>
077 |     public interface ICharacterHitTarget
078 |     {
079 |         Health Health { get; }
080 |         void ShowHit();
081 |     }
082 | }
```

| 行区间 | 实际调用、状态与边界 |
|---|---|
| 1–6 | 中文功能/边界/协作文件头，与当前实现同步；不执行业务。 |
| 7–13 | 声明窄契约所需类型，集中同领域接口但不实现调度。 |
| 14–21 | 新增只读会话：Current/Characters来自原Coordinator；IsIntentSamplingAllowed是共享屏障，ControlRevision使已采样的旧来源立即失效。Director/Selector是真实消费者。 |
| 22–27 | 新增伙伴移动候选源契约，由Director实现、Selector消费；可用性影响旧帧有效性，返回帧被Selector剥离玩家专属字段。 |
| 28–32 | 新增可选玩家实时许可，由原InputHandler实现，Selector消费；解决旧M02A不挂Director时外层帧在撤权后不归零的真实RED。 |
| 33–46 | 原玩家许可、必要Bus初始化与输入快照协议保留；不强迫所有组件拥有假Initialize。 |
| 47–82 | 运动、朝向、表现、命中及目标窄协议沿用原职责。现有接口补中文摘要，运行行为不变；Inspector仍保存具体组件引用。 |

用户结论：待审。变更后重审受影响范围。

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterControlCoordinator.cs

SHA256：1F4CEB2C68E0CA291319B291514436F06DAC7113D40F8F370330718C6D2597C1

```text
001 | /*
002 |  * 脚本：CharacterControlCoordinator
003 |  * 功能：保存同组角色唯一的玩家实控对象，并读取Inspector切换输入。
004 |  * 边界：不接管各角色动作时钟、HP或AI；不使用全局单例。
005 |  * 协作：在Root之前更新；玩家与伙伴选择器只读查询许可/代际，不复制Current或选中布尔。
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
032 |             Current != null && Current.isActiveAndEnabled && Current.DataBus != null && Time.frameCount > handoverFrame;
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
056 |             configured = valid;
057 |             if (!valid) { Status = "角色／身份／输入配置无效，输入关闭"; enabled = false; }
058 |         }
059 |
060 |         private void OnEnable()
061 |         {
062 |             switchIntent.SuppressUntilRelease();
063 |             if (!configured) return;
064 |             switchAction.Enable();
065 |             Select(characters[initialIndex]);
066 |         }
067 |
068 |         private void Update()
069 |         {
070 |             if (Current != null && (!Current.isActiveAndEnabled || Current.DataBus == null)) Revoke("当前角色不可用");
071 |             var held = switchAction != null && switchAction.enabled && switchAction.IsPressed();
072 |             if (switchIntent.Observe(held) && focused && !applicationPaused) SwitchNext();
073 |         }
074 |
075 |         /// <summary>只有Current对应的输入源可消费玩家输入；切换当帧新源禁止采样。</summary>
076 |         public bool IsPlayerInputAllowed(ICharacterInputSource source)
077 |         {
078 |             if (!IsIntentSamplingAllowed) return false;
079 |             for (var i = 0; i < characters.Length; i++)
080 |                 if (characters[i] == Current) return ReferenceEquals(inputs[i], source);
081 |             return false;
082 |         }
083 |
084 |         /// <summary>场景级切人入口；不会取消、复制或重建任一角色的已接受动作。</summary>
085 |         public bool SwitchNext()
086 |         {
087 |             if (!configured || !isActiveAndEnabled || !focused || applicationPaused) return false;
088 |             var index = Array.IndexOf(characters, Current);
089 |             for (var step = 1; step <= characters.Length; step++)
090 |             {
091 |                 var candidate = characters[(index + step) % characters.Length];
092 |                 if (candidate != null && candidate.isActiveAndEnabled && candidate.DataBus != null)
093 |                 { Select(candidate); return true; }
094 |             }
095 |             Revoke("没有可控制角色");
096 |             return false;
097 |         }
098 |
099 |         private void Select(CharacterRoot next)
100 |         {
101 |             // 先撤回旧权威并清所有快照，再授予新身份。整帧禁入避免外部晚调用导致同帧双控。
102 |             Current = null;
103 |             ControlRevision++;
104 |             foreach (var input in inputs) if (input != null) input.ResetPlayerInput();
105 |             handoverFrame = Time.frameCount;
106 |             Current = next;
107 |             Status = next == null ? "无实控" : "玩家输入 → " + next.ActorId;
108 |         }
109 |
110 |         private void Revoke(string reason) { Select(null); Status = reason; }
111 |         private void OnDisable()
112 |         {
113 |             if (switchAction != null) switchAction.Disable();
114 |             switchIntent.SuppressUntilRelease();
115 |             // 未取得绑定所有权的失败实例不能清理其它协调者的快照或重武装状态。
116 |             if (bindingsOwned)
117 |             {
118 |                 if (configured) Revoke("协调者禁用，输入关闭");
119 |                 else foreach (var input in inputs) if (input != null) input.ResetPlayerInput();
120 |             }
121 |         }
122 |         private void OnApplicationFocus(bool value)
123 |         {
124 |             focused = value;
125 |             ControlRevision++;
126 |             switchIntent.SuppressUntilRelease();
127 |             if (bindingsOwned) foreach (var input in inputs) if (input != null) input.ResetPlayerInput();
128 |         }
129 |         private void OnApplicationPause(bool value)
130 |         {
131 |             applicationPaused = value;
132 |             ControlRevision++;
133 |             switchIntent.SuppressUntilRelease();
134 |             if (bindingsOwned) foreach (var input in inputs) if (input != null) input.ResetPlayerInput();
135 |         }
136 |         // 输入仍保留“曾受管理”的关系；本对象销毁后查询失败关闭，不能突然恢复多个玩家源。
137 |         private void OnDestroy() { if (switchAction != null) switchAction.Dispose(); }
138 |     }
139 | }
```

| 行区间 | 实际调用、状态与边界 |
|---|---|
| 1–6 | 中文功能/边界/协作文件头，与当前实现同步；不执行业务。 |
| 7–14 | 保留InputAction/领域边沿依赖；唯一Current仍属于场景实例。 |
| 15–29 | 在Root前更新；角色成员、初始索引、可配置SwitchAction保留。Current是唯一选择真值，inputs是其成员输入引用。 |
| 30–33 | 新增只读代际与全来源许可：启动配置/组件/焦点/应用暂停/有效Current/Root就绪/移交帧共同约束Player和AI。无第二个选择状态。 |
| 34–59 | 沿原流程验证配置和身份，先完整检查可绑定性后统一绑定；错误配置已知成员关闭，第二owner拒绝抢占。 |
| 60–67 | 重启切换键要求松开，启用Action后选initialIndex。 |
| 68–75 | Current不可用时撤回输入；SwitchAction经边沿后调用SwitchNext，暂停/焦点规则保持。 |
| 76–84 | 复用新增全来源许可，再按原Current查唯一玩家输入对象，不给伙伴伪造玩家许可。 |
| 85–98 | 有限循环选择有效Root；不重建状态或取消动作，无有效成员时撤权。 |
| 99–109 | 先清Current并递增ControlRevision，使所有旧选择器快照立即失效；再清玩家物理输入快照与武装，记录屏障帧，设新Current。 |
| 110–121 | 具名撤回；禁用只清拥有绑定的输入，不干扰失败抢占者的原owner。 |
| 122–128 | 焦点变化递增代际、重武装切换、清本owner输入；AI与玩家旧帧均失效。 |
| 129–139 | 应用暂停变化同样递增代际与清理；销毁释放自身Action，不把其它角色变成默认玩家。 |

用户结论：待审。变更后重审受影响范围。

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterInputHandler.cs

SHA256：73AA2CBCF0399FB2CAFC422644607C2ADABDA484073A6FF48693ABD059491DCC

```text
001 | /*
002 |  * 脚本：CharacterInputHandler
003 |  * 功能：从Inspector配置的InputAction采样单角色输入快照。
004 |  * 边界：只采输入；不接受动作、不驱动物理或写HP；每实例管理Action生命周期。
005 |  * 协作：选择器经ICharacterInputSource读取；实时许可接口让外层帧随撤权失效，不复制Current。
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
045 |
046 |         public bool HasPlayerInput => !managedPlayerInput || (ControlSourceAlive && playerControl.IsPlayerInputAllowed(this));
047 |         private bool ControlSourceAlive => playerControl != null &&
048 |             (!(playerControl is Object unityObject) || unityObject != null);
049 |
050 |         public bool CanBindPlayerControl(ICharacterPlayerControlSource source)
051 |             => source != null && (!managedPlayerInput || !ControlSourceAlive || ReferenceEquals(playerControl, source));
052 |
053 |         /// <summary>绑定唯一场景权威；拒绝另一存活协调者抢占同一输入。</summary>
054 |         public bool BindPlayerControl(ICharacterPlayerControlSource source)
055 |         {
056 |             if (!CanBindPlayerControl(source)) return false;
057 |             playerControl = source;
058 |             managedPlayerInput = true;
059 |             ResetPlayerInput();
060 |             return true;
061 |         }
062 |
063 |         /// <summary>移交时清掉旧快照；不假装一次松键，必须真正观察释放后才重武装攻击。</summary>
064 |         public void ResetPlayerInput() { CurrentFrame = default; attackArmed = false; }
065 |         public void RegisterPointerRegion(object owner, Rect region) { pointerRegions[owner] = region; }
066 |         public void RemovePointerRegion(object owner) { pointerRegions.Remove(owner); }
067 |
068 |         private bool IsPointerBlocked(Vector2 pointer)
069 |         {
070 |             if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return true;
071 |             var guiPoint = new Vector2(pointer.x, Screen.height - pointer.y);
072 |             foreach (var region in pointerRegions.Values) if (region.Contains(guiPoint)) return true;
073 |             return false;
074 |         }
075 |
076 |         public CharacterInputFrame CurrentFrame { get; private set; }
077 |
078 |         public bool IsConfigured => movementAction != null && movementAction.bindings.Count > 0 &&
079 |             attackAction != null && attackAction.bindings.Count > 0 &&
080 |             ((mouseAction != null && mouseAction.bindings.Count > 0) ||
081 |              (gamepadAimAction != null && gamepadAimAction.bindings.Count > 0));
082 |
083 |         private IEnumerable<InputAction> Actions()
084 |         {
085 |             // 同步枚举仅统一启用/禁用/释放清单；不是Unity协程，没有跨帧等待。
086 |             yield return movementAction;
087 |             yield return mouseAction;
088 |             yield return gamepadAimAction;
089 |             yield return attackAction;
090 |             yield return cancelAction;
091 |             yield return pauseAction;
092 |             yield return debugValidAction;
093 |             yield return debugBlockedAction;
094 |             yield return debugResetAction;
095 |             yield return debugInspectAction;
096 |         }
097 |
098 |         private void OnEnable() { foreach (var action in Actions()) if (action != null) action.Enable(); }
099 |         private void OnDisable() { ResetPlayerInput(); foreach (var action in Actions()) if (action != null) action.Disable(); }
100 |         private void OnDestroy() { foreach (var action in Actions()) if (action != null) action.Dispose(); }
101 |
102 |         public CharacterInputFrame Read()
103 |         {
104 |             var frame = new CharacterInputFrame();
105 |             // 非实控在读取设备前返回空帧；Root仍推进自己已接受的动作，不会偷读另一角色输入。
106 |             if (!HasPlayerInput || !isActiveAndEnabled || movementAction == null || !movementAction.enabled)
107 |             { CurrentFrame = frame; return frame; }
108 |             frame.move = movementAction.ReadValue<Vector2>();
109 |             if (mouseAction != null && mouseAction.enabled)
110 |             {
111 |                 frame.pointer = mouseAction.ReadValue<Vector2>();
112 |                 frame.hasPointer = mouseAction.controls.Count > 0 && frame.pointer.x >= 0 && frame.pointer.x <= Screen.width &&
113 |                     frame.pointer.y >= 0 && frame.pointer.y <= Screen.height && !IsPointerBlocked(frame.pointer);
114 |             }
115 |             if (gamepadAimAction != null && gamepadAimAction.enabled)
116 |             {
117 |                 frame.aimDirection = gamepadAimAction.ReadValue<Vector2>();
118 |                 frame.hasDirectionalAim = frame.aimDirection.sqrMagnitude > .01f;
119 |             }
120 |             var held = attackAction != null && attackAction.enabled && attackAction.IsPressed();
121 |             if (!held) attackArmed = true;
122 |             frame.attackHeld = held && attackArmed;
123 |             if (attackAction != null && attackAction.activeControl != null && attackAction.activeControl.device is Pointer &&
124 |                 !frame.hasPointer) frame.attackPointerBlocked = true;
125 |             frame.cancel = Pressed(cancelAction);
126 |             frame.pause = Pressed(pauseAction);
127 |             frame.debugCommand = Pressed(debugValidAction) ? 1 : Pressed(debugBlockedAction) ? 2 :
128 |                 Pressed(debugResetAction) ? 3 : Pressed(debugInspectAction) ? 4 : 0;
129 |             CurrentFrame = frame;
130 |             return frame;
131 |         }
132 |
133 |         private static bool Pressed(InputAction action) => action != null && action.enabled && action.WasPressedThisFrame();
134 |     }
135 | }
```

| 行区间 | 实际调用、状态与边界 |
|---|---|
| 1–6 | 中文功能/边界/协作文件头，与当前实现同步；不执行业务。 |
| 7–41 | 输入快照和所有Inspector Action保留，本轮只新增实时许可接口实现与准确协作注释。 |
| 42–49 | 原owner GUI阻挡区、玩家来源引用、受管理标记与松键武装保持原样；HasPlayerInput现在也由Selector通过窄接口只读消费。 |
| 50–67 | 原玩家owner绑定/Reset流程：拒绝抢占，清原始Frame并要求真实松键；不写Current、角色时钟或位置。 |
| 68–75 | 已有EventSystem与GUI区域防穿透，不把鼠标像素当世界方向。 |
| 76–82 | 原始设备快照归本组件；IsConfigured校验绑定。它不是Bus的最终有效Frame，后者由Selector拥有。 |
| 83–101 | 同步枚举仅统一Action生命周期，不是协程。禁用清Frame/武装，销毁Dispose自己的Action。 |
| 102–135 | 保留原实现：无玩家许可先返回空帧；再读移动/鼠标/摇杆，检查指针合法性、实际松键、UI攻击边界、取消/暂停/开发输入。本轮未修改这些采样分支。 |

用户结论：待审。变更后重审受影响范围。

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterRoot.cs

SHA256：904BEE796A9F7F249AF8FD77907DF9B41EFDE42D28CA08F193A75233CAEFFE2A

```text
001 | /*
002 |  * 脚本：CharacterRoot
003 |  * 功能：组织单角色的接口接线、输入、动作事实、运动和表现。
004 |  * 边界：持有本角色运行时；不实现设备绑定、目标HP写入或开发GUI。
005 |  * 协作：唯一意图选择器→朝向→请求→推进→释放命中→运动→Spine；场景可注入伙伴帧源。
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
026 |         [Header("Prototype timing — not final balance")]
027 |         [SerializeField] private float releaseAt = .3f, moveReadyAt = .5f, attackReadyAt = .8f, finishAt = 1.1f;
028 |         [SerializeField, Min(1)] private int damage = 12;
029 |         [SerializeField] private bool verboseTrace;
030 |         private readonly ControlAuthority authority = new ControlAuthority();
031 |         private readonly AttackIntent intent = new AttackIntent();
032 |         private ICharacterInputSource inputSource;
033 |         private CharacterIntentSelector intentSelector;
034 |         private ICharacterMovement mover;
035 |         private ICharacterOrientation orienter;
036 |         private ICharacterPresentation presentation;
037 |         private ICharacterHitResolver hitResolver;
038 |         private BasicActionRuntime runtime;
039 |         private BasicAttackDefinition definition;
040 |         private bool ready, focused = true, paused, applicationPaused;
041 |         private string lastFact = "Idle";
042 |         public CharacterDataBus DataBus { get; private set; }
043 |         public string ActorId => actorId;
044 |         public string LastFact => lastFact;
045 |         public bool IsPaused => paused || applicationPaused;
046 |         public bool ActionActive => DataBus != null && DataBus.ActionActive;
047 |         public bool CanSimulateIntent => ready && isActiveAndEnabled && focused && !IsPaused;
048 |         public CharacterIntentKind EffectiveIntentKind => intentSelector == null ? CharacterIntentKind.Neutral : intentSelector.EffectiveKind;
049 |
050 |         public bool CanBindCompanionIntent(ICharacterControlSession session, ICompanionIntentSource source)
051 |             => ready && intentSelector != null && intentSelector.CanBind(session,source);
052 |         public bool BindCompanionIntent(ICharacterControlSession session, ICompanionIntentSource source)
053 |             => CanBindCompanionIntent(session,source) && intentSelector.Bind(session,source);
054 |         public void ClearCompanionIntent(ICompanionIntentSource owner)
055 |         { if (intentSelector != null) intentSelector.Clear(owner); }
056 |
057 |         private void Awake()
058 |         {
059 |             if (viewCamera == null) viewCamera = Camera.main;
060 |             if (movement == null || orientation == null || view == null || melee == null || input == null || viewCamera == null)
061 |             { Debug.LogError("Character requires registered input/movement/orientation/view/melee/camera", this); enabled = false; return; }
062 |             // Inspector保留具体组件槽；运行期通过窄接口消费，不反向依赖开发GUI或测试靶。
063 |             // 每角色只有这一条有效Frame入口；没有Director绑定时保持原玩家适配器行为。
064 |             intentSelector = new CharacterIntentSelector(this,input);
065 |             inputSource = intentSelector;
066 |             mover = movement;
067 |             orienter = orientation;
068 |             presentation = view;
069 |             hitResolver = melee;
070 |             if (!inputSource.IsConfigured) { Debug.LogError("Character actions are not configured", this); enabled = false; return; }
071 |             try
072 |             {
073 |                 definition = new BasicAttackDefinition(releaseAt, moveReadyAt, attackReadyAt, finishAt, damage);
074 |                 runtime = new BasicActionRuntime(actorId);
075 |             }
076 |             catch (ArgumentException error) { Debug.LogError("Character configuration: " + error.Message, this); enabled = false; return; }
077 |             // 每角色独立Bus只读取原所有者；只有确需Bus的运动和表现执行初始化。
078 |             DataBus = new CharacterDataBus(actorId, inputSource, mover, orienter, runtime, authority);
079 |             ready = InitializeSubsystems(DataBus, mover, presentation);
080 |             if (!ready) { Debug.LogError("Character subsystem initialization failed", this); enabled = false; }
081 |         }
082 |
083 |         private static bool InitializeSubsystems(CharacterDataBus dataBus, params ICharacterSubsystem[] subsystems)
084 |         {
085 |             foreach (var subsystem in subsystems) if (subsystem == null || !subsystem.Initialize(dataBus)) return false;
086 |             return true;
087 |         }
088 |
089 |         private void Update()
090 |         {
091 |             if (!ready) return;
092 |             // 选择器只采一个源；伙伴只能提供移动，切人屏障为空帧，动作时钟仍继续。
093 |             inputSource.Read();
094 |             var frame = DataBus.Input;
095 |             if (frame.pause) SetPaused(!paused);
096 |             if (frame.cancel) CancelAction("player-cancel");
097 |             var canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
098 |             if (focused && !IsPaused)
099 |                 orienter.ResolveDirections(frame.move, PointerDirection(frame), frame.hasDirectionalAim ? frame.aimDirection : Vector2.zero, canMove);
100 |             // 输入边沿不等于动作成功：焦点、UI和Runtime的暂停/权限/Ready资格分别把关。
101 |             if (intent.Observe(frame.attackHeld) && focused && !frame.attackPointerBlocked)
102 |             {
103 |                 var request = runtime.Request(definition, "player-input", authority, IsPaused);
104 |                 if (request.PreviousTerminal != null) Observe(request.PreviousTerminal);
105 |                 if (request.Accepted) { Observe(request.Fact); orienter.FaceAttackDirection(); }
106 |                 else lastFact = "Rejected: " + request.Reason;
107 |             }
108 |             // Accepted只建立动作身份；只有Released带一次性命中凭据。切人不会重发事实。
109 |             foreach (var fact in runtime.Advance(Time.deltaTime, IsPaused || !focused))
110 |             {
111 |                 Observe(fact);
112 |                 if (fact.Release != null)
113 |                 {
114 |                     // 在释放当帧按值采样方向；不是接受动作时锁定，前摇中仍可重新瞄准。
115 |                     var releaseDirection = orienter.AttackDirection;
116 |                     orienter.FaceAttackDirection();
117 |                     hitResolver.Resolve(fact.Release, releaseDirection);
118 |                 }
119 |             }
120 |             // 本帧可能跨过MoveReady；推进事实后重新查询许可，Run依据真实位移。
121 |             canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
122 |             mover.Tick(canMove, IsPaused || !focused ? 0 : Time.deltaTime);
123 |             presentation.SetPaused(IsPaused || !focused);
124 |             presentation.Present();
125 |         }
126 |
127 |         private Vector3? PointerDirection(CharacterInputFrame frame)
128 |         {
129 |             if (!frame.hasPointer) return null;
130 |             var plane = new Plane(Vector3.up, transform.position);
131 |             var ray = viewCamera.ScreenPointToRay(frame.pointer);
132 |             if (!plane.Raycast(ray, out var distance)) return null;
133 |             var direction = ray.GetPoint(distance) - transform.position;
134 |             direction.y = 0;
135 |             return direction.sqrMagnitude < .0001f ? (Vector3?)null : direction;
136 |         }
137 |
138 |         private void Observe(ActionFact fact)
139 |         {
140 |             lastFact = fact.Kind + " @" + fact.At.ToString("F3") + " " + fact.ActionId.ToString("N").Substring(0, 8);
141 |             if (fact.Reason != null) lastFact += " " + fact.Reason;
142 |             if (verboseTrace) Debug.Log("Character " + lastFact, this);
143 |         }
144 |
145 |         public void CancelAction(string reason)
146 |         {
147 |             // 取消必须具名；抑制仍按住的输入，不能把下一帧误判为新请求。
148 |             intent.SuppressUntilRelease();
149 |             var terminal = runtime == null ? null : runtime.Cancel(reason);
150 |             if (terminal != null) Observe(terminal);
151 |         }
152 |
153 |         public void SetPaused(bool value) { paused = value; intent.SuppressUntilRelease(); }
154 |         public void SetAttackRestriction(object owner, bool restricted, string reason = "attack-restricted")
155 |         {
156 |             if (restricted) { authority.Block(owner, ControlChannel.Attack); CancelAction(reason); }
157 |             else authority.Release(owner);
158 |         }
159 |
160 |         private void OnApplicationFocus(bool hasFocus)
161 |         { focused = hasFocus; if (!hasFocus) CancelAction("focus-lost"); else intent.SuppressUntilRelease(); }
162 |         private void OnApplicationPause(bool value)
163 |         { applicationPaused = value; if (value) intent.SuppressUntilRelease(); }
164 |         private void OnEnable() { intent.SuppressUntilRelease(); if (intentSelector != null) intentSelector.Clear(); }
165 |         private void OnDisable() { CancelAction("disabled"); if (intentSelector != null) intentSelector.Clear(); if (ready && presentation != null) presentation.SetPaused(false); }
166 |         private void OnDestroy()
167 |         {
168 |             // 终止本角色动作后解除本对象引用；不清其它角色或场景控制权威。
169 |             CancelAction("destroyed");
170 |             ready = false;
171 |             DataBus = null;
172 |             runtime = null;
173 |             inputSource = null;
174 |             if (intentSelector != null) intentSelector.Clear();
175 |             intentSelector = null;
176 |             mover = null;
177 |             orienter = null;
178 |             presentation = null;
179 |             hitResolver = null;
180 |         }
181 |     }
182 | }
```

| 行区间 | 实际调用、状态与边界 |
|---|---|
| 1–6 | 中文功能/边界/协作文件头，与当前实现同步；不执行业务。 |
| 7–13 | 保留领域、Unity与序列化兼容依赖；Root仍单角色组合根。 |
| 14–46 | 序列化槽、动作时序、权限/边沿与接口依赖保持原样；新增本角色Selector引用，不放队伍模块到角色Prefab。 |
| 47–49 | 只读报告单角色就绪/启用/焦点/暂停资格，供Director尊重；EffectiveIntentKind读取Selector，不写第二选中真值。 |
| 50–56 | 最小公开接线：Root准备好后校验来源owner、绑定只读session与伙伴provider；Clear只由同owner清有效Frame，不改物理Held或动作身份。 |
| 57–82 | 在原具体input槽外构造单个Selector，把同一对象注入Bus；未绑定Director时委托旧玩家源。领域Runtime/Definition、权限与子系统初始化保持原顺序。 |
| 83–88 | 只有运动与表现确需Bus，显式顺序初始化；失败关闭角色，不假初始化输入/朝向/命中。 |
| 89–126 | 仍只inputSource.Read一次，再从Bus取有效Frame；暂停/取消、朝向、边沿/动作资格、Advance、Released一次命中、运动与Spine顺序保持。没有第二Move读取器或AI动作时钟。 |
| 127–137 | 原屏幕到角色XZ平面映射；指针无效回退，AI帧不携带指针。 |
| 138–144 | 原动作事实观察/日志，不是第二状态机。 |
| 145–163 | 原具名取消、暂停、owner禁攻和焦点规则保留；切人不走取消入口。 |
| 164–182 | 启用/禁用清Selector快照；原Held抑制和禁用具名取消保留。OnDestroy清Runtime/Bus/Selector及接口引用，不动其它角色。 |

用户结论：待审。变更后重审受影响范围。

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterIntentSelector.cs

SHA256：87F0CDAA2B7695E525682B3FDD8394B6E4D1A63E863D80325F924C64CE5A1A18

```text
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
054 |             kind=CharacterIntentPolicy.Select(managed, Alive(session) && session.IsIntentSamplingAllowed,
055 |                 Alive(session) && session.Current == character, Alive(companion) && companion.IsAvailable);
056 |             if (kind == CharacterIntentKind.Player) frame=player.Read();
057 |             else if (kind == CharacterIntentKind.Companion)
058 |             {
059 |                 // 伙伴即使给了其它字段，也只准传递move；不能借AI帧自动攻击、取消或暂停。
060 |                 var candidate=companion.ReadCompanionIntent(character,Time.deltaTime);
061 |                 frame=new CharacterInputFrame { move=candidate.move };
062 |             }
063 |             sampledFrame=Time.frameCount;
064 |             sampledRevision=Alive(session) ? session.ControlRevision : 0;
065 |             return CurrentFrame;
066 |         }
067 |
068 |         private static bool Alive(object value) => value != null && (!(value is Object unityObject) || unityObject != null);
069 |     }
070 | }
```

| 行区间 | 实际调用、状态与边界 |
|---|---|
| 1–6 | 中文功能/边界/协作文件头，与当前实现同步；不执行业务。 |
| 7–13 | 依赖纯来源策略与Unity帧/对象生命周期，不采设备。 |
| 14–22 | 持有本角色、原玩家源、只读会话和伙伴源；managed表示曾绑定，不是isSelected。只有frame是Bus最终有效快照，kind是当前采样来源；帧号/代际用于失效，不是动作时钟。 |
| 23–33 | 构造只接依赖，配置沿用玩家源。ValidSample检查本帧、Root启用、session许可和代际；玩家实时许可/伙伴provider禁用也会立即使外层快照失效，旧场同样安全。 |
| 34–37 | 只接活会话/provider，拒绝另一活伙伴owner；原provider已销毁时允许替换。 |
| 38–43 | 显式绑定引用、标受管理并清旧有效帧；不复制Current。 |
| 44–49 | 只有同provider或Root内部清理可清frame/kind/帧号，不伪造玩家松键。 |
| 50–67 | 每次Root采样先清旧帧，纯策略只选一源。Player调用原适配器；Companion调用provider但只保留move，其它攻击/取消/暂停/debug字段强制零；最后记录本帧与代际，返回通过有效性判断的快照。 |
| 68–70 | 按Unity销毁语义识别已失效的接口实现，避免C#接口引用仍非null却访问已销毁对象。 |

用户结论：待审。变更后重审受影响范围。

## Assets/OSSASTRA/Scripts/Characters/Runtime/CompanionFollowDirector.cs

SHA256：C2BC03A9CE95338BB2739423F094DC9066FD6555D703E002606D823850B63ECD

```text
001 | /*
002 |  * 脚本：CompanionFollowDirector
003 |  * 功能：场景级最小跟随策略，读取唯一控制会话，为非实控角色提供移动帧。
004 |  * 边界：不写Current、Transform、HP、Spine或动作时钟；不寻路、加速或自动攻击。
005 |  * 协作：Start在所有Root初始化后显式绑定选择器；选择器Read时惰性计算，每角色策略独立。
006 |  */
007 | using System;
008 | using System.Collections.Generic;
009 | using Ossastra.CharacterCore;
010 | using UnityEngine;
011 |
012 | namespace Ossastra.Characters
013 | {
014 |     public readonly struct CompanionFollowObservation
015 |     {
016 |         public CompanionFollowState State { get; }
017 |         public string Reason { get; }
018 |         public float Distance { get; }
019 |         public string TargetId { get; }
020 |         public CompanionFollowObservation(CompanionFollowState state, string reason, float distance, string targetId)
021 |         { State=state; Reason=reason; Distance=distance; TargetId=targetId; }
022 |     }
023 |
024 |     public sealed class CompanionFollowDirector : MonoBehaviour, ICompanionIntentSource
025 |     {
026 |         [SerializeField] private CharacterControlCoordinator coordinator;
027 |         [Header("跟随候选参数 / 世界单位，不是正式玩法平衡")]
028 |         [SerializeField, Min(.01f)] private float followStartDistance=2.4f, followStopDistance=1.6f;
029 |         [SerializeField, Min(.01f)] private float blockedCheckWindow=.5f, retryTargetDelta=.5f;
030 |         private readonly Dictionary<CharacterRoot, Entry> entries = new Dictionary<CharacterRoot, Entry>();
031 |         private ICharacterControlSession session;
032 |         private bool started, configured, bindingsOwned;
033 |         public bool IsAvailable => configured && isActiveAndEnabled && coordinator != null;
034 |         public float StartDistance => followStartDistance;
035 |         public float StopDistance => followStopDistance;
036 |         public string Status { get; private set; } = "未绑定";
037 |         public CharacterControlCoordinator Coordinator => coordinator;
038 |
039 |         private sealed class Entry
040 |         {
041 |             public CompanionFollowPolicy policy;
042 |             public Vector3 lastPosition;
043 |             public int revision=-1;
044 |             public CompanionFollowState state;
045 |             public string reason="等待采样";
046 |         }
047 |
048 |         private void Start() { started=true; BindMembers(); }
049 |         private void OnEnable() { if (started) BindMembers(); }
050 |
051 |         private void BindMembers()
052 |         {
053 |             configured=false;
054 |             // 绑定失败/禁用的会话不能借Director占用原角色选择器，即使它仍保存成员引用。
055 |             if (coordinator == null || !coordinator.isActiveAndEnabled) { Status="控制会话缺失或未启用"; return; }
056 |             session=coordinator;
057 |             try { new CompanionFollowPolicy(followStartDistance,followStopDistance,blockedCheckWindow,retryTargetDelta); }
058 |             catch (ArgumentException) { Status="跟随阈值无效"; return; }
059 |             // 先验证全部消费者；第二Director不得清理或替换原Director的有效帧。
060 |             foreach (var character in session.Characters)
061 |                 if (character == null || !character.CanBindCompanionIntent(session,this))
062 |                 { Status="角色未就绪或伙伴来源已被占用"; return; }
063 |             foreach (var character in session.Characters)
064 |             {
065 |                 character.BindCompanionIntent(session,this);
066 |                 entries[character]=new Entry { policy=new CompanionFollowPolicy(followStartDistance,followStopDistance,blockedCheckWindow,retryTargetDelta) };
067 |             }
068 |             bindingsOwned=true; configured=true; Status="直接追随 / 无寻路";
069 |         }
070 |
071 |         /// <summary>只从合法会话的非Current成员产生移动；实际碰撞、Ready与表现仍经原角色管线。</summary>
072 |         public CharacterInputFrame ReadCompanionIntent(CharacterRoot character, float delta)
073 |         {
074 |             if (!IsAvailable || session == null || character == null || !entries.TryGetValue(character,out var entry)) return default;
075 |             var target=session.Current;
076 |             var allowed=session.IsIntentSamplingAllowed && target != null && target != character &&
077 |                 target.CanSimulateIntent && character.CanSimulateIntent && character.DataBus != null &&
078 |                 character.DataBus.AllowsMovement && character.DataBus.MoveReady;
079 |             var position=character.transform.position;
080 |             var travel=position-entry.lastPosition; travel.y=0;
081 |             if (entry.revision != session.ControlRevision)
082 |             {
083 |                 // 切人后的策略不能继承上一来源的运动命令或无位移计时。
084 |                 entry.policy.Reset(); travel=Vector3.zero; entry.revision=session.ControlRevision;
085 |             }
086 |             entry.lastPosition=position;
087 |             var targetPosition=target == null ? position : target.transform.position;
088 |             var offset=targetPosition-position;
089 |             var decision=entry.policy.Sample(offset.x,offset.z,targetPosition.x,targetPosition.z,travel.magnitude,delta,allowed);
090 |             entry.state=decision.State;
091 |             entry.reason=!allowed ? "会话／焦点／暂停／Ready／移动许可不足" :
092 |                 decision.State==CompanionFollowState.Blocked ? "受阻等待领队移动；不承诺绕墙" :
093 |                 decision.State==CompanionFollowState.CloseEnough ? "停止区或启动阈值内" : "朝领队位置移动";
094 |             // 只设置move；这里没有攻击、取消、暂停或调试命令，也不直接执行位移。
095 |             return new CharacterInputFrame { move=new Vector2((float)decision.MoveX,(float)decision.MoveZ) };
096 |         }
097 |
098 |         /// <summary>开发只读观察，不推进策略；未采样的新代际不展示旧Following作为当前事实。</summary>
099 |         public CompanionFollowObservation Observe(CharacterRoot character)
100 |         {
101 |             var target=coordinator == null ? null : coordinator.Current;
102 |             var offset=target == null || character == null ? Vector3.zero : target.transform.position-character.transform.position;
103 |             offset.y=0;
104 |             var targetId=target == null ? "无" : target.ActorId;
105 |             if (!IsAvailable || session == null || !session.IsIntentSamplingAllowed || character == null || !character.CanSimulateIntent)
106 |                 return new CompanionFollowObservation(CompanionFollowState.Inactive,"无合法会话／不可行动",offset.magnitude,targetId);
107 |             if (target==character) return new CompanionFollowObservation(CompanionFollowState.Inactive,"玩家接管",0,targetId);
108 |             if (!entries.TryGetValue(character,out var entry) || entry.revision!=session.ControlRevision)
109 |                 return new CompanionFollowObservation(CompanionFollowState.Inactive,"等待新代际采样",offset.magnitude,targetId);
110 |             return new CompanionFollowObservation(entry.state,entry.reason,offset.magnitude,targetId);
111 |         }
112 |
113 |         private void OnDisable()
114 |         {
115 |             configured=false;
116 |             if (!bindingsOwned) return;
117 |             foreach (var pair in entries)
118 |             {
119 |                 pair.Value.policy.Reset();
120 |                 if (pair.Key != null) pair.Key.ClearCompanionIntent(this);
121 |             }
122 |             Status="跟随关闭，玩家仍由原会话管理";
123 |         }
124 |         private void OnDestroy() { entries.Clear(); session=null; }
125 |     }
126 | }
```

| 行区间 | 实际调用、状态与边界 |
|---|---|
| 1–6 | 中文功能/边界/协作文件头，与当前实现同步；不执行业务。 |
| 7–23 | 场景适配器依赖纯策略和原会话；readonly观察DTO只做开发显示，不推进策略。 |
| 24–47 | Inspector coordinator是唯一权威来源，阈值为候选参数。entries只保存各成员策略历史/实际前位，不建立第二身份名单或Current。 |
| 48–50 | 所有Awake之后Start显式绑定，避免Root尚未初始化；重新启用按同一入口绑定。无额外Update。 |
| 51–71 | 配置与owner预检：缺失/被拒绝或禁用的coordinator不能抢占角色；阈值非法拒绝；全部Root可绑定后才统一绑定并建立独立策略。失败Director无bindingsOwned，不清原owner帧。 |
| 72–98 | 只在合法会话非Current、双方可行动且本角色MoveReady/移动许可通过时跟随。水平实际位置差只作为只读反馈；代际变化清旧策略/运动累计。纯策略返回归一move与诊断，所有攻击类字段保持零，不调用位移/Spine/HP。 |
| 99–112 | 只读开发观察：会话无效/不可行动/玩家接管/新代际未采样时如实Inactive；其余读最近策略状态、距离、目标和拒绝原因，不修改策略或Current。 |
| 113–126 | 关闭provider、清本owner策略与Root有效帧；原玩家会话仍管理实控。OnDestroy清局部字典/接口，没静态残留。 |

用户结论：待审。变更后重审受影响范围。

## Assets/OSSASTRA/Scripts/Development/Combat/CompanionFollowDebugView.cs

SHA256：C917CE39D099DE4EB9226DF6356279A6D8536240E0BAB1BDE06B6090350DD43F

```text
001 | /*
002 |  * 脚本：CompanionFollowDebugView
003 |  * 功能：展示跟随来源、目标、距离/阈值、状态与拒绝原因，提供可达开发入口。
004 |  * 边界：只读业务状态；切换/禁启通过真实入口，不伪造GUI成功、不移动角色。
005 |  * 协作：读取Director与Root；登记自己的GUI阻挡区域，禁用只清理自己。
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
035 |             GUILayout.Label("启动 " + director.StartDistance.ToString("F2") + " / 停止 " + director.StopDistance.ToString("F2") + " 世界单位");
036 |             foreach (var character in coordinator.Characters)
037 |             {
038 |                 if (character == null) { GUILayout.Label("成员引用缺失"); continue; }
039 |                 var observation=director.Observe(character);
040 |                 GUILayout.Label(character.ActorId + " | " + character.EffectiveIntentKind + " | " + observation.State + " | 距离 " + observation.Distance.ToString("F2"));
041 |                 GUILayout.Label("目标 " + observation.TargetId + " | " + observation.Reason);
042 |             }
043 |             if (GUILayout.Button("合法路径：切换实控角色")) coordinator.SwitchNext();
044 |             if (GUILayout.Button("非法/恢复：切换跟随启用")) director.enabled=!director.enabled;
045 |             if (GUILayout.Button("清理：禁用后重新启用跟随，重新采样")) { director.enabled=false; director.enabled=true; }
046 |             if (GUILayout.Button("检查：输出当前权威与状态")) Debug.Log(coordinator.Status + " | " + director.Status,this);
047 |             GUILayout.EndArea();
048 |         }
049 |     }
050 | }
```

| 行区间 | 实际调用、状态与边界 |
|---|---|
| 1–6 | 中文功能/边界/协作文件头，与当前实现同步；不执行业务。 |
| 7–11 | Development单向读取生产模块，生产Root不反向依赖本面板。 |
| 12–15 | 序列化Director与开发布局，没复制Current/跟随真值。 |
| 16–27 | 登记/移除自己的GUI阻挡区域；缺成员/输入安全跳过，禁用不影响其它owner。 |
| 28–50 | 读取原Current、阈值、每角色EffectiveIntentKind与Director只读观察；按钮调用真实切控/禁启/重建策略/检查入口，不写角色位置、不显示假成功。 |

用户结论：待审。变更后重审受影响范围。
