# Gal 系统第二阶段：全屏 AVG 设计

**状态：** 已确认方向，待用户复核本文后进入实现计划

**日期：** 2026-08-22

**依赖：** 第一阶段 `PausedOverlay` 垂直切片（提交 `c42a578`）

## 1. 目标

在不复制 Gal 状态机、不修改角色控制器的前提下，实现可实际运行的
`FullscreenExclusive` 全屏 AVG 模式：

- 使用独立背景完全遮挡 Gameplay；
- 显示原创二次元测试立绘；
- 复用现有姓名、对话正文和确认按钮；
- 每次确认推进一句，末句后关闭；
- 会话期间暂停 Gameplay，结束后完整恢复进入前状态；
- 保持 `PausedOverlay` 的现有行为不变；
- 用 `Shift + P` 提供仅限编辑器或开发构建的测试入口。

本阶段交付的是全屏 AVG 的最小完整纵切面，不实现分支选择、存档、历史、
自动播放、快进、配音、打字机或 Spine 立绘。

## 2. 核心决策

### 2.1 复用同一 Gal 会话内核

`FullscreenExclusive` 继续由以下既有链路负责：

```text
剧情触发器 / 调试热键
        ↓
GameFlowArbitrator.RequestGal(...)
        ↓
GalFlowRoot
        ↓
GalFlowStateMachine
        ├── GalSequencePlayer
        ├── GameplaySuspensionCoordinator
        └── GalDialogueView
```

本阶段不增加第二个 `GalFlowRoot`、第二个状态机或第二套确认按钮回调。
播放模式只决定暂停策略和 View 的视觉布局，不决定对话如何推进。

### 2.2 使用模式驱动的单一 View

保留一个 `PF_GalDialogueView`。`GalDialogueView` 在打开时接收
`GalPresentationMode`，据此切换：

- 全屏背景层；
- Gameplay 压暗层；
- 立绘舞台布局；
- 对话框布局。

这比复制一个全屏 Prefab 更能保证确认、隐藏、缺失资源降级和后续功能只维护
一次；同时比切换独立 Unity 场景更轻，不引入场景装卸与状态回灌问题。

### 2.3 背景属于内容数据，不属于触发请求

背景由 Gal 内容资产决定，而不是由 `GalPlayRequest` 携带。触发者仍然只需知道：

- 播放哪个 `GalSequenceSO`；
- 使用哪个 `GalPresentationMode`；
- 请求来源字符串。

这样任务、场景触发器和剧情脚本不会依赖 UI Sprite。

## 3. 备选方案与取舍

### 方案 A：单状态机、单 View、模式切换（采用）

优点是改动集中、无重复推进逻辑、不会扩大 GameFlow 接口。缺点是 View 需要维护
少量模式布局分支，但该分支只处理表现。

### 方案 B：为全屏 AVG 复制独立 View/控制器（不采用）

视觉隔离直接，但会复制确认、打开、关闭、错误降级和 Prefab 安装逻辑。随着历史、
自动播放等功能加入，两套控制器容易漂移。

### 方案 C：切换或叠加独立 AVG 场景（本阶段不采用）

适合未来极复杂、独立加载的大型章节，但当前会增加场景生命周期、资源加载、
Gameplay 状态保存和恢复成本，超出本阶段需求。

## 4. 内容数据扩展

### 4.1 `GalSequenceSO`

新增：

```csharp
[SerializeField] private Sprite defaultBackground;
public Sprite DefaultBackground => defaultBackground;
```

它表示本段对话的默认全屏背景。旧资产的该字段默认为 `null`，无需迁移。

### 4.2 `GalLine`

新增：

```csharp
[SerializeField] private Sprite backgroundOverride;
public Sprite BackgroundOverride => backgroundOverride;
```

构造函数的新参数放在参数列表末尾并提供默认值，以保持现有调用方源码兼容。

### 4.3 `GalLineViewModel`

新增只读 `Sprite Background`。`GalSequencePlayer` 固定按以下顺序解析：

1. 当前行 `BackgroundOverride`；
2. 当前序列 `DefaultBackground`；
3. `null`。

View 收到 `null` 时不终止会话：`FullscreenExclusive` 显示纯黑安全背景，其他模式
按各自规则隐藏全屏背景层。

这组字段足以支持本阶段的单背景，也为未来同一段对话中切换场景保留了自然入口，
不提前引入背景过渡、缓存器或资源加载抽象。

## 5. View 接口和会话数据流

`IGalDialogueView.Show()` 调整为：

```csharp
void Show(GalPresentationMode mode);
```

`Present(GalLineViewModel line)` 保持单一行模型入口。状态机启动顺序为：

1. 校验请求、序列与 View；
2. 用请求模式获取暂停令牌；
3. 解析第一行；
4. `view.Show(request.PresentationMode)`；
5. `view.Present(firstLine)`；
6. 发布既有 `GalStartedEvent` 与 `GalLineChangedEvent`。

