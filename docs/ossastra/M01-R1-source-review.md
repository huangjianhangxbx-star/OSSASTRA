> 历史快照：v0.5行为验证时的源码行号与SHA。v0.6 P0补中文注释后行号改变；后续最新源码以M02A审查包为准。本快照保留溯源，不能套用当前行号。

# M01-R1 全量当前源码与逐块行号审查
状态：全部待审。行号对应当前原文件；结构括号与空行完整列出，紧密语句块按实际控制流解释。接口/属性/字段含义不以旧文档代替。精确SHA见清单，源码原件另随审查ZIP提供。

## Assets/OSSASTRA/Scripts/Characters/Core/AttackIntent.cs
SHA256：DD1F501F5756FBED7AFBDF5A7898F9B35422C5878C38C95631AF015EBDF2C78E

```text
001 | namespace Ossastra.CharacterCore
002 | {
003 |     // 设备采样由未来Unity适配器负责；这里只保留按下边沿和重新武装规则。
004 |     public sealed class AttackIntent
005 |     {
006 |         private bool previousHeld;
007 |         private bool armed = true;
008 | 
009 |         public bool Observe(bool held)
010 |         {
011 |             if (!armed)
012 |             {
013 |                 if (!held) armed = true;
014 |                 previousHeld = held;
015 |                 return false;
016 |             }
017 |             var pressed = held && !previousHeld;
018 |             previousHeld = held;
019 |             return pressed;
020 |         }
021 | 
022 |         // 失焦/禁用后，仍按住的键不能被误当作一次新按下。
023 |         public void SuppressUntilRelease()
024 |         {
025 |             previousHeld = false;
026 |             armed = false;
027 |         }
028 |     }
029 | }
```

| 行区间 | 具体行为、调用与读写权威 |
|---|---|
| 1–5 | 纯领域边沿对象，既不读设备也不播放动画。 |
| 6–8 | previousHeld保存上次Held，armed表示是否已看到重新松键；每个角色独立。 |
| 9–22 | 未武装时只允许松键恢复，不发命令；已武装时仅false→true返回按下。持续按住不重复，调用者仍须另外检查动作资格。 |
| 23–29 | 生命周期/暂停/取消调用；清previousHeld并取消武装，避免恢复窗口时仍按住被误认新按下。 |
用户结论：待审。修改后受影响行区间必须重审。

## Assets/OSSASTRA/Scripts/Characters/Core/AttackRelease.cs
SHA256：3C96F30D6F5D9B5B2B728308D95F681D9DC9A0AB7DEF09B0A315E5262F2FCBAB

```text
001 | using System;
002 | using System.Collections.Generic;
003 | using System.Linq;
004 | 
005 | namespace Ossastra.CharacterCore
006 | {
007 |     public enum HitReason { Applied, Duplicate, Dead, Self }
008 | 
009 |     public sealed class HitOutcome
010 |     {
011 |         public HitReason Reason { get; }
012 |         public int HpBefore { get; }
013 |         public int HpAfter { get; }
014 |         public Guid ActionId { get; }
015 |         public string ActorId { get; }
016 |         public string TargetId { get; }
017 | 
018 |         internal HitOutcome(HitReason reason, int before, Health target, Guid actionId, string actorId)
019 |         {
020 |             Reason = reason;
021 |             HpBefore = before;
022 |             HpAfter = target.Current;
023 |             TargetId = target.Id;
024 |             ActionId = actionId;
025 |             ActorId = actorId;
026 |         }
027 |     }
028 | 
029 |     // 每次释放只能提交一个瞬时近战候选批次；空挥也关闭，避免永久开启命中窗口。
030 |     public sealed class AttackRelease
031 |     {
032 |         private readonly Guid actionId;
033 |         private readonly string actorId;
034 |         private readonly int damage;
035 |         private bool resolved;
036 | 
037 |         internal AttackRelease(Guid actionId, string actorId, int damage)
038 |         {
039 |             this.actionId = actionId;
040 |             this.actorId = actorId;
041 |             this.damage = damage;
042 |         }
043 | 
044 |         // 调用方必须先完成实际空间/阵营过滤；本方法不谎称已做物理检测。
045 |         public IReadOnlyList<HitOutcome> Resolve(IEnumerable<Health> targets)
046 |         {
047 |             if (resolved) throw new InvalidOperationException("Attack release already resolved");
048 |             if (targets == null) throw new ArgumentNullException(nameof(targets));
049 |             var batch = targets.ToArray();
050 |             if (batch.Any(t => t == null)) throw new ArgumentException("Null candidate target", nameof(targets));
051 |             resolved = true;
052 |             var seen = new HashSet<string>(StringComparer.Ordinal);
053 |             var outcomes = new List<HitOutcome>();
054 |             foreach (var target in batch)
055 |             {
056 |                 var before = target.Current;
057 |                 HitReason reason;
058 |                 if (!seen.Add(target.Id)) reason = HitReason.Duplicate;
059 |                 else if (target.Id == actorId) reason = HitReason.Self;
060 |                 else if (target.Current == 0) reason = HitReason.Dead;
061 |                 else
062 |                 {
063 |                     target.ApplyDamage(damage);
064 |                     reason = HitReason.Applied;
065 |                 }
066 |                 outcomes.Add(new HitOutcome(reason, before, target, actionId, actorId));
067 |             }
068 |             return outcomes;
069 |         }
070 |     }
071 | }
```

| 行区间 | 具体行为、调用与读写权威 |
|---|---|
| 1–8 | 定义命中结果类别和领域结算凭证，不包含Unity空间查找。 |
| 9–29 | 只读结果记录目标、动作/攻击者身份、HP前后与原因；用于反馈和验收，不作为第二份可写HP。 |
| 30–44 | 构造仅限领域程序集，保存动作ID/攻击者/伤害和是否已结算；只有真实Released可取得该凭证。 |
| 45–71 | 拒绝重复Resolve/null批次，先整体校验无null再写HP。空挥也关闭该凭证；按目标身份去重，排除自己和死亡对象，仅合法目标调用唯一Health.ApplyDamage，返回每个候选的真实结果。 |
用户结论：待审。修改后受影响行区间必须重审。

## Assets/OSSASTRA/Scripts/Characters/Core/BasicActionRuntime.cs
SHA256：4DC090007A7CF622A62A67D72B8E8912C63003F191743D2EBA2EC67F9DF3C933

