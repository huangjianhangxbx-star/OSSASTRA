# SCENE-LOOK-01 Development Player 定向烟测候选

这里只是验证源；由 Root 唯一 Unity writer 操作，不自动运行、不写生产模块。

## 文件与执行顺序

1. `SceneLookPlayerProbe.cs.txt`：Root 临时作为同名 `.cs` 导入自有 Development 目录。仅在 Windows Player（非 Editor）且命令行含 `--scene-look-probe <绝对报告目录>` 时启动；正常游戏无该 flag 不产生任何对象或行为。
2. `BuildSavedMain.cs.txt`：Root Native 读取后，追加一个 `IFunplayCommand` 包装器调用 `BuildSceneLookMain.RunImmediate()`。不要只调用 `Run()` 后因后台 delayCall 未调度又重放；构建未知时读取 `artifacts/SCENE-LOOK-01/build-result.json`。
3. 构建前以 Edit 作者态将 Commerce 明确路由到独立 `main-validation-commerce` 目录并保存当前主场。构建入口拒绝正在 Play/导入/编译、额外场景、未归还作者完整槽、重复绑定、没有角色声明/独立暖木 Look/环境候选等状态。不修改 BuildSettings 或 PlayerSettings。
4. 图形 Player 需实际可见渲染窗口；用 flag 启动并给新的绝对报告目录。探针在 `BeforeSceneLoad` 注册 `sceneLoaded`，库存 `Start` 之前先禁用库存、改隔离路由、复制源测试档、核对 SHA，再归还库存启用态。任何失败保持库存禁用，退出也不切回作者源目录。
5. 探针显式应用 `RichTimberHall` 和亮面15%/背光0%，开启当前独立场景环境候选，固定自然17时夕照。只改 flag 隔离会话；角色、天气、45°镜头与源资产保持。
6. 首个真实主相机 `endCameraRendering` 后至少观测10秒且8帧，取得同帧 `ScreenCapture` PNG和实际 RoomMask 状态，检查玩家双角色、体素源 SHA、碰撞/屋顶、真实材质参数和环境控制；45秒总超时。`result.json` 还要求0运行时错误、独立实际 SavePath、测试源档指纹不变，最后退出码0/1。
7. Root 核对实际 `result.json`、截图、Player退出后源测试档 SHA；必要证据保留后 Native 删除临时导入脚本，重新编译读回。

## 绑定断言

不写死274/247等历史数值：逐个安装根 Renderer 必须唯一绑定；房屋每槽均获持久明确角色声明，`lightingOnly=false` 且 A/B/C 源保持；其余地形、树、水 `lightingOnly=true`。实际数量写入结果。地形源/224原生块和屋顶结构仍按本轮已批准安装合同验证。

## 本地编译

```powershell
dotnet build Candidates/SCENE-LOOK-01/Player/PlayerProbeCompile.csproj --nologo --verbosity quiet
dotnet build Candidates/SCENE-LOOK-01/Player/BuildCompile.csproj --nologo --verbosity quiet
```

两项在当前 DLL 上已实测0错误0警告；这不代表 Root 已构建或运行 Player。GPU shader 效果、普通/Sight图像一致性及观感，仍由主场定向证据与 Root 真正运行后的结果负责。
