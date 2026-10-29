# KT-GROUND-02 验证记录

状态：本轮范围内技术交付与三次试玩往返已关联，83文件原生包/源码ZIP已导出并核验，保护指纹18/18保持。产品8cba9ffcd4de3e4ac566131d0094ad218326f8a8已推送并与GitHub main精确核对；全部writer/路径租约已释放。

实施基线为 `19ea13bdfd56b1f79f371441c739cbc607b6b950`。本轮开始的37项dirty与18条保护指纹按原清单保护，不把新增文件混算为原dirty。原始Native回执位于本机研究目录的 `01-evidence/`；含私有参考媒体或可逆源表的资料不复制到公开仓库。

## 交付与使用入口

- 工坊入口：`Tools/OSSASTRA/星骸地表工坊`，四页为制作地图、地表配方、素材、帮助。原生Tilemap是唯一格子作者数据；注册地图在Edit ready且非Prefab Stage时可写。
- 原创场：`Assets/OSSASTRA/Scenes/Research/SC_GroundWorkshopOriginalLab.unity`，3层、192/32/19格、44装饰；道路为本轮最终修正的连通弯路与分叉。
- 本机参考场：`Assets/OSSASTRA/Scenes/Research/SC_GroundWorkshopLab.unity`，7层、43装饰。源媒体与私有图鉴只在本机。
- 可选完整工程试玩：工坊“保存并进入角色试玩”；菜单 `Tools/OSSASTRA/地表工坊/打开双角色工坊场` 与 `Tools/OSSASTRA/地表工坊/返回作者地图`。使用临时副本承接既有角色/镜头/单敌，结束恢复作者图；Core插件不包含另一套角色控制器。
- 纯原创公开指南位于 `Assets/OSSASTRA/GroundWorkshop/Documentation/03-tutorial/地面组合逻辑教程.html` 与 `04-art-guide/素材绘制规范.html`，实际文件存在；包保持两目录相对结构。帮助优先打开用户设置的本机资料根，缺省回退项目内Documentation；无私有图鉴时明确提示。候选源在 `Candidates/KT-GROUND-02/UI/PublicGuide/`，不要求使用者进入该候选目录。

## Native合同与失败历史

数字是相应合同的断言数，含校准、清理和重复边界；不能相加为独立功能数或全场景覆盖数。

| 实际回执 | 结果 | 主要范围 |
|---|---|---|
| author-tile-semantics-green.json | 7/7 | 原生Tile/RuleTile flags、规则显示变换与作者手动矩阵语义 |
| core-recipes-i.json | 20通过 | 迁移预检/预览/取消/提交、属性保持、Undo/Redo、Future追加变体层、锁、未知与过期拒绝、磁盘保护 |
| save-as-isolation-i.json | 19通过 | 新身份与主题/Tile/材质/装饰独立，作者属性/手动TRS，两个真关场重开round |
| window-gate-green.json | 7/7 | 空/未注册地图与可编辑门 |
| window-owned-state-i.json | 13通过 | 原生选层/刷子、越界拒绝、完整MPB恢复、独立外部Brush/target/观察归属 |
| actual-gesture-poll.json | 86通过 | Native Down/Drag/Up、矩形、Alt/MMB/RMB导航排除、Esc、失焦、外部Undo组与作者数据恢复 |
| catalog-import.json / catalog-preflight.json | 114 / 9 | 600条目录加载，代表性Texture/Sprite/边缘族及选择集导入；过期/未知/坏hash/尺寸/越界等整批拒绝 |
| originals-first-green.json | 28通过 | 原创3层、两套256精确规则族与实际邻域、alpha/PPU/pivot、原创装饰及无参考依赖 |
| decorations-first.json / decorations-edge-first.json | 17 / 9 | 脚点、稳定撒布、间距/道路/范围、手摆/锁、非均匀父TRS与外部组漂移 |
| playground-cleanup-continuation.json | 7通过 | 合法“场景已删”前缀续清Prefab/marker/空目录，作者SHA与SceneSetup保持；未注入随机底层DeleteAsset失败 |
| help-routing-green.json | 7/7 | 真实本机文件优先、项目文档回退、私有图鉴缺失提示、绝对/越界/空路径拒绝 |

保留的真实失败包括首次创建Theme卸载生命周期、Core/SaveAs作者属性持久化、Window的null MPB与Native brush/tool恢复覆盖外部Palette target。完整RED栈/trace和后续修复回执均保留；最新Window13项通过没有删去此前失败。帮助先出现缺resolver的真实RED，再修至7/7。

