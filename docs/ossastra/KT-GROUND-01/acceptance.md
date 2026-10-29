# KT-GROUND-01｜验收矩阵与待复验项

本表沿用用户计划包 v1.0 的 `02_验收与试玩清单.md` 中 V01–V24 原检查及合格条件，以及执行计划第 10 节 M0–M7 原阶段名称、范围和出口。验证等级为 **B｜模块验证**。

这是本轮最终有界技术验收。所有通过项对应本轮实际回执；物理鼠标/Toolbar人工命中、连续视频级闪烁认证、原作实机与主观观感未代验。重复校准计数不代表独立功能数量。

## 基准与证据口径

- 工程：`E:/WORLDCREATOR/new/Project_ANKALUTE`，接手 HEAD 和 GitHub `main` 基准均为 `e390d1c92ebe8d99ef039b261965926fbd607a6b`；本次只读复核本地 HEAD/分支/远端地址仍与基准一致。
- 原作：用户指定的《开拓物语 Demo》，本机 Steam app 5010220、build 26043868，Unity 2022.3.60f1c1 IL2CPP；只读资源研究，未以原作实际运行画面作验收。
- 本机研究根记作 **R**：`E:/WORLDCREATOR/XingHaiHuiLang/研究/开拓物语地表/KT-GROUND-01`。下述 `R/...` 是本机证据，不代表公开仓库含有这些文件。
- 旧面积：`R/00-baseline/actual-ground-area.json` 的原始 MCP 回执，单人场和双角色场均为 64 个底层 Quad、XZ 并集 **32×32＝1024m²**，连续地面 Collider 为 32×0.5×32；回执结束场景 clean。没有累加草/路叠层面积。
- 新图目标为有效 XZ 覆盖 64×48＝3072m²、比值 3；已由最终保存场和原生Sprite顶点的裁剪XZ并集读回，V08为通过。
- `SOURCE_VERIFIED` 只指已读原资源及序列化关系；`INFERRED` 指图像语义推断；`UNITY_ADAPTATION` 指本工程自有适配；原 Shader 精确采样/照明、原作者工具及宣传图确切运行位置保持 `UNKNOWN`。

## M0–M7 阶段出口

| 阶段（计划原词） | 范围（计划原词） | 阶段出口（计划原词） | 当前状态 | 已有证据／尚待完成 |
|---|---|---|---|---|
| M0 基准 | 工程、旧场面积、原作版本、工具/Skill/空间、写入租约 | baseline与精确面积口径；无未授权写入 | 通过：限定接手基准 | R/00-baseline；当前 17 个精确保护 SHA、原 37 项 dirty 状态复核一致；最终交付仍须再对账。 |
| M1 调查 | 地表对象/依赖、截图对应、三位置证据链 | research-findings，确定本轮主路线，未知项明确 | 通过：限定静态研究 | R/01-research/research-findings.md；确认自然地表 7 层 Tilemap/TilemapRenderer、14 条原 RuleTile 规则和 15 个 Sprite。三个源保存位置可追踪，宣传图确切坐标和原作实机未确认。 |
| M2 定向导出 | 白名单、完整必要依赖、通道和参数回读 | manifest、联系图、资源到画面对应；无全量解包 | 通过：限定导出保真 | manifest-validation 43/43 项检查；7959/7959 源保存格的 Sprite/Matrix 匹配；15/15 Atlas 切片像素匹配。不是“43个资源”。研究目录逻辑文件大小约 28.3MiB，低于 2GiB 预算。 |
| M3 小样 | 草、道路、凹凸交界/多层小样，在目标URP实际运行 | 正常/底纹/结构对照与差异；不只贴一张PNG | 通过：本轮有界技术出口 | 实际小样先于扩图；URP正常图、Base/RoadMask、13项结构回读及适配差异已存。 |
| M4 作者工具 | 原生画笔配置或小型工具、同源数据、Undo/Redo/保存 | 用户能连续拖刷；退出重开不丢失 | 通过：本轮有界技术出口 | 原生事件实测拖刷/擦除/Box、单笔Undo/Redo/Esc/失焦、两次Close/Open均通过；物理Toolbar点击不代验。 |
| M5 三倍样地 | 64×48候选整图、现有角色/敌人/镜头接入 | 连续可走整图，清晰路网与可解释分层 | 通过：本轮有界技术出口 | 3072实际可见并集、5 Collider、两角色单敌、真实输入四角通过，最终chunk32/显式敌攻击关闭。 |
| M6 定向验证 | 编辑生命周期、跨块边缘、八角旋转、原战斗冒烟和有限性能 | 真实通过/失败/未测证据，不拿旧成绩冒充新验收 | 通过：本轮有界技术出口 | V09–V23本轮限定入口已验证，V20连续视频仍未测；保留原失败与纠偏，不冒充全历史回归。 |
| M7 交付 | 新场打开入口、资料/来源/操作说明、范围内发布 | 用户可立即打开、编辑、保存、查看；租约释放 | 通过：定向发布与租约释放 | 产品e67d40bd408c7a6473565fce166f52d54b26689e已GitHub main精确核验。新场Edit/ready/clean，全部租约释放。 |