```text
001 | using System;
002 | using System.Collections.Generic;
003 | using System.Linq;
004 | 
005 | namespace Ossastra.CharacterCore
006 | {
007 |     public enum ActionFactKind { Accepted, Released, MoveReady, AttackReady, Finished, Cancelled }
008 | 
009 |     // 事实是结果，不是任意广播；调用者按返回顺序处理。
010 |     public sealed class ActionFact
011 |     {
012 |         public ActionFactKind Kind { get; }
013 |         public Guid ActionId { get; }
014 |         public string Source { get; }
015 |         public string Reason { get; }
016 |         public double At { get; }
017 |         public AttackRelease Release { get; }
018 | 
019 |         internal ActionFact(ActionFactKind kind, Guid id, string source, double at,
020 |             string reason = null, AttackRelease release = null)
021 |         {
022 |             Kind = kind;
023 |             ActionId = id;
024 |             Source = source;
025 |             At = at;
026 |             Reason = reason;
027 |             Release = release;
028 |         }
029 |     }
030 | 
031 |     public sealed class ActionRequestResult
032 |     {
033 |         public bool Accepted => Fact != null;
034 |         public string Reason { get; }
035 |         public ActionFact Fact { get; }
036 |         public ActionFact PreviousTerminal { get; }
037 | 
038 |         internal ActionRequestResult(string reason, ActionFact fact = null, ActionFact previousTerminal = null)
039 |         {
040 |             Reason = reason;
041 |             Fact = fact;
042 |             PreviousTerminal = previousTerminal;
043 |         }
044 |     }
045 | 
046 |     // 单角色单动作权威，不读设备/Spine/世界碰撞，也不直接写HP。
047 |     public sealed class BasicActionRuntime
048 |     {
049 |         private readonly string actorId;
050 |         private BasicAttackDefinition definition;
051 |         private (double At, ActionFactKind Kind)[] nodes;
052 |         private int nextNode;
053 |         private double elapsed;
054 |         private double timeCorrection;
055 |         private Guid actionId;
056 |         private string source;
057 |         private bool moveReady;
058 |         private bool attackReady;
059 | 
060 |         public bool IsActive => definition != null;
061 |         public bool MoveReady => !IsActive || moveReady;
062 |         public bool AttackReady => !IsActive || attackReady;
063 |         public Guid ActionId => IsActive ? actionId : Guid.Empty;
064 | 
065 |         public BasicActionRuntime(string actorId)
066 |         {
067 |             if (string.IsNullOrWhiteSpace(actorId)) throw new ArgumentException("Actor identity required", nameof(actorId));
068 |             this.actorId = actorId;
069 |         }
070 | 
071 |         public ActionRequestResult Request(BasicAttackDefinition attack, string requestSource,
072 |             ControlAuthority gate, bool paused = false)
073 |         {
074 |             if (attack == null) throw new ArgumentNullException(nameof(attack));
075 |             if (gate == null) throw new ArgumentNullException(nameof(gate));
076 |             if (string.IsNullOrWhiteSpace(requestSource)) throw new ArgumentException("Request source required", nameof(requestSource));
077 |             if (paused) return new ActionRequestResult("paused");
078 |             if (!gate.Allows(ControlChannel.Attack)) return new ActionRequestResult("attack-blocked");
079 |             if (IsActive && !attackReady) return new ActionRequestResult("busy");
080 |             // Ready允许一次新按下；显式终结旧身体动作，不遗失其已释放效果。
081 |             var previousTerminal = IsActive ? Cancel("replaced-by-next-attack") : null;
082 |             definition = attack;
083 |             source = requestSource;
084 |             actionId = Guid.NewGuid();
085 |             elapsed = 0;
086 |             timeCorrection = 0;
087 |             nextNode = 0;
088 |             moveReady = false;
089 |             attackReady = false;
090 |             // 两种Ready可互换先后；同一时刻固定Release→Move→Attack→Finish。
091 |             nodes = new[] {
092 |                 (attack.ReleaseAt, ActionFactKind.Released),
093 |                 (attack.MoveReadyAt, ActionFactKind.MoveReady),
094 |                 (attack.AttackReadyAt, ActionFactKind.AttackReady),
095 |                 (attack.FinishAt, ActionFactKind.Finished)
096 |             }.OrderBy(n => n.Item1).ThenBy(n => n.Item2).ToArray();
097 |             return new ActionRequestResult(null, Fact(ActionFactKind.Accepted, 0), previousTerminal);
098 |         }
099 | 
100 |         public IReadOnlyList<ActionFact> Advance(double delta, bool paused)
101 |         {
102 |             if (!BasicAttackDefinition.Finite(delta) || delta < 0)
103 |                 throw new ArgumentOutOfRangeException(nameof(delta));
104 |             if (!IsActive || paused) return Array.Empty<ActionFact>();
105 |             // 补偿累计舍入误差；不靠提前Release的任意epsilon修补帧边界。
106 |             var correctedDelta = delta - timeCorrection;
107 |             var sum = elapsed + correctedDelta;
108 |             timeCorrection = (sum - elapsed) - correctedDelta;
109 |             elapsed = Math.Min(definition.FinishAt, sum);
110 |             if (elapsed == definition.FinishAt) timeCorrection = 0;
111 |             var facts = new List<ActionFact>();
112 |             while (nextNode < nodes.Length && elapsed >= nodes[nextNode].At)
113 |             {
114 |                 var node = nodes[nextNode++];
115 |                 AttackRelease release = null;
116 |                 if (node.Kind == ActionFactKind.Released)
117 |                     release = new AttackRelease(actionId, actorId, definition.Damage);
118 |                 if (node.Kind == ActionFactKind.MoveReady) moveReady = true;
119 |                 if (node.Kind == ActionFactKind.AttackReady) attackReady = true;
120 |                 facts.Add(Fact(node.Kind, node.At, release: release));
121 |                 if (node.Kind == ActionFactKind.Finished) definition = null;
122 |             }
123 |             return facts;
124 |         }
125 | 
126 |         public ActionFact Cancel(string reason)
127 |         {
128 |             if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Cancellation cause required", nameof(reason));
129 |             if (!IsActive) return null;
130 |             var terminal = Fact(ActionFactKind.Cancelled, elapsed, reason);
131 |             definition = null;
132 |             return terminal;
133 |         }
134 | 
135 |         private ActionFact Fact(ActionFactKind kind, double at, string reason = null, AttackRelease release = null)
136 |             => new ActionFact(kind, actionId, source, at, reason, release);
137 |     }
138 | }
```

| 行区间 | 具体行为、调用与读写权威 |
|---|---|
| 1–9 | 纯动作领域定义事实类型，不依赖Unity/Spine或物理。 |
| 10–30 | Accepted/Released/Ready/Finished/Cancelled事实对象只读；携动作ID、来源、原因、局部时间及合法Release凭证，构造不调度GUI。 |
| 31–46 | Accepted由Fact是否存在决定；Reason说明拒绝；PreviousTerminal显式返回被新攻击替换的旧恢复尾部取消，调用者不能丢掉它。 |
| 47–64 | 每角色唯一动作状态：配置、节点数组、下一个节点、elapsed及舍入补偿、身份/来源/两个Ready；对外只读查询。 |
| 65–70 | 拒绝空身份，保存本角色身份供真实Accepted和Release使用。 |
| 71–90 | 校验配置/来源/权限对象；paused、禁攻、未AttackReady分别拒绝且不创建ID。恢复尾部允许新按下时显式Cancel旧动作，再为真正接受创建新GUID并清时钟/节点/Ready。 |
| 91–99 | 按时间与事实种类稳定排序四个节点，保证同刻Release→MoveReady→AttackReady→Finish；返回Accepted，不等于已出手。 |
| 100–111 | 拒绝非法delta；无动作/暂停不推进。使用补偿求和减小帧分割误差，最多推进到Finish，不靠提前释放epsilon作弊。 |
| 112–125 | 一次跨多节点也顺序逐个处理。只有Released构造一次AttackRelease；对应Ready只写自身标志，Finished清配置。每个事实带节点时间而不是本帧时间。 |
| 126–134 | 具名原因必须非空；无动作返回null；有动作返回唯一Cancelled并清当前配置。已经返回的Release效果不撤销。 |
| 135–138 | 统一把当前动作身份/来源和参数组成只读事实，不依赖诊断开关。 |
用户结论：待审。修改后受影响行区间必须重审。

## Assets/OSSASTRA/Scripts/Characters/Core/BasicAttackDefinition.cs
SHA256：CE126E0F1A4A6E9CC65D32E04B2FB41CDB7E9305F97669A9D01D2BDB587CD5D1

```text
001 | using System;
002 | 
003 | namespace Ossastra.CharacterCore
004 | {
005 |     // 全部配置不可变；本类没有游戏平衡默认值。
006 |     public sealed class BasicAttackDefinition
007 |     {
008 |         public double ReleaseAt { get; }
009 |         public double MoveReadyAt { get; }
010 |         public double AttackReadyAt { get; }
011 |         public double FinishAt { get; }
012 |         public int Damage { get; }
013 | 
014 |         public BasicAttackDefinition(double release, double move, double ready, double finish, int damage)
015 |         {
016 |             if (!Finite(release) || !Finite(move) || !Finite(ready) || !Finite(finish) ||
017 |                 release < 0 || move < release || ready < release || finish < move || finish < ready)
018 |                 throw new ArgumentOutOfRangeException(nameof(release), "Invalid action node ordering or time");
019 |             if (damage <= 0) throw new ArgumentOutOfRangeException(nameof(damage));
020 |             ReleaseAt = release;
021 |             MoveReadyAt = move;
022 |             AttackReadyAt = ready;
023 |             FinishAt = finish;
024 |             Damage = damage;
025 |         }
026 | 
027 |         internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
028 |     }
029 | }
```

| 行区间 | 具体行为、调用与读写权威 |
|---|---|
| 1–7 | 不可变动作配置，默认数值由Root Inspector提供而非这里隐藏。 |
| 8–13 | 只读释放/移动恢复/攻击恢复/结束时间和伤害；创建后不能由表现模块篡改。 |
| 14–26 | 拒绝NaN/Infinity、负时间、释放后Ready顺序错误、结束早于Ready和非正伤害；成功后逐项赋只读配置。 |
| 27–29 | 集中定义有限实数校验供构造和Advance复用。 |
用户结论：待审。修改后受影响行区间必须重审。

## Assets/OSSASTRA/Scripts/Characters/Core/ControlAuthority.cs
SHA256：15B1692C37C998AB27D049AAC324B02FC50BE1EDD1E2104A49314D0E4AFBB6CA

