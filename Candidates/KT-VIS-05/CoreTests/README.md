# 门窗视域纯核心验证

在项目根运行：

```powershell
dotnet run --project Candidates/KT-VIS-05/CoreTests/PortalSight.Tests.csproj --no-launch-profile
```

`red.log`：生产合同仅有空求解/拒绝占位时，初版 19 个行为组中 16 组失败、3 组保守拒绝通过。首次没有求解器时也实跑了 bootstrap 缺功能失败；它没有把编译错误当作 RED。

共墙规则复核后新增第 20 组：`front-wall-red.log` 实跑 19 通过 / 1 失败，确认原先“与非 Full 邻室共墙必须保留”的额外限制会挡住当前实际种子。移除该限制后，`front-wall-green.log` 与最新 `green.log` 为 20/20 通过。独立编译语言版本 C# 8，0 warning / 0 error。本目录不提供 Unity / 最终像素 / 性能验收。

## 运行接入合同

- 命名空间 `Ossastra.SceneVisibility.Rooms.Core`；仅新的 `PortalSight*.cs`、`ObserverSnapshot.cs`、`FrontWallSolver.cs`，原 Membership 文件不变。
- `ObserverSnapshot.SpaceId` 是原主控实际空间。`@exterior` 表示室外；构造器也将 null/空白房间规范化为室外。`Position` 和 `Forward` 来自角色观察点与逻辑朝向，不来自相机。
- `PortalSightSnapshot.NormalAToB` 由 SideA 指向 SideB；孔洞必须真实、共面、凸且按边界顺序排列。允许反绕序；凹洞必须由作者明确分片，不能自动取凸包。快照复制输入数组。
- `ObserverRange` 是观察点至孔洞多边形的最短三维距离。`MaxSightDistance` 是从孔洞平面沿目标侧法线量出的有限距离，不是射线长度。
- 视域仅根据原始 Observer 的空间做第一跳。`FullSeedRoomId` 在室外为 null；`Volumes` 的目标空间只是 Partial，不能再次加入求解队列。运行层必须同时检验候选内容的空间归属，不可仅凭 `ContainsPoint` 把跨房内容全部注册为可见。
- `PortalSightVolume.ClipPlanes` 的合法侧统一为 `Normal·worldPoint + Distance >= 0`，顺序为孔洞每条边的观察侧面、孔洞近面、有限远面、人物朝向半空间。近面向目标推进 0.0001 米，拒绝开口面自身及源侧。CPU `ContainsPoint` 用同一组平面。
- `PortalSightBlocker` 是独立不透明实体 OBB。它的三个轴必须为单位正交轴、半尺寸必须正数。`IsValid=false` 的值按阻挡处理，运行层应单独报作者诊断。普通玻璃不应登记。`OccludesSegment` 将目标前 0.02 米作为默认终点偏置，以免目标自身表面遮住自己；实体后方仍被拒绝。
- `FrontWallSolver` 使用本帧真实 Camera.forward 的 XZ 朝向。作者登记墙段的任一侧若为当前实际 FullSeed，可按该侧向外法线隐藏面向镜头的必要墙段；共墙不例外。Partial、室外观察、未登记为种子任一侧的墙不取得该权，侧墙/背墙也不自动隐藏。结构显示与房间内容授权独立；共墙隐藏不使另一房间成为 FullSeed，也不改变屋顶、可见域或业务对象。

## 覆盖范围

同窗双向、窗框/上沿/窗台、近/远面、最近距离、背对/移远/错面、封板、门洞 Partial 与不递归、逻辑朝向半空间、三维平移旋转、孔洞反绕序、坏几何/非有限观察值、输入数组隔离、CPU/GPU 平面符号、实体封板双向射线、旋转高柜、目标表面偏置、前/侧/后墙、未入室与共墙权限。

## 动态表现登记小集

```powershell
dotnet run --project Candidates/KT-VIS-05/CoreTests/PresentationRegistry.Tests.csproj --no-launch-profile
```

该测试编译真实 `RoomPresentationRegistry` / `RoomPresentationBinding`，只替换 Unity 对象引用、假空值与 Transform 采样的最小边界，不模拟 Renderer 管线。`presentation-red.log` 为 1 通过 / 5 失败，`presentation-green.log` 为 6/6 通过；C# 8 独立编译无警告/错误。它覆盖旧登记与 owner 不可抢占、销毁引用清理、当前跟随锚点、FX 强制 Partial 标记、信息条锚点许可要求、失去跟随锚后撤权，以及叶适配器配置/禁用/返场/销毁的登记释放调用。最终图像、真实 Unity 回调顺序、原技能释放与真实阴影仍需父任务 Native 验证。

登记合同保留旧 `Register(owner, renderer, fixedAnchor)`。扩展入口是 `Register(owner, renderer, anchor, forcePartial, followAnchor = null, requireAnchorPermission = false)`。叶适配器 `RoomPresentationBinding.Attach/Configure(renderer, anchor, forcePartial, followAnchor = null, requireAnchorPermission = false)` 用自身作为 owner，不接管业务对象生灭、无 Update、无全场扫描。ForcePartial 不能因中心进入种子而升级为整件 Full；信息条还必须通过本帧观察锚点许可。
