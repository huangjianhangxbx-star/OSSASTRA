# Main 半幅草候选装配

Root 独占 Unity/正式源码/资产/Git；本目录只是待 Root 审阅的 Native 候选。未执行 Unity，没有创建 Main 资产。

`GrassMainAssembly.cs.txt` 默认 `Install=false`：仅生成自己的 `artifacts/VEGETATION-01A/main-integration/plan-census.json`，不创建 Unity 对象/材质/资产，不 Save Main。

Root 审阅 census 后才可把 `Install` 设为 true。装配前重新核对 terrain/material/UV/顶点色/建筑/道路/高台事实指纹及原 B 完整配置指纹。若编译重载重建临时旧高台碰撞对象而导致指纹变化，需保留旧 census、检查差异并明确另起收据；不要删除旧收据后盲目重放。

## 复用和布局

- 原 Native installation 的 224 对 mesh/collider；仅 `Terrain_Grass` 材质槽和世界朝上的三角形，按三顶点世界坐标去完全重复。
- 按 XZ 投影面积精确二分求 X 中线，裁切三角形取得正 X 半边。半面积指排除道路/房屋/上方地台之前的有效 Grass 顶面面积。最终真实根点逐项排除；census 仅以各小三角形中心估算排除面积，不冒称精确几何布尔面积。
- 已登记建筑 occupancyVolumes 的 XZ 占地外扩 0.4m；按 footprint 排除，不受内部体积下界微抬导致漏根。
- GroundResearchSurface 中明确登记的“浅石道路” Tilemap，匹配其真实高度，根点与四个 0.26m 边距点排除。包含仍保留的旧作者层，不把它们重新显示或修改。
- 实际启用的 Native collider，以及明确旧 A/B 高台与原连续缓坡 collider，从上方向下单 collider Raycast 拒绝更高表面之下的根；不全场扫描所有碰撞。
- 地形和碰撞保持；只有新 survey Collider 在生成期间短暂启用，完成后禁用。单 GrassLayer 使用 survey Transform 重建根点，所有生成显示仍为现有小分块。
- 复制完整已认可 B Profile 和 Material；36/m²不改，增加独立候选最大容量至有限需求（最高120000），不改原 B。

## 基础色

临时代理采用已支持的 URP Unlit，并保留原 Grass BaseMap/ST/BaseColor。代理顶点色折算原 `_VoxelVertexColors`，然后专用静态 baker 一次生成512²未受光图；不用真实灯光或额外相机。原 Native MPB/Detail/Parallax 等未支持项会拒绝，不能悄悄忽略。代理对象、mesh、material finally 精确销毁；永久仅保存一张颜色图和映射资产。

## 生命周期与结果

- Root 的 GrassLayer 可选 `registerRoomVisibility` / `roomExteriorAnchor` 必须已编译才允许装配；设 true 与 (4,0,-16)。候选不编写或绕过 Room 后端。
- 只保存 `Assets/OSSASTRA/WorldRendering/Vegetation/MainGrass/` 的六项新资产；Main 仅新增单根并标脏，**不 Save Main**。由 Root 完成黑幕/风/日夜/性能检查后统一保存并重开读回。
- 任何突变失败留下 `assembly-receipt.json` 的 partial 状态及精确新资产列表供审查，不自动回滚别人的内容、不自动重放。
- 编译检查：独立 AssemblyCompile.csproj，使用现有实际 Assembly-CSharp/Unity/Funplay 引用。此检查不代表 Native 几何或画面验收。