```text
001 | using System;
002 | using System.Collections.Generic;
003 | using System.Runtime.CompilerServices;
004 | 
005 | namespace Ossastra.CharacterCore
006 | {
007 |     [Flags]
008 |     public enum ControlChannel { None = 0, Move = 1, Attack = 2 }
009 | 
010 |     // 每个调用者只拥有自己的禁令；没有一个调用者可以清空别人的限制。
011 |     public sealed class ControlAuthority
012 |     {
013 |         private readonly Dictionary<object, ControlChannel> blocks =
014 |             new Dictionary<object, ControlChannel>(new OwnerComparer());
015 | 
016 |         public void Block(object owner, ControlChannel channels)
017 |         {
018 |             if (owner == null) throw new ArgumentNullException(nameof(owner));
019 |             Validate(channels);
020 |             blocks[owner] = channels;
021 |         }
022 | 
023 |         public void Release(object owner)
024 |         {
025 |             if (owner != null) blocks.Remove(owner);
026 |         }
027 | 
028 |         public bool Allows(ControlChannel channels)
029 |         {
030 |             Validate(channels);
031 |             foreach (var block in blocks.Values)
032 |                 if ((block & channels) != 0) return false;
033 |             return true;
034 |         }
035 | 
036 |         private static void Validate(ControlChannel channels)
037 |         {
038 |             if ((channels & ~(ControlChannel.Move | ControlChannel.Attack)) != 0)
039 |                 throw new ArgumentOutOfRangeException(nameof(channels));
040 |         }
041 | 
042 |         // owner按对象身份区分，避免两个值相同的owner互相覆盖。
043 |         private sealed class OwnerComparer : IEqualityComparer<object>
044 |         {
045 |             public new bool Equals(object x, object y) => ReferenceEquals(x, y);
046 |             public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
047 |         }
048 |     }
049 | }
```

| 行区间 | 具体行为、调用与读写权威 |
|---|---|
| 1–12 | 权限通道为Move/Attack独立位标记；不混成全局canAct。 |
| 13–15 | 按owner对象身份保存限制；比较器避免两个值相等但不同owner互相覆盖。 |
| 16–22 | 检查owner和通道合法性，只设置这个owner的限制，不写其他owner。 |
| 23–27 | 只移除此owner；null无动作，不能清空所有限制。 |
| 28–35 | 验证查询通道，任何owner覆盖相应通道就拒绝；无覆盖才允许。 |
| 36–42 | 只接受定义过的Move/Attack位，未知位抛出参数异常。 |
| 43–49 | ReferenceEquals与RuntimeHelpers.GetHashCode成对按身份比较，保证字典语义一致。 |
用户结论：待审。修改后受影响行区间必须重审。

## Assets/OSSASTRA/Scripts/Characters/Core/Health.cs
SHA256：4EFA12B068ED93D0156A0F7B3D2BF08D80DDE3067DED471501B29088FC8DD707

```text
001 | using System;
002 | 
003 | namespace Ossastra.CharacterCore
004 | {
005 |     // HP只有本对象能够写；测试目标将来引用同一个Health，不能维护另一份真实HP。
006 |     public sealed class Health
007 |     {
008 |         public string Id { get; }
009 |         public int Maximum { get; }
010 |         public int Current { get; private set; }
011 | 
012 |         public Health(string id, int maximum)
013 |         {
014 |             if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Target identity required", nameof(id));
015 |             if (maximum <= 0) throw new ArgumentOutOfRangeException(nameof(maximum));
016 |             Id = id;
017 |             Maximum = maximum;
018 |             Current = maximum;
019 |         }
020 | 
021 |         internal void ApplyDamage(int damage)
022 |         {
023 |             if (damage <= 0) throw new ArgumentOutOfRangeException(nameof(damage));
024 |             Current = Math.Max(0, Current - damage);
025 |         }
026 |     }
027 | }
```

| 行区间 | 具体行为、调用与读写权威 |
|---|---|
| 1–5 | 独立HP权威，不依赖Unity组件或DataBus副本。 |
| 6–11 | 身份、上限、当前HP只读；Current唯一私有写入口，不向外暴露setter。 |
| 12–20 | 拒绝空身份和非正上限，初始化满HP；开发靶创建它不代表完整正式角色生命系统已迁完。 |
| 21–27 | 仅领域程序集结算调用；拒绝非正伤害，扣减并钳制0，不播放表现或广播事件。 |
用户结论：待审。修改后受影响行区间必须重审。

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterContracts.cs
SHA256：8D394CB92AEAFA7AF88E931657D92221DEFC9419653E081F169FA79AE129647D

```text
001 | using Ossastra.CharacterCore;
002 | using UnityEngine;
003 | 
004 | namespace Ossastra.Characters
005 | {
006 |     public interface ICharacterSubsystem { bool Initialize(CharacterDataBus dataBus); }
007 |     public interface ICharacterInputSource
008 |     {
009 |         CharacterInputFrame CurrentFrame { get; }
010 |         bool IsConfigured { get; }
011 |         CharacterInputFrame Read();
012 |     }
013 |     public interface ICharacterMovement : ICharacterSubsystem
014 |     {
015 |         bool IsMoving { get; }
016 |         Vector3 Velocity { get; }
017 |         void Tick(bool allowed, float delta);
018 |         void Teleport(Vector3 position);
019 |     }
020 |     public interface ICharacterOrientation
021 |     {
022 |         Vector3 AttackDirection { get; }
023 |         float VisualFacingX { get; }
024 |         void ResolveDirections(Vector2 moveInput, Vector3? pointerDirection, Vector2 stickDirection, bool movementFacing);
025 |         void OrientTowards(Vector3 worldPosition);
026 |         void FaceAttackDirection();
027 |     }
028 |     public interface ICharacterPresentation : ICharacterSubsystem
029 |     {
030 |         string CurrentClip { get; }
031 |         void SetPaused(bool paused);
032 |         void Present();
033 |     }
034 |     public interface ICharacterHitResolver
035 |     {
036 |         string LastResult { get; }
037 |         void Resolve(AttackRelease release, Vector3 releaseDirection);
038 |     }
039 |     // World target adapter: retains the existing sole Health writer.
040 |     public interface ICharacterHitTarget
041 |     {
042 |         Health Health { get; }
043 |         void ShowHit();
044 |     }
045 | }
```

| 行区间 | 具体行为、调用与读写权威 |
|---|---|
| 1–5 | 接口共享只使用领域释放/Health和Unity向量，不引用具体组件或旧框架。 |
| 6–6 | 仅真实需要DataBus注入的模块实现bool Initialize；false让Root停止后续初始化。 |
| 7–12 | 读取配置状态/当前帧与Read采样；没有无意义Initialize。实际实现InputHandler与替换测试InputStub都可由Root/Bus消费。 |
| 13–19 | 包含初始化、许可位移、显式传送和只读运动结果；不把朝向职责塞回运动。 |
| 20–27 | 分别公开攻击/显示状态和有语义的解析/目标/攻击显示操作；消费者不会从同一Facing值猜所有含义。 |
| 28–33 | 表现模块需要Bus，接口只给clip诊断、暂停和按真实状态显示，不能释放动作或扣血。 |
| 34–39 | 一次Release加方向值完成世界适配；不要求无用途Bus初始化。参数完整所以可模拟替换，仍由AttackRelease/Health掌握结算。 |
| 40–45 | 世界目标提供原Health和受击反馈；开发靶实现接口，正式解析器不依赖开发类，不另建敌人状态系统。 |
用户结论：待审。修改后受影响行区间必须重审。

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterDataBus.cs
SHA256：2EB20A5BBBBC8BFF846842B95980C59DA30ECB0BB98F8FA5DD0088A9838FB594

