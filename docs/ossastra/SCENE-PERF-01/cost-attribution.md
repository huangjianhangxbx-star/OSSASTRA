# SCENE-PERF-01 成本归属与定向修复记录

## 当前结论：本机Player该机位通过30FPS预算，尚非60FPS认证

2026-11-08接续原任务。包含最新拒绝帧guard的实际Player已完成240帧：主相机p50 **24.5944ms**、p95 **26.1613ms**（中位约40.7FPS），最大30.9257ms，0/240超过33.33ms；GPU240个正样本p50 **10.6680ms**、p95 **12.0422ms**。隔离/路由/源档保护及RuntimeError 0均通过，并另有OS进程`exitCode=0`证据。这是本机本次机位通过30FPS预算，不是全设备、多机位或60FPS认证。

早期仅包含前两项修复的Player为36.2543/41.6961ms（约27.6FPS），不称完整无修复基线。两次Player的起始快照均在预热前、相机高度约收敛0.08m，没有每个正式采样帧的姿态证明，因此保留为两版实际运行观测，不宣传成严格逐帧固定像素A/B。早期Player未取得的OS退出码也不能用本次exit0倒填。

固定Editor三组已补齐：control为74.4921/83.8869ms、truth-only为49.3932/69.3602ms、同轮optimized为45.8393/51.4691ms，均240帧且pose/屏幕比较通过。后续HashSet、Truth跨Execute CRC及保守镜外Truth跳过也有局部合同；此前仅前五项对应固定240帧为**40.3753/45.8443ms**，中位约24.8FPS。Native在当时非默认阶段加全槽已提交提前返回后，实际固定240帧为**36.0534/39.1339ms**，中位约27.7FPS，240/240超33.33ms，该阶段仍未达预算。此前44.4909/48.4810ms且镜头转过180°的采样仍只保留为历史观测。

Native新增室外及一个室内机位的局部GPU遮罩比较后，源码开关现已`nativeTruthBucketsEnabled=true`；此前false/手工true为下文保留的历史实验状态。Editor固定240帧仍为**34.2965/37.6655ms**（约29.2FPS），220/240超过33.33ms，不能称Editor已稳定30FPS，距离60FPS也很远。被预先排除的动态区域、最终颜色、多角、baked occlusion与共面深度tie仍未全面验证，不能称普适视觉等价已解决。

同机位ON/OFF/ON曾把主要额外CPU成本收敛到**正向可见性合法世界重建整链约36ms**，而非Present等待占满一帧。该历史整链包含依赖的角色/环境重建，不能将36ms统称纯C#可见性算法或最新Player耗时，也不能把OFF图像当交付方案。

复用原现场和独立测试档，不重复备份/接手。采样主场为1600×900、D3D11、RTX4080 SUPER、VSync 1、targetFrameRate=-1、45°俯角。quiet只表示排除并行PBZ和完整Profiler，不代表镜头固定；不在编译或FrameDebugger冻结状态下计时。用户曾要求暂停重型验证以玩PBZ，该阶段不作为性能验收；现已恢复接续授权。

## 早期非严格可比的Editor观测

| 阶段 | 实际主相机采样 | p50 / p95（ms） | 结果与边界 |
|---|---|---|---|
| 原无完整Profiler基线 | 30暖＋90帧 | 69.5330 / 76.6840 | 90/90超预算；均值70.704ms |
| 仅Truth首用去重，quiet | 30暖＋240帧 | 47.0507 / 52.2886 | 240/240超预算；均值48.0095ms |
| 再加Bucket CRC缓存，quiet但镜头变化 | 30暖＋240帧 | 44.4909 / 48.4810 | 240/240超预算；均值45.4150ms；不是固定机位收益复测 |

三组帧间隔数值呈下降，但现有证据不足以将下降严格归因给两项修复。后cache的`baseline-20261107-162131-2218386.json`实际记录：cameraViewStart位置约(-3.5,18.5,-5.7)、yaw225°；cameraViewEnd位置约(-28.5,18.5,-30.7)、yaw45°，采样中转过180°。原基线与首项quiet的旧采样器没有cameraViewStart/End字段，也不能确认固定机位。三组不能作为同机位修复开关A/B或计算独立提速百分比；局部合同通过和整体仍持续RED是分别成立的结论。

各次导出的绘制统计也不恒定：原基线Batches/SetPass/casters中位数为4749/2182/1630；首项quiet为4004/2132/1196；两项后quiet为4817/2252/1630。保留原始样本，不把统计差异解释成已证明的网格删除、阴影减量或精确独立收益。后续固定对照见下文，与这组三次历史观测分开。

