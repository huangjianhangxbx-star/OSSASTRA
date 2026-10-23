# GalSystem 第三阶段：ConcurrentOverlay 并行搭话设计

**日期：** 2026-08-22  
**状态：** 已确认，待实施计划  
**适用项目：** Project VELGITA（Tuanjie 2022.3.62t12）

## 1. 目标

本阶段实现 GalSystem 的第三级表现模式 `ConcurrentOverlay`：Gameplay 正常运行时，角色通过左下角轻量通讯卡片向玩家搭话。

它与已有两种模式的区别如下：

| 模式 | Gameplay | 表现 | 推进方式 |
| --- | --- | --- | --- |
| `FullscreenExclusive` | 完全阻断 | 独立背景、完整立绘与对话框 | 玩家确认 |
| `PausedOverlay` | 暂停 | Gameplay 画面压暗、完整立绘与对话框 | 玩家确认 |
| `ConcurrentOverlay` | 正常运行 | 左下角头像通讯卡片 | 自动计时 |

本阶段继续遵守以下边界：

- 只保留一个 `GalFlowRoot`、一个 `GalFlowStateMachine` 和一个正式请求入口；
- 不修改 Character Controller、CharacterDataBus 或角色状态机；
- 不新增 `EventCenter` 依赖，继续使用现有 `EventBus` 发布 Gal 生命周期消息；
- 不为了并行搭话复制第二套 Gal 播放器或内容资产体系。

这里的“并行”是指 Gal 与 Gameplay 同时运行，而不是两段 Gal 内容彼此同时播放。

## 2. 选定架构

在现有单会话状态机上方增加 `GalConcurrentDispatcher`：

```text
GameFlowArbitrator
    → GalFlowRoot.RequestGal()
        → GalConcurrentDispatcher
            → GalFlowStateMachine
                → GalDialogueView
```

职责划分：

- `GalFlowStateMachine` 继续负责一段 Gal 会话的开始、逐句推进和关闭；
- `GalConcurrentDispatcher` 只负责并行请求的 FIFO、去重、冷却、自动计时和高级模式抢占；
- `GalFlowRoot` 持有两者，在 `Update()` 中以未缩放时间驱动调度器；
- `GalDialogueView` 仍是唯一视图入口，根据 `GalPresentationMode` 切换表现节点；
- `EventBus` 仅对外广播生命周期，不反向驱动内部控制流。

不采用以下方案：

- 将队列、时钟和去重全部塞入 `GalFlowStateMachine`：会混合会话生命周期与调度策略；
- 建立第二个 Concurrent Root/状态机：当前并不需要 Gal 与 Gal 并发，会造成重复状态、事件和仲裁逻辑。

## 3. 请求契约与返回结果

`GalPlayRequest` 在保持既有调用兼容的前提下，于构造参数末尾追加：

- `RequestKey`：触发者提供的稳定去重键；
- `DuplicateCooldownSeconds`：该 Key 完成或离开活动状态后的冷却时间。

有效 Key 的降级顺序为：

1. 显式 `RequestKey`；
2. `GalSequenceSO.SequenceId`；
3. 运行时资产标识，仅作为缺少作者配置时的安全降级。

`GalStartResult` 增加：

- `Queued`：请求已进入等待队列；
- `DuplicateIgnored`：相同 Key 正在播放、排队或处于冷却期；
- `QueueFull`：等待队列达到上限。

`GalStopReason` 增加 `Preempted`，表示当前并行搭话被更高级模式抢占。

这些扩展只增加新枚举成员与可选参数，不改变已有构造调用和两种高级模式的默认行为。

## 4. 调度规则

### 4.1 ConcurrentOverlay 请求

- Gal 空闲：立即启动；
- 当前正在播放 `ConcurrentOverlay`：请求进入 FIFO；
- 相同 Key 正在播放、排队或冷却：返回 `DuplicateIgnored`；
- 队列已满：返回 `QueueFull`；
- `PausedOverlay` 或 `FullscreenExclusive` 正在运行：返回 `Busy`，不缓存可能已经过时的 Gameplay 搭话。

