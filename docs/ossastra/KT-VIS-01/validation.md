# KT-VIS-01 验证记录（交付验证）

本表只记录本轮实际证据。截至2026-10-31，正式角色定向修复、有限后端、Sprite14/材质26项、真实Play限定23项、保存重建关闭重开42项、三次Play自有资源释放、OFF/ON碰撞与原攻击Applied、最终四态Editor成本、预算/投影参数8项、四阶段实际录像、旧场完整冒烟、低角/正交/观察旁路、真实跨界恢复、既有SceneView相机隔离及原死亡链交接均已取得限定通过证据。最终Stop/保存/保护已完成，功能源码与文档已定向发布并精确核验远端SHA；其余未认证范围直接列为技术/视觉边界，不冒称全局通过。历史RED保留，不再当作当前未修状态；旧KT03/FIX01计数不代填。

当前结论先看下方回执索引与V01–28矩阵；后面的诊断过程用于追溯。批准要求见[requirements.md](requirements.md)。

## 关键回执索引

下列结果按合同分别记录，不相加凑总分；“通过”仍受该行范围限制。

| 合同 | 关键runId/原始入口 | 已取得结果 | 范围 |
|---|---|---|---|
| 纯状态/数学 | state-green-valid.json | 20/0 | 临时SO与纯逻辑 |
| 明确Collider探针 | probe-green.json | 15/0 | 私有形体、相机投影、多组/内部/无效输入 |
| M1正式墙切修版 | 0436566d71394e4497939e37d2f63d3b | 正式墙/full1698/lost0 | 特定合法站位，不取消合法cap遮挡 |
| 正式身体与3装备原色 | 84e68684d65f4383b7b426e9e62f02f1 | Body/Left/Right/Sprite及组合RGB/alpha0错 | 同帧原图与定向启停 |
| 七层owned深度 | aabfd4e5c67544b0a72172c59326e92e | 核心/羽化/外围/关闭均0错 | 私有冻结正式身体/技术七层 |
| 双组/PMA/并集/脚盘 | 439d4c3788de42e18f79bfdc5af33f98 | 6门0错、源强门true、helper释放 | 独立原图+CPU三角深度oracle |
| Simple Sprite严格合同 | 96d2f1aaa0984ac4bb122b246d005001 | 14项有效/0错、源相同、资源0 | 原生SpriteRenderer独立参考 |
| Sprite材质原地变更 | 123b8ce0900d4ac782795155aa7c83b0 | 26项有效/0错、源相同、资源0 | 保留14项，再测颜色/cutoff/纹理 |
| 独立查询物理 | proxy-scene-green.json | 6/0 | 私有PhysicsScene，无Simulate/主物理写入 |
| 真实Play有限闭环 | b64d18402ebf4382b3028322f4904ec8 | 23项通过，归还/lease有效 | 状态、原控制/镜头/支撑；非HP死亡、非像素审美 |
| 新场保存/重建/关闭重开 | edit-save-rebuild-reopen-trace.json | 42/0，closed/reopened=true，单场Edit clean | 本图双平台/持久引用/197源文件保护 |
| 三次Play/Stop与确切资源 | m5-cycle1/2/3-edit-released.json；m5-cycle3-owned-identities-stopped.json | 三Stop标记资源0；104实际owned引用live0/reused0 | 不推导全引擎无泄漏，global Material增长未归因 |
| 开/关同起点碰撞 | 35099ec2d2e649668a89e4722e04a5c0；f315a294d0f247e19007ce0a075e24a5 | 两态PASS、最终Z差约3.8e-6 | 虚拟InputSystem事件走原动作/Move/CC；非物理键盘 |
| 开/关原攻击Applied | 7a7c8e2726e64a18ba5a21a0a1c3bdb5；4702d02bf7fd4785af7e62c558570ead | CurrentA专属Applied各4 | 不比较含伙伴自然攻击的敌总HP差 |
| 最终四态Editor成本 | d1f4eeca2d3a4ee7b6704e570b61f0e3 | 路线修后各90样本，OFF Queries0/detectMs0 | Render Thread/GPU未取得，非Standalone FPS/0GC |
| 五组预算/2比例×2距离 | 08b390d83d504893b485c56a531f65ad | 8项通过，5active/4capture/1overflow，归还完成 | 实际RT/投影参数；非GPU圆形像素审美 |
| 路线修后真实Play复验 | 94582326838f457587db10b175f259b2 | 23项GREEN/归还true/leasefalse | 核心逻辑未改；伙伴合法起点改主控左2米 |
| 四阶段连续录像v2 | dad01ac9d56d48259bac186545e327fa | 40.59805s/601帧，四PNG与19/23/27/31s解码图已目检 | H264 silent，非逐帧无闪认证或人工硬件 |
| 旧场完整最小冒烟与返回 | 0992bc268f6a488a9d619a24bea65de2 | PLAY_SMOKE_GREEN→RETURNED_NEW_SCENE；新增Error0/historyReset=false | 约2秒只读模拟/动画推进；旧场未保存、500作者项及197文件保护；非旧场走路/战斗全测 |
| 低角/正交/原观察旁路 | b52c36e909724b208e42fd2b56a20beb | 4/4，20°透视/正交均真捕获，observer2帧/main0，归还true/leasefalse | 实际Camera End与状态/参数；非低角像素颜色或审美 |
| 离屏/回屏/模型在后与实际跨界 | 2c9a0651c798496b946ca2cf00fd3a79 | 9项GREEN，离屏10状态释放/捕获0、回屏开窗/前后排除；8次跨界false/true交替 | 实际Probe校准+Motor±.15m，检测确已消费每姿态；非GPU逐帧无残洞/无闪认证 |
| 既有SceneView相机隔离/两cache稳定 | 7131dff0c4e840a59b1e7c7e90735a88 | main1036/SV1对Begin-End，材质main6216/SV6/end6222全过，cache身份49→49 | 既有相机manualRender、自有128RT释放且原target/aspect/PV精确归还；不签标签页GUI |
| 原死亡链与自动交接 | 55245105ceeb4ee7845dd98a4b2ed7e0 | 8/8，原ApplyDevelopmentHit产生HP100→0/Died各1/Current自动B，死者释放/伙伴延续 | 原开发受击入口，非敌方挥击/Guard/LOS/复活测试，不回填HP |

## V01—V28

状态使用通过、部分通过、失败、未执行、不适用。“部分通过”表示已有实际Native覆盖，并明确列出尚未认证的技术或视觉子域；不能把它计为整项全域通过，也不把已运行的检查继续标成未执行。最终发布门单独列明待收口，不把扩展域限制藏进“全部通过”。受限阶段证据见上节，不累加重复检查凑总分。