对应原始文件均在`artifacts/SCENE-PERF-01/`：

- 原基线：`baseline-20261107-132843-9978899.json`。
- 首项quiet240：`baseline-20261107-150910-2094295.json`。
- 两项后quiet240：`baseline-20261107-162131-2218386.json`。
- 排除：`baseline-20261107-150137-4709880.json`。此轮受并行使用/采样干扰，30秒超时、仅191帧、p50 134.0729ms；不并入quiet结果，不用它宣称修复回退。

## 早期Player：仅前两项修复，保留为历史运行观测

Build Queue请求`2af23c033e71457c94f7e53e4b1c72cd`已实际`Succeeded`，0错误、27警告，2710.2209秒，539,029,983 bytes（约539MB）。当时的`player-build-latest.json`收据记录构建采用独立场景副本，原主场文件SHA前后一致、原dirty仍保留；该latest文件会随后续构建更新，此处保留本次历史结果，不指代最新构建。

本轮目录`Player_20261107_191614_4021301/Run_20261107_200220_1649889/`下`result.json`记录：

| 项目 | 实测 |
|---|---|
| 实际内容与隔离 | 224 chunks、8 houseParts、274 bindings（239 lightingOnly）、2 participants；preStartIsolation、isolatedRouteValid、sourceArchiveUnchanged、roomMaskActive为true |
| 采样与帧间隔 | 30帧预热＋240帧；p50 36.2543ms、p95 41.6961ms、均值36.9213ms；240/240超预算 |
| FrameTiming CPU | 240个正样本，p50 36.3131ms、p95 41.8074ms |
| FrameTiming GPU | 200个正样本，p50 14.5521ms、p95 16.7629ms；完成时序可能滞后，不填补缺样本 |
| Batches / SetPass中位数 | 4750 / 2182 |
| RuntimeError / 未支持房间材质 | 0 / 0；roomMaskError为空 |
| 正确性与性能 | `pass=true`；两个perfWithinBudget均false，不能签性能完成 |
| OS退出状态 | 未捕获，不作0退出码或完整退出验证结论 |

Player起始快照在预热前，相机y=18.5766；结束y=18.4977，yaw同为45°。它说明初始相机收敛，不提供每个正式采样帧的pose校验；不能称为逐帧固定机位Player对照。截图与报告保留在同一Run目录。

Player报告的构建sourceSha与构建收据一致，报告内部测试源档4文件Before/After SHA也一致。`after-exit-archive.json`另有`sourceUnchangedAfterExit=false`，但该外部检查发生在Editor又进入Play并写测试源之后，因此不把它归因给Player。正常业务checkpoint的4文件SHA已由主执行者实查一致；测试源、独立运行档和正常业务档三者不能混为同一个保护结论。

## 最新guarded Player：240帧预算与OS退出均取得

Build Queue请求`dfef5596642a4d50a501605c5e17f4c8`为最新guard后的实际构建：`Succeeded`，34.9964秒，0错误、43警告，`originalSourcePreserved=true`；构建前后原主场磁盘SHA均为`49E1A4F265E65153CB2D91D6C0379D9C0691FFE2B5C375529CB8B83C7A3644E8`。该次`player-build-latest.json`收据对应保留目录`artifacts/SCENE-PERF-01/Player_20261108_000013_9118486/`。

实际运行证据位于其`Run_20261108_000246_9745637/result.json`与`exit.json`：

| 项目 | 实测结果 |
|---|---|
| 采样 | 30帧预热＋240完整采样，实际新版Native已构建进入此Player |
| 实际主相机间隔 | p50 24.5944ms、p95 26.1613ms、均值24.7740ms、最大30.9257ms；0帧超33.33ms |
| GPU | 240个正样本，p50 10.6680ms、p95 12.0422ms；不与CPU相加 |
| Main Thread recorder | 240个样本，p50 24.6154ms、p95 26.1737ms；线程总范围可含等待，非纯managed耗时 |
| Batches / SetPass中位数 | 4126 / 722 |
| 隔离与正确性 | preStartIsolation/sourceArchiveUnchanged/isolatedRouteValid/roomMaskActive均true；RuntimeError 0、pass=true |
| 性能预算字段 | perfMedianWithinBudget/perfP95WithinBudget均true；本机本次机位通过30FPS预算 |
| OS进程退出 | 独立exit.json记录exited=true、exitCode=0，构建请求ID与本次一致 |

