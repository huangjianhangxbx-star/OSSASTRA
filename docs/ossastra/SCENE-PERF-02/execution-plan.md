# SCENE-PERF-02 Implementation Plan

**Goal:** 当前已认可画面下，正常Player规定路线稳定60帧，并单独验证扩展余量。

**Architecture:** 沿原RoomVisibilityModule、RoomPositivePass与相机scope。先测当前Player，再把稳定结构计划与当相机借用分开；观察矩阵、角色与门窗仍实时更新。没有可靠变化通知的外部写入保留必要验证或原路线。

**Tech Stack:** 当前团结2022.3.62t16、实际URP包、D3D11、Mono2x；不升级。Root为唯一Unity/Git writer，其他执行者只写隔离候选。

**Spec:** 本机`E:/迅雷下载/SCENE-PERF-02_稳定60帧与扩展余量_接续计划包_v1.0.zip`内主计划；原文只读副本在外部研究/SCENE-PERF-02/approved-plan。本文是执行清单，测量后将选定的下一垂直切片精化到真实方法，不把预测写成收益。

## Global Constraints

- 正常Player未限帧；1600×900、45°、原Ultra/材质/阴影/角色/门窗黑幕，单列Editor。临时vSync0/target-1只服务未限帧测量，退出归还。
- 每条路线P95与P99≤1000/60ms、超预算比例≤1%；保留最大帧和最长连续超预算，解释可复现>33.33ms长帧。P50≤12.5ms为独立余量目标。
- 室外和室内固定点各两轮≥60秒，连续真实操作两轮各≥120秒；1/2/4倍离屏登记与约2倍真实可见负载另表，不混合平均。
- 继承74fcae9；原42 tracked dirty及无主meta、原内存场景/材质参数/业务档保持。只Native编辑序列化资产，不读取Assets/Script或Assets/Scripts。
- 不删正式模型、降质、撤阴影/观察规则、冻结世界、永久借材质或引入新总管理器。连续两项无净收益则停止同层微调，回到实际关键路径。
- 每项先真实失败信号/语义合同，再最小实现、相关模块B级和同条件quiet筛选；最终限定C级路线/扩展/生命周期。不重跑无关玩法历史。
- 最终无60门槛不能标performance complete；允许保留最好已验证版本阶段交付。R11仅定向GitHub，禁强推/Gitee，不改人工规范或Excel。

## Task 1：M0与M1测量出口

**Files:** `Candidates/SCENE-PERF-02/Snapshot.cs.txt`；`Player/ScenePerformanceContinuationProbe.cs.txt`、`Player/ReadPlayerCpu.cs.txt`；Root本轮Build/Run候选。临时导入`Assets/OSSASTRA/Scripts/Development/ScenePerformanceContinuationProbe.cs`，交付前删除。

**Interfaces:** CLI `--scene-perf02-cpu <uniqueRunDir>`生成有界真实Player CPU raw；`--scene-perf02-run <uniqueRunDir>`正常采样；`--scene-perf02-source <approvedArchive>`启动前隔离原业务，`--scene-perf02-route exterior|interior`、`--scene-perf02-seconds <duration>`控制本次路线。实际Build/Run请求ID绑定源码指纹、完整主场copy、构建和质量。

- [x] 读新计划与最新两turn，核HEAD和原dirty；已有整链证据复用。
- [x] 执行Snapshot原场SaveAsCopy，仅外部新副本；新test-commerce隔离四文件。前置夹具map.yaw编译失败无副作用，改实际ActualYaw后成功。
- [ ] Runtime候选离线编译；Native只导入临时探针、prepare_editor验证Edit ready；构建当前完整主场副本，不保存原dirty。
- [ ] 先运行CPU诊断40–80实际相机帧；正常计时不与Profiler、图片读回、JSON写盘重叠。Editor从独立raw读取有owner marker的实际Player Main/RHI树，记录独占/包含与具名等待，恢复原Profiler设置。
- [ ] 同版未限帧正常Player补快速筛选基线；正式计时窗口开始后记录每帧姿态/屏幕和条件，不把暖机落定写成固定A/B。
- [ ] 产出一页`热点→可省工作→实测成本范围→风险→单变量方法`。没有取得的GPU/子阶段记unavailable，不能填0或直接相加。

