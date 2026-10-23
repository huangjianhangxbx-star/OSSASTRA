# Gal 系统第一阶段设计

日期：2026-08-22  
项目：Project_ANOIR / Project_VELGITA  
状态：待用户最终审阅

## 1. 目标

建立一个专业、可扩展、且不侵入角色控制器实现的 Gal/AVG 对话模块。

第一阶段交付一个可运行的“Gameplay 暂停式覆盖对话”垂直切片：

- 显示人物立绘；
- 显示说话人名称；
- 显示对话文本；
- 点击确认按钮进入下一句；
- 最后一句结束后关闭界面并恢复 Gameplay；
- Editor 或 Development Build 中按下 `P` 可播放测试对话；
- 对话内容来自 ScriptableObject 资产，不硬编码在 UI 或状态中。

第一阶段不实现分支选项、自动播放、快进、历史记录、存档读取、语音、立绘动画、多角色舞台调度和本地化管线，但设计不得阻断这些能力的后续加入。

## 2. 已确认的约束

1. Gal 模块属于 `GameFlowRoot` 的子 Root。
2. 正式运行时，由 `GameFlowArbitrator` 决定请求能否开始以及 Gameplay 如何运行。
3. Gal 核心不得引用 `CharacterRoot`、角色 DataBus、角色状态机、移动、瞄准、武器或动画系统。
4. 不修改角色控制器代码来迁就 Gal。
5. 暂停 Gameplay 所需的角色控制封锁只能存在于 GameFlow/Hunt 侧的适配器中，通过角色已经公开的控制权接口实现。
6. Gal UI 使用未缩放时间更新，因此 Gameplay 暂停时按钮、过渡和未来的打字机仍可工作。
7. 所有新增设计和计划文档存放于 `Assets/Script/GameFlowManager/GalSystem`。

## 3. 现有架构问题

当前 `GameFlowArbitrator` 只有一个 `activeStateMachine`：

- 每帧只更新一个最高优先级流程；
- 切换时会对旧流程调用 `Exit()`；
- 恢复时会重新调用 `Enter()`。

因此，直接把 Gal 注册为更高优先级的 `IGameFlowState` 会产生两个问题：

- 暂停式对话会把 Hunt 误当成“退出后重新进入”，可能重复执行当前 Hunt 状态的进入副作用；
- 并行式搭话无法与 Hunt 同时更新。

结论：Gal 不能作为普通的单活跃主流程塞进现有字典。

## 4. 方案比较

### 方案 A：Gal 作为高优先级 IGameFlowState

改动最少，但只能可靠支持全屏独占模式，无法表达“挂起但不退出”和“与 Gameplay 并行”。拒绝。

### 方案 B：主流程通道与覆盖流程通道

`GameFlowArbitrator` 同时管理：

- Primary Flow Lane：Hunt、未来的 Exploration 等互斥主流程；
- Overlay Flow Lane：Gal 等可覆盖在主流程之上的流程。

Gal 的模式决定主流程继续、挂起还是隐藏，但不会把“暂停”伪装成主流程 `Exit()`。这是本设计采用的方案。

### 方案 C：通用流程栈与能力调度器

所有流程都通过栈、能力租约和并行组调度。扩展性最高，但当前只有 Hunt 与一个 Gal 覆盖流程，引入成本和测试面积过大。等第二种以上覆盖流程真正出现后再评估。

## 5. 总体结构

```text
GameFlowRoot
├── GameFlowArbitrator
├── GameplaySuspensionCoordinator
├── HuntFlowRoot                         Primary Flow
└── GalFlowRoot                          Overlay Flow
    ├── GalFlowStateMachine
    ├── GalSequencePlayer
    ├── GalDataBus
    ├── GalDialogueView
    └── GalDebugHotkeyTrigger            Editor / Development Build
```

### 执行规则

| Gal 模式 | Primary Flow | Gameplay 世界 | Gameplay 画面 | Gal UI |
|---|---|---|---|---|
| `PausedOverlay` | 保持当前状态但不推进 | 暂停 | 保留，后续加入模糊 | 完整对话框与立绘 |
| `FullscreenExclusive` | 保持当前状态但不推进 | 暂停 | 被独立背景遮挡 | 完整 AVG 舞台 |
| `ConcurrentOverlay` | 正常推进 | 正常运行 | 保留 | 轻量搭话界面 |

