# SCENE-LOOK-01 作者操作与接入

本轮把材质风格、表面受光和环境气氛分开。木屋切配色不改变太阳，场景融合开关不替换木材、地表或角色 Profile。当前主场为 `Assets/OSSASTRA/GroundWorkshop/Samples/KT-VIS-04/SC_RoomVisibilityLab.unity`，正常观察镜头保持45°。

## 先在主场比较

运行后展开左侧“体素受光”。“清透推荐／浓厚暖砂”是原样块候选；新增“清透木屋／浓厚暖木”除原样块外，还包含本轮木屋的明确木、瓦、石材质映射。地形、树与水不跟着换成砂色。“原 C”恢复原C材质会话，“A 原版”恢复完整作者材质数组，两个入口的含义不同。

展开右侧“环境模拟”，滚动到“场景融合 · 材质/受光档独立”：

| 操作 | 实际作用 |
|---|---|
| 原表现 A | 关闭本轮场景环境漫射融合，保留既有照明、角色光感与当前材质风格。 |
| 增强融合 B | 使用已登记的独立环境候选；仅调整场景环境漫射贡献的色度。 |
| 日出／正午／日落／深夜 | 沿用原时间与太阳入口，检查同一材质在不同环境下的表现。 |
| 手动覆盖天顶／地平线颜色 | 改用明确天空覆盖作为环境色目标；自然昼夜则从当前环境帧和昼夜档解析。 |

比较配色时固定时刻与太阳，比较环境融合时固定材质风格。不要同时换材质、改时间和拉曝光后，把所有变化归到一个按钮。太阳在地平线附近时水平地面本来就几乎没有太阳直射；B通过已有环境漫射形成气氛，不补造一份直射光。

## 配置与保存位置

`EnvironmentSimulationRoot` 上的 `EnvironmentSkyBinding` 保存 `sceneEnvironmentProfile` 引用与 `sceneEnvironmentEnabled` 开关。环境候选位于：

`Assets/OSSASTRA/EnvironmentSimulation/Profiles/SceneStyles/UnifiedSunset.asset`

候选是 `SceneEnvironmentProfileSO`。其中 `groundStrength`、`decorationStrength`、`voxelStrength` 分别用于现有地表、装饰与体素／已适配原生PBR漫射路径，范围0～1；0恢复该类原贡献。四个太阳高度字段控制自然暮色目标的连续淡入淡出，不控制真实太阳强度。日落色来自原 `DayCycleProfileSO.sunsetSunColor`，不会在太阳过0°切为月色时突然换一套目标。

在 Edit 模式通过 Inspector 调整并保存独立候选。运行窗口的 A/B 是会话开关；退出Play不会自动把临时选择当作者默认。需要长期使用某一启动状态时，先退出Play，再显式保存绑定开关与候选引用。不要为了保存环境气氛，覆盖体素材质 Look 或角色 Profile。

木屋候选位于：

`Assets/OSSASTRA/WorldRendering/VoxelSurface/LookDev/HouseStyles/TimberHall/`

| 资产 | 用途 |
|---|---|
| `TimberHallRoles.asset` | 持久作者材质 → 明确语义角色与变体。 |
| `ClearTimberHall.asset` | 清透木屋映射及原受光Profile引用。 |
| `RichTimberHall.asset` | 浓厚暖木映射及同一受光Profile引用。 |
| `Clear/*.mat`、`Rich/*.mat` | 各14份独立配色副本，保留源纹理、UV、透明、金属／粗糙与顶点色语义。 |

现有体素面板的“保存当前候选（材质＋响应）”沿用原显式保存目标；“显式设为本对象集默认”是另一个操作。保存前检查目标是自己的独立候选，不能把运行对照写回共享源材质或旧默认。

## 新房屋接入

