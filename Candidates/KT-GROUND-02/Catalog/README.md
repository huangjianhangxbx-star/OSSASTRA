# 图鉴共享契约验证

此目录只含自有工具与合成测试，不包含原作清单、切片表、邻接数组、裁片或图片。

- `catalog_core.py`：稳定来源对象身份、透明画布恢复、相对媒体白名单与纯ID选择集。需要Pillow；使用本机既有环境，不自动安装。
- `test_catalog_core.py`：7项行为验证，包括重命名/内容去重不合并语义、trim脚点、路径穿越与源空入口。全部先观察失败，再实现通过。
- `catalog-ui-core.js`、`test_catalog_ui.cjs`：真实页面使用的筛选/分页/代表帧/选择集逻辑；Node运行9项合成契约。
- `CatalogPreflightContracts.cs.txt`：Unity原生API验证9项失败预检，无导入资产写入；仅在显式外部输出目录写4份选择集fixture。由唯一Unity writer执行。
- `GroundWorkshopCatalog.cs.draft`：交给root集成前的冻结草稿；生产真相是`Assets/OSSASTRA/Scripts/GroundResearch/Editor/GroundWorkshopCatalog.cs`，不得把草稿当作另外一套可维护生产实现。

运行Python与Node测试：

```powershell
python Candidates/KT-GROUND-02/Catalog/test_catalog_core.py
node Candidates/KT-GROUND-02/Catalog/test_catalog_ui.cjs
```

Unity预检脚本执行前，root用`UnityEditor.SessionState.SetString`填入两个本次会话键：

- `GroundCatalog.TestManifest`：真实、已验证的本机`catalog.json`绝对路径。
- `GroundCatalog.TestOutput`：明确已创建、属于当前任务的外部证据目录。

它验证版本/修订过期、未知条目、混入静态预览、哈希损坏、路径越界、PNG尺寸不符与未注册条目均被拒绝，且整批未创建任何导入资产。有效导入、复用、Unity保存与显示需要root另行执行并记录，不能由本脚本通过推定。

本机HTML的物理浏览器交互与Source素材的审美效果由实际浏览器/用户确认；Node合成契约不是人工验收。