`FutureOnly`明确追加后续绘制变体层，旧层保留；原生Tilemap单材质条件下不暗改同层旧格RGB。`ExistingAndFuture`迁移已识别旧格，保留作者颜色/flags/有效手动矩阵；未知Tile拒绝迁移。

## 地图往返、输入与旋转

参考首次Preview与原创第二次Preview均实际具有两位存活玩家、一个初始关闭攻击的敌人；分别为7层/43装饰与3层/44装饰。各自8个碰撞为3个CharacterController加1连续地面和4边墙，本次未见旧floor/墙重复碰撞混入。

`reference-preview-returned.json`、`original-preview-returned.json` 与 `reference-yaw-final-returned.json`：三次均返回对应作者场Edit/clean，HasSession=false、editable=true、authorHashUnchanged=true、returnedIdentity=true。原创作者图本身为3层44装饰且无角色/敌人；临时试玩对象没有写回作者场。第三次最终Yaw参考图仍7层43装饰、两原角色/单原敌且敌攻击关闭，不以运行中截图替代退出清理。

`reference-input-story-result.json`：75.1848秒，24/24。配置输入链到达3072m²四角，滚轮遍历八个45°扇区、缩放、两次Current切换保持镜头；具体受控角色的Applied回执证明敌HP600→558，敌原动作释放使玩家HP200→170，再走既有重置。物理硬件与人工手感未验证，不能把此结果描述为物理键鼠试玩验收。

`original-combat-result.json`：5.1239秒，7/7，捕获的实际Current沿原动作时钟释放、获得专属Applied回执，敌HP600→558；敌原释放/玩家HP200→170有记录，原重置恢复存活身份与HP并保留观察yaw。hardwareVerified=false，不把这7项交战检查扩大为原创场完整24项输入故事。

`original-rotation-result.json`：8稳定角+1过渡角、9张1600×900原GameplayCamera.Render，13个Flat、31个UprightYaw，576条帧记录。`reference-yaw-rotation-final.json`：本图43个草丛Yaw适配后重跑8+1角、9图、569条帧记录。两者status=PASS、根固定、镜头恢复、对应视觉约束通过；参考90°图已目检，先前侧看薄片已消失。root定向适配保留作者根TRS与Imported原定义。

两场FixedUpright实际实例数均0，`threeModesCovered=false`、`unobservedModesAreNotPassed=true`；未观察模式不计为通过。上述旋转图含参考角色/媒体，publicPackEligible=false，只在本机。它们是相机渲染而非Editor窗口实拍，不能证明全场全素材或长时无闪烁。

## 有限性能证据

`reference-performance-result.json`：2.1915秒、两cycle共8行，每行60帧，固定1600×900/Perspective FOV35且相机漂移0，对比1024/3072m²与角色Renderer可见组合；Editor帧中位数2.4895—2.9076ms。每行地表Renderer7、装饰43、共享材质种类15。角色模拟持续，隐藏组仍有3个enabledActorRenderers（显示组6），装饰保留；不属于纯地表CPU或纯地表渲染成本。

`edit-author-timings.json`：3072m²原创图、3地表Renderer/3地表材质/44装饰；12格Native Begin/Paint/Finish为11.5795ms，42次Native RefreshTile为0.0936ms，Undo恢复基线后Maps.Save为622.9491ms。保存前后作者数据恢复，原创、参考与旧Frontier三场SHA相同。它是有界Editor API单次耗时，不能代替物理拖刷延迟、逐帧手感或最坏情况认证。

GPU为UNKNOWN；未测Standalone/Player、目标设备、长时泄漏，不作成品帧率承诺。

## 素材、教材与公共包边界

扫描范围为Demo build26043868的263个Addressables bundle，其中6候选、257排除、元数据失败0。资源11820行：Included599、Dependency210、Excluded10115、Unresolved896；图鉴600条另含1个边缘族wrapper，Ready57、StaticPreviewOnly418、Unresolved125。解码纹理身份202、提取Sprite身份537，媒体54,848,376字节，源写入0；bundle之外内嵌资源未扫描，复杂环境动画只作静态预览。

`source-fidelity-validation.json`：2937/2937，核验62个Texture2D与537个Sprite的源RGBA/画布等；实际未遇旋转打包Sprite，不能宣称该分支已有样本实测。SOURCE_VERIFIED、UNITY_ADAPTATION、INFERRED和UNKNOWN分别记录；原精确Shader/UV/光照/编辑器未恢复为已知。