| 编号 | 必须取得的本轮结果 | 状态 | 证据/边界 |
|---|---|---|---|
| V01 | HEAD/版本/dirty/规范/授权及旧图与未提交现场保护 | 通过 | 197/197源文件最终SHA相同；原用户装备脚本去除getter后精确等于快照，发布只取getter；原开发细则blob未变；远端基线3b3c403已核对 |
| V02 | 独立新场复用原环境/双角色/镜头，新注册及可变资源归属隔离 | 通过 | 独立注册3 Map/新可变资源、原双角色与镜头；trace42/0实际保存关闭重开、持久引用合法，197源文件未变 |
| V03 | 两类反馈分别复现并分类，正式网格/支撑/实际draw支持结论 | 通过 | 受控A显示面入墙、B合法前景cap；不反推截图原帧坐标，不等于修复通过 |
| V04 | 透视关闭、无阻挡合法站位不异常切脚/身体，实体墙仍阻挡 | 部分通过 | A正式修版1698/lost0、同起点OFF/ON实体墙均阻挡，B合法cap遮挡保留；B floor/walls各1 lost记录，未穷举全场连续站位 |
| V05 | 不改角色alpha/颜色、不置顶，透明核心完整身体清晰 | 部分通过 | Body/3装备RGB及alpha0错，PMA1521/0、v2正常动态身体清晰；未知长装备/全方向像素未代签 |
| V06 | 仅登记实际遮挡模型出现平滑柔边圆窗，外围正常 | 通过 | 技术像素门0错与v2同机位实际柔边/外围目检相互支持；限当前已知材质/样板 |
| V07 | 合法深入后可见扩大，静止不无限增长 | 通过 | v2半径1.3→2.2→2.5且轻挡/扩大可见；live持稳有界，未以时间无限增长 |
| V08 | 明确有限组可整件opacity0，地图/无关对象保持 | 通过 | v2阶段2小墙WholeFade1/实际消失，原地图与平台保持；不泛化整个世界可淡出 |
| V09 | 退出、出画再入画、换目标后平滑恢复，无残洞 | 部分通过 | 实际离屏10状态全部释放/捕获0、回屏重新开窗，ResetRoute自然释放/切控history保持；未签GPU全图无残洞 |
| V10 | 核心无旧深度挡角色，启用颜色/深度相关路径一致 | 部分通过 | 正式已知owned深度/多层/PMA/Sprite路径0错；限当前Forward配置，未启用的DepthNormals/SSAO/DepthPriming等组合不据此认证 |
| V11 | 前后双组实际遮挡均处理，不只最近命中 | 通过 | probe多组、交叉双组31480/0，实际Root五组全部Eligible；受限4capture/1overflow有诊断，不声称全5透视。NonAlloc缓冲子条不适用 |
| V12 | 同组墙/顶盖/封口/背面/附件完整处理 | 部分通过 | 厚墙/七层/Sprite技术门及实际24/25成员复合高台画面已覆盖；当前已知组有限通过，不泛化未知部件/材质或所有动态遮挡链 |
| V13 | 当前多层顶盖羽化不叠灰膜，核心真露身体 | 部分通过 | 技术七层四门及Sprite14/26均0错；当前正式高台窗口实际可见双角色头躯脚/花草，未作全场逐帧GPU颜色穷举 |
| V14 | 角色在前或模型在后不误淡出 | 通过 | probe目标截止/背后排除、静态opaque2024/0，实际双角色移到小墙前后Root需求释放/捕获0均有证；限本轮明示站位与材质 |
| V15 | 顶部/坡脚/低地与主控支撑保护正确 | 部分通过 | live主控整cap排除、伙伴footdisk及当前高台/低地画面有证；未取得已确认坡脚独立站位及其像素合同 |
| V16 | 双圆并集、切人优先级、死亡/失活释放 | 通过 | max像素、live切控history/GO撤权及真实开发伤害死亡8/8均过；原Died自动交接/死者释放/伙伴状态保持，不签敌挥击/复活 |
| V17 | 35°、八方位/连续过渡、原旋转与zoom/切控保持 | 部分通过 | 八次原45收敛/切控、20/28距离、实际20°透视与正交主链捕获均过并归还；未签全方向连续像素颜色 |
| V18 | 至少两宽高比/两zoom，圆与实际身体尺度正确，UI不透视 | 部分通过 | 480×360/640×360×20/28实际RT、BodyPixels与uniform圆XY像素相等；GPU圆形mask/UI最终画面未代签 |
| V19 | 无检测自反馈、静止/边界微移稳定、活跃组预算溢出明确 | 部分通过 | live持稳、真5/4/1预算及2c9…八次±.15m真实跨界有限状态/自然释放均过；未签微移GPU逐帧无闪，NonAlloc缓冲子条不适用 |
| V20 | 不移世界/Collider；透明前后碰撞和既有攻击结算一致 | 通过 | 同起点两态实际Input/Move/CC均挡墙；CurrentA原Applied各4。仅本轮现有动作路径，不新增墙挡所有攻击规则 |
| V21 | 材质隔离、重复启停及三次Play自有资源不持续增长 | 通过 | 三Stop资源归零，104确切owned引用跨Stop无live/复用；global Material增长未归因，不签全引擎无泄漏 |
| V22 | Gameplay/SceneView/观察相机隔离，同帧投影无一帧落后 | 通过 | main/aux及正式同帧Mesh回调、原observer2帧旁路均过；既有SceneView相机实际Render1对scope/六材质0、main1→End0、52源flags/代理归还，未签标签页GUI或任意Feature |
| V23 | 新场实际保存关闭重开可用，运行透明状态不入磁盘材质 | 通过 | trace42/0，真实关闭/重开，作者/材质/引用保持，单场Edit clean；三Play后的最终交还已再次核对磁盘和资源 |
| V24 | 显式顶盖重建后组引用/材质仍合法，不逐帧重建 | 通过 | 双平台显式Rebuild/保存重开引用及持久化有效；f47c四秒真实Play两cache身份保持、rebuild49→49，限本区间，非所有场/所有操作永不重建 |
| V25 | 同内容四态性能/资源/分配对照与明确未测范围 | 通过 | 最终四态各90样本、OFF0查询/0检测，成本与Editor分配已记；Render Thread/GPU不可用，不称0GC或Standalone FPS |
| V26 | 受影响旧场最小冒烟，无新模块者保持旧行为 | 通过 | 第三完整0992…真实Play模拟/动画推进、newModules0、作者500项/197文件相同；新增Error0/historyReset=false，旧场未保存且已回新场clean。前两轮工具取消/历史重置留档，不签全局Console永久0错误 |
| V27 | 正常游戏连续录像及四阶段同机位实际画面 | 通过 | v2连续40.598s/601帧、四Native PNG及四解码时点目检；保留压缩块状/静音/非人工硬件/非逐帧无闪边界 |
| V28 | 保存新场入口、接入/根因/限制、实际提交与媒体边界 | 通过 | 74文件白名单定向发布，功能提交04aac0039a83a80bfbdc357b10749a46240bdae8，fresh远端同SHA；原37dirty与受限媒体不入提交 |

