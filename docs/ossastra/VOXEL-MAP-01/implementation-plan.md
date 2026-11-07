# VOXEL-MAP-01 实施拆解

本文件是已授权v1.0包的执行拆解，不新增用户审批关卡。规格原件在外部研究/VOXEL-MAP-01/plan-input；参考1为地貌/树木，2为木构梁柱/檐口，3为庭院/替代植被，4为红树/灯塔。仅主屋与地表是本轮必做，完整屋顶轮廓为明确的设计外推。

## 接缝与文件

- 地编母体：既有map_editor的createAsset/validateAsset、toEditingMap、meshMap；自有生成配方与合同在Candidates/VOXEL-MAP-01/Terrain，不修改原地编工具。
- 严格接收：Scripts/VoxelTerrain/Core/VoxelTerrainSource.cs、VoxelTerrainGeometry.cs；只验冻结源和生成Mesh数组，固定.25m、轴交换、源SHA与面覆盖，不造第二mesher或运行时管理器。
- 场景装配：Runtime/VoxelTerrainInstallation仅保存明确引用；Candidates/VOXEL-MAP-01/InstallTerrainStage.cs.txt与Models/NativeHouseAuthoring.cs.txt为Root唯一Native authoring，生成资产归VoxelTerrainValidation。
- 建模：先三视图再既有Blender工作台受管理源，导出按语义合并。监察官独立记录在model-review.md。
- 开发导航：Development/VoxelTerrain/VoxelTerrainLabPanel复用PlaceCurrent与既有输入捕获，提供门外/屋内/双坡/水岸与全图，禁用精确归还。

## 执行与检查点

- [x] P0：Edit现场、原main磁盘与当前live副本、不可变4档备份/哈希；验证场Save-as-copy且单独保存Commerce路径，main磁盘未改。
- [x] 蓝图：正/侧/顶、2m净门/2.25高、1.5m人物、.25m格、3Cover与永久Structure；独立蓝图8.2，成品8.1后放行装配。
- [x] 源接缝：Feature先有效RED再12项GREEN，真实生成几何6项GREEN；3086.75m²、150760voxel、1902quads、224chunks、7608顶点；两坡.75/1.25、真洼底-.5、物件排不挖掉。
- [ ] 独立装配：保留原3/5m高台碰撞，旧二维地面显示关闭，真正mesh贴原grass texture且world UV/16；土侧与实际水格水面分材质。木屋地坪安装于世界.1m形成真实小基础，不与母体0m共面。
- [ ] 独立实际验证：正常输入登双坡/水岸与门，房屋进出整栋揭顶、碰撞与遮光保留、门窗双向合法范围、旋转视角、2D光感与支撑掉落不破坏；实际图由监察官补审。
- [ ] 通过后写validation-approved.json，才允许Native安装器操作原main；迁移前保留回退引用和完整旧main副本，不清场。
- [ ] main同类验证、保存重开/定向Player、作者重烘焙/回退说明、有限媒体与准确限制；R11仅自有源/文档发布，全部参考/媒体/本地作者资产仍本机。
- [ ] 完工保存、退出Unity并释放全部租约，最后按用户指令关机。

## 不能静默越过的边界

旧GroundSurfaceBinding/ThinTop仅共面，禁止把它变成起伏地形权威；原高台supportSurface仍有效。低地旧64×48 Box在新碰撞生效后退役，低地WorldProps显式迁到可有限Ray命中的同层新chunk/房间地板，保留旧支持引用。水与树必须进shared/static正向可见名单；房顶只进roofGroup，墙封口只进永久结构，terrain不能进角色实墙clearance。测试从原档不可变副本另建可写隔离，任何Play前核读实际路由。