第一阶段完整实现 `PausedOverlay`。另两个枚举值、调度决策和视图扩展点在第一阶段建立，但不制作对应的完整视觉成品。

## 6. 外部接口与模块深度

正式调用者只需要理解一个请求接口：

```csharp
GalStartResult RequestGal(GalPlayRequest request);
```

`GalPlayRequest` 只携带：

- 要播放的 `GalSequenceSO`；
- 本次使用的 `GalPresentationMode`；
- 可选的请求来源字符串，仅用于诊断。

`GalStartResult` 明确返回：

- `Started`；
- `Busy`；
- `InvalidSequence`；
- `ViewUnavailable`；
- `RejectedByFlow`。

调用者不接触行索引、UI 引用、状态对象、暂停令牌或当前立绘。Gal 的复杂度集中在模块内部。

### 不提供 Gal 内部仲裁器

第一阶段同一时间只允许一个 Gal 会话，因此增加第二个仲裁器只会形成浅层转发。生命周期由 `GalFlowStateMachine` 管理，请求是否允许由现有 `GameFlowArbitrator` 管理。

当未来出现多个同时竞争的 Gal 请求、可中断优先级或队列策略时，再引入内部 `GalSessionArbitrator`。

## 7. Gal 生命周期状态机

```text
Inactive
   │ RequestGal 成功
   ▼
Opening
   │ 视图准备完成
   ▼
PresentingLine ── Advance ──> PresentingLine
   │ 最后一句 Advance
   ▼
Closing
   │ 释放暂停并隐藏视图
   ▼
Inactive
```

- `Inactive`：不占用覆盖流程通道；
- `Opening`：校验资产、获取 Gameplay 暂停令牌、打开视图；
- `PresentingLine`：让 `GalSequencePlayer` 生成当前行显示模型并交给 View；
- `Closing`：幂等释放暂停令牌、清空 View、发布完成事件；
- 任意异常停止都必须汇合到同一个 Closing 清理路径。

选择、历史、自动播放等未来功能作为新状态或 `PresentingLine` 内部策略加入，不改变外部请求接口。

## 8. 内容数据

### GalActorSO

表示一个稳定的剧情角色身份：

- 稳定 ID；
- 默认显示名；
- 默认立绘；
- 可选的立绘变体列表，使用稳定字符串键表示表情或服装。

### GalSequenceSO

表示一段可播放对话：

- 稳定 Sequence ID；
- 行列表；
- 默认播放模式；
- 第一阶段可选的独立背景引用，为全屏模式预留。

### GalLine

第一阶段字段：

- `GalActorSO actor`；
- `string portraitVariantKey`；
- `Sprite portraitOverride`；
- `string speakerNameOverride`；
- `TextArea string text`；
- `GalPortraitSlot portraitSlot`，第一阶段支持 Left、Center、Right 枚举但只显示一个活动立绘。

解析优先级固定为：

1. 行级 override；
2. Actor 对应变体；
3. Actor 默认值；
4. 缺少立绘时隐藏立绘区域，但文本仍可继续。

## 9. UI 结构

第一阶段 `GalDialogueView` 使用一个独立 Canvas/Prefab：

```text
GalCanvas
├── GameplayBackdropDimmer
├── PortraitStage
│   └── ActivePortrait
└── DialoguePanel
    ├── SpeakerName
    ├── DialogueText
    └── ConfirmButton
```

职责规则：

- View 只负责显示 `GalLineViewModel`、显隐和发出 Confirm 事件；
- View 不读取 ScriptableObject，不维护当前行索引，不决定何时结束；
- ConfirmButton 只调用 Gal 的 `Advance()`；
- 第一阶段文本整句显示，打字机留到后续状态扩展；
- 暂停覆盖模式使用半透明压暗层；真正的像素风背景模糊作为独立渲染适配器在核心垂直切片稳定后加入，不能与对话推进逻辑耦合；
- Canvas 使用适配安全区的锚点和 CanvasScaler，并验证 16:9、16:10 和超宽比例。

