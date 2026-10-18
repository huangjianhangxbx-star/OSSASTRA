# M01 核心候选逐行审查包
基准main@724408b；本批所有C#均为本机新增候选，无提交/推送。原型只读HEAD efdb30e。全部区间用户待审；以下说明不是审查通过。空行和单独花括号只承担排版/作用域，其他行逐项列出。

## Assets/OSSASTRA/CharacterCore/AttackIntent.cs
把设备Held样本转为按下边沿；失焦后必须先松开，避免恢复时误触发
SHA256: DD1F501F5756FBED7AFBDF5A7898F9B35422C5878C38C95631AF015EBDF2C78E
```csharp
namespace Ossastra.CharacterCore
{
    // 设备采样由未来Unity适配器负责；这里只保留按下边沿和重新武装规则。
    public sealed class AttackIntent
    {
        private bool previousHeld;
        private bool armed = true;

        public bool Observe(bool held)
        {
            if (!armed)
            {
                if (!held) armed = true;
                previousHeld = held;
                return false;
            }
            var pressed = held && !previousHeld;
            previousHeld = held;
            return pressed;
        }

        // 失焦/禁用后，仍按住的键不能被误当作一次新按下。
        public void SuppressUntilRelease()
        {
            previousHeld = false;
            armed = false;
        }
    }
}
```

| 行 | 中文解释（用户全待审） |
|---:|---|
| 1 | 候选命名空间隔离新实现，避免重新依赖旧CharacterRoot。 |
| 3 | 设备采样由未来Unity适配器负责；这里只保留按下边沿和重新武装规则。 |
| 4 | 定义具名职责：把设备Held样本转为按下边沿；失焦后必须先松开，避免恢复时误触发。 |
| 6 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 7 | 维护输入边沿及重新武装状态；Held持续保持不产生重复请求，重置后先等待松开。 |
| 9 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 11 | 维护输入边沿及重新武装状态；Held持续保持不产生重复请求，重置后先等待松开。 |
| 13 | 维护输入边沿及重新武装状态；Held持续保持不产生重复请求，重置后先等待松开。 |
| 14 | 维护输入边沿及重新武装状态；Held持续保持不产生重复请求，重置后先等待松开。 |
| 15 | 把本方法结果直接交给调用者；不是全局事件、动画或伤害的第二条权威路径。 |
| 17 | 维护输入边沿及重新武装状态；Held持续保持不产生重复请求，重置后先等待松开。 |
| 18 | 维护输入边沿及重新武装状态；Held持续保持不产生重复请求，重置后先等待松开。 |
| 19 | 把本方法结果直接交给调用者；不是全局事件、动画或伤害的第二条权威路径。 |
| 22 | 失焦/禁用后，仍按住的键不能被误当作一次新按下。 |
| 23 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 25 | 维护输入边沿及重新武装状态；Held持续保持不产生重复请求，重置后先等待松开。 |
| 26 | 维护输入边沿及重新武装状态；Held持续保持不产生重复请求，重置后先等待松开。 |

## Assets/OSSASTRA/CharacterCore/AttackRelease.cs
接受一次已由调用方完成空间筛选的目标批次，完成去重、自身/死亡过滤和HP结算
SHA256: 3C96F30D6F5D9B5B2B728308D95F681D9DC9A0AB7DEF09B0A315E5262F2FCBAB
```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ossastra.CharacterCore
{
    public enum HitReason { Applied, Duplicate, Dead, Self }

    public sealed class HitOutcome
    {
        public HitReason Reason { get; }
        public int HpBefore { get; }
        public int HpAfter { get; }
        public Guid ActionId { get; }
        public string ActorId { get; }
        public string TargetId { get; }

        internal HitOutcome(HitReason reason, int before, Health target, Guid actionId, string actorId)
        {
            Reason = reason;
            HpBefore = before;
            HpAfter = target.Current;
            TargetId = target.Id;
            ActionId = actionId;
            ActorId = actorId;
        }
    }

    // 每次释放只能提交一个瞬时近战候选批次；空挥也关闭，避免永久开启命中窗口。
    public sealed class AttackRelease
    {
        private readonly Guid actionId;
        private readonly string actorId;
        private readonly int damage;
        private bool resolved;

        internal AttackRelease(Guid actionId, string actorId, int damage)
        {
            this.actionId = actionId;
            this.actorId = actorId;
            this.damage = damage;
        }

        // 调用方必须先完成实际空间/阵营过滤；本方法不谎称已做物理检测。
        public IReadOnlyList<HitOutcome> Resolve(IEnumerable<Health> targets)
        {
            if (resolved) throw new InvalidOperationException("Attack release already resolved");
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            var batch = targets.ToArray();
            if (batch.Any(t => t == null)) throw new ArgumentException("Null candidate target", nameof(targets));
            resolved = true;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var outcomes = new List<HitOutcome>();
            foreach (var target in batch)
            {
                var before = target.Current;
                HitReason reason;
                if (!seen.Add(target.Id)) reason = HitReason.Duplicate;
                else if (target.Id == actorId) reason = HitReason.Self;
                else if (target.Current == 0) reason = HitReason.Dead;
                else
                {
                    target.ApplyDamage(damage);
                    reason = HitReason.Applied;
                }
                outcomes.Add(new HitOutcome(reason, before, target, actionId, actorId));
            }
            return outcomes;
        }
    }
}
```

| 行 | 中文解释（用户全待审） |
|---:|---|
| 1 | 引入本文件直接使用的标准库类型；无Unity、Spine或第三方运行依赖。 |
| 2 | 引入本文件直接使用的标准库类型；无Unity、Spine或第三方运行依赖。 |
| 3 | 引入本文件直接使用的标准库类型；无Unity、Spine或第三方运行依赖。 |
| 5 | 候选命名空间隔离新实现，避免重新依赖旧CharacterRoot。 |
| 7 | 明确实际应用、同波重复、死亡和自身四种结果，不把空挥当命中。 |
| 9 | 定义具名职责：接受一次已由调用方完成空间筛选的目标批次，完成去重、自身/死亡过滤和HP结算。 |
| 11 | 公开只读状态/配置；调用方只能观察，不能通过此属性覆写状态权威。 |
| 12 | 公开只读状态/配置；调用方只能观察，不能通过此属性覆写状态权威。 |
| 13 | 公开只读状态/配置；调用方只能观察，不能通过此属性覆写状态权威。 |
| 14 | 公开只读状态/配置；调用方只能观察，不能通过此属性覆写状态权威。 |
| 15 | 公开只读状态/配置；调用方只能观察，不能通过此属性覆写状态权威。 |
| 16 | 公开只读状态/配置；调用方只能观察，不能通过此属性覆写状态权威。 |
| 18 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 20 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：接受一次已由调用方完成空间筛选的目标批次，完成去重、自身/死亡过滤和HP结算。 |
| 21 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：接受一次已由调用方完成空间筛选的目标批次，完成去重、自身/死亡过滤和HP结算。 |
| 22 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：接受一次已由调用方完成空间筛选的目标批次，完成去重、自身/死亡过滤和HP结算。 |
| 23 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：接受一次已由调用方完成空间筛选的目标批次，完成去重、自身/死亡过滤和HP结算。 |
| 24 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：接受一次已由调用方完成空间筛选的目标批次，完成去重、自身/死亡过滤和HP结算。 |
| 25 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：接受一次已由调用方完成空间筛选的目标批次，完成去重、自身/死亡过滤和HP结算。 |
| 29 | 每次释放只能提交一个瞬时近战候选批次；空挥也关闭，避免永久开启命中窗口。 |
| 30 | 定义具名职责：接受一次已由调用方完成空间筛选的目标批次，完成去重、自身/死亡过滤和HP结算。 |
| 32 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 33 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 34 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 35 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 37 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 39 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：接受一次已由调用方完成空间筛选的目标批次，完成去重、自身/死亡过滤和HP结算。 |
| 40 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：接受一次已由调用方完成空间筛选的目标批次，完成去重、自身/死亡过滤和HP结算。 |
| 41 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：接受一次已由调用方完成空间筛选的目标批次，完成去重、自身/死亡过滤和HP结算。 |
| 44 | 调用方必须先完成实际空间/阵营过滤；本方法不谎称已做物理检测。 |
| 45 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 47 | 同一释放已经结算就拒绝再次调用，避免空挥后继续伤害新目标。 |
| 48 | 拒绝或验证非法参数；要求失败发生在改变动作/HP之前。 |
| 49 | 先完整读取并检查批次，再改任何HP，防止null目标导致部分扣血。 |
| 50 | 先完整读取并检查批次，再改任何HP，防止null目标导致部分扣血。 |
| 51 | 在整个候选批次验证成功之后关闭释放，空批次同样关闭。 |
| 52 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：接受一次已由调用方完成空间筛选的目标批次，完成去重、自身/死亡过滤和HP结算。 |
| 53 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：接受一次已由调用方完成空间筛选的目标批次，完成去重、自身/死亡过滤和HP结算。 |
| 54 | 执行本行列明的条件或遍历；当前职责是接受一次已由调用方完成空间筛选的目标批次，完成去重、自身/死亡过滤和HP结算，不跨写其他模块状态。 |
| 56 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：接受一次已由调用方完成空间筛选的目标批次，完成去重、自身/死亡过滤和HP结算。 |
| 57 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：接受一次已由调用方完成空间筛选的目标批次，完成去重、自身/死亡过滤和HP结算。 |
| 58 | 在当前一次释放中按目标身份去重；多个Collider不能让同目标重复扣血。 |
| 59 | 拒绝自身命中，比较角色/目标的稳定业务身份。 |
| 60 | 零血目标不再受伤；不会从死亡目标继续扣出负数。 |
| 61 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：接受一次已由调用方完成空间筛选的目标批次，完成去重、自身/死亡过滤和HP结算。 |
| 63 | 调用Health内部唯一HP写路径；本行没有另一份镜像HP。 |
| 64 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：接受一次已由调用方完成空间筛选的目标批次，完成去重、自身/死亡过滤和HP结算。 |
| 66 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：接受一次已由调用方完成空间筛选的目标批次，完成去重、自身/死亡过滤和HP结算。 |
| 68 | 把本方法结果直接交给调用者；不是全局事件、动画或伤害的第二条权威路径。 |