## M0已知事实（不代签整项验收）

root的2026-10-30 21:07现场记录：当前基线`3b3c403`，保留FIX01；Edit ready，源KT03 Lab dirty=false（用户已保存），C约7.28GB/E约30.46GB。不沿用上轮dirty=true/C0旧值，也不归因于本轮保存或清dirty。适用规范/现场/资产归属与完整保护仍以root本轮原始回执为准。

用户网页未能读取最新正文，浏览器初始化failed；不能声称已读最新网页。ZIP内7份Markdown已完整内存读取，足够作为已批准执行规格。该资料读取不证明任何运行效果。

## 当前阶段Native证据（不代签整项）

本机证据根为`研究/可视遮挡/KT-VIS-01/01-evidence`。

- `baseline-native.json`：Tuanjie2022.3.62t16、URP14.2.0-t1、Forward，DepthPriming Disabled、copyDepth AfterTransparents、intermediate Always，当前Renderer features空；Asset MSAA4、depth/opaque texture支持打开。Gameplay相机1600×900、HDR/MSAA允许、near.05/far180、FOV35、无postprocessing或相机栈。这是实际配置读回，不代表新效果已兼容所有路径。
- `rehome-new-scene.json`：新场路径为`SC_OcclusionVisibilityLab.unity`，三个新Map独立身份及可变材质/登记；作者结构hash保持，197个源文件核对相同。该回执本身是dirty=true的中间登记阶段，不冒称已经保存。随后`scene-access-current.json`真实SaveLab合同9通过/0失败，`scene-access-availability-green.json`只读入口3/0单列，不相加凑总成绩。
- M2 baseline：run`1d4a4106ea4c4209b988a4652ccca2d0`，冻结一帧实际正式Spine身体/原Shader/材质及MPB、完整TRS，私有Unlit厚墙Preview。可见身体2574像素；core1812/错1812，outside38140/错0，feather4132/错4124，真实RED且样本有效。
- M2首轮candidate：run`ef99069a86e345948909a4634f5adf11`，core1590/错0、outside36876/错0、feather4364/错3978、off49746/错0。羽化仍失败，未用核心通过掩盖。对圆心Y额外翻转的候选问题修正后，下一条是重新Native渲染结果，不把图像拟合本身当Green。
- M2修版candidate：`m2-candidate-fixed-final.json`按runId取`3f214de4cd354edfa9c81e3feaa092e9`，GREEN、visibleBody2407，core1579/错0、outside36876/错0、feather4364/错0、off49737/错0。独立NoWall身体与原墙oracle、声明的linear-space羽化判据不放宽；sourceDirty前后false，无alpha修改/动画重播/Save/TimeScale写入。采样色RT为R16G16B16A16_SFloat、depth为D32_SFloat_S8_UInt，不据格式名称代签正式HDR/MSAA。
- 上述M2只签实际身体冻结帧＋私有Unlit厚墙的局部核心/羽化/外围/关闭恢复，不签正式Lit、多层顶盖、DepthNormals、多相机、动态扩大、整组淡出或正常Play最终效果；本小样阴影None为该小样不适用，不扩大为所有正式场阴影路径不适用。回执数组含历史结果，不能取第一个RED当最新，也不能只取GREEN丢历史。
- `state-red-valid.json`真实执行反射合同RED，缺5类型，0通过/1失败；更早`state-red.json`是入口RUN_METHOD_NOT_FOUND，不是行为RED。随后`state-green-valid.json`实际生产纯状态/数学合同20/0；构造器反射曾误选Unity Object隐式bool重载，修的是候选调用参数，不放宽状态判据。`probe-red.json`真实反射RED，缺Probe/Result，0/1；`probe-green.json`实际生产探针合同15/0。两者只签纯逻辑或私有形体检测，不签最终视觉，不相加当整体分数。

## M1：两类现象的受控归因

### A. 合法胶囊位置，但倾斜显示面伸入墙

`m1-wall-stage-locked.json`经既有Motor建立诊断合法起点，Root=(12,.02,12.62)、overlaps=0；相机通过原Update到yaw180°。`m1-wall-locked-final.json`内JSON的runId=`72ebd576b13842eb9498cc8050176751`，正式Mesh callbackFrame=renderFrame，callbacks1/staleSkips0；不是上一帧网格或未引用缓冲容量：487容量中250引用顶点，引用顶点alpha均255，实际不透明身体另由body-only像素测量。

胶囊bounds minZ12.4在墙maxZ12.28外；正式身体引用顶点minZ约11.3967、renderer bounds minZ约11.3926，说明35°倾斜billboard显示范围伸入墙。实际分组draw归因：

| 渲染对照 | 有效身体样本 | lost | 结论范围 |
|---|---:|---:|---|
| 身体重复负控 | 1,545 | 0 | 排除相同姿态重复渲染自身丢失 |
| 正控：诊断forceOff身体 | 1,545 | 1,545 | 证明检测敏感；不是产品修法/正常画面 |
| floor | 1,727 | 0 | 此站位不是脚下地面切身 |
| 加walls | 1,721 | 1,135 | 已知不透明身体被墙draw截掉 |
| 再加caps/decor/full | 1,721 | 1,135 | 不新增丢失，首个责任组仍为墙 |

分类为**物理起点合法、显示面与墙相交引起的异常上身切割**。以上是修复前受控原因证据；后续正式修版的限定结果另列，不能用开启透视替代切身修复验收。

### B. 身体与高台不相交，但前景顶盖合法遮挡

`m1-foreground-stage.json`Root=(3,.02,.5)、overlaps0、yaw270°。`m1-foreground-final.json`内runId=`68d6a0df06ed4f11afc09c259504b7df`，正式身体maxX约3.0355（renderer约3.0374）小于平台minX4.72，身体没有进入高台；支撑仍为低地Y0。正式callback与实际同帧渲染、callbacks1/staleSkips0。

