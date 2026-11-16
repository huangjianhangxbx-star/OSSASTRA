# POSTURE-01 定向逻辑验证

在仓库根目录运行以下命令。夹具从实际 Unity 源码编译数值账本、同设备组合逻辑和受击反应阶段，不依赖 Unity 编辑器。

```powershell
dotnet run --project Candidates/POSTURE-01/Tests/Ledger/Probe.csproj
dotnet run --project Candidates/POSTURE-01/Tests/Gestures/Probe.csproj
dotnet run --project Candidates/POSTURE-01/Tests/Reaction/Probe.csproj
```

主场配置、动画事件和真实碰撞证据另见 `docs/ossastra/POSTURE-01/`。