本次Player起始快照也在预热前，cameraY 18.5775→结束18.4977，yaw同45°；早期Player为18.5766→18.4977。不能仅凭起止值把任一Player称为逐帧固定机位；约40.7FPS与早期约27.6FPS是两个实际版本观测，旧版已经含前两项修复，不是“完全无修复”基线。正式Editor固定三组与整体开关对照提供另一类证据，不能把不同来源混算独立收益。

资料与可执行文件继续本机保留，本次结束不以删除有效Player来掩盖未测边界。较广设备、所有室内/窗洞/多角、被动态排除区、baked occlusion、共面tie及60FPS目标仍未认证。

## 固定Editor对照：三组已取得，后续仍持续RED

这些采样器只取得原MapResearchRoot输入捕获租约，不钉死世界状态；预热30帧后，对每个正式帧校验pose和屏幕。相机约(-28.5,18.49767,-30.7)、俯角45°/yaw45°、FOV45、1600×900；各组起止环境均12h、时钟停、太阳高度60°，质量Ultra、URP3D renderScale1、MSAA4、VSync1、shadowDistance150。环境/质量起止一致不是逐帧中间值证明。

| 组别与证据文件后缀 | 状态 | p50 / p95（ms） | Batches / SetPass / casters中位数 |
|---|---|---|---|
| 先前optimized固定观察 `200507-9716645` | 240帧，fixedViewComparable=true，cameraCaptureReleased=true | 46.0027 / 68.5857 | 4748 / 2182 / 1630 |
| 严谨对照control `201952-3253321` | 精确HEAD旧Refresh＋Truth逐槽重复；240帧，两标志true | 74.4921 / 83.8869 | 4748 / 2182 / 1630 |
| 严谨对照truth-only `202402-3284504` | 保留旧Refresh，只启Truth首用去重；240帧，两标志true | 49.3932 / 69.3602 | 4749 / 2182 / 1630 |
| 同轮optimized `202654-5340489` | Truth首用去重＋Bucket CRC；240帧，两标志true | 45.8393 / 51.4691 | 4748 / 2182 / 1630 |
| 再加surface HashSet `204956-0330640` | 240帧，两标志true，仍240/240超预算 | 45.8894 / 49.2690 | 4748 / 2182 / 1630 |
| 再加Truth跨Execute CRC `210135-6598217` | 240帧，两标志true，仍240/240超预算 | 43.1444 / 51.3614 | 4748 / 2182 / 1630 |
| 再加保守Truth视锥预筛 `214246-3608475` | 240帧，两标志true，仍240/240超预算 | 40.3753 / 45.8443 | 4127 / 1560 / 1630 |
| 非默认Native候选ON `231058-6654617` | 手工实验；240帧，两标志true，仍超预算 | 39.4289 / 43.5330 | 4125 / 722 / 1630 |
| Native完整槽已提交提前返回 `232110-0006442` | 手工实验；240帧，两标志true，仍超预算 | 36.0534 / 39.1339 | 4124 / 722 / 1630 |
| Native＋Validate读取复用 `233920-3356597` | 240帧，两标志true；220帧超预算 | 34.2965 / 37.6655 | 4124 / 722 / 1630 |

表内已完成固定样本的pose/screen不匹配帧均0，采样后输入捕获已释放。文件位于`artifacts/SCENE-PERF-01/fixed-view-baseline-20261107-<表内后缀>.json`；最新完整文件名为`fixed-view-baseline-20261107-233920-3356597.json`。三组control/truth-only/optimized质量字符串完全一致；truth-only有1个batch微差，不能声称逐draw完全一致。对照准备冻结了4个测试源文件，但control有一次采样前自动退出再进入Play，可能更新测试session元数据；不能声称所有before bytes完全一致。后续继续核对绘制、pose、质量和隔离路由，不通过改观察权限或画质换取数字。

## 整链ON/OFF/ON：主要额外成本约36ms，Present等待很小

`whole-chain-contrast-20261107-212307-3093922.json`完成每段30暖＋120帧，共450实际相机帧，`completed/comparable=true`；三段pose/screen不匹配均0，观察者、环境、质量一致，`positiveRenderingRestored/cameraCaptureReleased=true`、errors为空。1600×900、Ultra、4xMSAA、阴影距离150、XH_URP3D/renderScale1保持不变。

| 段 | 主相机p50 / p95（ms） | CPU-main p50 | CPU-render p50 | PresentWait p50 / p95 | Batches / SetPass |
|---|---|---|---|---|---|
| ON-1 | 42.5641 / 50.9369 | 40.8184ms | 10.4047ms | 0.0002 / 0.0003ms | 4747 / 2181 |
| OFF | 5.6725 / 6.2846 | 4.3322ms | 1.2613ms | 0.0002 / 0.0003ms | 2422 / 149 |
| ON-2 | 42.3533 / 50.0082 | 40.6526ms | 10.3060ms | 0.0002 / 0.0004ms | 4747 / 2181 |

