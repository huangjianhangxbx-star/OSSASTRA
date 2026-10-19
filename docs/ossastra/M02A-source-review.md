# M02-A 最终源码与逐段行号审查

全部为候选待用户审查；当前18份自有Scripts C#完整原文，包含P0补注释的16份及本轮新增2份。行号和SHA来自此刻磁盘，不沿用M01-R1历史行号。每一行（含结构括号和空白）都落入下表语句块；表格解释状态所有权、调用者与失败边界。

## Assets/OSSASTRA/Scripts/Characters/Core/AttackIntent.cs

SHA256：18AC442ABC02C89A96436D3D8535E4BC18C19F39F4846477D786EBE93157F459

```text
001 | /*
002 |  * 脚本：AttackIntent
003 |  * 功能：将按住状态转换为一次按下意图，并在失焦、禁启或移交输入后要求松键重武装。
004 |  * 边界：只维护边沿；不读设备、不判断动作资格、不释放攻击。
005 |  * 协作：Root调用Observe／SuppressUntilRelease；每角色独立。
006 |  */
007 | namespace Ossastra.CharacterCore
008 | {
009 |     // 设备采样由Unity输入适配器负责；这里只保留按下边沿和重新武装规则。
010 |     public sealed class AttackIntent
011 |     {
012 |         private bool previousHeld;
013 |         private bool armed = true;
014 | 
015 |         public bool Observe(bool held)
016 |         {
017 |             if (!armed)
018 |             {
019 |                 if (!held) armed = true;
020 |                 previousHeld = held;
021 |                 return false;
022 |             }
023 |             var pressed = held && !previousHeld;
024 |             previousHeld = held;
025 |             return pressed;
026 |         }
027 | 
028 |         // 失焦/禁用后，仍按住的键不能被误当作一次新按下。
029 |         public void SuppressUntilRelease()
030 |         {
031 |             previousHeld = false;
032 |             armed = false;
033 |         }
034 |     }
035 | }
```

| 行区间 | 调用、语义和状态权限 |
|---|---|
| 1–6 | 文件级中文职责总览：描述真实功能、负责/不负责范围和协作边界；不改变运行状态。 |
| 7–8 | 纯领域边沿对象，既不读设备也不播放动画。 |
| 9–9 | 设备采样已由Unity输入适配器实现；此对象只保留按下边沿与重新武装状态。 |
| 10–11 | 纯领域边沿对象，既不读设备也不播放动画。 |
| 12–14 | previousHeld保存上次Held，armed表示是否已看到重新松键；每个角色独立。 |
| 15–28 | 未武装时只允许松键恢复，不发命令；已武装时仅false→true返回按下。持续按住不重复，调用者仍须另外检查动作资格。 |
| 29–35 | 生命周期/暂停/取消调用；清previousHeld并取消武装，避免恢复窗口时仍按住被误认新按下。 |

用户结论：待审。修改任何职责或语义后，重新生成受影响行号并复审。

## Assets/OSSASTRA/Scripts/Characters/Core/AttackRelease.cs

SHA256：DB4C01D974AB1FB99FAC9C5B4A698BFDE2CD02B26C386A867971D4C5CA2ACDAC

```text
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
042 | 
043 |         internal AttackRelease(Guid actionId, string actorId, int damage)
044 |         {
045 |             this.actionId = actionId;
046 |             this.actorId = actorId;
047 |             this.damage = damage;
048 |         }
049 | 
050 |         // 调用方必须先完成实际空间/阵营过滤；本方法不谎称已做物理检测。
051 |         public IReadOnlyList<HitOutcome> Resolve(IEnumerable<Health> targets)
052 |         {
053 |             if (resolved) throw new InvalidOperationException("Attack release already resolved");
054 |             if (targets == null) throw new ArgumentNullException(nameof(targets));
055 |             var batch = targets.ToArray();
056 |             if (batch.Any(t => t == null)) throw new ArgumentException("Null candidate target", nameof(targets));
057 |             resolved = true;
058 |             var seen = new HashSet<string>(StringComparer.Ordinal);
059 |             var outcomes = new List<HitOutcome>();
060 |             foreach (var target in batch)
061 |             {
062 |                 var before = target.Current;
063 |                 HitReason reason;
064 |                 if (!seen.Add(target.Id)) reason = HitReason.Duplicate;
065 |                 else if (target.Id == actorId) reason = HitReason.Self;
066 |                 else if (target.Current == 0) reason = HitReason.Dead;
067 |                 else
068 |                 {
069 |                     target.ApplyDamage(damage);
070 |                     reason = HitReason.Applied;
071 |                 }
072 |                 outcomes.Add(new HitOutcome(reason, before, target, actionId, actorId));
073 |             }
074 |             return outcomes;
075 |         }
076 |     }
077 | }
```

| 行区间 | 调用、语义和状态权限 |
|---|---|
| 1–6 | 文件级中文职责总览：描述真实功能、负责/不负责范围和协作边界；不改变运行状态。 |
| 7–14 | 定义命中结果类别和领域结算凭证，不包含Unity空间查找。 |
| 15–35 | 只读结果记录目标、动作/攻击者身份、HP前后与原因；用于反馈和验收，不作为第二份可写HP。 |
| 36–50 | 构造仅限领域程序集，保存动作ID/攻击者/伤害和是否已结算；只有真实Released可取得该凭证。 |
| 51–77 | 拒绝重复Resolve/null批次，先整体校验无null再写HP。空挥也关闭该凭证；按目标身份去重，排除自己和死亡对象，仅合法目标调用唯一Health.ApplyDamage，返回每个候选的真实结果。 |

用户结论：待审。修改任何职责或语义后，重新生成受影响行号并复审。

## Assets/OSSASTRA/Scripts/Characters/Core/BasicActionRuntime.cs

SHA256：802846B0E0C2D935EC59A52D2CDC8261729777D22EEDC6F1628255F0B7122E99

```text
001 | /*
002 |  * 脚本：BasicActionRuntime
003 |  * 功能：维护单角色的动作身份、时钟与Accepted／Released／Ready／终结事实。
004 |  * 边界：不采设备、不驱动Spine、不查询碰撞、不直接改HP。
005 |  * 协作：Root请求并推进；Release对象交世界适配器；暂停不推进时钟。
006 |  */
007 | using System;
008 | using System.Collections.Generic;
009 | using System.Linq;
010 | 
011 | namespace Ossastra.CharacterCore
012 | {
013 |     public enum ActionFactKind { Accepted, Released, MoveReady, AttackReady, Finished, Cancelled }
014 | 
015 |     // 事实是结果，不是任意广播；调用者按返回顺序处理。
016 |     public sealed class ActionFact
017 |     {
018 |         public ActionFactKind Kind { get; }
019 |         public Guid ActionId { get; }
020 |         public string Source { get; }
021 |         public string Reason { get; }
022 |         public double At { get; }
023 |         public AttackRelease Release { get; }
024 | 
025 |         internal ActionFact(ActionFactKind kind, Guid id, string source, double at,
026 |             string reason = null, AttackRelease release = null)
027 |         {
028 |             Kind = kind;
029 |             ActionId = id;
030 |             Source = source;
031 |             At = at;
032 |             Reason = reason;
033 |             Release = release;
034 |         }
035 |     }
036 | 
037 |     public sealed class ActionRequestResult
038 |     {
039 |         public bool Accepted => Fact != null;
040 |         public string Reason { get; }
041 |         public ActionFact Fact { get; }
042 |         public ActionFact PreviousTerminal { get; }
043 | 
044 |         internal ActionRequestResult(string reason, ActionFact fact = null, ActionFact previousTerminal = null)
045 |         {
046 |             Reason = reason;
047 |             Fact = fact;
048 |             PreviousTerminal = previousTerminal;
049 |         }
050 |     }
051 | 
052 |     // 单角色单动作权威，不读设备/Spine/世界碰撞，也不直接写HP。
053 |     public sealed class BasicActionRuntime
054 |     {
055 |         private readonly string actorId;
056 |         private BasicAttackDefinition definition;
057 |         private (double At, ActionFactKind Kind)[] nodes;
058 |         private int nextNode;
059 |         private double elapsed;
060 |         private double timeCorrection;
061 |         private Guid actionId;
062 |         private string source;
063 |         private bool moveReady;
064 |         private bool attackReady;
065 | 
066 |         public bool IsActive => definition != null;
067 |         public bool MoveReady => !IsActive || moveReady;
068 |         public bool AttackReady => !IsActive || attackReady;
069 |         public Guid ActionId => IsActive ? actionId : Guid.Empty;
070 | 
071 |         public BasicActionRuntime(string actorId)
072 |         {
073 |             if (string.IsNullOrWhiteSpace(actorId)) throw new ArgumentException("Actor identity required", nameof(actorId));
074 |             this.actorId = actorId;
075 |         }
076 | 
077 |         public ActionRequestResult Request(BasicAttackDefinition attack, string requestSource,
078 |             ControlAuthority gate, bool paused = false)
079 |         {
080 |             if (attack == null) throw new ArgumentNullException(nameof(attack));
081 |             if (gate == null) throw new ArgumentNullException(nameof(gate));
082 |             if (string.IsNullOrWhiteSpace(requestSource)) throw new ArgumentException("Request source required", nameof(requestSource));
083 |             if (paused) return new ActionRequestResult("paused");
084 |             if (!gate.Allows(ControlChannel.Attack)) return new ActionRequestResult("attack-blocked");
085 |             if (IsActive && !attackReady) return new ActionRequestResult("busy");
086 |             // Ready允许一次新按下；显式终结旧身体动作，不遗失其已释放效果。
087 |             var previousTerminal = IsActive ? Cancel("replaced-by-next-attack") : null;
088 |             definition = attack;
089 |             source = requestSource;
090 |             actionId = Guid.NewGuid();
091 |             elapsed = 0;
092 |             timeCorrection = 0;
093 |             nextNode = 0;
094 |             moveReady = false;
095 |             attackReady = false;
096 |             // 两种Ready可互换先后；同一时刻固定Release→Move→Attack→Finish。
097 |             nodes = new[] {
098 |                 (attack.ReleaseAt, ActionFactKind.Released),
099 |                 (attack.MoveReadyAt, ActionFactKind.MoveReady),
100 |                 (attack.AttackReadyAt, ActionFactKind.AttackReady),
101 |                 (attack.FinishAt, ActionFactKind.Finished)
102 |             }.OrderBy(n => n.Item1).ThenBy(n => n.Item2).ToArray();
103 |             return new ActionRequestResult(null, Fact(ActionFactKind.Accepted, 0), previousTerminal);
104 |         }
105 | 
106 |         public IReadOnlyList<ActionFact> Advance(double delta, bool paused)
107 |         {
108 |             if (!BasicAttackDefinition.Finite(delta) || delta < 0)
109 |                 throw new ArgumentOutOfRangeException(nameof(delta));
110 |             if (!IsActive || paused) return Array.Empty<ActionFact>();
111 |             // 补偿累计舍入误差；不靠提前Release的任意epsilon修补帧边界。
112 |             var correctedDelta = delta - timeCorrection;
113 |             var sum = elapsed + correctedDelta;
114 |             timeCorrection = (sum - elapsed) - correctedDelta;
115 |             elapsed = Math.Min(definition.FinishAt, sum);
116 |             if (elapsed == definition.FinishAt) timeCorrection = 0;
117 |             var facts = new List<ActionFact>();
118 |             while (nextNode < nodes.Length && elapsed >= nodes[nextNode].At)
119 |             {
120 |                 var node = nodes[nextNode++];
121 |                 AttackRelease release = null;
122 |                 if (node.Kind == ActionFactKind.Released)
123 |                     release = new AttackRelease(actionId, actorId, definition.Damage);
124 |                 if (node.Kind == ActionFactKind.MoveReady) moveReady = true;
125 |                 if (node.Kind == ActionFactKind.AttackReady) attackReady = true;
126 |                 facts.Add(Fact(node.Kind, node.At, release: release));
127 |                 if (node.Kind == ActionFactKind.Finished) definition = null;
128 |             }
129 |             return facts;
130 |         }
131 | 
132 |         public ActionFact Cancel(string reason)
133 |         {
134 |             if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Cancellation cause required", nameof(reason));
135 |             if (!IsActive) return null;
136 |             var terminal = Fact(ActionFactKind.Cancelled, elapsed, reason);
137 |             definition = null;
138 |             return terminal;
139 |         }
140 | 
141 |         private ActionFact Fact(ActionFactKind kind, double at, string reason = null, AttackRelease release = null)
142 |             => new ActionFact(kind, actionId, source, at, reason, release);
143 |     }
144 | }
```