```text
001 | using System;
002 | using Ossastra.CharacterCore;
003 | using UnityEngine;
004 | 
005 | namespace Ossastra.Characters
006 | {
007 |     // Per-character read facade; referenced modules remain the sole mutable-state owners.
008 |     public sealed class CharacterDataBus
009 |     {
010 |         private readonly ICharacterInputSource input;
011 |         private readonly ICharacterMovement movement;
012 |         private readonly ICharacterOrientation orientation;
013 |         private readonly BasicActionRuntime actionRuntime;
014 |         private readonly ControlAuthority controlAuthority;
015 | 
016 |         public string ActorId { get; }
017 |         public CharacterInputFrame Input => input.CurrentFrame;
018 |         public bool IsMoving => movement.IsMoving;
019 |         public Vector3 Velocity => movement.Velocity;
020 |         public Vector3 AttackDirection => orientation.AttackDirection;
021 |         public float VisualFacingX => orientation.VisualFacingX;
022 |         public bool ActionActive => actionRuntime.IsActive;
023 |         public Guid ActionId => actionRuntime.ActionId;
024 |         public bool MoveReady => actionRuntime.MoveReady;
025 |         public bool AttackReady => actionRuntime.AttackReady;
026 |         public bool AllowsMovement => controlAuthority.Allows(ControlChannel.Move);
027 |         public bool AllowsAttack => controlAuthority.Allows(ControlChannel.Attack);
028 | 
029 |         public CharacterDataBus(string actorId, ICharacterInputSource input, ICharacterMovement movement,
030 |             ICharacterOrientation orientation, BasicActionRuntime actionRuntime, ControlAuthority controlAuthority)
031 |         {
032 |             if (string.IsNullOrWhiteSpace(actorId)) throw new ArgumentException("Actor identity required", nameof(actorId));
033 |             if (input == null) throw new ArgumentNullException(nameof(input));
034 |             if (movement == null) throw new ArgumentNullException(nameof(movement));
035 |             if (orientation == null) throw new ArgumentNullException(nameof(orientation));
036 |             if (actionRuntime == null) throw new ArgumentNullException(nameof(actionRuntime));
037 |             if (controlAuthority == null) throw new ArgumentNullException(nameof(controlAuthority));
038 |             ActorId = actorId;
039 |             this.input = input;
040 |             this.movement = movement;
041 |             this.orientation = orientation;
042 |             this.actionRuntime = actionRuntime;
043 |             this.controlAuthority = controlAuthority;
044 |         }
045 |     }
046 | }
```

| 行区间 | 具体行为、调用与读写权威 |
|---|---|
| 1–7 | Bus读Unity向量和领域runtime/权限，但不负责设备、Spine、物理查询或事件广播。 |
| 8–15 | 私有readonly接口引用指向原写入者；runtime/controlAuthority也是原对象引用，不复制状态。 |
| 16–28 | 身份初始化后只读；Input返回值类型副本；移动、显示和攻击方向分别读取对应接口。动作身份/Ready与权限均实时查询原所有者，无public setter。 |
| 29–46 | Root明确注入五类所有者与角色身份；检查空身份和每个必需依赖后保存引用。禁止全局服务定位，也没有给消费者任意写Bus的入口。 |
用户结论：待审。修改后受影响行区间必须重审。

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterInputHandler.cs
SHA256：E6D859DA5C58856A29B8C5F20A340DDE66F72EACF9846FC8F31911BF7ADE2BA5

```text
001 | using UnityEngine.EventSystems;
002 | using System.Collections.Generic;
003 | using UnityEngine;
004 | using UnityEngine.InputSystem;
005 | 
006 | namespace Ossastra.Characters
007 | {
008 |     public struct CharacterInputFrame
009 |     {
010 |         public Vector2 move, pointer, aimDirection;
011 |         public bool attackHeld, cancel, pause, hasPointer, hasDirectionalAim, attackPointerBlocked;
012 |         public int debugCommand;
013 |     }
014 | 
015 |     // Each character owns its serialized actions. Input does not accept attacks or write gameplay state.
016 |     [DisallowMultipleComponent]
017 |     public sealed class CharacterInputHandler : MonoBehaviour, ICharacterInputSource
018 |     {
019 |         [Header("移动 / VELGITA movementAction")]
020 |         public InputAction movementAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
021 |         [Header("鼠标位置 / VELGITA mouseAction")]
022 |         public InputAction mouseAction = new InputAction("Pointer", InputActionType.Value, expectedControlType: "Vector2");
023 |         [Header("手柄瞄准方向 / VELGITA gamePad_MouseAction")]
024 |         public InputAction gamepadAimAction = new InputAction("GamepadAim", InputActionType.Value, expectedControlType: "Vector2");
025 |         [Header("当前普攻 / VELGITA HoundWeaponAttack_Action")]
026 |         public InputAction attackAction = new InputAction("Attack", InputActionType.Button);
027 |         [Header("取消与暂停")]
028 |         public InputAction cancelAction = new InputAction("Cancel", InputActionType.Button);
029 |         public InputAction pauseAction = new InputAction("Pause", InputActionType.Button);
030 |         [Header("开发检查入口")]
031 |         public InputAction debugValidAction = new InputAction("DebugValid", InputActionType.Button);
032 |         public InputAction debugBlockedAction = new InputAction("DebugBlocked", InputActionType.Button);
033 |         public InputAction debugResetAction = new InputAction("DebugReset", InputActionType.Button);
034 |         public InputAction debugInspectAction = new InputAction("DebugInspect", InputActionType.Button);
035 | 
036 |         private readonly Dictionary<object, Rect> pointerRegions = new Dictionary<object, Rect>();
037 |         public void RegisterPointerRegion(object owner, Rect region) { pointerRegions[owner] = region; }
038 |         public void RemovePointerRegion(object owner) { pointerRegions.Remove(owner); }
039 | 
040 |         private bool IsPointerBlocked(Vector2 pointer)
041 |         {
042 |             if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return true;
043 |             var guiPoint = new Vector2(pointer.x, Screen.height - pointer.y);
044 |             foreach (var region in pointerRegions.Values) if (region.Contains(guiPoint)) return true;
045 |             return false;
046 |         }
047 | 
048 |         public CharacterInputFrame CurrentFrame { get; private set; }
049 | 
050 |         public bool IsConfigured => movementAction != null && movementAction.bindings.Count > 0 &&
051 |             attackAction != null && attackAction.bindings.Count > 0 &&
052 |             ((mouseAction != null && mouseAction.bindings.Count > 0) ||
053 |              (gamepadAimAction != null && gamepadAimAction.bindings.Count > 0));
054 | 
055 |         private IEnumerable<InputAction> Actions()
056 |         {
057 |             yield return movementAction;
058 |             yield return mouseAction;
059 |             yield return gamepadAimAction;
060 |             yield return attackAction;
061 |             yield return cancelAction;
062 |             yield return pauseAction;
063 |             yield return debugValidAction;
064 |             yield return debugBlockedAction;
065 |             yield return debugResetAction;
066 |             yield return debugInspectAction;
067 |         }
068 | 
069 |         private void OnEnable() { foreach (var action in Actions()) if (action != null) action.Enable(); }
070 |         private void OnDisable() { CurrentFrame = default; foreach (var action in Actions()) if (action != null) action.Disable(); }
071 |         private void OnDestroy() { foreach (var action in Actions()) if (action != null) action.Dispose(); }
072 | 
073 |         public CharacterInputFrame Read()
074 |         {
075 |             var frame = new CharacterInputFrame();
076 |             if (!isActiveAndEnabled || movementAction == null || !movementAction.enabled)
077 |             { CurrentFrame = frame; return frame; }
078 |             frame.move = movementAction.ReadValue<Vector2>();
079 |             if (mouseAction != null && mouseAction.enabled)
080 |             {
081 |                 frame.pointer = mouseAction.ReadValue<Vector2>();
082 |                 frame.hasPointer = mouseAction.controls.Count > 0 && frame.pointer.x >= 0 && frame.pointer.x <= Screen.width &&
083 |                     frame.pointer.y >= 0 && frame.pointer.y <= Screen.height && !IsPointerBlocked(frame.pointer);
084 |             }
085 |             if (gamepadAimAction != null && gamepadAimAction.enabled)
086 |             {
087 |                 frame.aimDirection = gamepadAimAction.ReadValue<Vector2>();
088 |                 frame.hasDirectionalAim = frame.aimDirection.sqrMagnitude > .01f;
089 |             }
090 |             frame.attackHeld = attackAction != null && attackAction.enabled && attackAction.IsPressed();
091 |             if (attackAction != null && attackAction.activeControl != null && attackAction.activeControl.device is Pointer &&
092 |                 !frame.hasPointer) frame.attackPointerBlocked = true;
093 |             frame.cancel = Pressed(cancelAction);
094 |             frame.pause = Pressed(pauseAction);
095 |             frame.debugCommand = Pressed(debugValidAction) ? 1 : Pressed(debugBlockedAction) ? 2 :
096 |                 Pressed(debugResetAction) ? 3 : Pressed(debugInspectAction) ? 4 : 0;
097 |             CurrentFrame = frame;
098 |             return frame;
099 |         }
100 | 
101 |         private static bool Pressed(InputAction action) => action != null && action.enabled && action.WasPressedThisFrame();
102 |     }
103 | }
```