floor2442有效样本/lost0，加入walls仍2442/lost0；加入caps后2442样本/lost1950，身体下部369有效样本全部lost。重复负控lost0，正控2337被移除，判据有有效样本。这一类为**合法前景顶盖遮挡**，交给可读性功能，不应靠取消深度/抬人“修掉”。

两种复现支持两种现象分类，不反推出用户截图的精确原帧坐标。M1深度修法必须减少A的异常切身，同时在透视关闭时保留B的合法前景关系；不能用Always同时消掉两者。

### 正式修版的受限原生结果

`m1-production-wall-final.json`run`0436566d71394e4497939e37d2f63d3b`：实际正式身体同帧回调及Gameplay Camera隔离对照，floor1703/lost0，walls、caps、decor、full各1698/lost0；原异常上身墙切割在该合法站位消失。`m1-production-foreground-final.json`run`ee3e4124c4e041cf92ccad9ced82545d`仍保留合法cap遮挡：1974有效样本中加入cap后lost1446，下部233全部被遮挡；floor/walls各有1个lost，不能写成此站位所有阶段零差异。这不是全场连续站位或实体墙运动回归的完整签收。

`normal-fixed-b-colour-final.json`run`84e68684d65f4383b7b426e9e62f02f1`，实际角色`temporary-b`的正式身体、左Spine、右Spine、实际左Sprite装备分别有1151/849/852/102个不透明内部样本，RGB mismatch与全帧alpha mismatch均0。含Sprite原组合1493/错0，不含Sprite诊断子案1389/错0；模块Disable/Reenable mismatch均0，variant数4归还4。采用已验证单位法线原值，未加bias或放宽阈值。此处为同姿态诊断Suspend/Prepare与显式启停，不等于正常Play全调度/三次生命周期完成；未覆盖未知自定义数组uniform。

## M2：从Unlit小样扩到真实Lit与七层技术组

`m2-lit-wall-final.json`run`34c6bf3cfb24475fa0a565eb2e147d89`实际GREEN：冻结原身体，已有URP/Lit原材质用于自有厚盒；core2166/outside38861/feather3992/off49604，错误全0。回调mainBegins2/mainEnds2、auxiliaryCallbacks0、auxiliaryRenders2，不把辅助渲染递归进主回调。color为R16G16B16A16_SFloat；外部raw depth拷贝是R32_SFloat，不把它误称D32硬件深度附件。两盏实际灯均ShadowNone；此私有HDR/MSAA1路径不签SceneView/全局相机/DepthNormals。

`m2-lit-seven-final.json`按runId取`0adf31d7d3b445f48f4eea7af23b487f`，**历史RED保留**：采用实际ThinTop Shader、7个自有不同色/遮罩共面技术层；可见层样本[5233,11446,11824,11908,11824,11446,5233]，每层均非空。core1500/错0、outside61099/错0、feather4416/错0，但关闭效果off74934样本/2480不匹配，该轮没有签M2闭环。

`m2-seven-ownedpass-valid-final.json`run`aabfd4e5c67544b0a72172c59326e92e`随后实际GREEN：owned原始深度pass，core1736/outside62936/feather4108/off74934，错误均0；上述7层各自仍有至少5233个可见样本，callbackContract=true。原ThinTop保持；失败的global CameraDepth路径及仅翻Y/仅加DepthOnly对照不能被写成已证首因修法。该结果仍是冻结正式身体、私有HDR/MSAA1路径。

这些M2结果本身仅是私有辅助相机/冻结正式身体技术样例，不能独自代替动态/双目标/支撑/生命周期；后续已取得的真实Play与录像证据另见当前索引及M5各节。

## 生产有限后端：双组、PMA前景与源场保护

`backend-visual-final.json`run`a3c9cd0ee347400d87ad54684a7f5178`的逐行像素有效，但整体为EXTERNAL_SOURCE_CHANGED；dirty前后均true，首轮缺逐项来源diff，原因仍未确定，不能事后指定为缓存或装饰问题。

增加只读来源仪器、保持原严格门后，run`b3420f90c590461fba7d61dd2e86dad0`实际sourceUnchanged=true：原场/active身份、dirty、时间倍率、完整层级矩阵/组件指纹全相同，17根/225节点，新增/移除/变化均0。六行中Zero31480/错0、交叉双组31480/错0、Dual31480/错0（7636点可区分max和相乘）、Support31480/错0（1703点不透明脚盘核心）、前方opaque2024/错0；前景PMA装备1521/错1521，因此整体正确为RED。

同一合同改用官方`Spine/Skeleton`、真实非白顶点RGBA/whiteTexture，保留原queue3000/PMA/ZWriteOff与独立无码gear参考。CoverageEnabled=false的run`51457fb62b2741bbbb7181cf9960217b`装备仍1521/错1521；该轮另有source stamp变化，raw准确列出57个装饰Visual矩阵在Reference渲染后改变，未新增/移除节点。root定位原装饰orientation在viewCamera为空时接受foreign Game Camera，在新Lab三份orientation中明确绑定真实Gameplay Camera，未改旧模块代码。此证据不能反推首轮相同原因。

`backend-coverage-enabled-final.json`run`439d4c3788de42e18f79bfdc5af33f98`才为完整限定合同GREEN：正式`CharacterOpaqueCoverage`先于有限后端Begin，保持源色材质/顶点不变；上述六项全部0错，PMA装备1521/错0。源场强门true，helper无错误，Dispose后确切Renderer及1份Material全部释放；normal=(0,0,1)，普通Spine保留原深度。标准为384×256、linear R16G16B16A16_SFloat/D32_SFloat_S8_UInt、正交单机位、MSAA1、固定容差4/1024、一般内部样本至少128/gear至少64。独立各组原opaque色图加CPU原三角逐像素深度排序作期望，不使用后端RT自比较。未把它扩大为正式HDR/MSAA全组合、动态Spine、SceneView或全局渲染路径通过。

## Sprite与查询代理：严格14项及材质变更26项已通过

`proxy-scene-green.json`原生独立PhysicsScene隔离合同6/0：显式Sprite AABB Box、原世界Collider隔离、bounds同步/登记上限、归还等限定门；没有`PhysicsScene.Simulate`或原物理设置写入，不代签真实角色攻击/运动。