| 行区间 | 调用、语义和状态权限 |
|---|---|
| 1–6 | 文件级中文职责总览：描述真实功能、负责/不负责范围和协作边界；不改变运行状态。 |
| 7–15 | 纯动作领域定义事实类型，不依赖Unity/Spine或物理。 |
| 16–36 | Accepted/Released/Ready/Finished/Cancelled事实对象只读；携动作ID、来源、原因、局部时间及合法Release凭证，构造不调度GUI。 |
| 37–52 | Accepted由Fact是否存在决定；Reason说明拒绝；PreviousTerminal显式返回被新攻击替换的旧恢复尾部取消，调用者不能丢掉它。 |
| 53–70 | 每角色唯一动作状态：配置、节点数组、下一个节点、elapsed及舍入补偿、身份/来源/两个Ready；对外只读查询。 |
| 71–76 | 拒绝空身份，保存本角色身份供真实Accepted和Release使用。 |
| 77–96 | 校验配置/来源/权限对象；paused、禁攻、未AttackReady分别拒绝且不创建ID。恢复尾部允许新按下时显式Cancel旧动作，再为真正接受创建新GUID并清时钟/节点/Ready。 |
| 97–105 | 按时间与事实种类稳定排序四个节点，保证同刻Release→MoveReady→AttackReady→Finish；返回Accepted，不等于已出手。 |
| 106–117 | 拒绝非法delta；无动作/暂停不推进。使用补偿求和减小帧分割误差，最多推进到Finish，不靠提前释放epsilon作弊。 |
| 118–131 | 一次跨多节点也顺序逐个处理。只有Released构造一次AttackRelease；对应Ready只写自身标志，Finished清配置。每个事实带节点时间而不是本帧时间。 |
| 132–140 | 具名原因必须非空；无动作返回null；有动作返回唯一Cancelled并清当前配置。已经返回的Release效果不撤销。 |
| 141–144 | 统一把当前动作身份/来源和参数组成只读事实，不依赖诊断开关。 |

用户结论：待审。修改任何职责或语义后，重新生成受影响行号并复审。

## Assets/OSSASTRA/Scripts/Characters/Core/BasicAttackDefinition.cs

SHA256：0C669D9A9BE2BA59BE2A5BF7266C2CDC8C3650874EF3E6AFED0D43897A856659

```text
001 | /*
002 |  * 脚本：BasicAttackDefinition
003 |  * 功能：验证并保存一次基础攻击的不可变时间节点和伤害配置。
004 |  * 边界：不内置正式平衡默认值；拒绝无效时间顺序和非正伤害。
005 |  * 协作：由Root构造，BasicActionRuntime读取。
006 |  */
007 | using System;
008 | 
009 | namespace Ossastra.CharacterCore
010 | {
011 |     // 全部配置不可变；本类没有游戏平衡默认值。
012 |     public sealed class BasicAttackDefinition
013 |     {
014 |         public double ReleaseAt { get; }
015 |         public double MoveReadyAt { get; }
016 |         public double AttackReadyAt { get; }
017 |         public double FinishAt { get; }
018 |         public int Damage { get; }
019 | 
020 |         public BasicAttackDefinition(double release, double move, double ready, double finish, int damage)
021 |         {
022 |             if (!Finite(release) || !Finite(move) || !Finite(ready) || !Finite(finish) ||
023 |                 release < 0 || move < release || ready < release || finish < move || finish < ready)
024 |                 throw new ArgumentOutOfRangeException(nameof(release), "Invalid action node ordering or time");
025 |             if (damage <= 0) throw new ArgumentOutOfRangeException(nameof(damage));
026 |             ReleaseAt = release;
027 |             MoveReadyAt = move;
028 |             AttackReadyAt = ready;
029 |             FinishAt = finish;
030 |             Damage = damage;
031 |         }
032 | 
033 |         internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
034 |     }
035 | }
```

| 行区间 | 调用、语义和状态权限 |
|---|---|
| 1–6 | 文件级中文职责总览：描述真实功能、负责/不负责范围和协作边界；不改变运行状态。 |
| 7–13 | 不可变动作配置，默认数值由Root Inspector提供而非这里隐藏。 |
| 14–19 | 只读释放/移动恢复/攻击恢复/结束时间和伤害；创建后不能由表现模块篡改。 |
| 20–32 | 拒绝NaN/Infinity、负时间、释放后Ready顺序错误、结束早于Ready和非正伤害；成功后逐项赋只读配置。 |
| 33–35 | 集中定义有限实数校验供构造和Advance复用。 |

用户结论：待审。修改任何职责或语义后，重新生成受影响行号并复审。

## Assets/OSSASTRA/Scripts/Characters/Core/ControlAuthority.cs

SHA256：21FBC419843BA0B5A74F7D962657D8AF6E210A736EBC2241370B8CC34E7F70A5

```text
001 | /*
002 |  * 脚本：ControlAuthority
003 |  * 功能：按调用者身份保存移动和攻击禁令，查询多个禁令的合并结果。
004 |  * 边界：调用者只能解除自己的禁令；不是玩家当前选中角色的全局真值。
005 |  * 协作：Root写入自身角色权限；DataBus只读查询。
006 |  */
007 | using System;
008 | using System.Collections.Generic;
009 | using System.Runtime.CompilerServices;
010 | 
011 | namespace Ossastra.CharacterCore
012 | {
013 |     [Flags]
014 |     public enum ControlChannel { None = 0, Move = 1, Attack = 2 }
015 | 
016 |     // 每个调用者只拥有自己的禁令；没有一个调用者可以清空别人的限制。
017 |     public sealed class ControlAuthority
018 |     {
019 |         private readonly Dictionary<object, ControlChannel> blocks =
020 |             new Dictionary<object, ControlChannel>(new OwnerComparer());
021 | 
022 |         public void Block(object owner, ControlChannel channels)
023 |         {
024 |             if (owner == null) throw new ArgumentNullException(nameof(owner));
025 |             Validate(channels);
026 |             blocks[owner] = channels;
027 |         }
028 | 
029 |         public void Release(object owner)
030 |         {
031 |             if (owner != null) blocks.Remove(owner);
032 |         }
033 | 
034 |         public bool Allows(ControlChannel channels)
035 |         {
036 |             Validate(channels);
037 |             foreach (var block in blocks.Values)
038 |                 if ((block & channels) != 0) return false;
039 |             return true;
040 |         }
041 | 
042 |         private static void Validate(ControlChannel channels)
043 |         {
044 |             if ((channels & ~(ControlChannel.Move | ControlChannel.Attack)) != 0)
045 |                 throw new ArgumentOutOfRangeException(nameof(channels));
046 |         }
047 | 
048 |         // owner按对象身份区分，避免两个值相同的owner互相覆盖。
049 |         private sealed class OwnerComparer : IEqualityComparer<object>
050 |         {
051 |             public new bool Equals(object x, object y) => ReferenceEquals(x, y);
052 |             public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
053 |         }
054 |     }
055 | }
```

| 行区间 | 调用、语义和状态权限 |
|---|---|
| 1–6 | 文件级中文职责总览：描述真实功能、负责/不负责范围和协作边界；不改变运行状态。 |
| 7–18 | 权限通道为Move/Attack独立位标记；不混成全局canAct。 |
| 19–21 | 按owner对象身份保存限制；比较器避免两个值相等但不同owner互相覆盖。 |
| 22–28 | 检查owner和通道合法性，只设置这个owner的限制，不写其他owner。 |
| 29–33 | 只移除此owner；null无动作，不能清空所有限制。 |
| 34–41 | 验证查询通道，任何owner覆盖相应通道就拒绝；无覆盖才允许。 |
| 42–48 | 只接受定义过的Move/Attack位，未知位抛出参数异常。 |
| 49–55 | ReferenceEquals与RuntimeHelpers.GetHashCode成对按身份比较，保证字典语义一致。 |

用户结论：待审。修改任何职责或语义后，重新生成受影响行号并复审。

## Assets/OSSASTRA/Scripts/Characters/Core/Health.cs

SHA256：5C4CE90D6FCF413FB308292FB8D26736F167D161E5D90FE393E80A0BAF7BAA80

