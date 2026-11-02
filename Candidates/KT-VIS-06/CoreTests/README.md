# 自然视野与相机孔洞纯合同

本目录只编译 `Assets/OSSASTRA/Scripts/SceneVisibility/Rooms/Core` 中的纯 C# 几何。没有 Unity 桩、相机、场景、物理查询或真实存档写入。

## 执行

在 `E:/WORLDCREATOR/new/Project_ANKALUTE` 下运行：

```powershell
dotnet run --project Candidates/KT-VIS-06/CoreTests/PortalNaturalSight.Tests.csproj --verbosity quiet
dotnet run --project Candidates/KT-VIS-06/CoreTests/LegacyRegression.Tests.csproj --verbosity quiet
```

第二个入口原样链接 KT-VIS-05 的 20 组测试，不修改旧候选。旧入口只通配 `PortalSight*.cs`，因此新入口显式纳入新增的 `PortalApertureCone.cs`。

## RED 与 GREEN

- `red.log`：新增生产 API 之前执行，20 组中 8 通过、12 失败。为了观察真实旧行为，测试文件在 API 尚未存在时明确调用旧二参 `Solve`，没有以编译错误冒充 RED。失败集中于旧 6m/8m 限制、相机误授完整种子、邻室和朝向混用。
- `green.log`：接入新生产 API 后，20/20 通过；编译条件切换为直接调用新的三参自然策略和独立相机窗口入口。
- `legacy-green.log`：旧 20 组合同原样通过，包含孔洞上下沿/侧框、近远面、双向首跳、凹洞拒绝、实体封板、OBB、前墙与不可变快照。

新合同分别覆盖：普通玩家在 20m 外仍可透真实洞口、窗后 80m 同射线不被截断、靠窗后的自然角度扩大、背后拒绝、邻室不递归、旧字段无效时自然策略仍独立、独立有限策略、相机实际 3D 原点、SideB 反向外窗、只允许当前房间到室外、相机无人物朝向裁面、封闭/错误侧/非有限/非凸/非平面拒绝，以及相机永远没有完整房间种子。

## 生产 API

```csharp
PortalSightSolver.Solve(observer, portals); // 原 Legacy 合同不变
PortalSightSolver.Solve(observer, portals, PortalSightPolicy.PlayerNatural);
PortalSightSolver.Solve(observer, portals, PortalSightPolicy.Finite(observerRange, maxSightDistance));
PortalSightSolver.SolveWindowScene(currentRoomId, actualCameraPosition, portals);
PortalApertureCone.TryBuild(origin, actualConvexAperture, travelNormal, out planes);
```

- `PortalSightPolicy` 是不可变值，`default`/`Legacy` 沿原二参路径；`PlayerNatural` 使用前半空间，不读取旧门窗靠近距离和窗后远面。
- `Finite` 为独立有限入口：`observerRange` 是到孔洞最近点的距离，`maxSightDistance` 是从孔洞沿目标法线的长度。本轮仍为前半空间，不承诺任意角度手电光锥、眼点径向球，也没有接入敌人或道具业务。
- 相机结果 `FullSeedRoomId` 永远为 `null`。每个卷的 `Observer.Position` 是相机实际原点；其平面只包含凸洞边与目标侧近面。它只服务于窗孔像素中的室外场景，不能合并进人物 `AllowsPoint`、标签许可、完整房间种子或探索历史。
- 相机不必处于房间体积中，但必须位于该开口的室内半空间。只从当前房间选择开放的室外洞口，禁止邻室与递归传播。
- 人物普通卷为孔洞边数+2平面，相机卷为孔洞边数+1，有限卷为孔洞边数+3；未扩大渲染后端预算。

## 支持边界

这些是引擎外的几何合同，不证明最终窗孔像素、真实 GPU 遮挡、灯光、过渡、资源释放或玩家观感。实体遮挡继续由原有 blocker/渲染深度适配负责；相机和人物原点不得混用。新文件的 Unity `.meta` 与导入、场景接线由唯一 Unity writer 处理。