后续行仍只调用 `Present`。背景是否变化由 View 比较并直接刷新，不增加状态机状态。
现有 `GameEventHub.Gal` 事件类型和 `GameFlowArbitrator.RequestGal()` 接口保持不变。

## 6. UI 结构与显示规则

目标 Prefab 仍为一个 Canvas：

```text
GalCanvas
├── FullscreenBackgroundStage
│   └── BackgroundImage
├── GameplayBackdropDimmer
├── PortraitStage
│   └── ActivePortrait
└── DialoguePanel
    ├── SpeakerName
    ├── DialogueText
    └── ConfirmButton
```

`BackgroundImage` 使用 16:9 原图，并通过 `AspectRatioFitter` 的
`EnvelopeParent` 规则铺满屏幕：窄屏或超宽屏只裁边，不拉伸人物和建筑。

| 模式 | 全屏背景 | Gameplay 压暗层 | Gameplay | 主要布局 |
| --- | --- | --- | --- | --- |
| `PausedOverlay` | 隐藏 | 显示 | 可见但暂停 | 保持第一阶段 |
| `FullscreenExclusive` | 显示且不透明 | 隐藏 | 被遮挡且暂停 | 大立绘 + 底部对话框 |
| `ConcurrentOverlay` | 隐藏 | 隐藏 | 可见且继续运行 | 本阶段不制作成品 |

全屏背景只负责视觉遮挡，不禁用 Gameplay Camera、不卸载场景，也不更改角色、敌人
或 Hunt 对象。若日后性能分析证明背后渲染有实际成本，再增加可替换的渲染暂停适配器；
本阶段不做无证据优化。

## 7. Gameplay 暂停与恢复

`GameplaySuspensionCoordinator` 已将除 `ConcurrentOverlay` 外的模式视为暂停模式，
因此 `FullscreenExclusive` 直接复用现有令牌：

- 保存并设置 `Time.timeScale = 0`；
- 保存并设置 `HuntFlowDataBus.isPaused = true`；
- 通过 `CharacterControlAuthority` 高优先级 override 封锁控制；
- 关闭时幂等释放 override，并恢复进入前的时间缩放和 Hunt 暂停值。

Gal 核心不直接读取或修改 Character 状态、移动、动画、朝向或武器系统。

## 8. 调试触发

保留 `P` 与第一阶段的行为，新增组合键：

| 按键 | 请求 |
| --- | --- |
| `P` | `testSequence` + `PausedOverlay` |
| `Shift + P` | `fullscreenTestSequence` + `FullscreenExclusive` |

`GalDebugHotkeyTrigger` 在一次 `P` 按下中先判断左右 Shift，再只发出一个请求，避免
组合键同时触发两种模式。新增序列引用不重命名现有 `testSequence`，也保留既有
`presentationMode` 字段和普通 `P` 行为，从而保留 Prefab/场景中的已序列化引用。

`GalSequenceSO.DefaultPresentationMode` 当前不是运行时请求的权威来源；本阶段不静默
改变 `GalPlayRequest` 构造函数的既有默认语义。全屏测试资产仍将该字段设为
`FullscreenExclusive` 以表达作者意图，但 `Shift + P` 必须显式把该模式写入请求。

该组件继续受 `UNITY_EDITOR || DEVELOPMENT_BUILD` 约束。正式触发方式仍是任务、
剧情节点或场景触发器构造相同的 `GalPlayRequest`，删除调试组件不影响正式链路。

## 9. 测试内容与美术资产

### 9.1 原创二次元立绘

生成新的原创成年女性测试角色：

- 人物特征参考图 1 的通用方向：金色短发、蓝眼、略带自信的神情；
- 画面语言参考图 2 的通用方向：纯日系二次元、清晰赛璐璐上色、精致线稿；
- 服装为原创红色军装风外套、黑色贝雷帽和深色内搭；
- 半身至大腿上部构图，姿势延续第一阶段立绘的自然站姿；
- 不复制参考角色的具体身份、饰品、徽记、吉祥物或构图；
- 无文字、无水印，最终交付透明 PNG。

保存路径：

`Assets/Script/GameFlowManager/GalSystem/Content/Test/Portraits/SPR_GalPortrait_Test_Anime.png`

`GalActor_Test.asset` 改为引用新立绘。旧测试立绘保留为未使用测试素材，本阶段不做
破坏性删除。

### 9.2 全屏背景

生成原创 16:9 日系 AVG 背景：黄昏中的废旧列车车厢，冷暖交界、无人、无文字，
中左与中右保留可放置立绘的视觉空间，底部避免关键叙事元素以免被对话框遮挡。

保存路径：

`Assets/Script/GameFlowManager/GalSystem/Content/Test/Backgrounds/BG_GalFullscreen_Test.png`

### 9.3 测试序列

新增：

`Assets/Script/GameFlowManager/GalSystem/Content/Test/GalSequence_Test_Fullscreen.asset`

要求：

- 默认模式为 `FullscreenExclusive`；
- 默认背景引用新背景；
- 至少三句中文测试对话；
- 使用更新后的 `GalActor_Test`；
- 至少一行显式使用左、中央或右立绘槽，验证全屏布局。