Sprite首版run`1053d50eb6254c2691cc21c46f7f1eff`为历史RED。`sprite-white-final.json`run`e6a56326322548048e13cff58a740d7f`也为历史RED：初始非白tint、clearcore、feather、外围、flipX/Y、动态tint、SpriteID及双flip共9项错误0；MPB后NativeMPB18374错、UpdatedFeather5796错、UpdatedOutside7070错、ExplicitMainTexOnly9133错；更新后的clearcore3228/错0。sourceUnchanged=true，曾据此阻止签完整Sprite。

`SpriteGroup_96d2f1aaa0984ac4bb122b246d005001/result.json`随后严格14项实际GREEN：所有有效样本wrong=0，原Native SpriteRenderer独立参考、非对称alpha纹理/非白色、flip/tint/SpriteID/原生MPB/显式MainTex及核心/羽化/外围均保留原判据。updated feather5796/错0、outside7070/错0，sourceUnchanged=true，Dispose自有RT/Material/Mesh均0。不能把首版失败继续写成当前状态。

进一步原地修改同一个Material对象的颜色、cutoff和纹理时，`SpriteMaterialMutation_f890b42a20c0408c911b5b9f9dae6644/result.json`实际RED，原14项仍保持，新增颜色/裁剪/纹理与羽化/外围出现差异。生产同步修版（root登记文件指纹前缀`9008A74D`，不是Git发布SHA）后，`SpriteMaterialMutation_123b8ce0900d4ac782795155aa7c83b0/result.json`实际26项全部valid/wrong=0：原14项不删，再加三种原地变更各4门。sourceUnchanged=true，originalFourteenOraclePreserved=true，Dispose后RT/Material/Mesh均0。当前可签已知Simple Sprite/已知cutout材质及上述更新路径；不能扩成任意Shader/DrawMode、未知数组MPB或最终Gameplay全镜头像素均已测。

真实Play的`LiveValidationDriver`首次run`4cee7c1ebc96483a86595583d44d07f9`遇到候选工具自身JSON序列化错误：`checks[0].detail.runtime.disk.CenterViewport`直接返回Unity Vector2，触发self reference，文件停留Stage1，不能视为完整动态验收。root只读核实旧Runner finished=true、ownLease=false、stage11/finished，capture owners为空、TimeScale=1，世界已归还出生点。候选将向量改为primitive数组，其他输出逐项检查，并提供只读旧Runner/lease轮询；随后才取得下列重新执行结果。此工具错误不伪装成生产视觉失败，也不把清理成功冒充M5通过。

### 真实Play限定闭环已执行

`LiveValidation_b64d18402ebf4382b3028322f4904ec8/result.json`实际GREEN/error=null，23项检查均true，约7.3088秒，记录1374次main End回调；不能把回调数除时间当FPS。按公开Routes Stage2产生真实遮挡至小墙WholeFade1，固定需求持稳并核半径/状态/同姿资源集合不增长；公开ResetRoute后小墙状态自然释放。八次原`RequestRotationStep(1)`各45度实际收敛；原`SwitchNext`切控后每角色的状态实例按身份保留。

主控站三米A/伙伴低地时，实际Root登记排除承载cap、whole=0；反向伙伴站A时保留脚盘，cap仍可供主控前景处理。测试原GO失活撤权、公开切到存活角色、恢复原GO并保持单一合法实控，没有写HP，不代签真实死亡事件。最后原Actor矩阵与Current即时归还、两平台TRS未改、状态实例10→10、公开镜头配置/请求归还、TimeScale1、ownInputLease=false、sceneValuesRestored=true。

这些是实际玩法链与状态/所有权证据，未使用假Demand或手工Tick；没有GPU颜色oracle，不单独代签最终身体清晰度/审美。后续预算、生命周期、输入与成本已有下列实证；各合同计数不相加为整轮验收。为使四阶段示意不被伙伴更深遮挡提前触发WholeFade1，root仅将调试Routes伙伴起点改为主控左侧2米，核心遮挡/所有权代码未改；新路线run`94582326838f457587db10b175f259b2`随后实际23项GREEN/error=null、sceneValuesRestored=true/leasefalse，旧结果保留原版本范围。

### M5：三次Play、实际输入和本模块资源

`m5-cycle1/2/3-edit-released.json`分别记录三次实际Stop后：ownedCapture/queryProxy均null，marked Material/RT/Mesh为0，唯一canonical场、12个原Collider/115个Renderer、无capture owner/诊断设备。Play→Stop后Edit dirty=true反映原有派生缓存生命周期，不写成每次都自动clean；最后另有显式Save回执。

`m5-cycle3-owned-identities-play.json`收集104个实际模块owned引用，`m5-cycle3-owned-identities-stopped.json`逐ID/实际引用核实tracked104、stillLive0、live=[]、reused=[]、passed=true。其证据强于只看命名前缀或总数，但范围是这些明确owned对象。全局原生Material总数从5834到5951/6098/6341的增长仍未归因，不能据模块资源释放签“全引擎无泄漏”。三次Play里的第一轮另重跑Live合同run`1e845cbb068541d18505f5d0bd395ccf`，只读poll报告GREEN/leasefalse。`m5-final-source-sha.json`最终197个保护文件unchanged=true。

真实输入合同使用InputSystem虚拟设备事件，经原InputAction/角色链消费；不是实体硬件操作。`m5-walk-final.json`run`b76396e2ae66414299430cb9243032db`为PASS，原Current temporary-a在61个移动帧产生约1.79457m位移；测试阶段不直接改坐标、HP、mask或时间，设备均移除。

同起点(12,.01999982,12.62)墙碰撞：ON run`35099ec2d2e649668a89e4722e04a5c0`最终Z12.48423，blocked/sides102/102；OFF run`f315a294d0f247e19007ce0a075e24a5`最终Z12.4842262，blocked109/sides110，两态PASS、最终Z差约3.8e-6m。既有真实CC在有移动请求时截断，不是直接把角色放入墙再判断；不同帧数不当作差异。

原攻击链：OFF run`7a7c8e2726e64a18ba5a21a0a1c3bdb5`、ON run`4702d02bf7fd4785af7e62c558570ead`均PASS，Current temporary-a专属Applied各4个唯一ActionId，设备移除/捕获归还。敌人同时受伙伴正常攻击，因此不比较敌总HP损失；不声称所有攻击应被墙阻挡，也不代签真实角色HP死亡事件。

### M5：最终四态有限成本