| 行区间 | 具体行为、调用与读写权威 |
|---|---|
| 1–7 | 只依赖Unity输入和UI有效性查询，不引入动作runtime、HP或Spine。 |
| 8–15 | 值类型采样快照：移动、屏幕指针、手柄方向和按键状态均camelCase；attackPointerBlocked与Held分开，防止UI内按住移出后变成幽灵新按下。 |
| 16–35 | 每个角色一个输入组件；十个InputAction在Inspector序列化，绑定与逻辑分离，字段Header说明旧VELGITA来源，不硬读设备当前按键。 |
| 36–47 | 每个输入实例保存owner→UI阻挡矩形；开发组件只登记自己的区域，停用时移除。IsPointerBlocked先查UGUI，再把屏幕坐标转GUI坐标并检查区域，不更改动作资格。 |
| 48–54 | 唯一输入写入点是此组件。Bus只取其副本；IsConfigured检查移动/攻击绑定和至少一种瞄准来源，不能把缺配置的Prefab默认为可运行。 |
| 55–68 | 同步枚举一份动作名单，供启用/停用/释放复用；没有StartCoroutine或跨帧等待。新增动作需加入此名单。 |
| 69–72 | 启用时Enable各动作；禁用时先清输入帧再Disable；销毁时Dispose自己拥有的动作。不会操作别的角色动作。 |
| 73–89 | 接口消费者Root调用：禁用/无移动动作时发布零帧；否则按Action读取移动。鼠标保留原屏幕位置，但合法性需在窗口内且不压UI；手柄方向独立采样。 |
| 90–100 | Held来自Action原始按住状态，不因UI隐藏；仅标记鼠标指针阻挡，让Root消耗边沿而拒绝请求。Cancel/Pause/开发命令取本帧按下；最后一次性发布CurrentFrame并返回同一值。 |
| 101–103 | 统一空引用/启用状态检查后查询WasPressedThisFrame；这是按帧查询，不是限帧或等待协程。 |
用户结论：待审。修改后受影响行区间必须重审。

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterMeleeHit.cs
SHA256：0A259D33FE3948688F785CA09E49AE075F3025EA824890F6D14FBD3BB80FC398

```text
001 | using System;
002 | using System.Linq;
003 | using Ossastra.CharacterCore;
004 | using UnityEngine;
005 | 
006 | namespace Ossastra.Characters
007 | {
008 |     public sealed class CharacterMeleeHit : MonoBehaviour, ICharacterHitResolver
009 |     {
010 |         [SerializeField, Min(0)] private float reach = .8f;
011 |         [SerializeField, Min(.01f)] private float radius = .65f;
012 |         [SerializeField] private float height = .8f;
013 |         [SerializeField] private LayerMask targetLayers = ~0;
014 |         public string LastResult { get; private set; } = "未释放";
015 | 
016 | public void Resolve(AttackRelease release, Vector3 releaseDirection)
017 |         {
018 |             var direction = releaseDirection;
019 |             var center = transform.position + Vector3.up * height + direction * reach;
020 |             var targets = Physics.OverlapSphere(center, radius, targetLayers, QueryTriggerInteraction.Collide)
021 |                 .Select(c => c.GetComponentsInParent<MonoBehaviour>().OfType<ICharacterHitTarget>().FirstOrDefault()).Where(t => t != null).Distinct().ToArray();
022 |             var outcomes = release.Resolve(targets.Select(t => t.Health));
023 |             foreach (var outcome in outcomes)
024 |                 if (outcome.Reason == HitReason.Applied)
025 |                     targets.First(t => t.Health.Id == outcome.TargetId).ShowHit();
026 |             LastResult = outcomes.Count == 0 ? "空挥" : string.Join("; ", outcomes.Select(o =>
027 |                 o.TargetId + " " + o.HpBefore + "→" + o.HpAfter + " " + o.Reason));
028 |         }
029 |     }
030 | }
```

| 行区间 | 具体行为、调用与读写权威 |
|---|---|
| 1–7 | 只依赖领域结算、Unity物理和目标接口；不再引用Development或保存无用途Bus。 |
| 8–15 | Inspector几何参数保持reach/radius/height/LayerMask；LastResult是诊断文本，不是第二份HP或命中权威。 |
| 16–30 | 仅由Root收到Released后调用，传入的releaseDirection是释放帧值快照。计算球心后OverlapSphere，沿Collider父级查ICharacterHitTarget，过滤空并按目标对象去重。把同一批Health交给AttackRelease；只对Applied结果ShowHit，最后格式化实际HP前后值。Release自身防止二次结算。 |
用户结论：待审。修改后受影响行区间必须重审。

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterMovement.cs
SHA256：61C538C455F66C9BF6F5F396A0FDECC7C2328197ADC4A43EF0AE6CAF4DA637C7

```text
001 | using System;
002 | using UnityEngine;
003 | 
004 | namespace Ossastra.Characters
005 | {
006 |     [DisallowMultipleComponent]
007 |     [RequireComponent(typeof(CharacterController))]
008 |     public sealed class CharacterMovement : MonoBehaviour, ICharacterMovement
009 |     {
010 |         [SerializeField, Min(.01f)] private float speed = 3;
011 |         [SerializeField, Min(.01f)] private float groundStick = 2;
012 |         private CharacterController body;
013 |         private CharacterDataBus dataBus;
014 |         public bool IsMoving { get; private set; }
015 |         public Vector3 Velocity { get; private set; }
016 | 
017 |         public bool Initialize(CharacterDataBus dataBus)
018 |         {
019 |             if (dataBus == null) throw new ArgumentNullException(nameof(dataBus));
020 |             this.dataBus = dataBus;
021 |             body = GetComponent<CharacterController>();
022 |             return body != null;
023 |         }
024 | 
025 |         public void Tick(bool allowed, float delta)
026 |         {
027 |             IsMoving = false;
028 |             Velocity = Vector3.zero;
029 |             if (dataBus == null || body == null || !body.enabled || delta <= 0) return;
030 |             var move = allowed ? Vector2.ClampMagnitude(dataBus.Input.move, 1) : Vector2.zero;
031 |             var before = transform.position;
032 |             body.Move((new Vector3(move.x, 0, move.y) * speed + Vector3.down * groundStick) * delta);
033 |             var displacement = transform.position - before;
034 |             displacement.y = 0;
035 |             Velocity = displacement / delta;
036 |             IsMoving = displacement.sqrMagnitude > .000001f;
037 |         }
038 | 
039 |         public void Teleport(Vector3 position)
040 |         {
041 |             if (body == null) return;
042 |             var wasEnabled = body.enabled;
043 |             body.enabled = false;
044 |             transform.position = position;
045 |             body.enabled = wasEnabled;
046 |             IsMoving = false;
047 |             Velocity = Vector3.zero;
048 |         }
049 |     }
050 | }
```

| 行区间 | 具体行为、调用与读写权威 |
|---|---|
| 1–5 | 运动只依赖Unity CharacterController及初始化参数验证，不决定攻击角度。 |
| 6–16 | 同角色唯一运动组件，RequireComponent确保胶囊控制器存在；speed/groundStick仍保留原Inspector数据。速度和是否移动由实际位移得出。 |
| 17–24 | Root通过ICharacterSubsystem注入Bus，验证非空并绑定同物体CharacterController；找不到控制器返回false，让Root停止初始化。 |
| 25–38 | 每次清旧速度，未初始化/控制器停用/delta非正直接返回。获准时从Bus.Input.move取向量并限制长度，加显式贴地运动；碰撞后的实际水平差值计算Velocity/IsMoving，不把输入当作实际速度。 |
| 39–50 | 显式位置变更入口：暂时停用CharacterController，改位置，再恢复原启用状态，清移动结果；不更新朝向或HP。 |
用户结论：待审。修改后受影响行区间必须重审。

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterOrientation.cs
SHA256：87E4DDD0BC60C59419C22B689699CC6234F3AF02EEA0B3CE5F415E3A7091369D