## V01–V24 技术验收

“未执行”表示整项尚未取得足够证据；该行列出的局部事实可以已通过。不得把尚未验证的部分省略后将整行改为通过。

| 编号 | 检查（原词） | 合格条件（原词） | 当前状态 | 证据与后续动作 |
|---|---|---|---|---|
| V01 | 来源与工程身份 | 原作本机版本/容器可追溯，正式工程远端、分支、本地dirty与活动租约已核对 | 通过：接手范围 | 原作身份/容器见 R/01-research/research-findings.md；正式工程基准、唯一 root Unity writer 及原 dirty 已登记在本轮任务。发布时再次核对远端 SHA。 |
| V02 | 受保护文件 | 安装目录、开发细则、人工区、原Scene、基础角色Prefab和全局管线无本轮未授权变化 | 通过：精确保护范围 | 最终17/17保护SHA、原37项dirty状态保留；安装目录操作只读，无人工区/制度/旧场/Prefab/管线写入。不冒称全盘字节审计。 |
| V03 | 资源定位 | 草地内部、草路交界、复杂边角三个位置有资源/组件/画面对应；未知明确 | 通过：证据链；原作实机未知 | source-three-locations.json三处源保存格；SourceTrace.json对基础草/道路/复杂边角实际Pick回读Tile/Sprite/Atlas/底纹/源容器。宣传图精确坐标UNKNOWN，未运行原作。 |
| V04 | 提取范围 | 白名单及必要依赖，无全量导出；实际磁盘用量和缓存受控 | 通过：定向研究范围 | 仅所选自然地表依赖；未导出人物、音频、所有地图或完整工程。终审研究目录46,810,989逻辑字节、约44.7MiB，低于2GiB；包含证据及本轮进程临时文件，不是Unity共享Library或磁盘分配大小。 |
| V05 | 转换保真 | 尺寸、alpha、UV、必要颜色通道/控制图和材质参数回读；缺失通道不能称已保留 | 通过：已声明的保真范围 | R/01-research/manifest-validation.json 为 43/43 检查，atlas-slice-validation.json 为 15/15 像素匹配；Sprite Rect/Pivot/PPU/UV 数据及材质参数有记录。未恢复原生 Mip 链、完整 Shader/光照，不宣称全材质等效。 |
| V06 | 结构结论 | 区分原作几何/材质/贴图组织与本工程适配，不能只给“平面+贴图”一句话 | 通过：静态结构范围 | 已确认 7 层原生 Tilemap/TilemapRenderer＋共用规则边缘 Sprite＋不同 SecondTex 底纹。源 7959 格规则回算一致；GPU 批次网格、精确 Shader 公式和原作者窗口未知。自有世界采样/满覆盖底层/裁切/刷地工具另标适配。 |
| V07 | 原始小样 | 至少包含草、路、凹凸交界与相应正常/结构对照；确认后再扩图 | 通过：URP小样后扩图 | build-sample/sample-readback/sample-scene-green和sample-game.png：16×16、256㎡、草/路/凹凸及7层、两角色单敌；其后才Expand。整图另有Base/RoadMask实际显示对照。自有Unlit/16m差异明示。 |
| V08 | 三倍面积 | 实测旧/新有效XZ覆盖与比值；候选1024→3072m²，不能累加重叠层/诊断区凑数 | 通过：有效并集3倍 | 旧实际并集1024；final-VisibleCoverage.json读取192个原生FullRect Sprite顶点、Tilemap变换与实际Shader裁切，XZ并集3072，比值3；连续Collider与Grid范围交叉核对一致。 |
| V09 | 图案尺度 | 三倍扩图后石子、草叶、纹理周期及人物比例不被拉伸改变 | 通过：固定尺度配置 | sample-readback/expand-full/final Scene：Grid尺度4不变、PPU99、Sprite视觉5.1717、纹理周期16、两个角色scale1不变；未用缩放角色或底纹凑面积。16m为自有候选。 |
| V10 | 道路与边缘 | 弯路、路口、端部、凹/凸角、窄段和真实存在的多地表交界没有明显漏片、硬方框或接缝 | 通过：本轮有限观察 | 正常小样/整图、八角angle-0..7、Base/RoadMask截图已逐一观察；道路分支/端部与草路凹凸、手绘7格邻边有实际Sprite。未见明显漏片/方框；不宣称所有任意图案都已穷举。 |
| V11 | 拖刷 | 实际Scene View拖刷实时显示；选取范围正确，UI/视口导航不误绘 | 通过：实际SceneView事件链 | actual-brush-calibrated-result原生Palette+独立临时SceneView、NativePreview校准、SendEvent Down/两Drag/Up使7格实时变化，导航/错误target/窗外不写。没有直接调用Paint造通过；物理鼠标和Toolbar人工命中未代验。 |
| V12 | 编辑历史 | 一笔一次Undo；Redo恢复；取消/失焦无半事务；跨块刷改及邻边一致 | 通过：正常及冲突策略 | 实际单Undo/Redo、Esc、真切窗口失焦、连续Box两次及Box Esc/无新Down拖拒收通过；brush-safety9项证明不回滚/合并foreign Undo，漂移明确保留本笔+警告。chunk4跨块笔画见Persistence。 |
| V13 | 数据持久化 | 保存关闭重开保留人工笔画；重复进入Play不重置；同数据/种子重建稳定 | 通过：同份作者数据 | persistence-result：真拖刷/真擦除Undo、同Lab两次实际Close/Open、新对象实例、7格Tile/Sprite及Rebuild保留。另非Demo哨兵两轮Play进出authorHash六次相同；最终移除自有哨兵并保存示范。 |
| V14 | 生成边界 | 首次生成、重建缓存、重置作者数据分开，重建不静默覆盖人工编辑 | 通过：生成/重建/重置分开 | GenerationBoundary.json 5/5：拒覆盖既有候选、显式Reset清标记、一次Undo恢复、Rebuild保留、夹具恢复。Persistence最后的示范重置为本轮明确测试清理，普通开场不重置。 |
| V15 | 结构观察 | 能显示实际线框/分块/遮罩或图块；关闭层有可解释效果；Shader层不冒充独立Mesh | 通过：实际层/遮罩与原生块 | Base/RoadMask为真实Renderer/MPB，不是假Mesh层；7个Chunk TilemapRenderer最终chunkSize32。Persistence临时road chunk4，7格必跨多个原生块，保存两次设置/Tile/Sprite保留，最终恢复32。原生内部Mesh句柄不公开，未伪造枚举。 |
| V16 | 来源查看 | 点选至少3处不同地面，可定位Unity资产和本机源引用/用途；缺失依赖有明确反馈 | 通过：公开Pick与依赖反馈 | SourceTrace.json三处公开Pick和真实资源字段；MissingDependency-final为1/1，缺默认Sprite明确停止。真正PrefabStage三项拒画/无写入通过。物理Inspector点击需用户使用时确认。 |
| V17 | 真实行走 | 现有角色能走到新图边角，路/草交界不改变未授权碰撞；无旧32×32隐形边界 | 通过：真实输入四角 | full-input-complete：实际InputAction/原Motor到达约(±29.5,±21.5)，超过原±16；连续地面/外墙五Collider。未改Transform、速度、时间或原碰撞脚本。 |
| V18 | 镜头与切控 | 八个45°角能观察地表；滚轮、Z切人及保留角度/缩放行为不回退；Q/E未被占用 | 通过：输入与八角画面 | 实际滚轮90/135/180/225/270/315/0/45度、Page缩放、两次Z切控保角/缩放；八幅Game View正常显示。Q/E未改；使用虚拟InputSystem设备，非实体硬件认证。 |
| V19 | 单敌冒烟 | 同一原有两角色、一敌能完成一次真实交战与显式重置；不修改动作/HP来伪通过 | 通过：实际主控命中因果 | combat-result7/7：捕获Current的ReleaseCount2→4及两个Applied回执，敌HP600→558，敌释放及真实角色伤害观察，调用原Reset复位600/200与保角；无直接HP/动作时钟修改。 |
| V20 | 材质/深度 | 近远缩放、斜角和边界无可复现共面闪烁/Atlas串色；旧Spine深度策略保留 | 有限通过；连续闪烁录像未测 | 八角静态、正常斜视及缩放输入可用，原SpineDepth SHA保留，源Atlas15切片同纹理且无Mip串色。静态画面未见明显穿插/拼缝；未用连续视频逐帧认证所有近远状态，不做绝对无闪烁承诺。 |
| V21 | 图形与资源成本 | 有同条件1倍/3倍及纯地面/带角色的有限统计；只写实际可得指标和环境 | 通过：有限渲染统计；GPU未知 | perf-result两轮×四条件、同1600×900/机位零漂移：带角色1倍/3倍批次46/46、三角2621/3209；隐藏三实体Renderer43/43、1296/1884。玩法模拟/HUD保留；Editor间隔非构建GPU帧率，GPU UNKNOWN。 |
| V22 | 生命周期 | 两轮保存重开/进入退出及反复刷改撤销后，自有Mesh/材质等无已知单调增长，Missing Script=0 | 通过：已测生命周期范围 | 两次真Save/Close/Open；非Demo哨兵两轮Play进出hash六次相同、地表材质7/7且MissingScript0。性能两轮全Editor材质1455/Mesh39无单调增长，作者数据恢复；native chunk Mesh不可归属，不泛化全工程无泄漏。 |
| V23 | 旧场冒烟 | 原双角色场打开、移动/旋转/切人不受影响；无共享代码变更时不扩成全历史回归 | 通过：原场窄冒烟 | old-smoke-result16/16：两段真实行走、8角、缩放、2切控保角；旧场未保存。初次因闲置战斗全员倒下Current为空，使用原Reset并仅在未保存测试会话关闭敌攻击后重跑，未伪写HP/位置。 |
| V24 | 交接/发布 | 实际新场、操作说明、差异/未测、可复跑工具齐全；GitHub仅本任务自有内容，媒体及派生资源不外传 | 通过：精确白名单发布 | 产品e67d40bd408c7a6473565fce166f52d54b26689e的57文件已GitHub main精确核验；原图/原规则/派生manifest/私有配置未提交，原37dirty未夹带，全部租约释放。 |

