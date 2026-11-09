# SCENE-PERF-02 Player候选

这三份入口只由Root导入／执行；子任务不操作Unity或Git。生产画面、质量、阴影与观察规则保持原样。

## M1 短CPU诊断

将 `ScenePerformanceContinuationProbe.cs.txt` 临时导入为 `Assets/OSSASTRA/Scripts/Development/ScenePerformanceContinuationProbe.cs`，由现有真实主场内容副本构建同类型Windows Development Player。

独立启动参数：

```text
--scene-perf02-cpu <全新绝对RunDir>
--scene-perf02-route exterior
--scene-perf02-source <已批准冻结测试源绝对目录>
--scene-perf-build-scene <本次实际主场副本Assets路径>
```

`--scene-perf02-source` 默认是 `E:/WORLDCREATOR/XingHaiHuiLang/研究/SCENE-PERF-01/test-commerce`；也接受明确放在 `研究/SCENE-PERF-02/` 下的冻结测试源。构建场Commerce作者route必须与来源精确一致，sceneLoaded在Start之前先禁库存并转向 `RunDir/isolated-commerce` 后才读取复制来源。失败后不回退业务路由。RunDir必须新建或空目录，禁止与来源重叠。

`--scene-perf-build-scene` 可省略，此时只接受原主场或已有 `Assets/OSSASTRA/Development/ScenePerformanceBuild/ScenePerfBuild_*.unity`、本轮 `Assets/OSSASTRA/Development/ScenePerformance02Build/ScenePerfBuild_*.unity` 副本（禁止路径穿越）；给出时额外核对实际路径完全一致。

启动后一次调用已有Lab的路线定位（不逐帧传送），至少30真帧并连续15帧镜头稳定后开始诊断。Profiler二进制记录64个实际主相机帧，总60秒超时；唯一 `SCENE_PERF02_PLAYER_<token>` marker用于区分来源。结束后归还Profiler三个原字段、VSync和targetFrameRate及自有镜头捕获，关闭raw后计算SHA，再写 `result.json` 并退出。

CPU模式是诊断版。它开启Profiler，采样结果不得冒称正常Player性能。

## Editor读取实际Player raw

使用 `ReadPlayerCpu.cs.txt` 的命令类 `ReadPlayerCpu`。在Root确认Editor已退出Play、无正在采集的Profiler后，写：

```json
{
  "runDirectory": "E:/.../本次CPU-RunDir"
}
```

请求文件是工程 `artifacts/SCENE-PERF-02/player-cpu-read-request.json`。也可在同次Native代码中设置 `ReadPlayerCpu.RunDirectory` 后调用其 `Execute(ctx)`。

读取器明确调用 `ProfilerDriver.LoadProfile(raw,false)`，会替换Profiler当前显示缓冲，但不改变Profiler开关或场景。它先核WindowsPlayer/非Editor manifest、原始raw SHA和唯一marker，再只导出实际包含该marker的40至80帧。Main与实际命名的RHI／Render Thread／GfxDeviceWorker单列；线程自身时间只减直接子节点，不累计嵌套包含时间或相加并行线程。具名Wait/Idle/Sleep单列，余下标为非等待或未分类，不宣称全部是有效C#工作。缺RHI数据明确写不可得。

输出 `RunDir/player-cpu-tree.json`。旧Editor缓冲、缺marker或错误SHA不能充当Player树。

## 正常未限帧采样

同一Player另起进程：

```text
--scene-perf02-run <另一全新绝对RunDir>
--scene-perf02-route exterior
--scene-perf02-seconds 60
--scene-perf02-source <同一冻结测试源>
```

`interior` 复用已有Lab室内定位点；暖机结束检查实际ExteriorView与所选路线一致，位置不合适时失败，不偷偷修改房间规则。`dynamic` 暂只保留正常作者入口，不装配回放、不捕获相机指令，Root后续接已有真实输入路线；不能把它标成已经完成动态路线。

正常模式不启动Profiler。采样前临时vSync=0/target=-1并保存原值；不改质量／阴影／渲染比例。要求实际1600×900、45°、Ultra；正常计时阶段不截图、不写JSON，使用预分配内存逐帧保存位姿、屏幕／质量、全部相机间隔、FrameTiming及独立时间戳去重的正CPU/GPU值，缺值为不可得。窗口结束后一次输出全部帧、P50/P95/P99、最大帧、全部超预算段与最长连续段。

