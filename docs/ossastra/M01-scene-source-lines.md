> 历史M01候选文档。2026-10-19共享脚本命名、路径和输入接线已修整；当前以 [修整报告](character-control-refinement.md) 和 [用户规则](../../OSSASTRA_RULES.md) 为准。旧逐行解释不代表新版本已审。

# M01 场景适配器逐行审查草稿
全部行用户待审；以下语法/行为说明与完整源码同存，不代签架构认可。独立花括号/空行仅作为结构，语句按实际行号列出；一行多语句需按源码顺序审。

## Assets/OSSASTRA/M01/Runtime/M01CharacterRoot.cs
组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP
```csharp
using System;
using Ossastra.CharacterCore;
using UnityEngine;

namespace Ossastra.M01
{
    [DefaultExecutionOrder(-100)]
    public sealed class M01CharacterRoot : MonoBehaviour
    {
        [SerializeField] private string actorId = "m01-player";
        [SerializeField] private M01Motor motor;
        [SerializeField] private M01SpineView view;
        [SerializeField] private M01MeleeHit melee;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private M01HealthTarget debugTarget;
        [Header("M01 temporary timing — not final balance")]
        [SerializeField] private float releaseAt = .3f, moveReadyAt = .5f, attackReadyAt = .8f, finishAt = 1.1f;
        [SerializeField, Min(1)] private int damage = 12;
        [SerializeField] private bool verboseTrace;
        private readonly ControlAuthority authority = new ControlAuthority();
        private readonly AttackIntent intent = new AttackIntent();
        private readonly M01PlayerInput input = new M01PlayerInput();
        private readonly object debugOwner = new object();
        private BasicActionRuntime runtime;
        private BasicAttackDefinition definition;
        private bool ready, focused = true, paused, applicationPaused, debugBlocked;
        private string lastFact = "Idle";
        private static readonly Rect DebugPanel = new Rect(12, 12, 400, 270);
        public string LastFact => lastFact;
        public bool IsPaused => paused || applicationPaused;
        public bool ActionActive => runtime != null && runtime.IsActive;

        private void Awake()
        {
            if (motor == null || view == null || melee == null || viewCamera == null || !view.Initialize())
            { Debug.LogError("M01 requires registered motor/view/melee/camera and valid Spine clips", this); enabled = false; return; }
            try { definition = new BasicAttackDefinition(releaseAt, moveReadyAt, attackReadyAt, finishAt, damage); }
            catch (ArgumentException e) { Debug.LogError("M01 invalid timing: " + e.Message, this); enabled = false; return; }
            runtime = new BasicActionRuntime(actorId);
            ready = true;
        }

        private void Update()
        {
            if (!ready) return;
            var frame = input.Read();
            if (frame.DebugCommand != 0) DebugCommand(frame.DebugCommand);
            if (frame.Pause) { paused = !paused; intent.SuppressUntilRelease(); }
            if (frame.Cancel) Interrupt("player-cancel");
            if (frame.HasPointer && focused)
            {
                var plane = new Plane(Vector3.up, transform.position);
                var ray = viewCamera.ScreenPointToRay(frame.Pointer);
                if (plane.Raycast(ray, out var distance)) motor.AimAt(ray.GetPoint(distance));
            }
            var overDebug = frame.HasPointer && DebugPanel.Contains(new Vector2(frame.Pointer.x, Screen.height - frame.Pointer.y));
            if (intent.Observe(frame.AttackHeld) && focused && !overDebug)
            {
                var request = runtime.Request(definition, "player-input", authority, IsPaused);
                if (request.PreviousTerminal != null) Observe(request.PreviousTerminal);
                if (request.Accepted) Observe(request.Fact);
                else lastFact = "Rejected: " + request.Reason;
            }
            foreach (var fact in runtime.Advance(Time.deltaTime, IsPaused || !focused))
            {
                Observe(fact);
                if (fact.Release != null) melee.Resolve(fact.Release, motor.Aim);
            }
            var canMove = focused && !IsPaused && authority.Allows(ControlChannel.Move) && runtime.MoveReady;
            motor.Tick(frame.Move, canMove, IsPaused ? 0 : Time.deltaTime);
            view.SetPaused(IsPaused || !focused);
            view.Present(runtime.IsActive, runtime.ActionId, motor.Moving, motor.FacingX);
        }

        private void Observe(ActionFact fact)
        {
            lastFact = fact.Kind + " @" + fact.At.ToString("F3") + " " + fact.ActionId.ToString("N").Substring(0, 8);
            if (fact.Reason != null) lastFact += " " + fact.Reason;
            if (verboseTrace) Debug.Log("M01 " + lastFact, this);
        }

        private void Interrupt(string reason)
        {
            intent.SuppressUntilRelease();
            if (runtime == null) return;
            var terminal = runtime.Cancel(reason);
            if (terminal != null) Observe(terminal);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            if (!hasFocus) Interrupt("focus-lost");
            else intent.SuppressUntilRelease();
        }
        private void OnApplicationPause(bool value)
        { applicationPaused = value; if (value) intent.SuppressUntilRelease(); }
        private void OnDisable() { Interrupt("disabled"); if (ready && view != null) view.SetPaused(false); }
        private void OnEnable() { intent.SuppressUntilRelease(); }

        public void DebugCommand(int command)
        {
            if (!ready) return;
            if (command == 1 && debugTarget != null)
            {
                Interrupt("debug-near-target");
                debugTarget.RestoreHealth();
                motor.Teleport(debugTarget.transform.position + Vector3.left * 1.15f + Vector3.up * .03f);
                motor.AimAt(debugTarget.transform.position);
            }
            if (command == 2)
            {
                debugBlocked = !debugBlocked;
                if (debugBlocked) { authority.Block(debugOwner, ControlChannel.Attack); Interrupt("debug-attack-block"); }
                else authority.Release(debugOwner);
            }
            if (command == 3)
            {
                Interrupt("debug-clear");
                authority.Release(debugOwner);
                debugBlocked = false;
                paused = false;
                if (debugTarget != null) debugTarget.RestoreHealth();
            }
            if (command == 4)
                Debug.Log("M01 state active=" + runtime.IsActive + " moveReady=" + runtime.MoveReady +
                    " attackReady=" + runtime.AttackReady + " paused=" + IsPaused + " last=" + lastFact, this);
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(DebugPanel, GUI.skin.box);
            GUILayout.Label("M01 本机候选 · WASD移动 / 鼠标左键单普攻 / X取消 / P暂停");
            GUILayout.Label("动画: " + (view == null ? "missing" : view.CurrentClip) + " | " + lastFact);
            GUILayout.Label("命中: " + (melee == null ? "missing" : melee.LastResult));
            GUILayout.Label("暂停: " + IsPaused + " | 禁攻: " + debugBlocked);
            if (GUILayout.Button("F1 合法状态：恢复靶HP并站到攻击距离")) DebugCommand(1);
            if (GUILayout.Button("F2 非法状态：切换具名禁攻")) DebugCommand(2);
            if (GUILayout.Button("F3 清理：取消动作／解除禁攻／恢复靶")) DebugCommand(3);
            if (GUILayout.Button("F4 检查：输出当前领域状态")) DebugCommand(4);
            GUILayout.EndArea();
        }
    }
}
```
| 行 | 行为与权威说明 |
|---:|---|
| 1 | 引用本行所示真实引擎/领域类型；不重新启用旧代码。 |
| 2 | 引用本行所示真实引擎/领域类型；不重新启用旧代码。 |
| 3 | 引用本行所示真实引擎/领域类型；不重新启用旧代码。 |
| 5 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 7 | 让组合Root先于默认Spine更新下发表现计划，减少同帧时序歧义。 |
| 8 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 10 | 本行是Inspector可见的显式参数或引用；按场景接线表赋值，修改配置后重新进Play初始化。 |
| 11 | 本行是Inspector可见的显式参数或引用；按场景接线表赋值，修改配置后重新进Play初始化。 |
| 12 | 本行是Inspector可见的显式参数或引用；按场景接线表赋值，修改配置后重新进Play初始化。 |
| 13 | 本行是Inspector可见的显式参数或引用；按场景接线表赋值，修改配置后重新进Play初始化。 |
| 14 | 本行是Inspector可见的显式参数或引用；按场景接线表赋值，修改配置后重新进Play初始化。 |
| 15 | 本行是Inspector可见的显式参数或引用；按场景接线表赋值，修改配置后重新进Play初始化。 |
| 16 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 17 | 本行是Inspector可见的显式参数或引用；按场景接线表赋值，修改配置后重新进Play初始化。 |
| 18 | 本行是Inspector可见的显式参数或引用；按场景接线表赋值，修改配置后重新进Play初始化。 |
| 19 | 本行是Inspector可见的显式参数或引用；按场景接线表赋值，修改配置后重新进Play初始化。 |
| 20 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 21 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 22 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 23 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 24 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 25 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 26 | 接Unity焦点生命周期；失焦具名取消并停止读取有效动作，不移除此保护来迁就自动化。 |
| 27 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 28 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 29 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 30 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 31 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 33 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 35 | 本行显式处理条件、遍历或返回；职责限制为：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 36 | 引用/配置非法时清楚报错并停止根调度，避免半初始化运行。 |
| 37 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 38 | 引用/配置非法时清楚报错并停止根调度，避免半初始化运行。 |
| 39 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 40 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 43 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 45 | 本行显式处理条件、遍历或返回；职责限制为：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 46 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 47 | 具名开发入口实际操作领域或空间状态；不是只改显示文字，正式发布前应审查其开放方式。 |
| 48 | 清掉按住状态并等待松开；恢复焦点或启用时不能自动接续旧Held。 |
| 49 | 本行显式处理条件、遍历或返回；职责限制为：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 50 | 本行显式处理条件、遍历或返回；职责限制为：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 52 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 53 | 把屏幕指针投射到角色脚底高度的XZ平面；尚非任意复杂地形高度系统。 |
| 54 | 把屏幕指针投射到角色脚底高度的XZ平面；尚非任意复杂地形高度系统。 |
| 56 | 消耗开发面板上的鼠标按下，防止点击开发按钮同时误攻。 |
| 57 | 消耗开发面板上的鼠标按下，防止点击开发按钮同时误攻。 |
| 59 | 只请求领域动作；由核心检查暂停/禁攻/AttackReady并分配身份，不直接释放或扣血。 |
| 60 | 先观察被替换旧动作的具名终结，随后才接受新动作事实，不能丢掉取消历史。 |
| 61 | 本行显式处理条件、遍历或返回；职责限制为：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 62 | 本行显式处理条件、遍历或返回；职责限制为：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 64 | 唯一推进核心时钟；暂停/失焦时不推进，返回有序事实供表现和命中消费。 |
| 66 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 67 | 仅真实Release事实交给空间检测，不依赖动画Hit事件触发另一份伤害。 |
| 69 | 移动许可同时满足焦点、暂停、owner权限和动作MoveReady，不把攻击资格冒充移动资格。 |
| 70 | 移动许可同时满足焦点、暂停、owner权限和动作MoveReady，不把攻击资格冒充移动资格。 |
| 71 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 72 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 75 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 77 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 78 | 本行显式处理条件、遍历或返回；职责限制为：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 79 | 本行显式处理条件、遍历或返回；职责限制为：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 82 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 84 | 清掉按住状态并等待松开；恢复焦点或启用时不能自动接续旧Held。 |
| 85 | 本行显式处理条件、遍历或返回；职责限制为：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 86 | 向唯一动作权威提交具名取消；没有活跃动作就不生成第二个终结。 |
| 87 | 本行显式处理条件、遍历或返回；职责限制为：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 90 | 接Unity焦点生命周期；失焦具名取消并停止读取有效动作，不移除此保护来迁就自动化。 |
| 92 | 接Unity焦点生命周期；失焦具名取消并停止读取有效动作，不移除此保护来迁就自动化。 |
| 93 | 本行显式处理条件、遍历或返回；职责限制为：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 94 | 清掉按住状态并等待松开；恢复焦点或启用时不能自动接续旧Held。 |
| 96 | 保存应用暂停权威，后续领域与表现共用它。 |
| 97 | 清掉按住状态并等待松开；恢复焦点或启用时不能自动接续旧Held。 |
| 98 | 对象禁启时取消和清Held；恢复不会重绑静态总线或复用旧动作。 |
| 99 | 清掉按住状态并等待松开；恢复焦点或启用时不能自动接续旧Held。 |
| 101 | 具名开发入口实际操作领域或空间状态；不是只改显示文字，正式发布前应审查其开放方式。 |
| 103 | 本行显式处理条件、遍历或返回；职责限制为：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 104 | 本行显式处理条件、遍历或返回；职责限制为：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 106 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 107 | 具名开发入口实际操作领域或空间状态；不是只改显示文字，正式发布前应审查其开放方式。 |
| 108 | 具名开发入口实际操作领域或空间状态；不是只改显示文字，正式发布前应审查其开放方式。 |
| 109 | 把屏幕指针投射到角色脚底高度的XZ平面；尚非任意复杂地形高度系统。 |
| 111 | 本行显式处理条件、遍历或返回；职责限制为：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 113 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 114 | 只写入/释放开发入口自己的owner限制，不解除其他来源。 |
| 115 | 只写入/释放开发入口自己的owner限制，不解除其他来源。 |
| 117 | 本行显式处理条件、遍历或返回；职责限制为：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 119 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 120 | 只写入/释放开发入口自己的owner限制，不解除其他来源。 |
| 121 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 122 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 123 | 具名开发入口实际操作领域或空间状态；不是只改显示文字，正式发布前应审查其开放方式。 |
| 125 | 本行显式处理条件、遍历或返回；职责限制为：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 126 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 127 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 130 | 按本行源码声明、赋值或传参；所属职责：组合依赖、调度输入与领域结果、处理生命周期；不自行计攻击时钟、不写HP。 |
| 132 | 只绘制开发信息/实际HP及调试按钮，不充当领域状态。 |
| 133 | 只绘制开发信息/实际HP及调试按钮，不充当领域状态。 |
| 134 | 只绘制开发信息/实际HP及调试按钮，不充当领域状态。 |
| 135 | 只绘制开发信息/实际HP及调试按钮，不充当领域状态。 |
| 136 | 只绘制开发信息/实际HP及调试按钮，不充当领域状态。 |
| 137 | 具名开发入口实际操作领域或空间状态；不是只改显示文字，正式发布前应审查其开放方式。 |
| 138 | 具名开发入口实际操作领域或空间状态；不是只改显示文字，正式发布前应审查其开放方式。 |
| 139 | 具名开发入口实际操作领域或空间状态；不是只改显示文字，正式发布前应审查其开放方式。 |
| 140 | 具名开发入口实际操作领域或空间状态；不是只改显示文字，正式发布前应审查其开放方式。 |
| 141 | 只绘制开发信息/实际HP及调试按钮，不充当领域状态。 |

