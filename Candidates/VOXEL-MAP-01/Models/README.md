# VOXEL-MAP-01 模型源与导入约定

本目录只有自有蓝图/建模/验证源码。图片参考、实际blend/FBX/GLB/PNG在本机研究目录，不作为第三方美术公开发布。

## 制作顺序与来源

1. `GenerateBlueprint.py`先绘正/侧/顶三视图及拆分JSON；Root与独立监察官看图后放行（蓝图8.2/10）。门按Root最终小修改为2m、中心0，图/JSON/模型一致。
2. `BuildTimberHouse.py`调用现有`Origin/自制工具/voxel_workbench/addon/api`创建受管理recipe块，保留原生源blend；只在导出副本中按语义合并，使用既有`api.export_asset`调用官方FBX/GLB导出。不修改或另造工作台建模器。
3. `BuildTrees.py`经同一API生成层叠绿松与多干红伞冠两种树；`AuditModelExports.py`在新的Blender场景重导入实际FBX，核对语义数、真实门窗射线与屋顶遮光。此检查不替代Unity导入/碰撞/可见性验证。

主参考2只展示木构剖开室内：宽檐层叠、粗柱/深梁、暖板/灰蓝基座是可观察特征；完整坡屋顶属于合理外推，不宣称逐像素复刻。树体块由参考1/3/4启发，所有材质为原创实色，没有参考贴图。

独立监察官实际查看最新灰盒/闭顶/揭顶三图并复核源占格后，首轮资产评分8.1/10（轮廓1.7、风格1.6、色光1.6、尺度1.7、适配1.5），放行进入独立Unity场装配；美术返工0/6。中途承檐封口与证明范围纠正属几何/证据校正，不冒充美术达到新分数。该资产放行不代签Unity受光、碰撞、揭顶功能、用户审美或整轮任务完成。

## 房屋接口

- 固定0.25m格。地板顶为Unity Y=0，基础向下0.25m；外墙平面范围8×7m、一般墙段到3m，承檐结构封口到3.25m，两侧阶梯山墙最高4.5m，顶盖最高4.75m。全屋含檐9×8m。
- 正门朝Unity-Z，净孔2×2.25m，中心(0,1.125,-3.5)；侧窗中心(±4,1.625,0)、净孔1.5×1.25；背窗中心(0,1.625,+3.5)、净孔2×1.25。
- `.blend`含449个受管理源块及8个合并语义mesh；FBX只导出8个语义mesh，没有逐体素GameObject。内部面保留，不冒称外壳抽取或单draw。
- `Structure_Floor`、`Structure_WallSouth/North/West/East`为结构；侧墙包含山墙和封口。`Cover_RoofSouth/North/Ridge`为可隐藏顶盖。墙体/封口不并入Cover；模型本身没有视野权威或透视逻辑。
- 灰色长屋面条是实体roof.base面而非透明槽。1152个顶视列均有Cover；84个Cover-only相邻列的连续性由保留Structure封口接替。仅完整roof+Structure装配态可声明各深度相邻列有共同高度，不能说Cover独立处处两格厚。
- 具体颜色使用`TimberHouseManifest.json.paletteSrgb`。Blender材质名为`VW.ossastra.timber-house.<colorId>`；Unity应按显式后缀映射VoxelLit并保留该sRGB作者色，不从最终截图取色。

## 实际输出

`E:/WORLDCREATOR/XingHaiHuiLang/研究/VOXEL-MAP-01/Models/`

- `TimberHouseBlueprint.{png,svg,json}`、`BlueprintNotes.md`
- `TimberHouse-v1/TimberHouse.blend`、`ossastra.timber-house.semantic-export.material.{fbx,glb}`、`TimberHouseManifest.json`、`house.recipe.json`
- `TimberHouse-v1/HouseClosed.png`、`HouseCutaway.png`、`HouseGraybox.png`
- `Trees-v1/Trees.blend`、`ossastra.tree.{layeredgreen,redumbrella}.semantic-export.material.{fbx,glb}`、`TreesManifest.json`与两种树预览
- `ModelExportAudit.json`：实际FBX重导入8项房屋检查均通过；两种树各2个mesh通过。

Blender内部为Z-up；源描述按Unity(x,y,z)换为Blender(x,-z,y)。FBX由现有API采用`-Z Forward / Y Up`导出。真实FBX节点自动带`.001`等副本后缀，接线按明确语义前缀并核对唯一性，不硬猜完全相等名称。Root仍须在Unity首次导入读实际bounds/方向；不能以Blender往返通过代替该项。

树均2个语义mesh：绿松Trunk+Canopy，82源块/661格；红树Trunks+Canopy，113源块/900格。`TreesManifest.json`含各自材质色与实际部件bounds。

复现：使用Blender5.1.2后台`--factory-startup --python-exit-code 1 --python <本目录脚本绝对路径>`。脚本仅写上述明确研究输出目录，不影响用户已打开的Blender窗口或Unity资产；没有安装插件。当前5.1有use_nodes未来弃用提示，不声称兼容Blender6。

## Root专用Native装配候选

`NativeHouseAuthoring.cs.txt`只供Root通过Unity Native执行；自身没有Run或自动入口。`VoxelTimberHouseAuthoring.Assemble`返回新建筑/单房/四墙MeshCollider/15开孔SightBox/8Renderer/3屋顶/局部暖灯/Trigger雨棚，由Root接入既有唯一Root/Module/Binding；不改旧列表，不SaveScene。identity容器与导入FBX本体分开，保留FBX原导入TRS，避免把Unity轴元数据放进FBX坐标系。14种材质与3孔洞网格只在明确传入Assets目录创建或验证复用，拒绝冲突覆盖；不扫描全场或按颜色猜类别。所有新GO默认层0，Root须按现有碰撞与光照分层接线。当前仅离线实际编译0错0警告；Native场景验证由Root负责，不能把编译记成装配通过。
