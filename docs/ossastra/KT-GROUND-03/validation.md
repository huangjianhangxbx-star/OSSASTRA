# KT-GROUND-03 验证记录

本轮已经保存独立高台Lab，完成真实作者操作、坡道登高、双角色切控、运行观察和一次真正关场重开。本文按实际Native回执记录；**本轮技术验证与最终正常现场已收口；发布SHA和租约释放由root补录**。严格像素仍11/12及物理UI未验的边界不改成全通过。原KT02结果不移作KT03通过数。

## 环境与现场

`native-context.json`：Project_ANKALUTE、Tuanjie 2022.3.62t16、URP 14.2.0-t1、D3D11、RTX 4080 SUPER、i7-14700KF。开始时Edit、非编译、无PrefabStage，旧工坊场clean；原镜头35°、distance25、FOV35、yaw45。root持有唯一Unity writer。

`plan-integrity.json`逐项通过9个计划文件的长度/SHA；正文与两张用户参考图已读。`initial-status.txt`记录原dirty现场，原37项由root保留。`protected-after-old-smoke.json`实际20/20基准指纹相同；这是本轮该快照的20项，不写成全部工作区无变化。旧场冒烟另读19个场景/依赖文件，字节均未变。

新场为 `Assets/OSSASTRA/GroundWorkshop/Samples/KT-GROUND-03/SC_CliffEdgeLab.unity`。参考媒体、含参考Sprite形状/UV的派生Mesh及其场景依赖仅本机；公开代码、自有模型与说明另分界，不只按PNG后缀筛选。

## V01—V26

“有证据”仅指所列范围，不等于实体鼠标、全历史系统、任意模型或正式Player均通过。

