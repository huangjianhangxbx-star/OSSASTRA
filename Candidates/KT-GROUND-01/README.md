# KT-GROUND-01 可复跑验证

只由本任务唯一Unity writer按项目Funplay工作流执行，先核工程、Edit/Play与PrefabStage。M6虚拟设备测试要求物理输入闲置；它不改变项目绑定或Settings。

- `SurfaceContracts`、`BrushContracts`、`BrushSafetyContracts`：临时Preview对象/原生Undo，覆盖边界、事务及外部组保护。
- `GroundViewContracts`、`NativeMpbProbe`：观察/指针与原生MPB恢复。新TilemapRenderer有内建属性块，真正空分支显式清空后测。
- `ImportContracts`、`MissingDependency`：本机白名单导入与缺失Sprite反馈；本机素材资产不在公开仓库。
- `SceneContracts`、`VisibleCoverage`、`ChunkInformation`、`SourceTrace`、`GenerationBoundary`、`StageSafety`：本场实际数据/并集/来源/入口与Stage约束。
- `ActualSceneBrush`、`ActualSceneEdgeContracts`：独立临时SceneView和真正Native Palette事件。校准只用NativePreview，不落作者数据；笔画通过真实Down/Drag/Up、实际Tile/Sprite与Undo证明。对应Poll候选读取静态结果，不重复Start。
- `ActualScenePersistence`：在本轮原始示范场验证实际Erase/Undo、两次保存关闭重开并明确恢复示范。**它会重置示范，不能在已经创作的地图上自动运行。**当前原始演示无人编辑的授权状态才适用。
- `M6InputStory`、`M6CombatOnlyStory`、`M6PerformanceStory`及Poll：原输入/控制/战斗与有界渲染统计。不要同时启动；报告Runtime/CPU Editor/GPU未知的真实边界。

辅助即时修复/切场/原生哨兵脚本留在本机；公开只列可复跑的本模块验证。原游戏数据、私有JSON、原贴图和派生资源不随这些脚本发布。原失败回执保留在本机研究根，不将后来的通过回写成历史全绿。