```text
001 | using UnityEngine;
002 | 
003 | namespace Ossastra.Characters
004 | {
005 |     [DisallowMultipleComponent]
006 |     public sealed class CharacterOrientation : MonoBehaviour, ICharacterOrientation
007 |     {
008 |         public Vector3 AttackDirection { get; private set; } = Vector3.right;
009 |         public float VisualFacingX { get; private set; } = 1;
010 | 
011 |         public void ResolveDirections(Vector2 moveInput, Vector3? pointerDirection, Vector2 stickDirection, bool movementFacing)
012 |         {
013 |             var pointerValid = pointerDirection.HasValue && TrySetAttackDirection(pointerDirection.Value);
014 |             if (!pointerValid && !TrySetAttackDirection(new Vector3(stickDirection.x, 0, stickDirection.y)))
015 |                 TrySetAttackDirection(new Vector3(moveInput.x, 0, moveInput.y));
016 |             if (movementFacing && Mathf.Abs(moveInput.x) > .01f) VisualFacingX = Mathf.Sign(moveInput.x);
017 |         }
018 | 
019 |         public void OrientTowards(Vector3 worldPosition) { TrySetAttackDirection(worldPosition - transform.position); }
020 | 
021 |         public void FaceAttackDirection()
022 |         {
023 |             if (Mathf.Abs(AttackDirection.x) > .01f) VisualFacingX = Mathf.Sign(AttackDirection.x);
024 |         }
025 | 
026 |         private bool TrySetAttackDirection(Vector3 direction)
027 |         {
028 |             direction.y = 0;
029 |             if (float.IsNaN(direction.x) || float.IsNaN(direction.z) || float.IsInfinity(direction.x) ||
030 |                 float.IsInfinity(direction.z) || direction.sqrMagnitude < .0001f) return false;
031 |             AttackDirection = direction.normalized;
032 |             return true;
033 |         }
034 |     }
035 | }
```

| 行区间 | 具体行为、调用与读写权威 |
|---|---|
| 1–4 | 该组件只用世界向量和数学函数，不依赖输入设备、动画或物理执行。 |
| 5–10 | 每角色独立状态：AttackDirection默认右，VisualFacingX默认1；属性只有本组件可写，前者进物理出手，后者只进显示。 |
| 11–18 | 调用方先决定是否允许移动显示。攻击方向依次尝试合法鼠标、右摇杆、移动向量；全部无效则保留原合法方向。可动水平输入独立改变VisualFacingX，不覆盖攻击方向；W/S不抖左右。 |
| 19–20 | 显式世界目标适配，只更新攻击方向，不再暗改显示。开发传送后需显式FaceAttackDirection，调用含义可查。 |
| 21–25 | 只在攻击接受/释放等明确场合把显示朝向转到出手侧；攻击X分量近零时保留左右符号，避免正前/正后抖动。 |
| 26–35 | 压平Y；拒绝NaN、Infinity和近零长度；合法方向归一后成为唯一AttackDirection，返回成功与否供有序回退。 |
用户结论：待审。修改后受影响行区间必须重审。

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterRoot.cs
SHA256：B99C7D9438F338B219EE92CDD2C2A8140F2B8ED7DC257B526EA5D7A1DA79982F

```text
001 | using System;
002 | using Ossastra.CharacterCore;
003 | using UnityEngine;
004 | using UnityEngine.Serialization;
005 | 
006 | namespace Ossastra.Characters
007 | {
008 |     [DefaultExecutionOrder(-100)]
009 |     [RequireComponent(typeof(CharacterMovement), typeof(CharacterMeleeHit), typeof(CharacterInputHandler))]
010 |     [RequireComponent(typeof(CharacterOrientation))]
011 |     public sealed class CharacterRoot : MonoBehaviour
012 |     {
013 |         [SerializeField] private string actorId = "player";
014 |         [FormerlySerializedAs("motor"), SerializeField] private CharacterMovement movement;
015 |         [SerializeField] private CharacterOrientation orientation;
016 |         [SerializeField] private CharacterSpineView view;
017 |         [SerializeField] private CharacterMeleeHit melee;
018 |         [SerializeField] private CharacterInputHandler input;
019 |         [SerializeField] private Camera viewCamera;
020 |         [Header("Prototype timing — not final balance")]
021 |         [SerializeField] private float releaseAt = .3f, moveReadyAt = .5f, attackReadyAt = .8f, finishAt = 1.1f;
022 |         [SerializeField, Min(1)] private int damage = 12;
023 |         [SerializeField] private bool verboseTrace;
024 |         private readonly ControlAuthority authority = new ControlAuthority();
025 |         private readonly AttackIntent intent = new AttackIntent();
026 |         private ICharacterInputSource inputSource;
027 |         private ICharacterMovement mover;
028 |         private ICharacterOrientation orienter;
029 |         private ICharacterPresentation presentation;
030 |         private ICharacterHitResolver hitResolver;
031 |         private BasicActionRuntime runtime;
032 |         private BasicAttackDefinition definition;
033 |         private bool ready, focused = true, paused, applicationPaused;
034 |         private string lastFact = "Idle";
035 |         public CharacterDataBus DataBus { get; private set; }
036 |         public string LastFact => lastFact;
037 |         public bool IsPaused => paused || applicationPaused;
038 |         public bool ActionActive => DataBus != null && DataBus.ActionActive;
039 | 
040 |         private void Awake()
041 |         {
042 |             if (viewCamera == null) viewCamera = Camera.main;
043 |             if (movement == null || orientation == null || view == null || melee == null || input == null || viewCamera == null)
044 |             { Debug.LogError("Character requires registered input/movement/orientation/view/melee/camera", this); enabled = false; return; }
045 |             inputSource = input;
046 |             mover = movement;
047 |             orienter = orientation;
048 |             presentation = view;
049 |             hitResolver = melee;
050 |             if (!inputSource.IsConfigured) { Debug.LogError("Character actions are not configured", this); enabled = false; return; }
051 |             try
052 |             {
053 |                 definition = new BasicAttackDefinition(releaseAt, moveReadyAt, attackReadyAt, finishAt, damage);
054 |                 runtime = new BasicActionRuntime(actorId);
055 |             }
056 |             catch (ArgumentException error) { Debug.LogError("Character configuration: " + error.Message, this); enabled = false; return; }
057 |             DataBus = new CharacterDataBus(actorId, inputSource, mover, orienter, runtime, authority);
058 |             ready = InitializeSubsystems(DataBus, mover, presentation);
059 |             if (!ready) { Debug.LogError("Character subsystem initialization failed", this); enabled = false; }
060 |         }
061 | 
062 |         private static bool InitializeSubsystems(CharacterDataBus dataBus, params ICharacterSubsystem[] subsystems)
063 |         {
064 |             foreach (var subsystem in subsystems) if (subsystem == null || !subsystem.Initialize(dataBus)) return false;
065 |             return true;
066 |         }
067 | 
068 |         private void Update()
069 |         {
070 |             if (!ready) return;
071 |             inputSource.Read();
072 |             var frame = DataBus.Input;
073 |             if (frame.pause) SetPaused(!paused);
074 |             if (frame.cancel) CancelAction("player-cancel");
075 |             var canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
076 |             if (focused && !IsPaused)
077 |                 orienter.ResolveDirections(frame.move, PointerDirection(frame), frame.hasDirectionalAim ? frame.aimDirection : Vector2.zero, canMove);
078 |             if (intent.Observe(frame.attackHeld) && focused && !frame.attackPointerBlocked)
079 |             {
080 |                 var request = runtime.Request(definition, "player-input", authority, IsPaused);
081 |                 if (request.PreviousTerminal != null) Observe(request.PreviousTerminal);
082 |                 if (request.Accepted) { Observe(request.Fact); orienter.FaceAttackDirection(); }
083 |                 else lastFact = "Rejected: " + request.Reason;
084 |             }
085 |             foreach (var fact in runtime.Advance(Time.deltaTime, IsPaused || !focused))
086 |             {
087 |                 Observe(fact);
088 |                 if (fact.Release != null)
089 |                 {
090 |                     // Explicit release-frame value snapshot, not an acceptance-time direction lock.
091 |                     var releaseDirection = orienter.AttackDirection;
092 |                     orienter.FaceAttackDirection();
093 |                     hitResolver.Resolve(fact.Release, releaseDirection);
094 |                 }
095 |             }
096 |             canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
097 |             mover.Tick(canMove, IsPaused || !focused ? 0 : Time.deltaTime);
098 |             presentation.SetPaused(IsPaused || !focused);
099 |             presentation.Present();
100 |         }
101 | 
102 |         private Vector3? PointerDirection(CharacterInputFrame frame)
103 |         {
104 |             if (!frame.hasPointer) return null;
105 |             var plane = new Plane(Vector3.up, transform.position);
106 |             var ray = viewCamera.ScreenPointToRay(frame.pointer);
107 |             if (!plane.Raycast(ray, out var distance)) return null;
108 |             var direction = ray.GetPoint(distance) - transform.position;
109 |             direction.y = 0;
110 |             return direction.sqrMagnitude < .0001f ? (Vector3?)null : direction;
111 |         }
112 | 
113 |         private void Observe(ActionFact fact)
114 |         {
115 |             lastFact = fact.Kind + " @" + fact.At.ToString("F3") + " " + fact.ActionId.ToString("N").Substring(0, 8);
116 |             if (fact.Reason != null) lastFact += " " + fact.Reason;
117 |             if (verboseTrace) Debug.Log("Character " + lastFact, this);
118 |         }
119 | 
120 |         public void CancelAction(string reason)
121 |         {
122 |             intent.SuppressUntilRelease();
123 |             var terminal = runtime == null ? null : runtime.Cancel(reason);
124 |             if (terminal != null) Observe(terminal);
125 |         }
126 | 
127 |         public void SetPaused(bool value) { paused = value; intent.SuppressUntilRelease(); }
128 |         public void SetAttackRestriction(object owner, bool restricted, string reason = "attack-restricted")
129 |         {
130 |             if (restricted) { authority.Block(owner, ControlChannel.Attack); CancelAction(reason); }
131 |             else authority.Release(owner);
132 |         }
133 | 
134 |         private void OnApplicationFocus(bool hasFocus)
135 |         { focused = hasFocus; if (!hasFocus) CancelAction("focus-lost"); else intent.SuppressUntilRelease(); }
136 |         private void OnApplicationPause(bool value)
137 |         { applicationPaused = value; if (value) intent.SuppressUntilRelease(); }
138 |         private void OnEnable() { intent.SuppressUntilRelease(); }
139 |         private void OnDisable() { CancelAction("disabled"); if (ready && presentation != null) presentation.SetPaused(false); }
140 |         private void OnDestroy()
141 |         {
142 |             CancelAction("destroyed");
143 |             ready = false;
144 |             DataBus = null;
145 |             runtime = null;
146 |             inputSource = null;
147 |             mover = null;
148 |             orienter = null;
149 |             presentation = null;
150 |             hitResolver = null;
151 |         }
152 |     }
153 | }
```

