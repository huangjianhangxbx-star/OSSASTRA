# 房屋材质预设接入候选

本轮有界接入复用现有 `VoxelSurfaceComparison` 和 Look 缓存，不建运行材质管理器。Root 是唯一 Unity/Git 写入者；本目录的 Native 命令由 Root 审查执行。

## 执行顺序

1. 在旧代码/资产状态运行 `HouseMaterialContract`，记录实际 RED。本次 Root 已确认缺角色声明、两 Look 与房屋换色策略，其他基线通过。
2. 编译生产 `VoxelSurfaceMaterialRoleSetSO`、`VoxelSurfaceMaterialAuthoring` 与 Registrar 增量；Comparison 新增 `public VoxelSurfaceMaterialRoleSetSO materialRoles`。
3. 保留当前运行参数后回到主场 Edit、归还临时预览材质；运行 `InstallHouseMaterialStyles`。它创建候选资产、将明确八部件换色策略接入原 Comparison，保留旧预设后追加两项；不保存场景、不切模式、不改默认。
4. Root 读回/显式保存当前主场；重新运行只读 `HouseMaterialContract`，再做同机位游戏尺度对照、保存重开。代码合同不能代签最终美术效果。

## 精确新增资产

位于 `Assets/OSSASTRA/WorldRendering/VoxelSurface/LookDev/HouseStyles/TimberHall/`：

- `TimberHallRoles.asset`：14 个持久 source→语义角色/变体声明。
- `ClearTimberHall.asset`：清透木屋，复制旧 Clear 全部映射后追加 14 房屋映射。
- `RichTimberHall.asset`：浓厚暖木，复制旧 Rich 全部映射后追加 14 房屋映射。
- `Clear/*.mat` 和 `Rich/*.mat`：各 14 独立候选副本，原作者材质不改。

两 Look 继续引用原本相同的 `CalibratedResponse`，不复制或修改受光参数。旧 Look、原全部 `lookPresets` 和当前模式/默认引用保持；需要预览新 Look 时由现有面板显式选择。

## 明确角色与第一轮色板

颜色为 sRGB 作者 RGB，非采样外部图片。原模型 `BuildTimberHouse.py` 的 PALETTE 与 `NativeHouseAuthoring` 精确资产导出提供角色事实。只按这张已登记资产表建立声明，运行时不读对象名或 RGB 分类。

| 语义角色 | 清透木屋 | 浓厚暖木 |
|---|---|---|
| wood.frame | .280/.225/.170 | .265/.150/.085 |
| wood.panel | .550/.440/.310 | .550/.335/.170 |
| wood.floor | .480/.375/.260 | .460/.275/.135 |
| wood.trim | .650/.550/.405 | .660/.460/.250 |
| roof.base | .385/.430/.445 | .370/.320/.275 |
| roof.edge | .550/.565/.510 | .525/.455/.350 |
| stone.base | .445/.490/.500 | .440/.410/.355 |
| stone.cap | .585/.625/.620 | .580/.545/.465 |

`wood.panel`、`wood.floor`、`roof.base` 另含 dark/light，保持原整块变体的 RGB±.025，合计 14 源。屋面仍为灰色层叠块面，暖版没有将整屋换为砂岩。

材质副本只修改 BaseColor RGB（如存在同义 `_Color` 同步），alpha、纹理/采样变换、UV、金属/粗糙参数、Shader/关键字、顶点色和其他源媒体来自完整材质复制。

## 新建筑作者接线

1. 创建独立 `VoxelSurfaceMaterialRoleSetSO`，为每个真实持久作者材质选择明确 `roleId` 与 `variantId`。未知源不能隐式归类。
2. 通过原统一受光接口先登记完整 A/B/C 原槽。仅给希望参与风格换色的明确 Renderer 调用 `VoxelSurfaceMaterialAuthoring.ApplyToComparison`，所有槽预检成功才提交策略。
3. 通过现有 Look 为声明源提供独立 replacement，并继续引用所需响应 Profile。地形、树、水保持 lightingOnly；它们的夕阳融合由独立环境阶段处理。

当前角色集字段是一个明确集合；如果已有另一集合，接口拒绝静默替换，作者应显式整合集合。Registrar 对已改为换色的房屋仅在真实 timberHouse 子树且完整槽被持久角色集合覆盖时幂等放行。

本目录候选不是自动更新器：重放只接受内容完全一致的已有资产，否则报错等待 Root 核查。没有源媒体重写、纹理烘色、屋顶/门窗/碰撞和业务修改。
