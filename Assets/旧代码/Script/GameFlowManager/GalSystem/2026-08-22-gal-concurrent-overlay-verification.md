# Gal ConcurrentOverlay 真机验收记录

## 验收环境

- 引擎：团结引擎 `1.10.0`（兼容编辑器 `2022.3.62t12`，revision `ab02e98c9779`）
- 场景：`Assets/Scenes/测试场3_电车45.unity`
- Unity MCP：Funplay，端口 `28603`
- 验收日期：2026-08-23
- 场景保护：工作树中的场景原本已有用户修改；本任务没有调用 `SaveScene`，也没有写入场景或 Character 源码。

## TDD 证据

首个真实 RED 来自扩展后的组成验收器。Unity 编译器只报告预期缺口：

```text
CS0103: The name 'VerifyConcurrentEditModeComposition' does not exist in the current context
```

补齐验证器后，Unity 重编译为零错误、零警告；EditMode 组成验证返回：

```text
PASS gal edit mode composition
```

## Editor Probe 回归

Unity MCP 在真实编辑器域中依次执行并返回：

```text
PASS gal auto advance timing
PASS gal sequence player
PASS gal flow lifecycle
PASS gal concurrent dispatcher
PASS gal dialogue view
PASS gal edit mode composition
```

## PlayMode 验收

Runner 在真实 PlayMode 中按以下顺序自动执行：

1. `O` 启动 Concurrent A；保留第二次 `O` 输入链路并确认没有新增活动会话或队列项；随后通过活动场景的真实 `GameFlowArbitrator` 以同一 A、同一 `debug.concurrent.a` request key 再次请求，精确取得 `GalStartResult.DuplicateIgnored`，排除输入未消费或返回 `Busy` 等假阳性。
2. `Shift+O` 将 Concurrent B 加入容量为 3 的 FIFO 队列，队列数变为 1。
3. A 播放期间持续注入移动输入；角色位置发生变化。
4. A 自动完成后 B 自动开始，事件顺序严格为 A → B，队列回到 0。
5. B 活动时用 `Shift+P` 请求 Fullscreen；B 收到 `Preempted`，队列保持 0。
6. Fullscreen 完成后等待超过一个旧 B 时长；Concurrent 没有恢复。
7. 随后完整回归 `P` PausedOverlay 与 `Shift+P` FullscreenExclusive 的三句确认、关闭和恢复流程。

最终 Console 原始 PASS：

```text
PASS Gal ConcurrentOverlay + PausedOverlay + FullscreenExclusive PlayMode verification
```

### Gameplay 不受 Concurrent 影响

| 状态 | Concurrent 前 | Concurrent A/B 期间 | Concurrent 结束后 |
|---|---:|---:|---:|
| `Time.timeScale` | `1` | `1` | `1` |
| `HuntFlowDataBus.isPaused` | `false` | `false` | `false` |
| Move authority | `Allowed` | `Allowed` | `Allowed` |
| 玩家移动 | 可移动 | 实际位置已改变 | 可移动 |

## Installer 幂等与组成回读

Task 5 已连续运行 Installer 两次并回读相同组成：第二次没有新增 GameObject、Component 或 Prefab modification，`sceneChanged=false`、`sceneSaved=false`。本任务再次验证了：

- Concurrent A/B 的 ID、模式、两行内容与时长覆盖正确；
- View 的精确层级、八个引用、CanvasGroup、Raycast 与进度条配置正确；
- Root 队列容量为 3，Trigger 的 A/B 引用正确；
- 活动场景仍只有一个 GameFlowRoot、GalFlowRoot、GalDialogueView 和 Gal Canvas；
- 场景 GalFlowRoot 仍连接到 `PF_GalFlowRoot`，既有 P/Shift+P 回归仍通过。

## 截图

- `Assets/Script/GameFlowManager/GalSystem/Tests/Artifacts/GalConcurrentOverlay.png`

目视结果：通讯卡片位于左下角，未遮挡玩家和准心；头像裁切可辨认，姓名、两行正文区域和自动推进进度条层级清楚。