## Assets/OSSASTRA/M01/Runtime/M01HealthTarget.cs
持有唯一Health并显示只读HP和受击闪烁；恢复仅通过明确开发调试入口
```csharp
using Ossastra.CharacterCore;
using UnityEngine;

namespace Ossastra.M01
{
    public sealed class M01HealthTarget : MonoBehaviour
    {
        [SerializeField] private string targetId = "target-a";
        [SerializeField, Min(1)] private int maximumHp = 100;
        [SerializeField, Min(.01f)] private float flashSeconds = .15f;
        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private float flashUntil;
        private bool flashing;
        public Health Health { get; private set; }

        private void Awake()
        {
            Health = new Health(targetId, maximumHp);
            renderers = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
        }

        public void ShowHit() { flashing = true; flashUntil = Time.time + flashSeconds; Paint(Color.white); }
        public void RestoreHealth()
        {
            Health = new Health(targetId, maximumHp);
            flashing = false;
            Paint(new Color(.15f, .7f, .7f));
        }

        private void Update()
        {
            if (!flashing || Time.time < flashUntil) return;
            flashing = false;
            Paint(Health.Current == 0 ? Color.gray : new Color(.15f, .7f, .7f));
        }

        private void Paint(Color color)
        {
            if (block == null) return;
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            foreach (var r in renderers) if (r != null) r.SetPropertyBlock(block);
        }

        private void OnGUI()
        {
            var camera = Camera.main;
            if (camera == null || Health == null) return;
            var point = camera.WorldToScreenPoint(transform.position + Vector3.up * 1.8f);
            if (point.z <= 0) return;
            GUI.Box(new Rect(point.x - 70, Screen.height - point.y, 140, 26),
                targetId + "  HP " + Health.Current + "/" + Health.Maximum);
        }
    }
}
```
| 行 | 行为与权威说明 |
|---:|---|
| 1 | 引用本行所示真实引擎/领域类型；不重新启用旧代码。 |
| 2 | 引用本行所示真实引擎/领域类型；不重新启用旧代码。 |
| 4 | 按本行源码声明、赋值或传参；所属职责：持有唯一Health并显示只读HP和受击闪烁；恢复仅通过明确开发调试入口。 |
| 6 | 按本行源码声明、赋值或传参；所属职责：持有唯一Health并显示只读HP和受击闪烁；恢复仅通过明确开发调试入口。 |
| 8 | 本行是Inspector可见的显式参数或引用；按场景接线表赋值，修改配置后重新进Play初始化。 |
| 9 | 本行是Inspector可见的显式参数或引用；按场景接线表赋值，修改配置后重新进Play初始化。 |
| 10 | 本行是Inspector可见的显式参数或引用；按场景接线表赋值，修改配置后重新进Play初始化。 |
| 11 | 按本行源码声明、赋值或传参；所属职责：持有唯一Health并显示只读HP和受击闪烁；恢复仅通过明确开发调试入口。 |
| 12 | 用每Renderer属性块表现闪白/死亡色，不复制共享材质、不改用户材质资产。 |
| 13 | 按本行源码声明、赋值或传参；所属职责：持有唯一Health并显示只读HP和受击闪烁；恢复仅通过明确开发调试入口。 |
| 14 | 按本行源码声明、赋值或传参；所属职责：持有唯一Health并显示只读HP和受击闪烁；恢复仅通过明确开发调试入口。 |
| 15 | 按本行源码声明、赋值或传参；所属职责：持有唯一Health并显示只读HP和受击闪烁；恢复仅通过明确开发调试入口。 |
| 17 | 按本行源码声明、赋值或传参；所属职责：持有唯一Health并显示只读HP和受击闪烁；恢复仅通过明确开发调试入口。 |
| 19 | 为目标初始化/显式恢复唯一Health对象；不保留另一份真实HP。 |
| 20 | 按本行源码声明、赋值或传参；所属职责：持有唯一Health并显示只读HP和受击闪烁；恢复仅通过明确开发调试入口。 |
| 21 | 用每Renderer属性块表现闪白/死亡色，不复制共享材质、不改用户材质资产。 |
| 24 | 读取Applied结果后触发受击表现；闪白不是伤害权威。 |
| 25 | 按本行源码声明、赋值或传参；所属职责：持有唯一Health并显示只读HP和受击闪烁；恢复仅通过明确开发调试入口。 |
| 27 | 为目标初始化/显式恢复唯一Health对象；不保留另一份真实HP。 |
| 28 | 按本行源码声明、赋值或传参；所属职责：持有唯一Health并显示只读HP和受击闪烁；恢复仅通过明确开发调试入口。 |
| 29 | 按本行源码声明、赋值或传参；所属职责：持有唯一Health并显示只读HP和受击闪烁；恢复仅通过明确开发调试入口。 |
| 32 | 按本行源码声明、赋值或传参；所属职责：持有唯一Health并显示只读HP和受击闪烁；恢复仅通过明确开发调试入口。 |
| 34 | 本行显式处理条件、遍历或返回；职责限制为：持有唯一Health并显示只读HP和受击闪烁；恢复仅通过明确开发调试入口。 |
| 35 | 按本行源码声明、赋值或传参；所属职责：持有唯一Health并显示只读HP和受击闪烁；恢复仅通过明确开发调试入口。 |
| 36 | 读取领域HP或已结算结果显示/判断，不在表现层改数值。 |
| 39 | 按本行源码声明、赋值或传参；所属职责：持有唯一Health并显示只读HP和受击闪烁；恢复仅通过明确开发调试入口。 |
| 41 | 本行显式处理条件、遍历或返回；职责限制为：持有唯一Health并显示只读HP和受击闪烁；恢复仅通过明确开发调试入口。 |
| 42 | 用每Renderer属性块表现闪白/死亡色，不复制共享材质、不改用户材质资产。 |
| 43 | 用每Renderer属性块表现闪白/死亡色，不复制共享材质、不改用户材质资产。 |
| 44 | 用每Renderer属性块表现闪白/死亡色，不复制共享材质、不改用户材质资产。 |
| 47 | 按本行源码声明、赋值或传参；所属职责：持有唯一Health并显示只读HP和受击闪烁；恢复仅通过明确开发调试入口。 |
| 49 | 按本行源码声明、赋值或传参；所属职责：持有唯一Health并显示只读HP和受击闪烁；恢复仅通过明确开发调试入口。 |
| 50 | 本行显式处理条件、遍历或返回；职责限制为：持有唯一Health并显示只读HP和受击闪烁；恢复仅通过明确开发调试入口。 |
| 51 | 只绘制开发信息/实际HP及调试按钮，不充当领域状态。 |
| 52 | 本行显式处理条件、遍历或返回；职责限制为：持有唯一Health并显示只读HP和受击闪烁；恢复仅通过明确开发调试入口。 |
| 53 | 只绘制开发信息/实际HP及调试按钮，不充当领域状态。 |
| 54 | 读取领域HP或已结算结果显示/判断，不在表现层改数值。 |