## Assets/OSSASTRA/CharacterCore/BasicActionRuntime.cs
唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine
SHA256: 4DC090007A7CF622A62A67D72B8E8912C63003F191743D2EBA2EC67F9DF3C933
```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ossastra.CharacterCore
{
    public enum ActionFactKind { Accepted, Released, MoveReady, AttackReady, Finished, Cancelled }

    // 事实是结果，不是任意广播；调用者按返回顺序处理。
    public sealed class ActionFact
    {
        public ActionFactKind Kind { get; }
        public Guid ActionId { get; }
        public string Source { get; }
        public string Reason { get; }
        public double At { get; }
        public AttackRelease Release { get; }

        internal ActionFact(ActionFactKind kind, Guid id, string source, double at,
            string reason = null, AttackRelease release = null)
        {
            Kind = kind;
            ActionId = id;
            Source = source;
            At = at;
            Reason = reason;
            Release = release;
        }
    }

    public sealed class ActionRequestResult
    {
        public bool Accepted => Fact != null;
        public string Reason { get; }
        public ActionFact Fact { get; }
        public ActionFact PreviousTerminal { get; }

        internal ActionRequestResult(string reason, ActionFact fact = null, ActionFact previousTerminal = null)
        {
            Reason = reason;
            Fact = fact;
            PreviousTerminal = previousTerminal;
        }
    }

    // 单角色单动作权威，不读设备/Spine/世界碰撞，也不直接写HP。
    public sealed class BasicActionRuntime
    {
        private readonly string actorId;
        private BasicAttackDefinition definition;
        private (double At, ActionFactKind Kind)[] nodes;
        private int nextNode;
        private double elapsed;
        private double timeCorrection;
        private Guid actionId;
        private string source;
        private bool moveReady;
        private bool attackReady;

        public bool IsActive => definition != null;
        public bool MoveReady => !IsActive || moveReady;
        public bool AttackReady => !IsActive || attackReady;
        public Guid ActionId => IsActive ? actionId : Guid.Empty;

        public BasicActionRuntime(string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId)) throw new ArgumentException("Actor identity required", nameof(actorId));
            this.actorId = actorId;
        }

        public ActionRequestResult Request(BasicAttackDefinition attack, string requestSource,
            ControlAuthority gate, bool paused = false)
        {
            if (attack == null) throw new ArgumentNullException(nameof(attack));
            if (gate == null) throw new ArgumentNullException(nameof(gate));
            if (string.IsNullOrWhiteSpace(requestSource)) throw new ArgumentException("Request source required", nameof(requestSource));
            if (paused) return new ActionRequestResult("paused");
            if (!gate.Allows(ControlChannel.Attack)) return new ActionRequestResult("attack-blocked");
            if (IsActive && !attackReady) return new ActionRequestResult("busy");
            // Ready允许一次新按下；显式终结旧身体动作，不遗失其已释放效果。
            var previousTerminal = IsActive ? Cancel("replaced-by-next-attack") : null;
            definition = attack;
            source = requestSource;
            actionId = Guid.NewGuid();
            elapsed = 0;
            timeCorrection = 0;
            nextNode = 0;
            moveReady = false;
            attackReady = false;
            // 两种Ready可互换先后；同一时刻固定Release→Move→Attack→Finish。
            nodes = new[] {
                (attack.ReleaseAt, ActionFactKind.Released),
                (attack.MoveReadyAt, ActionFactKind.MoveReady),
                (attack.AttackReadyAt, ActionFactKind.AttackReady),
                (attack.FinishAt, ActionFactKind.Finished)
            }.OrderBy(n => n.Item1).ThenBy(n => n.Item2).ToArray();
            return new ActionRequestResult(null, Fact(ActionFactKind.Accepted, 0), previousTerminal);
        }

        public IReadOnlyList<ActionFact> Advance(double delta, bool paused)
        {
            if (!BasicAttackDefinition.Finite(delta) || delta < 0)
                throw new ArgumentOutOfRangeException(nameof(delta));
            if (!IsActive || paused) return Array.Empty<ActionFact>();
            // 补偿累计舍入误差；不靠提前Release的任意epsilon修补帧边界。
            var correctedDelta = delta - timeCorrection;
            var sum = elapsed + correctedDelta;
            timeCorrection = (sum - elapsed) - correctedDelta;
            elapsed = Math.Min(definition.FinishAt, sum);
            if (elapsed == definition.FinishAt) timeCorrection = 0;
            var facts = new List<ActionFact>();
            while (nextNode < nodes.Length && elapsed >= nodes[nextNode].At)
            {
                var node = nodes[nextNode++];
                AttackRelease release = null;
                if (node.Kind == ActionFactKind.Released)
                    release = new AttackRelease(actionId, actorId, definition.Damage);
                if (node.Kind == ActionFactKind.MoveReady) moveReady = true;
                if (node.Kind == ActionFactKind.AttackReady) attackReady = true;
                facts.Add(Fact(node.Kind, node.At, release: release));
                if (node.Kind == ActionFactKind.Finished) definition = null;
            }
            return facts;
        }

        public ActionFact Cancel(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Cancellation cause required", nameof(reason));
            if (!IsActive) return null;
            var terminal = Fact(ActionFactKind.Cancelled, elapsed, reason);
            definition = null;
            return terminal;
        }

        private ActionFact Fact(ActionFactKind kind, double at, string reason = null, AttackRelease release = null)
            => new ActionFact(kind, actionId, source, at, reason, release);
    }
}
```