`m5-fourstate-poll1.json`run`fa51f2ebbd3b467f8a32acca62711138`先取得OFF旁路版四态数据。路线修后再以`final-route-perf-poll.json`run`d1f4eeca2d3a4ee7b6704e570b61f0e3`作为最终版本：DONE/error=null、inputLeaseReleased=true，同Stage2/1600×900/yaw180/TimeScale1、四态各90样本。下面ms列分别是最后20Hz检测样本及capture提交CPU，非GPU时间。

| 状态 | Queries / Capture | 检测ms | 提交CPU ms | Draw / SetPass | owned RT / Mat / Mesh |
|---|---:|---:|---:|---:|---:|
| OFF | 0 / 0 | 0 | .0441 | 93 / 84 | 0 / 5 / 1 |
| 仅检测 | 500 / 0 | .2317 | .0466 | 93 / 84 | 0 / 5 / 1 |
| 局部 | 500 / 1 | .2332 | .4595 | 99 / 88 | 2 / 7 / 2 |
| 整组许可 | 500 / 1 | .2340 | .4956 | 99 / 88 | 2 / 7 / 2 |

最终Main counter约2.948/3.108/4.524/4.068ms，GC counter均值约65093/77501/78159/78153 bytes，含Editor/其它视图/采样与正常世界，不归因成模块净成本，更不能称0GC。Render Thread counter无效不可用，GPU未测、Standalone未测；不能从这些Editor计数推最终FPS。旧run`269f…`的OFF500查询保留历史，当前生产旁路已由OFF0查询/0检测实际确认。

### M5：预算与两比例/两距离的参数域

`BudgetProjection_08b390d83d504893b485c56a531f65ad/result.json`实际GREEN/error=null，8项通过。五个真实自有模型及独立查询Box产生真需求（各组Eligible/coverage1），ActiveGroups5、CaptureCount4、BudgetOverflow1；主场Collider IDs仍12个、未增加，5个查询Box属于独立物理场。超预算的一组保留原显示，不冒称五组全透明。

实际main临时RT使用4:3的480×360和16:9的640×360，分别distance20/28；公开ResetView确保真实zoom确为20/28。BodyPixels约61.3892/44.1322与独立投影相等，实际shader `_VisScreen`匹配输出尺寸，各圆XY换算像素半径相等。该合同明确pixelOracle=false，只签实际投影/uniform参数，不代签GPU圆形边缘或审美。结束自有Scene卸载done、原5组/相机distance25/yaw45/target/aspect/MSAA归还，lease=false、TimeScale1。

### 最终连续画面：v2已取得正确四阶段示意

首轮Movie run`e38be…`虽是有效真实录屏，约67.725秒/1072帧，但伙伴正常AI跟随进入墙后，max WholeFade提前到1，四图都显示整件淡出，未用于签四阶段示意。原视频/图保留历史，不覆盖raw；仅调试Routes把伙伴合法起点放到主控左2米，核心逻辑不改。

`movie-v2-record-stop.json`recording_id=`dad01ac9d56d48259bac186545e327fa`实际completed/ready、error=null，H264 silent、1280×720、601帧/7,027,899 bytes。ffprobe时长40.59805s，停止回执elapsed40.58526s；请求的录制fps20不是游戏FPS结论。root实际查看四张`private-v2-stage-0..3.png`及视频19/23/27/31s的四张解码图，确认轻柔边→扩大→整件消失→原墙恢复。录屏有压缩块状，四Native PNG清晰；这只是时点抽检，不宣称逐帧无闪。

比较使用稳定`movie-v2-stage0..3-state.json`，不是仍在旋转中的即时Stage命令回执。四态camera position/rotation完全相同、FOV35、TimeScale1、RenderError=null：主控radius1.3/Whole0、radius2.2/Whole.1562501、双方Whole1、最后双方IsReleased/Active0。前两态伙伴Whole0，解除此前伙伴提前全淡出干扰。使用原Motor合法起点和原AI跟随、世界正常运行，非人工键鼠/实体硬件示范。

### 保存/重建/关闭重开已执行；第一轮性能仅保留历史

初轮`edit-save-rebuild-reopen.json`31/1并未关闭或重开；失败在测试夹具进入中性过渡场的安全检查，不能以31项前置通过冒称已完成重开。修正夹具后，`edit-save-rebuild-reopen-trace.json`实际42/0、status=COMPLETED、error=null，closed/reopened均true：实际SaveLab、两平台显式Rebuild、再次保存、关闭并打开新Lab，原197文件SHA逐项核对；作者/角色和支撑TRS保持，渲染/持久引用有效，未膨胀缓存或改变既有GUID-localId。最终是本canonical单场、Edit、dirty=false。原因是`NewScene`已成为active，去掉冗余`SetActiveScene`后读取实际active，而非放宽场景边界。

第一轮`perf-final.json`run`269f957deba84fb4b777b97eb80f17c8`四态各90样本时OFF仍Queries500，之后生产增加OFF检测旁路。因此原始数值只保留为历史，当前最终数据以本节前方`d1f4eeca…`为准，不从Editor计数推Standalone FPS。

### 旧场最小冒烟与安全返回已执行

第三完整run`0992bc268f6a488a9d619a24bea65de2`的`legacy-smoke-…-play-observed.json`为PLAY_SMOKE_GREEN/error=null，约2.0226秒实际Play；两个原角色的模拟时间和动画track clock均前进、原运动/动画端口ready，原Current temporary-a合法、TimeScale1、新模块0。它是只读最小兼容冒烟，没有注入旧场走路/战斗，不据此签旧场完整玩法。

`legacy3-return-new.json`为RETURNED_NEW_SCENE：500项非派生作者快照与197保护文件保持，旧场没有保存，只有已识别缓存/装饰朝向等派生dirty在保护门通过后丢弃；新Lab已返回Edit、dirty=false。该完整链初始/观察/返回Console Error计数均0、newErrors=0、historyReset=false。首轮`4ec085…`因MCP prepare跨reload取消产生工具Error，第二轮遇到ClearOnPlay历史重置，均保留原回执，不冒称它们通过，也不据第三轮范围写“全局永远零错误”。

### 低角、正交与原结构观察旁路

`ViewModes_b52c36e909724b208e42fd2b56a20beb/result.json`实际GREEN/error=null，4/4、约1.7753秒。通过原Map公开配置/ResetView及真实Routes Stage2，实际Gameplay Camera pitch20.0000038°、透视Capture1/targetValid=true，切正交后orthographicSize9、Capture1/targetValid=true，RenderError均null。未新建相机、未造Demand或推进状态，pixelOracle=false；这证明实际主链支持，不代签低角/正交全部像素颜色或审美。