## 10. Gameplay 暂停与角色隔离

`GalFlowRoot` 不直接暂停任何角色或 Hunt。它向 `GameFlowArbitrator` 声明当前模式，由 `GameplaySuspensionCoordinator` 执行。

暂停协调器负责：

1. 保存进入前的 `Time.timeScale`；
2. 将 `Time.timeScale` 设为 0；
3. 将 `HuntFlowDataBus.isPaused` 设为 true 并保存旧值；
4. 通过一个 Hunt 侧适配器，对当前玩家已有的 `CharacterControlAuthority` 设置高优先级控制封锁；
5. 关闭时严格恢复旧值并移除自己的控制权 override；
6. 多次 Stop、对象销毁或异常均不能重复恢复或泄漏暂停状态。

Gal 核心只知道“暂停令牌”，不知道玩家、敌人或角色控制通道。该适配器可以删除或替换，而无需修改 Gal 状态机和内容资产。

Gal 使用 `Time.unscaledDeltaTime`，因此暂停期间 UI 仍可响应。

## 11. P 键调试入口

`GalDebugHotkeyTrigger` 仅在 `UNITY_EDITOR || DEVELOPMENT_BUILD` 中处理输入：

1. Inspector 引用一个测试 `GalSequenceSO`；
2. 检测 `Keyboard.current.pKey.wasPressedThisFrame`；
3. 构造与正式剧情触发器完全相同的 `GalPlayRequest`；
4. 调用 `GameFlowArbitrator.RequestGal()`；
5. Gal 已运行时返回 Busy，不重启、不叠加会话。

正式整合方式不是按键，而是由剧情触发器、场景触发区、任务系统或事件监听器构造同一种请求。删除调试适配器不会影响正式流程。

## 12. 事件

`GameEventHub` 新增独立的 `Gal` 事件域，避免把剧情事件混入 UI 或 Hunt：

- `GalStartedEvent`；
- `GalLineChangedEvent`；
- `GalCompletedEvent`；
- `GalStoppedEvent`。

事件用于音频、日志、字幕辅助和未来任务联动。UI 的 Confirm 不经过全局事件总线，而通过 View 到 GalFlowRoot 的局部回调，避免一次按钮点击广播到整个项目。

## 13. 失败处理

- null Sequence 或空行列表：返回 `InvalidSequence`，不暂停 Gameplay；
- Gal 已在运行：返回 `Busy`；
- View 引用不完整：返回 `ViewUnavailable` 并输出一次明确错误；
- 缺少 Actor：允许使用行级名称与文本；
- 缺少立绘：隐藏立绘，不中止文本；
- 缺少文本：该行仍可显示说话人并由确认继续；
- 运行中对象销毁、场景卸载或异常停止：统一执行幂等 Closing；
- 暂停令牌未成功获取时，不进入 Opening。

## 14. 第一阶段文件结构

```text
Assets/Script/GameFlowManager/GalSystem/
├── 2026-08-22-gal-system-design.md
├── Runtime/
│   ├── Core/
│   │   ├── GalFlowRoot.cs
│   │   ├── GalFlowStateMachine.cs
│   │   ├── GalSequencePlayer.cs
│   │   ├── GalDataBus.cs
│   │   └── GalContracts.cs
│   ├── Content/
│   │   ├── GalActorSO.cs
│   │   └── GalSequenceSO.cs
│   ├── Presentation/
│   │   ├── GalDialogueView.cs
│   │   └── GalLineViewModel.cs
│   └── Integration/
│       ├── GameplaySuspensionCoordinator.cs
│       ├── HuntGameplaySuspensionAdapter.cs
│       └── GalDebugHotkeyTrigger.cs
├── Tests/Editor/
│   ├── GalSequencePlayerProbe.cs
│   ├── GalFlowLifecycleProbe.cs
│   └── GameplaySuspensionProbe.cs
├── Prefabs/
│   ├── PF_GalFlowRoot.prefab
│   └── PF_GalDialogueView.prefab
└── Content/Test/
    ├── GalActor_Test.asset
    └── GalSequence_Test.asset
```