正常统计必须由真实样本计算：

```csharp
double budgetMs = 1000d / 60d;
bool basePass = p95Ms <= budgetMs && p99Ms <= budgetMs
    && (double)overBudgetFrames / sampledFrames <= .01;
// 全部正式窗口样本参与；按相邻超预算帧计算最长连续段，冷启动另表。
```

## Task 2：M2首片——持久结构计划与相机借用分离

**Files:** 首选`Rooms/Rendering/RoomPositiveMaterialBuckets.cs`；真实失败夹具放`Candidates/SCENE-PERF-02/Static/`。待M1确认成本足够后才登记生产写入。

**Interfaces:** 保持既有`Prepare(RoomMaskFrame, RoomPositiveShaderResolver)`、`RefreshAdapters()`、`Restore()`、`ResolveOriginal(Material)`，调用者不增加新总管。内部候选为`EnsurePlan`生成持久Request/Swap/Keys/Written数组，`Borrow`只处理本相机必要验证/写入，`Restore`继续让权归还。

- [ ] 先实际跑稳定场景的结构重建/分配RED和现有Bucket语义。夹具必须调用实际Prepare/Restore，不用假的计数替代行为。
- [ ] 不变时复用集合、分类排序和计划数组；必要逐Renderer槽身份/每唯一材质内容验证仍保留。Source/Clone/Adapter CRC和污染修复不撤。
- [ ] 对加入/移除/重排/Full或Independent变化、槽/Shader/queue类别及排序数值变化、resolver输出变化、销毁/注销重生建立失效。不可用每帧新Frame数组引用作key。
- [ ] 外部仍引用借用clone时pin并拒绝叠加；正常CameraCleanup/EndScope保持幂等Restore，原Caster/辅助相机不继承本相机借用。
- [ ] 相关合同GREEN后固定240帧quiet+热点分项；保留超过噪声的实际收益，没净收益撤回本候选并写否定结果。

## Task 3：M2–M4剩余关键路径

**Files:** 按M1/M2结果选择`RoomVisibilitySight.cs`、`RoomPositivePropertyScope.cs`及它们实际静态登记者中的一个垂直切片；每次先补具体接口/失效图再写生产。不平行改三个入口。

- [ ] 优先选择已有明确作者的常规3D静态描述：注册身份/原槽/家族/owner几何按登记版本变化更新，门窗/观察与相机动态数据保持实时。无法可靠通知的外部对象走旧验证路线。
- [ ] 以真实不变稳态RED、变化/销毁/重绑定/启停/外部改写/双相机合同保护缓存；持续GC和缓存增长单列。
- [ ] 若MPB仍是大头，比较同语义组常量或持久适配数据中的一条候选；先测收益，不为兼容Batcher盲删MPB或让不同owner共享全局值。
- [ ] 需要缩小复杂路径时，由现有观察语义证明整面许可；跨区、窗口、动态/透明/屋顶保守保留。颜色镜外剔除不撤影子，不能复用旧屏幕遮罩。
- [ ] 每项单变量quiet筛选；连续两项无净收益转向更大组织切片。GPU仅在确实成为限制或尾部回退时定向进入，不叠新视觉。

## Task 4：最终路线与扩展

**Files:** 同一RuntimeProbe正常模式、原合法输入回放接口；`Candidates/SCENE-PERF-02/Expansion/`隔离装配；合并结果`docs/ossastra/SCENE-PERF-02/report.md`与执行链说明。