| 行 | 中文解释（用户全待审） |
|---:|---|
| 1 | 引入本文件直接使用的标准库类型；无Unity、Spine或第三方运行依赖。 |
| 2 | 引入本文件直接使用的标准库类型；无Unity、Spine或第三方运行依赖。 |
| 3 | 引入本文件直接使用的标准库类型；无Unity、Spine或第三方运行依赖。 |
| 5 | 候选命名空间隔离新实现，避免重新依赖旧CharacterRoot。 |
| 7 | 明确接受、释放、两个Ready、自然结束、取消六种事实；此处不广播事件。 |
| 9 | 事实是结果，不是任意广播；调用者按返回顺序处理。 |
| 10 | 定义具名职责：唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine。 |
| 12 | 公开只读状态/配置；调用方只能观察，不能通过此属性覆写状态权威。 |
| 13 | 公开只读状态/配置；调用方只能观察，不能通过此属性覆写状态权威。 |
| 14 | 公开只读状态/配置；调用方只能观察，不能通过此属性覆写状态权威。 |
| 15 | 公开只读状态/配置；调用方只能观察，不能通过此属性覆写状态权威。 |
| 16 | 公开只读状态/配置；调用方只能观察，不能通过此属性覆写状态权威。 |
| 17 | 公开只读状态/配置；调用方只能观察，不能通过此属性覆写状态权威。 |
| 19 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 20 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine。 |
| 22 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine。 |
| 23 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine。 |
| 24 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine。 |
| 25 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine。 |
| 26 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine。 |
| 27 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine。 |
| 31 | 定义具名职责：唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine。 |
| 33 | 提供当前模块推导的只读结果；具体实际资格还需组合外部控制/暂停状态。 |
| 34 | 公开只读状态/配置；调用方只能观察，不能通过此属性覆写状态权威。 |
| 35 | 公开只读状态/配置；调用方只能观察，不能通过此属性覆写状态权威。 |
| 36 | 公开只读状态/配置；调用方只能观察，不能通过此属性覆写状态权威。 |
| 38 | 新按下替换已AttackReady动作时，显式返回旧Cancel及原因；旧释放凭据不撤销。 |
| 40 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine。 |
| 41 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine。 |
| 42 | 新按下替换已AttackReady动作时，显式返回旧Cancel及原因；旧释放凭据不撤销。 |
| 46 | 单角色单动作权威，不读设备/Spine/世界碰撞，也不直接写HP。 |
| 47 | 定义具名职责：唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine。 |
| 49 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 50 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 51 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 52 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 53 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 54 | 使用补偿累计处理浮点误差，确保15个1/60秒能准确跨越0.25秒，不靠提前触发的epsilon。 |
| 55 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 56 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 57 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 58 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 60 | 提供当前模块推导的只读结果；具体实际资格还需组合外部控制/暂停状态。 |
| 61 | 提供当前模块推导的只读结果；具体实际资格还需组合外部控制/暂停状态。 |
| 62 | 提供当前模块推导的只读结果；具体实际资格还需组合外部控制/暂停状态。 |
| 63 | 提供当前模块推导的只读结果；具体实际资格还需组合外部控制/暂停状态。 |
| 65 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 67 | 拒绝或验证非法参数；要求失败发生在改变动作/HP之前。 |
| 68 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine。 |
| 71 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 72 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine。 |
| 74 | 拒绝或验证非法参数；要求失败发生在改变动作/HP之前。 |
| 75 | 拒绝或验证非法参数；要求失败发生在改变动作/HP之前。 |
| 76 | 拒绝或验证非法参数；要求失败发生在改变动作/HP之前。 |
| 77 | 暂停时明确拒绝请求，调用方须向Request和Advance传同一暂停权威。 |
| 78 | 读取独立控制权限；拒绝路径不创建动作或覆写当前动作。 |
| 79 | 未到AttackReady拒绝新请求，避免“显示可攻击却仍一直busy”的假Ready。 |
| 80 | Ready允许一次新按下；显式终结旧身体动作，不遗失其已释放效果。 |
| 81 | 新按下替换已AttackReady动作时，显式返回旧Cancel及原因；旧释放凭据不撤销。 |
| 82 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine。 |
| 83 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine。 |
| 84 | 仅在真正接受时创建动作身份；不依赖日志开关，不复用取消前的身份。 |
| 85 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine。 |
| 86 | 使用补偿累计处理浮点误差，确保15个1/60秒能准确跨越0.25秒，不靠提前触发的epsilon。 |
| 87 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine。 |
| 88 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine。 |
| 89 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine。 |
| 90 | 两种Ready可互换先后；同一时刻固定Release→Move→Attack→Finish。 |
| 91 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine。 |
| 92 | 建立四个独立时间节点并按时间、同刻事实顺序排列；Ready可交换先后。 |
| 93 | 建立四个独立时间节点并按时间、同刻事实顺序排列；Ready可交换先后。 |
| 94 | 建立四个独立时间节点并按时间、同刻事实顺序排列；Ready可交换先后。 |
| 95 | 建立四个独立时间节点并按时间、同刻事实顺序排列；Ready可交换先后。 |
| 96 | 建立四个独立时间节点并按时间、同刻事实顺序排列；Ready可交换先后。 |
| 97 | 返回明确接受事实或拒绝原因；调用方不能把发出请求当成已经释放。 |
| 100 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 102 | 执行本行列明的条件或遍历；当前职责是唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine，不跨写其他模块状态。 |
| 103 | 拒绝或验证非法参数；要求失败发生在改变动作/HP之前。 |
| 104 | 未运行或暂停时返回空事实列表，不消耗时间。 |
| 105 | 补偿累计舍入误差；不靠提前Release的任意epsilon修补帧边界。 |
| 106 | 使用补偿累计处理浮点误差，确保15个1/60秒能准确跨越0.25秒，不靠提前触发的epsilon。 |
| 107 | 使用补偿累计处理浮点误差，确保15个1/60秒能准确跨越0.25秒，不靠提前触发的epsilon。 |
| 108 | 使用补偿累计处理浮点误差，确保15个1/60秒能准确跨越0.25秒，不靠提前触发的epsilon。 |
| 109 | 把累计动作时间限制到Finish；大帧跨节点仍由后续循环逐个处理。 |
| 110 | 使用补偿累计处理浮点误差，确保15个1/60秒能准确跨越0.25秒，不靠提前触发的epsilon。 |
| 111 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine。 |
| 112 | 依序消费尚未产生的节点；先推进索引，防止同节点重复释放或终结。 |
| 114 | 依序消费尚未产生的节点；先推进索引，防止同节点重复释放或终结。 |
| 115 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine。 |
| 116 | 执行本行列明的条件或遍历；当前职责是唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine，不跨写其他模块状态。 |
| 117 | 仅Release节点创建一次效果凭据，动画回调没有创建或写HP的入口。 |
| 118 | 执行本行列明的条件或遍历；当前职责是唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine，不跨写其他模块状态。 |
| 119 | 执行本行列明的条件或遍历；当前职责是唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine，不跨写其他模块状态。 |
| 120 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine。 |
| 121 | 关闭当前身体动作的活跃状态；不改动已经返回给调用方的AttackRelease。 |
| 123 | 把本方法结果直接交给调用者；不是全局事件、动画或伤害的第二条权威路径。 |
| 126 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 128 | 拒绝或验证非法参数；要求失败发生在改变动作/HP之前。 |
| 129 | 执行本行列明的条件或遍历；当前职责是唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine，不跨写其他模块状态。 |
| 130 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：唯一接受动作、分配身份、推进补偿时钟及产生有序动作事实；不直接操作HP或Spine。 |
| 131 | 关闭当前身体动作的活跃状态；不改动已经返回给调用方的AttackRelease。 |
| 132 | 把本方法结果直接交给调用者；不是全局事件、动画或伤害的第二条权威路径。 |
| 135 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 136 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |

## Assets/OSSASTRA/CharacterCore/BasicAttackDefinition.cs
验证并保存不可变动作节点和伤害配置；没有本作平衡默认数值
SHA256: CE126E0F1A4A6E9CC65D32E04B2FB41CDB7E9305F97669A9D01D2BDB587CD5D1
```csharp
using System;

namespace Ossastra.CharacterCore
{
    // 全部配置不可变；本类没有游戏平衡默认值。
    public sealed class BasicAttackDefinition
    {
        public double ReleaseAt { get; }
        public double MoveReadyAt { get; }
        public double AttackReadyAt { get; }
        public double FinishAt { get; }
        public int Damage { get; }

        public BasicAttackDefinition(double release, double move, double ready, double finish, int damage)
        {
            if (!Finite(release) || !Finite(move) || !Finite(ready) || !Finite(finish) ||
                release < 0 || move < release || ready < release || finish < move || finish < ready)
                throw new ArgumentOutOfRangeException(nameof(release), "Invalid action node ordering or time");
            if (damage <= 0) throw new ArgumentOutOfRangeException(nameof(damage));
            ReleaseAt = release;
            MoveReadyAt = move;
            AttackReadyAt = ready;
            FinishAt = finish;
            Damage = damage;
        }

        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
```

| 行 | 中文解释（用户全待审） |
|---:|---|
| 1 | 引入本文件直接使用的标准库类型；无Unity、Spine或第三方运行依赖。 |
| 3 | 候选命名空间隔离新实现，避免重新依赖旧CharacterRoot。 |
| 5 | 全部配置不可变；本类没有游戏平衡默认值。 |
| 6 | 定义具名职责：验证并保存不可变动作节点和伤害配置；没有本作平衡默认数值。 |
| 8 | 公开只读状态/配置；调用方只能观察，不能通过此属性覆写状态权威。 |
| 9 | 公开只读状态/配置；调用方只能观察，不能通过此属性覆写状态权威。 |
| 10 | 公开只读状态/配置；调用方只能观察，不能通过此属性覆写状态权威。 |
| 11 | 公开只读状态/配置；调用方只能观察，不能通过此属性覆写状态权威。 |
| 12 | 公开只读状态/配置；调用方只能观察，不能通过此属性覆写状态权威。 |
| 14 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 16 | 执行本行列明的条件或遍历；当前职责是验证并保存不可变动作节点和伤害配置；没有本作平衡默认数值，不跨写其他模块状态。 |
| 17 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：验证并保存不可变动作节点和伤害配置；没有本作平衡默认数值。 |
| 18 | 拒绝或验证非法参数；要求失败发生在改变动作/HP之前。 |
| 19 | 拒绝或验证非法参数；要求失败发生在改变动作/HP之前。 |
| 20 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：验证并保存不可变动作节点和伤害配置；没有本作平衡默认数值。 |
| 21 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：验证并保存不可变动作节点和伤害配置；没有本作平衡默认数值。 |
| 22 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：验证并保存不可变动作节点和伤害配置；没有本作平衡默认数值。 |
| 23 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：验证并保存不可变动作节点和伤害配置；没有本作平衡默认数值。 |
| 24 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：验证并保存不可变动作节点和伤害配置；没有本作平衡默认数值。 |
| 27 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |

## Assets/OSSASTRA/CharacterCore/ControlAuthority.cs
按owner对象身份维护移动/普攻禁令，移除自己的禁令不能解除别人的限制
SHA256: 15B1692C37C998AB27D049AAC324B02FC50BE1EDD1E2104A49314D0E4AFBB6CA
```csharp
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Ossastra.CharacterCore
{
    [Flags]
    public enum ControlChannel { None = 0, Move = 1, Attack = 2 }

    // 每个调用者只拥有自己的禁令；没有一个调用者可以清空别人的限制。
    public sealed class ControlAuthority
    {
        private readonly Dictionary<object, ControlChannel> blocks =
            new Dictionary<object, ControlChannel>(new OwnerComparer());

        public void Block(object owner, ControlChannel channels)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            Validate(channels);
            blocks[owner] = channels;
        }

        public void Release(object owner)
        {
            if (owner != null) blocks.Remove(owner);
        }

        public bool Allows(ControlChannel channels)
        {
            Validate(channels);
            foreach (var block in blocks.Values)
                if ((block & channels) != 0) return false;
            return true;
        }

        private static void Validate(ControlChannel channels)
        {
            if ((channels & ~(ControlChannel.Move | ControlChannel.Attack)) != 0)
                throw new ArgumentOutOfRangeException(nameof(channels));
        }

        // owner按对象身份区分，避免两个值相同的owner互相覆盖。
        private sealed class OwnerComparer : IEqualityComparer<object>
        {
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
```

| 行 | 中文解释（用户全待审） |
|---:|---|
| 1 | 引入本文件直接使用的标准库类型；无Unity、Spine或第三方运行依赖。 |
| 2 | 引入本文件直接使用的标准库类型；无Unity、Spine或第三方运行依赖。 |
| 3 | 引入本文件直接使用的标准库类型；无Unity、Spine或第三方运行依赖。 |
| 5 | 候选命名空间隔离新实现，避免重新依赖旧CharacterRoot。 |
| 7 | 使通道可按位组合，Move与Attack仍可独立限制。 |
| 8 | 定义None/Move/Attack的位值；没有把所有资格压成一个canAct。 |
| 10 | 每个调用者只拥有自己的禁令；没有一个调用者可以清空别人的限制。 |
| 11 | 定义具名职责：按owner对象身份维护移动/普攻禁令，移除自己的禁令不能解除别人的限制。 |
| 13 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 14 | 使用owner对象引用身份而不是值相等，防止两个独立owner互相覆盖。 |
| 16 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 18 | 拒绝或验证非法参数；要求失败发生在改变动作/HP之前。 |
| 19 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：按owner对象身份维护移动/普攻禁令，移除自己的禁令不能解除别人的限制。 |
| 20 | 只写入或替换这个owner自己的通道禁令。 |
| 23 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 25 | 只移除指定owner，不清空其他owner状态。 |
| 28 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 30 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：按owner对象身份维护移动/普攻禁令，移除自己的禁令不能解除别人的限制。 |
| 31 | 执行本行列明的条件或遍历；当前职责是按owner对象身份维护移动/普攻禁令，移除自己的禁令不能解除别人的限制，不跨写其他模块状态。 |
| 32 | 任一owner阻挡所查询通道就拒绝；没有最后写入覆盖所有限制。 |
| 33 | 把本方法结果直接交给调用者；不是全局事件、动画或伤害的第二条权威路径。 |
| 36 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 38 | 执行本行列明的条件或遍历；当前职责是按owner对象身份维护移动/普攻禁令，移除自己的禁令不能解除别人的限制，不跨写其他模块状态。 |
| 39 | 拒绝或验证非法参数；要求失败发生在改变动作/HP之前。 |
| 42 | owner按对象身份区分，避免两个值相同的owner互相覆盖。 |
| 43 | 定义具名职责：按owner对象身份维护移动/普攻禁令，移除自己的禁令不能解除别人的限制。 |
| 45 | 使用owner对象引用身份而不是值相等，防止两个独立owner互相覆盖。 |
| 46 | 使用owner对象引用身份而不是值相等，防止两个独立owner互相覆盖。 |