保持现有项目的全局命名方式，第一阶段不新增 asmdef，也不重构已有 GameFlow/Hunt 文件目录。

## 15. 验证策略

### 纯逻辑 Probe

- 空序列被拒绝且不暂停；
- 第一行正确显示；
- Confirm 每次只推进一行；
- 最后一句 Confirm 进入 Closing；
- Busy 请求不替换当前会话；
- Stop 可重复调用且只释放一次暂停；
- 三种模式产生正确的 Primary Flow 调度策略。

### 集成 Probe

- `PausedOverlay` 不调用 Hunt 的 `Exit/Enter`；
- 暂停期间 Gal 使用未缩放时间；
- 关闭后恢复原 `timeScale` 与原 `isPaused`；
- 控制权 override 添加和移除配对；
- View 缺失时不泄漏暂停；
- P 键使用正式请求接口。

### 场景验收

1. 进入当前测试场景；
2. 按 P；
3. Gameplay 停止，背景保留并压暗；
4. 立绘、名称和首句文本可见；
5. 点击 Confirm 逐句推进；
6. 最后一句后 UI 关闭，Gameplay 恢复；
7. 再次按 P 可重新播放；
8. Console 无新增 Error；
9. 退出 Play Mode 后场景和 Prefab 引用保持正确。

## 16. 分阶段执行顺序

### 阶段 1：暂停覆盖式垂直切片

完成数据资产、Gal 生命周期、双通道 GameFlow 调度、暂停协调、基础 UI、P 键调试和测试对话。

### 阶段 2：全屏独占 AVG

复用同一会话和内容系统，增加独立背景、完整舞台布局与全屏视觉配置；不新增第二套播放逻辑。

### 阶段 3：Gameplay 并行搭话

启用 ConcurrentOverlay 调度，制作轻量 UI 配置，验证 Gameplay 与 Gal 同时推进及 UI 输入不误触 Gameplay。

### 阶段 4：专业 Gal 能力扩展

按真实需求逐项加入打字机、自动播放、快进、历史、选项分支、语音、表情与站位过渡、剧情变量、存档和本地化。每项能力独立设计，不在第一阶段预先实现空壳。

## 17. 明确不做的事情

- 不让 `GalDialogueView` 自己遍历对话；
- 不把对话文本硬编码进 MonoBehaviour；
- 不直接从 Gal 查找或操作 `CharacterRoot`；
- 不用 `Time.timeScale = 0/1` 的成对散落调用；
- 不把暂停等同于 Hunt `Exit()`；
- 不为未来功能预建大量空状态；
- 不在第一阶段替换现有 GameFlow 架构，只增加 Primary/Overlay 调度能力。

## 18. 已确认的立绘与事件决策

### 18.1 第一阶段立绘

- 第一阶段使用普通 `Sprite`，正式 Spine 立绘不进入本次实现范围；
- 测试内容使用 `Content/Test/Portraits/SPR_GalPortrait_Test.png`，它只被测试 Actor 资产引用；
- 立绘字段允许为空。解析不到 Sprite 时 View 隐藏立绘区域，但继续显示说话人和正文；
- 对话序列只保存 Actor 与变体键。未来接入 Spine 时替换表现层解析器，不改变播放状态机、请求接口和序列推进逻辑。

### 18.2 直接命令与领域事件

- “请求播放 Gal”是需要同步得知接受/拒绝结果的命令，调用 `GameFlowArbitrator.RequestGal(GalPlayRequest)`；
- “Gal 已开始、已换句、已完成、被停止”是已经发生的事实，通过新增的 `GameEventHub.Gal` 类型化广播域发布；
- `ConfirmButton` 是 Gal 模块内部交互，使用 View 到 `GalFlowRoot` 的局部回调，不广播到全局事件总线；
- 新 Gal 代码不使用旧 `EventCenter`，也不在本任务中迁移角色控制器残留的 `EventCenter` 引用。
