# NG-TERRAIN-01 横版地形样板

地形作者数据保存单元坐标和套件编号。`NgTerrainAuthoring.Paint` 是画板与脚本共用的修改入口，`NgTerrainAuthoring.Bake` 由同一份数据生成渲染网格与现有角色可用的 3D `BoxCollider`。套件提供图件、邻接条件和稳定变体，装饰可独立于地形编辑保存。

在本机已装配样板的工程中，从 `OSSASTRA → 横版` 打开地形样板和地形画板。选中带 `NgTerrainLayout` 的对象并启用画笔。在 Scene 里拖动预览，松开提交一笔，Shift 临时擦除，Esc 取消，Ctrl+Z 撤销。保存场景后可重新打开或在同一菜单进入 Play。

将获授权的自有或合成图片作为套件输入。通过 `Create → OSSASTRA → Terrain Lab → Material Set` 创建 `NgTerrainStyle`，每个图层可选填充、边缘等图件。用 `require`／`forbid` 指定邻接，用 `pixels` 指定源纹理区域，用 `destinationCells` 指定在地形单位中的位置和大小。紧邻异材格仍对碰撞连续，套件可独立选择外观。红色导引框提示所需图件未配置。

仓库保留自有实现、合成合同测试与本说明。发行游戏的二进制、素材、原始摆放数据及本机私有验证场均由本机研究目录管理。公开分支中的打开／试玩菜单需要对应的本机验证场。单向平台、原作全部画法和运行中的玩家挖掘仍是各自后续验证事项。

从仓库根运行合成合同测试。

```powershell
dotnet run --project Candidates/NG-TERRAIN-01/Tests/TerrainContract.csproj
```