| 编号 | 必须取得的结果 | 本轮证据与当前边界 |
|---|---|---|
| V01 | HEAD/dirty/Unity现场、保护快照与单writer | root同步记录；native-context、initial-status及保护20/20。最终租约释放待root。 |
| V02 | 独立新Lab，旧场/基础Prefab保持 | M1新场保存clean；旧工坊12/12，19个场景/依赖字节相同，保护20/20。 |
| V03 | 原草/浅石路/覆盖/花草真实复用 | M1对象与真实Game View、运行观察及视频可见；未用计划截图冒充运行画面。人工审美未签。 |
| V04 | 独立侧壁/薄盖，无重复共面顶盖 | 实际A/B独立墙、裁剪盖与Collider；Bare/Structure/Exploded/BeforeClip可分辨。normal-final最终现场passed=true，双平台每平台cache各1且全部持久Mesh/材质/cap有效。 |
| V05 | 从已保存Mesh读回开放上沿，保留凹口 | 模型读回修版15/15；M1 A凹6点、B斜5点。不是同一硬编码多边形同时生成墙与域。 |
| V06 | 非法环拒绝并保留正确状态 | 纯几何26/26含孔/多环、自交、非共面、重复面、缺边及负镜像/倾斜等；作者冷门8/8拒绝未登记写入。 |
| V07 | 凹L/斜切/非整格/局部裁剪 | 纯几何26/26、真实Native缓存构建12项、模型8/8与刷隔离11/11；真实样本近景保留。 |
| V08 | 连续裸缝误差与35°/低角度 | actual-seam-green真实3/3：原墙11边，每边5点共55样本（A30/B25），到实际cache/cap的XZ/Y最大误差均0；容差1e-4/5e-4，缓存全部顶点独立检验在原墙凹域内。35°/12°运行图保留；有限采样不扩大成任意模型数学保证。 |
| V09 | 基层连续封口；域外/凹口无悬空显示或高台碰撞 | 真实构建、凹口Physics、BeforeClip对照及上下坡有支撑；V08实际墙边55采样也通过。 |
| V10 | Native/缓存UV、矩阵、颜色、alpha与周期一致 | 严格12案**11/12，总false保留**；自定义orientation35°有1个RGB像素差。纯同内容性能A场另有486三角、全帧像素0。 |
| V11 | Tilemap唯一作者、缓存可再生 | Native刷改36/36、配方闭环40/40和重开52/52；重建替换缓存对象，作者数据仍来自Tilemap。 |
| V12 | 真Scene View拖刷/擦/吸管/UndoRedo/锁不串层 | Native SendEvent手势36/36；API隔离11/11。本次未借用既有刷分支，实体鼠标未验。 |
| V13 | Preview/Cancel/Commit/Undo及显隐同步 | Road实际配方闭环40/40，Existing/Future临时层、Cancel、Commit和Undo自动同步。显示联动修版34/34：Hide/Show的SceneVisibility、mask材质属性、Release恢复以及非A保持；不改Renderer enabled/GameView。首轮Hide与第二轮重载错误历史保留。 |
| V14 | 改真实保存模型/重取，保留手绘 | 保存版本0→1→0、Native Undo/Redo8/8；直接API而非实体面板点击；Inspector MeshFilter引用变更错误门修版9/9：自动提示过期、不静默重取、不破坏旧缓存，Undo恢复后自动清错。 |
| V15 | 缩域保留旧格，失效装饰显式处理 | 模型8项保留域外格、扩大恢复及装饰；装饰域10/10，诊断只读与显式选择2/2。不静默删除/重排。 |
| V16 | 同XZ多surfaceId不串写 | 三Map独立登记，API隔离11、手势36及配方40均保持非A作者数据；高度Physics分离。 |
| V17 | 整Lab保存，真关场重开，引用稳定 | 最终c11f轮52/52：两次Save后一次真正Single重开、即时持久引用有效、文件/GUID/localID稳定不增长。先前398c轮仍保留，不把单轮52项叫两次重开，也不凭条数推导额外的“Undo后保存”专案。新增Mask观察后保存顺序修版8/8：Save前退出同Lab原Window观察，持久运行材质诊断关闭，原作者保持。 |
| V18 | Collider/侧壁/缓坡连续、凹口不误填 | 刷隔离真实Physics、原角色上下坡19/19、真实Game View/视频；独立原墙边55采样误差也为0，按限定容差记录。 |
| V19 | 原Root唯一Move真实上下坡 | 高度修版19/19，Y约0.02→3.02再下降，另一角色原控制器可行走；noActorTransformWrites=true。 |
| V20 | 35°跟高、yaw/zoom与切控保持 | 高度19/19读原root真实zoom，八档45°目标、切控与高度跟随；不以瞬时camera-to-actor距离代zoom。 |
| V21 | 所属平台脚点、退让/间距/域与保护 | 装饰域10/10、只读诊断2/2；真实Place/Scatter、模型缩域、域外拒绝/保留、Undo恢复。 |
| V22 | 八方向/连续旋转及原Spine深度 | 高度19项、八档图、40秒实录、运行观察图；静止/旋转44/44缓存与实例稳定。未签全场长期无闪烁。 |
| V23 | 同内容A、真实场B、0重建/数量稳定 | A同内容像素0、4轮；B真实场四模式3轮视觉消融、Full恢复像素0；静止/旋转44/44，运行高度故事0重建。Editor wall-clock非GPU/Player/FPS。 |
| V24 | 诊断不改真实世界，恢复与运行继续 | 冷门Gate5/5、真实组件归属18/18，真实Play观察PASS；物理/姿态/缓存保持，游戏时间前进4.347s。normal-final最终Edit clean、Final/诊断0、敌攻击关、35°/distance25/FOV35、无MissingScript或测试设备；双平台正常持久引用。 |
| V25 | 旧工坊最小刷改/撤销及原装配回归 | 旧工坊12/12：真实API刷/擦与Native Undo、双角色/Controller、单敌、35°原MapResearch装配读回；未Save/Play旧场。Undo后内存dirty=true如实保留，文件19项不变。 |
| V26 | 入口、资源分界、完整交接与发布SHA | 本机HTML与操作说明已整理；最终原地回归及normal-final已通过；仅公开精确清单/发布SHA和租约释放由root补，不预报远端成功。 |

## 关键实际回执

本机根为 `研究/开拓物语地表/KT-GROUND-03`；下列相对文件均是该根内原始回执，不把本文转述当原始测试。