CPU-main的ON减OFF约36.49/36.32ms，两次ON返回相近水平，支持主要成本位于这条合法重建整链。OFF同时撤去依赖的环境/角色重建且改变图像与功能，仅用于归属，不接受为优化成品。该对照在Truth视锥预筛之前；它不证明最新全部修复后的整链成本仍精确等于36ms。

FrameTiming均按非零、全局唯一frameStartTimestamp去重，三段各120个有效CPU-main样本；PresentWait为0只有与有效正CPU-main同行才接受，因此这里的微小值不是缺数据填0。它不支持“这一帧主要在等Present”的解释，也不能泛化为系统没有任何其他等待。GPU正样本ON-1/OFF/ON-2为106/120/105，p50分别16.1239/1.5401/16.1802ms，完成时序存在滞后，不与CPU时间相加。

## 211341阶段CPU树：工作归属与RHI等待必须分开（保留）

`cpu-timeline-20261107-211341-6292692.json`取得30个相机帧和20个Main树，`flagsRestored=true`；`cpu-existing-rhi-211341.json`离线重读同组既有帧的实际RHI Thread，没有新开采样。两组invalid/cross-frame-overlap均0。

| 同组20树 | 平均线程帧时长 | 具名Wait/Sleep/Idle自身 | 非等待/未分类自身 |
|---|---|---|---|
| Main Thread | 48.6641ms | 0.0497ms | 48.6144ms |
| RHI Thread | 48.6642ms | 35.8813ms | 12.7829ms |

RHI的`Semaphore.WaitForSignal`自身均值35.8438ms，不是35ms有效渲染工作；剩余12.7829ms也不能自动称全部有效工作，仍可能含未按名称识别的等待。Main与RHI并行，不能将48.6641＋48.6642相加。结合整链ON/OFF，Present等待很小而Main构造/原生提交路径占用明显；RHI信号等待具体由生产者、GPU或调度的哪一种造成仍未知，不能凭marker名武断归因。

Main较大平均self：`Inl_RoomPositive.PreviewCoverage.Build`3.2904ms、同名命令执行标记2.8721ms；`Inl_RoomPositive.Permission.Build`3.0918ms、同名命令执行标记2.7070ms；PrepareBeforeCulling self 3.1163ms、Buckets.Prepare 3.0205ms、InternalStartRendering 3.0921ms、StaticSurfaces 2.8184ms。它们是树的self归属，包含C#之外的原生工作；不跨嵌套层或跨线程相加当作新的帧时长。采样发生在最新Truth视锥预筛前，不能直接当其后CPU树。

## 两项后阶段采样：剩余工作仍需归因

`gpu-stages-20261107-200757-0704373.json`完成120实际相机帧，以下inlineCPU均119个正sample-count观测；Recorder状态已恢复，探针没有改变Profiler GPU开关。

| 阶段 | inlineCPU p50 / p95（ms） |
|---|---|
| UniversalRenderTotal | 40.2940 / 42.9148 |
| RoomPositive.PrepareBeforeCulling | 7.6314 / 8.1203 |
| RoomVisibility.BuildPositiveFrame | 5.6704 / 6.1532 |
| PreviewCoverage.Build | 3.7781 / 4.1482 |
| Permission.Build | 3.7267 / 4.1407 |
| Buckets.Prepare | 2.8673 / 3.1119 |
| DrawTruth.MaterialCopy | 2.8482 / 3.1205 |
| PropertyScope.Apply | 1.8342 / 2.0783 |
| DrawFinalWorld | 1.5830 / 1.8226 |
| AddPositiveSurface.Dedup | 1.0221 / 1.0918 |
| RefreshAdapters | 0.9443 / 1.1374 |
| ApplyEnvironmentTag | 0.1552 / 0.1798 |

这些是嵌套/并行阶段，不能逐行相加当帧时间。PreviewCoverage及Permission各有117个正GPU样本，p50分别3.3075、1.4920ms；其余无正GPU计数的阶段继续标unavailable，不能读默认0。CPU提交与GPU阶段也不能相加。该时点`RoomVisibilitySight`仅新增五个细分sampler，没有算法调整；后续实际细分和优化记录保留如下。

