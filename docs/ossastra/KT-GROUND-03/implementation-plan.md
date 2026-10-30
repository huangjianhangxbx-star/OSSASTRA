# 独立高台顶盖 Implementation Plan

> **For agentic workers:** 本轮已获用户执行授权；以当前主会话的subagents按下述互斥路径实施，root独占Unity。未安装的executing/subagent-driven Skill不冒称使用。步骤勾选代表实际回执，不代表用户审美验收。

**Goal:** 新SC_CliffEdgeLab在真实凹形侧壁上精确封口，能继续原生刷改、保存、旋转并用现有角色沿缓坡上下行。

**Architecture:** Tilemap唯一作者格；侧壁Mesh提供显式高度开放环；域验证/耳切/三角相交为纯几何。Binding组织每平台身份/源Mesh/Boundary与派生缓存；Editor桥消费原生变更与原工坊预览，按平台层块构建显示，物理共面与深度偏移独立。

**Tech Stack:** 现有Tuanjie2022.3、URP14.2、Tilemap/Extras，不升级/安装包；已有双角色Root与CharacterController唯一Move。

**Spec:** 用户v1.0计划包六文档（本机研究KT-GROUND-03/00-plan），V01—V26为验收索引，包与参考图不公开。

## Global Constraints

- 方案B独立侧壁＋顶盖；二维mask，不厚化/逐格Renderer/每帧三角化。
- Scene/SO/mesh/material/importer/meta由root Native Unity API生成；禁止文本改序列化资产。
- 新Lab在GroundWorkshop/Samples/KT-GROUND-03白名单；旧场/角色Prefab/原素材/原37dirty/全局设置保护。
- 35°玩法俯角、现有45°旋转/缩放/切控；不新建角色控制器、不做完整多层AI/跨层战斗。
- 几何容差初值：焊接1e-4m，平面残差5e-4m；面积/重心使用double计算，M1原始误差登记，不能临时放宽掩盖裂缝。

## 契约冻结

Namespace `Ossastra.GroundSurfaces`。Runtime定义文件由root持有：

```csharp
// Boundary为可再生连接环证据，不保存作者格。
GroundSurfaceBoundary : ScriptableObject {
  Vector2[] rim; float localTopY; float weldTolerance; float planeTolerance;
  string sourceFingerprint; int revision;
}
GroundSurfaceBinding : MonoBehaviour {
  GroundWorkshopMap map; MeshFilter[] sideWalls; GroundSurfaceBoundary boundary;
  Transform cacheRoot; MeshCollider capCollider; int chunkSize=8;
  bool useCache=true; bool flattenDisplay=true;
  string SurfaceId => map.registration.mapId;
}
```

几何代理独占Runtime/GroundSurfaceGeometry.cs、Runtime/GroundSurfaceGeometryTypes.cs及Candidates/KT-GROUND-03/Geometry/。精确API为`TryExtractTopRim(MeshSnapshot[],float topY,GeometryOptions,out SurfaceDomain,out GeometryIssue)`、`TryBuildDomain(Vector2[],GeometryOptions,out SurfaceDomain,out GeometryIssue)`、`ContainsXZ(SurfaceDomain,Vector2)`、`ClipTriangle(ClipVertex,ClipVertex,ClipVertex,SurfaceDomain,List<ClipVertex>,List<int>)`。MeshSnapshot为vertices/triangles/meshToSurface/sourceId/submesh；Domain只派生ring/triangles/bounds/area；ClipVertex为position/uv/color。代理先发确切类型字段与RED候选，再生产实现。

缓存代理独占Editor/GroundSurfaceMeshGenerator.cs、Editor/GroundSurfaceMeshTypes.cs、Shaders/GroundThinTop.shader和Candidates/KT-GROUND-03/Cache/。`Build(GroundSurfaceBinding,SurfaceDomain,SurfaceLayerInput[],bool clipToBoundary)`返回含chunks及统计的SurfaceMeshBuildResult；输入每层真实Tilemap/Material/layerIndex，输出每块Mesh/mainTexture/sourceMaterial/layerIndex/chunk/sourceTilemap及cells/Sprites来源证据；不创建Asset、不管场景/生命周期，root包装保存与清理。

