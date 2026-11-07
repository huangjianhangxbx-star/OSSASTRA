# VOXEL-MAP-01 实际主场短 Player 验证

Root 唯一导入、构建和运行者。不得修改旧 VOXEL-LIGHT 候选。该候选不挂进或保存到场景；Root 只临时将 `VoxelMapPlayerProbe.cs.txt` 导入为开发脚本，构建后移除临时导入。没有显式 `--voxel-map-probe` 时，Player hook 完全不创建对象或运行验证。

构建前：实际 `SC_RoomVisibilityLab` 保存重开，27 个 Original 绑定保留、唯一 VOXEL-MAP-01 安装存在，Root 将主场 Commerce 作者路由明确切到叶名 `main-validation-commerce` 的隔离目录并记录原路由。构建候选只验证该路由，不自行替用户切换。运行 `BuildVoxelMapMain.RunImmediate()`，只构建实际主场；不改 PlayerSettings / BuildSettings。状态文件为 `artifacts/VOXEL-MAP-01/build-result.json`，不确定是否已构建时先读状态，禁止盲重放。

```powershell
VoxelMapMain.exe --voxel-map-probe E:/absolute/unique/voxel-map-player-report -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile E:/absolute/unique/voxel-map-player-report/player.log
```

必须为有图形设备的 Windows Player，不使用 `-batchmode` 或 `-nographics`。约 14 秒真实主相机观察，25 秒硬超时。验证实际 224 块 Mesh/Collider、原两角色 Coordinator、45°镜头、木屋一房间三块屋顶、原 RoomMask 实际 Active / Error 空 / Unsupported 0；只有真实主相机 endCameraRendering 计帧。等待同帧 EndOfFrame 后截取 `actual-main-camera.png`，写 `result.json` 并退出。没有交易回归、位置/时间/天气调整或作者资产修改。

存档隔离复用旧验证模式：BeforeSceneLoad 建命令行专用宿主并注册 sceneLoaded；在所有 Inventory Start 之前禁用库存，先把 Commerce 内存路由切至报告目录下 `isolated-commerce`，再复制 `main-validation-commerce` 文件并核 SHA。失败保持库存禁用并退出。源/目标不得相交、复用目标或含重解析文件；退出不恢复源目录，生命周期保存继续指向本进程隔离 Store。报告同时核验实际 SavePath 和源档前后指纹。Root 在进程退出后可再次核源目录指纹，以涵盖退出保存阶段。

这次 Player 只证明几何/接线/实际帧与存档隔离，不替代 Editor 已完成的 25/25 保存重开、建模审查和玩法验收。