1. 保留完整源材质数组，先通过原 `VoxelSurfaceComparison`／统一受光作者接口登记明确 Renderer。新组默认 `lightingOnly=true`，只共用受光，保留源配色。
2. 为希望参与换色的完整槽建立 `VoxelSurfaceMaterialRoleSetSO`。本屋8类语义为 `wood.frame`、`wood.panel`、`wood.floor`、`wood.trim`、`roof.base`、`roof.edge`、`stone.base`、`stone.cap`；板、地板和基础屋面各含base／dark／light，合计14源。名称是作者声明，不是运行时通过对象名或RGB猜出的分类。
3. Edit模式调用 `VoxelSurfaceMaterialAuthoring.ApplyToComparison(comparison, roleSet, renderers)`，只传本次明确需要换色的对象。接口会先验证所有目标及完整槽，再一次性提交；未知材质、缺槽、跨场景或非持久声明会拒绝，原接线保持。
4. 在独立 Look 中为声明源提供明确 replacement。保留源材质与纹理，材质风格不要烘入夕阳色。需要比较纯配色时，两套 Look 引用相同受光Profile。
5. 在正午与日落检查原C／两候选，再保存重开确认；不要以面板计数代替真实材质与画面检查。

当前 `materialRoles` 是一个明确集合。若已有另一集合，应由作者显式整合；接口拒绝静默覆盖。包含顶点色调色板的未来房屋还需明确保留该数据的响应策略，不能直接套本屋无顶点调色的假设。统一受光登记允许已声明的本屋换色策略幂等重入，不会把它重新改回只受光。

## 新地表与草木接入

先核实际 Renderer 和 Shader。贴图铺在Mesh上仍可能走 `VoxelLit`，不能只因它看起来像二维图，就只检查Sprite入口。体素草顶／土侧／水与树默认保持 `lightingOnly`；它们接受环境与受光响应，不自动参与房屋配色。

当前共享 HLSL 覆盖已经接入环境的 Voxel、场景Sprite／Tile，以及许可后端的原生 PBR／SimpleLit 适配。一个原生 `URP/Lit` 或Unlit对象若未经过现有环境适配入口，不会仅因全局参数存在而自动获得本轮响应；先查实际Pass与显式登记，再接线，不能批量替换全场材质。

环境漫射融合按表面世界位置判定室内外，角色朝向或主控是否进屋不作为全场颜色开关。为可见性刻意放大的房间体积不应顺带把房屋外壳当室内；需要时在 `EnvironmentSkyBinding.indoorVolumeOverrides` 为已登记 `RoomRegion` 明确配置照明专用 `BoxCollider`，只覆盖它的环境室内矩阵与尺寸。这个Box可以禁用，仅作OBB数据；不要启用碰撞或把它用作新的进入触发器。未提供覆盖的房间沿用原范围；缺引用、重复房间、跨场景或无效尺寸会使环境工作集明确失败，不静默猜测。

照明范围应包含真实地板和室内灯，排除外墙外表面与屋顶。可见性仍使用原 `RoomRegion.volumes`，不为调色缩小房间显示边界。屋顶揭开仍保留遮光与碰撞，不能用染色补偿屋顶Caster问题。

## 没变化时从哪里查

- 材质候选：确认当前已选新增木屋Look、真实槽在映射中、Renderer不是由其他系统接管；“统一受光”数量与“候选换色”数量不需要相等。
- 环境候选：确认B已启用、Profile已登记、当前贡献不是“原场／仅日照”；零强度、无效目标色与室内资格会保留原结果。
- 看诊断：既有体素0～7分项仍区分固有色、法线、主灯漫射、间接漫射、镜面、环境反射和真实阴影。新增融合应主要出现在间接漫射，不应凭空增加主灯分项。
- 局部暖火／冷灯不应一起变橙，黑色无光处不应自发光；若只有最终WorldLook有效，应回查基础环境入口。

## 已知边界与证据

这是有界的美术色度适配，不是新的物理天空积分或实时GI。它重分配已有环境漫射亮度，不给地面复制角色的常亮保底、假法线或轮廓光；角色继续沿用已认可系统。HDR峰值经过原有高亮保护时，最终显示亮度仍可能随色度变化，不能把“漫射亮度守恒”理解为所有最终屏幕像素不变。

函数／数值合同不能替代真实主场、门窗和Player验证。截图、连续录像、保存重开结果及当前未测的性能项目以本目录最终交付记录为准；未测GPU耗时不得从录像帧率推断。参考图只作本机风格目标，不作为本工程自有媒体发布。