## 已有底层合同，不能替代上述实际操作

已读取 `R/04-evidence/SurfaceContracts-green.json`：**22/22、failed=0**。覆盖逻辑 XY 半开范围/z限制、登记后代与同 Grid 归属、空配置拒绝、XZY＋旋转＋非均匀缩放、实时面积/包围盒与倾斜时 XZ 投影。其前置 RED 明确为生产类型缺失。

该回执不证明 Scene View 真拖刷、小样观感、整图实际面积、角色实战、GPU无闪烁或最终保存重开已通过。其他合同与编译、装配、真实输入回执由 root 按最终受测版本追加，不能累加为一次完整全绿。

## 本次只读保护复核

已对 `R/00-baseline/protected-hashes.json` 的15项和 `protected-prefabs.json` 的2项逐项重新计算 SHA256：**17/17 相同**。核对对象如下，完整初始 SHA 留在本机基准文件，不批量复制无关保护数据。

| 精确保护对象 | 数量 | 本次结果 |
|---|---:|---|
| AGENTS.md、OSSASTRA_RULES.md | 2 | SHA一致 |
| ProjectSettings/GraphicsSettings.asset、QualitySettings.asset | 2 | SHA一致 |
| SC_DontStarveMapLab.unity、SC_DontStarvePartyCombatLab.unity | 2 | SHA一致 |
| SC_CommerceIntegration.unity、SC_FreyciaPreview.unity | 2 | SHA一致 |
| SC_CharacterVerticalSlice.unity、SC_VC02B_IntegrationReady.unity（原dirty） | 2 | SHA一致 |
| MapResearchRoot.cs、MapResearchSpineDepth.cs | 2 | SHA一致 |
| CharacterEquipmentView.cs、CharacterDebugControls.cs（原dirty） | 2 | SHA一致 |
| VC02B/PlayerInputLayout.asset（原dirty） | 1 | SHA一致 |
| PlayerCharacter_BlueFiveFace.prefab、PlayerCharacter.prefab | 2 | SHA一致 |

