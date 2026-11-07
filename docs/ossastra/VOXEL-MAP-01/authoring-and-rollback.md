# VOXEL-MAP-01 使用、编辑与回退

工程根目录：`E:/WORLDCREATOR/new/Project_ANKALUTE`。本页说明操作和边界；本轮实际验证、发布记录由主执行者另行补齐。

## 在哪里体验

- 主场保持原路径：`Assets/OSSASTRA/GroundWorkshop/Samples/KT-VIS-04/SC_RoomVisibilityLab.unity`。
- 独立样地：`Assets/OSSASTRA/VoxelTerrainValidation/Scenes/SC_VoxelTerrainHousing_Validation.unity`。

Play 后，左上方“体素地图样板”默认折叠。展开可定位至房屋门外、木屋室内、小坡 A、小坡 B、水洼岸边。“地图总览”临时拉远观察；“恢复跟随”恢复进入总览前的距离与缩放上限，并返回主控跟随。定位是开发检查入口；正常进出建筑仍应自行走门。面板不暂停世界，点击期间会捕获本窗口输入以避免穿透攻击或转镜头。

## 地形母体与重新烘焙

可编辑母体在本机：

`E:/WORLDCREATOR/XingHaiHuiLang/研究/VOXEL-MAP-01/Terrain/voxel-map-01-terrain.xhasset.json`

使用既有地编工具 `E:/WORLDCREATOR/XingHaiHuiLang/Origin/自制工具/map_editor` 编辑。保持 0.25 m 格度、原生 `RH_Z_UP` 坐标、8 色调色板和明确的顶面/侧面角色；不要通过 Unity 对母体整体缩放来改变格度。

本轮初始样地生成器为 `Candidates/VOXEL-MAP-01/Terrain/GenerateTerrain.mjs`。它会重建本轮初始母体，日常手工编辑后不要直接运行它覆盖自己的成果。修改前先保留一份母体副本。

后续更新沿以下链路执行：

1. 保存编辑母体，由原生工具调用 `validateAsset → toEditingMap → meshMap`，生成冻结 greedy quads；外部地编工具本身保持只读，不另造 mesher。
2. 冻结表面 `voxel-map-01-terrain.surface.json` 必须携带该母体原始 UTF-8 文本的 SHA256。不要手改哈希来绕过校验。
3. Unity Core 的 `VoxelTerrainSource.Parse` 校验哈希、格度、预算和完整外露面；`VoxelTerrainGeometry.Build` 只生成分块几何。
4. 由唯一 Native Editor 执行者更新明确的 Mesh、Collider、材质与支撑引用，先独立场检查，再回替主场。涉及 Prefab 实例的支撑改动必须记录 override，并保存重开读回。

**已有 `VoxelTerrainInstallation` 标记的场景禁止重新执行整套 installer。** 它是一次性装配入口，重复执行会破坏迁移前引用与归属。更新应先核对现有标记、源文件与目标资产，再做定向重烘焙或受控回退。

## 房屋、角色与水面约定

房屋按 0.25 m 格度制作，门净宽约 2 m，适配约 1.5 m 高的现有角色。结构、地板与可隐藏顶盖分别登记；揭顶继续由既有建筑可见性模块负责，原屋顶遮光与物理碰撞保留。

Blender 原文件仅在本机：

- 房屋：`E:/WORLDCREATOR/XingHaiHuiLang/研究/VOXEL-MAP-01/Models/TimberHouse-v1/TimberHouse.blend`
- 树木：`E:/WORLDCREATOR/XingHaiHuiLang/研究/VOXEL-MAP-01/Models/Trees-v1/Trees.blend`

本轮角色场景实例的 CharacterController `stepOffset=0.3`，用于跨越 0.25 m 阶梯；角色 Prefab 默认值未修改。后续调整坡形或格度时，应重新检查通行，不能仅凭视觉判断。

水洼具有真实凹地碰撞，水面高度 -0.18 m，最低床顶 -0.5 m。当前水面采用 **opaque 水色**，避免现有合法视域后端对透明 Lit 水面拒绝绘制；本轮没有扩展透明水、折射、游泳或流体系统。

## 如何回退

原主场在盘快照：`E:/WORLDCREATOR/XingHaiHuiLang/研究/VOXEL-MAP-01/original-main-on-disk.unity`。

先退出 Play 并保留当前主场备份，再由 Native Asset/Scene Editor 执行受控资产复制、导入和场景恢复；确认主场路径及引用正确后保存重开。不要通过文本编辑 `.unity`，也不要对已有安装标记盲目重放 installer。

只回退地形时，需要一并恢复旧地表显示、旧低地支撑启用状态，以及 `previousPropSupports` 记录的业务支撑引用；不能只隐藏新地面。高台原碰撞与角色业务不随显示回退而删除。

旧高台缓存由稳定的 `GroundSurfaceBinding` 管理：安装时记录 `previousCacheBindings / previousCacheUse`，将对应 Binding 的 `useCache=false`，再调用 `SetCacheVisible(false)`。缓存 Renderer 是允许重建的叶对象，不作为长期回退引用；`previousGroundRenderers / previousGroundEnabled` 只保留稳定的旧 Tilemap 与明确退役装饰，每个引用仍须有效且处于关闭显示状态。原 `capCollider` 独立于被关闭的 `cacheRoot`，继续启用承载与物件支撑。

局部回退时，由 Native Editor 按 `previousCacheUse` 恢复各稳定 Binding 的 `useCache`，并通过 `SetCacheVisible` 更新缓存显隐；不要尝试找回已经重建的旧缓存叶 Renderer。恢复后再次确认高台承载碰撞仍启用、业务支撑引用正确，再保存重开。**回退不删除任何业务档、验证档或隔离存档目录。**

## 本地资源与发布范围

原作资源、模型、参考图、生成网格/材质与场景保留本机，不上传 Git。本轮按既有 R11 规则定向发布源码和文档；提交前核对路径，不对整个工作树执行全量暂存。生成器、Core、开发入口及说明与本地美术资产分别保留来源。