默认等待容量为 3，并在 `GalFlowRoot` Inspector 中以最小值 1 的序列化字段开放调整。

请求入队前必须验证 Sequence 非空且包含有效行，不能把无效内容延迟到出队时才发现。

### 4.2 高级模式请求

当 `PausedOverlay` 或 `FullscreenExclusive` 到达时：

- 若当前为 `ConcurrentOverlay`，立即以 `Preempted` 关闭当前会话；
- 清空所有尚未开始的并行请求；
- 立即尝试启动高级模式；
- 高级模式结束后不恢复旧搭话。

若当前已经是 `PausedOverlay` 或 `FullscreenExclusive`，继续返回既有 `Busy`，不在本阶段引入高级模式之间的优先级抢占。

### 4.3 停止和销毁

显式 `StopGal()`、`GalFlowRoot` 销毁或视图失效时，当前会话和等待队列必须一并清理。所有停止、释放与销毁路径保持幂等。

已入队但从未开始的请求不发布 `GalStoppedEvent`，因为它们没有成为活动 Gal 会话。队列因抢占或停止被清空时，发布一条聚合的队列清空事件，携带清除数量与原因，供调试和上层观察。

## 5. 去重与冷却

调度器维护三类状态：

- 当前活动请求的 Key；
- FIFO 中所有等待请求的 Key；
- Key 到冷却截止时间的映射。

提交请求时按以上三处依次检查。相同请求不会因触发器连续多帧调用而刷入队列。

冷却使用未缩放时间。默认建议为 1 秒，触发者可显式设置为 0。过期冷却项按需清理，不增加长期常驻的定时任务。

## 6. 内容资产与自动推进

`ConcurrentOverlay` 继续使用现有 `GalSequenceSO` 和 `GalLine`，不创建搭话专用 Sequence 类型。

`GalLine` 增加可选字段 `AutoAdvanceSecondsOverride`：

- 小于或等于 0：根据可见文本长度自动计算；
- 大于 0：使用策划填写的秒数；
- 该字段仅被 `ConcurrentOverlay` 消费。

默认自动时长：

```text
duration = clamp(0.8 + visibleCharacterCount × 0.14, 1.8, 6.5)
```

可见字符计算忽略空白和 TMP 富文本标签。第一版不加入标点权重或语言检测，避免为了轻量搭话过度设计。

计时使用 `Time.unscaledDeltaTime`。每次显示新行时重置计时与进度；到时直接调用状态机的 `Advance()`。最后一句结束后关闭当前 Sequence，并在同一调度流程中启动 FIFO 下一项。

内部换句通知使用直接方法或局部 C# 事件。已有 `GalStartedEvent`、`GalLineChangedEvent`、`GalCompletedEvent` 和 `GalStoppedEvent` 仍由 `EventBus` 对外发布，但不作为调度器自身的控制回路。

## 7. 通讯卡片表现

`GalDialogueView` 的同一个 Canvas 下新增：

```text
VisualRoot
├─ FullscreenBackgroundStage
├─ GameplayBackdropDimmer
├─ PortraitStage
├─ DialoguePanel
└─ ConcurrentCard
   ├─ AvatarMask
   │  └─ AvatarImage
   ├─ SpeakerName
   ├─ DialogueText
   └─ AutoAdvanceProgress
```

`ConcurrentOverlay` 的显隐规则：

- 仅显示左下角 `ConcurrentCard`；
- 隐藏全屏背景、Gameplay 压暗层、完整立绘、传统对话框与确认按钮；
- 不修改 Gameplay 画面或相机；
- 卡片采用半透明深色底、细边框和约 0.15 秒淡入淡出；
- 队列连续播放时只替换内容，卡片不反复闪烁；队列耗尽后才淡出。