正常版若本来已启Profiler，会明确拒绝把它算作正常性能通过。静态镜头／质量变化会使性能窗口无效。退出码仅区分本次测量和隔离正确性；`performance60Passed` 才是本次有限路线16.667ms/P95/P99/1%门槛，`headroomPassed` 单独对应P50≤12.5ms。最终仍需Root执行室内外各两轮60秒、动态两轮120秒、扩展负载与视觉正确性，不能凭一次通过完成整轮任务。

## 离线编译

```text
dotnet build Candidates/SCENE-PERF-02/Player/RuntimeCompile.csproj
dotnet build Candidates/SCENE-PERF-02/Player/ReadCpuCompile.csproj
```

两入口已在当前DLL引用下编译0警告0错误；尚未由本子任务进行真实Player捕获或Editor导入验证。

## 动态真实输入路线候选（不阻塞首轮CPU构建）

另有 `ScenePerformanceDynamicRouteProbe.cs.txt` / `DynamicCompile.csproj`。它尚未随首轮Player导入或实跑；Root校准可通行路线后再临时导入同名`.cs`。不改已冻结的采样入口。

额外CLI：`--scene-perf02-input-route <路线JSON绝对路径>`，同时必须是 `--scene-perf02-run <RunDir> --scene-perf02-route dynamic`。

路线结构是 `schemaVersion:1`、`loop:true`、`steps:[...]`。每项字段：

| kind | 必要字段 | 实际执行 |
|---|---|---|
| move | `worldTarget:{x,y,z}`，可选`radius`、`verticalTolerance` | 按当前真实镜头基计算WASD八向意图，经已配置keyboard composite的实际KeyControl送入原CharacterInputHandler |
| orbit | `direction:1`或`-1` | 一次虚拟Mouse scroll=±120，验证原TargetYaw准确变化45°并等待ActualYaw自然到位 |
| switch | 无 | 按当前协调者实际switchAction绑定的KeyControl发一次按下／释放，观察Current身份交接 |
| door | `open:true/false` | 单次既有Lab.SetDoorOpen，至少等两次真实主相机渲染 |
| shutter | `open:true/false` | 单次既有Lab.SetShutterOpen，至少等两次真实主相机渲染 |
| wait | `minimumSeconds` | 中立输入，世界继续运行 |

每项还可写 `minimumSeconds`（默认.2）及 `maximumSeconds`（默认15，最多60）。不会默认生成未经验证的世界坐标；Root必须按当前真实门洞／台阶提供路径拐点，使循环能物理走通。没有达到点位会超时失败，不传送兜底。

候选在测量前建立自有虚拟设备、解析双方真实键位并预分配16个方向状态；owner进入采样后才发动作。真实Player必须有焦点；不伪造焦点，不禁用真实硬件，不调用InputSystem.Update、角色Move或SwitchNext，不写动画。门窗调用是明确开发入口，不宣称原生F开门。连续动作路线和性能采样相互独立：采样JSON仍需与 `dynamic-route.json` 的实际移动／转镜头／切人／门窗／进出房间证据一起审查；harness局部pass不替代整轮动态验收。

退出后中立化、移除自有键鼠并恢复原门窗状态，写 `dynamic-route.json`；路线原JSON在测量前复制到 `route-plan.json`。动作失败会请求同launch测量owner输出失败回执并退出，不悄悄将余下窗口改成静止。

## CPU raw结束边界修正（2026-11-08 接续）

首份实际Player raw曾被原生LoadProfile拒绝，Editor日志同时有TMP.SetArraySizes的非配对EndSample和Deserializer错误；该raw不计为CPU树证据。Reader API与Unity2022.3原生Profiler窗口一致，不换扩展名或忽略false来绕过。

当前Runtime候选仅CPU模式变更：render回调只请求开始／停止；单独的CLI专属 `ScenePerformanceCpuBoundary` 在early Update执行Profiler开关，先留两帧未标记主相机帧，再采64帧唯一marker，结束后留两完整帧排空，于后续early Update停止、显式清空logFile并归还原三个字段，再等三个Update后计算SHA。结果增加cpuCaptureStartFrame／cpuLastMarkedFrame／cpuCaptureStopFrame／cpuPrerollCameraFrames／cpuDrainFrameCount。正常模式不创建boundary组件，原采样路径及Update顺序不改。