原创教材最终 `Story_af1facdf26994ef4ab3c532ec4a83005/story.json`：21检查、7帧渲染通过，baselineRestored和sceneDiskShaUnchanged=true。实际32×32底纹导入/3×3、15×15拒绝、32格Overlay预览/取消/提交/Undo与19格连通道路、44装饰保持均有检查；sceneDirtyAfterUndo=true，不把该回执直接当作者场最终clean证明。

PublicGuide冻结payload为21文件、1,655,482字节：原11份自有文字/图示/CSV/练习，加7张纯原创NativeCamera.Render完整1024×576 PNG、2张32px/96px练习PNG及去绝对路径的capture-provenance。7原图与Native最终图逐SHA相同，没有私有参考图/媒体/可逆源规则表；qa目录不进payload。

`public-manifest.json` SHA256：`be51f922b16b795f28a095dd6f2203ca2946534772653102b1db607360992da1`。两指南和本机入口在1280/720宽度共10视口headless文档QA通过，破图、断链、横溢、重复id均0；这是独立HTML排版检查，不是Unity UI实测。

`Compile_ffb9994f9ae84bbaa691fdbeafaf7cfb/compilation.json`：独立Editor Core真实25份源编译，排除172个游戏/第三方DLL引用，0error/1warning，输出存在、源保持；警告为TextureImporter.spritesheet弃用。该源编译回执不验证Shader；本版本两个Shader另有Native实际渲染与最终消息[]。**空工程实际导入、Player构建和其他渲染管线仍未验证**。

## A01—A25 对照

“已支撑”只指列明范围；没有以有限合同替用户签署审美、制作体验或未测平台。

| ID | 已有真实支撑 | 未测或root收口项 |
|---|---|---|
| A01 现场保护 | protected-final.json为18/18相同，含全局设置/旧场/原模型相关保护；最终Editor clean | 原37dirty路径/status保留的精确stage审计随发布回执关联 |
| A02 来源分层 | 两指南和研究资料明确四种证据等级 | 不宣称精确原Shader/原编辑器 |
| A03 范围清单 | 容器、源对象、图鉴分口径计数及纳入/排除/未知 | 不覆盖bundle外内嵌资源，未知不算适配 |
| A04 保真 | 2937检查、实际62纹理537Sprite | 无旋转打包样本，任意格式不在承诺中 |
| A05 离线图鉴 | 本机目录/图鉴生成、600条与114+9导入/预检协议 | 检索/筛选/背景/平铺最终浏览交互回执由root关联 |
| A06 共用身份 | HTML/Unity的assetId/修订、选择集与代表导入链 | 不把代表导入扩大为600条全部实装 |
| A07 中文入口 | 四页、Gate7、观察/工具13、帮助7 | UI_PHYSICAL_UNVERIFIED，Editor主窗口实拍缺失 |
| A08 地图隔离 | 参考7层/原创3层、SaveAs19、未注册/旧图拒绝，三次返回原图SHA/身份一致；最终参考图Edit/clean | 不把已测两图扩大为任意工程/外部资产组合 |
| A09 连续绘制 | Native SceneView事件86检查含导航排除 | 自动派发事件不是物理硬件输入 |
| A10 编辑事务 | Undo/Redo、矩形Esc、失焦、外部组与Palette/MPB归属 | 任意外部Editor扩展组合未认证 |
| A11 保存重开 | SaveAs两次真关场重开，三次Play返回clean/SHA/身份一致，18保护指纹不变 | 未穷举强杀或底层磁盘故障 |
| A12 配方替换 | Core20两种scope、已知迁移/未知与过期拒绝、作者属性保留 | 不承诺全部未来素材族 |
| A13 失败/回滚 | 无效/缺失/过期/取消拒绝，合法删除前缀续清7 | 随机底层磁盘/删除失败未注入 |
| A14 共享隔离 | 主题COW、SaveAs独立Tile/材质/主题与两round | 只证明已测共享路径，不代表全工程隔离 |
| A15 基层/边缘/锁 | 原创族实际邻域、Base192格、Native锁/笔画、19格连通分叉图 | 全素材全场景chunk/视觉组合未穷举 |
| A16 装饰摆放 | 脚点/间距/道路/范围与非均匀TRS17+9，真实Preview8碰撞构成 | 每个素材的视觉适用性不由代表样本代签 |
| A17 稳定与保护 | 稳定种子、手摆/锁、局部擦除、SaveAs重开，三次往返作者SHA及18保护指纹保持 | 任意未来装饰/种子组合未穷举 |
| A18 旋转视觉 | 原创13Flat+31Yaw、参考43Yaw各8稳定+1过渡真实图，根固定；90°薄片修复 | 两场Fixed实例0，物理/全场全素材长时无闪烁未认证 |
| A19 角色试玩 | 两图真实2玩家1敌；参考输入24与原创交战7均含Current专属Applied与敌原攻击 | 物理硬件/手感未验收，不把原创交战7当完整输入故事 |
| A20 生命周期 | 工具观察归属、预览保存、合法续清、三次返回无Session，最终临时资产0 | 全DomainReload/强杀/磁盘失败组合未穷举 |
| A21 性能 | 固定条件Editor8行；12格API/42刷新/基线保存单次实测，三场SHA不变 | 物理拖刷/GPU/Player/纯地表CPU/设备/长时未测 |
| A22 跟做教程 | 八章、拆层/mask/边缘/原创换材7真实帧，本机参考图；10视口HTML QA | Editor主窗口实拍与物理操作未验证 |
| A23 绘制规范 | 底纹/mask/Sprite规范、SVG/透明PNG、原创256CSV、实际32PNG+3×3下载与错误检查 | 技术样本不是正式美术或用户绘制验收 |
| A24 自有包 | 原创无foreign依赖、指南21/SHA、25源独立Editor编译；Native83数据与metadata归档复核、实际两Shader渲染 | 空工程实际导入/Player/其他渲染管线未测；独立Editor编译不代替这些验证 |
| A25 交接发布 | 真实本机入口与指南链接、失败历史、83文件两种终包/实际SHA，18指纹保持 | 产品8cba9ffcd4de3e4ac566131d0094ad218326f8a8已远端精确核验；281文件allowlist审计通过，原37dirty与Registry17行隔离，全部租约释放 |