| 回执 | 实测范围 |
|---|---|
| `01-evidence/actual-seam-green.json` | 3/3：11个真实墙边、55采样点，实际cache/cap的XZ/Y最大0，容差1e-4/5e-4。 |
| `01-evidence/cache-health-queue-fixed.json` / `update-scheduler-fixed.json` | 重载后27个chunk Mesh/Mat与双cap恢复，pending0；未手工Rebuild/重开。 |
| `01-evidence/display-sync-final-result.json` | 34/34实际显隐、Mask和Release；不禁用Renderer，不改变GameView。 |
| `01-evidence/mask-save-fixed-result.json` | 8/8，Save先退出原Window观察再持久化，诊断不写入运行材质。 |
| `01-evidence/mesh-reference-gate-fixed-result.json` | 按type取MeshReference结果9/9，Inspector引用变更错误门与Undo自动恢复。 |
| `01-evidence/cache-health-second-reload.json` | 再次重载后双平台27chunk Mesh/Mat、双cap可读且源指纹相同。 |
| `01-evidence/geometry-first.json` | 纯数组几何26/26，不变场景或资源。 |
| `01-evidence/reader-scene-fix.json` | 已保存自有Mesh读回15/15；首轮reader-first失败保留。 |
| `01-evidence/authoring-first.json` | 未登记写入冷门8/8。 |
| `01-evidence/cache-build-first.json` | Native实际Sprite三角、UV/变换/颜色/alpha构建12项，sourceUnchanged=true。 |
| `01-evidence/ui-gate-first.json` / `ui-owned-first.json` | 5/5与18/18真实组件合同，不是可见渲染认证。 |
| `01-evidence/gesture-fixed-result.json` | 36/36自动Scene View手势；borrowedBrush=false。 |
| `01-evidence/preview-loop-road-result.json` | 取最后COMPLETED结果40/40；前锁层拒绝记录保留。 |
| `01-evidence/model-variants-fixed.json` / `brush-isolation-fixed.json` | 8/8模型、11/11原刷API/Physics。 |
| `01-evidence/decor-domain-first.json` / `decor-diagnostics-green.json` | 10/10装饰域、2/2只读诊断。 |
| `01-evidence/height-final-result.json` | 26.8984秒19/19真实输入与角色高度，无角色Transform直写，physicalHardwareVerified=false。 |
| `01-evidence/runtime-observation-result.json` | 实际Play Final/Bare35/12°/Structure/Exploded恢复；时间继续，物理/缓存不改。 |
| `01-evidence/save-reopen-final-result.json` | 按type取SaveReopen，`c11f3d5434b24f7793e4e59f8b2b2dcb`最终轮52/52、reopened=true；先前398c轮另保留。 |
| `01-evidence/idle-rotation-result.json` | 静止/旋转44/44；有限时段cache ID、signature、重建数与资源数量稳定。 |
| `01-evidence/old-workshop-smoke.json` | 旧场12/12，19文件相同；原Root显式Ossastra命名空间、Unity CharacterController。 |
| `01-evidence/protected-after-old-smoke.json` | 20/20基准指纹不变，非25项。 |

## 最终原地回归与正常现场

重载队列与M3定向修复之后，未靠重开掩盖自动更新，重新执行：`gesture-final-result.json` 36/36；`preview-final-result.json`按唯一结果/最终runId `b86f0b1069d84f799fca586a6a1cb6c4`取40/40；`idle-rotation-final-result.json`按type取`99127b4abb5a410ea9b8a32eb4cd7dfb` 44/44；`save-reopen-final-result.json`按type取`c11f3d5434b24f7793e4e59f8b2b2dcb` 52/52且真正重开。回执数组含历史合同，不能取最后一个对象当目标类型。最终正常状态见`normal-final.json`。

全部定向修复后再次运行原角色高度故事：`height-after-all-fixes-result.json` PASS 19/19、26.8476秒、maxY=3.02，仍无角色Transform直写且physicalHardwareVerified=false。root最终保护检查20项指纹仍相同。

用户入口为 [使用说明](README.md) 与 [模型接合约定](model-contract.md)。它们说明支持范围和如何改模型；本记录不扩大成任意FBX自动接合。

## 画面与性能如何解释

`height-shot-first.json`是真实Play Game View，原1600×900输出1280×720；`02-visuals/HeightInput/yaw-0..7.png`为八目标档；`02-visuals/RuntimeObservation/01-bare35.png`至`04-exploded12.png`为真实运行观察；`02-visuals/高台上下坡与八向旋转_40秒.mp4`保留连续原生录像。M1低角图为15°，后运行侧看为12°，不混写角度。最终静态八图见`lab-final-visuals.json`及`02-visuals/LabFinal/`，groundOnly=true：静态相机改变时临时隐藏4个actorRenderer，避免未Tick角色的纸片朝向误投影，finally恢复全部；这些图只证明场地/接合/观察，角色表现仅用Runtime图和实录支撑。这些都不是Editor主窗口/实体鼠标截图，也不凭单帧签全场无闪烁。

严格像素对照 `cache-pixel-ab-color.json` 的12案中，`05_CustomOrientation_pitch35`在512×512全帧有1个RGB像素不同，maxRgb=107/255、alpha差0、全帧meanAbsoluteRgb=0.000249226888。root定向说明为world采样边界差；严格总false不改阈值伪装通过。其余11案原图保留。

性能A为纯自有PreviewScene，三层192/32/19格、同输入/Shader/MPB、未裁剪未flatten，Native/缓存均486三角，512×288像素差0。每路8次预热，4轮AB/BA交替，每路每轮12样本。Native Renderer3；缓存Renderer/Mesh10，共享材质每路3、输出1458顶点；四轮资源数量稳定。Camera.Render同步调用中位/p95：Native 0.434/0.5181ms，缓存0.4085/0.4616ms；构建另计1.7472ms。

性能B为真实高台场视觉消融，512×288、35°、HDR/MSAA/occlusion保留，3次预热、3轮每模式8样本，正反顺序交替。A盖13Renderer/294三角、墙24三角；B盖14Renderer/356三角、墙20三角。角色3、Collider9、低地、灯和相机保持。Full/NoCaps/NoWalls/NoCapsOrWalls的中位/p95分别0.4537/0.4947、0.4433/0.5264、0.4530/0.5816、0.4320/0.4503ms；Full恢复全帧像素差0，物理/姿态/缓存保持。删盖/删墙是视觉消融，不是同内容等价替代。