## Assets/OSSASTRA/M01/Runtime/M01MeleeHit.cs
在Release瞬时执行物理范围筛选，交给领域释放去重结算，然后只发受击表现
```csharp
using System.Linq;
using Ossastra.CharacterCore;
using UnityEngine;

namespace Ossastra.M01
{
    public sealed class M01MeleeHit : MonoBehaviour
    {
        [SerializeField, Min(0)] private float reach = .8f;
        [SerializeField, Min(.01f)] private float radius = .65f;
        [SerializeField] private float height = .8f;
        [SerializeField] private LayerMask targetLayers = ~0;
        public string LastResult { get; private set; } = "未释放";

        public void Resolve(AttackRelease release, Vector3 direction)
        {
            var center = transform.position + Vector3.up * height + direction * reach;
            var targets = Physics.OverlapSphere(center, radius, targetLayers, QueryTriggerInteraction.Collide)
                .Select(c => c.GetComponentInParent<M01HealthTarget>()).Where(t => t != null).Distinct().ToArray();
            var outcomes = release.Resolve(targets.Select(t => t.Health));
            foreach (var outcome in outcomes)
                if (outcome.Reason == HitReason.Applied)
                    targets.First(t => t.Health.Id == outcome.TargetId).ShowHit();
            LastResult = outcomes.Count == 0 ? "空挥" : string.Join("; ", outcomes.Select(o =>
                o.TargetId + " " + o.HpBefore + "→" + o.HpAfter + " " + o.Reason));
        }
    }
}
```
| 行 | 行为与权威说明 |
|---:|---|
| 1 | 引用本行所示真实引擎/领域类型；不重新启用旧代码。 |
| 2 | 引用本行所示真实引擎/领域类型；不重新启用旧代码。 |
| 3 | 引用本行所示真实引擎/领域类型；不重新启用旧代码。 |
| 5 | 按本行源码声明、赋值或传参；所属职责：在Release瞬时执行物理范围筛选，交给领域释放去重结算，然后只发受击表现。 |
| 7 | 按本行源码声明、赋值或传参；所属职责：在Release瞬时执行物理范围筛选，交给领域释放去重结算，然后只发受击表现。 |
| 9 | 本行是Inspector可见的显式参数或引用；按场景接线表赋值，修改配置后重新进Play初始化。 |
| 10 | 本行是Inspector可见的显式参数或引用；按场景接线表赋值，修改配置后重新进Play初始化。 |
| 11 | 本行是Inspector可见的显式参数或引用；按场景接线表赋值，修改配置后重新进Play初始化。 |
| 12 | 本行是Inspector可见的显式参数或引用；按场景接线表赋值，修改配置后重新进Play初始化。 |
| 13 | 按本行源码声明、赋值或传参；所属职责：在Release瞬时执行物理范围筛选，交给领域释放去重结算，然后只发受击表现。 |
| 15 | 按本行源码声明、赋值或传参；所属职责：在Release瞬时执行物理范围筛选，交给领域释放去重结算，然后只发受击表现。 |
| 17 | 按本行源码声明、赋值或传参；所属职责：在Release瞬时执行物理范围筛选，交给领域释放去重结算，然后只发受击表现。 |
| 18 | 用当前世界碰撞体筛出真实近战候选；中心/半径/层均为显式配置。 |
| 19 | 只接纳注册HealthTarget并消除同一靶多个Collider带来的重复对象。 |
| 20 | 让核心一次性批次去重和唯一HP结算；本适配器不直接写HP。 |
| 21 | 本行显式处理条件、遍历或返回；职责限制为：在Release瞬时执行物理范围筛选，交给领域释放去重结算，然后只发受击表现。 |
| 22 | 本行显式处理条件、遍历或返回；职责限制为：在Release瞬时执行物理范围筛选，交给领域释放去重结算，然后只发受击表现。 |
| 23 | 读取Applied结果后触发受击表现；闪白不是伤害权威。 |
| 24 | 按本行源码声明、赋值或传参；所属职责：在Release瞬时执行物理范围筛选，交给领域释放去重结算，然后只发受击表现。 |
| 25 | 读取领域HP或已结算结果显示/判断，不在表现层改数值。 |