这里的默认模式用于内容资产自描述；调试热键仍显式构造 `FullscreenExclusive` 请求。

## 10. 错误处理

- 全屏默认背景和行背景都缺失：显示纯黑背景，对话继续；
- 立绘缺失：隐藏立绘区域，对话继续；
- 全屏测试序列未配置：只记录一次明确警告，不影响 `P`；
- 会话正在运行：沿用 `Busy`，不重启或叠加；
- View 新增背景引用缺失：`IsReady` 返回 false，启动返回 `ViewUnavailable`，不暂停
  Gameplay；
- 对象销毁、场景卸载或显式停止：继续汇入既有 `Closing` 清理路径。

## 11. 测试策略

实施遵循测试先行。

### 11.1 编辑器探针

扩展 `GalSequencePlayerProbe`：

- 行背景覆盖序列背景；
- 无行覆盖时使用序列背景；
- 两者均为空时 ViewModel 背景为 `null`；
- 旧立绘、姓名和正文解析行为不回归。

扩展 `GalFlowLifecycleProbe`：

- 状态机把 `FullscreenExclusive` 传给 View；
- 全屏模式成功获取暂停令牌；
- Busy、完成和强制停止仍只释放一次；
- `PausedOverlay` 原有测试继续通过。

增加缺失背景用例：全屏模式收到 `null` 背景时不拒绝会话，View 显示不透明纯黑，
确认按钮仍能推进至正常结束。

### 11.2 Unity 资产与 Prefab 验证

`GalSystemTestContentInstaller` 继续幂等创建或更新：

- `PF_GalDialogueView.prefab` 的背景层、Image 和模式布局引用；
- 全屏测试序列及其背景引用；
- 调试热键的全屏序列引用；
- 现有场景实例只做必要连接，不保存用户原本已脏的场景。

现有两个 Prefab 应通过 `PrefabUtility.LoadPrefabContents` 原位增量更新并保持既有层级
路径与 local fileID；不得删除后重建 Gal Root、Canvas、`VisualRoot`、
`PortraitStage/ActivePortrait` 或 `DialoguePanel/ConfirmButton`。这样可保留场景实例的
Prefab 连接和既有 `gameFlowArbitrator` override。

所有 Prefab、`.asset` 和场景序列化修改通过 Unity Editor API 完成，不手工编辑 YAML。

### 11.3 Play Mode 验收

验证流程：

1. `P` 启动 `PausedOverlay`，确认第一阶段无回归并正常关闭；
2. `Shift + P` 启动 `FullscreenExclusive`；
3. 确认背景完全遮挡 Gameplay，立绘、姓名、正文和按钮可见；
4. 确认 `Time.timeScale == 0`、Hunt 已暂停、玩家控制权限被封锁；
5. 连续点击确认按钮，逐句推进并在末句关闭；
6. 确认时间缩放、Hunt 暂停值和控制权限恢复；
7. 保存截图：
   `Assets/Script/GameFlowManager/GalSystem/Tests/Artifacts/GalFullscreenExclusive.png`。

## 12. 预计修改范围

运行时代码：

- `Runtime/Content/GalSequenceSO.cs`
- `Runtime/Core/GalContracts.cs`
- `Runtime/Core/GalSequencePlayer.cs`
- `Runtime/Core/GalFlowStateMachine.cs`
- `Runtime/Presentation/GalLineViewModel.cs`
- `Runtime/Presentation/GalDialogueView.cs`
- `Runtime/Integration/GalDebugHotkeyTrigger.cs`

编辑器与测试：

- `Editor/GalSystemTestContentInstaller.cs`
- `Editor/GalPlayModeVerificationRunner.cs`
- `Tests/Editor/GalSequencePlayerProbe.cs`
- `Tests/Editor/GalFlowLifecycleProbe.cs`
- `PF_GalDialogueView.prefab`
- `PF_GalFlowRoot.prefab`（仅在新增序列引用确实位于该 Prefab 时）
- 新测试内容、美术资产和验收截图

不会修改：

- `Character` 状态、移动、朝向、动画、武器或 DataBus；
- `HuntFlow` 的状态机结构；
- 既有 `GameFlowArbitrator.RequestGal()` 公共签名；
- `EventCenter` 或 `EventBus` 的底层实现；
- 当前 Gameplay 场景的非 Gal 内容。

## 13. 完成标准

- 项目编译零错误；
- 全部 Gal 编辑器探针通过；
- `P` 与 `Shift + P` 分别稳定启动两个预期模式；
- 全屏模式有原创背景和新版原创二次元立绘；
- NEXT 逐句推进与末句关闭正常；
- 进入和退出前后的 Gameplay 状态完全一致；
- 旧 `PausedOverlay` 功能无回归；
- Prefab、测试 ScriptableObject 和引用经 Unity 回读验证；
- 设计、实施计划和最终验证记录存放在 `GameFlowManager/GalSystem` 内。
