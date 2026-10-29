# 星骸地表工坊窗口设计与实施计划

**Goal:** 用一个中文 Unity EditorWindow 完成选地图、选层、编辑、配置与来源导航，不另造地图数据或 runtime HUD。

**Architecture:** `GroundWorkshopWindow` 只保存短期选择、工具所有权和预览会话；地图、配方事务和装饰规则调用 root 所有的服务。实际画笔继续使用 `FrontierGroundBrush` 和原生 SceneView/Tile Palette。

**Tech Stack:** Tuanjie/Unity Editor IMGUI、原生 Tilemap、ScriptableObject 配方/主题；无第三方 UI 依赖。

**Spec:** 用户批准的 KT-GROUND-02 v1.0，尤其主计划 §§3–8、A07–A20。按用户连续执行授权与 root 的互斥代码分工实施，不重复征求设计批准。

## 写入范围与约束

- 唯一生产文件：`Assets/OSSASTRA/Scripts/GroundResearch/Editor/GroundWorkshopWindow.cs`。
- 测试：`Candidates/KT-GROUND-02/UI/`；无 Unity/MCP、meta、场景、资产或其他生产写入。
- root 独占共享模型/服务和 Unity 验证。生产写入须收到 RED 与 API 契约后的 go。
- 不引用 Character、旧 SceneBuilder 或 GroundSourceImporter；公开包不依赖其编译存在。
- 所有写入口先通过 root 的注册地图、Edit ready、非 PrefabStage 检查；旧保护场拒绝。
- Palette、选择、Scene 隐藏只恢复仍与本窗最近写入值相同的项；不覆盖后来外部操作。

## 操作设计

### 四页入口

1. **制作地图**：顶部当前地图和可写/只读原因；新建/保存/另存；主题副本；带缩略图的图层、锁和 Scene 显隐；刷/擦/矩形/吸管、画笔大小；装饰点放/撒布/擦生成项，脚点策略与稳定种子。
2. **地表配方**：当前层配方、底纹、边缘族、世界周期；选择“后续笔画”或“已有同类地块”；通过服务生成影响预览，明确未知 Tile，随后提交或取消。预览未提交时切图/关窗/Play 必须取消。
3. **素材**：查询与类型筛选、缩略图、用途/证据/兼容状态；同一 assetId 的工程定位/来源图鉴；导入 HTML 选择集。大图按需，不在 OnGUI 写导入资产。
4. **帮助**：开始使用、组合教程、绘制规范和图鉴入口；来源边界、操作提示、当前只读原因；链接缺失显示真实缺依赖，不造死链接。

### 观察和生命周期

- 选层变更先提交当前笔画；地图变更提交笔画、取消预览、恢复自己拥有的 Scene 观察，再绑定新地图。
- Play/脚本重载/关窗释放画笔，解绑本窗订阅，不删除地图或素材。
- 原生导航保持：Alt/中右键交给 SceneView；Esc 的单笔取消复用旧 Brush。
- 窗口不自动保存所有 Scene；保存只调用当前自有地图服务。
- 锁是 root 作者服务和 Brush 强制门，窗口仅展示与发起命令。

## 实施步骤与测试

- [x] 阅读计划、brainstorming、writing-plans、TDD 与 writing-good-tests；阅读旧 Window/Brush/Surface。
- [x] 提出最小接口需求，交 root 冻结。
- [x] 先提交 `WindowGateContracts.cs.txt`：正式 Window 缺失应明确 RED；已有类后在未注册 PreviewScene 上调用真实公开操作，证明不能启动画笔、不能改变 Tile/Palette。
- [x] root 运行缺Window类的真实 RED，并给 exact API 与生产 go。
- [x] 实现四页薄 UI 与地图切换/工具所有权；root Native Gate 合同7/7通过。
- [x] 在root真实注册图上运行Window所有权合同13/13；Native外部target恢复失败与修复轨迹保留。
- [x] 接入配方服务的预览/取消/提交与保存；服务负责事务，窗口不重做格迁移。Core20/SaveAs19提供技术支撑，物理UI另列。
- [x] 接入装饰与Catalog；Native装饰17+9、目录114+预检9支撑服务链，未冒称实际鼠标页面验收。
- [ ] root 定向集成、截图与真实 SceneView 流程；物理设备不可用标记 UI_PHYSICAL_UNVERIFIED。

## 需要 root 冻结的接口

注册组件类型；可写检查；地图新建/保存/另存；主题复制；配方迁移预览及取消/提交；装饰点放/撒布/擦除/保护；Catalog 列表/选择集导入/来源；`EnsureSamples()` 返回内容。窗口公开正常操作者方法 `SelectSurface`、`SelectLayer`、`CanAuthorSurface(out reason)`、`ActivateBrush(Type)`、`ReleaseAuthoring`，内部按钮使用同一入口。

## 自审

窗口未拥有第二份格子表；无生产字段/方法只为测试；故障和只读原因对操作者可见；先 Gate RED 后生产；旧场拒绝、多图隔离、预览取消与工具恢复都有真实副作用断言。服务 exact API 冻结后以实际声明更新调用，不用 reflection 隐藏生产契约。

## 实测失败与定向修复记录

root Native 首次 `WindowOwnedStateContracts` 为3通过/1失败，前三项选层/画笔/越界正确；增强同一合同的stage与完整BaseException后，再次定位 `SetMaskDebug` 的 `Renderer.GetPropertyBlock(readBlock)` 收到null，renderer和夹具原块有效。证据为本机 `01-evidence/window-owned-first.json`、`window-owned-red-stack.json`，没有把失败删掉或改成通过。

缓冲修复只将窗口观察块改为明确NonSerialized字段，并在OnEnable、SetMaskDebug、RestoreObservation正常入口以显式if惰性创建。随后真实Palette trace证明外部target赋值生效，但恢复brush/tool会触发Native默认target。已按字段独立归属捕获前值，完成Native副作用后仅修复已知fallback并保留有效外部target；不销毁仍active的ownedBrush。完整MPB与外部新值逻辑保持，root的PlayHooks与图鉴路径保留。

root `window-owned-state-i.json` 13/13已通过，`actual-gesture-poll.json` 86检查通过。帮助可移植路由新增实际缺resolver RED，参考试玩返回后仅将本机优先/项目Documentation fallback窄补丁应用Window，当前待prepareM及Native复验。UI_PHYSICAL_UNVERIFIED、无实际Editor主窗截图、独立新工程/Standalone未测继续保留。