```text
001 | /*
002 |  * 脚本：Health
003 |  * 功能：保存目标身份与唯一可写的当前HP，限制伤害结果不低于零。
004 |  * 边界：不管理动画、控制权、敌人AI或正式死亡流程。
005 |  * 协作：AttackRelease经内部ApplyDamage写入；开发靶只持有同一对象。
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
032 |     }
033 | }
```

| 行区间 | 调用、语义和状态权限 |
|---|---|
| 1–6 | 文件级中文职责总览：描述真实功能、负责/不负责范围和协作边界；不改变运行状态。 |
| 7–11 | 独立HP权威，不依赖Unity组件或DataBus副本。 |
| 12–17 | 身份、上限、当前HP只读；Current唯一私有写入口，不向外暴露setter。 |
| 18–26 | 拒绝空身份和非正上限，初始化满HP；开发靶创建它不代表完整正式角色生命系统已迁完。 |
| 27–33 | 仅领域程序集结算调用；拒绝非正伤害，扣减并钳制0，不播放表现或广播事件。 |

用户结论：待审。修改任何职责或语义后，重新生成受影响行号并复审。

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterContracts.cs

SHA256：A11E2F17548F6AF1C9589AE2CAA2ABC723B21510A2A94085F4E5AB50CE13C500

```text
001 | /*
002 |  * 脚本：CharacterContracts
003 |  * 功能：集中声明角色领域的窄职责契约，供组合根和DataBus依赖。
004 |  * 边界：只定义能力；实际实现与调用顺序不在本文件。
005 |  * 协作：Inspector仍保存具体组件；Root运行时按接口连接，只有必要模块接受Bus初始化。
006 |  */
007 | using Ossastra.CharacterCore;
008 | using UnityEngine;
009 | 
010 | namespace Ossastra.Characters
011 | {
012 |     /// <summary>由场景实控权威回答谁可以采样玩家输入；输入源不保存第二份选中状态。</summary>
013 |     public interface ICharacterPlayerControlSource
014 |     {
015 |         bool IsPlayerInputAllowed(ICharacterInputSource source);
016 |     }
017 |     /// <summary>仅为确需角色Bus的模块提供显式初始化；失败阻止角色启动。</summary>
018 |     public interface ICharacterSubsystem { bool Initialize(CharacterDataBus dataBus); }
019 |     public interface ICharacterInputSource
020 |     {
021 |         CharacterInputFrame CurrentFrame { get; }
022 |         bool IsConfigured { get; }
023 |         CharacterInputFrame Read();
024 |     }
025 |     /// <summary>执行实际运动并报告结果；不决定攻击朝向。</summary>
026 |     public interface ICharacterMovement : ICharacterSubsystem
027 |     {
028 |         bool IsMoving { get; }
029 |         Vector3 Velocity { get; }
030 |         void Tick(bool allowed, float delta);
031 |         void Teleport(Vector3 position);
032 |     }
033 |     public interface ICharacterOrientation
034 |     {
035 |         Vector3 AttackDirection { get; }
036 |         float VisualFacingX { get; }
037 |         void ResolveDirections(Vector2 moveInput, Vector3? pointerDirection, Vector2 stickDirection, bool movementFacing);
038 |         void OrientTowards(Vector3 worldPosition);
039 |         void FaceAttackDirection();
040 |     }
041 |     /// <summary>从角色状态更新表现与暂停，不反向产生命中。</summary>
042 |     public interface ICharacterPresentation : ICharacterSubsystem
043 |     {
044 |         string CurrentClip { get; }
045 |         void SetPaused(bool paused);
046 |         void Present();
047 |     }
048 |     public interface ICharacterHitResolver
049 |     {
050 |         string LastResult { get; }
051 |         void Resolve(AttackRelease release, Vector3 releaseDirection);
052 |     }
053 |     // World target adapter: retains the existing sole Health writer.
054 |     public interface ICharacterHitTarget
055 |     {
056 |         Health Health { get; }
057 |         void ShowHit();
058 |     }
059 | }
```

| 行区间 | 调用、语义和状态权限 |
|---|---|
| 1–6 | 文件级中文职责总览：描述真实功能、负责/不负责范围和协作边界；不改变运行状态。 |
| 7–11 | 接口共享只使用领域释放/Health和Unity向量，不引用具体组件或旧框架。 |
| 12–16 | 新增场景玩家许可查询契约。协调者实现，InputHandler消费；参数是输入源身份，返回只读许可，不把全局选择状态塞进每个Bus。 |
| 17–17 | <summary>仅为确需角色Bus的模块提供显式初始化；失败阻止角色启动。</summary> |
| 18–18 | 仅真实需要DataBus注入的模块实现bool Initialize；false让Root停止后续初始化。 |
| 19–24 | 读取配置状态/当前帧与Read采样；没有无意义Initialize。实际实现InputHandler与替换测试InputStub都可由Root/Bus消费。 |
| 25–25 | <summary>执行实际运动并报告结果；不决定攻击朝向。</summary> |
| 26–32 | 包含初始化、许可位移、显式传送和只读运动结果；不把朝向职责塞回运动。 |
| 33–40 | 分别公开攻击/显示状态和有语义的解析/目标/攻击显示操作；消费者不会从同一Facing值猜所有含义。 |
| 41–41 | <summary>从角色状态更新表现与暂停，不反向产生命中。</summary> |
| 42–47 | 表现模块需要Bus，接口只给clip诊断、暂停和按真实状态显示，不能释放动作或扣血。 |
| 48–53 | 一次Release加方向值完成世界适配；不要求无用途Bus初始化。参数完整所以可模拟替换，仍由AttackRelease/Health掌握结算。 |
| 54–59 | 世界目标提供原Health和受击反馈；开发靶实现接口，正式解析器不依赖开发类，不另建敌人状态系统。 |

用户结论：待审。修改任何职责或语义后，重新生成受影响行号并复审。

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterControlCoordinator.cs

SHA256：393DEF54934515440948E9C587A0B05D8A04067F089ECB524DB16E5384B4951E

```text
001 | /*
002 |  * 脚本：CharacterControlCoordinator
003 |  * 功能：保存同组角色唯一的玩家实控对象，并读取Inspector切换输入。
004 |  * 边界：不接管各角色动作时钟、HP或AI；不使用全局单例。
005 |  * 协作：在Root之前更新；InputHandler通过只读契约查询许可，不复制选中布尔。
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
017 |     public sealed class CharacterControlCoordinator : MonoBehaviour, ICharacterPlayerControlSource
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
030 | 
031 |         private void Awake()
032 |         {
033 |             if (characters == null) characters = Array.Empty<CharacterRoot>();
034 |             inputs = new CharacterInputHandler[characters.Length];
035 |             var identities = new HashSet<string>(StringComparer.Ordinal);
036 |             var sources = new HashSet<CharacterInputHandler>();
037 |             var valid = characters.Length >= 2 && initialIndex < characters.Length && initialIndex >= 0 &&
038 |                 switchAction != null && switchAction.bindings.Count > 0;
039 |             // 先完整校验，再绑定；合法成员在配置错误时也归本协调者关闭，不能退回双控。
040 |             for (var i = 0; i < characters.Length; i++)
041 |             {
042 |                 var character = characters[i];
043 |                 if (character == null) { valid = false; continue; }
044 |                 inputs[i] = character.GetComponent<CharacterInputHandler>();
045 |                 if (inputs[i] == null || !sources.Add(inputs[i]) ||
046 |                     string.IsNullOrWhiteSpace(character.ActorId) || !identities.Add(character.ActorId)) valid = false;
047 |             }
048 |             foreach (var input in inputs)
049 |                 if (input != null && !input.CanBindPlayerControl(this))
050 |                 { Status = "输入已归另一协调者；拒绝抢占"; enabled = false; return; }
051 |             foreach (var input in inputs) if (input != null) input.BindPlayerControl(this);
052 |             bindingsOwned = true;
053 |             configured = valid;
054 |             if (!valid) { Status = "角色／身份／输入配置无效，输入关闭"; enabled = false; }
055 |         }
056 | 
057 |         private void OnEnable()
058 |         {
059 |             switchIntent.SuppressUntilRelease();
060 |             if (!configured) return;
061 |             switchAction.Enable();
062 |             Select(characters[initialIndex]);
063 |         }
064 | 
065 |         private void Update()
066 |         {
067 |             if (Current != null && (!Current.isActiveAndEnabled || Current.DataBus == null)) Revoke("当前角色不可用");
068 |             var held = switchAction != null && switchAction.enabled && switchAction.IsPressed();
069 |             if (switchIntent.Observe(held) && focused && !applicationPaused) SwitchNext();
070 |         }
071 | 
072 |         /// <summary>只有Current对应的输入源可消费玩家输入；切换当帧新源禁止采样。</summary>
073 |         public bool IsPlayerInputAllowed(ICharacterInputSource source)
074 |         {
075 |             if (!configured || !isActiveAndEnabled || !focused || applicationPaused ||
076 |                 Current == null || !Current.isActiveAndEnabled || Time.frameCount <= handoverFrame) return false;
077 |             for (var i = 0; i < characters.Length; i++)
078 |                 if (characters[i] == Current) return ReferenceEquals(inputs[i], source);
079 |             return false;
080 |         }
081 | 
082 |         /// <summary>场景级切人入口；不会取消、复制或重建任一角色的已接受动作。</summary>
083 |         public bool SwitchNext()
084 |         {
085 |             if (!configured || !isActiveAndEnabled || !focused || applicationPaused) return false;
086 |             var index = Array.IndexOf(characters, Current);
087 |             for (var step = 1; step <= characters.Length; step++)
088 |             {
089 |                 var candidate = characters[(index + step) % characters.Length];
090 |                 if (candidate != null && candidate.isActiveAndEnabled && candidate.DataBus != null)
091 |                 { Select(candidate); return true; }
092 |             }
093 |             Revoke("没有可控制角色");
094 |             return false;
095 |         }
096 | 
097 |         private void Select(CharacterRoot next)
098 |         {
099 |             // 先撤回旧权威并清所有快照，再授予新身份。整帧禁入避免外部晚调用导致同帧双控。
100 |             Current = null;
101 |             foreach (var input in inputs) if (input != null) input.ResetPlayerInput();
102 |             handoverFrame = Time.frameCount;
103 |             Current = next;
104 |             Status = next == null ? "无实控" : "玩家输入 → " + next.ActorId;
105 |         }
106 | 
107 |         private void Revoke(string reason) { Select(null); Status = reason; }
108 |         private void OnDisable()
109 |         {
110 |             if (switchAction != null) switchAction.Disable();
111 |             switchIntent.SuppressUntilRelease();
112 |             // 未取得绑定所有权的失败实例不能清理其它协调者的快照或重武装状态。
113 |             if (bindingsOwned)
114 |             {
115 |                 if (configured) Revoke("协调者禁用，输入关闭");
116 |                 else foreach (var input in inputs) if (input != null) input.ResetPlayerInput();
117 |             }
118 |         }
119 |         private void OnApplicationFocus(bool value)
120 |         {
121 |             focused = value;
122 |             switchIntent.SuppressUntilRelease();
123 |             if (bindingsOwned) foreach (var input in inputs) if (input != null) input.ResetPlayerInput();
124 |         }
125 |         private void OnApplicationPause(bool value)
126 |         {
127 |             applicationPaused = value;
128 |             switchIntent.SuppressUntilRelease();
129 |             if (bindingsOwned) foreach (var input in inputs) if (input != null) input.ResetPlayerInput();
130 |         }
131 |         // 输入仍保留“曾受管理”的关系；本对象销毁后查询失败关闭，不能突然恢复多个玩家源。
132 |         private void OnDestroy() { if (switchAction != null) switchAction.Dispose(); }
133 |     }
134 | }
```