调用原`SetStructureMode(true)`后，原12°observer取得2次实际End回调，main回调增量0；52个原source Renderer的forceRenderingOff保持/归还，自有capture代理隐藏，世界Time.time继续约.01764秒且TimeScale1。最后回到正常35°透视、distance25/FOV35/yaw45，原Actor即时姿态归还，restored=true/ownLease=false。只签该原观察路径，不把它扩大成SceneView所有工具或所有相机Feature验证。

`SceneViewIsolation_f47c989999c343ec8929e367c4f92a62/result.json`仅Repaint既有视图的第一轮为UNAVAILABLE/error=null、failures=[]：约4.006秒内main1053 Begin/End，已绑定已知自有材质的Begin/End各6318样本符合1→0，52源显示flags及代理归还；两平台当前cache ID均未替换、rebuildCount各49→49。世界Time.time前进约4.31958秒、TimeScale1。SceneView没有真实Begin/End，不能将Repaint当作SceneView通过；该轮足以支持本区间main scope与两cache不逐帧重建，不代签隐藏标签页或所有操作。

随后可选Focus模式run`d34f8a416fd2404dbebb23c4bc6d0668`在Start的原焦点归还前提检查即ERROR：Unity非前台，`EditorWindow.focusedWindow`为null。未取得新帧、没有Focus或世界变化，不能冒领GUI验证。最终切换为不聚焦UI的既有相机模式，`SceneViewIsolation_7131dff0c4e840a59b1e7c7e90735a88/result.json`才为GREEN/error=null/failures=[]：约4.004秒，main实际1036 Begin/End、原有cameraType.SceneView相机实际manual Render取得1 Begin/End；main已绑定材质6216、SceneView6、End6222样本均符合main1/SV0/End0，52原显示flags与自有代理归还。两cache身份和rebuild49保持，世界时间+4.316162/TimeScale1。

此模式只创建自有128×128 RT，原SceneView相机targetTexture、aspect、投影/视图矩阵及RenderTexture.active精确回原值，RT disposed=true。没有创建Camera或聚焦标签页，**只签既有SceneView相机的实际渲染隔离，不签标签页GUI绘制、硬件操作或像素审美**。两个可选探针开关随后归还false。

### 离屏恢复、前后资格与当前复合高台画面

`QualificationRecovery_672b682151c84b6cb096584813f43938/result.json`整体PARTIAL/error=null，所有功能检查true、valuesRestored=true/ownInputLease=false。原公开镜头目标移离角色后，两目标visible=false，10个状态全部IsReleased、Active/Capture/Queries0；回屏由真实检测重新取得小墙需求并Capture1。经原Motor把两角色移到小墙前方时，仍visible=true但小墙Demand不合格、状态释放、Capture0；不把屏幕重叠误当模型在前。原角色/镜头/所有权成功归还，世界持续运行。

此轮预设X=-6.55/-6.45的八次微动均Eligible=true，虽然状态有限/无错误并在离开后自然释放，**没有跨越资格边界**，因此未签跨界微移，整体不写GREEN。随后`87fad4a183b948238e53e0666ad207ed`也保留相同PARTIAL边界。这是取样对照不足，不冒充生产闪烁故障。

最终`QualificationRecovery_2c9a0651c798496b946ca2cf00fd3a79/result.json`为GREEN/error=null，9项均true，约5.7339秒、valuesRestored=true/ownInputLease=false。只读真实Probe校准实际边界X约-6.59491，再经原Motor在其两侧±.15m往返8次；正式Root Demand的Eligible严格false/true交替，各姿态保持约.18秒并由检测deadline前进证明真实检测已消费，不把旧检测结果误作当前微动。LocalStrength保持0–1、WholeFade均0，退出后全部10状态自然IsReleased/Capture0；两原模拟时钟继续推进。未写Demand/State或暂停世界。离屏/恢复与跨界的状态证据不代签GPU全图无残洞或微移逐帧审美。

`m5-cycle3-play-foreground.json`真实Play读回高台A/B各24/25显示成员、各10个Sprite，当前一组活跃/捕获1、RenderError=null。对应本机`lifecycle/private-cycle3-foreground.png`及较早`private-props-foreground.png`已实际查看：当前复合高台窗口中双角色头、躯干、脚可见，花草随窗口显示，未见技术七层叠灰膜式遮身。结合七层独立像素及Simple Sprite合同，支持当前已知复合组的有限结果，不能再写“没有实际高台画面”；也不泛化未知长武器、坡脚、任意材质或GPU全场逐帧。人物及参考媒体仅本机，不随公开文档打包。

### 真实死亡事件与原协调器自动交接

`RealDeathRelease_55245105ceeb4ee7845dd98a4b2ed7e0.json`为GREEN/error=null，8/8、约1.3642秒。使用原`CombatDeathDebugView.ApplyDevelopmentHit`，沿实际Released→Health返回Applied，temporary-a HP100→0，Vitals.Died与Root.Died各触发1次；同一Died事件栈内原Coordinator已自动把Current交给temporary-b，没有调用SwitchNext或直接写Current/HP。

死者Target资格立即撤销，正式相机随后不再选择死者，原需求状态自然释放；存活伙伴原State实例及真实Demand保持、Strength1/HP100，世界正常推进。ownInputLease=false，运行态死者HP不回填、不保存；由root正常Stop返回磁盘初始状态。该门不包含敌方物理挥击、闪避/Guard/LOS规则、复活或任意伤害种类，不能用开发受击入口冒充这些扩展回归。

## Native判据与未认证范围

下列保留原判据；其中已执行部分由前文runId与矩阵登记，未列实证的部分仍为未认证范围。以后扩展时仍需先冻结条件与阈值，取得RED，再写生产；不在失败后放宽阈值或删去困难区域。Preview/程序事件可支持因果，但不能独自代替新场实际Play、既有角色与连续画面。

### A. 核心必须真的看见身体

在正式Spine网格更新后，取同一个有效姿态/方向的实际Mesh、原纹理/材质及当前相机，作受控参考：无遮挡身体图、遮挡关闭基线、效果开启图。确保比较使用相同动画时间/几何/光照/输出条件，不将两帧动画差异误当透明错误。

从无遮挡图选身体不透明、与背景可区分的内部像素，按头/躯干/脚部分区分别报告有效样本，不把隐藏方向面、未引用顶点和透明atlas边角当身体。清晰核心里应匹配正常身体颜色/可见结果；记录wrong-covered/gray-tinted/missing样本，样本不足不得判通过。核心透明不等于圈中心一个点透明。