root：Boundary/Binding/Cache runtime定义、Editor源模型读回/指纹/事务/持久缓存、原工坊变更与错误门、场景组装与Native验证。预览需传有效临时Tilemap和Material，不能只监听原layerBindings。

UI/审计代理：接口冻结后独占Runtime/GroundSurfaceLabView.cs、Editor/GroundSurfaceLabWindow.cs及本机教程/专题验收草稿；不写root服务。所有代理不操作Unity。

## 1. 模型提边与最小封口（M0—M1）

- [x] Native基准/当前场、PrefabStage、版本与保护SHA，9文件计划manifest校核。
- [x] 几何RED候选：L形面积5、跨缺口三角裁为3而非3.125、24缝顶点侧壁提6点环；错误环保持旧状态。
- [x] 纯数组实现临时焊接/degree2闭环、自交/非流形拒绝、耳切Up面、UV/Color插值。先在Native实际跑合同。
- [x] Native保存技术侧壁A.asset，再重载Mesh读TopRim，不能把builder私有多边形直接喂顶面。
- [x] 用真实现有草/道路生成独立顶面；geometry首因测同坐标接合误差和凹口排除。

## 2. 原生几何一致性与块缓存（M2）

- [x] 先真实小矩形Native/缓存A/B：sprite.vertices/uv/triangles、GetCellCenterLocal、orientationMatrix、实际GetTransformMatrix、全父TRS和颜色，旋转/镜像/手工矩阵非交换哨兵。
- [x] 输入层由root有效显示适配提供，不重跑RuleTile；动画/未知动态Tile明确拒绝。
- [x] 域内保持原三角，边缘相交裁剪，域外剔除；不能仅凭三个点在凹域内走捷径。
- [x] 以surface/layer/chunk/texture/material分组；Native Mesh写登记assetFolder，base Collider同域独立于alpha；高台显示共面，Shader深度偏移不使顶盖浮空。

## 3. 原工坊与多表面安全（M3）

- [x] 注册每Map独立资源；旧SaveAs/试玩在任何写入前拒绝不兼容多Map；新Lab提供场景级保存/直接Play。
- [x] 真实NativeBrush手势选当前平台，不误写低地；域外笔画拒绝，已有域外数据保留。
- [x] 预览Existing/Future、取消/提交、Rebuild/Undo/Tile变更触发缓存同步，静止/转相机不重建。
- [x] 切换已保存的侧壁版本重提环；墙盖同时变化、旧Tile格不丢，悬空手摆/锁装饰只标记。
- [x] 两次保存/真正关场重开恢复Map/模型源/缓存/装饰；一次Undo后再保存。

## 4. 成品、角色与观察（M4—M5）

- [x] 低地保留旧原生路径；A凹L/非整格缺口+3m、B斜切+5m；缓坡静态MeshCollider接A，非凸误填禁用。
- [x] 复用原两角色/单敌和镜头，敌攻击关闭；以真实InputAction输入上坡/下坡、切控/八角与低视角。
- [x] 花草每平台真实脚点、边距/间距/所属surface；不因刷新或转相机重撒。
- [x] 最终/裸缝/结构/拆分/未裁剪对照，临时显示不移动真实Collider/作者根；关闭恢复。
- [x] 同内容A/B与高台项消融、有界多轮CPU/可用GPU/几何与资源计数；未知不补造。

## 5. 交接（M6）

- [x] V01—V26逐项实际回执；少量同机位图、短视频、操作/模型契约与本机HTML。
- [x] 旧工坊真实刷改撤销与角色最小冒烟；初始保护SHA核对且原dirty保留。
- [ ] 最终新Lab正常35度/诊断关/敌攻击关/哨兵移除已读回通过；R11定向公开自有代码文档，参考场/纹理/派生Mesh仅本机，核远端SHA、释放租约，不电源操作。

## 实际收口备注

M0—M5及Native交付检查已执行，证据索引见validation.md。严格Native/缓存像素12案为11案精确同、一案单RGB像素采样边界差，总false保留；性能A同内容486三角全帧像素差0，GPU/Player未测。最终分段墙沿55点与实际盖/Collider误差0，normal-final已通过。M6仅精确发布/远端核验与租约释放由root最后补。