原 `initial-dirty.txt` 的37条 Git 状态逐项核对为 **37/37 保持**：

- 31条旧 M01/M02A/M02B/M02C/ROADMAP01–03 场景、目录meta及少量材质的 `D`：保持原删除状态，未作为本轮清理或恢复对象。
- 5条生产/配置 `M`：上表两处原dirty场景、两份原dirty脚本和输入配置；这5份内容均在精确 SHA 保护中。
- 1条 `docs/agent-sync/TASK_REGISTRY.md` 的 `M`：属于起始状态；root本轮登记/释放租约可授权更新其内容，本表只确认原状态保持，**不声称登记表字节冻结**。

此检查不是整个工程、安装目录或人工区的全量快照。最终应再次核对上述保护对象、原状态及实际发布差异；不得一键还原用户 dirty，也不得将31条旧删除或5条原修改夹入本轮提交。

## Builder 的许可与实际验证边界

自有 Builder 仅对 `SC_FrontierGroundLab.unity` 首次建场，拒绝已有候选、dirty源现场或 Prefab Stage；以已保存双角色场 `SaveScene(source,newPath,true)` 复制，验证源和副本的角色/敌人/桥/40物件关系后，只移除已确认的“固定世界根_手工研究样地”。原角色根、会话、深度、输入与相机链不重组。