| 行区间 | 调用、语义和状态权限 |
|---|---|
| 1–6 | 文件级中文职责总览：描述真实功能、负责/不负责范围和协作边界；不改变运行状态。 |
| 7–14 | 依赖InputAction、领域按下边沿和单角色Root；不是全局注册表。 |
| 15–30 | 协调者Update在Root之前；Inspector角色数组、初始索引、切换Action为场景配置。Current为唯一选择真值；inputs只是成员输入引用；handoverFrame是移交屏障，bindingsOwned标本实例是否真的持有绑定。 |
| 31–56 | 只初始化一次配置：空数组标准化，检查人数、索引、Action绑定、成员/输入/身份唯一性。先检查所有源是否能绑定，再统一绑定；已有活owner时拒绝抢占且不执行其清理。无效成员配置关闭可知输入，禁止退回双控。 |
| 57–64 | 重新启用先抑制切换Held；只有有效配置才能启用Action并选初始角色。此时不重建角色Bus/动作。 |
| 65–71 | 检测当前Root失能或初始化失败并撤回输入；读取配置的切换Action，经switchIntent转为按下边沿，焦点/应用暂停允许时才调用SwitchNext。 |
| 72–81 | 输入源通过接口查询：配置、组件、焦点、应用暂停、当前Root和移交帧均通过后，只有Current映射的同一输入对象获得许可。切换当帧没有新消费者；角色不保存isSelected副本。 |
| 82–96 | SwitchNext由Action或开发按钮调用；从Current之后循环找已初始化且启用的Root，交给Select。全部不可用则具名撤回。循环上界是成员数，无静态寻址/AI行为。 |
| 97–106 | 移交顺序是Current清空→清所有成员输入快照并解除攻击武装→记录本帧→赋新Current→更新只读状态说明。即使在旧Root处理后被晚调用，新Root也要等下一帧，避免双控。动作Runtime/权限/位置/动画不在此改写。 |
| 107–118 | Revoke复用Select(null)并保留原因。OnDisable关闭切换Action和边沿；只有真正拥有绑定的实例可清输入。错误配置保留诊断文字，冲突失败实例不能干扰原owner。 |
| 119–124 | 失焦和返回都抑制切换Held、清本owner输入；不通过GUI或别人的owner解除权限。 |
| 125–130 | 应用暂停变化也清重武装资格。此值只管玩家输入许可，各Root自己的暂停/取消仍由原生命周期管理。 |
| 131–134 | 销毁时释放自己的InputAction；InputHandler保留受管理关系，通过Unity对象有效性判断失败关闭，不因协调者消失突然回退双控。 |

用户结论：待审。修改任何职责或语义后，重新生成受影响行号并复审。

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterDataBus.cs

SHA256：7BB25084AAF056FA4E96FA3AD61644A2BBCA0D7064C119EC11806F3822A5EF16

```text
001 | /*
002 |  * 脚本：CharacterDataBus
003 |  * 功能：为单个角色提供输入、运动、朝向、动作和权限的只读访问门面。
004 |  * 边界：状态仍由原模块唯一写入；不复制可写HP、动作时钟或全局控制状态。
005 |  * 协作：Root创建；Movement／Spine及消费者实时读取接口拥有者。
006 |  */
007 | using System;
008 | using Ossastra.CharacterCore;
009 | using UnityEngine;
010 | 
011 | namespace Ossastra.Characters
012 | {
013 |     // Per-character read facade; referenced modules remain the sole mutable-state owners.
014 |     public sealed class CharacterDataBus
015 |     {
016 |         private readonly ICharacterInputSource input;
017 |         private readonly ICharacterMovement movement;
018 |         private readonly ICharacterOrientation orientation;
019 |         private readonly BasicActionRuntime actionRuntime;
020 |         private readonly ControlAuthority controlAuthority;
021 | 
022 |         public string ActorId { get; }
023 |         public CharacterInputFrame Input => input.CurrentFrame;
024 |         public bool IsMoving => movement.IsMoving;
025 |         public Vector3 Velocity => movement.Velocity;
026 |         public Vector3 AttackDirection => orientation.AttackDirection;
027 |         public float VisualFacingX => orientation.VisualFacingX;
028 |         public bool ActionActive => actionRuntime.IsActive;
029 |         public Guid ActionId => actionRuntime.ActionId;
030 |         public bool MoveReady => actionRuntime.MoveReady;
031 |         public bool AttackReady => actionRuntime.AttackReady;
032 |         public bool AllowsMovement => controlAuthority.Allows(ControlChannel.Move);
033 |         public bool AllowsAttack => controlAuthority.Allows(ControlChannel.Attack);
034 | 
035 |         public CharacterDataBus(string actorId, ICharacterInputSource input, ICharacterMovement movement,
036 |             ICharacterOrientation orientation, BasicActionRuntime actionRuntime, ControlAuthority controlAuthority)
037 |         {
038 |             if (string.IsNullOrWhiteSpace(actorId)) throw new ArgumentException("Actor identity required", nameof(actorId));
039 |             if (input == null) throw new ArgumentNullException(nameof(input));
040 |             if (movement == null) throw new ArgumentNullException(nameof(movement));
041 |             if (orientation == null) throw new ArgumentNullException(nameof(orientation));
042 |             if (actionRuntime == null) throw new ArgumentNullException(nameof(actionRuntime));
043 |             if (controlAuthority == null) throw new ArgumentNullException(nameof(controlAuthority));
044 |             ActorId = actorId;
045 |             this.input = input;
046 |             this.movement = movement;
047 |             this.orientation = orientation;
048 |             this.actionRuntime = actionRuntime;
049 |             this.controlAuthority = controlAuthority;
050 |         }
051 |     }
052 | }
```

| 行区间 | 调用、语义和状态权限 |
|---|---|
| 1–6 | 文件级中文职责总览：描述真实功能、负责/不负责范围和协作边界；不改变运行状态。 |
| 7–13 | Bus读Unity向量和领域runtime/权限，但不负责设备、Spine、物理查询或事件广播。 |
| 14–21 | 私有readonly接口引用指向原写入者；runtime/controlAuthority也是原对象引用，不复制状态。 |
| 22–34 | 身份初始化后只读；Input返回值类型副本；移动、显示和攻击方向分别读取对应接口。动作身份/Ready与权限均实时查询原所有者，无public setter。 |
| 35–52 | Root明确注入五类所有者与角色身份；检查空身份和每个必需依赖后保存引用。禁止全局服务定位，也没有给消费者任意写Bus的入口。 |

用户结论：待审。修改任何职责或语义后，重新生成受影响行号并复审。

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterInputHandler.cs

SHA256：DB562F2479DCA225A2445C376B45491D79631FF24E289864A31928CA7998DC1D