| 行区间 | 具体行为、调用与读写权威 |
|---|---|
| 1–12 | 引入System、领域层、Unity与序列化兼容属性；没有Development依赖。下面的执行顺序保证输入/动作主调度早于开发面板更新。 |
| 13–23 | Inspector身份和具体组件槽；movement保留旧motor字段迁移标记，其他引用由Prefab持久化。动作四个时间点和damage仍是候选数值，不是架构常量。 |
| 24–39 | Root拥有权限对象和按下边沿对象；五个接口字段只保存注册引用。runtime独占动作状态；focused/paused/applicationPaused属于应用与角色暂停条件；LastFact只是诊断，不参与判定。 |
| 40–61 | Unity创建角色时调用：先解析缺省相机，逐项验证组件，再具体槽赋值到窄接口；检查输入绑定。创建不可变攻击配置和唯一runtime，非法配置报错禁用。随后创建每角色Bus，明确按movement→presentation注入；失败禁用Root，不能部分就绪继续跑。 |
| 62–67 | 同一个实际初始化循环被Root和替换实现测试调用；从左到右调用接口Initialize，任何null/false立即返回，不执行后续模块。Input/Orientation/Hit不需要无意义注入。 |
| 68–74 | Unity每帧唯一调度：接口Read发布CurrentFrame；处理暂停和具名取消；分别计算移动许可与方向。本帧输入资格不等于动作接受，下一段单独提交请求。 |
| 75–77 | 动作推进可能跨过MoveReady，故重新计算许可。Movement接口执行位移；失焦/暂停delta为0。Presentation按同一Bus读取真实结果并同步暂停。 |
| 78–84 | 先消耗真实Held的按下边沿，再检查焦点与鼠标UI阻挡；向runtime提交。替换旧恢复尾部时先记录PreviousTerminal，再记录Accepted，只有接受后才显示面向攻击方向；拒绝仅记原因。 |
| 85–101 | 推进唯一领域时钟，暂停/失焦不推进。逐个处理有序事实；Released时复制AttackDirection到局部Vector3并交给HitResolver，同时显示面向出手方向，不把Spine回调变成第二时钟。 |
| 102–112 | 屏幕指针必须先通过InputHandler有效性筛选；再投射到角色所在XZ平面。无交点、近零方向返回null以触发回退，不把屏幕像素当世界向量。 |
| 113–119 | 只把事实写到诊断字符串，verboseTrace只决定日志；动作身份/伤害不依赖日志开关。 |
| 120–126 | 供输入/生命周期/开发工具使用的通用具名取消入口。先要求松键重新武装，再取消尚存runtime；若已无动作，不伪造终结事实。 |
| 127–133 | SetPaused修改角色暂停并抑制Held复用。SetAttackRestriction按owner添加/解除禁攻，添加时取消当前动作；无法解除其他owner限制。 |
| 134–139 | 失焦具名取消；恢复焦点要求先松键。应用暂停记录独立标志。启用抑制Held、禁用取消并恢复动画速度，不重新构造动作状态。 |
| 140–153 | 销毁再做幂等取消，然后清ready/Bus/runtime/接口引用；输入动作由InputHandler自身释放，无静态注册表残留。 |
用户结论：待审。修改后受影响行区间必须重审。

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterSpineView.cs
SHA256：F8E6B69C2D8665F17A95686D618F4415F293CC039AED6673252B5AE998BA78BA

```text
001 | using System;
002 | using Spine.Unity;
003 | using UnityEngine;
004 | 
005 | namespace Ossastra.Characters
006 | {
007 |     public sealed class CharacterSpineView : MonoBehaviour, ICharacterPresentation
008 |     {
009 |         [SerializeField] private SkeletonAnimation skeleton;
010 |         [SerializeField] private string idle = "idle1_1", move = "run", attack = "atk1";
011 |         private CharacterDataBus dataBus;
012 |         private string lastClip;
013 |         private Guid lastAction;
014 |         private float originalScaleX;
015 |         private float originalTimeScale;
016 |         public string CurrentClip => lastClip;
017 | 
018 |         public bool Initialize(CharacterDataBus dataBus)
019 |         {
020 |             if (dataBus == null) return false;
021 |             this.dataBus = dataBus;
022 |             if (skeleton == null) skeleton = GetComponent<SkeletonAnimation>();
023 |             if (skeleton == null || skeleton.skeletonDataAsset == null) return false;
024 |             skeleton.Initialize(false);
025 |             var data = skeleton.skeletonDataAsset.GetSkeletonData(false);
026 |             if (data == null || data.FindAnimation(idle) == null || data.FindAnimation(move) == null ||
027 |                 data.FindAnimation(attack) == null || skeleton.Skeleton == null) return false;
028 |             originalScaleX = Mathf.Abs(skeleton.Skeleton.ScaleX);
029 |             originalTimeScale = skeleton.timeScale;
030 |             return true;
031 |         }
032 | 
033 |         public void SetPaused(bool paused)
034 |         {
035 |             if (skeleton != null) skeleton.timeScale = paused ? 0 : originalTimeScale;
036 |         }
037 | 
038 |         public void Present()
039 |         {
040 |             if (dataBus == null || skeleton == null || skeleton.Skeleton == null || skeleton.AnimationState == null) return;
041 |             var actionActive = dataBus.ActionActive;
042 |             var actionId = dataBus.ActionId;
043 |             var moving = dataBus.IsMoving;
044 |             var facing = dataBus.VisualFacingX;
045 |             var clip = actionActive && !moving ? attack : moving ? move : idle;
046 |             var replay = clip == attack && actionId != lastAction;
047 |             skeleton.Skeleton.ScaleX = originalScaleX * facing;
048 |             if (clip != lastClip || replay)
049 |             {
050 |                 skeleton.AnimationState.SetAnimation(0, clip, clip != attack);
051 |                 lastClip = clip;
052 |             }
053 |             lastAction = actionId;
054 |         }
055 |     }
056 | }
```

| 行区间 | 具体行为、调用与读写权威 |
|---|---|
| 1–6 | Spine.Unity仅留在表现适配器，不让骨架类型进入动作核心。 |
| 7–17 | 保存序列化骨架/clip，Bus引用和上一clip/动作ID；原ScaleX绝对值和timeScale用于可逆显示。 |
| 18–32 | 接口初始化验证Bus、骨架数据与idle/run/atk1动画是否真实存在；初始化骨架后保存原比例/速度。失败返回false，不用假角色替代成功。 |
| 33–37 | 只把骨架速度置0或恢复初始化捕获值，不修改动作runtime的时钟。 |
| 38–56 | 从Bus读取真实动作ID、位移结果、VisualFacingX；攻击且不移动选atk1，否则run/idle。同clip新动作允许重播，其他情况去重；ScaleX仅改视觉符号，未变碰撞与攻击方向。 |
用户结论：待审。修改后受影响行区间必须重审。