`gpu-stages-20261107-202959-7237388.json`的119个正inlineCPU观测：StaticSurfaces p50 3.9136ms、FinalizeMasks 1.4182ms、PortalCore 0.1672ms、CameraWindow 0.0653ms、DynamicSurfaces 0.0725ms；Dedup 0.9872ms（每帧412次调用），TruthCopy 2.7997ms、Bucket.Prepare 2.8456ms。Static范围包含Dedup，不能把两者重复相加；后续按这份证据建立局部合同，不把Portal或CameraWindow先认作最大热点。

## 局部合同记录：语义通过不等于整场性能通过

### 1. Truth每次Execute内按完整Key首用刷新

实际主场5帧预热后观测10帧，修改前TruthCopy为1826次，超过实际材质Key预算164，合同RED；修改后155次≤164，10帧合同GREEN，相关Recorder开关均已恢复。不是根据renderer数量估算复制次数。

完整Key含Source引用、Kind、CutRoof、Permission、Shaded、EnvironmentFull。每次Execute开始清空`used`，同Key仅首用复制材质并设置派生状态；Owner全局、材质槽和每槽DrawRenderer照常提交。preview、permission、shaded分别隔离，跨帧继续刷新，不把动态源永久视作静态；保留原许可、深度、屋顶Caster和室内灯关系。

证据：`truth-refresh-contract-20261107-144704-4420982.json`（RED）与`truth-refresh-contract-20261107-150043-1353551.json`（GREEN）。

### 2. Bucket源/Clone/Adapter原生CRC缓存

针对真实生产私有`Refresh(Bucket)`，使用完全临时材质做三轮、每组250次的稳定源/每次变化源对照。原实现稳定/变化中位耗时5.6469/5.5128ms，ratio **1.0243**，16项中性能项RED；修改后0.8724/6.9200ms，ratio **0.1261**，**16/16 GREEN**。变化组每次包含一次SetColor；这是有限fixture实测，不是整场提速比例。

缓存同时核对Original、Clone、Adapter三份原生CRC。任一变化或新建/Adapter目标Shader重指均重复制；私有队列仍同步，先同步队列再保存副本CRC。覆盖颜色、纹理引用、纹理ST、keyword、同类opaque队列、GI、instancing、doubleSidedGI和源Shader变更；借出Clone的颜色/keyword污染及Adapter颜色污染均实际恢复通过。Renderer换入/换出、冲突pin、MPB及原Shader Caster合同不改。

`Material` dirty count在本机对已改变的原生Copy未递增，不能作为“复制已经跳过”的证据；使用真实Refresh耗时及上述变化/污染合同，明确保留这个限制。

证据：`bucket-refresh-contract-20261107-161205-2632905.json`（RED）与`bucket-refresh-contract-20261107-161740-3004627.json`（GREEN）。

### 3. 成功表面成员HashSet去重

旧路径与新路径的真实私有`AddPositiveSurface`语义fixture均16/16 GREEN：`surface-dedup-contract-20261107-204329-7268048.json`、`surface-dedup-contract-20261107-204954-8609445.json`。每次Build同步清List/Set，成功Add后才登记，保留firstWins、被拒绝后重试及Unity对象边界。该合同仅验证实际私有方法和对应scratch清理，不是完整BuildPositiveFrame生命周期或新的性能阈值；不能把“旧版也是GREEN”改写成曾取得性能RED。

### 4. Truth跨Execute源/副本CRC缓存

真实私有DrawTruth在临时Pass/Renderer/material上执行，`used.Clear/cmd.Clear`模拟新Execute，命令不提交。原版稳定/变化中位4.0364/4.0228ms、ratio1.00338，20项中性能项RED，见`truth-cross-frame-contract-20261107-205749-7372004.json`。

中间版`truth-cross-frame-contract-20261107-210134-4505425.json`虽然ratio0.33495，但副本keyword/派生深度污染、permission与返回变体共4项语义失败，仍RED，不作通过证据。最终`truth-cross-frame-contract-20261107-210741-6287866.json`稳定/变化1.6521/4.9931ms、ratio0.33088，20/20 GREEN，临时资源清理完成、dirty保留。完整Key、源CRC和全部派生参数后的副本CRC用于失效，Owner全局仍逐draw上传。此合同不是字面跨真实帧、GPU图像、精确Copy调用次数或整场验收。

### 5. 仅标准3D Truth的保守视锥预筛

`truth-frustum-contract-20261107-213829-0186715.json`为原路径RED（16项中4项失败）；`truth-frustum-contract-20261107-214245-1470057.json`为16/16 GREEN。实际私有DrawTruth确认完全侧外/背后/远裁剪外不再提交，触边/跨near平面、移动返回及镜头旋转后应保留项不误删；ThinTop排除族仍提交，源材质/renderer引用及shadow/light状态不变，临时资源清理完成。

