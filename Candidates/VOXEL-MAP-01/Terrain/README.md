# VOXEL-MAP-01 原生地形接缝

复用外部 `Origin/自制工具/map_editor` 的 `createAsset / validateAsset → toEditingMap → meshMap`。编辑母体是 Workshop `.xhasset.json`，冻结表面是独立 JSON；Unity 接收器只校验和建网格，不再实现第二套体素 mesher。

## 锁定 API

```csharp
namespace Ossastra.VoxelTerrain
VoxelTerrainSource VoxelTerrainSource.Parse(string authorJson, string surfaceJson);
VoxelTerrainMeshData[] VoxelTerrainGeometry.Build(VoxelTerrainSource source, bool linearVertexColors = true);
```

`VoxelTerrainSource` 的只读属性：`AssetId : string`、`SourceSha256 : string`、`VoxelSize : float`、`CellCount : int`、`QuadCount : int`、`ExposedUnitFaceCount : int`。

`VoxelTerrainMeshData` 的只读属性：`ChunkKey : string`、`Vertices : Vector3[]`、`Normals : Vector3[]`、`Uv : Vector2[]`、`Colors : Color[]`、`SubmeshTriangles : int[][]`。三个槽固定为 `grass / soil / basin`；空槽保留空数组。每块由 Native 创建一个 Mesh、MeshRenderer 与 MeshCollider，材质由调用者提供。UV 为转换后的 Unity 世界 X/Z 米坐标除以 16。

`Build` 返回纯几何数据，不创建 GameObject、资产或材质，不修改场景，不包含 Update 或管理器。默认将十六进制 sRGB 顶点色转换为 linear，Gamma 项目可显式传 false。

## 数据契约

母体必须是 `xinghai-workshop-asset-1`，`voxelSize = 0.25`，8 色调色板保留；坐标为整数格的 `RH_Z_UP`，`anchorM = [0,0,0]`、`rootMode = grid`。本轮母体无实例变换；支持范围内的非零源坐标，不重新居中。

```json
{
  "schema": "ossastra-voxel-surface-1",
  "sourceSha256": "母体原始 UTF-8 无 BOM 文本的 SHA256 小写十六进制",
  "assetId": "voxel-map-01-terrain",
  "revision": 1,
  "voxelSize": 0.25,
  "axes": "RH_Z_UP",
  "materialRoles": ["grass", "soil", "basin", "soil", "grass", "soil", "basin", "soil"],
  "quads": [
    {"axis":2,"sign":1,"plane":0,"a":0,"b":0,"w":1,"h":1,"color":0,"chunk":"0,0,-1"}
  ]
}
```

`quads` 保持原生 `meshMap` 的整数坐标和 16³ 分块。`axis` 为法线轴；另外两轴按 0、1、2 的顺序映射到 a、b。正向面 plane 为 cell[axis]+1，负向面为 cell[axis]。草槽只允许源 +Z 面；其他面必须保持母体 sideColor 规则。

接收器严格拒绝：源哈希/身份/修订不匹配、非 0.25 单位、未知轴、非零锚点或旧 rootMode、无效色值/角色/坐标/重复 cell、超预算、内部面、漏面、重复面、越分块 quad 与违反原生颜色规则。上限为 250,000 cells、250,000 quads、1,000,000 vertices；不静默补洞或修复。

源坐标转换为 Unity `[x,z,y] * 0.25`，该轴交换反转手性，三角绕序必须随之修正；法线必须指向真实实体外部。屋顶、角色控制、碰撞挂接、旧支持面迁移由主执行者处理。

## 本轮布局约束

圆角超椭圆母体 X 半径 32 m、Z 半径 26 m、幂 4，地面 top=0，底=-0.75 m。保留 `(7..21,-24)` 物件排与原房屋 `(-15,-10)` 的平坦地坪。两小坡位于 `(-24,-4)` 与 `(24,12)`，每级 0.25 m；水洼中心 `(15,-16)`、半径 3/2 m、床底 -0.5 m、水面 -0.18 m。原 3/5 m 高台与其物理留给主执行者迁接。

先由 `VoxelTerrainFeatureContract.cs.txt` 在实际 Unity Native 环境得到有效 RED，再写生产接收器。该测试通过反射识别缺失类型，不以编译错误冒充 RED。

## 运行与当前产物

```powershell
node --experimental-strip-types --test E:/WORLDCREATOR/new/Project_ANKALUTE/Candidates/VOXEL-MAP-01/Terrain/GenerateTerrain.test.mjs
node --experimental-strip-types E:/WORLDCREATOR/new/Project_ANKALUTE/Candidates/VOXEL-MAP-01/Terrain/GenerateTerrain.mjs
```

生成器仅调用既有地编模块，不改地编工具。默认输出在 `E:/WORLDCREATOR/XingHaiHuiLang/研究/VOXEL-MAP-01/Terrain/`：母体 `voxel-map-01-terrain.xhasset.json`、冻结表面 `voxel-map-01-terrain.surface.json`、`terrain-layout.json` 与生成报告。生成器的实际 RED 是“native terrain generator is missing”；实现后 Node 合同通过，覆盖边界业务样本平坦保留、两坡实际高度、实际挖凹体素与 8 色母体。

初次产物：150,760 个实体 voxel、49,388 列、3,086.75 m²、1,902 quads、7,608 顶点、224 原生 chunk、102,364 外露单位面。源 SHA256 为 `f4635f08f266280976712561872b72fb0329cb1b40223684fa9a529c04afcbf0`。母体约 7.69 MB，冻结表面约 164 KB；不新增贴图或重复音像归档。

`terrain-layout.json` 使用 Unity Y-up 米坐标，`pond.waterTilesM` 为 304 个实际凹地格的左下角 `[x,z]`，`waterTileSizeM=0.25`，在 `waterLevelM=-0.18` 建水面即可准确贴合凹地，避免椭圆面跨到实体岸上。水洼最低实体床顶为 -0.5 m，外围浅床顶为 -0.25 m。

`VoxelTerrainGeneratedContract.cs.txt` 使用真实输出 Parse/Build 后，直接测量 Unity 三角面的两坡、凹地、原建筑和业务边界高度，并检查全体绕序、角色槽与 world UV；它只返回验证结果，不创建资产或更改场景。Native 编译和合同结果由唯一 Unity 写者记录，本目录不自行宣称尚未执行的 Native GREEN。