## Assets/OSSASTRA/M01/Runtime/M01Motor.cs
唯一调用CharacterController.Move/Teleport并观察实际水平位移，维护平面瞄准
```csharp
using UnityEngine;

namespace Ossastra.M01
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class M01Motor : MonoBehaviour
    {
        [SerializeField, Min(.01f)] private float speed = 3;
        [SerializeField, Min(.01f)] private float groundStick = 2;
        private CharacterController body;
        public Vector3 Aim { get; private set; } = Vector3.right;
        public bool Moving { get; private set; }
        public float FacingX { get; private set; } = 1;

        private void Awake() { body = GetComponent<CharacterController>(); }

        public void AimAt(Vector3 point)
        {
            var offset = point - transform.position;
            offset.y = 0;
            if (offset.sqrMagnitude < .0001f) return;
            Aim = offset.normalized;
            if (Mathf.Abs(Aim.x) > .01f) FacingX = Mathf.Sign(Aim.x);
        }

        public void Tick(Vector2 input, bool allowed, float delta)
        {
            if (body == null || !body.enabled) return;
            var move = allowed ? Vector2.ClampMagnitude(input, 1) : Vector2.zero;
            var before = transform.position;
            body.Move((new Vector3(move.x, 0, move.y) * speed + Vector3.down * groundStick) * delta);
            var displacement = transform.position - before;
            displacement.y = 0;
            Moving = displacement.sqrMagnitude > .000001f;
        }

        public void Teleport(Vector3 position)
        {
            if (body == null) return;
            var wasEnabled = body.enabled;
            body.enabled = false;
            transform.position = position;
            body.enabled = wasEnabled;
            Moving = false;
        }
    }
}
```
| 行 | 行为与权威说明 |
|---:|---|
| 1 | 引用本行所示真实引擎/领域类型；不重新启用旧代码。 |
| 3 | 按本行源码声明、赋值或传参；所属职责：唯一调用CharacterController.Move/Teleport并观察实际水平位移，维护平面瞄准。 |
| 5 | 要求位移对象存在CharacterController，不能以Transform裸移动穿过地面边界。 |
| 6 | 按本行源码声明、赋值或传参；所属职责：唯一调用CharacterController.Move/Teleport并观察实际水平位移，维护平面瞄准。 |
| 8 | 本行是Inspector可见的显式参数或引用；按场景接线表赋值，修改配置后重新进Play初始化。 |
| 9 | 本行是Inspector可见的显式参数或引用；按场景接线表赋值，修改配置后重新进Play初始化。 |
| 10 | 通过控制器处理实际碰撞/地面；位移不是仅改变UI或动画。 |
| 11 | 按本行源码声明、赋值或传参；所属职责：唯一调用CharacterController.Move/Teleport并观察实际水平位移，维护平面瞄准。 |
| 12 | 按本行源码声明、赋值或传参；所属职责：唯一调用CharacterController.Move/Teleport并观察实际水平位移，维护平面瞄准。 |
| 13 | 按本行源码声明、赋值或传参；所属职责：唯一调用CharacterController.Move/Teleport并观察实际水平位移，维护平面瞄准。 |
| 15 | 通过控制器处理实际碰撞/地面；位移不是仅改变UI或动画。 |
| 17 | 按本行源码声明、赋值或传参；所属职责：唯一调用CharacterController.Move/Teleport并观察实际水平位移，维护平面瞄准。 |
| 19 | 按本行源码声明、赋值或传参；所属职责：唯一调用CharacterController.Move/Teleport并观察实际水平位移，维护平面瞄准。 |
| 20 | 将瞄准限制在XZ地面，维护二维Spine左右朝向，不改导入比例。 |
| 21 | 本行显式处理条件、遍历或返回；职责限制为：唯一调用CharacterController.Move/Teleport并观察实际水平位移，维护平面瞄准。 |
| 22 | 将瞄准限制在XZ地面，维护二维Spine左右朝向，不改导入比例。 |
| 23 | 将瞄准限制在XZ地面，维护二维Spine左右朝向，不改导入比例。 |
| 26 | 按本行源码声明、赋值或传参；所属职责：唯一调用CharacterController.Move/Teleport并观察实际水平位移，维护平面瞄准。 |
| 28 | 显式开发传送暂时停用Controller再恢复原启用值；不会修改源场。 |
| 29 | 按本行源码声明、赋值或传参；所属职责：唯一调用CharacterController.Move/Teleport并观察实际水平位移，维护平面瞄准。 |
| 30 | 按本行源码声明、赋值或传参；所属职责：唯一调用CharacterController.Move/Teleport并观察实际水平位移，维护平面瞄准。 |
| 31 | 通过控制器处理实际碰撞/地面；位移不是仅改变UI或动画。 |
| 32 | 从真实位移推导Moving，撞墙停住时不会仅凭WASD误认已经移动。 |
| 33 | 从真实位移推导Moving，撞墙停住时不会仅凭WASD误认已经移动。 |
| 34 | 从真实位移推导Moving，撞墙停住时不会仅凭WASD误认已经移动。 |
| 37 | 具名开发入口实际操作领域或空间状态；不是只改显示文字，正式发布前应审查其开放方式。 |
| 39 | 本行显式处理条件、遍历或返回；职责限制为：唯一调用CharacterController.Move/Teleport并观察实际水平位移，维护平面瞄准。 |
| 40 | 显式开发传送暂时停用Controller再恢复原启用值；不会修改源场。 |
| 41 | 显式开发传送暂时停用Controller再恢复原启用值；不会修改源场。 |
| 42 | 显式开发传送暂时停用Controller再恢复原启用值；不会修改源场。 |
| 43 | 显式开发传送暂时停用Controller再恢复原启用值；不会修改源场。 |
| 44 | 从真实位移推导Moving，撞墙停住时不会仅凭WASD误认已经移动。 |