实现只在每次Execute用当前主相机更新平面，对已知标准Lit/VoxelLit/SimpleLit/Unlit的MeshRenderer，用保守合并并扩张的有效bounds跳过完全镜外Truth；不删表面登记、不改变FinalWorld或Caster，不处理2D/Spine/Upright/ThinTop等族。该fixture命令从未执行，证明的是提交合同，不是GPU像素等价或整场速度认证；最新固定240帧取得40.3753/45.8443ms及更低Batches/SetPass，但仍约25FPS，需要继续。

### 未保留实验：同scope的Truth标量全局命令去重

`truth-global-state-contract-20261107-221709-1039117.json`为实际私有DrawTruth＋未执行CommandBuffer字节合同RED：零Owner重复绘制的命令增量仍为52 bytes。实验版`truth-global-state-contract-20261107-222029-7629284.json`为10/10 GREEN，同状态第二次增量降至16 bytes（16/52≈0.3077）；跨scope/CommandBuffer仍重新写标量，正Owner的矩阵与extent完整数组仍逐次上传。该证据只说明真实命令字节减少，不是GPU像素或整场提速证明。

实验版主场固定复测`fixed-view-baseline-20261107-222154-5518640.json`完成240帧，fixedViewComparable/cameraCaptureReleased均true，p50 **40.7449ms**、p95 **45.7740ms**，240/240超预算；Batches/SetPass/casters中位数仍4127/1560/1630。对比此前保留方案40.3753/45.8443ms，没有明确性能收益。

因此已精确撤回该42行生产实验，该阶段只保留前五项修复；以上GREEN描述的是已撤回实验的局部合同，不能改写成当前生产仍启用该优化。保留失败/通过合同与整场样本，避免仅凭局部字节改善继续累积复杂度。回退后的新整场结果若尚未取得，应标未知，不能将实验版样本冒作回退后复测；后来的Native为另一项独立记录。

### Native早期非默认阶段：GPU局部遮罩证据与实际性能（历史）

`truth-native-20261107-230934-1528143/result.json`及同目录原始RGBA8读回，实际完成Native OFF/ON/OFF三阶段，每阶段12个主相机帧；同一个1600×900室外机位，镜头未变，报告记录Native桶路径实际运行。比较对象是生产`previewCoverage`和`labelPermission`，不是最终彩色画面。

比较前按所有非StaticEnvironment表面在三阶段的屏幕AABB并集、外扩8像素且保守排除两种Y方向，先定义动态排除区，不根据差异位置事后剔除。每组排除758,544像素（全图约52.68%），剩余**681,456像素**。两张遮罩各三组配对、共六组比较，在剩余区域RGBA字节均严格0差；全图并非0差：OFF-vs-ON的preview有5像素差，permission最多212像素差（另一配对206），均落在预先排除区；OFF/OFF的permission全图也有38像素动态差。

这证明当前机位的保留区域遮罩一致，**不能称全图等价**，不能覆盖被排除的约半幅画面、最终颜色、室内、窗洞或多角。报告`pass=true`只按其明确的局部比较合同理解。

候选首次ON固定采样`fixed-view-baseline-20261107-231058-6654617.json`为39.4289/43.5330ms，SetPass中位722、Batches4125、casters1630。随后定位标准Native槽已经提交却仍进入手工fallback的重复路径，完整槽已提交时提前返回，未完整覆盖时继续原fallback；再采`fixed-view-baseline-20261107-232110-0006442.json`取得36.0534/39.1339ms（约27.7FPS），240/240超预算，SetPass722、Batches4124、casters1630。

后一次FrameTiming的CPU-main/render p50分别34.3696/7.0706ms（均240个有效样本）；GPU222个正样本，p50 14.6483ms，完成时序可能滞后，不将三个时间相加。ProfilerRecorder名为Render Thread的计数器仍未取得正值，不是0；它与FrameTiming render字段是不同来源。

截至232110阶段，默认开关仍false、Play临时true仅用于实际采样；230934遮罩比较早于完整槽提前返回修复，当时修复后重测及室内/窗洞/多角和新Player尚未取得。该历史结果不能将SetPass下降或单机位局部遮罩GREEN延伸成性能/视觉问题已解决；之后新增证据见下一节。

### Native后续有限验证、默认启用与拒绝帧保护

完整槽提前返回后重采`truth-native-20261107-232437-3048913/result.json`：室外OFF/ON/OFF中Native实际运行98桶/1544登记槽，两遮罩各三组比较的保留681,044像素均严格0差；其余758,956像素仍为预先动态排除区，全图并非0差。

