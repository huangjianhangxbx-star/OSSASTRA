# 星骸地表工坊 Implementation Plan

> Agent执行：由root集成，互斥子代理分别实现窗口、原创样本和本机资料；沿用户明确的连续执行授权直接执行，不重复阶段审批。环境中没有superpowers子技能，不伪称已调用；使用实际可用writing-plans/TDD与项目Funplay工作流。

**Goal:** 原创素材→配方/主题→多地图原生作者数据→保存重开→换材/花草的Unity闭环，配套真实离线教材与可追溯图鉴。

**Architecture:** Native Tilemap为唯一格子真相；SO保存资源定义，注册文件和受控目录限定地图所有权。单一作者事务服务掌握地图/资产变更，窗口薄调用；装饰原生实例是唯一位置真相，显式种子生成不在重绘/Play自动执行。

**Tech Stack:** Tuanjie2022.3.62t16、URP14.2、Tilemap Extras3.1.3、Editor API、Newtonsoft3.2.2、本机Python/UnityPy、无CDN静态HTML。

**Spec:** 用户KT-GROUND-02连续执行计划包v1.0，完整来源副本在本机研究00-plan；其02为主规范，03为A01—A25验收。

## Global Constraints

- 原作 `D:/steam/steamapps/common/开拓物语 Demo` 只读；原图/源规则/派生资料仅本机。
- 本轮新增资料预算2GiB；先贯通M3，再扩图鉴。无EXE/全游戏提取/高度地形。
- 原场/角色/全局管线/人工区/原37dirty不修改；root唯一Unity writer。
- Edit ready且非PrefabStage才作者写入；Play前冻结生产C#。场景/SO/导入配置均Unity API。
- 确切原Shader/UV/光照/原作者工具保持UNKNOWN；原创技术样本不是正式商业美术。
- 完成定向GitHub发布，核远端；不Gitee、不强推、不关机休眠。

## 1. 数据契约与写入边界（M0—M1）

Files：Runtime/GroundEdgeFamily.cs、GroundRecipe.cs、GroundTheme.cs、GroundMapRegistration.cs、GroundWorkshopMap.cs、GroundDecorationDefinition.cs、GroundDecorationInstance.cs、GroundWorkshopContracts.cs；Editor/GroundWorkshopOwnership.cs。

接口核心：
```csharp
GroundWorkshopMap GroundWorkshopMaps.CreateMap(GroundTheme theme, string scenePath, Vector2Int size);
bool GroundWorkshopAuthoring.CanEdit(GroundWorkshopMap map, out string reason);
void GroundWorkshopAuthoring.SetLayerLocked(GroundWorkshopMap map, int layer, bool value);
```

- [x] 先执行候选RegistrationContracts：未注册/伪注册/保护场/Play/Stage拒绝，合法三层接受；记录缺功能RED。
- [x] SO只保存定义与版本，MapRegistration保存owner/mapId/scenePath/assetFolder；绑定数组不另存格位置。
- [x] 路径与资产身份、实际scenePath、层归属、有效范围和重复层预检；锁由Brush和批量服务共同消费。
- [x] 原生测试确认拒绝时Tile/场景/资产目录无副作用。

## 2. 原创独立样本与创建地图（M2—M3）

Files：Editor/GroundWorkshopOriginalAssets.cs、GroundWorkshopMaps.cs；生成资产只在GroundWorkshop/Originals、Palettes、Maps/Samples。

```csharp
GroundTheme theme = GroundWorkshopOriginalAssets.EnsureSamples();
GroundWorkshopMap map = GroundWorkshopMaps.CreateMap(theme, path, new Vector2Int(16,12));
GroundWorkshopMaps.Save(map);
```

- [x] 先跑样本和地图创建行为RED；无源媒体依赖、3层与第二张地图独立为预期。
- [x] 自有8邻接解析规则独立生成两种边缘族；3底纹+备用底纹、3装饰、明确技术样本身份；不读源15图/规则。
- [x] 每图独有材质/Tile/主题副本和注册，初始基层填满，Overlay/Road留空；连续3D地面碰撞，普通相机。
- [x] 另存复制同一Native布局并重建自己的配置/资源身份；不串改原图，保存只指向所选场。

## 3. 换材事务与原生笔刷（M3—M4）

Files：Editor/GroundWorkshopAuthoring.cs、GroundRecipeChange.cs；窄扩原FrontierGroundBrush、Runtime/GroundLayer。

```csharp
var change = GroundWorkshopAuthoring.PrepareReplacement(map, layer, recipe, GroundReplacementScope.ExistingAndFuture);
GroundWorkshopAuthoring.Preview(change); // 临时Native显示，不写作者格
GroundWorkshopAuthoring.CancelPreview();
int activeLayer = GroundWorkshopAuthoring.Commit(change);
```

