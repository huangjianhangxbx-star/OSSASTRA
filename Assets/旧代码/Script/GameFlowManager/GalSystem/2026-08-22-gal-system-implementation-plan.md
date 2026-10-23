# Gal 系统第一阶段实施计划

> **执行约束：** 按 TDD 的 Red → Green → Refactor 顺序逐项完成。所有场景、Prefab 和 ScriptableObject 变更只通过 Unity 编辑器 API 完成。

**目标：** 交付一个可由 `P` 键启动、暂停 Gameplay、显示临时 Sprite 立绘与逐句对话、结束后可靠恢复 Gameplay 的 PausedOverlay 垂直切片。

**架构：** `GameFlowArbitrator` 保留 Primary Flow，并新增独立的 Gal Overlay 会话入口。`GalFlowRoot` 只管理会话和 View；暂停由 GameFlow 侧协调器负责；跨模块通知使用 `GameEventHub.Gal`，确认按钮使用模块内回调。

**技术栈：** Tuanjie/Unity 2022.3、C#、Unity UI、TextMeshPro、Input System、ScriptableObject、现有 `EventBus`。

**设计依据：** `Assets/Script/GameFlowManager/GalSystem/2026-08-22-gal-system-design.md`

**全局约束：**

- 不引用或修改角色状态机、移动、动画、武器与瞄准代码；
- 不迁移现存 `EventCenter`；
- 不新增 asmdef；
- UI 使用未缩放时间，第一阶段只完整实现 `PausedOverlay`；
- 新增文档留在 `Assets/Script/GameFlowManager/GalSystem`；
- 测试立绘允许替换或删除，缺失立绘不能中止对话。

---

### 任务 1：纯逻辑内容解析与逐句播放器

**文件：**

- 创建 `Runtime/Content/GalActorSO.cs`
- 创建 `Runtime/Content/GalSequenceSO.cs`
- 创建 `Runtime/Presentation/GalLineViewModel.cs`
- 创建 `Runtime/Core/GalSequencePlayer.cs`
- 创建 `Tests/Editor/GalSequencePlayerProbe.cs`

**接口：**

- `GalSequencePlayer.TryBegin(GalSequenceSO, out GalLineViewModel)`
- `GalSequencePlayer.TryAdvance(out GalLineViewModel)`
- `GalSequencePlayer.HasActiveSequence` 与 `CurrentLineIndex`

**TDD 步骤：**

- [ ] 先写 Probe，覆盖 null/空序列拒绝、首句解析、override 优先级、逐句推进和末句结束；
- [ ] 通过 Unity 编译或编辑器执行观察缺失类型/失败断言；
- [ ] 添加最小内容类型与播放器实现；
- [ ] 重跑 Probe，预期返回 `PASS gal sequence player`。

### 任务 2：会话状态机、暂停令牌与事件契约

**文件：**

- 创建 `Runtime/Core/GalContracts.cs`
- 创建 `Runtime/Core/GalFlowStateMachine.cs`
- 创建 `Runtime/Integration/GameplaySuspensionCoordinator.cs`
- 创建 `Tests/Editor/GalFlowLifecycleProbe.cs`
- 修改 `Assets/Script/GameEvent/GameEventHub.cs`

**接口：**

- `GalFlowStateMachine.TryStart(...)`、`Advance()`、`Stop(reason)`
- `IGalDialogueView`、`IGameplaySuspensionService` 与可释放的暂停令牌
- `GameEventHub.Gal` 及 Started/LineChanged/Completed/Stopped 事件

**TDD 步骤：**

- [ ] 先写生命周期 Probe，覆盖 Busy、正常完成、异常 Stop、重复 Stop 和暂停只释放一次；
- [ ] 观察预期失败；
- [ ] 实现最小状态机和幂等暂停协调器；
- [ ] 重跑 Probe，预期返回 `PASS gal flow lifecycle`。

### 任务 3：Unity View、Root 与 GameFlow 集成

**文件：**

- 创建 `Runtime/Presentation/GalDialogueView.cs`
- 创建 `Runtime/Core/GalFlowRoot.cs`
- 创建 `Runtime/Integration/GalDebugHotkeyTrigger.cs`
- 修改 `Assets/Script/GameFlowManager/GameFlowArbitrator.cs`
- 修改 `Assets/Script/GameFlowManager/GameFlowRoot.cs`

**接口：**

- `GameFlowArbitrator.RequestGal(GalPlayRequest)` 返回明确 `GalStartResult`
- `GalDialogueView` 实现 `IGalDialogueView`，发出局部 `Confirmed` 回调
- `GalDebugHotkeyTrigger` 在 Editor/Development Build 监听 `P`

**TDD 步骤：**

- [ ] 先扩展生命周期 Probe，验证请求结果与 View 不可用分支；
- [ ] 观察预期失败；
- [ ] 接入 Root、Arbitrator 与 View；
- [ ] 重跑所有 Gal Probe。

### 任务 4：测试资产、Prefab 与活动场景挂接

**文件：**

- 创建 `Content/Test/GalActor_Test.asset`
- 创建 `Content/Test/GalSequence_Test.asset`
- 创建 `Prefabs/PF_GalDialogueView.prefab`
- 创建 `Prefabs/PF_GalFlowRoot.prefab`
- 导入 `Content/Test/Portraits/SPR_GalPortrait_Test.png`
- 修改当前测试场景中的 `GameFlowRoot` 子级组成

**Unity API 步骤：**

- [ ] 设置测试 PNG 为单 Sprite、透明、无 mipmap；
- [ ] 创建测试 Actor 与至少三句 Sequence；
- [ ] 创建包含压暗层、立绘、姓名、正文、确认按钮的 Canvas Prefab；
- [ ] 将 `GalFlowRoot` 作为 `GameFlowRoot` 子 Root 挂接并回读全部引用；
- [ ] 只保存目标 Prefab、资产与活动场景。

### 任务 5：编译、Play Mode 与交互验收

- [ ] 退出 Play Mode，刷新并等待编译，确认没有新增编译错误；
- [ ] 执行全部 Gal Probe；
- [ ] 进入 Play Mode，按 `P`，确认 Gameplay 暂停且 Gal UI 可见；
- [ ] 点击确认按钮逐句推进，末句关闭后确认原 timeScale、Hunt pause 和角色控制权恢复；
- [ ] 再按一次 `P` 验证可重复播放；
- [ ] 截取 Game View，并确认 Console 没有新增 Error；
- [ ] 仅提交 Gal、GameFlow 与 GameEvent 相关文件，不纳入现存角色控制器工作区改动。

## 2026-08-22 执行记录

- `GalSequencePlayerProbe`：PASS；
- `GalFlowLifecycleProbe`：PASS；
- Edit Mode 资产与场景组成检查：PASS；
- Play Mode：P 热键、PausedOverlay 暂停、测试立绘、三句 NEXT、关闭清理与状态恢复全部 PASS；
- 截图：`Tests/Artifacts/GalPausedOverlay.png`；
- 活动场景在安装前已经存在未保存修改，因此安装器只把 `GalFlowRoot` 挂入当前内存场景，没有自动保存整张场景。保存前应由使用者确认当前场景中的其他修改。