卡片内容：

- 小头像复用当前行已经解析出的立绘，通过 UI 遮罩显示头肩区域；
- 缺少立绘时隐藏头像，姓名与正文区域自动扩展；
- 姓名显示在正文上方；
- 正文最多两行，超出部分省略，并由编辑器验证提示作者；
- 底部细进度条表示本句自动推进进度。

第一版不增加专用头像字段。若正式 Spine/立绘的自动裁切效果不足，后续可在 `GalActorSO` 增加可选头像并回退到现有立绘，不影响本阶段契约。

## 8. 输入与 Gameplay 隔离

并行模式不订阅确认按钮，不显示“下一句”控件。

`ConcurrentCard` 使用：

- `CanvasGroup.blocksRaycasts = false`；
- `CanvasGroup.interactable = false`；
- 所有装饰 Image/TMP 关闭不必要的 Raycast Target。

因此卡片既不抢占鼠标，也不会把 Gameplay 点击转换成 Gal 推进。

`GameplaySuspensionCoordinator` 对 `ConcurrentOverlay` 继续返回无暂停租约；不得修改：

- `Time.timeScale`；
- `HuntFlow` 的暂停状态；
- Character Move/Gameplay 权限。

## 9. 事件语义

- 请求立即启动时返回 `Started`，并照常发布 `GalStartedEvent`；
- 请求进入 FIFO 时返回 `Queued`，只有真正出队启动时才发布 `GalStartedEvent`；
- 重复或队列满由同步返回结果表达，不伪造 Started/Stopped 生命周期；
- 正常播完发布 `GalCompletedEvent`；
- 活动并行搭话被高级模式打断时发布 `GalStoppedEvent(Preempted)`；
- 未开始队列被清除时只发布聚合的队列清空事件。

## 10. 调试入口

保留现有：

- `P`：`PausedOverlay`；
- `Shift+P`：`FullscreenExclusive`。

新增：

- `O`：提交并行搭话 A；
- `Shift+O`：提交不同 Key 的并行搭话 B，用于观察 FIFO。

正式游戏仍通过 `GameFlowArbitrator.RequestGal(GalPlayRequest)` 进入。调试热键不是正式依赖，移除后不影响运行时架构。

## 11. 验证策略

### 11.1 纯逻辑 Probe

- 空闲请求立即开始；
- 不同 Key 严格按 FIFO 播放；
- 播放中、排队中和冷却期内去重；
- 队列满返回 `QueueFull`；
- 自动时长公式与单句覆盖；
- 使用未缩放时间推进；
- 高级模式抢占活动会话并清空队列；
- `Stop()`、`Dispose()` 与重复清理保持幂等。

### 11.2 View Probe

- 三种模式的显隐矩阵互不回归；
- 并行卡片不阻挡 Raycast；
- 缺少头像时安全降级；
- 姓名、正文、头像和进度条正确更新；
- 队列衔接不重复开关卡片。

### 11.3 PlayMode 验收

- 通讯卡片播放期间角色可以持续移动；
- `Time.timeScale`、HuntFlow 和 Gameplay 权限不变；
- 多段不同搭话按 FIFO 自动播放；
- 连续重复请求不会刷屏；
- 随后启动全屏 AVG，当前搭话立即消失、队列清空且不会恢复；
- 既有暂停式和全屏 AVG 验收继续通过；
- 输出明确 PASS 日志和 ConcurrentOverlay 截图。

## 12. 本阶段非目标

- 同时播放两段 Gal 内容；
- 搭话优先级与紧急插播；
- 高级模式之间互相抢占；
- 打字机、语音同步、口型或 Spine 头像；
- 对话历史、回看、跳过和自动模式设置页；
- 专用头像资产管线；
- 标点、语言或配音长度驱动的高级时长算法。

这些能力均可建立在本阶段明确的调度边界上，但不应提前进入第一版。