此图像判据同时抓住“角色参数未改但仍隔着灰墙”的假通过。软边、MSAA与低对比像素另列，不能无限扩大排除区域。正常贴身装备与伸长武器/特效另报，不用后者把洞撑满屏。

### B. 角色alpha未被本模块修改

在稳定姿态测试中，读角色Skeleton/相关slot颜色、正式Mesh有效颜色alpha、材质引用与有关MPB颜色；开启/加深/全淡出/恢复/禁用后比较。本模块不得向角色写alpha/颜色或替换Always材质。

正常动画自身可能合法改变slot/顶点颜色，因此不能对不同时刻全部Mesh颜色做无条件hash相等；需同姿态参考或只比较应保持的通道/所有权。结构只读检查与A的最终像素结果一起成立，不能相互代替。

### C. 平滑羽化与多层不发灰

厚墙＋实际Spine先验证中心、羽化带、外围。用可区分的受控颜色、已知opacity和标准混合参考检查羽化，而非只看Inspector数值；相邻环带应有连续中间不透明度，不以二值洞/明显空间抖动签平滑主效。

复杂组取当前实际可见的顶层/表面作独立期望，比较该组一次正确羽化与实际多层输出；不将七层各自混合的结果当真值。核心需与无遮挡身体参考一致，外围需与原组正常显示一致，羽化不得因层数增加显著叠灰。墙背面、封口与隐藏层仍可能挡住角色，要同时查实际启用深度Pass。双组是两件真实遮挡，按各自需求处理，不能错误套用“整个世界只允许混一次”。

### D. 碰撞与既有攻击保持

透明前后比对Collider enabled/层/形状/Transform与角色Controller/运动入口等状态，再用同一条现有Input路线实际走向厚墙，确认均不能穿入。不能直接写Root进入墙内再宣布碰撞失败或通过。

使用相同攻击条件、实际既有Attack/HitResolver，比较效果关/开时的命中资格与结算；保持原规则，不强加“所有攻击都必须被墙挡”的新设计。查询代理不进入角色碰撞/攻击语义。数据不变加真实交互均需记录，单纯字段hash不足以证明行为。

### E. 检测、两角色及投影

- 未淡出代理驱动检测；固定遮挡几何后持稳，经过过渡时间仍保持需求，不因已经开洞自取消；静止半径/整组强度不持续累积。
- 同一射线布置前后两登记组，每组的显式volume查询均可取得资格，不让较近的其他组吞掉本组；登记顺序改变不改变资格。无NonAlloc/hit缓冲，排序/命中缓冲饱和子条不适用；Root活跃组超预算仍须诊断和受控处置。起点在支持代理内/近裁面专项记录，非凸内部不靠Bounds猜。
- 两目标需求为max/并集；关闭/失活其中一个，另一个洞仍有效；切主控保留旧需求并平滑调整。共同支撑冲突按主控策略显式拒绝整组淡出。
- 在实际viewport投影中检查核心中心/半径与身体关系；两宽高比、两zoom、viewport偏移、透视及实际支持的正交路径分别记。相机前后/角色前后资格与屏幕重叠分离。
- 记录最终相机矩阵、身体正式更新帧号、mask帧号及渲染相机；旋转/行走时必须同帧一致。检查SceneView/观察相机不改变Gameplay需求，恢复后无残参数。

### F. 生命周期与有限成本

三次Play按相同热身/退出条件记录模块自有材质、RT、代理、Pass注册及事件订阅，禁用/退出恢复后数量无持续增长；Editor其他资源波动另列，不凭全局对象数一帧相等证明无泄漏。若其他写入者接管同一属性，旧模块释放不能覆盖其新值。

在新场显式重建顶盖，取得新cacheRoot/实际Renderer并检查组引用已更新；不能继续读取已毁对象的managed字段。实际保存关闭重开后，再从正常场验证状态/组/材质，不把运行opacity存进资源。

性能四态使用同一场、镜头、姿态、分辨率与画质；报告实际采样方法、CPU/能取得的Render Thread/GPU、Draw/SetPass、材质/RT与分配。无Profiler证据不签0GC，无法取得GPU/Player就记未测，不从Editor时间推FPS。

## 后续证据登记格式

每个回执保留：runId/版本、实际场与相机、原始输入与判据、有效样本/失败数、清理与保护、自动事件或实体输入、图片/录像位置、范围及未执行项。颜色路径、实际启用的DepthOnly/DepthNormals、Depth Priming/深度拷贝/SSAO和世界阴影各列当前配置与支持状态；“不适用”必须有实际未启用依据。

用户截图、人物/参考材质与运行媒体仅本机；公开文档可总结证据，不能把受限媒体或其派生几何随源码发布。最终报告不使用“所有穿模彻底消除”“全局0错误”“零开销”替代限定结果。

## 最终本机交还

`final-stop-ready.json`及`final-delivery-save.json`、`final-delivery-resource.json`：唯一`SC_OcclusionVisibilityLab`，Edit ready、编译空闲、dirty=false、TimeScale1。两个角色回到(12,.02,-18)/(10,.02,-18)且IsAlive=true，运行态死亡没有保存。捕获实例/查询场为null，两个角色OwnedVariant/OwnedCoverage/ownedRenderer均0；标名材质/RT空、标名Mesh0；输入Owner0、M1 lease空、合成设备空。源文件最终197/197 SHA相同，原开发细则blob为22678d85258bc80c8f8448b0162281e38d2834c1且未改。

全Editor原生对象总数不作为本模块无泄漏证明。源码测试针对本机当前工作树，用户原有未提交改动保留；发布不等于含私有依赖的干净仓库构建认证。真实媒体仅在本机`研究/可视遮挡/KT-VIS-01/02-visuals/M7/`，不纳GitHub。

最后只规范化本轮源码/文档换行；final-lf-ready.json再次Native编译ready、0编译错误。post-format-final-save/resource.json再次确认新Lab Edit clean/单场与自有资源、输入归零，行为实现未改。Unity原生.meta保持导入器格式；源码及文档diff检查通过。

## 定向发布

功能提交 `04aac0039a83a80bfbdc357b10749a46240bdae8` 已推送 GitHub OSSASTRA `main`，fresh `ls-remote` 与本地完全相同。74文件仅自有源码/Shader/HLSL及Unity生成meta、技术文档和必要窄接线；装备脚本只含只读getter两行，Registry只含本任务段。发布后原37项dirty仍保持，未携带其用户逻辑、退休删除、场景、美术、缓存或录像。后续完成记录作为独立文档提交，不改变功能实现。