## Assets/OSSASTRA/M01/Runtime/M01PlayerInput.cs
采样键鼠和调试键；不调用伤害或Spine，不接受动作
```csharp
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ossastra.M01
{
    public struct M01InputFrame
    {
        public Vector2 Move, Pointer;
        public bool AttackHeld, Cancel, Pause, HasPointer;
        public int DebugCommand;
    }

    // 只采集设备，不接受动作、不写HP、不播放动画。
    public sealed class M01PlayerInput
    {
        public M01InputFrame Read()
        {
            var f = new M01InputFrame();
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            var m = Mouse.current;
            if (k != null)
            {
                f.Move = new Vector2((k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) -
                    (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0),
                    (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) -
                    (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0));
                f.Cancel = k.xKey.wasPressedThisFrame;
                f.Pause = k.pKey.wasPressedThisFrame;
                f.DebugCommand = k.f1Key.wasPressedThisFrame ? 1 : k.f2Key.wasPressedThisFrame ? 2 :
                    k.f3Key.wasPressedThisFrame ? 3 : k.f4Key.wasPressedThisFrame ? 4 : 0;
            }
            if (m != null) { f.Pointer = m.position.ReadValue(); f.AttackHeld = m.leftButton.isPressed; f.HasPointer = true; }
#else
            f.Move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            f.AttackHeld = Input.GetMouseButton(0);
            f.Pointer = Input.mousePosition;
            f.HasPointer = true;
            f.Cancel = Input.GetKeyDown(KeyCode.X);
            f.Pause = Input.GetKeyDown(KeyCode.P);
            f.DebugCommand = Input.GetKeyDown(KeyCode.F1) ? 1 : Input.GetKeyDown(KeyCode.F2) ? 2 :
                Input.GetKeyDown(KeyCode.F3) ? 3 : Input.GetKeyDown(KeyCode.F4) ? 4 : 0;
#endif
            return f;
        }
    }
}
```
| 行 | 行为与权威说明 |
|---:|---|
| 1 | 引用本行所示真实引擎/领域类型；不重新启用旧代码。 |
| 2 | 按实际启用的输入后端编译分支；当前项目Both，优先InputSystem，未改全局设置。 |
| 3 | 引用本行所示真实引擎/领域类型；不重新启用旧代码。 |
| 4 | 按实际启用的输入后端编译分支；当前项目Both，优先InputSystem，未改全局设置。 |
| 6 | 按本行源码声明、赋值或传参；所属职责：采样键鼠和调试键；不调用伤害或Spine，不接受动作。 |
| 8 | 按本行源码声明、赋值或传参；所属职责：采样键鼠和调试键；不调用伤害或Spine，不接受动作。 |
| 10 | 按本行源码声明、赋值或传参；所属职责：采样键鼠和调试键；不调用伤害或Spine，不接受动作。 |
| 11 | 按本行源码声明、赋值或传参；所属职责：采样键鼠和调试键；不调用伤害或Spine，不接受动作。 |
| 12 | 具名开发入口实际操作领域或空间状态；不是只改显示文字，正式发布前应审查其开放方式。 |
| 15 | 只采集设备，不接受动作、不写HP、不播放动画。 |
| 16 | 按本行源码声明、赋值或传参；所属职责：采样键鼠和调试键；不调用伤害或Spine，不接受动作。 |
| 18 | 按本行源码声明、赋值或传参；所属职责：采样键鼠和调试键；不调用伤害或Spine，不接受动作。 |
| 20 | 按本行源码声明、赋值或传参；所属职责：采样键鼠和调试键；不调用伤害或Spine，不接受动作。 |
| 21 | 按实际启用的输入后端编译分支；当前项目Both，优先InputSystem，未改全局设置。 |
| 22 | 按本行源码声明、赋值或传参；所属职责：采样键鼠和调试键；不调用伤害或Spine，不接受动作。 |
| 23 | 按本行源码声明、赋值或传参；所属职责：采样键鼠和调试键；不调用伤害或Spine，不接受动作。 |
| 24 | 本行显式处理条件、遍历或返回；职责限制为：采样键鼠和调试键；不调用伤害或Spine，不接受动作。 |
| 26 | 从真实设备采样按下/保持或指针；攻击边沿仍由核心AttackIntent去重。 |
| 27 | 从真实设备采样按下/保持或指针；攻击边沿仍由核心AttackIntent去重。 |
| 28 | 从真实设备采样按下/保持或指针；攻击边沿仍由核心AttackIntent去重。 |
| 29 | 从真实设备采样按下/保持或指针；攻击边沿仍由核心AttackIntent去重。 |
| 30 | 从真实设备采样按下/保持或指针；攻击边沿仍由核心AttackIntent去重。 |
| 31 | 从真实设备采样按下/保持或指针；攻击边沿仍由核心AttackIntent去重。 |
| 32 | 具名开发入口实际操作领域或空间状态；不是只改显示文字，正式发布前应审查其开放方式。 |
| 33 | 从真实设备采样按下/保持或指针；攻击边沿仍由核心AttackIntent去重。 |
| 35 | 从真实设备采样按下/保持或指针；攻击边沿仍由核心AttackIntent去重。 |
| 36 | 按实际启用的输入后端编译分支；当前项目Both，优先InputSystem，未改全局设置。 |
| 37 | 从真实设备采样按下/保持或指针；攻击边沿仍由核心AttackIntent去重。 |
| 38 | 从真实设备采样按下/保持或指针；攻击边沿仍由核心AttackIntent去重。 |
| 39 | 按本行源码声明、赋值或传参；所属职责：采样键鼠和调试键；不调用伤害或Spine，不接受动作。 |
| 40 | 按本行源码声明、赋值或传参；所属职责：采样键鼠和调试键；不调用伤害或Spine，不接受动作。 |
| 41 | 从真实设备采样按下/保持或指针；攻击边沿仍由核心AttackIntent去重。 |
| 42 | 从真实设备采样按下/保持或指针；攻击边沿仍由核心AttackIntent去重。 |
| 43 | 具名开发入口实际操作领域或空间状态；不是只改显示文字，正式发布前应审查其开放方式。 |
| 44 | 从真实设备采样按下/保持或指针；攻击边沿仍由核心AttackIntent去重。 |
| 45 | 按实际启用的输入后端编译分支；当前项目Both，优先InputSystem，未改全局设置。 |
| 46 | 本行显式处理条件、遍历或返回；职责限制为：采样键鼠和调试键；不调用伤害或Spine，不接受动作。 |