示范与笔刷共同写原生 Tilemap，没有持久第二份格子数组。首次生成、`RebuildDisplay`、`ResetDemonstration`、`ExpandToFull` 是不同入口；刷新不能代替人工笔画，显式重置才替换作者数据。碰撞为新候选连续地面与四边墙，不从原图颜色推导玩法碰撞。

这些为最终源码的静态边界说明；首次拒覆盖、Undo恢复、场景保存与原场未受影响仍按 V02、V12–V14、V23 实际验证。

## GitHub 与 LocalOnly 边界

允许定向发布本轮自有 C#、Shader、候选测试、自有场景壳及说明；发布前必须逐文件核对，不能因文件出现在工作树就全量提交。GitHub 远端为 OSSASTRA `main`，完成必要验证后按既有 R11 发布并核验远端 SHA；禁止同步 Gitee或强推。

以下保持本机：原游戏文件、截图、原图/Atlas/切片、原规则与布局的可逆派生数据、原 Shader 编译产物、恢复程序集、完整导出工程，以及本轮 LocalOnly 材质/Tile配置/manifest及受限证据。对应目录包括 `Assets/OSSASTRA/Art/FrontierTaleReferenceLocal/`、`Assets/OSSASTRA/GroundResearch/LocalGenerated/` 与 R 下来源/转换内容；不是公开包的可独立运行依赖。

干净克隆缺少本机依赖时应给明确提示并保留当前现场，不能静默生成替代媒体；普通重新Import不承诺恢复既有GUID。公开场景壳存在，不等于仅克隆公开仓库即可复现本机原作素材表现。

## 尚待用户确认

root技术验证完成后，用户仍需先看新场正常游玩与比例，再看真实组成/层开关，最后退出Play亲自拖刷、Undo/Redo、保存重开。本轮不代签主观观感，不要求复刻宣传图中的建筑、天气或最终游戏美术。


## 本轮最终回执补充

- 生产编译Edit/ready，无编译错误；范围22、笔刷16、观察面板29、导入15、Undo安全9、Scene13、缺默认Sprite1、PrefabStage3、生成边界5均对应实际回执。
- Scene View事件的50/61/34计数包含重复的无落笔校准，不相加冒称145个独立功能。正常画笔/Box的MouseUp收笔、Esc/失焦和跨Undo组保护均已按真实顺序回归。
- Windows界面服务两个入口启动均因kernel assets路径os3失败；已保留该能力边界。实际编辑验证由原生Palette＋临时SceneView＋公开SendEvent、NativePreview和Tile/Sprite回读完成，没有用直接Paint/SetTile代替被保存的实际笔画。
- 初次正交SceneGUI坐标和离屏焦点使夹具失败；最终以原生单格Preview校准选格，禁止落笔校准，失败原件保留。源Tilemap的alpha/材质结构不受该夹具问题影响。
- 两轮Play使用额外非Demo作者哨兵，避免“自动重置成同一个示范也会过测”的漏洞：六次hash为F63FBC1E6BF8C86EB29EC7C176E7D49DF0FE3506AAB36277F9C569AF910FD60C。清理哨兵后484格的最终示范hash为DADEA9B88402F1D441A264349456278681BBA1D041653CAEE60E19CB73594E12，MissingScript=0，材质7。
- 最终原生Sprite顶点并集/Collider/作者范围均为3072㎡；新场Edit clean，chunk32，敌攻击默认false，原场/Prefab/深度和全局管线受保护。
