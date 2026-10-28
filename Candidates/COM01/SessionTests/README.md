# COM-01 InventorySession 宿主夹具

本项目编译真实 `InventorySession.cs`、`InventorySessionRuntimeState.cs`、`IInventoryStartupAdapter.cs` 与全部纯 `Characters/Core` 源码。`HostStubs.cs` 仅提供位置、两角色存活/装备口及最小Unity类型；不是Editor/Physics/InputSystem真实运行，不能代签正常地图验收。

运行：

```powershell
dotnet run --project Candidates/COM01/SessionTests/Session.Tests.csproj -c Release --no-restore
```

已执行26项GREEN；零编译警告/错误。夹具项目仅抑制Unity序列化私有字段在普通C#编译器中的CS0649，真实Unity编译仍由主任务执行。

真实行为RED记录：

- UI订阅故障令已完成注册抛异常：`injected view failure`。改为逐handler安全通知后GREEN，其余观察者仍收到刷新。
- 启动恢复后、Ready之前切当前地图会错误搬迁旧`external`：`FAIL map rebind after restore never relocates saved legacy external box`。恢复标记与首次绑定区分后GREEN。
- 可选字符串由null编码为空串时，合法恢复被误拒：`FAIL optional null strings and empty-string codec representation restore equivalently`。仅归一可选空值，不放宽未知非空定义ID。

其余覆盖：恢复拒绝不播种/不Ready、恢复时序与无adapter旧场、稳定多箱不重赠/冲突拒绝、原子整箱授予失败不留半箱、指定当前箱快移、取全部部分成功/重按不复制、快捷栏独立dirty事件、保存编辑门不阻断已受理消费与全灭、完整runtime字段候选及无中间通知发布、必需容器/手槽owner校验、地图卸载保留逻辑状态。

## Runtime DTO与端口

`InventoryRuntimeState`：`schema=1`、`currentMapId`、`activeExternalId`、`priorityDefinitionId`、`selectedHotbar=-1`、`hotbar[10]`、`worldContainers[]`。

`InventoryWorldContainerState`：`id/mapId/displayName`、`x/y/z`、`width/height`、`isGround/isEmpty`。容器/物品本体仍只在Ledger快照；`isEmpty`必须与候选Ledger一致。

共同恢复：先`PrepareRuntimeState(value, inventorySnapshot, out error)`完整验证并预分配；领域`Economy.Restore`成功后立即`PublishRuntimeState(prepared)`，后者仅切换引用、不通知。会话完成装备绑定才令`Ready=true`并调用`OnInventoryReady`。

主任务负责场景/多地图实际卸载、真实输入/UI回归、字段JSON编码及完整领域保存重启；本夹具不是这些验收的替代品。