两者均为Editor主线程`Camera.Render`同步调用wall-clock，可能包含驱动/GPU等待；GPU、独立renderThread、Native内部Mesh、Player未测。微小时间差不据以宣称FPS、draw-call或纯地表收益；数个短轮数量稳定不是长期无泄漏证明。原始细表由 `performance-a-first.json` 与 `lab-ablation-first.json` 链接。

## 保留失败与未验范围

日志不声明全局零错误。`final-console-errors.json`保留2条Editor `GetTransformInfoExpectUpToDate / RendererUpdateManager.UpdateAll`错误，仅见GUIUtility堆栈。再次正常Play的原始读回以`play-console-raw.json`为准：1条Funplay MCP工具切模式任务取消错误（同任务key后续ready正常），9条均为原temporary-b的`JogSlowToIdle`动画映射未命中警告；所见没有高台脚本异常栈。MCP `get_console_logs`会过滤工具自身错误，过滤后的无error不能作为零error断言。原角色配置保持，不在本轮扩大修角色音画或工具插件。

- 手势首轮13PASS/1FAIL时Tile/Sprite已写，夹具读取Rebuild前的已毁Cache managed字段。修版从当前binding.cacheRoot读回：旧fake-null、ID改变、计数19→20，最终Undo后signature恢复。修的是测试引用，不改写为生产通知缺陷。
- 高度首轮17/18误把瞬时camera-to-actor距离当zoom。修版读原root实际zoom后19/19，首轮保留，不改写为生产缩放缺陷。
- 分段接缝前两轮各1PASS/2FAIL，因实际cap Mesh不可读；仅源墙SHA不变通过。修复重载引用与队列后actual-seam-green真实3/3，原墙11个开放边每边t=0/.25/.5/.75/1取样，A30+B25共55点，实际cache/cap的XZ/Y最大误差均0（1e-4/5e-4）。缓存全部顶点独立检查在原墙凹域内。这一轮cap/cache为当前重建显示对象（persistent=false），不把接缝回执本身当持久保存复验。
- 显示首轮33PASS/1FAIL只缺SceneVisibility Hide镜像。第二轮1PASS/1FAIL在DisplayHash/DebugMask遇NullReference；root进一步确认BeforeReload误删仍被Scene引用的Mesh/Mat，加上delayCall队列挂住，并非仅将其归咎于夹具。窄修保留重载引用并改为重载后有界一次性update队列；cache-health-queue-fixed双平台27个chunk Mesh/Mat及两个可读cap恢复，update-scheduler-fixed读回pending0、scheduled/draining/waiting=false，未手工Rebuild或重开场景掩盖。随后display-sync-final-result真实34/34，包含实际Hide/Show/Mask/Release与非A保持。第二次脚本重载后cache-health-second-reload再次27个chunk/双cap健康、源指纹相同；不是只让健康读回通过而放弃原显示合同。
- 新增真实MaskSave顺序RED5PASS/3FAIL：保存前没有释放原工坊Window观察，导致运行材质残留mask。root已窄补Save前释放同Lab该Window拥有的观察。Inspector MeshFilter引用错误门RED8PASS/1FAIL：实际引用变更但无stale-model错误；root已窄补Modified对精确MeshFilter引用的匹配。编译后mask-save-fixed-result真实8/8、mesh-reference-gate-fixed-result真实9/9通过；失败历史保留，不再将RED当当前结论。
- `normal-final.json`已真实passed=true：最终Edit clean、root确认单个新场、三独立Map、两个原角色/单敌（敌攻击false）、35°/distance25/FOV35、Final/diagnosticRenderer0、MissingScript0、temporaryTestDevices空；两平台各一个cache，全部Mesh/材质/cap持久有效、DebugMask0、源指纹匹配、失效装饰0。最终公开清单、远端提交与租约释放仍由root收口，不提前填SHA；本轮没有休眠。

首版只支持经校验的水平简单单外环、正TRS/绕Y旋转；复杂孔洞、非共面、负缩放、非流形等拒绝，不自动凸包填平。二维mask不称体积mask。完整伙伴跨层寻路、跨层战斗、任意FBX自动接合、动态平台和任意曲面刷地不在本轮承诺内。

实体鼠标/Editor主窗口实拍为 **UI_PHYSICAL_UNVERIFIED**。独立空工程导入、正式Player、GPU与人工审美未测。观察不暂停时间、不移动真实Collider/作者Root/人物，不增加第二个Move或每帧强设角色Y。原参考媒体/派生Mesh仅本机。本轮没有电源动作。
