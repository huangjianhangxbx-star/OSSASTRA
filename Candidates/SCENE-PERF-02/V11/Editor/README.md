# V11：Editor Play 独立成本候选

九个 Native 源已整体离线编译，0 warnings / 0 errors；最终编译前确认最新 Player PID 48464 已退出。**尚未执行 Native，当前没有新的 Editor 成本或根因结论。**代码已冻结；Root 唯一 Unity / Git / 生产写者。

## 源码确认的候选成本

| 实际入口 | 已确认的行为 | 本轮如何观察 |
| --- | --- | --- |
| `Development/Environment/EnvironmentSimulationPanel.cs:67,193` | 折叠后仍每 .12 秒刷新控件 / 状态字符串 | 正常组保留；安静组只禁用此开发面板组件 |
| `Development/CharacterLighting/CharacterLightingPanel.cs:56,211` | 折叠后仍遍历参数、调用详细诊断、计算 TMP 首选高度 | 同上；若持有时钟租约则拒绝禁用 |
| `EnvironmentSimulation/Rendering/EnvironmentSkyBinding.cs:107,120` | 只有实际展开消费者才申请 384×192 预览 RT；PreviewReady 时每帧 Blit | census 读实际 PreviewReady / 请求数；禁用面板走原释放接口，不禁天空 / 光照后端 |
| `Development/SceneVisibility/RoomVisibilityLabPanel.cs:56` | OnGUI / 门窗线显示；`SceneVisibilityRoot.cs:234` 的诊断文本每 .25 秒且受 showDiagnostics 控制 | 只停面板 / 显示 bool；不关可见性求解 |
| `SceneVisibility/Rooms/Editor/RoomVisibilityInspectors.cs:93`、`Characters/Editor/CharacterRootEditor.cs:19`、`Development/Editor/CharacterFollowEditor.cs:30` | Play 时请求持续 Inspector repaint | 记录实际所选 Inspector；GameView 最大化使窗口隐藏，保持选择与锁定状态 |
| `GroundSurfaces/Editor/GroundSurfaceLabWindow.cs:56`、`Audio/Editor/BgmMonitorWindow.cs:23`、`GroundResearch/Editor/GroundWorkshopWindow.cs:159` | 自有窗口的 OnInspectorUpdate 刷新 / 重绘（工坊有条件） | census 记录窗口与实际 EditorApplication.update 订阅；不关闭窗口、禁插件或清空队列 |
| `GroundSurfaces/Editor/GroundSurfaceUpdates.cs:65` | 有变更才订阅 Drain；Play 时可能等 WaitReady | 只是已确认代码路径，不宣称实际正在运行或昂贵；不调用 ClearPending |

所有路径前缀为 `Assets/OSSASTRA/Scripts/`。上表是可测入口，**不是毫秒归属**。旧 SceneView.alwaysRefresh 对后台 Player 已属 negative，本轮不重试；不重试 Ancestry / SourceFamily。

## 复用关系与边界

- `EditorCpuTimeline.cs.txt` 复用 PERF01 `CpuTimeline.cs.txt` 的原始树读取器。唯一关键观测改变是 **profileEditor=true**，否则旧命令的 false 会漏掉要找的 EditorLoop / UI 工作。仅 CPU、非 deep、GPU profiling 关闭；30–40 个实际相机帧、最近 20 个已完成同 Editor session 帧，15 秒总超时；各原开关独立归还。识别实际 Main 与 RHI / Render Thread / GfxDeviceWorker，不把并行线程相加。
- `EditorNormal.cs.txt` 复用原 Baseline，Profiler recording / GPU / deep 必须关闭。原相机真实回调：至少 5 秒并连续 60 稳定帧预热，再记录 60 秒；100 秒总超时。实际 GameView 必须保持 1600×900，p50 / p95 / p99 / 超预算比例和原始间隔保留；GPU 不可用就报告 unavailable。`measuredConditionsStable` 必须为 true。采集器不写相机、天气、帧率或分辨率。
- `ReadState.cs.txt` 只读选择、Inspector 目标与锁定、所有窗口位置 / 最大化 / 焦点、实际开发面板、预览申请和有限编辑器回调名；只在采样前后执行，避免把 census 开销塞进正常窗口。
- 所有临时回调遇到退出 Play / reload / quit / 超时清理。不要在正常采样期间导入代码、dotnet 编译或按帧 MCP 轮询。

## Root 精确执行序列

所有 CodeFile 均通过现有 `Candidates/SCENE-LOOK-01/InvokeUnity.ps1 -CodeFile <file> -Receipt <唯一回执>` 执行（已有 skip_refresh），无需导入 Assets。以下仅是待 Root 执行的顺序，未代签任何实际结果。