## Assets/OSSASTRA/M01/Runtime/M01SpineView.cs
按可信状态去重播放clip，保存原始ScaleX/timeScale，同步暂停；无伤害回调
```csharp
using System;
using Spine.Unity;
using UnityEngine;

namespace Ossastra.M01
{
    public sealed class M01SpineView : MonoBehaviour
    {
        [SerializeField] private SkeletonAnimation skeleton;
        [SerializeField] private string idle = "idle1_1", move = "run", attack = "atk1";
        private string lastClip;
        private Guid lastAction;
        private float originalScaleX;
        private float originalTimeScale;
        public string CurrentClip => lastClip;

        public bool Initialize()
        {
            if (skeleton == null) skeleton = GetComponent<SkeletonAnimation>();
            if (skeleton == null || skeleton.skeletonDataAsset == null) return false;
            skeleton.Initialize(false);
            var data = skeleton.skeletonDataAsset.GetSkeletonData(false);
            if (data == null || data.FindAnimation(idle) == null || data.FindAnimation(move) == null ||
                data.FindAnimation(attack) == null || skeleton.Skeleton == null) return false;
            originalScaleX = Mathf.Abs(skeleton.Skeleton.ScaleX);
            originalTimeScale = skeleton.timeScale;
            return true;
        }

        public void SetPaused(bool paused)
        {
            if (skeleton != null) skeleton.timeScale = paused ? 0 : originalTimeScale;
        }

        public void Present(bool actionActive, Guid actionId, bool moving, float facing)
        {
            if (skeleton == null || skeleton.Skeleton == null || skeleton.AnimationState == null) return;
            var clip = actionActive && !moving ? attack : moving ? move : idle;
            var replay = clip == attack && actionId != lastAction;
            skeleton.Skeleton.ScaleX = originalScaleX * facing;
            if (clip != lastClip || replay)
            {
                skeleton.AnimationState.SetAnimation(0, clip, clip != attack);
                lastClip = clip;
            }
            lastAction = actionId;
        }
    }
}
```
| 行 | 行为与权威说明 |
|---:|---|
| 1 | 引用本行所示真实引擎/领域类型；不重新启用旧代码。 |
| 2 | 引用本行所示真实引擎/领域类型；不重新启用旧代码。 |
| 3 | 引用本行所示真实引擎/领域类型；不重新启用旧代码。 |
| 5 | 按本行源码声明、赋值或传参；所属职责：按可信状态去重播放clip，保存原始ScaleX/timeScale，同步暂停；无伤害回调。 |
| 7 | 按本行源码声明、赋值或传参；所属职责：按可信状态去重播放clip，保存原始ScaleX/timeScale，同步暂停；无伤害回调。 |
| 9 | 本行是Inspector可见的显式参数或引用；按场景接线表赋值，修改配置后重新进Play初始化。 |
| 10 | 本行是Inspector可见的显式参数或引用；按场景接线表赋值，修改配置后重新进Play初始化。 |
| 11 | 按clip和动作身份去重，同clip的新动作允许重播；没有完成回调写领域状态。 |
| 12 | 按clip和动作身份去重，同clip的新动作允许重播；没有完成回调写领域状态。 |
| 13 | 保存当前骨架水平比例，再只改变左右符号；不重制PPU或骨架尺度。 |
| 14 | 保存并恢复原动画速度；暂停设0，避免领域已停而Spine继续前进。 |
| 15 | 按clip和动作身份去重，同clip的新动作允许重播；没有完成回调写领域状态。 |
| 17 | 按本行源码声明、赋值或传参；所属职责：按可信状态去重播放clip，保存原始ScaleX/timeScale，同步暂停；无伤害回调。 |
| 19 | 本行显式处理条件、遍历或返回；职责限制为：按可信状态去重播放clip，保存原始ScaleX/timeScale，同步暂停；无伤害回调。 |
| 20 | 本行显式处理条件、遍历或返回；职责限制为：按可信状态去重播放clip，保存原始ScaleX/timeScale，同步暂停；无伤害回调。 |
| 21 | 核验真实Spine数据与idle/run/atk1；缺少引用或clip就明确拒绝初始化，不用替身冒充。 |
| 22 | 核验真实Spine数据与idle/run/atk1；缺少引用或clip就明确拒绝初始化，不用替身冒充。 |
| 23 | 核验真实Spine数据与idle/run/atk1；缺少引用或clip就明确拒绝初始化，不用替身冒充。 |
| 24 | 核验真实Spine数据与idle/run/atk1；缺少引用或clip就明确拒绝初始化，不用替身冒充。 |
| 25 | 保存当前骨架水平比例，再只改变左右符号；不重制PPU或骨架尺度。 |
| 26 | 保存并恢复原动画速度；暂停设0，避免领域已停而Spine继续前进。 |
| 27 | 本行显式处理条件、遍历或返回；职责限制为：按可信状态去重播放clip，保存原始ScaleX/timeScale，同步暂停；无伤害回调。 |
| 30 | 按本行源码声明、赋值或传参；所属职责：按可信状态去重播放clip，保存原始ScaleX/timeScale，同步暂停；无伤害回调。 |
| 32 | 保存并恢复原动画速度；暂停设0，避免领域已停而Spine继续前进。 |
| 35 | 按本行源码声明、赋值或传参；所属职责：按可信状态去重播放clip，保存原始ScaleX/timeScale，同步暂停；无伤害回调。 |
| 37 | 本行显式处理条件、遍历或返回；职责限制为：按可信状态去重播放clip，保存原始ScaleX/timeScale，同步暂停；无伤害回调。 |
| 38 | 按本行源码声明、赋值或传参；所属职责：按可信状态去重播放clip，保存原始ScaleX/timeScale，同步暂停；无伤害回调。 |
| 39 | 按clip和动作身份去重，同clip的新动作允许重播；没有完成回调写领域状态。 |
| 40 | 保存当前骨架水平比例，再只改变左右符号；不重制PPU或骨架尺度。 |
| 41 | 按clip和动作身份去重，同clip的新动作允许重播；没有完成回调写领域状态。 |
| 43 | 按clip和动作身份去重，同clip的新动作允许重播；没有完成回调写领域状态。 |
| 44 | 按clip和动作身份去重，同clip的新动作允许重播；没有完成回调写领域状态。 |
| 46 | 按clip和动作身份去重，同clip的新动作允许重播；没有完成回调写领域状态。 |