- [ ] 正常构建明确Development/调试状态；基线与候选使用同构建类型，不将关调试开销计为源码收益。
- [ ] 两轮静外/静内各60秒、真实连续操作各120秒；移动/八向转镜/切主控/进出房间/实际门窗，存在的攻击与掉落一并观察。不得每帧传送、重置动画或冻结世界。
- [ ] 分别记录P50/P95/P99/超16.667比例/连续段/最大帧、CPU工作与等待/GPU、原始慢帧及未覆盖项；重型分析与正常性能采样分离。
- [ ] 隔离场登记1/2/4倍保持屏内/portal/阴影/反射/照明同条件，身份独立，记候选数/重建数/更新量/内存；2倍实际可见负载另测，失败不隐藏。
- [ ] 通过预算后单独实际60Hz呈现检查；12.5ms余量与2倍负载分别判定。
- [ ] 有限像素/角色动态、窗墙黑幕、材质源和Caster、重开/切换/销毁/缓存释放；恢复正常route/用户dirty与最好已验证代码，清临时资产。
- [ ] 按真实状态交付：性能未达／当前60帧通过但余量待补／本轮通过。R11只含本任务自有源码与必要验证、说明，核远端SHA。

## 初始审查

已核对用户主计划M0–M5、测试矩阵和取消低质量捷径要求；没有设计语义缺口需要重新问用户。实现中的缓存边界由M1成本和真实失效合同决定，阶段报告须明确未完成项，不以局部GREEN替代60帧验收。

## v1.1 接续（2026-11-09）

Spec：`E:/迅雷下载/SCENE-PERF-02_v1.1_静态地形共享准备与60帧收敛_增量计划包.zip` 内01；已批准原文位于外部 `研究/SCENE-PERF-02/approved-plan/v1.1/`。继承M2B+M3+M6，原快照、隔离源、5秒/60稳定帧与固定观察目标、实际Player CPU读取器复用。以下替代旧候选优先级，不再次执行Ancestry/SourceFamily/旧Bucket缓存，也不依赖Cull后MPB回补。

- [x] M7：在`QueueBuild.cs.txt`/`RunPlayer.ps1`引入明确Development或Release配置身份，各稳定路径/manifest、同FrozenScene和Mono2x；非Development仅`BuildOptions.None`，核实内容门不删生产组件。实际同场正常室外≥60秒配对，计数不可用记unavailable。构建差值单列，不当代码收益。
- [x] M8：复用`Player/ReadPlayerCpu.cs.txt`补当前M6的64帧Development短采，列当前包含/自身/具名等待与前三项，不使用最初26ms排名；正常计时不与Profiler/编译/截图并发。
- [x] M9：只选一个受控地形结构切片（候选在`V11/StaticTerrain/`）。现有登记者拥有成员/槽/归属版本；唯一源内容必要验证保留；稳定登记和计划按实际变化重建，相机/观察/门窗/天气继续当帧。先真实变化失效/外部写者/生命周期合同，再生产单变量A/B；共享参数绑定必须在有效捕获时点完成。具体接口由M8后的单页成本表确定，实施前登记生产路径。
- [x] Editor独立项：候选`V11/Editor/`复用实际Editor Play采样，当前布局与GameView聚焦/最大化对照，精确归还窗口/选择/刷新/Inspector。只修实测自有诊断热点，不重做后台alwaysRefresh/Player实验、不禁正式HUD/世界或插件。
- [ ] 最终仅最终候选跑非Development室外/室内各两轮60秒、动态两轮120秒、1/2/4倍登记、约2倍可见及真实60Hz；分别判Player、Editor、余量与画面。保留真实慢帧。M10 WorldColor/同版URP窄入口只作为有最新收益和依赖证据的后备，不默认Embed或改管线。

当前v1.1入口红信号为已测M6正常Player P95/P99=21.2036/22.4031ms，超16.667ms门槛；不是缺测试或功能崩溃。任务仍为未完成，不以新文档或代码数量表示通过。


2026-11-09阶段出口：M9完整片及唯一MaterialBinding后备已结束；保留实测较优M9、后备停止。六轮同测量构建静外/静内2x60和动态2x120已完成；均measurement pass但60/余量未达，扩展与实际60Hz仍未签，整体目标不标complete。普通试玩修复另构建并真实两次启动退出通过；Native清理4脚本/10过期场景，原场dirty未保存。R11按自有源码/依赖/证据定向发布。
