# VOXEL-LIGHT-01 保存主场 Windows Player 烟测

运行源码：`Assets/OSSASTRA/Scripts/Development/VoxelSurface/VoxelSurfacePlayerProbe.cs`。
只编入`UNITY_STANDALONE_WIN && !UNITY_EDITOR`；Root无需把组件挂进任何场景。
仅下面精确命令行参数会创建临时RuntimeInitialize对象：

```text
-voxel-light-smoke "E:\WORLDCREATOR\new\Project_ANKALUTE\artifacts\VOXEL-LIGHT-01\player-reports\main-smoke"
```

Root以保存的`SC_RoomVisibilityLab`构建实际Windows图形Player，并可见启动exe、附带上述参数。
没有此flag时无对象、无协程、无事件订阅，也不切档或退出普通Player。报告路径必须为完整绝对路径；
拒绝batchmode/Null图形设备。此探针不修改场景文件、Profile、源材质或业务源码，且不新建验证场景。
显式smoke进程的Commerce路由会在任何库存恢复前临时隔离，见下文；普通无flag进程完全不改路由。

持久化隔离：BeforeSceneLoad只在flag成立时注册`sceneLoaded`。回调先同步禁用本Scene的InventorySession，
验证精确Main与唯一Commerce、明确Inventory且Store尚未建立；然后将`saveDirectory`指向`reportdir/isolated-commerce`，
`profileId`保持原值。只复制源目录顶层文件（当前确切4文件），已有目标不覆盖；复制前后及Finish再次记录原文件SHA256。
全部校验成功才恢复Inventory原enabled态；异常保持禁用后退出，不能让待退出的一帧仍恢复/写入原档。
Finish/OnDestroy只退订，绝不把directory改回原值，保证随后Commerce Quit/Disable/Destroy保存仍进隔离Store。
Main资产没有改写。Root在进程结束后再次核原4文件SHA；报告的Finish哈希是退出前见证。

流程：

1. 只在实际活动主场解析唯一comparison、EnvironmentRoot、MapResearchRoot；要求22个唯一有效绑定及已保存A/B/C源、B/C Profile。
2. 使用Environment与Map共同指向的真实启用gameplayCamera。仅其`RenderPipelineManager.endCameraRendering`累加，
   同一Unity帧去重；先冷启至少60个实际渲染帧。
3. 临时关闭跟随、观察固定中心`(24,0,-9)`，保留原45度、yaw、距离与FOV。只把环境时钟临时切手动、停止运行、使用自然SunPath。
4. A/B/C各在12/18/0时至少20个实际相机渲染帧。帧条件达到后，EOF只用于Native ScreenCapture，不作为帧成功证据。
5. 每阶段保存PNG与同名JSON；A精确检查原材质引用，B/C检查实际Shader家族、支持状态和9个参数等于对应配置的校验值。
   许可通道必须真实Active、实际draw>0且无错误。图片尺寸通过不代表视觉通过，Root须实际查看截图。
6. 结束归还原mode、时刻/时钟模式/手动太阳开关、跟随/固定中心/平滑值。
   起始A必须恢复精确original作者引用；起始B/C只核对应source Shader与Profile九参数，允许合法销毁后重建私有clone，
   不保存或要求恢复已销毁的初始clone身份。同mode刷新身份稳定由原配置23项合同覆盖。
   再核环境作者序列化值、角色与体素Profile、A/B/C作者材质完整属性/关键词/绑定指纹未变，
   且实际私有缓存数不超过明确作者Material/Profile对数，退出0；失败或真实Stopwatch达60秒退出1并记录原因。

报告：`result.json`、九组`Original/Calibrated/Faceted_12/18/00.png/.json`。
日志最终行带`VOXEL_PLAYER_RESULT`，各阶段带`VOXEL_PLAYER_PHASE`。本次显式烟测期间全局Error/Assert/Exception会令最终失败，
记录总数及最多16条有界原文，不把“还有画面”算作启动无异常。

资源记录来自真实主相机RoomMaskStatistics（draw/材质/纹理/Buffer/MPB/桶/估算字节）、WorldLook公开自有计数、
角色覆盖/外缘draw与字节、comparison实际缓存计数，以及全局已加载Material/RenderTexture对象数。
全局已加载对象数不等于显存或该模块自有对象。Draw Calls Count/SetPass Calls Count使用真实ProfilerRecorder；
若平台/构建不可用，值为`-1`且明确`unavailable`；可用值标注为全局最近完整帧，不能当作每相机或GPU耗时。

离线验证：

```powershell
dotnet build Candidates/VOXEL-LIGHT-01/Player/PlayerProbeCompile.csproj --nologo -v:q
```

此项目实际编译Windows Player条件分支并引用本机Unity/当前主工程程序集，已0错误0警告。
Root仍须实际build、可见exe运行与图像验收；离线编译不代签这些结果。

隔离Native合同：执行`VoxelPlayerIsolationContract.cs.txt`后调用`VoxelPlayerIsolationContract.Run()`。
合同仅在Candidates/Player内生成Windows分支副本（移除首if/末endif）并动态编译，绝不改生产文件。
首次Native有效RED已确认缺少隔离入口且0编译错误；生产实现后同入口使用previewScene与inactive Inventory/Commerce测试
未授权不动、Start前隔离、四文件复制不覆盖、原Hash不变、重复调用、Destroy不回原目录及异常保持库存禁用。
夹具不调用RestoreOrSeed，不接触主场已有Store或真实原档。
