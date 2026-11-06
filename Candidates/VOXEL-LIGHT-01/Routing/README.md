# VOXEL-LIGHT-01 路由 Native 合同与实施顺序

已有设计及授权见 `docs/agent-sync/tasks/2026-11-06-voxel-light-01.md`。本子任务只修改 Rooms/Rendering 的 RenderingLayers、MaterialBuckets、PositivePass 与实际 Feature `RoomMaskRendererFeature.cs`，不操作 Unity/Git；Root 唯一执行 Native。

1. Root 导入真实 `Ossastra/WorldRendering/VoxelLit` 与 `Ossastra/WorldRendering/SightVoxelLit` 后，加载 `VoxelRoutingContract.cs.txt`，调用 `VoxelRoutingContract.Run()`。`prerequisitesMissing=true` 是环境未准备，不能作为 RED。
2. 旧生产代码应 `ordinaryPrepared=true`、`ordinaryRestored=true`，但 `voxelClassified=false`、`mixedSlotsPrepared=false`、`perSourceAdapters=false`、`roofUsesVoxelAdapter=false`，由缺少新源家族产生有效 RED。Root 明确确认后才改生产。
3. 生产接入：新源精确名字映射既有 PartialLit 调度位；resolver 接收源 Material、family 与 out pass；Feature 仅新增显式 `positiveVoxelLitShader`。Validate 逐材质检查实际 Shader，静态桶与只读动态适配均用源感知选择，普通 Lit 保留原入口。
4. 静态桶捕获 Prepare 时源 Shader 身份；RefreshAdapters 在改写任何桶前检查全部源身份及队列类别，变化则拒绝旧 adapter，下帧 Prepare 恢复。不得改动原 256 桶、16 槽、6 动态 Lit 组、冲突 pin、材质原引用与 MPB 归还边界。
5. Root 导入后重跑同一合同，所有行为项应为 true。再执行真实 GPU 的普通/Full/Partial/Roof/EnvironmentFull 颜色和合法深度、主场与 Player 检查；本合同不代签 GPU 输出或 Player。

合同只用实际生产 Pass/MaterialBuckets/PropertyScope 和真实 Shader，不向生产增加测试入口。临时对象均由 finally 释放，不保存场景/配置，不改变当前作者参数。

## 当前进度

- Root 已确认实际 Native RED：普通 Lit 的 Prepare/恢复通过；Voxel Classify、三槽适配、EnvironmentFull、Roof、引用恢复和 source Shader 变化检查失败，明确原因为未支持源家族，不是编译失败。
- 收到 RED 与四文件租约确认后，已实施上述源感知路由与静态桶全量身份预检；没有增加渲染位，也未改变 PropertyScope、角色受光或 WorldLook role。
- `git diff --check` 通过。`dotnet build Candidates/VOXEL-LIGHT-01/Routing/RoutingContractCompile.csproj -v:q` 为合同语法检查，0 错 0 警；不代签生产 Native 编译或执行。
- Native GREEN、主场 GPU 与 Player 由 Root 后续运行并记录。