离线Runtime重新编译0警告0错误；真实新raw能否被原生Loader接纳仍待Root验证。Unity文档给出关闭顺序依据：https://docs.unity.cn/ScriptReference/Profiling.Profiler-enableBinaryLog.html （停止Profiler后清空logFile以关闭文件）。Reader保持原样。

## 动态路线接续与Root操作口（2026-11-08）

`dynamic-route-example.json` 现在给出27步循环，显式 `initializeAtExterior:true`：允许在测量前仅一次调用既有Lab.PlaceCurrent(exteriorPosition)。执行前后必须均sampling=false；记录初始前后位置、frame、ownerWarmedFramesAtPlacement及warmNeverSampling。进入测量后，循环永不定位／传送。此选项默认false，只有JSON显式true才生效。

路线坐标依据：BuildRoomSample创建A中心(-16,-12)、外入口(-16,-16)、A→C门(-12,-12)、走廊(-11,-12)；ConfigureIncrement配置exterior(-16,.1,-18.2)；ConfigureFinal把doorPortal连接至portal-a-c。路线只走A/C，不进入原敌位于(-6,-12)的B室；穿门前打开A→C门，并在A室内各关闭／打开该门和西窗。共8次同向45°实际转镜头、2次切人和进／出建筑。切人后继续向同一合法世界点走，跟随角色不被传送。

这是源代码坐标候选，不是已跑通路线。Root在当前Native场必须确认外入口/门/地面高度仍吻合，且从外侧→A→C→A→外侧实际可走通。未到点按原deadline失败，不擅自跳过障碍或关碰撞。

当前局部pass已加强为实际覆盖门槛：observedYawMask==255、orbit>=8、switch>=2、entry/exit各>=1、同一Current累计自然移动>=5米、door与shutter各实际完成开／关（stateMask==3）。进出计数只在sampling开始后累计。它仍需与同launch120秒正常采样、PBZ退出后的相同条件与性能门槛共同审查；局部pass不宣称整轮完成。

启动动态Player时必须可见且真正有焦点，不能用 `-WindowStyle Hidden`；失焦即失败，不伪造焦点。正常采样期间不要运行CPU Profiler或截屏。Root可在120秒之前先用较短smoke验证动作，局部pass若因覆盖未齐为false属实，不改阈值掩盖。

本轮Reader、Runtime、Dynamic三个csproj重新编译均0警告0错误；Runtime仅CPU边界改动，Reader未修改。没有本子任务Native/Player执行。

## 原生启动CPU流（显式备用诊断）

若手动启停raw仍被原生Loader拒绝，当前候选支持同时指定：

```text
--scene-perf02-cpu <全新RunDir>
--scene-perf02-startup-profiler
-profiler-enable
-profiler-log-file <同一RunDir/startup-cpu.raw>
-profiler-capture-frame-count 256
-profiler-maxusedmemory 268435456
```

运行时严格核对这些CLI：唯一RunDir允许引擎在Bootstrap前仅创建这一个精确raw，其余文件一概拒绝；正常Player日志仍放RunDir外。暖机后只读实际Profiler.enabled/enableBinaryLog/logFile及frame余量，记录raw实际状态，不改这三个Profiler字段。只发原64唯一marker，等待Time.frameCount>=259再退出，raw由引擎256帧上限与实际进程退出关闭。CPU启动流仍是诊断，不能作正常性能结果。

启动流 `result.json` 初始 rawHashPending=true／rawBytes=-1，没有对打开的raw计算SHA。Root启动器必须保持实际OS进程handle直到exit0，并写RunDir/exit.json。随后调用：

```powershell
& 'Candidates/SCENE-PERF-02/Player/FinalizeStartupRaw.ps1' -RunDirectory '<same RunDir>'
```

helper核实际exit成功、原manifest、唯一raw路径、非链接、PD3U，再用独占只读handle计算已关闭raw的SHA并记录rawClosedAfterProcessExit=true。Reader对启动流额外拒绝pendinghash或缺进程退出闭合证明，再按原64marker和WindowsPlayer身份读取；不忽略LoadProfile=false、不拿旧Editor数据兜底。