```text
001 | /*
002 |  * 脚本：CharacterInputHandler
003 |  * 功能：从Inspector配置的InputAction采样单角色输入快照。
004 |  * 边界：只采输入；不接受动作、不驱动物理或写HP；每实例管理Action生命周期。
005 |  * 协作：Root经ICharacterInputSource读取；场景协调者只读授权；未受管理时保持单角色使用。
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
023 |     public sealed class CharacterInputHandler : MonoBehaviour, ICharacterInputSource
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

| 行区间 | 调用、语义和状态权限 |
|---|---|
| 1–6 | 文件级中文职责总览：描述真实功能、负责/不负责范围和协作边界；不改变运行状态。 |
| 7–13 | 只依赖Unity输入和UI有效性查询，不引入动作runtime、HP或Spine。 |
| 14–21 | 值类型采样快照：移动、屏幕指针、手柄方向和按键状态均camelCase；attackPointerBlocked与Held分开，防止UI内按住移出后变成幽灵新按下。 |
| 22–41 | 每个角色一个输入组件；十个InputAction在Inspector序列化，绑定与逻辑分离，字段Header说明旧VELGITA来源，不硬读设备当前按键。 |
| 42–42 | 每个输入实例保存owner→UI阻挡矩形；开发组件只登记自己的区域，停用时移除。IsPointerBlocked先查UGUI，再把屏幕坐标转GUI坐标并检查区域，不更改动作资格。 |
| 43–64 | 场景权威引用与受管理标记不复制Current。HasPlayerInput通过接口实时查询；Unity provider已销毁则失败关闭。CanBind拒绝抢占活owner；Bind设置来源并清输入快照。ResetPlayerInput清Frame并要求真实采样松键，再允许新攻击；不修改动作时钟。 |
| 65–75 | 每个输入实例保存owner→UI阻挡矩形；开发组件只登记自己的区域，停用时移除。IsPointerBlocked先查UGUI，再把屏幕坐标转GUI坐标并检查区域，不更改动作资格。 |
| 76–82 | 唯一输入写入点是此组件。Bus只取其副本；IsConfigured检查移动/攻击绑定和至少一种瞄准来源，不能把缺配置的Prefab默认为可运行。 |
| 83–97 | 同步枚举一份动作名单，供启用/停用/释放复用；没有StartCoroutine或跨帧等待。新增动作需加入此名单。 |
| 98–98 | 启用时Enable各动作；禁用时先清输入帧再Disable；销毁时Dispose自己拥有的动作。不会操作别的角色动作。 |
| 99–99 | 输入禁用清快照与武装并禁用自身Action；重新启用后的Held不能成为新攻击。 |
| 100–101 | 启用时Enable各动作；禁用时先清输入帧再Disable；销毁时Dispose自己拥有的动作。不会操作别的角色动作。 |
| 102–104 | 接口消费者Root调用：禁用/无移动动作时发布零帧；否则按Action读取移动。鼠标保留原屏幕位置，但合法性需在窗口内且不压UI；手柄方向独立采样。 |
| 105–106 | 在读取任何玩家设备值之前拒绝非实控源，发布空快照；Root的Advance仍执行，所以旧动作可以继续完成。 |
| 107–119 | 接口消费者Root调用：禁用/无移动动作时发布零帧；否则按Action读取移动。鼠标保留原屏幕位置，但合法性需在窗口内且不压UI；手柄方向独立采样。 |
| 120–122 | 先读物理Held；必须观察到未按住才武装。Frame.attackHeld是已通过移交重武装的Held，Root仍用AttackIntent检查边沿和Runtime判断资格；两者不产生第二动作时钟。 |
| 123–132 | Held来自Action原始按住状态，不因UI隐藏；仅标记鼠标指针阻挡，让Root消耗边沿而拒绝请求。Cancel/Pause/开发命令取本帧按下；最后一次性发布CurrentFrame并返回同一值。 |
| 133–135 | 统一空引用/启用状态检查后查询WasPressedThisFrame；这是按帧查询，不是限帧或等待协程。 |

用户结论：待审。修改任何职责或语义后，重新生成受影响行号并复审。

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterMeleeHit.cs

SHA256：E6A4C619FA9DEEC760EEE1C37F1ABE2C1803E0507C3CAA323FA4C03229FAF220

```text
001 | /*
002 |  * 脚本：CharacterMeleeHit
003 |  * 功能：将一次释放的方向快照转换为物理范围检测和目标批次。
004 |  * 边界：不创建动作时钟或另一份HP；不直接依赖开发靶类型。
005 |  * 协作：Root经ICharacterHitResolver调用；ICharacterHitTarget连接Health及命中表现。
006 |  */
007 | using System;
008 | using System.Linq;
009 | using Ossastra.CharacterCore;
010 | using UnityEngine;
011 | 
012 | namespace Ossastra.Characters
013 | {
014 |     public sealed class CharacterMeleeHit : MonoBehaviour, ICharacterHitResolver
015 |     {
016 |         [SerializeField, Min(0)] private float reach = .8f;
017 |         [SerializeField, Min(.01f)] private float radius = .65f;
018 |         [SerializeField] private float height = .8f;
019 |         [SerializeField] private LayerMask targetLayers = ~0;
020 |         public string LastResult { get; private set; } = "未释放";
021 | 
022 | public void Resolve(AttackRelease release, Vector3 releaseDirection)
023 |         {
024 |             var direction = releaseDirection;
025 |             var center = transform.position + Vector3.up * height + direction * reach;
026 |             var targets = Physics.OverlapSphere(center, radius, targetLayers, QueryTriggerInteraction.Collide)
027 |                 .Select(c => c.GetComponentsInParent<MonoBehaviour>().OfType<ICharacterHitTarget>().FirstOrDefault()).Where(t => t != null).Distinct().ToArray();
028 |             var outcomes = release.Resolve(targets.Select(t => t.Health));
029 |             foreach (var outcome in outcomes)
030 |                 if (outcome.Reason == HitReason.Applied)
031 |                     targets.First(t => t.Health.Id == outcome.TargetId).ShowHit();
032 |             LastResult = outcomes.Count == 0 ? "空挥" : string.Join("; ", outcomes.Select(o =>
033 |                 o.TargetId + " " + o.HpBefore + "→" + o.HpAfter + " " + o.Reason));
034 |         }
035 |     }
036 | }
```

| 行区间 | 调用、语义和状态权限 |
|---|---|
| 1–6 | 文件级中文职责总览：描述真实功能、负责/不负责范围和协作边界；不改变运行状态。 |
| 7–13 | 只依赖领域结算、Unity物理和目标接口；不再引用Development或保存无用途Bus。 |
| 14–21 | Inspector几何参数保持reach/radius/height/LayerMask；LastResult是诊断文本，不是第二份HP或命中权威。 |
| 22–36 | 仅由Root收到Released后调用，传入的releaseDirection是释放帧值快照。计算球心后OverlapSphere，沿Collider父级查ICharacterHitTarget，过滤空并按目标对象去重。把同一批Health交给AttackRelease；只对Applied结果ShowHit，最后格式化实际HP前后值。Release自身防止二次结算。 |

用户结论：待审。修改任何职责或语义后，重新生成受影响行号并复审。

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterMovement.cs

SHA256：7195E1C36830F02896DD8301F5683ABB7EE09BA04249554DC71FEF23EC0DDD6C

```text
001 | /*
002 |  * 脚本：CharacterMovement
003 |  * 功能：依据角色输入与移动许可执行XZ位移、碰撞及贴地。
004 |  * 边界：只管理运动；不决定攻击方向、不结算伤害。
005 |  * 协作：Root初始化Bus并调用Tick；CharacterController实际位移决定速度和Run资格。
006 |  */
007 | using System;
008 | using UnityEngine;
009 | 
010 | namespace Ossastra.Characters
011 | {
012 |     [DisallowMultipleComponent]
013 |     [RequireComponent(typeof(CharacterController))]
014 |     public sealed class CharacterMovement : MonoBehaviour, ICharacterMovement
015 |     {
016 |         [SerializeField, Min(.01f)] private float speed = 3;
017 |         [SerializeField, Min(.01f)] private float groundStick = 2;
018 |         private CharacterController body;
019 |         private CharacterDataBus dataBus;
020 |         public bool IsMoving { get; private set; }
021 |         public Vector3 Velocity { get; private set; }
022 | 
023 |         public bool Initialize(CharacterDataBus dataBus)
024 |         {
025 |             if (dataBus == null) throw new ArgumentNullException(nameof(dataBus));
026 |             this.dataBus = dataBus;
027 |             body = GetComponent<CharacterController>();
028 |             return body != null;
029 |         }
030 | 
031 |         public void Tick(bool allowed, float delta)
032 |         {
033 |             IsMoving = false;
034 |             Velocity = Vector3.zero;
035 |             if (dataBus == null || body == null || !body.enabled || delta <= 0) return;
036 |             var move = allowed ? Vector2.ClampMagnitude(dataBus.Input.move, 1) : Vector2.zero;
037 |             var before = transform.position;
038 |             body.Move((new Vector3(move.x, 0, move.y) * speed + Vector3.down * groundStick) * delta);
039 |             // 用碰撞处理后的实际位移报告运动，而不是把“按住移动键”当成真的跑动。
040 |             var displacement = transform.position - before;
041 |             displacement.y = 0;
042 |             Velocity = displacement / delta;
043 |             IsMoving = displacement.sqrMagnitude > .000001f;
044 |         }
045 | 
046 |         public void Teleport(Vector3 position)
047 |         {
048 |             if (body == null) return;
049 |             var wasEnabled = body.enabled;
050 |             body.enabled = false;
051 |             transform.position = position;
052 |             body.enabled = wasEnabled;
053 |             IsMoving = false;
054 |             Velocity = Vector3.zero;
055 |         }
056 |     }
057 | }
```

| 行区间 | 调用、语义和状态权限 |
|---|---|
| 1–6 | 文件级中文职责总览：描述真实功能、负责/不负责范围和协作边界；不改变运行状态。 |
| 7–11 | 运动只依赖Unity CharacterController及初始化参数验证，不决定攻击角度。 |
| 12–22 | 同角色唯一运动组件，RequireComponent确保胶囊控制器存在；speed/groundStick仍保留原Inspector数据。速度和是否移动由实际位移得出。 |
| 23–30 | Root通过ICharacterSubsystem注入Bus，验证非空并绑定同物体CharacterController；找不到控制器返回false，让Root停止初始化。 |
| 31–45 | 每次清旧速度，未初始化/控制器停用/delta非正直接返回。获准时从Bus.Input.move取向量并限制长度，加显式贴地运动；碰撞后的实际水平差值计算Velocity/IsMoving，不把输入当作实际速度。 |
| 46–57 | 显式位置变更入口：暂时停用CharacterController，改位置，再恢复原启用状态，清移动结果；不更新朝向或HP。 |

用户结论：待审。修改任何职责或语义后，重新生成受影响行号并复审。

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterOrientation.cs

SHA256：9B53699FCCDBA86EBA6FAB28CB0F711C2AAD10C502486A1B306B15498A9C8A13

```text
001 | /*
002 |  * 脚本：CharacterOrientation
003 |  * 功能：分别保存可见左右翻转和实际攻击方向。
004 |  * 边界：有效鼠标优先，其次摇杆、移动，最后保留；方向有限、非零且平面归一。
005 |  * 协作：Root解析输入并在接受／释放时要求面向攻击；Spine只读取显示翻转。
006 |  */
007 | using UnityEngine;
008 | 
009 | namespace Ossastra.Characters
010 | {
011 |     [DisallowMultipleComponent]
012 |     public sealed class CharacterOrientation : MonoBehaviour, ICharacterOrientation
013 |     {
014 |         public Vector3 AttackDirection { get; private set; } = Vector3.right;
015 |         public float VisualFacingX { get; private set; } = 1;
016 | 
017 |         public void ResolveDirections(Vector2 moveInput, Vector3? pointerDirection, Vector2 stickDirection, bool movementFacing)
018 |         {
019 |             var pointerValid = pointerDirection.HasValue && TrySetAttackDirection(pointerDirection.Value);
020 |             if (!pointerValid && !TrySetAttackDirection(new Vector3(stickDirection.x, 0, stickDirection.y)))
021 |                 TrySetAttackDirection(new Vector3(moveInput.x, 0, moveInput.y));
022 |             if (movementFacing && Mathf.Abs(moveInput.x) > .01f) VisualFacingX = Mathf.Sign(moveInput.x);
023 |         }
024 | 
025 |         public void OrientTowards(Vector3 worldPosition) { TrySetAttackDirection(worldPosition - transform.position); }
026 | 
027 |         public void FaceAttackDirection()
028 |         {
029 |             if (Mathf.Abs(AttackDirection.x) > .01f) VisualFacingX = Mathf.Sign(AttackDirection.x);
030 |         }
031 | 
032 |         private bool TrySetAttackDirection(Vector3 direction)
033 |         {
034 |             direction.y = 0;
035 |             if (float.IsNaN(direction.x) || float.IsNaN(direction.z) || float.IsInfinity(direction.x) ||
036 |                 float.IsInfinity(direction.z) || direction.sqrMagnitude < .0001f) return false;
037 |             AttackDirection = direction.normalized;
038 |             return true;
039 |         }
040 |     }
041 | }
```

| 行区间 | 调用、语义和状态权限 |
|---|---|
| 1–6 | 文件级中文职责总览：描述真实功能、负责/不负责范围和协作边界；不改变运行状态。 |
| 7–10 | 该组件只用世界向量和数学函数，不依赖输入设备、动画或物理执行。 |
| 11–16 | 每角色独立状态：AttackDirection默认右，VisualFacingX默认1；属性只有本组件可写，前者进物理出手，后者只进显示。 |
| 17–24 | 调用方先决定是否允许移动显示。攻击方向依次尝试合法鼠标、右摇杆、移动向量；全部无效则保留原合法方向。可动水平输入独立改变VisualFacingX，不覆盖攻击方向；W/S不抖左右。 |
| 25–26 | 显式世界目标适配，只更新攻击方向，不再暗改显示。开发传送后需显式FaceAttackDirection，调用含义可查。 |
| 27–31 | 只在攻击接受/释放等明确场合把显示朝向转到出手侧；攻击X分量近零时保留左右符号，避免正前/正后抖动。 |
| 32–41 | 压平Y；拒绝NaN、Infinity和近零长度；合法方向归一后成为唯一AttackDirection，返回成功与否供有序回退。 |

用户结论：待审。修改任何职责或语义后，重新生成受影响行号并复审。

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterRoot.cs

SHA256：970E9FDE78D07FDBB66B73A8B2305C1173C55F5C726061BF36156B6632DBA8AE

```text
001 | /*
002 |  * 脚本：CharacterRoot
003 |  * 功能：组织单角色的接口接线、输入、动作事实、运动和表现。
004 |  * 边界：持有本角色运行时；不实现设备绑定、目标HP写入或开发GUI。
005 |  * 协作：输入→朝向→请求→推进→释放命中→运动→Spine；Bus只读访问原状态。
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
033 |         private ICharacterMovement mover;
034 |         private ICharacterOrientation orienter;
035 |         private ICharacterPresentation presentation;
036 |         private ICharacterHitResolver hitResolver;
037 |         private BasicActionRuntime runtime;
038 |         private BasicAttackDefinition definition;
039 |         private bool ready, focused = true, paused, applicationPaused;
040 |         private string lastFact = "Idle";
041 |         public CharacterDataBus DataBus { get; private set; }
042 |         public string ActorId => actorId;
043 |         public string LastFact => lastFact;
044 |         public bool IsPaused => paused || applicationPaused;
045 |         public bool ActionActive => DataBus != null && DataBus.ActionActive;
046 | 
047 |         private void Awake()
048 |         {
049 |             if (viewCamera == null) viewCamera = Camera.main;
050 |             if (movement == null || orientation == null || view == null || melee == null || input == null || viewCamera == null)
051 |             { Debug.LogError("Character requires registered input/movement/orientation/view/melee/camera", this); enabled = false; return; }
052 |             // Inspector保留具体组件槽；运行期通过窄接口消费，不反向依赖开发GUI或测试靶。
053 |             inputSource = input;
054 |             mover = movement;
055 |             orienter = orientation;
056 |             presentation = view;
057 |             hitResolver = melee;
058 |             if (!inputSource.IsConfigured) { Debug.LogError("Character actions are not configured", this); enabled = false; return; }
059 |             try
060 |             {
061 |                 definition = new BasicAttackDefinition(releaseAt, moveReadyAt, attackReadyAt, finishAt, damage);
062 |                 runtime = new BasicActionRuntime(actorId);
063 |             }
064 |             catch (ArgumentException error) { Debug.LogError("Character configuration: " + error.Message, this); enabled = false; return; }
065 |             // 每角色独立Bus只读取原所有者；只有确需Bus的运动和表现执行初始化。
066 |             DataBus = new CharacterDataBus(actorId, inputSource, mover, orienter, runtime, authority);
067 |             ready = InitializeSubsystems(DataBus, mover, presentation);
068 |             if (!ready) { Debug.LogError("Character subsystem initialization failed", this); enabled = false; }
069 |         }
070 | 
071 |         private static bool InitializeSubsystems(CharacterDataBus dataBus, params ICharacterSubsystem[] subsystems)
072 |         {
073 |             foreach (var subsystem in subsystems) if (subsystem == null || !subsystem.Initialize(dataBus)) return false;
074 |             return true;
075 |         }
076 | 
077 |         private void Update()
078 |         {
079 |             if (!ready) return;
080 |             // 受管理输入源会先检查场景许可；非实控发布空帧，但下方动作时钟仍继续。
081 |             inputSource.Read();
082 |             var frame = DataBus.Input;
083 |             if (frame.pause) SetPaused(!paused);
084 |             if (frame.cancel) CancelAction("player-cancel");
085 |             var canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
086 |             if (focused && !IsPaused)
087 |                 orienter.ResolveDirections(frame.move, PointerDirection(frame), frame.hasDirectionalAim ? frame.aimDirection : Vector2.zero, canMove);
088 |             // 输入边沿不等于动作成功：焦点、UI和Runtime的暂停/权限/Ready资格分别把关。
089 |             if (intent.Observe(frame.attackHeld) && focused && !frame.attackPointerBlocked)
090 |             {
091 |                 var request = runtime.Request(definition, "player-input", authority, IsPaused);
092 |                 if (request.PreviousTerminal != null) Observe(request.PreviousTerminal);
093 |                 if (request.Accepted) { Observe(request.Fact); orienter.FaceAttackDirection(); }
094 |                 else lastFact = "Rejected: " + request.Reason;
095 |             }
096 |             // Accepted只建立动作身份；只有Released带一次性命中凭据。切人不会重发事实。
097 |             foreach (var fact in runtime.Advance(Time.deltaTime, IsPaused || !focused))
098 |             {
099 |                 Observe(fact);
100 |                 if (fact.Release != null)
101 |                 {
102 |                     // 在释放当帧按值采样方向；不是接受动作时锁定，前摇中仍可重新瞄准。
103 |                     var releaseDirection = orienter.AttackDirection;
104 |                     orienter.FaceAttackDirection();
105 |                     hitResolver.Resolve(fact.Release, releaseDirection);
106 |                 }
107 |             }
108 |             // 本帧可能跨过MoveReady；推进事实后重新查询许可，Run依据真实位移。
109 |             canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
110 |             mover.Tick(canMove, IsPaused || !focused ? 0 : Time.deltaTime);
111 |             presentation.SetPaused(IsPaused || !focused);
112 |             presentation.Present();
113 |         }
114 | 
115 |         private Vector3? PointerDirection(CharacterInputFrame frame)
116 |         {
117 |             if (!frame.hasPointer) return null;
118 |             var plane = new Plane(Vector3.up, transform.position);
119 |             var ray = viewCamera.ScreenPointToRay(frame.pointer);
120 |             if (!plane.Raycast(ray, out var distance)) return null;
121 |             var direction = ray.GetPoint(distance) - transform.position;
122 |             direction.y = 0;
123 |             return direction.sqrMagnitude < .0001f ? (Vector3?)null : direction;
124 |         }
125 | 
126 |         private void Observe(ActionFact fact)
127 |         {
128 |             lastFact = fact.Kind + " @" + fact.At.ToString("F3") + " " + fact.ActionId.ToString("N").Substring(0, 8);
129 |             if (fact.Reason != null) lastFact += " " + fact.Reason;
130 |             if (verboseTrace) Debug.Log("Character " + lastFact, this);
131 |         }
132 | 
133 |         public void CancelAction(string reason)
134 |         {
135 |             // 取消必须具名；抑制仍按住的输入，不能把下一帧误判为新请求。
136 |             intent.SuppressUntilRelease();
137 |             var terminal = runtime == null ? null : runtime.Cancel(reason);
138 |             if (terminal != null) Observe(terminal);
139 |         }
140 | 
141 |         public void SetPaused(bool value) { paused = value; intent.SuppressUntilRelease(); }
142 |         public void SetAttackRestriction(object owner, bool restricted, string reason = "attack-restricted")
143 |         {
144 |             if (restricted) { authority.Block(owner, ControlChannel.Attack); CancelAction(reason); }
145 |             else authority.Release(owner);
146 |         }
147 | 
148 |         private void OnApplicationFocus(bool hasFocus)
149 |         { focused = hasFocus; if (!hasFocus) CancelAction("focus-lost"); else intent.SuppressUntilRelease(); }
150 |         private void OnApplicationPause(bool value)
151 |         { applicationPaused = value; if (value) intent.SuppressUntilRelease(); }
152 |         private void OnEnable() { intent.SuppressUntilRelease(); }
153 |         private void OnDisable() { CancelAction("disabled"); if (ready && presentation != null) presentation.SetPaused(false); }
154 |         private void OnDestroy()
155 |         {
156 |             // 终止本角色动作后解除本对象引用；不清其它角色或场景控制权威。
157 |             CancelAction("destroyed");
158 |             ready = false;
159 |             DataBus = null;
160 |             runtime = null;
161 |             inputSource = null;
162 |             mover = null;
163 |             orienter = null;
164 |             presentation = null;
165 |             hitResolver = null;
166 |         }
167 |     }
168 | }
```

| 行区间 | 调用、语义和状态权限 |
|---|---|
| 1–6 | 文件级中文职责总览：描述真实功能、负责/不负责范围和协作边界；不改变运行状态。 |
| 7–18 | 引入System、领域层、Unity与序列化兼容属性；没有Development依赖。下面的执行顺序保证输入/动作主调度早于开发面板更新。 |
| 19–29 | Inspector身份和具体组件槽；movement保留旧motor字段迁移标记，其他引用由Prefab持久化。动作四个时间点和damage仍是候选数值，不是架构常量。 |
| 30–41 | Root拥有权限对象和按下边沿对象；五个接口字段只保存注册引用。runtime独占动作状态；focused/paused/applicationPaused属于应用与角色暂停条件；LastFact只是诊断，不参与判定。 |
| 42–42 | 只读暴露配置身份，供协调者在Root Awake前验证唯一性；Runtime/Bus仍使用同一actorId，不新增身份写入口。 |
| 43–46 | Root拥有权限对象和按下边沿对象；五个接口字段只保存注册引用。runtime独占动作状态；focused/paused/applicationPaused属于应用与角色暂停条件；LastFact只是诊断，不参与判定。 |
| 47–70 | Unity创建角色时调用：先解析缺省相机，逐项验证组件，再具体槽赋值到窄接口；检查输入绑定。创建不可变攻击配置和唯一runtime，非法配置报错禁用。随后创建每角色Bus，明确按movement→presentation注入；失败禁用Root，不能部分就绪继续跑。 |
| 71–76 | 同一个实际初始化循环被Root和替换实现测试调用；从左到右调用接口Initialize，任何null/false立即返回，不执行后续模块。Input/Orientation/Hit不需要无意义注入。 |
| 77–84 | Unity每帧唯一调度：接口Read发布CurrentFrame；处理暂停和具名取消；分别计算移动许可与方向。本帧输入资格不等于动作接受，下一段单独提交请求。 |
| 85–87 | 动作推进可能跨过MoveReady，故重新计算许可。Movement接口执行位移；失焦/暂停delta为0。Presentation按同一Bus读取真实结果并同步暂停。 |
| 88–95 | 先消耗真实Held的按下边沿，再检查焦点与鼠标UI阻挡；向runtime提交。替换旧恢复尾部时先记录PreviousTerminal，再记录Accepted，只有接受后才显示面向攻击方向；拒绝仅记原因。 |
| 96–114 | 推进唯一领域时钟，暂停/失焦不推进。逐个处理有序事实；Released时复制AttackDirection到局部Vector3并交给HitResolver，同时显示面向出手方向，不把Spine回调变成第二时钟。 |
| 115–125 | 屏幕指针必须先通过InputHandler有效性筛选；再投射到角色所在XZ平面。无交点、近零方向返回null以触发回退，不把屏幕像素当世界向量。 |
| 126–132 | 只把事实写到诊断字符串，verboseTrace只决定日志；动作身份/伤害不依赖日志开关。 |
| 133–140 | 供输入/生命周期/开发工具使用的通用具名取消入口。先要求松键重新武装，再取消尚存runtime；若已无动作，不伪造终结事实。 |
| 141–147 | SetPaused修改角色暂停并抑制Held复用。SetAttackRestriction按owner添加/解除禁攻，添加时取消当前动作；无法解除其他owner限制。 |
| 148–153 | 失焦具名取消；恢复焦点要求先松键。应用暂停记录独立标志。启用抑制Held、禁用取消并恢复动画速度，不重新构造动作状态。 |
| 154–168 | 销毁再做幂等取消，然后清ready/Bus/runtime/接口引用；输入动作由InputHandler自身释放，无静态注册表残留。 |

用户结论：待审。修改任何职责或语义后，重新生成受影响行号并复审。

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterSpineView.cs

SHA256：3D3D34FACDD32A0E32F301CD72CB9249069306F41104903DB9E407A5ACE5B96E

```text
001 | /*
002 |  * 脚本：CharacterSpineView
003 |  * 功能：把角色动作、实际移动和显示翻转投影到Spine。
004 |  * 边界：只写表现，不用动画回调判命中或推进动作时钟。
005 |  * 协作：Root初始化和调度；按ActionId区分同名攻击重播，暂停恢复原动画速度。
006 |  */
007 | using System;
008 | using Spine.Unity;
009 | using UnityEngine;
010 | 
011 | namespace Ossastra.Characters
012 | {
013 |     public sealed class CharacterSpineView : MonoBehaviour, ICharacterPresentation
014 |     {
015 |         [SerializeField] private SkeletonAnimation skeleton;
016 |         [SerializeField] private string idle = "idle1_1", move = "run", attack = "atk1";
017 |         private CharacterDataBus dataBus;
018 |         private string lastClip;
019 |         private Guid lastAction;
020 |         private float originalScaleX;
021 |         private float originalTimeScale;
022 |         public string CurrentClip => lastClip;
023 | 
024 |         public bool Initialize(CharacterDataBus dataBus)
025 |         {
026 |             if (dataBus == null) return false;
027 |             this.dataBus = dataBus;
028 |             if (skeleton == null) skeleton = GetComponent<SkeletonAnimation>();
029 |             if (skeleton == null || skeleton.skeletonDataAsset == null) return false;
030 |             skeleton.Initialize(false);
031 |             var data = skeleton.skeletonDataAsset.GetSkeletonData(false);
032 |             if (data == null || data.FindAnimation(idle) == null || data.FindAnimation(move) == null ||
033 |                 data.FindAnimation(attack) == null || skeleton.Skeleton == null) return false;
034 |             originalScaleX = Mathf.Abs(skeleton.Skeleton.ScaleX);
035 |             originalTimeScale = skeleton.timeScale;
036 |             return true;
037 |         }
038 | 
039 |         public void SetPaused(bool paused)
040 |         {
041 |             if (skeleton != null) skeleton.timeScale = paused ? 0 : originalTimeScale;
042 |         }
043 | 
044 |         public void Present()
045 |         {
046 |             if (dataBus == null || skeleton == null || skeleton.Skeleton == null || skeleton.AnimationState == null) return;
047 |             var actionActive = dataBus.ActionActive;
048 |             var actionId = dataBus.ActionId;
049 |             var moving = dataBus.IsMoving;
050 |             var facing = dataBus.VisualFacingX;
051 |             var clip = actionActive && !moving ? attack : moving ? move : idle;
052 |             // 同名攻击也可能属于新ActionId，需重播；同一动作逐帧Present不可反复从头播放。
053 |             var replay = clip == attack && actionId != lastAction;
054 |             skeleton.Skeleton.ScaleX = originalScaleX * facing;
055 |             if (clip != lastClip || replay)
056 |             {
057 |                 skeleton.AnimationState.SetAnimation(0, clip, clip != attack);
058 |                 lastClip = clip;
059 |             }
060 |             lastAction = actionId;
061 |         }
062 |     }
063 | }
```

| 行区间 | 调用、语义和状态权限 |
|---|---|
| 1–6 | 文件级中文职责总览：描述真实功能、负责/不负责范围和协作边界；不改变运行状态。 |
| 7–12 | Spine.Unity仅留在表现适配器，不让骨架类型进入动作核心。 |
| 13–23 | 保存序列化骨架/clip，Bus引用和上一clip/动作ID；原ScaleX绝对值和timeScale用于可逆显示。 |
| 24–38 | 接口初始化验证Bus、骨架数据与idle/run/atk1动画是否真实存在；初始化骨架后保存原比例/速度。失败返回false，不用假角色替代成功。 |
| 39–43 | 只把骨架速度置0或恢复初始化捕获值，不修改动作runtime的时钟。 |
| 44–63 | 从Bus读取真实动作ID、位移结果、VisualFacingX；攻击且不移动选atk1，否则run/idle。同clip新动作允许重播，其他情况去重；ScaleX仅改视觉符号，未变碰撞与攻击方向。 |

用户结论：待审。修改任何职责或语义后，重新生成受影响行号并复审。

## Assets/OSSASTRA/Scripts/Development/Combat/CharacterControlDebugView.cs

SHA256：190C6F55E2B4FCD93AFE4B1C62CF5B3B7D41FE9576899B430381B57B667C979E

```text
001 | /*
002 |  * 脚本：CharacterControlDebugView
003 |  * 功能：显示唯一实控来源与两角色的身份、位置、动作和许可，提供切换验证入口。
004 |  * 边界：仅开发展示，不保存第二份选中状态，不从显示内容反向决定控制权。
005 |  * 协作：读取协调者和各角色Bus；按钮调用真实SwitchNext，GUI区域登记到输入以阻挡穿透。
006 |  */
007 | using Ossastra.Characters;
008 | using UnityEngine;
009 | 
010 | namespace Ossastra.Development
011 | {
012 |     public sealed class CharacterControlDebugView : MonoBehaviour
013 |     {
014 |         [SerializeField] private CharacterControlCoordinator coordinator;
015 |         private static readonly Rect panel = new Rect(12, 294, 430, 220);
016 |         private void OnEnable()
017 |         {
018 |             if (coordinator == null) return;
019 |             foreach (var character in coordinator.Characters)
020 |             {
021 |                 var input = character == null ? null : character.GetComponent<CharacterInputHandler>();
022 |                 if (input != null) input.RegisterPointerRegion(this, panel);
023 |             }
024 |         }
025 |         private void OnDisable()
026 |         {
027 |             if (coordinator == null) return;
028 |             foreach (var character in coordinator.Characters)
029 |             {
030 |                 var input = character == null ? null : character.GetComponent<CharacterInputHandler>();
031 |                 if (input != null) input.RemovePointerRegion(this);
032 |             }
033 |         }
034 |         private void OnGUI()
035 |         {
036 |             if (coordinator == null) return;
037 |             GUILayout.BeginArea(panel, GUI.skin.box);
038 |             GUILayout.Label("双实例控制验证候选 · 临时同素材 · Tab / 手柄Select切换");
039 |             GUILayout.Label(coordinator.Status);
040 |             foreach (var character in coordinator.Characters)
041 |             {
042 |                 if (character == null) { GUILayout.Label("角色引用缺失"); continue; }
043 |                 var bus = character.DataBus;
044 |                 var input = character.GetComponent<CharacterInputHandler>();
045 |                 GUILayout.Label(character.ActorId + " | 玩家许可 " + (input != null && input.HasPlayerInput) +
046 |                     " | " + character.transform.position.ToString("F2"));
047 |                 GUILayout.Label("动作 " + (bus == null ? "未初始化" : bus.ActionId.ToString("N").Substring(0, 8)) +
048 |                     " | " + character.LastFact);
049 |             }
050 |             if (GUILayout.Button("切换到下一个可控制角色")) coordinator.SwitchNext();
051 |             GUILayout.EndArea();
052 |         }
053 |     }
054 | }
```

| 行区间 | 调用、语义和状态权限 |
|---|---|
| 1–6 | 文件级中文职责总览：描述真实功能、负责/不负责范围和协作边界；不改变运行状态。 |
| 7–11 | 仅Development依赖生产接口/组件。Root不反向引用本类。 |
| 12–15 | 序列化coordinator是只读数据来源；panel是开发GUI布局，不保存当前选中角色。 |
| 16–24 | 对所有可用成员输入登记本GUI阻挡区域，判空处理缺引用；避免面板点击穿透到攻击。 |
| 25–33 | 仅移除自身owner的区域；不清其它UI注册或改变玩家实控选择。 |
| 34–54 | 从coordinator与各角色Bus读取状态：ActorId、许可、位置、动作ID、最近阶段事实。缺引用显示明确诊断。按钮调用同一个SwitchNext入口，不改GUI文本冒充切人成功。 |

用户结论：待审。修改任何职责或语义后，重新生成受影响行号并复审。

## Assets/OSSASTRA/Scripts/Development/Combat/CharacterDebugControls.cs

SHA256：879B3E09580329EF6B4D4612935DD070AB9CD4FB86040E5D4E828026EF91FC2A

```text
001 | /*
002 |  * 脚本：CharacterDebugControls
003 |  * 功能：为单个角色提供合法／非法／清理／检查的开发入口。
004 |  * 边界：开发命令调用真实Root接口；不是正式UI，Root不引用本组件。
005 |  * 协作：读取该角色Bus快照；注册GUI阻挡区；禁用只解除自己的禁攻owner。
006 |  */
007 | using Ossastra.Characters;
008 | using UnityEngine;
009 | 
010 | namespace Ossastra.Development
011 | {
012 |     [DefaultExecutionOrder(100)]
013 |     public sealed class CharacterDebugControls : MonoBehaviour
014 |     {
015 |         [SerializeField] private Ossastra.Characters.CharacterRoot character;
016 |         [SerializeField] private CombatTestTarget target;
017 |         private CharacterInputHandler input;
018 |         private bool blocked;
019 |         private static readonly Rect panel = new Rect(12, 12, 400, 270);
020 | 
021 |         private void OnEnable()
022 |         {
023 |             if (character == null) character = GetComponent<Ossastra.Characters.CharacterRoot>();
024 |             input = GetComponent<CharacterInputHandler>();
025 |             if (input != null) input.RegisterPointerRegion(this, panel);
026 |         }
027 |         private void OnDisable()
028 |         {
029 |             if (input != null) input.RemovePointerRegion(this);
030 |             if (character != null) character.SetAttackRestriction(this, false);
031 |             blocked = false;
032 |         }
033 |         private void Update()
034 |         {
035 |             if (character == null || character.DataBus == null) return;
036 |             var command = character.DataBus.Input.debugCommand;
037 |             if (command != 0) DebugCommand(command);
038 |         }
039 |         public void DebugCommand(int command)
040 |         {
041 |             if (character == null || character.DataBus == null) return;
042 |             if (command == 1 && target != null)
043 |             {
044 |                 character.CancelAction("debug-near-target");
045 |                 target.RestoreHealth();
046 |                 var movement = GetComponent<CharacterMovement>();
047 |                 var orientation = GetComponent<CharacterOrientation>();
048 |                 if (movement != null) movement.Teleport(target.transform.position + Vector3.left * 1.15f + Vector3.up * .03f);
049 |                 if (orientation != null) { orientation.OrientTowards(target.transform.position); orientation.FaceAttackDirection(); }
050 |             }
051 |             if (command == 2)
052 |             {
053 |                 blocked = !blocked;
054 |                 character.SetAttackRestriction(this, blocked, "debug-attack-block");
055 |             }
056 |             if (command == 3)
057 |             {
058 |                 character.CancelAction("debug-clear");
059 |                 character.SetAttackRestriction(this, false);
060 |                 character.SetPaused(false);
061 |                 blocked = false;
062 |                 if (target != null) target.RestoreHealth();
063 |             }
064 |             if (command == 4) Debug.Log("Character " + character.DataBus.ActorId + " " + character.LastFact, character);
065 |         }
066 |         private void OnGUI()
067 |         {
068 |             if (character == null) return;
069 |             if (input != null && !input.HasPlayerInput) return;
070 |             GUILayout.BeginArea(panel, GUI.skin.box);
071 |             GUILayout.Label("角色候选 · WASD移动 / 左键普攻 / X取消 / P暂停");
072 |             GUILayout.Label(character.LastFact);
073 |             GUILayout.Label("暂停: " + character.IsPaused + " | 禁攻: " + blocked);
074 |             if (GUILayout.Button("F1 合法状态：恢复靶并站到攻击距离")) DebugCommand(1);
075 |             if (GUILayout.Button("F2 非法状态：切换owner禁攻")) DebugCommand(2);
076 |             if (GUILayout.Button("F3 清理：取消／解除禁攻／恢复靶")) DebugCommand(3);
077 |             if (GUILayout.Button("F4 检查：输出动作状态")) DebugCommand(4);
078 |             GUILayout.EndArea();
079 |         }
080 |     }
081 | }
```

| 行区间 | 调用、语义和状态权限 |
|---|---|
| 1–6 | 文件级中文职责总览：描述真实功能、负责/不负责范围和协作边界；不改变运行状态。 |
| 7–11 | Development依赖正式组件与Unity GUI；生产Root不会反向引用本类。 |
| 12–20 | 在Root本帧采样之后消费开发命令；保存当前角色与场景开发靶的Inspector引用、自身UI区域和owner禁攻状态。 |
| 21–26 | 同角色绑定输入组件并登记此owner的GUI区域，避免点击按钮同时普攻；不扫描全场服务。 |
| 27–32 | 只移除自身UI区域与自身owner禁攻，清本地开发标志，不解除其他控制者。 |
| 33–38 | 角色/Bus尚未就绪不处理；读取本帧debugCommand并调用明确开发入口。 |
| 39–65 | F1取消、恢复靶、通过运动组件传送、显式设攻击方向和显示；F2通过Root添加/解除自身禁攻；F3取消/清自身禁攻/解除暂停/恢复靶；F4只输出真实状态。无直接伪造成功UI。 |
| 66–68 | 开发面板只显示真实LastFact/暂停/禁攻，并把按钮映射到同一DebugCommand；移出Root后可独立禁用，不影响生产依赖。 |
| 69–69 | 开发面板只显示当前可消费玩家输入的角色，避免两份GUI重叠；显式测试命令仍是开发入口，不属于角色生产逻辑。 |
| 70–81 | 开发面板只显示真实LastFact/暂停/禁攻，并把按钮映射到同一DebugCommand；移出Root后可独立禁用，不影响生产依赖。 |

用户结论：待审。修改任何职责或语义后，重新生成受影响行号并复审。

## Assets/OSSASTRA/Scripts/Development/Combat/CombatTestTarget.cs

SHA256：B47E1A1F1C48F1D7E784E1D6D2B042AE253CAC482353EE7CD3C25EC471EF5831

```text
001 | /*
002 |  * 脚本：CombatTestTarget
003 |  * 功能：提供开发验证靶、独立Health和短暂命中闪白／数值显示。
004 |  * 边界：仅验证辅助，不代表正式敌人、受击或死亡系统已经迁移。
005 |  * 协作：通过ICharacterHitTarget被命中模块发现；Health保持唯一写入。
006 |  */
007 | using Ossastra.Characters;
008 | using Ossastra.CharacterCore;
009 | using UnityEngine;
010 | 
011 | namespace Ossastra.Development
012 | {
013 |     public sealed class CombatTestTarget : MonoBehaviour, ICharacterHitTarget
014 |     {
015 |         [SerializeField] private string targetId = "target-a";
016 |         [SerializeField, Min(1)] private int maximumHp = 100;
017 |         [SerializeField, Min(.01f)] private float flashSeconds = .15f;
018 |         private Renderer[] renderers;
019 |         private MaterialPropertyBlock block;
020 |         private float flashUntil;
021 |         private bool flashing;
022 |         public Health Health { get; private set; }
023 | 
024 |         private void Awake()
025 |         {
026 |             Health = new Health(targetId, maximumHp);
027 |             renderers = GetComponentsInChildren<Renderer>();
028 |             block = new MaterialPropertyBlock();
029 |         }
030 | 
031 |         public void ShowHit() { flashing = true; flashUntil = Time.time + flashSeconds; Paint(Color.white); }
032 |         public void RestoreHealth()
033 |         {
034 |             Health = new Health(targetId, maximumHp);
035 |             flashing = false;
036 |             Paint(new Color(.15f, .7f, .7f));
037 |         }
038 | 
039 |         private void Update()
040 |         {
041 |             if (!flashing || Time.time < flashUntil) return;
042 |             flashing = false;
043 |             Paint(Health.Current == 0 ? Color.gray : new Color(.15f, .7f, .7f));
044 |         }
045 | 
046 |         private void Paint(Color color)
047 |         {
048 |             if (block == null) return;
049 |             block.SetColor("_BaseColor", color);
050 |             block.SetColor("_Color", color);
051 |             foreach (var r in renderers) if (r != null) r.SetPropertyBlock(block);
052 |         }
053 | 
054 |         private void OnGUI()
055 |         {
056 |             var camera = Camera.main;
057 |             if (camera == null || Health == null) return;
058 |             var point = camera.WorldToScreenPoint(transform.position + Vector3.up * 1.8f);
059 |             if (point.z <= 0) return;
060 |             GUI.Box(new Rect(point.x - 70, Screen.height - point.y, 140, 26),
061 |                 targetId + "  HP " + Health.Current + "/" + Health.Maximum);
062 |         }
063 |     }
064 | }
```

| 行区间 | 调用、语义和状态权限 |
|---|---|
| 1–6 | 文件级中文职责总览：描述真实功能、负责/不负责范围和协作边界；不改变运行状态。 |
| 7–12 | 开发层依赖正式目标接口与原Health，依赖方向由开发→生产。 |
| 13–23 | 目标ID/最大HP/闪白时长是开发配置；Renderer数组、MPB和闪白时刻只掌管表现。Health属性引用唯一结算对象。 |
| 24–30 | Play初始化该开发靶的Health和Renderers/MPB；没有另一个真实HP字段。 |
| 31–31 | Applied命中后开启闪白并设到期时间；不再次扣血。 |
| 32–38 | 明确开发重置入口重新创建目标Health、清闪白并恢复颜色；不冒充正式治疗/复活机制。 |
| 39–45 | 闪白未到期直接返回，到期只按Health.Current选死灰/活青颜色。 |
| 46–53 | MPB写颜色而非克隆/修改共享材质，逐个忽略已销毁Renderer。 |
| 54–64 | 开发HP标签取真实Health与Camera.main屏幕投影；缺相机/Health或在相机后方则不画。不是正式战斗UI。 |

用户结论：待审。修改任何职责或语义后，重新生成受影响行号并复审。