## Assets/OSSASTRA/CharacterCore/Health.cs
目标HP唯一写入者，防止出现另一份真实HP或负数HP
SHA256: 4EFA12B068ED93D0156A0F7B3D2BF08D80DDE3067DED471501B29088FC8DD707
```csharp
using System;

namespace Ossastra.CharacterCore
{
    // HP只有本对象能够写；测试目标将来引用同一个Health，不能维护另一份真实HP。
    public sealed class Health
    {
        public string Id { get; }
        public int Maximum { get; }
        public int Current { get; private set; }

        public Health(string id, int maximum)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Target identity required", nameof(id));
            if (maximum <= 0) throw new ArgumentOutOfRangeException(nameof(maximum));
            Id = id;
            Maximum = maximum;
            Current = maximum;
        }

        internal void ApplyDamage(int damage)
        {
            if (damage <= 0) throw new ArgumentOutOfRangeException(nameof(damage));
            Current = Math.Max(0, Current - damage);
        }
    }
}
```

| 行 | 中文解释（用户全待审） |
|---:|---|
| 1 | 引入本文件直接使用的标准库类型；无Unity、Spine或第三方运行依赖。 |
| 3 | 候选命名空间隔离新实现，避免重新依赖旧CharacterRoot。 |
| 5 | HP只有本对象能够写；测试目标将来引用同一个Health，不能维护另一份真实HP。 |
| 6 | 定义具名职责：目标HP唯一写入者，防止出现另一份真实HP或负数HP。 |
| 8 | 公开只读状态/配置；调用方只能观察，不能通过此属性覆写状态权威。 |
| 9 | 公开只读状态/配置；调用方只能观察，不能通过此属性覆写状态权威。 |
| 10 | 公开只读状态/配置；调用方只能观察，不能通过此属性覆写状态权威。 |
| 12 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 14 | 拒绝或验证非法参数；要求失败发生在改变动作/HP之前。 |
| 15 | 拒绝或验证非法参数；要求失败发生在改变动作/HP之前。 |
| 16 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：目标HP唯一写入者，防止出现另一份真实HP或负数HP。 |
| 17 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：目标HP唯一写入者，防止出现另一份真实HP或负数HP。 |
| 18 | 按当前方法顺序执行本行赋值/构造/参数续行；本文件职责：目标HP唯一写入者，防止出现另一份真实HP或负数HP。 |
| 21 | 声明本行所示字段、构造或方法；访问级别限制写入范围，完整调用关系见权威表。 |
| 23 | 拒绝或验证非法参数；要求失败发生在改变动作/HP之前。 |
| 24 | Health内部实际写HP，扣除正伤害后下限为0。 |

## Candidates/M01/Tests/Program.cs
无第三方包的真实合同测试宿主。最终29项，不把.NET结果当作Unity实机验收。
SHA256: 0CB49284D7C025F6309DDB289B86AA6B518BFEF22475D3E98CBE908BDE154575
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.CharacterCore;