- [x] 用有颜色/flags/手动矩阵与异种Tile的夹具先跑RED；未知Tile阻断整体已有格替换并列身份，不能静默迁移。
- [x] 提交前重新核对上下文、锁、布局版本，资产创建精确ledger，失败仅撤回本次输出；成功单一Undo。
- [x] ExistingAndFuture迁移已识别Tile并更新真实材质/预览/来源；FutureOnly明确新增变体Tilemap，旧层保留。
- [x] 复用NativeBrush的Down/Drag/Up、Esc、失焦/外部Undo策略；锁保护刷/擦/矩形/换材/主题入口。
- [x] 两轮真关场重开、跨chunk邻接和主题隔离；关闭/换目标/Play取消预览和释放Palette。

## 4. 中文窗口与资源入口（M2—M4）

Files：Editor/GroundWorkshopWindow.cs、GroundWorkshopCatalog.cs；Candidates/KT-GROUND-02/UI与Catalog。

菜单 `Tools/OSSASTRA/星骸地表工坊`，制作地图/地表配方/素材/帮助四页。新建、保存/另存、缩略图图层、显隐/锁、刷擦矩形吸管、实际来源、配方预览/取消/提交，所有写操作只调域服务。

- [x] 先跑窗口生命周期/错误目标行为RED，再实现薄窗口；不要仅断言源码字符串。
- [x] 目录和HTML共用 `ground-workshop-catalog` v1稳定assetId/revision；选择集只传ID，hash/路径预检通过才导入受控副本。
- [x] 缺依赖/无效尺寸/缺边缘族有可操作错误；取消不落半成品绑定。
- [x] 真实菜单和SceneView事件完成一条工作故事；物理GUI工具不可用明记UI_PHYSICAL_UNVERIFIED。

## 5. 装饰和主题复用（M5）

Files：Editor/GroundWorkshopDecorations.cs、Runtime/GroundDecorationOrientation.cs、Shaders/GroundDecorationCutout.shader。

```csharp
GroundWorkshopDecorations.Place(map, definition, world, true);
GroundWorkshopDecorations.Scatter(map, variants, bounds, seed, densityPer100, spacing, true);
GroundWorkshopDecorations.EraseGenerated(map, bounds);
```

- [x] 确定性/道路/间距/手摆锁保护先RED；只生成明确区域和本工具对象，不在Update撒布。
- [x] 原生实例保存assetId/生成来源/脚点；单个地图表现控制器处理贴地/固定直立/仅yaw面向镜头，不控制相机。
- [x] 显式相同配置/种子可重现，保存/转镜头/Play不重撒；点放与拖放、擦自动保留手摆。
- [x] 八角及中间角、脚点/深度/人物前后检查；无玩法碰撞默认。

## 6. 本机图鉴与教材（M1—M2、M6）

Files：本机01-evidence、02-catalog、03-tutorial、04-art-guide、05-local-media；公开纯自有工具/模板放Candidates和docs。

- [x] 元数据与引用分类先行；容器/候选/纳入/排除/未知/失败分口径计数，再定向导出。
- [x] 同一manifest生成聚合离线HTML；搜索、筛选、背景、平铺/alpha/脚点、路径、收藏/选择集，file://无fetch依赖。
- [x] 简单Python行为fixture先RED验证路径/hash/分帧/trim与选择集，代表原图像素核对。
- [x] 八章教程和三类绘制规范配实际Native相机场景图（物理UI截图因工具故障未取得）；源事实/兼容/建议分列，模板透明PNG+SVG不伪造PSD。
- [x] 开始使用.html真实汇总工坊/图鉴/教程/模板/已知问题。

## 7. 玩法集成与有界验证（M7）

Files：Editor/GroundWorkshopPlaygroundBuilder.cs（独立可选游戏适配，不进入纯插件包）；两新场；Candidates契约/真实事件脚本。

- [x] 先记录复制源/保护SHA；复制Frontier只在新场注册7层并克隆资源，不保存源。
- [x] 双角色切控/行走/45°旋转和显式开敌后一次真实命中；旧控制和深度模块不改。
- [x] 固定机位/分辨率记录刷/局部刷新/保存耗时与材质/Renderer趋势，未测GPU/Player分开。
- [x] A01—A25逐条用真实证据填写，保留失败和未测；必要验证过后不空跑无限回归。

## 8. 自有包与交接（M8）

- [x] 以显式文件列表导出自有插件/源码包，排除旧游戏适配、参考源数据与受限截图；原创场依赖闭包验证。
- [x] 真实入口可开、引用完整、两地图稳定；旧保护SHA/37dirty保持。
- [x] 中文脚本头与关键事务/所有权注释复核；定向stage并检查无源数据/原dirty。
- [ ] GitHub push核远端，最终Edit/clean自有保存场、精确退订与临时资源清理、租约释放；用户审美/手感待用户确认。


## 执行收口说明

技术步骤已按实际回执完成，详见validation.md；上表的物理UI、审美手感、空工程/Player与其他平台未验边界仍保留。M8的83文件原生包和源码ZIP已逐项核对，远端发布与租约释放待最终交接填写。勾选表示对应技术流程完成，不替代其中明记的用户验收。