`truth-native-20261107-232905-3549656/result.json`覆盖一个室内机位，ON实际53桶/786登记槽、seedVolumeCount=1；labelPermission三组配对在保留**729,208像素**均严格0差，排除710,792像素。室内previewCoverage三阶段均未生成，报告明确`not-produced / not certified`，不能将其0计数当作preview等价。最终颜色、多角、所有窗洞变化、baked occlusion和共面深度tie仍无完整覆盖。

真实临时私有方法合同`native-plan-contract-20261107-234623-1159291.json`为14/14 GREEN：整桶拒绝非static/非Partial、混合或未准备槽、不同EnvironmentFull/WorldLookEnvironment、独立DrawMask、LOD、ShadowsOnly、submesh数不匹配、相机layer外和源pass禁用；确认还原源材质及清理。它不执行Native/GPU，不能替代上述像素边界。

Validate读取/分类结果复用的前后合同`validate-contract-20261107-233527-9153994.json`和`validate-contract-20261107-233918-1080396.json`均11/11 GREEN，保留mixed/unknown/17槽/非Lit顶盖/非法Owner的拒绝及空mesh/null Renderer边界，不修改live frame或registry。固定复测`fixed-view-baseline-20261107-233920-3356597.json`取得34.2965/37.6655ms，GPU225个正样本p50 14.2787ms，仍220/240超33.33ms；不是完整Profiler或已达标证明。

只读审查曾发现Validate已拒绝的帧仍在Execute的try之前构造Native计划，null/超4096表面可能逃逸或额外遍历。已在源码修正：仅ready帧Build，其余Clear；Build自身另守卫null/4096，Clear归零计数、available并清旧桶映射。`native-plan-contract-20261107-235934-9761541.json`已16/16 GREEN，新增null surface与4097项surface两例确认清除旧计划且不抛异常；它仍是私有Build方法合同，不是整段GPU执行证明。NativePreview先于sky/FinalWorld并清回主颜色深度；Permission保留最终深度且按environment真再假；Owner继续由既有Renderer/slot MPB提供，cmd.Clear不跨未归还的RenderCommandScope。

当前默认Native为true。请求`21aece86a9d0488793d0287e412345bb`曾35.6213秒成功（0错、43警告），目录`Player_20261107_234905_6198962`，但它未运行且早于拒绝帧修正，不冒作最新guard验证；之后另有dfef构建及本次实际运行，如上所列。限定源码审查在guard修正后未发现其他blocking；baked occlusion/共面tie继续保留为未覆盖边界。

## 本轮清理与业务保护

`cleanup-owned.json`及`cleanup-owned-receipt.json`记录Native清理已删除本轮5份临时Build Scene与`ScenePerformancePlayerProbe.cs`，有效Player与研究证据仍在本机。普通业务route已归还`E:/WORLDCREATOR/XingHaiHuiLang/研究/可见性修复/KT-VIS-06/profiles`；原dirty=true在清理后仍true，`sceneSaved=false`，没有为清理覆盖用户未保存现场。

清理记录的主场SHA仍为上述`49E1...64E8`，与本次构建前后完全一致。普通业务4文件（主档、bak、session、lock）已只读重新计算SHA，均与`E:/WORLDCREATOR/XingHaiHuiLang/研究/SCENE-PERF-01/business-checkpoint.json`一致；这是普通业务保护，不与测试源/独立运行档的保护结论混同。

## 历史首项修复后的CPU/RHI记录（保留）

初次查询未取得名为Render Thread的合格树，不代表渲染线程为0。随后确认本机实际线程名为 **RHI Thread**，从首项修复后、CRC修复前已有CPU-only采集离线重读Profiler帧155—174，共20个有效树；无invalid或跨帧/重叠样本，没有为这次解析新开采样。

| 同组20帧线程树 | 平均线程帧时长（ms） | 具名等待自身（ms） | 非等待/未分类自身（ms） |
|---|---|---|---|
| Main Thread | 50.3531 | 0.0372 | 50.3159 |
| RHI Thread | 50.3533 | 38.6944 | 11.6589 |

自身时间按inclusive减直接子节点inclusive计算，不把父子嵌套重复累加；两个线程并行，不将50.3531与50.3533相加。等待仅按明确Wait/Sleep/Idle名称分类，非等待/未分类不是“所有时间均为有效工作”。RHI的`Semaphore.WaitForSignal`自身均值38.6573ms，不能凭名称进一步认定全是GPU背压、Present、VSync或某个特定生产者。