internal static class Program
{
    private static int failures;
    private static int total;
    private static BasicAttackDefinition Attack(double release = .25, double move = .4,
        double ready = .6, double finish = .8, int damage = 12)
        => new BasicAttackDefinition(release, move, ready, finish, damage);

    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }

    private static void Test(string name, Action body)
    {
        total++;
        try { body(); Console.WriteLine("PASS " + name); }
        catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + ": " + e.Message); }
    }

    private static BasicActionRuntime Started()
    {
        var runtime = new BasicActionRuntime("player");
        Check(runtime.Request(Attack(), "player-input", new ControlAuthority()).Accepted,
            "A legal request must be accepted");
        return runtime;
    }

    private static AttackRelease Released()
        => Started().Advance(.25, false).Single(f => f.Kind == ActionFactKind.Released).Release;

    private static int Main()
    {
        Test("owner release cannot unlock another owner's block", () => {
            var gate = new ControlAuthority(); var menu = new object(); var stun = new object();
            gate.Block(menu, ControlChannel.Attack); gate.Block(stun, ControlChannel.Attack);
            gate.Release(menu);
            Check(!gate.Allows(ControlChannel.Attack), "Second owner's attack block lost");
            gate.Release(stun); Check(gate.Allows(ControlChannel.Attack), "Block never released");
        });
        Test("move and attack permissions remain independent", () => {
            var gate = new ControlAuthority(); gate.Block(new object(), ControlChannel.Move);
            Check(!gate.Allows(ControlChannel.Move) && gate.Allows(ControlChannel.Attack), "Channels conflated");
        });
        Test("owner changes only its own channels", () => {
            var gate = new ControlAuthority(); var owner = new object();
            gate.Block(owner, ControlChannel.Attack); gate.Block(owner, ControlChannel.Move);
            Check(gate.Allows(ControlChannel.Attack) && !gate.Allows(ControlChannel.Move), "Owner replacement failed");
        });
        Test("held input emits only a rising edge", () => {
            var input = new AttackIntent();
            Check(input.Observe(true) && !input.Observe(true), "Held repeated attack");
            Check(!input.Observe(false) && input.Observe(true), "Next press lost");
        });
        Test("focus reset requires release before a fresh press", () => {
            var input = new AttackIntent(); input.Observe(true); input.SuppressUntilRelease();
            Check(!input.Observe(true) && !input.Observe(true), "Stale held fired after focus reset");
            input.Observe(false); Check(input.Observe(true), "Fresh press not rearmed");
        });
        Test("request acceptance creates identity without releasing", () => {
            var runtime = new BasicActionRuntime("player");
            var result = runtime.Request(Attack(), "player-input", new ControlAuthority());
            Check(result.Accepted && result.Fact.Kind == ActionFactKind.Accepted &&
                result.Fact.ActionId != Guid.Empty && runtime.IsActive && !runtime.MoveReady,
                "Request/accept/release identity collapsed");
            Check(runtime.Advance(.24, false).Count == 0, "Attack released before its node");
        });
        Test("blocked and busy requests create no new action", () => {
            var runtime = new BasicActionRuntime("player"); var gate = new ControlAuthority();
            var owner = new object(); gate.Block(owner, ControlChannel.Attack);
            Check(runtime.Request(Attack(), "player-input", gate).Reason == "attack-blocked" && !runtime.IsActive,
                "Blocked input accepted");
            gate.Release(owner); var first = runtime.Request(Attack(), "player-input", gate);
            var denied = runtime.Request(Attack(), "player-input", gate);
            Check(!denied.Accepted && denied.Reason == "busy" && runtime.ActionId == first.Fact.ActionId,
                "Busy request replaced accepted action");
        });
        Test("large delta emits ordered nodes exactly once", () => {
            var runtime = Started(); var facts = runtime.Advance(2, false);
            Check(string.Join(",", facts.Select(f => f.Kind)) == "Released,MoveReady,AttackReady,Finished",
                "Crossed nodes missing, repeated or unordered");
            Check(facts[0].At == .25 && facts[3].At == .8, "Node stamped with overshot frame time");
            Check(runtime.Advance(2, false).Count == 0 && !runtime.IsActive, "Finished action ran again");
        });
        Test("paused clock does not consume elapsed time", () => {
            var runtime = Started(); Check(runtime.Advance(100, true).Count == 0, "Pause emitted nodes");
            Check(runtime.Advance(.24, false).Count == 0, "Paused time accumulated");
            Check(runtime.Advance(.01, false).Single().Kind == ActionFactKind.Released, "Unpause lost release");
        });
        Test("move and attack readiness remain separate from finish", () => {
            var runtime = Started(); runtime.Advance(.25, false);
            Check(!runtime.MoveReady && !runtime.AttackReady, "Release silently unlocked both permissions");
            runtime.Advance(.15, false); Check(runtime.MoveReady && !runtime.AttackReady, "Move node lost");
            runtime.Advance(.2, false); Check(runtime.AttackReady && runtime.IsActive, "Ready collapsed into finish");
        });
        Test("zero-time nodes wait for tick then follow stable tie order", () => {
            var runtime = new BasicActionRuntime("player");
            Check(runtime.Request(Attack(0, 0, 0, 0), "system", new ControlAuthority()).Accepted, "Zero-time request rejected");
            Check(string.Join(",", runtime.Advance(0, false).Select(f => f.Kind)) ==
                "Released,MoveReady,AttackReady,Finished", "Zero-time nodes wrong");
        });
        Test("invalid time never mutates a live action", () => {
            var runtime = Started(); Throws<ArgumentOutOfRangeException>(() => runtime.Advance(double.NaN, false));
            Throws<ArgumentOutOfRangeException>(() => runtime.Advance(-1, false));
            Throws<ArgumentOutOfRangeException>(() => runtime.Advance(double.PositiveInfinity, false));
            Check(runtime.Advance(.24, false).Count == 0, "Invalid delta changed clock");
        });
        Test("invalid configuration rejected before use", () => {
            Throws<ArgumentOutOfRangeException>(() => Attack(-.1));
            Throws<ArgumentOutOfRangeException>(() => Attack(.3, .2));
            Throws<ArgumentOutOfRangeException>(() => Attack(.2, .3, .4, .1));
            Throws<ArgumentOutOfRangeException>(() => Attack(damage: 0));
            Throws<ArgumentOutOfRangeException>(() => Attack(finish: double.NaN));
        });
        Test("pre-release cancel has one terminal and no damage credential", () => {
            var runtime = Started(); runtime.Advance(.1, false);
            var terminal = runtime.Cancel("focus-lost");
            Check(terminal != null && terminal.Kind == ActionFactKind.Cancelled && terminal.Reason == "focus-lost",
                "Cancellation fact or cause missing");
            Check(runtime.Cancel("disabled") == null && runtime.Advance(1, false).Count == 0,
                "Cancelled action terminated twice or released");
        });
        Test("empty cancel cause cannot erase an action", () => {
            var runtime = Started(); Throws<ArgumentException>(() => runtime.Cancel(" "));
            Check(runtime.IsActive, "Invalid cancellation mutated action");
        });
        Test("cancel after release preserves committed damage and never releases again", () => {
            var runtime = Started(); var release = runtime.Advance(.25, false).Single().Release;
            var target = new Health("enemy", 100); runtime.Cancel("disabled");
            var outcome = release.Resolve(new[] { target }).Single();
            Check(outcome.HpBefore == 100 && outcome.HpAfter == 88 && target.Current == 88,
                "Body cancellation erased committed effect");
            Check(runtime.Advance(10, false).Count == 0, "Cancelled action emitted another release");
        });
        Test("re-enable simulation cannot reuse cancelled action identity", () => {
            var runtime = Started(); var oldId = runtime.ActionId; runtime.Cancel("disabled");
            var next = runtime.Request(Attack(), "player-input", new ControlAuthority());
            Check(next.Accepted && next.Fact.ActionId != oldId, "New lifecycle reused old identity");
        });
        Test("duplicate collider candidates damage one target only once", () => {
            var target = new Health("enemy", 100);
            var hits = Released().Resolve(new[] { target, target });
            Check(target.Current == 88 && hits.Count(h => h.Reason == HitReason.Applied) == 1 &&
                hits.Count(h => h.Reason == HitReason.Duplicate) == 1, "Duplicate target damaged twice");
        });
        Test("one release may hit two independently identified targets", () => {
            var a = new Health("enemy-a", 100); var b = new Health("enemy-b", 100);
            Released().Resolve(new[] { a, b }); Check(a.Current == 88 && b.Current == 88, "Targets conflated");
        });
        Test("empty swing closes the release without later phantom hits", () => {
            var release = Released(); var target = new Health("enemy", 100);
            Check(release.Resolve(Array.Empty<Health>()).Count == 0, "Empty swing invented target");
            Throws<InvalidOperationException>(() => release.Resolve(new[] { target }));
            Check(target.Current == 100, "Closed empty swing hit later");
        });
        Test("same release cannot be resolved for new targets a second time", () => {
            var release = Released(); var a = new Health("a", 100); var b = new Health("b", 100);
            release.Resolve(new[] { a }); Throws<InvalidOperationException>(() => release.Resolve(new[] { b }));
            Check(a.Current == 88 && b.Current == 100, "Release window reopened");
        });
        Test("different actions can damage the same target", () => {
            var target = new Health("enemy", 100); Released().Resolve(new[] { target });
            Released().Resolve(new[] { target }); Check(target.Current == 76, "Target ledger leaked across actions");
        });
        Test("self and dead target do not receive damage", () => {
            var self = new Health("player", 100); var enemy = new Health("enemy", 5);
            Released().Resolve(new[] { enemy });
            var hits = Released().Resolve(new[] { self, enemy });
            Check(self.Current == 100 && enemy.Current == 0 && hits[0].Reason == HitReason.Self &&
                hits[1].Reason == HitReason.Dead, "Self/dead filtering failed or HP became negative");
        });
        Test("null target batch is validated before any HP changes", () => {
            var release = Released(); var target = new Health("enemy", 100);
            Throws<ArgumentException>(() => release.Resolve(new[] { target, null }));
            Check(target.Current == 100, "Invalid batch partially damaged target");
            release.Resolve(new[] { target }); Check(target.Current == 88, "Invalid batch consumed release");
        });
        Test("invalid identity and health cannot enter authority", () => {
            Throws<ArgumentException>(() => new Health(" ", 100));
            Throws<ArgumentOutOfRangeException>(() => new Health("enemy", 0));
            Throws<ArgumentException>(() => new BasicActionRuntime(" "));
        });
        Test("source identity follows accepted action into all facts and hit result", () => {
            var runtime = new BasicActionRuntime("player");
            var accepted = runtime.Request(Attack(), "player-input", new ControlAuthority());
            var facts = runtime.Advance(1, false); var release = facts[0].Release;
            var hit = release.Resolve(new[] { new Health("enemy", 100) }).Single();
            Check(facts.All(f => f.ActionId == accepted.Fact.ActionId && f.Source == "player-input") &&
                hit.ActionId == accepted.Fact.ActionId && hit.ActorId == "player" && hit.TargetId == "enemy",
                "Identity/source not preserved through result");
        });
        Test("sixty-Hz frame partition releases on frame fifteen, not sixteen", () => {
            var runtime = Started();
            for (int frame = 1; frame < 15; frame++)
                Check(runtime.Advance(1.0 / 60, false).Count == 0, "Release happened before frame fifteen");
            Check(runtime.Advance(1.0 / 60, false).Single().Kind == ActionFactKind.Released,
                "Floating-point accumulation delayed the release by one frame");
        });
        Test("AttackReady actually accepts a new press before old Finish", () => {
            var runtime = Started(); var oldId = runtime.ActionId;
            runtime.Advance(.6, false);
            var next = runtime.Request(Attack(), "player-input", new ControlAuthority());
            Check(next.Accepted && next.Fact.ActionId != oldId,
                "AttackReady advertised readiness but still rejected the next press");
            var terminal = next.PreviousTerminal;
            Check(terminal != null && terminal.ActionId == oldId && terminal.Kind == ActionFactKind.Cancelled &&
                terminal.Reason == "replaced-by-next-attack", "Prior action lost its named terminal");
            Check(runtime.Advance(.25, false).Single().ActionId == next.Fact.ActionId,
                "Previous runtime leaked into new action");
        });
        Test("pause rejects new requests without consuming the next valid press", () => {
            var runtime = new BasicActionRuntime("player"); var gate = new ControlAuthority();
            var denied = runtime.Request(Attack(), "player-input", gate, true);
            Check(!denied.Accepted && denied.Reason == "paused" && !runtime.IsActive,
                "Paused request created an active action");
            Check(runtime.Request(Attack(), "player-input", gate, false).Accepted,
                "Paused rejection prevented a later legal request");
        });
        Console.WriteLine($"RESULT {total - failures}/{total} passed");
        return failures == 0 ? 0 : 1;
    }
}
```
| 行 | 中文解释（用户全部待审） |
|---:|---|
| 1 | 引入标准库或真实核心命名空间，不是模拟核心实现。 |
| 2 | 引入标准库或真实核心命名空间，不是模拟核心实现。 |
| 3 | 引入标准库或真实核心命名空间，不是模拟核心实现。 |
| 4 | 引入标准库或真实核心命名空间，不是模拟核心实现。 |
| 6 | 测试“测试宿主”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 8 | 测试“测试宿主”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 9 | 测试“测试宿主”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 10 | 测试“测试宿主”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 11 | 测试“测试宿主”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 12 | 测试“测试宿主”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 14 | 断言“测试宿主”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 15 | 测试“测试宿主”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 17 | 验证非法操作被拒绝；具体异常类型由本行列明，不应部分改变HP/动作。 |
| 19 | 测试“测试宿主”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 20 | 测试“测试宿主”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 23 | 测试“测试宿主”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 25 | 记录测试通过/失败并用退出码交付结果；不是生产日志权威。 |
| 26 | 记录测试通过/失败并用退出码交付结果；不是生产日志权威。 |
| 27 | 记录测试通过/失败并用退出码交付结果；不是生产日志权威。 |
| 30 | 测试“测试宿主”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 32 | 为“测试宿主”创建独立的真实核心对象，避免测试间共享状态。 |
| 33 | 断言“测试宿主”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 34 | 测试“测试宿主”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 35 | 测试“测试宿主”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 38 | 测试“测试宿主”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 39 | 测试“测试宿主”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 41 | 测试“测试宿主”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 43 | 建立独立反例：owner release cannot unlock another owner's block。 |
| 44 | 为“owner release cannot unlock another owner's block”创建独立的真实核心对象，避免测试间共享状态。 |
| 45 | 测试“owner release cannot unlock another owner's block”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 46 | 测试“owner release cannot unlock another owner's block”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 47 | 断言“owner release cannot unlock another owner's block”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 48 | 断言“owner release cannot unlock another owner's block”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 49 | 测试“owner release cannot unlock another owner's block”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 50 | 建立独立反例：move and attack permissions remain independent。 |
| 51 | 为“move and attack permissions remain independent”创建独立的真实核心对象，避免测试间共享状态。 |
| 52 | 断言“move and attack permissions remain independent”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 53 | 测试“move and attack permissions remain independent”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 54 | 建立独立反例：owner changes only its own channels。 |
| 55 | 为“owner changes only its own channels”创建独立的真实核心对象，避免测试间共享状态。 |
| 56 | 测试“owner changes only its own channels”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 57 | 断言“owner changes only its own channels”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 58 | 测试“owner changes only its own channels”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 59 | 建立独立反例：held input emits only a rising edge。 |
| 60 | 为“held input emits only a rising edge”创建独立的真实核心对象，避免测试间共享状态。 |
| 61 | 断言“held input emits only a rising edge”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 62 | 断言“held input emits only a rising edge”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 63 | 测试“held input emits only a rising edge”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 64 | 建立独立反例：focus reset requires release before a fresh press。 |
| 65 | 为“focus reset requires release before a fresh press”创建独立的真实核心对象，避免测试间共享状态。 |
| 66 | 断言“focus reset requires release before a fresh press”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 67 | 断言“focus reset requires release before a fresh press”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 68 | 测试“focus reset requires release before a fresh press”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 69 | 建立独立反例：request acceptance creates identity without releasing。 |
| 70 | 为“request acceptance creates identity without releasing”创建独立的真实核心对象，避免测试间共享状态。 |
| 71 | 为“request acceptance creates identity without releasing”创建独立的真实核心对象，避免测试间共享状态。 |
| 72 | 断言“request acceptance creates identity without releasing”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 73 | 测试“request acceptance creates identity without releasing”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 74 | 测试“request acceptance creates identity without releasing”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 75 | 断言“request acceptance creates identity without releasing”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 76 | 测试“request acceptance creates identity without releasing”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 77 | 建立独立反例：blocked and busy requests create no new action。 |
| 78 | 为“blocked and busy requests create no new action”创建独立的真实核心对象，避免测试间共享状态。 |
| 79 | 测试“blocked and busy requests create no new action”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 80 | 断言“blocked and busy requests create no new action”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 81 | 测试“blocked and busy requests create no new action”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 82 | 测试“blocked and busy requests create no new action”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 83 | 测试“blocked and busy requests create no new action”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 84 | 断言“blocked and busy requests create no new action”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 85 | 测试“blocked and busy requests create no new action”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 86 | 测试“blocked and busy requests create no new action”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 87 | 建立独立反例：large delta emits ordered nodes exactly once。 |
| 88 | 测试“large delta emits ordered nodes exactly once”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 89 | 断言“large delta emits ordered nodes exactly once”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 90 | 测试“large delta emits ordered nodes exactly once”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 91 | 断言“large delta emits ordered nodes exactly once”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 92 | 断言“large delta emits ordered nodes exactly once”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 93 | 测试“large delta emits ordered nodes exactly once”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 94 | 建立独立反例：paused clock does not consume elapsed time。 |
| 95 | 断言“paused clock does not consume elapsed time”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 96 | 断言“paused clock does not consume elapsed time”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 97 | 断言“paused clock does not consume elapsed time”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 98 | 测试“paused clock does not consume elapsed time”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 99 | 建立独立反例：move and attack readiness remain separate from finish。 |
| 100 | 测试“move and attack readiness remain separate from finish”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 101 | 断言“move and attack readiness remain separate from finish”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 102 | 断言“move and attack readiness remain separate from finish”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 103 | 断言“move and attack readiness remain separate from finish”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 104 | 测试“move and attack readiness remain separate from finish”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 105 | 建立独立反例：zero-time nodes wait for tick then follow stable tie order。 |
| 106 | 为“zero-time nodes wait for tick then follow stable tie order”创建独立的真实核心对象，避免测试间共享状态。 |
| 107 | 断言“zero-time nodes wait for tick then follow stable tie order”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 108 | 断言“zero-time nodes wait for tick then follow stable tie order”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 109 | 测试“zero-time nodes wait for tick then follow stable tie order”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 110 | 测试“zero-time nodes wait for tick then follow stable tie order”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 111 | 建立独立反例：invalid time never mutates a live action。 |
| 112 | 验证非法操作被拒绝；具体异常类型由本行列明，不应部分改变HP/动作。 |
| 113 | 验证非法操作被拒绝；具体异常类型由本行列明，不应部分改变HP/动作。 |
| 114 | 验证非法操作被拒绝；具体异常类型由本行列明，不应部分改变HP/动作。 |
| 115 | 断言“invalid time never mutates a live action”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 116 | 测试“invalid time never mutates a live action”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 117 | 建立独立反例：invalid configuration rejected before use。 |
| 118 | 验证非法操作被拒绝；具体异常类型由本行列明，不应部分改变HP/动作。 |
| 119 | 验证非法操作被拒绝；具体异常类型由本行列明，不应部分改变HP/动作。 |
| 120 | 验证非法操作被拒绝；具体异常类型由本行列明，不应部分改变HP/动作。 |
| 121 | 验证非法操作被拒绝；具体异常类型由本行列明，不应部分改变HP/动作。 |
| 122 | 验证非法操作被拒绝；具体异常类型由本行列明，不应部分改变HP/动作。 |
| 123 | 测试“invalid configuration rejected before use”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 124 | 建立独立反例：pre-release cancel has one terminal and no damage credential。 |
| 125 | 测试“pre-release cancel has one terminal and no damage credential”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 126 | 测试“pre-release cancel has one terminal and no damage credential”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 127 | 断言“pre-release cancel has one terminal and no damage credential”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 128 | 测试“pre-release cancel has one terminal and no damage credential”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 129 | 断言“pre-release cancel has one terminal and no damage credential”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 130 | 测试“pre-release cancel has one terminal and no damage credential”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 131 | 测试“pre-release cancel has one terminal and no damage credential”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 132 | 建立独立反例：empty cancel cause cannot erase an action。 |
| 133 | 验证非法操作被拒绝；具体异常类型由本行列明，不应部分改变HP/动作。 |
| 134 | 断言“empty cancel cause cannot erase an action”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 135 | 测试“empty cancel cause cannot erase an action”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 136 | 建立独立反例：cancel after release preserves committed damage and never releases again。 |
| 137 | 测试“cancel after release preserves committed damage and never releases again”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 138 | 为“cancel after release preserves committed damage and never releases again”创建独立的真实核心对象，避免测试间共享状态。 |
| 139 | 测试“cancel after release preserves committed damage and never releases again”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 140 | 断言“cancel after release preserves committed damage and never releases again”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 141 | 测试“cancel after release preserves committed damage and never releases again”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 142 | 断言“cancel after release preserves committed damage and never releases again”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 143 | 测试“cancel after release preserves committed damage and never releases again”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 144 | 建立独立反例：re-enable simulation cannot reuse cancelled action identity。 |
| 145 | 测试“re-enable simulation cannot reuse cancelled action identity”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 146 | 为“re-enable simulation cannot reuse cancelled action identity”创建独立的真实核心对象，避免测试间共享状态。 |
| 147 | 断言“re-enable simulation cannot reuse cancelled action identity”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 148 | 测试“re-enable simulation cannot reuse cancelled action identity”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 149 | 建立独立反例：duplicate collider candidates damage one target only once。 |
| 150 | 为“duplicate collider candidates damage one target only once”创建独立的真实核心对象，避免测试间共享状态。 |
| 151 | 测试“duplicate collider candidates damage one target only once”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 152 | 断言“duplicate collider candidates damage one target only once”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 153 | 测试“duplicate collider candidates damage one target only once”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 154 | 测试“duplicate collider candidates damage one target only once”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 155 | 建立独立反例：one release may hit two independently identified targets。 |
| 156 | 为“one release may hit two independently identified targets”创建独立的真实核心对象，避免测试间共享状态。 |
| 157 | 断言“one release may hit two independently identified targets”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 158 | 测试“one release may hit two independently identified targets”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 159 | 建立独立反例：empty swing closes the release without later phantom hits。 |
| 160 | 为“empty swing closes the release without later phantom hits”创建独立的真实核心对象，避免测试间共享状态。 |
| 161 | 断言“empty swing closes the release without later phantom hits”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 162 | 验证非法操作被拒绝；具体异常类型由本行列明，不应部分改变HP/动作。 |
| 163 | 断言“empty swing closes the release without later phantom hits”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 164 | 测试“empty swing closes the release without later phantom hits”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 165 | 建立独立反例：same release cannot be resolved for new targets a second time。 |
| 166 | 为“same release cannot be resolved for new targets a second time”创建独立的真实核心对象，避免测试间共享状态。 |
| 167 | 验证非法操作被拒绝；具体异常类型由本行列明，不应部分改变HP/动作。 |
| 168 | 断言“same release cannot be resolved for new targets a second time”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 169 | 测试“same release cannot be resolved for new targets a second time”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 170 | 建立独立反例：different actions can damage the same target。 |
| 171 | 为“different actions can damage the same target”创建独立的真实核心对象，避免测试间共享状态。 |
| 172 | 断言“different actions can damage the same target”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 173 | 测试“different actions can damage the same target”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 174 | 建立独立反例：self and dead target do not receive damage。 |
| 175 | 为“self and dead target do not receive damage”创建独立的真实核心对象，避免测试间共享状态。 |
| 176 | 测试“self and dead target do not receive damage”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 177 | 测试“self and dead target do not receive damage”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 178 | 断言“self and dead target do not receive damage”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 179 | 测试“self and dead target do not receive damage”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 180 | 测试“self and dead target do not receive damage”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 181 | 建立独立反例：null target batch is validated before any HP changes。 |
| 182 | 为“null target batch is validated before any HP changes”创建独立的真实核心对象，避免测试间共享状态。 |
| 183 | 验证非法操作被拒绝；具体异常类型由本行列明，不应部分改变HP/动作。 |
| 184 | 断言“null target batch is validated before any HP changes”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 185 | 断言“null target batch is validated before any HP changes”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 186 | 测试“null target batch is validated before any HP changes”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 187 | 建立独立反例：invalid identity and health cannot enter authority。 |
| 188 | 验证非法操作被拒绝；具体异常类型由本行列明，不应部分改变HP/动作。 |
| 189 | 验证非法操作被拒绝；具体异常类型由本行列明，不应部分改变HP/动作。 |
| 190 | 验证非法操作被拒绝；具体异常类型由本行列明，不应部分改变HP/动作。 |
| 191 | 测试“invalid identity and health cannot enter authority”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 192 | 建立独立反例：source identity follows accepted action into all facts and hit result。 |
| 193 | 为“source identity follows accepted action into all facts and hit result”创建独立的真实核心对象，避免测试间共享状态。 |
| 194 | 为“source identity follows accepted action into all facts and hit result”创建独立的真实核心对象，避免测试间共享状态。 |
| 195 | 测试“source identity follows accepted action into all facts and hit result”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 196 | 为“source identity follows accepted action into all facts and hit result”创建独立的真实核心对象，避免测试间共享状态。 |
| 197 | 断言“source identity follows accepted action into all facts and hit result”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 198 | 测试“source identity follows accepted action into all facts and hit result”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 199 | 测试“source identity follows accepted action into all facts and hit result”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 200 | 测试“source identity follows accepted action into all facts and hit result”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 201 | 建立独立反例：sixty-Hz frame partition releases on frame fifteen, not sixteen。 |
| 202 | 测试“sixty-Hz frame partition releases on frame fifteen, not sixteen”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 203 | 测试“sixty-Hz frame partition releases on frame fifteen, not sixteen”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 204 | 断言“sixty-Hz frame partition releases on frame fifteen, not sixteen”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 205 | 断言“sixty-Hz frame partition releases on frame fifteen, not sixteen”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 206 | 测试“sixty-Hz frame partition releases on frame fifteen, not sixteen”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 207 | 测试“sixty-Hz frame partition releases on frame fifteen, not sixteen”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 208 | 建立独立反例：AttackReady actually accepts a new press before old Finish。 |
| 209 | 测试“AttackReady actually accepts a new press before old Finish”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 210 | 测试“AttackReady actually accepts a new press before old Finish”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 211 | 为“AttackReady actually accepts a new press before old Finish”创建独立的真实核心对象，避免测试间共享状态。 |
| 212 | 断言“AttackReady actually accepts a new press before old Finish”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 213 | 测试“AttackReady actually accepts a new press before old Finish”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 214 | 测试“AttackReady actually accepts a new press before old Finish”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 215 | 断言“AttackReady actually accepts a new press before old Finish”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 216 | 测试“AttackReady actually accepts a new press before old Finish”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 217 | 断言“AttackReady actually accepts a new press before old Finish”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 218 | 测试“AttackReady actually accepts a new press before old Finish”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 219 | 测试“AttackReady actually accepts a new press before old Finish”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 220 | 建立独立反例：pause rejects new requests without consuming the next valid press。 |
| 221 | 为“pause rejects new requests without consuming the next valid press”创建独立的真实核心对象，避免测试间共享状态。 |
| 222 | 测试“pause rejects new requests without consuming the next valid press”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 223 | 断言“pause rejects new requests without consuming the next valid press”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 224 | 测试“pause rejects new requests without consuming the next valid press”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 225 | 断言“pause rejects new requests without consuming the next valid press”的实际结果；若生产合同被破坏，本行导致非零退出。 |
| 226 | 测试“pause rejects new requests without consuming the next valid press”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 227 | 测试“pause rejects new requests without consuming the next valid press”的本行调用、配置、参数续行或夹具辅助；完整表达式见上方源码，行为目标来自本测试标题。 |
| 228 | 记录测试通过/失败并用退出码交付结果；不是生产日志权威。 |
| 229 | 记录测试通过/失败并用退出码交付结果；不是生产日志权威。 |