## M8 最终填写区

| 字段 | 当前值 |
|---|---|
| 参考Yaw第三次返回 | reference-yaw-final-returned.json：Edit/clean、HasSession=false、editable=true、原SHA/MapID相同、43装饰；三次均完成 |
| 最新生产prepare/ready与Console | task-r.json operation.ready与current_editor.ready均true；final-console-full.json无error；两Shader原生消息[] |
| 最终场景/Edit ready/dirty/Stage/MissingScript | final-editor-checkpoint.json：SC_GroundWorkshopLab，Edit/ready/clean/非Stage、MissingScript0、Window1、预览/Session/临时资产0 |
| 37原dirty与18保护指纹终核 | protected-final.json 18/18相同；37原dirty路径/status保留，精确stage审计另附发布回执 |
| 实际交付包、字节数与SHA256 | Native83文件，2,157,793 bytes，SHA36b662d0586c72fc28849877ab4177579231b60de27047c5223dba172a461740；SourceZIP2,043,911 bytes，SHAea1866d3b70bd7c816da66681277280714837ad008dda4530145884391cae8db |
| 定向本机提交与远端ref/SHA | 产品8cba9ffcd4de3e4ac566131d0094ad218326f8a8；git push成功后ls-remote origin refs/heads/main相同 |
| 共享sync租约释放 | complete/read-only，全部writer及路径租约释放 |

**UI_PHYSICAL_UNVERIFIED**：真实桌面工具初始化/reset报kernel assets error3，物理鼠标流程与Editor主窗口截图未取得。相机渲染、Game View抓取、自动SceneView事件和合同分别记录。审美、手感、正式美术适配与制作体验待用户；本轮不关机或休眠。

### 最终补充

最终Yaw配置另跑8行有限Editor采样，中位2.3777–3.1091ms，原生作者状态已恢复；隐藏请求组仍有3个角色Renderer，不能按mode标签解释为纯地表。83个Native包数据字节逐项与计划SHA一致，83meta仅原生导出器把Tuanjie编码顶层GUID写成AssetDatabase GUID，其余字节一致。源码ZIP逐字节复制原生导出内容。83条归档/metadata复核、21公开指南、22模板、三层原创依赖闭包均无LocalReference/LocalGenerated/角色/参考场。

独立Editor源编译与本版本两Shader真实渲染已验证；空工程导入、Player和其他渲染管线仍未验证。Native包不是包含参考媒体的完整工程克隆。所有初次失败与后续修复回执仍保留本机，包括首次SaveAs最后场关闭、Tile Palette刷新手工矩阵、Palette归属、无效输入和导出GUID序列化差异。