1. 保留当前 author main、dirty、用户参数。先复用 `Candidates/SCENE-PERF-02/V11/CurrentState.cs.txt` 记录参数和原业务四 SHA，执行本目录 `ReadState.cs.txt`。不要重跑旧 M0 Snapshot、重建基线、保存原场或恢复旧文档默认。
2. 稳定 Edit 执行 **BorrowEditRoute.cs.txt**。它只从当前精确 normal `研究/可见性修复/KT-VIS-06/profiles` 只读复制四文件，创建全新 `研究/SCENE-PERF-02/v11-editor/<token>/commerce`；Pre-Start SavePath 必须为空。四文件与源的 SHA 一致后，才借用 author Commerce 的 saveDirectory。保留原场 GUID / 磁盘 SHA / Commerce GlobalObjectId / profileId / 原 dirty 和 route，**不保存场景、不调用 Undo / SetDirty、不读写 frozen-v2**。响应丢失先检查 `route-active.json`，不要重放。
3. Root 沿既有 prepare_editor 进入正常 Play。执行 **RestoreEditRoute.cs.txt**：在 Play 此命令严格只验证同一 author identity 与私有 SavePath，不会改运行中的 Commerce。若尚未初始化则本轮停止采集，先检查原服务状态；不回落正常业务目录。
4. 执行 **BorrowFixedView.cs.txt**。它要求上一步私有 route，沿现有 `lab.PlaceCurrent(lab.exteriorPosition)` 一次定位；只借用 `map.fixedTarget=lab.exteriorPosition`、`followParticipant=false` 和本 owner 相机输入 token。地图自身继续平滑 / 更新，无逐帧相机写入，世界 / 战斗 / 时钟 / 天气继续；**不写任何时钟天气数值**。若原 clockRunning=true，报告会保留事实，不能把环境变化中的两组当严格固定光照 A/B。
5. **A 当前布局**：ReadState → EditorNormal（等待唯一 JSON `completed=true`、measuredSeconds≥60、条件稳定）→ EditorCpuTimeline（等待 cpuDataAvailable / flagsRestored=true、errors 空）。CPU 采样有额外开销，不能以其 fps 代替正常采样；GPU 未取到不能写 0。
6. **B GameView 聚焦最大化 + 开发诊断停用**：执行 **BorrowQuiet.cs.txt**，再 ReadState → EditorNormal → EditorCpuTimeline。仅借用六个明确开发面板 enabled 和 MapResearchRoot.showOverlay / SceneVisibilityRoot.showDiagnostics / LabRoutes.showPanel；不禁对应世界根、正式 HUD、窗口插件、渲染 / 阴影 / 角色功能或质量。预检会拒绝活动面板输入、选中的开发 uGUI、角色面板时钟租约、体素总览租约或正在追踪的 flicker buffer，避免破坏现场。不存在 / 原已禁用项不虚构成本。
7. **RestoreQuiet.cs.txt** 按原动态 owner assembly + MVID + token 归还实际对象引用；不凭 ID 绑定新窗口。只归还当前仍等于自有写入的字段；外改让权。GameView 取消最大化后使用 Unity 原 docking 恢复，最后回原焦点；选择 / Inspector 锁定从未被写入。独立借用计时回调很小但仍属于本次观测，应披露；240 秒安全超时会自动归还。
8. **ReleaseFixedView.cs.txt** 归还同一 map 引用、仍由本次写入持有的字段和本 owner 输入。一次角色定位是 Play 测试夹具，不伪称业务历史已回滚；停 Play 才沿原 Editor 生命周期回到作者态。固定视角另有 600 秒安全超时。
9. Root 停 Play 并确认稳定 Edit 后，执行 **RestoreEditRoute.cs.txt**，仅当场 GUID / 磁盘 SHA / Commerce GlobalObjectId / profileId 都一致且当前 route 仍是同一私有写入时，归还原 route。它重新比较原四 SHA，但绝不恢复 / 覆盖原存档文件。再执行 CurrentState + ReadState，对比原参数、原四 SHA、dirty、选择、窗口、Inspector 与锁定；任何不同单独报告，不自动 MarkSceneClean、重开旧场或覆盖用户数据。

`route-active.json` 是跨 domain reload 的恢复依据，不能依赖旧动态程序集静态引用跨 Play。相反，Quiet / FixedView 都只在当前 Play 生命周期拥有原对象引用，并在 reload 前归还。公开 bool / Vector3 没有写者版本，无法检测外部改回同值的 ABA 历史；回执不声称这种完整追踪。

## 不应直接复用的旧入口

- `SetPlayerBuildRoute.cs.txt` 指向旧共享 `test-commerce`，不为本轮复制新私有目录。
- `Snapshot.cs.txt` 的旧 archive 已存在，不可重复创建；本轮复用已有现场与只读 CurrentState。
- `ScenePerformanceDefaultPlaytestIsolation` 只编译于非 Editor 的特定 Build 场，不保护 author main Editor Play。
- Player probe 的图形 WindowsPlayer / CLI / 来源断言不适合在 Editor 接管；本轮只复用其一次定位和固定 map target 窄接口。

A/B 是“整个可操作 Editor 开发工作集”的组合诊断；即使有差异，也不能把差值分摊给某个面板。以 CPU 树实际自身 / 等待成本选择下一项，再单变量。Editor 结果单列，不冒充稳定 60 的最终 Release Player 验收。