Unity2022.3官方CLI依据：https://docs.unity3d.com/cn/2022.3/Manual/ProfilerWindow.html 。256MiB只是诊断流缓冲，并非画质或业务内存预算变更。正常模式、原手动CPU模式保留，均不隐式启用启动流。真实新raw能否被当前TuanjieLoader接纳仍需Root实跑验证。

## CPU后窗MPB清点（独立候选，不改采样器）

文件：`ScenePerformanceBlockCensus.cs.txt`／`CensusCompile.csproj`，临时Native导入命令 `ImportCensus.cs.txt`。仅同时存在 `--scene-perf02-cpu <RunDir> --scene-perf02-mpb-census` 才创建，无flag无行为；正常性能模式拒绝启动它。Root若要清点，先Native导入新脚本、编译、再新建相同CPU诊断Player，CPU CLI附加censusflag即可。没有相机重渲染、GPU读回、MPB／材质／业务写入。

清点时机：在Start两帧后订阅现有endCamera，等待CPUowner真实sampled==64、stopPending，以及当前Time.frameCount严格大于最后标记帧；只在下一个未标记真相机帧清点一次。先检查相机真实scriptableRenderer对应Feature的rendererOwner、positiveCameras实际pass，再只读反射检查propertyScope.entries全部RendererBlock／Slots.Written=false、materialBuckets.swaps.Count==0。缺字段或未还原一律conclusive=false，不伪装成原始空MPB。缓存pins非零也标不确定。

每个真实surface存OwnerVolumes矩阵／HalfExtents、Exterior、静态／WorldLook／环境role与Full、response/min/shadow、roof/cutRoof、kind/drawMask等完整payload；新建MPB读实际Renderer块与每个材质槽块，保存isEmpty及已存在的owned属性名，不把这些属性一律判为污染。当前已还原的实际材质使用桶的只读ResolveOriginal解析私有克隆来源，并同时记录实际绑定引用与源family/queue；不把comparison.bindings.original当必须相等的原始源（正式校准派生材质可以合法不同）。

按“实际源材质ID＋完整surfacepayload”分cohort，存Renderer/槽数、原块非空数、static数、仍绑定私有克隆数及具体RendererIDs。只是一次源事实清点，不实现也不声称MPB跳过安全或性能收益。结果仅在Profiler.enabled=false且owner.profilerBorrowed=false以后写 `RunDir/mpb-census.json`，写盘不混入64标记CPU窗口。是否conclusive、scopeRestored、outsideSelected64Frames、cpuStoppedBeforeWrite必须由Root实际读回；候选离线编译0警告0错误，尚未实际导入／运行。

## 默认双击试玩存档隔离（独立候选）

`ScenePerformanceDefaultPlaytestIsolation.cs.txt`／`DefaultPlaytestCompile.csproj`；Root Native导入入口 `ImportDefaultPlaytest.cs.txt`。不修改既有Runtime／Reader／Census。只在没有 `--scene-perf02-cpu` 和 `--scene-perf02-run` 时订阅sceneLoaded；仅实际 `Assets/OSSASTRA/Development/ScenePerformance02Build/ScenePerfBuild_*.unity` 单场副本生效，主场与通用构建不变。

该副本在Start前先禁InventorySession，再把找到的CommerceService.saveDirectory全部改到固定 `研究/SCENE-PERF-02/playtest-profile`，然后才校验唯一服务、preStart、profileId=`kt-vis06-natural-01`、原作者入口精确=`test-commerce-v2`。冻结源只接受领域计算出的精确四文件，主档／备份都通过原领域只读Load身份与封套验证；源和目标目录链／文件拒绝Reparse。

首次试玩使用同任务目录内唯一staging复制四文件，SHA读回并证明冻结源未变后原子Directory.Move发布新playtest-profile，不覆盖或合并既有目录。后续双击只验证既有试玩档身份／封套，沿用已有进度，完全不复制覆盖。成功后恢复原InventorySession启用状态；失败保留安全路由、禁用库存并Quit1，不回退冻结源。CLI的隔离与测量条件保持原样。

无镜头／画质／Profiler／网络／UI／业务状态修改。成功preStart回执在任务根 `playtest-profile-launch-latest.json`，不放入四文件档案目录。候选与导入命令均离线0警告0错误，Root仍需实际无flag运行一次及第二次启动保持试玩进度的校验；本子任务没有Unity/Player/Git操作。