同组RHI较大的非等待self包括PreviewCoverage.Build 2.7995ms、Permission.Build 2.7236ms、RenderLoop.Draw 2.0655ms。它们是该线程树中的CPU标记归属，不是GPU阶段耗时，也不是当时两项修复后的新采样。对应Main树及所有Profiler开关恢复状态一起保留。

证据：`cpu-timeline-20261107-152007-9158930.json`；其现有树补充解析为`cpu-existing-rhi.json`。

## 第一出口历史数据及测量限制

| 数据 | 有效样本与结果 | 解释 |
|---|---|---|
| 原FrameTiming GPU | 79个正值，p50 21.270ms、p95 75.013ms | 完成帧延迟与波动存在；不和Main/RHI线程时间相加 |
| 首项quiet FrameTiming GPU | 221个正值，p50 15.7297ms、p95 48.5468ms | 延迟完成帧观测，不等同逐阶段GPU归因 |
| 两项后quiet FrameTiming GPU | 228个正值，p50 16.4495ms、p95 45.5905ms | 不能用缺失/非正样本填0，也不能单凭p50认定CPU或GPU唯一瓶颈 |
| 最初CPU-only Timeline | 30真实相机帧、12个有效Main树；平均175.952ms，具名等待self 26.533ms、非等待/未分类self 149.420ms | Profiler/Editor干扰明显；不是无Profiler有效工作149ms |
| 最初Main较大self | 未命名ScriptableRenderPass约38.20ms、BeginCameraRendering约18.71ms、EditorLoop约65.17ms | 历史仪表样本，不作为两项修复后的时长 |
| 最初URP sampler 119样本 | 原Opaque CPU p50 1.79ms/GPU .13ms；MainShadow CPU .39ms/GPU .01ms；AdditionalShadow CPU .45ms/GPU .02ms | 只覆盖命名阶段；不能声称总阴影成本已全部覆盖 |
| 最初URP总inlineCPU | p50 72.71ms/p95 78.88ms；采样回调自身p50 44.5µs | CPU命令区间可含原生提交、同步/驱动等待，不统称纯managed工作 |

历史原始索引：`cpu-timeline-20261107-142344-7072069.json`、`gpu-stages-20261107-142441-9154263.json`。第一次CPU＋GPU完整Profiler没有可用CPU树，记unavailable，不能当0或性能通过。`CommandBuildMilliseconds`覆盖Execute内部的原生提交、DrawRenderers和环境/角色阶段，不是单独C#构造计时，也不包含剔除前全部准备和普通URP/阴影。

FrameDebugger只用于事件归属，不能用于正常性能计时。`frame-events-20261107-145412-7473261.json`与`frame-events-20261107-151622-6578337.json`均超时、事件未取得，已恢复禁用及原limit；**无事件表示unavailable，不表示场景0 draw，也不证明地形/屋顶/木屋/角色各有几条实际事件路径**。若后续用单次GameView重绘取归属，必须与正常quiet时长分开记录。

原Snapshot动态程序集嵌套字段曾被JsonUtility省略；原始Native完整场景副本与顶层路由/dirty/相机信息仍保留，不把不存在的357项JSON明细冒作已保存。

## 保留的路径事实与后续出口

RoomPositivePass正常路径清除常规世界颜色/深度后重建合法世界；外景屋顶预览和最终标签许可仍逐Renderer、逐完整材质槽提交Truth。最终世界按源材质桶DrawRenderers而非每Renderer，MPB逐对象应用/归还。两项修复减少重复材质刷新，没有删掉许可绘制、放行黑区或撤掉原ShadowCaster；不能因双Shader存在就说投影两次。

同轮optimized固定Editor组、整链ON/OFF/ON与最新guarded Player本机机位预算出口已取得。后续若继续追求更高帧率或普适认证，应保留本轮数据，继续减小已定位的合法世界重建/提交成本，并补全更广图像边界；若宣称Player逐帧固定对照，补足正式采样窗口pose校验。保留分辨率、画质和观察许可；整链OFF只用于归属并已归还。触及许可或深度的进一步优化必须额外验证图像合同，不能以撤销黑区隔离、角色光感或屋顶遮光换取帧率。

本轮最终Player在本机该机位通过30FPS预算，正确性、OS退出与业务保护证据均取得；Editor仍未稳定30FPS，60FPS和完整设备/视觉边界未认证。按R11定向发布源码、验证源和说明，本机媒体/资产/Player不混入公开仓库。保留这些完成范围与限制，不能把本次预算通过扩成全游戏、全设备或60FPS性能完成。