## Assets/OSSASTRA/Scripts/Development/Combat/CharacterDebugControls.cs
SHA256：E87FA6FEEA674F29DE9BF0C07EA1DF69608C2550F33A6489DD838BDED524D2A3

```text
001 | using Ossastra.Characters;
002 | using UnityEngine;
003 | 
004 | namespace Ossastra.Development
005 | {
006 |     [DefaultExecutionOrder(100)]
007 |     public sealed class CharacterDebugControls : MonoBehaviour
008 |     {
009 |         [SerializeField] private Ossastra.Characters.CharacterRoot character;
010 |         [SerializeField] private CombatTestTarget target;
011 |         private CharacterInputHandler input;
012 |         private bool blocked;
013 |         private static readonly Rect panel = new Rect(12, 12, 400, 270);
014 | 
015 |         private void OnEnable()
016 |         {
017 |             if (character == null) character = GetComponent<Ossastra.Characters.CharacterRoot>();
018 |             input = GetComponent<CharacterInputHandler>();
019 |             if (input != null) input.RegisterPointerRegion(this, panel);
020 |         }
021 |         private void OnDisable()
022 |         {
023 |             if (input != null) input.RemovePointerRegion(this);
024 |             if (character != null) character.SetAttackRestriction(this, false);
025 |             blocked = false;
026 |         }
027 |         private void Update()
028 |         {
029 |             if (character == null || character.DataBus == null) return;
030 |             var command = character.DataBus.Input.debugCommand;
031 |             if (command != 0) DebugCommand(command);
032 |         }
033 |         public void DebugCommand(int command)
034 |         {
035 |             if (character == null || character.DataBus == null) return;
036 |             if (command == 1 && target != null)
037 |             {
038 |                 character.CancelAction("debug-near-target");
039 |                 target.RestoreHealth();
040 |                 var movement = GetComponent<CharacterMovement>();
041 |                 var orientation = GetComponent<CharacterOrientation>();
042 |                 if (movement != null) movement.Teleport(target.transform.position + Vector3.left * 1.15f + Vector3.up * .03f);
043 |                 if (orientation != null) { orientation.OrientTowards(target.transform.position); orientation.FaceAttackDirection(); }
044 |             }
045 |             if (command == 2)
046 |             {
047 |                 blocked = !blocked;
048 |                 character.SetAttackRestriction(this, blocked, "debug-attack-block");
049 |             }
050 |             if (command == 3)
051 |             {
052 |                 character.CancelAction("debug-clear");
053 |                 character.SetAttackRestriction(this, false);
054 |                 character.SetPaused(false);
055 |                 blocked = false;
056 |                 if (target != null) target.RestoreHealth();
057 |             }
058 |             if (command == 4) Debug.Log("Character " + character.DataBus.ActorId + " " + character.LastFact, character);
059 |         }
060 |         private void OnGUI()
061 |         {
062 |             if (character == null) return;
063 |             GUILayout.BeginArea(panel, GUI.skin.box);
064 |             GUILayout.Label("角色候选 · WASD移动 / 左键普攻 / X取消 / P暂停");
065 |             GUILayout.Label(character.LastFact);
066 |             GUILayout.Label("暂停: " + character.IsPaused + " | 禁攻: " + blocked);
067 |             if (GUILayout.Button("F1 合法状态：恢复靶并站到攻击距离")) DebugCommand(1);
068 |             if (GUILayout.Button("F2 非法状态：切换owner禁攻")) DebugCommand(2);
069 |             if (GUILayout.Button("F3 清理：取消／解除禁攻／恢复靶")) DebugCommand(3);
070 |             if (GUILayout.Button("F4 检查：输出动作状态")) DebugCommand(4);
071 |             GUILayout.EndArea();
072 |         }
073 |     }
074 | }
```

| 行区间 | 具体行为、调用与读写权威 |
|---|---|
| 1–5 | Development依赖正式组件与Unity GUI；生产Root不会反向引用本类。 |
| 6–14 | 在Root本帧采样之后消费开发命令；保存当前角色与场景开发靶的Inspector引用、自身UI区域和owner禁攻状态。 |
| 15–20 | 同角色绑定输入组件并登记此owner的GUI区域，避免点击按钮同时普攻；不扫描全场服务。 |
| 21–26 | 只移除自身UI区域与自身owner禁攻，清本地开发标志，不解除其他控制者。 |
| 27–32 | 角色/Bus尚未就绪不处理；读取本帧debugCommand并调用明确开发入口。 |
| 33–59 | F1取消、恢复靶、通过运动组件传送、显式设攻击方向和显示；F2通过Root添加/解除自身禁攻；F3取消/清自身禁攻/解除暂停/恢复靶；F4只输出真实状态。无直接伪造成功UI。 |
| 60–74 | 开发面板只显示真实LastFact/暂停/禁攻，并把按钮映射到同一DebugCommand；移出Root后可独立禁用，不影响生产依赖。 |
用户结论：待审。修改后受影响行区间必须重审。

## Assets/OSSASTRA/Scripts/Development/Combat/CombatTestTarget.cs
SHA256：0AA909B79AB31A42630237694A5321744B4F2071DAD81499A1D3EF1FC33CA557

```text
001 | using Ossastra.Characters;
002 | using Ossastra.CharacterCore;
003 | using UnityEngine;
004 | 
005 | namespace Ossastra.Development
006 | {
007 |     public sealed class CombatTestTarget : MonoBehaviour, ICharacterHitTarget
008 |     {
009 |         [SerializeField] private string targetId = "target-a";
010 |         [SerializeField, Min(1)] private int maximumHp = 100;
011 |         [SerializeField, Min(.01f)] private float flashSeconds = .15f;
012 |         private Renderer[] renderers;
013 |         private MaterialPropertyBlock block;
014 |         private float flashUntil;
015 |         private bool flashing;
016 |         public Health Health { get; private set; }
017 | 
018 |         private void Awake()
019 |         {
020 |             Health = new Health(targetId, maximumHp);
021 |             renderers = GetComponentsInChildren<Renderer>();
022 |             block = new MaterialPropertyBlock();
023 |         }
024 | 
025 |         public void ShowHit() { flashing = true; flashUntil = Time.time + flashSeconds; Paint(Color.white); }
026 |         public void RestoreHealth()
027 |         {
028 |             Health = new Health(targetId, maximumHp);
029 |             flashing = false;
030 |             Paint(new Color(.15f, .7f, .7f));
031 |         }
032 | 
033 |         private void Update()
034 |         {
035 |             if (!flashing || Time.time < flashUntil) return;
036 |             flashing = false;
037 |             Paint(Health.Current == 0 ? Color.gray : new Color(.15f, .7f, .7f));
038 |         }
039 | 
040 |         private void Paint(Color color)
041 |         {
042 |             if (block == null) return;
043 |             block.SetColor("_BaseColor", color);
044 |             block.SetColor("_Color", color);
045 |             foreach (var r in renderers) if (r != null) r.SetPropertyBlock(block);
046 |         }
047 | 
048 |         private void OnGUI()
049 |         {
050 |             var camera = Camera.main;
051 |             if (camera == null || Health == null) return;
052 |             var point = camera.WorldToScreenPoint(transform.position + Vector3.up * 1.8f);
053 |             if (point.z <= 0) return;
054 |             GUI.Box(new Rect(point.x - 70, Screen.height - point.y, 140, 26),
055 |                 targetId + "  HP " + Health.Current + "/" + Health.Maximum);
056 |         }
057 |     }
058 | }
```

| 行区间 | 具体行为、调用与读写权威 |
|---|---|
| 1–6 | 开发层依赖正式目标接口与原Health，依赖方向由开发→生产。 |
| 7–17 | 目标ID/最大HP/闪白时长是开发配置；Renderer数组、MPB和闪白时刻只掌管表现。Health属性引用唯一结算对象。 |
| 18–24 | Play初始化该开发靶的Health和Renderers/MPB；没有另一个真实HP字段。 |
| 25–25 | Applied命中后开启闪白并设到期时间；不再次扣血。 |
| 26–32 | 明确开发重置入口重新创建目标Health、清闪白并恢复颜色；不冒充正式治疗/复活机制。 |
| 33–39 | 闪白未到期直接返回，到期只按Health.Current选死灰/活青颜色。 |
| 40–47 | MPB写颜色而非克隆/修改共享材质，逐个忽略已销毁Renderer。 |
| 48–58 | 开发HP标签取真实Health与Camera.main屏幕投影；缺相机/Health或在相机后方则不画。不是正式战斗UI。 |
用户结论：待审。修改后受影响行区间必须重审。
