# ANKALUTE 共享项目状态

> 2026-10-22装备约束：副手能力/短按长按由SO与映射决定；投掷来源和投递方式分离；正式装备为独立Spine模型+SO骨骼/插槽跟随+语义动画联动。当前盾防/炸弹/Sprite为过渡，未完成通用化；见OSSASTRA_RULES R29-R32及tasks/2026-10-22-equipment-boundaries.md。

> OSSASTRA接手必读：[用户规则](../../OSSASTRA_RULES.md)及[最新角色基础与DataBus](../ossastra/character-foundation.md)。旧代码已按用户要求解除禁编，共享脚本已去M01命名；下方较早迁移记录属于历史。

> 2026-10-08 2D Atmosphere v2.2执行完成，整体Visual PARTIAL/技术当前场核心实测PASS，停本轮不做第三路线/户外/v2.3/2.5D。独立SC_2D_AtmosphereV22_Dungeon与5档Profile；首因原生Surface Lighting Visibility楔形、旧有限遮挡散射放大。24样本SDF软可见性+地面灯光信号局部过滤，保留几何楔形表达；实际实体终点遗漏红→绿，27318固体内GPU暖散射0、门洞空气与有限散射保留。Stable旧v21D4，BestVisual候选新Balanced/用户Pending。9主图、5档/消融、DB/DC滑块、55.007s原生2930帧、时间戳平均53.263fps；编码块状限制不认证细边动态。1440唯一FrameTiming样本、GPU零值剔除，当前整帧中位D4 0.293→Balanced 0.574ms，非单Pass/低端认证。20交错自有2RT/11Mat/52Mask数量稳定、Restore归0；全EditorRT/bytes有短暂波动，原严格false保留，未认证全引擎无泄漏。3304冻结含2879旧文件零变更、59完整TRS/Sprite/order/metadata/原灯参数/相机保持；新/旧保存重开精确，窗口最大1/255差。最终V22 Edit ready/clean/Balanced，Probe/自有RT/Mat0、5Shader和材质错误0，默认Renderer0/15项保持。报告docs/render-research/2D-Atmosphere-v22.md，比较artifacts/xinghai-2d-atmosphere-v22/comparison.html，本地8861。禁区未读取，无PNG/3D/角色/全局管线/提交推送。Unity与全部路径租约释放；用户明确要求完成后关机，交付保存后执行正常关机。

> 2026-10-08 2D Atmosphere v2.1地牢实验交付完成；Dungeon Visual PARTIAL、Engineering核心实测PASS、Gameplay PARTIAL/正式玩法未测、Outdoor NOT STARTED、用户审美Pending。独立SC_2D_AtmosphereV21_Dungeon和自有Shader/Mask，Renderer13只读、默认0/15项不变，无3D/角色/源素材改画。连续二维空气、两暖灯遮挡散射、空间暗边、同源Near/Far Dust独立控制；25主图/18折叠诊断、55.01s原生2925帧时间戳平均53.17fps，启动857ms停顿保留；录像细节简化，不认证细边全程稳定。D4最终编辑器整帧GPU中位1.601ms/P952.450、Draw391，单Pass/低端未测。6 GPU契约/最终20交错恢复/跨场与窗口/EditPlay/重开通过；2879冻结SHA和59层布局不变、旧两N3与新D4重开逐像素一致、窗口最大1/255差。自有2RT/4Mat恢复归零，最终新场Edit ready/clean/D4、无Probe。硬阴影楔形、二维Alpha/语义边及灰膜感仍在，按有界艺术实验停止，不转2.5D、不推进户外。报告docs/render-research/2D-Atmosphere-v21.md；比较artifacts/xinghai-2d-atmosphere-v21/comparison.html，本地8860。禁止目录未读，无提交推送，Unity及全部独占路径释放。

> 2026-10-08 2D Foundation v2实验交付完成，停Gate2D-V2 Partial/用户审美Pending：3独立场景、43原创模块215图、Renderer13只读复用；31同素材N0–N3/预设/适用Off、Play巡游各34秒实际56.323/56.467fps、1200编辑器帧样本、两场20轮稳定、三场重开与2266冻结SHA全不变。户外受光/彩色暗部/远空气有价值，Dungeon厚空气/暗边融合/连续阅读仍Partial；DOF非真实Depth、RoomId元数据、有限太阳投影固定2D。N3 GPU中位Outdoor1.088/Dungeon1.625ms，非单Pass/低端认证。旧四路/LookDev/T7 Light新鲜实拍恢复；旧缩图不与native1600逐像素等价。v2 shader0error/1warning，Asset Refresh缓存SpeedTree需重生成×4保留，未修冻结旧资源。最终Outdoor Edit ready/clean/N3Light、默认0/renderer15项保持；所有租约释放。禁读目录未读，无新3D/角色/全局管线/PNG修改/采购/提交。报告docs/render-research/2D-Foundation-v2-visual-target.md；比较artifacts/xinghai-2d-foundation-v2/comparison.html，本地8859。

> 2026-10-08 2D Rendering Foundation迁移验证已完成：独立SC_2DModularRuins_Plaza_FoundationV1、Renderer13/14；Green A+Outdoor Light同镜头Play A0/A4/F1/F2、局部/消融/深度证据齐全。F1能承载主要配色、材质角色、冷暖与轻后处理；F2局部明暗/接触更强但更暗，未证明整体明显更好。浅卡片处于8m清晰带内、可见距离不足35m，DOF/Air基本无贡献；不适用BaseHaze/云影/风/水未强套。278原对象TRS/102旧Mesh/654旧资源及42PNG保持；32次64帧切换19mat/6RT稳定、编译/Shader/控制台通过，重开Play平均差<.013/255。旧LookDev/T7只读、原Renderer0–12和默认0保持；无T8、无新3D美术、未读Script/Scripts。最终Edit ready/clean/F1，全部租约释放。报告docs/render-research/T7-2D-foundation-v1-transfer.md；对比artifacts/xinghai-2d-foundation-v1/comparison.html（当前本地127.0.0.1:8858）。T7原H3与O-B焦点门禁状态保持。

> 2026-10-07 T7-C.3已执行并停H3：四独立SC_FinalComposition_*_T7C3场景、12Post Profile与两Green材质修订；37正式图（12主图/4同主视角Hero/15消融/6Green）、38/45秒原生MP4平均53.999/53.711fps、1620实际1600×900 HDR/MSAA4独立GPU样本交付。四场各80模式、保存/关窗/Play退出、10轮40跨场进入和10历史代表恢复通过；1251旧SHA一致、四场旧几何/相机/灯光/Mono引用严格审计无未授权变更，Amber129surfaces引用曾空已精确恢复。仅C3 exact-owner补偿1×1阴影RT、提前fake-null的SSAO与六个renderer/debug内部材质，实际RED/GREEN且暖机后RT/SSAO/material数量/native bytes无持续增长，不清历史缓存；依赖团结2022.3.62t13/URP14.2.0-t1内部字段，升级需重验。H3工程Pass，Gameplay/视觉Conditional：正式O-A focus最大0.02819m，额外O-B快速anchor0.36055m原Fail保留；32抽样帧非完整视频认证。用户明确选户外Light/地牢Light/夕照Expressive/Green A，Foundation未正式冻结、不自动C4，旧G2/F4与DungeonMedium Experimental保留。Future仅独立GPU输入+CPU深度参考、HUD占位，无正式角色/Spine。最终Amber Edit/ready/clean/restored，四场MissingMaterial/ShaderError0、自有临时/target/lease0，root与代理租约全部释放，未读取Script/Scripts、未提交推送。报告docs/render-research/T7-C3-final-composition.md；对照artifacts/xinghai-t7c3-final-composition/index.html。离线数据/JS/37图文件尺寸和链接已验，浏览器file策略拒绝，实际浏览器布局未验；三组原图对照已直接在对话展示。

> 2026-10-07 T7-C.1区域色彩已执行完成并停H1：独立SC_RegionalColor_Validation_T7C1、三色板与11源材质/278Renderer可逆用途绑定，原409Renderer几何相机材质引用保持。16正式图+12重开（11逐像素同，余1像素≤1/255）、36秒Play/35.002秒1005帧录像实际28.713fps、两轮1440p性能、20恢复/关窗/保存重开/机位入口通过；1178旧SHA不变，无新Pass，四状态Draw297。工程状态PASS，BestVisual推荐翠绿Full、GradeOff为BestStable工程候选；用户待看图选择。O-B低反差、朴素测试形体和草影/水边锯齿保留，旧G2未全通过。最终C1 Edit/ready/clean/restored、三色板clean、temp/RT/haze0，窗口开；全部租约释放，未进入C2/C3/角色/DOF，未提交推送。报告docs/render-research/T7-C1-regional-color-value.md，对比artifacts/xinghai-t7c1-regional-color/index.html；浏览器安全策略拒绝file协议，实际页面布局验收未完成。

> 2026-10-07 T7-B.1执行与素材追加验证完成，Gate G2不整体通过；新增独立原代理/下载素材两场景、植被V0/V1/V2、相位风、尺度链/60-100-160m巨构与局部底雾。57正式捕获（53唯一）/33诊断/16空气/39消融/12下载素材，60秒1697帧原生录像实际28.28fps；16密集相邻帧+16路线抽帧。两场各60切换+20恢复、窗口关闭/保存重开、原7书签、1926冻结SHA和4个P0逐像素复拍通过。最终1440p4080SUPER GPU旧Visual1.562→新Visual1.802ms/Draw239→290，编辑器复测有波动。下载Kenney CC0方块树草并独立验证；用户提供MC网站已查，晨曦回廊百度网盘地图未取得，未冒称Minecraft原版已导入。Gate C/F/G/H通过，A/B/D/E部分；不进入T7-C、不无限追加Shader。最终下载素材场Edit/clean/restored、窗口开、RT/temp0；全部租约释放。 报告docs/render-research/T7-B1-outdoor-life-scale.md。

> 2026-10-07 T7-B Outdoor Atmosphere 执行与验证完成；完整艺术 Gate G 不通过：独立260×300m场景/13候选/7机位、天空云/连续彩色空气/水/风/RealToon副本和Sunset语义适配交付。63矩阵+56消融+40诊断+24质量+5材质+10夕照及14先导，共212组合；90秒Play路线、86秒8fps原生视频/16抽帧。18契约、两场景各60Apply+20Restore、窗口关闭/保存重开通过；1827旧文件仅允许Sunset新增根变化，原75块保留仅SceneRoots追加，旧6图5SHA同/余36像素≤1/255。剔除最佳档Wet/Bloom后实际1440p4080SUPER整帧GPU median Neutral1.545/Low1.388/Medium1.365/BestVisual1.343ms；单轮编辑器样本，不据此排名shader成本或认证低端。B/E/F/H通过，A/D/G部分、C尺度未达成；保留Vibrant与O8审美候选，Wet白化/强风/浓空气和通用昼景套夕照为失败对照。BestStable为Low工程候选，BestVisual为调轻O8综合候选；不等同审美或完整时序认证。录像为静态Wet/Bloom剔除前版本，8fps不证明高频稳定性。用户Dungeon Medium审美偏好独立保留，本轮地牢不改。最终新场景Edit/ready/clean/restored、T7B窗口开启、RT/temp0；全部租约释放，未进入T7-C、未提交推送。报告docs/render-research/T7-B-outdoor-atmosphere.md，对比artifacts/xinghai-t7b-outdoor-atmosphere/index.html。

> 2026-10-07 T7-A.2 执行与验证完成，艺术 Gate F3 **未通过**：地牢新增独立、可逆的16个局部空气体积与Route A/B/C，BestCurrent为Route A、6样本、Noise Off、Dust/遮挡 On。36正式矩阵+30消融+18诊断+84参数图、74.50秒真实Play、13/13测试、20轮三状态恢复、旧三档重开18/18逐像素同；旧冻结1720文件仅允许修改的主场景变化，其余1719不变。4080SUPER 2560×1440 GPU中位T7-A 1.609→T7A2 2.432ms（+51.2%），Draw同1019。D-A暗楔减轻且关Dust仍有空气，但D-D空气偏薄、D-F径向受光、D-I直边、D-H散射角楔仍未满足非径向黑暗目标；不冻结地牢，不进入T7-B/C。最终地牢Edit/clean/原始渲染状态，全部租约释放。见docs/render-research/T7-A2-non-radial-darkness.md及artifacts/xinghai-t7a2-non-radial-darkness/index.html。

> 2026-10-06 T7-A.1 参考对齐执行完成，但艺术 Gate F2 **不建议通过**：九独立候选与可逆窗口交付，RA1 Salt Ash/RA5 Deadlight DarkEdge 为各支较稳选择，RA8 仅供 A/B。119 图、55秒真实巡游/60秒录屏、20×旧/20×RA8/20×Restore、18/18重开逐像素同、131旧SHA不变；GPU中位1.672704→1.681920ms，Draw均1019。动态仍有清楚灯区圆边，空气层次不够；提高散射出现角状伪影。不升级正式地牢基线，不冻结方向、不进入T7-B。最终主场景Edit/ready/clean/原始状态，所有租约释放。详见docs/render-research/T7-A1-dungeon-reference-alignment.md及artifacts/xinghai-t7a1-dungeon-reference/index.html。

> 2026-10-06 T7-A完成：仅地牢独立Atmosphere层，T62 RealToon基础不改。轻雾+有遮挡局部空气近似+近远双层真实尘粒+AshGray，Best关闭Grain，强Shaft否决。59唯一图、12重开/6P0字节同、60秒录像、7契约直接调用、8GPU遮挡样本与20恢复通过；1470旧SHA不变、两其他场景字节同、地牢仅追加根。无窗口重载/资产保存入口先恢复，T62 Air曾写入预览值已恢复原SHA并复测。GPU4080SUPER中位1.54→1.71ms、+2draw，未作目标低端认证。Gate F建议通过，用户画面待验收，T7B/C未启动。当前地牢Edit/BestCurrent/T7A窗口；全部租约释放。详见docs/render-research/T7-A-dungeon-atmosphere.md。

> 2026-10-06 T6.2收束完成：RealToon环境基础保留，T61高反差为Optional；新增EnvironmentLookProfile/Controller、Outdoor_Vibrant_Base/Dungeon_Dusty_Base，三场可逆环境预览不替换基础材质。17测试/三场20轮/494旧SHA/原组件保持；19首拍19重开最大1/255，地牢6Play，真实场5Current与T61像素同。环境VolumeMask生效、角色/DOF/Grain/Dust仅预留关闭；光区参数可调但物理叠加，不是硬隔离。Gate E架构建议通过、审美待用户，未进T7；最终夕照Edit/clean/Neutral及T6.2窗口。全部租约释放，详见docs/render-research/T6.2-render-contract.md。

> 2026-10-06 T6.1 RealToon高反差候选已交付，停Gate D2、未进入T7。三独立接收端与78Profile可逆预览，旧默认保持；夕照/地牢8机位全关像素一致，15/15测试、三场26轮恢复、701旧文件SHA不变。297正式图/55重开复拍（48字节同，余最大2/255）、39运动帧/45秒录像。GPU4080SUPER编辑器单次180帧约2.40→2.60ms，非Low认证；MPB影响SRPBatcher。候选保留但不无条件放行，暗部可读性、阶梯/细闪烁与G-A弱单灯证据限制详见docs/render-research/T6.1-realtoon-receiver.md。最终夕照Edit/clean/Original、窗口已开，全部租约释放。

> 2026-10-06 T6执行完成并停Gate D：用户已接受前轮Gate C，但本轮建议不放行。新Surface/Local/Natural模块、23Profile、92自然区Renderer与4新机位；4Band/独立金属高光为克制lab候选，未形成MC Shader综合质变，旧G-A/N4结构局部响应收益未证明。两真实场景直接替换材质损失暗部可读性，回归FAIL并完整恢复。121正式图/重开121SHA一致，T5七图一致，13/13测试、23轮恢复、162保护文件/旧13机位/几何/两场景SHA不变；120运动帧、六GIF、45秒录屏。GPU未测得，非Low认证。标准场Edit ready/clean、Baseline/V-A，窗口已开；所有租约释放，T7未开始。报告docs/render-research/T6-surface-local-natural.md；对比artifacts/xinghai-t6-natural/index.html。

> 2026-10-06 T5恢复执行完成（覆盖较早T5暂停记录）：原生Balanced为Gameplay BestCurrent，Variable及中像素FAIL；61正式+30消融+24T4回归/保存重开完成，114/115 SHA一致，余1张≤1/255；T4原图24/24一致。11/11测试、20轮恢复、337几何相机段与旧六书签保持。82保护文件71字节相同、11旧Profile仅新增默认字段，既有参数不变。六候选96运动帧/六GIF；GPU帧时间未测得，4096与2048预算明确区分。最终Edit ready/clean、T3 Baseline、窗口已开，唯一writer释放。Gate C待用户评审，T6未开始。报告docs/render-research/T5-gameplay-shadow.md，对比页artifacts/xinghai-t5-gameplay/index.html。

> 2026-10-06 T5阶段交接：用户转入火环T0只读研究；47张T5正式截图完成并恢复，Editor退出Play/重开保存T3且clean，阶段测试11/11。视觉裁决、运动/成本/消融/最终回归及Gate C报告未完成；全部Unity与文件租约释放。详见tasks/2026-10-06-xinghai-renderlab-t5-gameplay.md。火环只产出外部报告，未改Unity。下方T5未开始为历史状态。

> 2026-10-06 T4 XH Low v0 已交付，停 Gate B：三预设、独立Renderer12与Sky、轻AO/线性雾/ACES/局部HDR灯芯；修复HDR截图，24正式+16消融，重开24图逐字节一致。9/9测试、20轮恢复、149保护资源/几何相机不变、旧T3六LDR基线一致。最终T3 Edit/clean/Baseline、实验室窗口已开，租约全释放。视觉收益主要在天空/环境光/空气，阴影宽软边与云形仍待后续；T5未开始。报告 docs/render-research/T4-xh-low-v0.md，对比 artifacts/xinghai-xhlow-t4/index.html；浏览器file协议限制使HTML布局实测未完成。以下旧记录为历史状态。

> 2026-10-05 星骸渲染研发 T2/T3 到 Gate B：新增独立 Generic RenderLab 与 `SC_VoxelRenderLab_Standard`（50×50 m、八个故意朴素的测试模块、Neutral URP 基线、六固定机位）。Renderer11 追加，原 0–10 与默认0保留；Play/Ultra/Linear 六张基线、十二张同机位 A/B、可恢复批捕与本地对比页完成。Generic NUnit 6/6、旧 Dungeon 24预设/4机位/20轮合同及保存重开/Shader/引用回读通过；旧场景、旧 Renderer、旧脚本 SHA 与本轮起点一致，17份旧材质按 T0 保存态恢复且 D-A 复拍近似一致。现用原版 RealToon 不支持可调 Band，面板明确禁用；Rim 可用。T3 临时 B 组合 V-A 有深色天空边界副作用，不作为 XH Low 目标。当前 Editor 为 T3 Edit/clean、新窗口已开，租约全释放；T4 未开始。详见 `docs/render-research/T2-renderlab-structure.md`、`T3-voxel-standard-lab.md` 与本轮 task。

> 2026-10-05 Cartoon地牢新增局部破败版5：老化墙面Albedo、12局部污渍裂纹贴花，B与干净版4比较。模型镜头不变，保存重开Play像素一致/编译通过，最终Edit ready，全部租约释放。见docs/xinghai-anime-dungeon-decay.md。

> 2026-10-05 Cartoon验证场景新增克制石材v2，默认URP Lit+浅法线+局部光，面板五材质方案/B比较。保留RealToon连续光分支；原模型镜头完全不变。七Play实拍、60切换、保存重开像素一致与编译通过；最终Edit/clean，全部租约释放。见docs/xinghai-anime-dungeon-v2.md。

> 2026-10-05 星骸渲染研发 T0/T1 第一部分完成，停在 Gate A：团结 Editor 原生回读确认当前 URP 14.2.0-t1、六档 Quality 共用 `XH_URP3D.asset`，夕照/暗牢主相机分别用 Renderer 4/6。两场景七个固定机位的 1600×900 Ultra 离屏基线图、实际保存态参数快照、Photon/Chocapic13 逐档矩阵、URP 映射、阴影/大气笔记及 XH Low/Medium/Stylized 候选在 `docs/render-research/` 与 `artifacts/xinghai-render-research-t0/`。暗牢已保存 AO/材质值与其 `Recommended_01` 预设不完全相同，基线按当前保存态命名。七图哈希和 54 个本地链接验证通过；原 Editor 场景仍是 SC_PBRDungeon_Cartoon_Validation、Edit/clean。未改 Unity 渲染资产、场景或玩法，未测目标机性能/Play 动态效果，未进入 T2/T3。详见 `tasks/2026-10-05-xinghai-render-research-t0-t1.md`；租约释放。

> 2026-10-05 卡通地牢改为生成手绘石材，实验室新增CartoonDungeonLook面板路由；默认手绘、B照片比较，原暗牢路由保留。60切换与保存重开Play一致，模型镜头不变，最终Edit/clean，全部租约释放。见docs/xinghai-cartoon-painted.md。

> 2026-10-05 C4纯渲染LookDev交付：独立SC_2DModularRuins_Plaza_LookDev，同42PNG/同投影布局三路对照，五材质族/2D光色与3D卡片法线/AO/轻空气；Renderer7/8追加且0–6/default0保留。10阶段+10关闭效果与固定局部HTML完成。材质/调色收益主要、AO仅0.51%像素受影响，不据此升级3D正式路线。44源PNG/原场景SHA不变、164原TRS/相机不变、引用/保存重开Play/Shader/编译通过。已保存默认A4，当前Play显示A4；Edit刚重开曾偏暗，准确对照以Play为准。全部租约释放，详见docs/xinghai-2d-lookdev.md。

> 2026-10-05 地牢RenderLab交付：SC_DarkDungeon_Exploration默认Recommended_01五档/弱雾/轻Bloom，24预设、4固定机位、39实机对照及HTML总览；隐藏原巡游占位网格，未加角色。独立Renderer6保留0–5及默认0，RealToon描边通过独立Pass实际绘制。20轮切换及取消恢复通过，保存重开3311地图变换与默认相机/导航不变，引用/Shader/编译通过；最终Edit/clean，全部租约释放。阴影为接触AO+软投影近似、非PCSS。详见docs/xinghai-dungeon-render-lab.md。

> 2026-10-05 C4 2D拼接流程验证交付：Assets/2DExperiment含42独立PNG与48派生Tile；SC_2DModularRuins_Plaza/Path为两新场景，第二布局复用40张且新增0。Tilemap与完整Ground Patch对照、逐层真实Play实拍、5种道具alpha脚点、原生保存重开/引用/Shader与编译核对通过，地面4向连通784/784、528/528。本场2D Renderer5，默认URP3D0及既有0–4路径保留；最终广场Edit/clean/SceneView2D。源图42副本SHA一致，暗牢原Scene SHA不变，起始dirty另存Backups副本；全部租约释放。尚有地板片区/接边、重复根蔓和道路边界手修，不作为正式路线或性能结论。详见docs/xinghai-2d-modular.md与artifacts/xinghai-2d-modular/comparison.html。


> 2026-10-05 暗牢结构光版本交付：SC_DarkDungeon_Exploration默认短范围三档局部受光，17材质副本区分地板/墙面/结构，B原柔光与新版切换。地图/镜头/导航不变，20/20路径及CC真实移动通过，四房间Play截图和提灯关闭对照完成。保存重开引用/Shader检查通过，最终Edit/clean、巡游启用，全部租约释放。详见docs/xinghai-dungeon-blocklight.md；夕照结构Shader只读共享，原材质仍保留。

> 2026-10-05 夕照局部块光修正完成：SC_SunsetCart_Validation保留原构图；短半径/三档附加点聚光与16个分级材质，灯杆/墙顶保留局部亮部，车厢/墙肚子撤掉大面渐变铺光。B默认比较本轮前后；最初Legacy仍可选。真实Play、20轮材质数量稳定、保存重开几何相机相同/Shader错误0。最终Edit/clean，全部租约释放。见docs/xinghai-sunset-blocklight.md。

> 2026-10-05 夕照HD2D渲染验证完成：SC_SunsetCart_Validation使用独立Renderer4，实际路灯/木车补光、距离高度空气、RealToon副本、轻AO与Bloom/ACES、B旧新切换。模型/相机构图不变，保存重开及编译通过。按用户要求移除全部Spine测试内容；最终Edit/clean，租约释放。详见docs/xinghai-sunset-hd2d.md。

> 2026-10-05 暗牢地图交付：独立SC_DarkDungeon_Exploration，144×120米、20房间/25连接/6回环、6496平方米铺地、3275模块实例。使用DungeonFamily_v1资源，独立Renderer3的SSAO与低层雾、ACES、局部提灯和黑暗天空球；默认Renderer0保留。NavMesh20/20房间可达，Play实际CharacterController9835次移动20/20通过，保存重开资源/材质/编译检查通过。最终Edit/clean，全部租约释放；操作与限制见docs/xinghai-dark-dungeon.md。未读Assets/Script或Assets/Scripts，未接旧玩法。

> 2026-10-04 夕照纯渲染精调完成：用户明确保留模型细节和构图。SC_SunsetCart_Validation现在使用程序化夕照天空球、低角度暖逆光/软阴影、8份Refined RealToon材质、原生SSAO+自定义深度高度雾、Refined Volume。XH_URP3D追加专用Renderer为2且开启高质量软阴影支持，默认仍0、旧2D仍1；仅本场相机选择2。27个模型变换和相机位姿/FOV与本轮起点副本一致，两FBX SHA与源一致；真实Play、效果开关对照、保存重开、Shader编译及引用验证通过。截图09_纯渲染精调_Play实拍.png；未声称原图逐像素复现或性能验收。原始未保存场景已留Backups副本，旧材质/Volume保留。最终Edit/clean、租约全释放；详见docs/xinghai-sunset-cart-validation.md及sunset-render-refine任务。

> 2026-10-04 夕照木车引擎验证完成：用户指定夕照木车_Unity验证包已植入独立Assets/Scenes/XingHaiHuiLang/SC_SunsetCart_Validation.unity；26网格/83014三角形、8份RealToon、实际夕照晴空Sphere、相机/灯光/雾/Volume均保存。默认UniversalRenderer下真实Play与Hard/None跨帧截图确认实时投影生效，覆盖前轮“投影尚待实际场景验收”状态；保存重开无空材质槽、Shader/C#编译无错误，原测试场景SHA256不变。最终效果为冷影暖空，木车正面仍偏暗，天空与源Blender体素大太阳不同；未验目标设备性能。已退出Play，场景留在编辑器供用户查看；未读Assets/Script及Assets/Scripts、未改全局URP。任务complete/read-only，全部租约释放，详见docs/xinghai-sunset-cart-validation.md与同日sunset-cart任务记录。

> 2026-10-04 星骸回廊渲染实验：用户明确将这份原型备份作为URP 3D验证载体，旧纯2D限制不适用于此用途；星骸渲染任务禁读Assets/Script及Assets/Scripts。已建立Assets/XingHaiHuiLang/Rendering/XH_URP3D.asset与UniversalRendererData（默认0，旧2D可选1），Graphics/当前管线/6档Quality与磁盘GUID一致。RealToon URP V5.0.14已导入、正确Shader路径末级为Default/Default；基础受光与Shader可用，实时投影效果尚待实际场景验收。未来各地图独立建场、先选天空球、必要时优先RealToon；本轮未植入地图/天空球或保存旧测试场景，原场景SHA256不变、Edit ready/clean、无临时对象、C#编译无错误。旧SpeedTree材质曾提示需重生成，未批量迁移。任务complete、全部租约释放；详见docs/xinghai-render-lab.md与tasks/2026-10-04-xinghai-urp3d-render-lab.md。

> 2026-09-20 HTML探索基础接入：测试场3_电车45已接DRPGDungeonRuntime/Hud，右下地图/行囊/原有卡牌三页签；局部揭图、连续走廊、显式门、整窗道具判定、宝箱拾取与满包拒换、同节点战斗回流。复用Party/Coordinator/PartyInventory/CardHand。新增探索探针及4组既有回归通过，最终编译无错误，场景保存dirty=false；unity-writer释放。地图存档、完整生存效果/跟随惯性未迁入；原生拖拽注入坐标异常，已验UI射线事件链；胜利回流使用开发胜利入口。详细试玩/验收边界：`docs/ankalute-expedition-playtest.md`，任务 `tasks/2026-09-20-web-expedition-integration.md`。

> 2026-09-20 GameRender 调参交付纠正：原临时Profile使用HideAndDontSave导致Inspector滑条只读，旧代码赋值截图没覆盖用户输入。现移除NotEditable并在提交后刷新编辑渲染，真实生产Inspector鼠标事件拖动已改变GameView；材质覆盖从普通Float改成与Shader门槛一致的勾选框（历史负值显示关闭）。三相关探针PASS、编译0、Console无error；保留用户美术值和dirty场景未保存，测试副本已撤回。任务complete、租约释放。详见 `tasks/2026-09-20-game-render-inspector-fix.md`。

> 2026-09-19 GameRender 收尾更新（覆盖下方租约状态）：场景与指定 Sprite 可调版本已交付，任务 paused，全部写入租约释放。临时主角材质经历攻击/回位后绑定保留；退出 Play 后确认场景 clean、临时测试材质数量0、相机恢复正交。正式 Spine/图集、目标设备性能与完整死亡链等专项仍未完成，不把当前里程碑当全计划完成。

> 2026-09-19 GameRender 实施进行中（优先于下方同日“尚未实施”的历史设计记录）：测试场 `ProCamera_World` 已接全局预设和Renderer2D `GameRenderFeature`，场景受光后/原Volume前进行保守黑线、尖角暗部与高光，原Volume后/Overlay UI前加细颗粒；角色、伤害反馈和UI保护覆盖率实景可视化，开启/关闭对比有可见差异。现有Volume/Pixelate设置保留；特写基础实机录像可运行。预览克隆、显式保存不改资源身份、Volume恢复和20轮生命周期探针通过。另选场景 `fff` Sprite 挂独立新材质测试逐项局部覆盖与全局继承，可逆安装/恢复，极端值A/B实景可见且已复原；新Shader编译0错误，原材质和贴图未动。正式Play经公开F8入口方法Started、技能和敌方目标真实点击后战斗继续；场景保存后clean。正式战斗Sprite/Spine材质、图集/PMA、3D透视转角、目标设备性能和完整击杀链仍未验收，不得称全计划完成。早前一次Play退出有`[DOTween]` GameObject残留错误，本轮退出Console无error；不归因GameRender，也不可报告项目长期0错。任务仍持唯一unity-writer租约，详见 `tasks/2026-09-19-game-render-style-implementation.md`、`docs/game-render-tuning.md`。

> 2026-09-19 GameRender画风设计Q1–Q16已确认，规格与3里程碑/5任务计划完成，尚未实施。场景优先，单全局预设，角色由材质逐项覆盖；无手绘蒙版，自动分类缓冲允许；Sprite/Spine及场景可见3D兼容。保留现有Volume基底，新增黑边/尖角暗部扩张/高光/细颗粒；UI、数字与X豁免。新内容集中Assets/Script/GameRender（目录已存在且为空）。实机Renderer2D顺序、透明分类/稳定坐标及现Closeup采样正确性列为首个技术门；不将设计承诺写成运行事实。见2026-09-19-game-render-style规格/计划与ADR0006。设计任务complete/read-only，文档租约释放，未改运行代码/Unity资产。

> 2026-09-19 相机透视试验事实：仅在Play临时添加URP/Unlit立方体，现Renderer2D正交与10°透视均可显示并接受GameRender屏幕空间处理；测试场原`farClipPlane=100`不足以覆盖10°透视保持当前视高时约103单位的相机后退，必须同步扩远裁剪面，否则黑屏。退出Play后场景/相机均恢复，未永久启用透视。尚未验3D真实建筑受光、转角及排序。

> 2026-09-18 攻击特写景深与出列层次实施完成（优先于下条设计记录）：阶段5–7使用同一ActionIdentity作用域统一驱动镜头1.15倍、参与者1.08倍及战斗区域高3%下移，环境/旁观者进入实时模糊压暗层；角色悬浮状态和选敌提示隐藏，伤害数字/X保留，阶段8开始恢复UI并平滑回位。玩家与敌方连续行动复用同一正式绑定；Renderer2D灯光层已兼容，不再出现参与者黑色剪影。死亡姿态由原视觉所有者保持，Closeup只撤销自身几何。Geometry/生产生命周期PASS，直接相关合同Conflict 1/1、Motion 2/2、Return/Defeat 5/5，编译0，真实F8 14秒录像Console error0。任务complete/read-only、全部租约释放，未commit/push/clean；全合同菜单仍有Wave/Trace各1项既存非Closeup失败。详见 `docs/agent-sync/tasks/2026-09-18-battle-closeup-depth-implementation.md` 与 `artifacts/battle-closeup-depth/`。

> 2026-09-18 攻击特写升级设计已确认Q1–Q10，规格与3大节点/6任务实施计划已写入 `docs/superpowers/` 对应battle-closeup-depth文件；尚未实施。环境/旁观者实时虚化轻压暗，镜头1.15倍、参与者额外1.08倍及战斗区域高3%下移；群攻覆盖全部真实目标，第三方反应逐步加入且不重复推镜头。全场角色悬浮UI阶段5–7隐藏，阶段8开始回位即恢复；数字/X保持清晰，允许下半身被底部HUD遮挡。几何复原与死亡姿态保留分离，仍用原Action/Return屏障。当前MCP连接拒绝，渲染路线仅经磁盘源码核对，必须先做实机兼容性门。设计任务complete/read-only，租约释放，无运行代码或Unity资产修改。

> 2026-09-16 14:08 自然遇敌正式入口完成（优先于下方仅 F8 的历史）：测试横版地图中，权威队伍根真实向右行走会依次穿越 X=44.98/52.98 两个预置点，在当前地图、同一相机与同一己方实例上原地开战；不调用 F8、不装载新背景。第一战与第二战均以真实技能/世界目标点击完成，结算按钮返回探索；两个点胜利消费后反向跨越不再触发。薄触发带跨帧检测与离开重试门禁 RED→GREEN，In-place 5/5、编译0、Console error0、场景dirty=false。专用1 HP/1架势单敌仅用于验收，不替换正式内容；F8仍保留为开发快捷入口。外层节点地图、正式关卡遇敌配置和生成特效不在本轮。任务complete/read-only、全部租约释放，未commit/push/clean。详见 `docs/agent-sync/tasks/2026-09-16-natural-encounter-flow-integration.md`。

> 2026-09-14 镜头/死亡补修（优先于旧视觉完成声明）：旧CameraPresenter绝对舞台镜头曾强制切透视，节点借用相机现在拒绝该路径；标准相对特写保留。Sprite失能替身/X回位移交渲染后移除，空home渲染完成才允许补位，避免死亡立绘占据survivor槽位。63项回归通过；实机52次UI操作至正常Defeat，相机突变0、可操作回合有效scene失能残留0。120秒录像仅验证行动/波次与镜头，不覆盖后段死亡；死亡依据实机对象/专项测试。未改数值、Scene/Prefab/SO；任务complete/read-only、全部租约释放。详见tasks/2026-09-14-in-place-camera-departure-fix.md。

> 2026-09-14 21:17 本轮场景内战斗回归修复完成（优先于下方历史）：节点探索相机锚点使己方根位于左侧约25%、敌根按相机镜像右侧约75%，F8不移动己方；两方Y=.22/基础Sprite高4.2，特写保留Y/Z。目标投影不再离屏自停用；正式Sprite失能绑定、死亡借用实例隐藏、提交home跨模式保存与归还视觉变换补齐。跨波次额外修复inactive池被累计压暗、失效敌方换排计划每帧重试。62项相关回归全绿，最终实机32次UI事件、跨波次后正常Defeat，非Fault/Abort，编译/近期运行错误0；未验整场胜利。正常Abort后再次F8 Started且三人位置/高度不变。任务记录 `docs/agent-sync/tasks/2026-09-14-in-place-battle-regression-fix.md` 含录像/截图/备份；complete/read-only、租约释放。当前留在PlayMode新开局等待用户，自动测试回调已移除。六敌队内立绘/状态条仍拥挤，不声称美术细调完成；未改VELGITA项目或删除fff静态Sprite，节点路径仅绕过旧FPS SpawnPlayer。

> 2026-09-14 用户实机回归推翻下条完整交付结论：旧VELGITA角色误绑定为己方、探索/战斗位置跳变、特写向下偏移、可见无血条敌人不可选。`in-place-battle-regression-fix` 已重新打开验证与修复，尚未修复。旧5/5探针及短录像未覆盖这些验收，不得引用为总体完成证据。

> 2026-09-14 场景内战斗实施完成（优先于下方规划记录）：F8 不再装载独立背景/重建己方，而是在当前节点地图与场景相机中借用同一批己方实例，敌方首批与增援原地部署；节点阵型、探索调序、固定遇敌点安全边界、直接战斗入口及节点内 HP 连续已接线。真实多回合完成架势恢复→敌方行动→玩家技能与目标选择，清理后第二次 F8 仍可继续行动，LastError空、Console error0。借用 `CharacterRoot` 时暂停其2D/3D刚体并完整恢复，冻结home采用有限浮点容差，修复了数回合后误判离位并 Aborted 的故障。In-place核心5/5、编译0。旧 `DRPGBattleEntryPoint` 已按废弃流程迁移；仍被正式池/投影依赖的Battle World host/prefab保留。任务complete/read-only，全部租约释放，未commit/push/clean。证据与已知旧Telegraph探针冲突见同日任务记录及`artifacts/in-place-battle/`。

> 2026-09-14 场景内战斗规格已获用户整体批准，实施计划已完成：`docs/superpowers/plans/2026-09-14-in-place-battle.md`，4大节点/7任务。计划明确身份与物理站位分离、借用己方不回敌方池、同相机/真实地图特写、F8在M2接线、固定遇敌点/跨战状态和独立世界归档。自审已补齐M2 bounds及场景接线依赖。仅规划与只读源码核对，尚未改运行代码或Unity资产、未执行运行测试；规划任务complete/read-only，租约释放。下条“待审阅”为历史。

> 2026-09-14 场景内战斗设计 Q1–Q24 已分项确认，整体稿待审阅，尚未实施。探索/直接战斗复用当前关卡环境、相机和己方实例；移动由队伍根驱动，左移保持朝右，模式切换不重排。探索可调序/恢复默认，主角实际站位锁定取消（优先于旧记忆），死亡后补位结果跨模式保留。预置遇敌点禁止位于边缘安全区，胜利消费，非胜利/失败须离开再进入才能重试；重试敌方重开，己方保留已生效损耗。保留当前地图上的特写及事务屏障，敌方首批/增援原地出现。旧独立战场路径迁移后归档，不留双模式；节点地图及动画制作不在本轮。见同日 in-place-battle 规格与 ADR0004；不把本设计当运行事实。

> 2026-09-13 音频池场景膨胀已修复：测试场3磁盘曾含2976个PooledAudioSource及三类专用源各93个，共3255个（93套默认32+3），场景14,446,082字节。根因为编辑器/域重载后GameAudioRoot的非序列化初始化状态丢失，而既有可序列化子节点未复用。现会回收同宿主已有命名源、裁剪重复项，并把动态节点标记DontSaveInEditor；新增编辑器重建回归先RED（5→10）后GREEN（维持5）。通过Unity API移除唯一纯音频孤儿容器并保存，场景降至2,017,495字节；重开仍为0音频节点、dirty=false。运行时实测32池+3专用，退出后0；三个音频专项通过、编译0，隔离重开Console error0。清理前磁盘版备份位于`Backups/测试场3_电车45.pre-audio-cleanup.2026-09-13.unity`。DRPGBattlePresentation仅在Scene视图隐藏，运行activeSelf仍true，不改变探索/战斗显示。详见同日任务记录。

> 2026-09-13 零架势事务退出修复完成（优先于下方诊断记录）：统一新命中/提交校验为架势<=0，不再比较旧计时崩解状态；避免零架势重复写入造成无对应结果的Posture脏标记。新增敌我真实事务提交回归RED→GREEN，Battle V2全部14组通过，编译无错误，最近600秒Console error为空。正式F8入口方法启动后，通过模拟EventSystem鼠标点击连续行动，首批2敌退场、下一批4敌入场、候补8→4，随后敌我继续行动，LastError始终为空；未验证整场胜利或物理F8按键。探索HUD保持可见，原卡牌复位修复保留。详见tasks/2026-09-13-battle-v2-zero-posture-transaction-fix.md；任务complete/read-only、租约释放，已退出PlayMode，未保存场景。

> 最新用户纠正：探索需要保留战斗HUD，已撤销之前擅加的隐藏逻辑，保留卡牌修复，A4/A6通过。F8打数回合返回探索已复现为BUG：第二名敌人准备时命中标志exposedHit=True、状态exposedState=False，被DRPGCompleteActionLedgerCommit拒绝，BattleCompleted结果Aborted（己方全存活、5场上敌人+5候补）。本轮仅诊断，事务语义不一致仍待修复；见tasks/2026-09-13-battle-v2-exploration-visibility-correction.md，勿沿用旧“全部完成”结论。

> 2026-09-13后续用户复现补修（优先于旧完成记录）：未按F8的探索HUD残留与空卡移出后聚到原点确实仍存在。已让BattleHudBinding在Awake/Unbind隐藏、Bind显示HUD；卡牌只有持有动画位移时才恢复，快速重入不重记基线，Bind/Disable安全恢复。A4/A6新边界RED→GREEN，PlayMode探索隐藏/战斗显示/解绑隐藏以及四卡重复处理器回归通过；未宣称物理鼠标验收，未改场景/Prefab。任务complete/read-only、租约释放。

> 2026-09-13 21:40纠正Battle V2旧实机验收：正式Prefab的四技能与架势恢复Button目标Graphic曾被设为`raycastTarget=false`，导致真实鼠标完全无响应；卡牌又因`Awake`早于Layout排版而把四张回位基线都记成`(0,0)`。现已用Unity API恢复五个按钮射线，并让卡牌在真实PointerEnter捕获各自排版坐标。真实鼠标已完成技能→选敌→行动事务、架势恢复推进及四卡扫过复位；Battle V2 A1–A6/B1–B3/C1–C5全绿，编译0、Console error0。旧C5录像/“真实输入闭环”声明不作为证据，新证据在`artifacts/battle-v2/input-regression/`。任务complete/read-only、租约全释放；未保存测试场3，未commit/push/clean。

> 2026-09-13最新实施完成：`battle-ui-simplification-implementation` complete/read-only、全部租约释放。正式F8已接入固定四技能、体力豆、架势恢复、卡牌预览、冷铁箭头、世界状态/敌方意图、通用技能事务、整数面板/抗性与架势优先分层生命；职业初始四槽均已授予，行动宣告显示具体技能名而非“普通攻击”。Battle V2 A1–A6/B1–B3/C1–C5、基础设施59/59合同/25生产场景、F8实机闭环与Console error0全部通过。证据在`artifacts/battle-v2/C5/`。亲友、卡牌真实效果/库存、正式Spine、同律/武器技能最终入口仍属明确排除项。共享树高度dirty，未commit/push/clean，未保存测试场3。

> 2026-09-13最新：用户整体确认机制减法设计；writing-plans主计划及A UI/B技能路由/C数值三个子计划已完成，共14任务。入口`docs/superpowers/plans/2026-09-13-battle-ui-simplification.md`。先在现有F8交付新UI/模拟卡牌，再通用技能与原地恢复，最后真实体力和无上限架势数值。仅文档变更，运行代码/Unity资产尚未实施、未运行新测试；任务planning-complete/read-only，全部文档租约释放，无后台任务。以下待整体审阅记录为历史。

> 2026-09-13机制减法访谈收口（仅设计，待整体审阅）：Q32最终确认架势无玩法上限、不绑定HP；默认值仅每场初始化，恢复可超过默认值，自身回合不重置，UI无上限显示。此前Q20恢复封顶/Q26悬停上限推荐已被替代；体力豆仍有独立上限。Q31四格取显式出战位、Q33恢复不减攻击抗性已确认。设计审阅稿位于`docs/superpowers/specs/2026-09-13-battle-ui-simplification-design.md`，包括功能/不兼容接口分类、UI优先大节点与验收；未实施，等待整体批准后写实施计划。

> 2026-09-13机制减法第四轮（设计，未实施）：技能区固定4格，无分页/额外栏，架势恢复独立。同律/武器技能入口与去留未定，可能转卡牌或取消，不自建入口或擅自归档；此限制优先于早期同律保留方向。卡牌定义合法目标，箭头冷铁色克制外形、内部小红/绿块区分敌我，不采用卡通大红外形。其余Q23–30按推荐，详见设计规格；仅文档变更。

> 2026-09-13 Q19频率澄清（设计确认，未实施）：架势每场战斗开始设置为角色默认小整数，非每角色回合重置；默认10被打到2后，下次自身行动仍为2。HP跨战继承。默认架势与最大架势关系仍待确认；不能把“每场初始化”自动解释成回满最大值。UI参考正文与6张原图已完整读取，下一轮确认构图与交互。

> 2026-09-13机制减法第二轮确认（仅设计）：完整敌我角色行动各贡献一次恢复进度，按当时恢复率累计、自身回合入账，已有与待入账合计封顶；初版每场满豆。敌方体力与容量隐藏，容量独立于玩家豆显示上限，Boss可配置很高但仍走真实扣费规则。面板与对应属性抗性先各自整数化，最终差值非负、先架势再HP；三位数是内容目标而非隐藏截断。破盾有HP溢出才播受击，恰好清空仍播防御。详情见同日设计规格第二轮；未改代码/Unity资产。

> 2026-09-13新设计方向（尚未实施）：用户通过Grill with Docs确认UI优先、全攻击技能化与明示整数体力豆成本；架势为先于HP扣除的第二血层，取消崩解固有跳回合/增伤；架势恢复0豆耗行动且绝不进攻击特写。体力按敌我全局经过的角色回合累积、到自身回合入账，细分边界待访谈。同律未来取消选定单对象/固定队位/专属充能门槛，要求对应亲友在场存活及技能/体力条件；召回替换本轮不实施。以`docs/superpowers/specs/2026-09-13-battle-ui-simplification-design.md`区分确认与待定；旧代码与历史规则未自动迁移。参考实际为`Assets/随便尝试/废弃楼/UI参考.doc`，图文尚未读取。

> 2026-09-13 室内样板初版交付（优先于以下实施历史）：`lowpoly-interior-sample` complete/read-only，全部租约释放。独立Scene位于`Assets/随便尝试/废弃楼/3d版本/Scenes/SC_InteriorSample.unity`；六FBX、13Prefab、五最终截图和材质/资产文档已保存。实际PlayMode、编译、结构/作用域probe和关闭重开恢复PASS；最终回原测试场EditMode/dirty=false、Quality=null恢复原管线。源blend hash不变，原场景未保存，不改战斗/移动。此为低模制作样板，角色Unlit无动态阴影、窗光/裂缝仍简化，不代表最终美术或设备性能验收。目录被现有gitignore忽略，未改忽略规则；无后台任务。

> 2026-09-13 室内样板实施更新（优先于下一条初始暂停记录）：用户批准样板专属3D管线临时启用与退出恢复；`lowpoly-interior-sample`为唯一active/unity-writer，仅写样板目录和同步文档。六份源模型拆分、三张共享贴图、独立SC_InteriorSample已落盘；实际PlayMode场景/材质/静态比例尺探针通过，最近180秒无Console error。原blend hash未变，原测试场未保存；正在完成截图与真实管线恢复验收，不涉及战斗与移动。

> 2026-09-13 室内样板范围同步：用户批准独立低模3D横版环境样板，只在`Assets/随便尝试/废弃楼/3d版本/`制作，`fff`仅作比例尺，不处理移动或战斗。实时只读确认当前为Renderer2D，fff场景缩放约0.78，原测试场dirty=false；源blend合并mesh含40连通岛，门/柱/墙裙/灯具需按空间组核验拆分，不得按对象名误判不存在。本轮只写盘点/任务文档，未改模型、Scene、Prefab或渲染资产；等待专属3D渲染配置启用/恢复边界确认，`lowpoly-interior-sample` paused/read-only，文档租约已释放。战斗任务的新完成状态以登记表与其2026-09-13任务记录为准，以下9月9日条目为历史，不覆盖新记录。

- 最后更新：2026-09-09 16:05 +08:00
- 更新任务：`basic-attack-task11-finalization`

> 16:05最新（优先于下方历史）：Task13B关键资产已落盘并读回，Standard flag=true、批准timing非空、ActorMotionRoot+六anchor、独立acceptance party/encounter与场景launcher均已接。F8键已从普通AI入口切到既有scoped BasicAttackOnly入口；实际键盘F8读回Enabled/seed42/normal-hit，3笔玩家真实Button+target proxy和2笔敌方自动Standard行动完成，owner释放、LastError空、Console error0；Foundation121/121。正常入口另验证敌方X/压暗/回位与下一批3人原子部署，但新批普通AI会因“destination formation row is full”保留非法reposition而卡住，严格F8跨波尚待补测/修复。证据及全部hash见Task11 16:05和`artifacts/ankalute-2026-09-09-f8-cutover/`。Task13B窄installer/幂等probe、己方incap绑定、cleanup/完整反馈和Task14 25/59/人眼矩阵仍未完成；scene相对HEAD有超大fileID序列化diff，写前基线归因不明，不得擅自回退或再保存。writer已释放，无后台任务。

> 13:44最新（优先于下方历史）：已接世界角色单次闪白，与命中数字/音效同拍，.09秒衰减，Evade不闪，取消恢复原材质且不碰阴影/死亡dim；实际URP Sprite兼容，新50秒真实录像及抽帧已验证。正常原始AI实际6行动PASS（player3/enemy3、standard4/Legacy2、AudioSource4/worldFlash4）。退出PlayMode发现的音乐协程错误通过GameAudioRoot显式quit生命周期修复，最终实际退出无error；普通inactive检查单独不足以解决此边界。基础121/入口55/启动23/闪白与死亡回位等59专项通过，最后quit修改另跑基础121和真实6行动/退出。最终EditMode、compile无错误、Console0/25/23、SceneDirty=False、磁盘F8 flag=False。未改正式Scene/Prefab/SO、未commit/push/清dirty；writer全部释放，无后台任务。完整VFX/HitStop/震屏、永久清理/无环composition、Wave降级、失能/明确MotionRoot绑定及最终F8/矩阵仍未完成，不提高生产验收计数。详情Task11 13:44。

> 12:34最新：真实FlowRoot原始AI连续6行动PASS（player3/enemy3、standard4/Legacy2）；60秒录像见Task11 11:55。新平A同拍音效已接Surface真实sink，4次standard均检测到实际AudioSource播放；SkillId/单次结果及cue-local失败降级、后续反应隔离通过，不等于人工听感验收。Root丢失cleanup ack增加最多3次原owner重确认，永久失败仍fail-closed；FlowRoot两类观察者异常隔离实景通过。Legacy双方归零统一Defeat优先，真实Session RED→GREEN。最后Foundation121、TurnEntry55、Startup23及47项音效/Root/Stage9/Wave/SafeEvent专项PASS；EditMode、compile0、Console error0/warning16、SceneDirty实际False，未保存场景或正式资产。F8磁盘flag仍False，完整VFX/flash/hit-stop、失能绑定、无环composition/永久清理及最终生产矩阵仍未完成。writer已释放、无后台任务；接续详见Task11 12:34，不沿用历史“dirty=True”作当前事实。
- 项目根：`E:\WORLDCREATOR\new\Project_ANKALUTE`
- 当前阶段：大方向系统首轮打通；后续再按系统进行第二轮阅读、纠偏和精修

## 项目边界

> 2026-09-09 11:40连续实施更新：真实普通攻击按钮/取消/非空target proxy与FlowStateMachine敌方平A已通过，FlowRoot创建前冻结Standard策略、绑定后startup、BattleStarted先于actor已通过；磁盘配置仍关闭，非F8切换。Legacy Defend已通过唯一Inbox接续TurnEntry；Legacy Skill致死→整批Wave实景RED→GREEN，一敌一批及两敌部分清场均通过，禁止WholeWave模式单体补员。修复后基础121、入口55、startup23、Root/结算/Wave等36项回归通过。**目前正在用原始敌人build/正常AI连续验证，已发现并正在验证修复Legacy演出期间AI重复提交；不是全部正式验收通过。** 原SceneDirty保留、正式资产未改、同一active writer继续。详细接续见Task11记录。

> 2026-09-09 02:54正在连续实施：正式绑定已加入共享Wave factory/冻结Formation槽位/同池Sprite交接，显式CreateWaveSession已走通Startup→首批Wave→候选→连续5笔平A（enemy2、impact/AC各5），以及Root末敌退场→第二批（2→2，AC3）实际PlayMode验证。联测修复退场后Enemy.IsDeployed残留，56项窄回归通过。**仍测试basic-only与直接目标入口，F8保持Legacy；TurnEntry致死、故障、正式AI/UI等剩余继续active，不能宣称完整交付。**未改正式资产，原SceneDirty保留；writer仍由同一任务持有。详见Task11新记录；下面是历史节点。

> 2026-09-09 01:12最新：新增实际 `BattleWaveSpritePresentationPort`，复用宣告栏，冻结整批身份/槽位、同池暂存、DOTween横向OutCubic入场≤.5s总错峰、全员home+2render帧ready、normal cleanup后由外层原实例adopt。与真实Wave Run/Lifecycle/State/Inbox联测正常/故障通过；新9项含取消重入ack、就绪后角色丢失/home漂移保护，既有回归与Foundation121、Entry55、Startup23通过。实际同Session连续5笔平A再次PASS（3敌方，impact/AC各5）。**正式Session/PresentationRoot/代际port slot尚未绑定此port，冻结Formation供给、adoption与continuation调用点仍待接；F8仍Legacy，无新Wave录像/布局验收。**新测试用正式Sprite资源但手动时钟与独立投影fixture，不等于正式波次可玩交付。最终EditMode、SceneDirty=True保留、compile0、Console error0/warning42；无正式资产修改，writer释放，无后台任务。详情见Task11记录01:10/01:12。

> 2026-09-09 00:49最新：Wave角色池新增同池暂存scope，只有该pool当前Wave projection可创建；暂存view不进active池，Collider2D禁用；所有已stage演员均规则部署后才能原实例整体adopt，取消/投影关闭回收。实际绑定回调中取消导致回收后重激活、碰撞体提前启用两例RED→GREEN。新9项含正式Sprite prefab/profile身份和位姿读回通过，既有projection/Wave/Root/Stage9及Foundation121、Entry55、Startup23通过；实际Sprite同Session连续5笔再次PASS（3敌方、impact/AC各5）。**这是暂存交接组件，未接完整Sprite Wave port/Session driver/adoption调用点，没有新Wave动画或录像，F8仍Legacy。**最终EditMode、测试场3 SceneDirty=True原样保留、compile0、Console error0/warning42；未改正式资产，writer释放、无后台任务。详见Task11记录00:49。

> 2026-09-09 00:15最新：新增Wave演出协议Run，按宣告时长+render帧→stage→aggregate及全member ready→normal cleanup确认→原子部署一次Inbox提交；复用watchdog并在port pump前处理超时，晚ready/晚cleanup不能抢先提交，取消走撤回确认且不代外层释放rule lease。受控port+真实State联测6项及既有回归PASS，Foundation121/Entry55/Startup23通过。**仍未接正式Sprite Wave port或Session driver，无新Wave画面，F8仍Legacy；超时degraded/placeholder及永久cleanup恢复仍缺。**本轮无PlayMode/录像，00:02是最后实际连续5笔证据。最终EditMode、SceneDirty=True保持、compile0、Console error0/warning16；未改正式资产，writer释放、无后台任务。详见Task11记录00:15。

> 2026-09-09 00:02最新：Composition.Build已能在显式wavePlan下持有唯一Wave规则Lifecycle，将分批计划→全ready原子部署→一次Inbox完成→exact typed全退场→下一批连起来；真实State 2+1部署/仅清空规则列表仍拒绝/party defeat优先/取消迟到回调4项通过。Entry55、Startup23、基础121及Wave/Root/Stage9回归通过；实际Sprite连续5笔再次PASS。**这是dormant规则组合，不是完整Wave presentation Module或正式入口：宣告/入场/placeholder/watchdog/cleanup、Legacy前驱、startup Wave1和Factory/F8仍未接。**F8仍Legacy，没有新录像；最终EditMode、SceneDirty=True保留、compile0、Console error0/warning42、host0；未改正式资产，writer释放、无后台任务。详情见Task11文件00:00/00:02。

> 2026-09-08 23:42最新：TurnEntry completion新增最终提交后的immutable revision并纳入fingerprint，已覆盖eligible消费scheduler与skip重新排程；Wave退场ledger现在也接受真实TurnEntry→Inbox封存记录，旧revision/重复/同批存活成员保护通过。真实TurnStart中毒致死模块集成RED→GREEN；候选/TurnEntry55、Startup23、Foundation121及Wave/Root/Stage9相关回归PASS。**仍非实际Wave入场/F8交付：WaveModule/port/composition/Legacy precursor未接，TurnEntry空敌场保护仍保留，F8仍Legacy。**本轮无PlayMode或新录像，最后实景证据仍22:54。最终EditMode、SceneDirty=True保留、compile0、Console error0/warning16；未改正式资产，writer已释放、无后台任务。详见Task11记录23:42。

> 2026-09-08 22:54最新：Task12补Wave immutable completion→唯一Inbox→后续Pump原子TurnEntry转交；真实State整批ready提交时封存completion，不从live状态重建；新增本批exact authored→runtime退场集合（当前只支持已封存Root source，尚未装入Wave生命周期）。新增Wave continuation7与既有wave11、Inbox/Root/Stage9等回归通过；Entry54、Startup23、Foundation121/121 PASS。真实Sprite同Session连续5笔再次PASS（3敌方，impact/AC各5）。**完整Wave Module/实际入场/TurnEntry和Legacy wave precursor/初始波接线仍未完成，F8仍Legacy；这是核心接续基础，不是生产波次交付。**最终EditMode、原SceneDirty=True保留、compile0、Console error0/warning42、formal host0；未改正式资产、无新录像、全部writer释放、无后台任务。详细证据和下一步见Task11任务22:53/22:54记录。

> 2026-09-08 22:23最新：Task11补partial candidate scope失败后的引用/清理保留，entry54通过；Task12新增确定性WavePlan/versioned只读旧配置迁移、必填BattleSeed与真实State原子部署ready门，wave6+5通过，两次可逆行为mutation准确失败后恢复GREEN。Foundation121和Root/Stage9等回归通过，实际同Session连续5笔再次PASS。**这些是Task12核心primitive，尚无完整Wave module/typed清场集合/实际入场/跨模块接续；F8仍Legacy，完整反馈、失能、Task11收尾和Task13/14仍未完成。**用户明确录像不是最终效果标准，不能因此跳过原规划。最终EditMode、SceneDirty=True保留、compile0、Console error0/warning42、host0；未保存正式资产，无新录像，全部writer释放、无后台任务。详见任务22:20/22:23记录。

> 2026-09-08 21:09最新：已修复实景Root阶段9完成后、continuation校验拒绝造成的永久持锁（真实RED→GREEN）。保留原lease等待cleanup，Session封技术终态后释放并一次BF；不重算HP或改已封存完成记录。实景接续前/后故障均PASS（impact1；AC分别0/1；BE/next actor0；BF/host Aborted1），正常连续5笔再次PASS。Foundation121、entry52、startup23及Finalization8/Root11/Settlement8/SafeEvent3/Standard1通过。最终EditMode、SceneDirty=True保留、host0、Console error0/warning96。**仍非F8验收：Task11永久cleanup失败恢复与无环composition等边界、Task12整批Wave、Task13正式AI/UI/F8及己方独立失能绑定待做；F8仍Legacy。**未改正式资产，无新录像；Task11 paused/read-only、全部writer释放、无后台任务。详细证据/hash见任务21:09记录；下方为历史。

> 2026-09-08 20:47最新：Startup/TurnEntry技术fault已补清理后独立BattleFaulted→FlowStateMachine host Aborted；延迟ack、交接后factory失败、scope清理抛错重试和观察者/restart重入4项RED→GREEN。正式Sprite入场port丢失/首候选actor丢失两例通过，AC/BE/actor发布均0、BF/host各1。连续5笔再次通过；Foundation121、candidate/entry52、startup23、finalization/Root/settlement/safe-event回归通过。EditMode、SceneDirty=True保留、probe空、编译无错误、真实Console error=0。**Task11仍未全部完成：Root完成后continuation拒绝、永久cleanup失败恢复、无环对象图待做；己方失能资源/绑定、整批Wave、正式AI/UI/F8未接，F8仍Legacy。**本轮未改正式美术/资产或保存Scene，未借Hit/Broken图充当独立失能资源；详细下一步和hash见Task11记录20:47。全部writer释放，无后台工作；下方为历史。

> 2026-09-08 19:52最新：Root故障已封Faulted并等待cleanup确认再释放projection/rule；Session独立BattleFaulted，实际FlowStateMachine一次Aborted交回host，不伪造领域BattleEnded。真实演员丢失命中前/后、抛错/重入观察者通过。正式Prefab缺失X引用已增量补齐，真实敌方致死完成X/dim→原演员回Home→退场→己方中毒/再生→Victory；最终录像`C:/Users/Administrator/Documents/Codex/2026-09-02/new-chat-2/artifacts/ankalute-2026-09-08-defeat/05-enemy-defeat-readable-x.mp4`已抽帧核验。连续5笔、Foundation121/入口48/启动23/Finalization7等通过；编译0/0、Console无error、EditMode、probe空。**Task11未全部完成：非Root故障/cleanup拒绝或丢失ack的恢复/无环composition待收口；己方失能模型、整批Wave及正式F8/AI/UI未接，F8仍Legacy。**只保存X增量Prefab，SceneDirty=True保留未保存、不清脏树。Task11 paused/read-only、全部租约释放、无后台任务。详见任务19:47/19:52；下方为历史。

> 2026-09-08 19:02最新平A节点：Root最后Stage9屏障→关闭projection→唯一typed Inbox→后续Pump完成通知并转交TurnEntry已接；同一个显式Standard Session真实资源连续5笔平A（3笔敌方），impact=5、ActionCompleted=5。录像在`C:/Users/Administrator/Documents/Codex/2026-09-02/new-chat-2/artifacts/ankalute-2026-09-08-continuation/03-five-consecutive-actions.mp4`。真实Abort-on-completion及观察者异常隔离通过；基础121/候选入口48/启动接续23及新专项通过，最终EditMode、SceneDirty=False、编译0/0、Console无error、probe host空。**F8仍Legacy，测试直接提交目标且enemy使用显式测试basic-only构筑；不等于正式AI/UI验收。真实致死因Defeat marker/world dim绑定缺失失败；Task11故障收尾、死亡资源、整批增援与正式入口仍待实施。**未保存正式资产、未清理脏树。Task11 paused/read-only、全部租约释放、无后台工作，详情见对应任务19:02记录。下方15:33及更早条目为历史。

> 2026-09-08最新平A节点：同Session Startup→首TurnEntry、延迟Abort及正式PresentationRoot显式Prepared绑定已实现；正式战斗Prefab增量绑定宣告栏与Sprite演出。已使用真实F8队伍/敌人/背景录得己方3–9、敌方3–9及己方中毒→己方再生→敌方中毒的顺序结算。短片位于`C:/Users/Administrator/Documents/Codex/2026-09-02/new-chat-2/artifacts/ankalute-2026-09-08/`。基础121、入口48、启动接续23、新adapter2项通过。**F8仍Legacy；最终完成/下一事务、整批增援与正式失能资产接线仍未完成。** 实景动作由集成测试驱动，不是UI输入验收；不以此计算总完成率。已达用户允许的可见验证节点并释放writer，无后台任务，场景未保存修改。详细证据/剩余项见Task10D任务文档15:33记录。

- 本项目是 **异界地宫 / ANKALUTE**。
- `E:\WORLDCREATOR\Project_AMIAS` 只作为冻结的旧版回合制参考，不得修改。
- `E:\WORLDCREATOR\new\Project_VELGITA` 是异度结弦，不得因 ANKALUTE 任务而顺手修改。
- 长期架构、主角规则、Break 规则和废弃资产规则以 `$project-ankalute-memory` 为准。

## 当前代码版图

- 2026-09-08 13:45：Task10D接通同Session的TurnEntry BattleResult/Defeat交付：source精确一次性terminal→Inbox下一Pump→清表现scope→Session.CompleteBattle→通知结束释放lease；不生成ActionCompleted。修复真实BattleEnded回调清空Session后Flow.Update空引用。43专项、Startup23、Foundation121通过；强化Console异常即FAIL后的Flow实帧玩家/敌方/延迟战败3例及原9例通过。修正测试导演未绑定的两条异常，不把首次PASS当无错误；最终EditMode/测试场3/SceneDirty=False、编译0/0、Console无error。**Startup/PresentationRoot首次创建和factory绑定、关闭生命周期、Root最终化、整批wave及正式F8仍未接，F8仍Legacy。**未保存正式资产或清理脏树，Task10D paused/read-only、全部租约释放，无后台工作；接续以任务记录13:44–13:45为准，不重做已完模块。

- 2026-09-08 13:04：Task10D实际Flow.Update已驱动TurnEntry module→唯一Inbox→下一Pump→Session普通actor/token发布，回调结束前domain input保持关闭；补非平A选择入口的锁检查。旧/新候选准备共用helper，敌方Windup/Scheduled只准备不触发TurnStart，同time先处理准备；SelectAgain自动原子转移至新候选并通过factory创建后续module。新增真正Sprite projection/adapter factory及scope清理；42专项、Startup23、Foundation121通过，新增Flow/Session两侧实帧2例和旧TurnEntry9例通过，最终编译0/0、EditMode/测试场3/SceneDirty=False、Console无error。**正式Startup/PresentationRoot首次创建与factory绑定、BattleResult接收及关闭生命周期仍未接；Root最终化/整批wave/正式F8仍待做，F8仍Legacy。**本轮未保存正式资产或清理脏树，Task10D paused/read-only、全部租约释放，无后台工作；准确下一接续见任务记录13:04，不将专项数量当总体完成率。

- 2026-09-08 12:25：Task10D普通玩家/敌方skip及死候选续选接通到source terminal；Session与source共享pending时间线效果，敌方skip复用原AI准备/换位/Wait策略，成功后一次重排程。typed TurnEntry完成进入唯一Inbox/Router：重复幂等、原physical lease校验、下一Pump转交，SelectAgain期间无输入间隙。35专项、原Startup23及Foundation121通过，编译0/0，EditMode/测试场3/SceneDirty=False，缓存Console无error。**正式Composition/Session/Flow接收端及自动Tick/Submit尚未接；旧计划准备/玩家响应语义仍需迁移，空敌场wave/outcome上下文仍待做。F8保持Legacy，不能将纯模块分发通过当正式入口完成。**Task10D paused/read-only、全部租约释放，无后台任务；详情见Task10D记录12:25。

- 2026-09-08 11:39：Task10D新增部分候选terminal并纠正中断代码的旧队首消费/敌方计划丢失。正常候选一次签发token；真实TurnStart把候选time10推至35后仍消费原actor、logical time保持10并递增revision；敌方转Executing保留计划/目标；己方全灭优先Defeat（含互灭）；Uninterruptible强制skip保留原计划并SelectAgain。candidate/module30项及Foundation121通过，编译0/0，EditMode/测试场3/SceneDirty=False，MCP缓存Console无error。**普通skip重排程、其他队友存活时的死候选续选、wave/outcome上下文、typed inbox/Session.CurrentActor及正式对象图/input接线未完成；空敌场目前保护性拒绝且保持锁，不能接F8。F8仍Legacy。**项目Skill已1.0.4，MCP v0.6.7实际连接ANKALUTE；Project_ANOIR是配置的Product name。任务paused/read-only、全部租约释放，无后台工作。详情见Task10D记录。本轮不增加production25矩阵或总体完成率。

- 2026-09-07 15:59：新增仅Editor的一键观看入口 `Tools/ANKALUTE/DRPG/Basic Attack/Watch Current Visual Demo`，不替换F8、不保存正式资产。它自动进入Play并串行播放当前TurnEntry 9例、Stage9/Root 14例、命中/反应/死亡/双方回位34例；状态查询与停止菜单同组。2427先RED→2432 GREEN，2433–2437真实菜单整链57例全部PASS且SceneDirty=False；2440 EditMode、2441编译0/0、2442 Console无error。该入口是当前成果演示，不代表正式Session/Flow/F8接线完成；Task10D剩余不变。任务complete/read-only、全部租约释放，细节见`tasks/2026-09-07-basic-attack-visual-demo.md`。

- 2026-09-07 15:35：Task10D候选收尾预检已接到真实module最终屏障。State-owned source仅在原State/physical lease/exact reservation/有效plan pin、真实formation完成及live scope/revision/clock仍有效时冻结最终候选事实；保留原候选与结算后时间线，不改CurrentActor、不消费候选或放开输入。入口17项（含5项新增先RED后GREEN）、原candidate8、Foundation121/Motion7与shared/startup/Root回归通过；强化后的9个真实Sprite帧改用State真正reserve并验证逐帧禁止早取收尾facts，全部PASS。**仍仅preflight：outcome/skip重排程/唯一consume-discard/typed completion/actor-token publication及Composition/Startup/Flow/domain输入未接；F8仍Legacy。**manifest42/59不变，registered矩阵本节点未重跑。2420最终EditMode/SceneDirty=False/runner空，2418编译0/0；**2419播放退出新增inactive GameFlowManager上的StopMusic协程错误，音频退出清理未修，不能宣称Console无error**。详见Task10D记录15:35与4源码hash。全部租约释放、paused/read-only、无后台工作；519脏项保留，无正式资产保存/提交/清理，Sprite不等Spine。

- 2026-09-07 14:56：Task10D候选source迁移节点完成。State-owned `DRPGTurnCandidateAuthority`为旧Session和新TurnEntry提供同一reservation/plan表，纯reserve、exact reservation与plan引用、原physical lease pin；已锁候选不能被旧接口消费/丢弃，plan被替换后规则提交与演出停止并保留owner。8专项RED→GREEN，入口/Root/shared/startup回归及Foundation121/Motion7通过，新adapter9、旧Stage9/Root/widgets14、原34真实帧通过；固定manifest42/59不变，registered矩阵本轮未重跑，不增加上次5/25计数。**仅source/pin完成；新候选terminal（consume/discard/skip重排程/plan refresh）、outcome/typed completion/actor-token publication及Composition/Startup/Flow/domain输入仍待实施，不能提前将未完成的pin生命周期用于正式F8；F8仍Legacy。**Task11–14未完成。2364–2367最终正确项目/测试场3、EditMode、SceneDirty=False、编译0/0、真实Console无error、runner Phase空。精确5源码hash与接续见Task10D记录14:56；全部租约释放，paused/read-only，无后台工作；519脏项保留，无正式资产保存/提交/清理，Sprite不等Spine。以下为历史。

- 2026-09-07 14:21：Task10D生产TurnEntry表现适配节点完成。新`BattleTurnEntryPresentationAdapter`从冻结合法Home捕获顺序，复用真实状态cursor/TMP/HUD、独立生命周期提示(.45秒+新帧)、typed death及整体formation，修正生命周期插入后的final ordinal；运动随projection创建。9专项检查及9个正式adapter Sprite实帧通过，原Stage9/Root/widgets14与原34实帧、旧规则/候选/Startup回归、Foundation121/Motion7全通过；固定manifest42/59、registered5 Passed/20 Pending/0 Failed不变。**adapter尚未绑定正式Session/Composition/Flow，候选source/exact enemy plan pin/skip重排程、outcome/typed terminal/consume-discard/actor-token publication和单domain输入仍待做；F8仍Legacy。**不要重做已完成module/adapter；接续及8源码hash见Task10D记录14:21。最终EditMode、SceneDirty=False、编译0/0、真实Console无error，无后台工作；全部租约释放，Task10D paused/read-only。519折叠脏项保留，无正式资产保存/提交/清理，Sprite方案不等待Spine。以下条目为历史状态。

- 2026-09-07 13:33：Task10D候选原子生命周期及入口模块规则/演出屏障节点完成。`State.TryCommitTurnEntry`一次提交清Defend→TurnStart闭包→PostureBreak skip/recovery/regen与logical-time；新`DRPGBattleTurnEntryModule`由后续Tick驱动serial→必要lifecycle(.45s)→typed death→共用reconcile→formation(.30s+render fence)，只到ReadyForFinalization，原owner/input/reservation仍锁定，无ActionId/ActionCompleted。20新检查和5个新module真实Sprite帧案例通过，旧规则/Foundation121/Motion7及7/14/34实帧通过；manifest仍42/59，registered5 Passed/20 Pending/0 Failed。**仍待生产TurnEntry port/lifecycle widget与ordinal适配、candidate source/exact enemy plan pin/skip重排程、outcome/typed terminal/正式actor publication、Composition/Startup/Flow/input；F8仍Legacy。**不能从未消费reservation直接peek同候选。2218–2221正确项目/测试场3，EditMode、SceneDirty=False、编译0/0、真实Console无error、runner Phase空。13源码/meta/4文档租约释放，Task10D paused/read-only，无后台工作；519折叠脏项保留，不保存正式资产/提交/清理。最新接续/hash见任务记录13:33/13:30，优先于下方历史。

- 2026-09-07 12:43：Task10D共享死亡规则与整体重排节点完成。State-owned唯一post-settlement reconcile只接受本State已提交ledger+原始physical lease+typed rendered departure，一次移除死亡候选/整体压缩阵型；Root委托同一store。TurnEntry共享冻结journal、full-owner motion/layout及至少.30秒同批平滑重排+新Home渲染门。新增13检查、7实帧通过；原34实帧、Stage9/Root/控件14实帧与Foundation121/Motion7通过；固定manifest仍42/59，registered5 Passed/20 Pending/0 Failed。**TurnEntry module、候选Defend/PostureBreak/一次logical-time、正式候选发布/结果续流及Session/Flow/输入仍未接；F8仍Legacy。**2103–2106实读EditMode、SceneDirty=False、编译0/0、真实Console无error、runner Phase空。10脚本/meta及全部租约释放，Task10D paused/read-only，无后台工作；519折叠脏项保留，无场景/资产保存或commit/push/清理。最新接续/hash见任务记录12:43，优先于下方历史。

- 2026-09-07 09:26：Task10D新增共享TurnStart ledger/因果闭包、串行receipt和真实Sprite/HP/状态/死亡primitive，完整TurnEntry owner、不借ActionId；非空逐项≥.45秒、空队列≥.30秒+render fence，死者保留原view/X/dim，新HomeRendered后才失能或敌方release。新增23检查2000、原基础23检查2001、Foundation121/Motion7/manifest42/59通过；实际原34帧案例2011、Stage9/Root/控件14案例2013、新TurnEntry5案例2015通过；注册矩阵2017仍5 Passed/20 Pending/0 Failed。**共享primitive已完成，但TurnEntry module、死亡后的规则/整体formation、候选Defend/PostureBreak/时间线生命周期、Session/FlowRoot/唯一输入接线未完成，F8仍Legacy；不可把这些测试换算为总体完成率。**2018–2021实际EditMode、编译0/0、真实Console无error、SceneDirty=False、runner Phase空；无场景/资产保存、无commit/push/清dirty，519折叠脏项保留。Task10D paused/read-only，13脚本/meta/文档租约全部释放，无后台工作；精确接续与hash见任务记录09:26节点，优先于下方08:17历史说明。

- 2026-09-07 08:17：Task10D基础节点新增候选/正式turn共用单调状态边界，身份分配与matching lease激活分离；typed completion inbox/Pump不inline启动下一runner、校验精确lease、防重复/失权/重入；Startup独立模块Prepare→Enqueue→后续Tick签发entry，取消等待已创建handle清理后才释放。23项新检查与Foundation121/Motion7/manifest42/59通过，最终原34真实帧1901、新14真实帧1903、registered5 Passed/20 Pending/0 Failed1905通过。**新Startup/TurnEntry尚未接Session/FlowRoot，TurnStart可复用结算与真实演出、单输入权威、Task11–14仍未完成，F8仍Legacy。**1906–1909实际EditMode、SceneDirty=False、编译0/0、真实Console无error、runner Phase空；无保存/清dirty/commit/push，519项脏树保留。Task10D paused/read-only，9脚本及所有租约释放，无后台测试；详见任务记录08:17交接与精确下一接线点。Task10C已complete，不能把23项基础测试或42/59换算成总体完成率。

- 2026-09-07 07:48：Task10C代码与回归完成。Root自动Stage3→9并停ReadyForFinalization；三种HUD/world状态控件按冻结cursor更新，延迟canonical/LateUpdate不泄漏最终值；武器损坏的HP/架势夹取在最后可见结果投影。1809固定42/59、Motion7、Foundation121通过；1817注册production5 Passed /20 Pending /0 Failed；1780原34真实帧、1822新14真实帧通过。可逆断线mutation1802准确失败后已恢复且GREEN。1824–1827实读EditMode、SceneDirty=False、无编译error/warning、当前缓存无error、runner Phase为空，未保存/清dirty。共享树519折叠项（130M/20D/369??），未commit/push/归档。Task10C租约释放，主代理继续Task10D startup/TurnEntry/单输入权威；Task11最终推进、Task12波次、Task13正式模板/F8/完整反馈、Task14整体验收仍未完成。正式流程完成率不能用42/59或5/25代替；Sprite正常无需Spine资源。

- 2026-09-07 03:00：Root已自动串联Stage3→9：Stage8完成后冻结当前Home顺序、一次结算、Player→Enemy串行结果、新死亡可见/退场及最终formation，停在ReadyForFinalization，原actor/输入/Root锁仍持有。1688新Root7项、1683原Root11项、1689十组回归、1690Foundation121/Motion7通过；1695补充真实帧12格通过（8项Stage9+4项敌我/普通致死完整Root3→9）。1697确认EditMode、不编译；未保存场景或清dirty。状态控件/部分资源投影、Task10D/11/12/13/14与F8切换仍未完成；正式矩阵本组尚未重跑，不据此提高完成计数。唯一unity-writer继续本任务；下面02:39及更早停点为历史。

- 2026-09-07 02:39：Task10C真实Stage9 Adapter已具备逐项结果（.45秒、空阶段.30秒）及独立新死亡HomeRendered→ally失能/enemy退场→一次规则/视觉formation。旧Stage8 permit不能授权Stage9新死亡；最新Home替代旧LockBaseline检查；死亡屏障超时不能Degraded通过。12项专项、3项serial、8格补充真实Stage9帧、原34格真实帧与Foundation121/Motion7通过。**Root尚未自动接Stage9，当前继续接线；完整状态控件、资源投影、最终化/波次/F8仍待做。**正式矩阵计数不据此增加，F8仍Legacy。1646正确项目/测试场3、EditMode/SceneDirty=True，未保存或清dirty；当前唯一unity-writer仍本任务，详细证据见Task10C记录02:39。

- 2026-09-07 01:31：Task10C新增只读PresentationPlan及串行receipt sequence（可见项>=0.45秒/空阶段>=0.30秒，跨渲染门、完整身份、watchdog、重入/失权/清理异常保护），完成后仅ReadyForDefeatFinalization；投影baseline在ViewModel生成时冻结完整HP/架势/状态实例，逐项before匹配，旧HP-only更新保留语义。新增10+6项通过。注册回归曾真实复现宣告0.994秒但仅1帧；TDD修复为>=0.75秒且>=2渲染帧，敌我同规则，不弱化测试；相关fixture只对齐模拟帧输入。最终Root11、Prelude17、Motion7、Foundation121、补充34真实帧通过，manifest仍38/59，registered4 Passed/21 Pending/0 Failed。**真实Stage9 Presenter/UI、Stage7状态/架势cursor衔接、新死亡最终屏障及Root pump仍未完成；Adapter.PresentSettlement仍Unavailable，F8仍Legacy**。仅有冻结元数据不等于可见状态图标已完成。1563最终EditMode/SceneDirty=False/无pending runner，1561编译0/0、1562当前Console无error，未保存/清dirty，不关闭历史DOTween退出风险。Task10C paused/read-only，13脚本及全部租约释放；细节、hash、直接续做入口见任务文末01:31交接。Sprite正常，不等Spine资源。

- 2026-09-06 23:56：Task10C因果规则节点完成：status roots的Immediate/OnDefeat在同一新workspace按父先子后完整求值，Player→Enemy lane不被跨side child重开；真实participant sides另冻结。Stage7→9共享passive/parent去重、事件序号与32步/8层预算，晚期错误整笔丢弃且retry不消耗预算；child typed事实进入fingerprint。实际PreparedRuleAction新增仅post-return允许的TryCommitSettlement，保留Stage7封存continuation、不重复primary/costs/events。新增13项、可逆丢失passive继承mutation、原Settlement14、Reaction13、状态RunAll、资源整组、Root11、Foundation121、最终源码真实帧34通过；manifest38/59及registered4 Passed/21 Pending/0 Failed不变。**Root pump尚未调用新结算seam；可见cursor、逐项receipts、新死亡最终屏障、Task10D/11/12/13/14仍未完成，F8仍Legacy**。最终1444 EditMode/SceneDirty=False、1445编译0/0、1446当前Console无error；未保存/清dirty。Task10C paused/read-only，全部租约释放，7脚本hash及下一大节点见任务记录。

- 2026-09-06 23:07：Task10C普通status roots结算规则组完成：实例消费随HP/Posture一次State journal提交；Player→Enemy、真实owner HomeSlot/timing/priority/instance稳定排序，stack合并、跳过项不扣duration、普通治疗不复活、显式source死亡后持续配置冻结；ActionIdentity缓存保证重复提交不增加revision。14项新检查及lane反转mutation、状态13项、Root11/11、Foundation121/121、补充真实帧34/34通过，manifest38/59与registered production4/25不变。**Immediate/OnDefeat因果children、Stage9可见cursor/receipts/死亡最终屏障和Root接线仍未完成**；F8仍Legacy，不宣称完整阶段9或总任务完成。1357最终EditMode/SceneDirty=False（1300本轮启动实读即False，未保存/清dirty），1358编译0/0、1359当前Console无error。Task10C paused/read-only，全部租约释放；10文件hash与承接接口见`tasks/2026-09-06-basic-attack-task10c-settlement.md`。

- 2026-09-06 22:23：Task10B状态实例/触发边界组完成。不同来源独立实例，同源叠层保持一项；Root应用冻结TurnSerial边界，普通新状态等待下一匹配边界；duration按实例单次消费；旧快照/删空高水位保留，source/身份/边界纳入State校验和ledger指纹。13项状态检查（含6格身份防伪及可逆mutation）、manifest38/59、Foundation121/121、Root11/11、补充真实帧34/34通过；registered production仍4/25。阶段9ledger/cursor尚未接，F8仍Legacy；Sprite正常不依赖Spine。最终EditMode/SceneDirty=True继承未保存，编译0/0、当前Console无error；全部Task10B租约释放，下一独立组Task10C。详情及14文件hash见`tasks/2026-09-06-basic-attack-task10b-status-identity.md`。

- 2026-09-06 21:39：Task10A显式headless测试组合前置组完成，31 foundation脚本57处及NineStage9处构造迁移；test-only immediate port不参与真实节拍，生产constructor无fallback。新增固定contract与编译依赖防回退审计通过，manifest37/59，Foundation121/121、Root11/11、真实帧34/34、注册production4/25通过。阶段9状态/ledger/可见队列未实施，Root仍ReadyForSettlement，F8仍Legacy；Sprite正常继续，无Spine资源阻塞。最终EditMode/SceneDirty=True（继承基线未保存），编译0/0、当前Console无error。全部Task10A路径/meta/文档租约释放，下一组10B；详细hash见独立Task10A记录。

- 2026-09-06 21:04：Sprite新事务根已串联Stage3 prepare/投影锁→宣告/构图/前摇→一次规则commit→Stage7队列→Return/HomeRendered→typed departure→规则/表现formation，停在ReadyForSettlement并保留actor/Root锁。新增11项、补充34格真实帧、Foundation121/121通过；manifest36/59、注册production4/25不变。阶段9/完整反馈/正式反应策略与F8接线仍待做；Spine模型不属当前阻塞。1194最终EditMode/SceneDirty=True（本轮临时fixture清理Undo标脏，未保存正式场景；不得自动保存/宣称dirty=false），1195编译0/0，1196当前Console无error。全部本轮租约已释放，详情/hash见任务记录最新节点。

- 2026-09-06 20:19：Sprite/环境恢复组完成补充验证。全场survivor姿态/颜色/alpha、未位移旁观者和CanvasGroup/背景同批恢复；败退保留X/dim到HomeRendered。修复不同尺寸/pivot静态攻击图在回位中脚底下沉/放大的渲染级问题，私有Sprite mesh归一并保留原UV，不改原资产。新8项、补充真实帧30/30、Foundation121/121通过；manifest36/59、注册production4/25仍保持。1123 EditMode/SceneDirty=False、1124编译0/0、1125当前Console无error。Spine恢复/完整反馈、Root串联、阶段9和正式F8接线仍待完成，不宣称Task8/9整体完成。全部本轮租约释放，详情/hash见任务记录2026-09-06 20:19。

- 2026-09-06 18:57：第三方Counter/Pursuit来源可在当前ledger步骤加入冲突，四人+camera同批回位；反应先>=0.12秒/渲染帧紧凑名准备，再独立>=0.45秒结果，增加无缩放横向Dodge。新增5项检查通过，补充真实帧矩阵26/26通过（含2格第三方回位）；Foundation121/121，manifest仍36/59、注册production4/25，无新失败。最终EditMode/SceneDirty=False、编译0/0、当前Console无error。正式Root/F8未切换，完整renderer/pose/UI/environment恢复、VFX/SFX/flash/hit-stop/反应姿态、Root串联和阶段9仍待完成。本组交接paused/read-only、全部本轮租约释放；详情和hash见任务记录18:57。

- 2026-09-06 18:40：Task9真实adapter增加冻结全场布局与独立formation表现屏障，typed departure完成后全体survivor同批>=0.30秒平滑重排、跨渲染门并更新当前Home checkpoint；失能anchor已禁止挂active actor子树。Formation4、DefeatTransfer9及补充24格真实帧主链（含3+3）通过。manifest仍36/59、注册production4/25，Foundation121/121，编译0 error/warning；退出后EditMode、SceneDirty=False、当前Console无error。Task8/9整组未完成，Root仍Prelude ReadyForImpact、F8仍Legacy；第三方反应位移、完整反馈/恢复、Root串联和阶段9/后续资产接线仍待做。阶段性交接为paused/read-only，全部租约释放，详见任务记录最新节点。早先“继续持有unity-writer”记录仅为当时历史。

- 2026-09-06 18:05：Task9已接真实adapter的HomeRendered之后ally独立失能模型/X移交、enemy精确View permit释放，以及独立规则退场名单/原子candidate removal+occupied-row compaction。7项规则检查+8项交接检查、补充12格真实帧完整命中/回位/退场/规则重排链通过；完整manifest36/59和Foundation121/121仍通过。整体world formation动画屏障、Root完整接线、正式失能/X资产与F8迁移尚未做，Task8/9不标完成。大节点继续，详见任务记录。

- 2026-09-06 17:28：Task9新增双方+camera同批平滑回位、HP0保留X/dim/原演员、Home后至少一个真实EndOfFrame的死亡可见门。12格真实帧命中/回位镜像（含致死）通过；注册manifest仍36/59、production4/25，Foundation121/121最近通过。尚无失能transfer/enemy departure/formation完整链，Root仍停Prelude，F8仍ProductionLegacy。详情见任务记录；同一Task7→8→9大节点继续，不是交付。

- 2026-09-06 16:52：Task7投影保护已整组通过；Task8新增真实ledger驱动的主命中/反应串行表现，≥0.45秒/项，同批HP/world数字/横向recoil、敌我命中/防御/闪避镜像、非命中治疗cursor、关闭立即清理自有label均通过。808整组contracts+运动7项+Foundation121/121通过，manifest36/59，production仍4/25；Unity EditMode、场景不脏。此为在途检查点，不是Task8/9大节点完成：Root尚未越过Prelude接rule commit，完整VFX/SFX/flash/hit-stop/逐write parity和Task9死亡/回位屏障仍未完成；F8仍ProductionLegacy。主代理继续唯一unity-writer。

- 当前已按用户要求连续完成 Task6A～6D 大节点：宣告≥0.75秒、构图协议/渲染门、前摇≥0.30秒/真实播放身份、限定事务的超时恢复，以及 Director 叶节点隔离。17项窄检查与Foundation121/121通过；manifest完成32/59，真实production场景通过3/25（其余22待实施）。下一大节点从Task7表现投影租约开始，衔接Task8命中/运动和Task9死亡/回位；不再每个小切片交接。F8仍为Legacy，九阶段最终视觉尚未验收。
- 当前场景退出风险：2026-09-05 19:43 production矩阵退出后出现“Some objects were not cleaned up … [DOTween]”；随后不运行任何Task6场景、临时Task6对象为0的空PlayMode周期也复现（日志计数4→5）。具体退出组件尚未定位，未做全局Tween清理或场景改动，不得宣称Console全干净或全量smoke已通过。

以下是当前工作树中已经存在的主要方向，不代表每个方向都已完成或通过验收：

- DRPG 主体：`Assets/Script/GameFlowManager/DRPGSystem/`
- ANKALUTE 顶层/Strategic Hub：`Assets/Script/GameFlowManager/AnkaluteGameLoop/`（纯核心、运行时协调器和探索阶段门禁已验证并接线；`DRPGGameLoopRuntimeRoot` 是唯一 Unity adapter / legacy facade）。
- 已出现的子域：配置、战斗核心、回合调度、表现层、交互战斗、编队、状态/反应、游戏循环、结算、网格背包、装备、成长和编辑器测试探针。
- DRPG 资源：`Assets/GameResources/DRPG/`
- 总控菜单：`Assets/Script/GameFlowManager/MenuSystem/`
- 开发调试工具：`Assets/Script/Development/`
- 旧角色 PackageSystem 仍存在；不要仅凭目录名认定旧系统已经安全废弃。

任何“已经完成”的判断都必须回到代码、Unity 引用和测试结果验证，不能只根据文件存在作结论。

## 已确定的近期规则

- 2026-09-08用户最新澄清：此前满意录像只代表“终于有了正确样子”，不是最终效果验收，更不是冻结演出。不得因此跳过已批准的反馈/演出/波次/正式入口工作；继续完整批准范围，不在小节点反复请求继续。

- 2026-09-06用户明确当前以Sprite替代Spine；没有Spine模型可注入是正常资源状态。平A事务闭环按Sprite路径推进，Spine正式资源/骨骼恢复延后，不作为当前开发阻塞。九阶段顺序和死亡/回位/最终推进验收不变。

- `AnkaluteGameLoopState` 已接线为 ANKALUTE 唯一应用级阶段权威；现有 `DRPGGameLoopRuntimeRoot` 负责 Unity 适配和 legacy facade，`GameFlowArbitrator` 仅继续仲裁 Hunt/DRPG Battle，不能据此直接删除旧兼容类型。
- 单场 DRPG 内部采用唯一分层流程权威：`DRPGBattleFlowOrchestrator` 只负责战斗生命周期、行动事务阶段与合法转换；`DRPGBattleSession` 是领域门面/组合根，不再拥有可直接写入的 `Phase`。回合调度、行动解析、效果、反应、进度等仍是深模块，不应被膨胀为伪状态或取得流程推进权。
- 一个行动严格区分“规则解析/命中提交”“ActionSettled 结算表现”“最终发布与轮转”。领域结果可以先提交，但 `ActionCompleted`、`BattleEnded` 和下一行动者只能在同一 action identity 的表现 `WhenFinished` 门完成后发布；表现 Coordinator 只持有门控句柄，不得自行决定下一回合。陈旧 binding、generation 或 action 回调必须被拒绝。
- 当前可玩顶层路径是 Title -> StrategicHub/HubHome -> DungeonSelection -> ExpeditionExploration。Title/Hub 不生成或推进 Hunt；typed 副本确认成功后才显式激活 Hunt 并开放探索输入。
- Victory -> BattleSettlement -> 原 Expedition；Escaped/Aborted 直接回原 Expedition；Defeat、Completed、Retreated 回 StrategicHub/HubHome 并清副本上下文。Hunt 激活返回失败或抛异常时也会补偿回 HubHome，探索门禁保持关闭。
- Title/Hub 的应用阶段门禁统一控制 Hunt pause、角色 gameplay input、拾取与 CharacterControlAuthority；Battle/BattleSettlement 继续使用既有 battle suspension。Game Menu、团队背包和 legacy Package 只允许在 ExpeditionExploration 打开。
- 顶层目标循环是“标题/读档 → Strategic Hub（SLG）→ 副本探索 ↔ 普通战斗”；普通战斗结算后回当前副本探索，副本完成、撤退或队伍全灭后回 Strategic Hub，并默认落到 `HubHome`。
- 城市地图、建筑、设施、商店和副本选择属于 Strategic Hub 内部导航，不增加顶层游戏阶段。Gal 是覆盖宿主阶段的剧情演出，结束后返回原宿主上下文。
- 主角永久驻场，占五人队伍中的 1 号位；取消可选支援位抽象。
- 运行时队伍顺序以 `DRPGPartyCompositionState` 为权威：主角固定 slot 0；同律对象固定显示/菜单 slot 1；战斗只消费冻结快照。同律对象导致的 HUD 重排不改变阵型或调度顺序。
- 旧手动瞄准部位破坏点/破坏后暴露空间弱点的方案已经取消。
- 核心方向是攻击造成架势/防御破坏，进入 `PostureBreak` 弱点暴露期；它与低血量立绘状态 `DamageBroken` 是两个独立概念。
- `PostureBreak` 已形成明确生命周期：中断可中断预备、跳过一次行动、暴露期间提高受击/异常收益，恢复后返还部分架势并进入短期稳定保护；敌我具体参数仍可二轮调优。
- 特殊攻击不与 Break 强制绑定；只有具体职业或被动明确监听 Break 时才建立关联。
- 充能型特殊攻击从每场战斗 0 开始，由统一不可变战斗事件增加进度，受武器容量限制，并在每次被接受的 FreeStrike 输入上原子消费充能/姿势/耐久；失败预检不得产生部分 mutation。
- 主角拥有独立于武器职业的同律身份层。当前仅亲友可成为对象；对象选择会归一到队伍 slot 1；每个亲友分支可自动贡献已解锁被动，并装备一个不占职业四技能槽的主动同律技能。
- 同律主动技能进度属于 Expedition：同一副本跨战斗保留；重新选择相同对象不清空，切换对象清除旧对象进度；副本结束、失败或返回标题时清空。对象失能只禁用技能，不自动换人。
- 完全弃用的 Unity 内容采用 `Assets/废弃内容/` 归档流程，不直接删除。
- 普通敌方 `Standard` 攻击采用“先规划技能、后在预警 0 承诺目标”的 2/1/0 意图生命周期；逻辑时间只在离散调度/行动结算中推进，不在玩家思考期间连续流逝。
- 预警 0 必须先展示在一次玩家可操作回合，并由全队任意一名可行动己方角色完成一次正常行动后才可执行；同批多个就绪敌人随后可以连续行动。
- 行动条是混合预测：平时展示己方行动预测，仅把已经预警 0/承诺执行的敌方头像按顺序插入；敌方卡不附加技能名、目标或倒计时信息。
- 普通敌方目标默认隐藏；只有技能显式选择 `RevealAtCommit` 才使用独立战场反馈公开锁定。目标承诺后失效时默认落空/取消，不静默换目标。
- 每名存活、已部署的普通敌人必须始终持有显式行动计划。无合法攻击时优先选择已有合法标准辅助技能，其次尝试合法换位；完全无事可做时执行无倒计时、无表现帧的静默 `Wait`，完成后重新进入调度，禁止回退到无计划 legacy 路径。
- `TelegraphedSpecial`、`FreeStrike`、现有特殊攻击释放时序和手动特殊防御继续走 legacy 旁路。本轮未实现防御技能熟练度或 AI 自动完美防御概率，但保留在现有视觉流程前接入策略层的边界。
- 用户已正式允许 DRPG 战斗表现使用独立透视相机与 2.5D 世界空间代理；探索、规则与物理仍保持纯 2D，探索相机不得被永久改写，战斗表现不引入 3D 物理。
- 探索 `ProCamera_World` 继续保持 Orthographic。默认 DRPG 战斗由顶层场景空间 prefab `PF_DRPGBattleWorld` 承载：独立 Perspective Camera 直接输出屏幕，舞台、敌我演员与世界 VFX 使用普通 Transform 下的 SpriteRenderer/Spine，并统一处于 `DRPGBattleWorld` Layer。`PF_DRPGBattlePresentation` 只承载 HUD、覆盖演出和透明屏幕点击代理；RenderTexture/RawImage 仅保留为可选配方输出，不再作为常规战斗默认承载。默认普通攻击的构图作用域从 Windup 保持至 Impact，并仅在 Settled/取消/故障后统一恢复。
- 技能冲突演出不采用强制的低/中/高成本枚举。`DRPGSkillSO.PresentationKey` 关联独立语义配方，每个 UI、舞台、镜头、攻击者、目标、背景、Spine、反馈和退出模块使用“继承/覆盖/显式关闭”；未配置模块继承默认冲突模板。
- Spine Event 仅是风、命中、HitStop 和结束的时间信号，不得直接修改 HP 或战斗状态。多段命中必须由规则层预解析并通过 exactly-once ledger 分段提交；缺失事件必须由 watchdog 确定性补交或退出，不能死锁。
- 冲突作用域恢复必须同时检查表现总结果与 Action Flow Gate：非阻塞尾部提示在下一阶段开始时被替换可以使总结果为 `Cancelled`，但 Gate 已完成代表核心冲突演出已合法交棒，此时不得把攻击姿态提前回滚。敌方世界演员的受击帧由语义世界表现层唯一拥有，旧 UGUI 反馈层只负责伤害数字。
- 敌方表现目标是去卡牌化：全身 Sprite/Spine 使用世界空间代理和前中后纵深排布，UGUI 仅承载轻量浮动状态与透明屏幕命中代理。默认视觉测试为前 3 + 后 3，15 敌人仅作怪海压力测试。
- 战斗入场首版采用固定约 1.7 秒流程：HUD/输入先隐藏与锁定，镜头注视生成区，怪物分批生成，镜头回默认机位，角色 UI 与其他 HUD 分层进入后开放输入。

## 条件门槛功能的调试可达性准则

- 凡是依赖资源积累、解锁、关系进度、战斗状态、随机结果、冷却或其他前置条件才能生效的功能，实施计划必须同时规划可插拔调试项。
- 调试项至少覆盖：直接构造有效条件、构造失败/边界条件、清除或复位状态、显示当前关键状态。
- 调试入口应通过明确的领域调试接口或测试 seam 改变真实运行状态，不得只改 UI 文案或表现层伪造成功。
- 每个调试项应注册到 `Assets/Script/Development/` 的开发调试系统，并提供简短按钮说明与详细悬停说明；不得污染正式发布行为或普通存档。
- 验收必须同时覆盖“调试直达路径”和“正常游玩达成路径”。仅有底层 probe 能构造状态，不等于功能已具备实际可测试性。
- 当前同律系统的后续调试补强至少包括：指定同律对象、填满/清空已装备同律技能条件、令对象失能/恢复，并最终通过真实战斗 UI 释放技能。

## 仍在设计、不可当成定论

- 敌我双方是否采用完全对称的 Break 后果。
- 同律身份层的正式关系条件来源、具体亲友分支内容与剧情法则演出映射。
- 特殊攻击最终采用爆发条、连携奖励、职业被动条件还是组合条件。
- 单场战斗乐趣循环的最终数值节奏和具体技能量级。

## 当前工作树风险

2026-08-30 检查时，工作树包含大量已修改和未跟踪的 DRPG 源码、测试、资源与场景文件。这些是既有开发成果，不属于同步层建立任务。

后续任务必须：

- 修改前检查目标文件 diff；
- 不清理、不回滚、不覆盖不属于自己的改动；
- 将 Unity 编译/运行结果标注为“在共享脏工作树上验证”；
- 使用 [TASK_REGISTRY.md](TASK_REGISTRY.md) 防止并行写入重叠。

## 当前活动任务

2026-09-13 当前无活动写入任务；`basic-attack-task11-finalization` 已完成并释放全部租约。最新依据本页“最近里程碑”首条和TASK_REGISTRY；以下旧在途记录仅作历史，不再代表当前完成率。

2026-09-06 00:27补充验证：标准平ATask7投影层17个窄行为及3个manifest contracts通过；完整manifest35/59、production矩阵Passed4/Pending21/Failed0，Foundation121/121通过。Root在宣告前持有全战场表现租约，旧刷新不能改投影，世界敌方HP有独立cursor，关闭一次canonical刷新；演员/Surface丢失Root fault。Task8/9命中运动/死亡回位尚未完成，F8仍Legacy；Spine内部恢复快照仍待确认，不能据此宣称生产九阶段已完成。主代理继续大节点。

以 [TASK_REGISTRY.md](TASK_REGISTRY.md) 为准。`basic-attack-transaction-flow-completion` 已完成 Task0～5C 与 Task6A～6D（12/24叶节点），当前 active/code-writer 连续推进 Task7→8→9 大节点。Task5C 已验证唯一原子规则/反应 ledger；Task6 已验证宣告、构图命令、Spine/静态帧前摇、watchdog与旧叶节点旁路隔离。正式已完成 manifest32/59、production3/25；Task7新增投影租约正在实施验证，尚不提高整组验收计数。Task8～14的命中运动、死亡回位、阶段9逐项结算、波次、最终推进与正式入口仍未完成，F8保持ProductionLegacy并在Task13统一切换。当前场景空PlayMode退出也能复现DOTween残留，作为独立Task14风险保留；不能宣称全量smoke或Console无错误。最新证据与活动精确路径见任务记录及登记表。

## 最近里程碑

- 2026-09-13：标准平A批准范围完成。正式PlayMode生产矩阵 `25/25`（pending/failed/watchdog/incomplete/infrastructure均0），九阶段合同账本18组 `59/59`，Foundation `121/121`，最终编译0错误0警告。补齐敌我单结果节拍、纯横向受击、双方同批回位、致死X/压暗/HomeRendered后退场、Stage9一笔一完成再换人、波次宣告/就绪/撤回/身份及各层watchdog；修复重复canonical bind造成的回合刷新与事务运动冲突，并预热首命中反馈资源。MCP 0.6.7的serverInfo仍显示旧Project_ANOIR，但编辑器进程与`Application.dataPath`双重确认实际为ANKALUTE；不影响结果。保持Sprite正式路径，Spine资源缺失不构成当前阻塞。未保存`测试场3_电车45.unity`（磁盘时间保持2026-09-12 12:08:32），未触碰用户新Spine/美术，未commit/push/clean。

- 2026-09-05 18:26：Task5C整体规则边界完成。连续新增PostureBreak7、Expedition同律原子journal4、配置进度来源4、真实Session规则核10、生产武器跨行动2，共27项新检查通过；全组/旧四类probe/Foundation121/121通过，编译无当前error/warning、Console error=0。正确项目与测试场3保持EditMode/dirty=false。38份代码/测试文件的最终hash与完整RED→GREEN见任务记录；脏树聚合501（124M/20D/357折叠未跟踪项）不变，未commit/push/清理/归档或改序列化资产。下一大节点Task6A～6D，之后Task7～9与10～14。

- 2026-09-05 17:15：Task5C治疗/护盾/架势恢复共同ledger子检查点完成。7项真实Unsupported行为RED→GREEN，另补7类篡改、Shield overflow、敌方来源同路径，总10/10通过；有序typed恢复事实、精确指纹与RuleOnly分流，伤害在effect实际位置核对HP/Shield/Posture，普通恢复不复活归零目标。完整既有组/Foundation121/121通过，manifest仍27 completed/32 pending。编译无当前error/warning、Console error=0，正确项目/场景EditMode/dirty=false，未切F8或改序列化资产。PostureBreak完整生命周期与独立PostureDamage仍待接入，不宣称Task5C整体或九阶段实机通过。

- 2026-09-05 16:37：Task5C即时行动条delta子检查点完成。6项初始行为RED→GREEN，另实证“先timeline后lethal被拒”和“近过去时间在写HP/MP后抛错”；补typed TimelineChange、有序投影/对账/指纹与预构建队列整体apply。新增11/11（含7类伪造、exact int优先级、RuleOnly、晚期资源失败）；既有反应/资源/状态组与Foundation121/121通过，manifest仍27 completed/32 pending。编译无当前error/warning、Console error=0，正确场景EditMode/dirty=false。未切F8、未改序列化资产，整体九阶段仍未完成。
- 2026-09-05 15:18：Task5C 纯状态/零 hit 反应检查点通过。先复现5项“必须有Damage”的RED与无效状态仍展开后代的RED，再实现显式非命中上下文、独立目标指纹、零hit状态事实对账和RuleOnly分流。新增9/9（含7类篡改、死亡不重触发/不提前移除、深度与总数预算、失败全回滚）；既有完整反应/资源/状态组与Foundation121/121通过，manifest保持27 completed/32 pending。编译无当前error/warning、Console error=0，正确项目/场景且EditMode/dirty=false。未切F8或改序列化资产；下一步仍为Task5C剩余范围。
- 2026-09-05 14:55：Task5C 状态 workspace 与单命中反应内 ApplyStatus 共同 journal 子检查点通过。补齐 staged apply/remove 对后续伤害的影响、独立 Fork、状态预计算后 primitive 提交、持续时间 overflow/巨大 stack/定义 drift 拒绝及冻结图标元数据指纹；完整账本逐项对账状态前后快照、明确 lethal 后跳过状态且不移除部署、敌我同路径/闪避无附带状态。新 workspace7/7、status-ledger9/9；既有反应/武器/成本/容量组与Foundation121/121通过，manifest仍27 completed/32 pending。编译无当前错误/警告、Console error=0；未切F8、未改序列化资产。纯状态/零hit反应及timeline等仍未完成。

- 2026-09-05 14:13：Task5C 武器断裂后的 Battle 进度容量/数值共同提交通过。进度定义保留基础容量与装备修正来源，不重读 mutable SO；完整行动成本/进度变动之后生成只读容量前后/溢出事实，和伤害、耐久、碎片一次 apply；禁止缺项、重复、伪造来源/数值或同显示容量但不同 base 的替换。补齐容量请求全部语义字段的 fingerprint，重放不遗漏请求差异。新容量11/11、原武器10/10、成本9/9、ledger8/8、资源8/8、Foundation121/121；manifest仍27 completed/32 pending，编译无当前错误/警告、Console error=0，未改序列化资产或切F8。Task5C整体、生产事件消费者和九阶段实机仍未完成。

- 2026-09-05 13:52：Task5C 武器断裂装备结果共同 journal 子检查点通过。普通武器最后耐久→卸装/释放占用/同名碎片累加；专属武器零耐久保留；构筑回退平A、移除武器技能但保留配置槽与饰品，HP/Posture 上限收缩均在完整 State journal 一次提交。新增不回退 mutation epoch 防 equipment/inventory ABA 恢复；补齐 use/event/completion 精确覆盖、完整行动资源对账和私有 capability 门禁。新武器10/10、成本9/9、ledger8/8、资源8/8、Foundation121/121，manifest仍27 completed/32 pending，编译无当前错误/警告、Console error=0；共享脏树与 Unity 场景未清理/改写、未切 F8。完整 Task5C、生产接线和九阶段实机验收仍待完成。

- 2026-09-05 12:43：Task 5C 的 MP/Posture/Action Progress reservation 生命周期已共同接入完整 ledger。工作区 Fork 保留同一预留证明，主攻击/反击前先暂存成本，提交后只读映射真实 child transaction 状态/receipt；取消和失败不消费 live state。测试验证架势 `100 → 成本后90 → 反击后83`、MP `30→25`、Progress `10→7`、武器 `100→99`、State revision 只增1，重放不再消费。补齐完整 ledger gate、captured actor/stable progress owner 与提交前 owner drift 拒绝。新成本9/9、完整ledger8/8、资源8/8、Foundation121/121；manifest仍27 completed/32 pending，编译无当前错误/警告、Console error=0。未修改 Scene/Prefab/SO、未切 F8，完整 Task 5C 与实机演出仍未完成。

- 2026-09-05 12:06：Task 5C root ledger 一次提交与不可变事实子检查点通过。主命中/反应/typed resources 共享 cache 与 journal；相同请求重放不重复扣费，反应/资源/武器事件内容变化拒绝。补齐本 State lease 校验、逐步 HP/架势/护盾对账、scoped immutable facts 与 Impact Trace。新 ledger 8/8、上轮资源 8/8、Foundation 121/121，编译无当前错误/警告、Console error=0；manifest 仍 27 completed / 32 pending。未迁完整生产资源生命周期/事件消费者、未切 F8、未验收九阶段视觉，不能将此子检查点当作 Task 5C 全部完成。

- 2026-09-05 11:32：Task 5C 的真实 Battle Progress/Weapon Use journal 子检查点通过。HP/MP、主攻击/即时反应、进度和武器消耗可在同一 State 私有 capability 下共同提交；捕获值及 revision 漂移（含 ABA）、错误 owner、重复/冲突和 prepare 晚期故障均有零部分写入检查。新资源 8/8、既有反应 2/2、主命中 4/4、Foundation 121/121；固定 manifest 仍为 27 completed / 32 pending。编译无当前错误/警告，Console error=0；未切换生产入口、未改序列化资产。完整 ledger、生产资源生命周期、普通断裂/碎片等仍未完成，不能据此宣称九阶段或实机通过。

- 2026-09-05 10:57：Task 5C 的独立反应闭包已通过：稳定类别/优先级/Home Slot 顺序、深度8/步骤32边界、整笔被动与同父去重、完整 root/parent identity、死亡后取消普通反应且只允许真实 OnDefeat、故障整链丢弃；修正闪避读取旧 HP/Posture 的问题。反应 2/2（12 个细分行为），主命中 4/4、Foundation 121/121；固定 59 项 manifest 当前 27 completed / 32 pending，编译无错误、Console error=0。尚未完成 Task 5C 的资源/进度/武器与完整 ledger 提交，更未进入新演出实机验收；未切换生产入口或修改序列化资产。

- 2026-09-05：标准平A已完成 Task 3～5B：revisioned workspace/capability、唯一 Router/composition、冻结快照与不可变主命中 ledger。重启后恢复 Unity/MCP，新增真实行为 RED→GREEN 修复零成本夹带 mutation、冻结威力不匹配、journal 重放冲突、重复 Trace ID、第二 primary ordinal 和双向资源跨 Legacy 边界。主命中 4/4、路由 5/5、workspace 7/7、identity 5/5、Foundation 121/121；编译诊断无错误、Console error=0（仍有旧 Spine/未使用字段警告）。未切换生产入口、未修改序列化资产；完整九阶段、反应、阶段9、波次与实机验收仍未完成。

- 2026-09-04：标准平A实施 Task 2 完成。战斗 Session 现拥有 process-monotonic BattleGeneration、完整 ActorTurn/Selection/Action identity、固定 generation 的唯一事务 arbiter 与 typed root receipt；Action Trace 从 Stage 1 开始并精确关联 TurnBegin -> SelectionOpened -> ActionLocked，每个 receipt 返回 action-scoped snapshot。Task 2 六项契约、Unity 编译、Console 与 Foundation 121/121 全绿，最终只读复审 GO；下一步进入 scheduler 纯查询与 revisioned mutation workspace。

- 2026-09-03：获批标准平A规格的严格 TDD 实施计划已完成：15 个顶层章节拆为 24 个独立 GREEN checkpoint，显式锁定18组 deterministic contract 与25项 production-adapter scenario；三路最终复核 GO/无 P0/P1。计划从脏树 baseline 开始，依次建立 identity/arbiter、workspace、双 ledger、九阶段表现、Return/Settlement/TurnEntry、Wave、生产入口与三层验收；尚未修改运行代码或 Unity 资产。
- 2026-09-02：用户整体批准 `docs/superpowers/specs/2026-09-02-standard-basic-attack-transaction-flow-design.md`。标准平A采用 A+ 深事务模块、唯一 BasicAttack seam、双不可变 ledger、全战场 Projection Lease、Return/Settlement 屏障、敌我同构表现、延迟失能呈现与独立 Wave Transition。既有 `121/121` 只证明当前门控与局部配方，不代表九阶段实机验收。
- 2026-09-02：完成敌方普通攻击冲突演出时序对齐。根因是 Action Flow Gate 已完成后，尾部非阻塞提示被下一阶段替换使总结果成为 `Cancelled`，旧恢复策略误把它当成核心演出取消；现已改为 Gate-aware 恢复，并移除敌方世界受击后的旧 UGUI 二次闪帧。另确认 Unity 曾持续运行陈旧 `Assembly-CSharp.dll`，强制重编译后正式游戏流中敌方攻击姿态跨 `PreparingAction -> ExecutingAction` 多帧保持。DRPG probes `121/121`、最终 Console error=0，实机截图为 `Library/FunplayMcp/Screenshots/enemy-basic-executing-verified.png`。
- 2026-09-02：单场 DRPG 底层流程收敛为 `GameFlowArbitrator -> DRPGBattleFlowOrchestrator -> action transaction` 的分层权威结构。`DRPGBattleSession` 只通过仲裁器转换生命周期；`TryCompleteAction` 已拆成领域提交和显式最终推进，ActionSettled 表现完成前不会轮转、发布胜负或清空事务身份。Unity 编译 0、DRPG probes `121/121`，Play Mode 聚焦生命周期/表现事务/行动事务探针全部 PASS。全局 smoke 仍受既有 World Host 延迟销毁探针的 Play Mode 立即计数问题阻挡，该问题不属于本次流程重构。
- 2026-09-01：DD1 式战斗表现首轮闭环完成。默认战斗采用浅透视固定侧视舞台，三名己方与六名敌方均为场景空间 Sprite/Spine 演员；旧卡牌式敌方 UI、重复背景和右侧当前角色大立绘在该模式下关闭。常驻 UI 收敛为顶部混合行动条、左下队伍状态和右下 3x2 指令区；普通攻击通过现有 Windup -> Impact -> Settled 事务驱动 DOTween 前冲、静态攻击帧、一次性命中结算和安全回位。原创临时攻击帧已接入第二名队员及两类敌人。Unity 编译 0 error/0 warning、DRPG probes `119/119`、连续多回合 Play Mode 与最终 Console error=0 通过；验收截图为 `Library/FunplayMcp/Screenshots/dd1-runtime-final-multiturn.png`。
- 2026-09-01：DD1 阵型与基础攻击对称性二次验收完成。前/中/后仍是战斗逻辑数据，但每一侧的场景表现统一投影为一个不可重复的横向视觉槽序列，避免各逻辑排独立居中造成重合；Sprite 意图切换统一遵守脚底基线契约，后续 Spine 驱动也应实现同一视觉锚点协议。普通攻击只走对称的世界冲突路径，玩家专属旧 `AttackCutIn` 不再混入基础攻击，但该执行器仍保留给明确配置的高成本技能。重启后正式游戏流与 3 己方/6 敌方实机通过，Unity 编译 0、DRPG probes `120/120`。
- 2026-09-01：完成战斗世界/UI 架构纠正。新增顶层 `PF_DRPGBattleWorld`、场景空间默认舞台、`DRPGBattleWorld` Layer 与运行时世界宿主；Perspective Camera 默认直接输出，UGUI 仅保留 HUD/覆盖/点击代理，RenderTexture 为可选路径。全量探针 `118/118`、Victory+Defeat Play Mode smoke、截图与最终 Console error=0 通过。
- 2026-08-30：建立跨 Codex 对话的共享状态、任务登记、独占路径和交接协议。
- 2026-08-30：确认 ANKALUTE 顶层应用流、Strategic Hub/Expedition 术语及旧 `GameFlowArbitrator` 的迁移定位；当前仅完成设计定论，尚未修改运行代码。
- 2026-08-30：完成并获准以 `docs/superpowers/specs/2026-08-30-strategic-hub-game-loop-design.md` 作为实施基线；首轮实现只新增 `Assets/Script/GameFlowManager/AnkaluteGameLoop/` 纯 C# 核心和专属 probes，避开既有 DRPG GameLoop、公共组合根与 Unity 资产。
- 2026-08-30：完成 `docs/superpowers/plans/2026-08-30-strategic-hub-game-loop-foundation.md` 严格 TDD 实施计划并通过最终契约审查；执行任务 ID 为 `strategic-hub-flow-foundation`。计划尚未执行，运行代码与 Unity 资产仍未修改。
- 2026-08-30：普通敌方 2/1/0 意图生命周期、最低一次玩家响应、混合行动条和选择性目标公开完成；基础探针 `90/90`、Play Mode 冒烟、最终编译与 Console error 检查全部通过。验证报告：`Logs/DRPGPlayModeSmokeTest.txt`。
- 2026-08-30：修复后排/增援敌人无计划导致的战斗停滞；补齐普通敌方 `Attack / SupportSkill / Reposition / Wait` 显式计划闭环，其中纯待机不显示倒计时且不触发表现帧。基础探针 `90/90`、Play Mode 冒烟、编译和 Console error 检查通过。
- 2026-08-30：`strategic-hub-flow-foundation` 启动执行；审计确认最近 DRPG 战斗开发不改变 Strategic Hub/GameLoop 契约且目标路径、公开类型、菜单、默认程序集均无冲突。取得唯一 `unity-writer` 后，在零新增脚本状态完成共享脏工作树 preflight 编译，错误/警告为 0。
- 2026-08-30：Strategic Hub 纯核心首个里程碑通过严格 TDD：已验证 `HubHome -> CityMap -> Building`、精确 Back/history、copy-on-publish 快照、同路由/根 Back/干净 reset NoOp、深层 reset 与拒绝不变量；独立菜单输出 `PASS Strategic Hub route stack`，全项目编译错误/警告为 0。尚未接线运行时。
- 2026-08-30：Strategic Hub Activity 里程碑通过严格 TDD：Gal、Shop、DialogueEvent 作为宿主 Route 上的阻塞活动，使用单调正 token；导航/第二活动 Busy，matching Failed/Completed 完成，旧 token Stale，特权 reset 清活动但不复用 token。route/activity 两个独立 probes PASS，编译错误/警告为 0。
- 2026-08-30：Strategic Hub typed 副本确认与事件安全里程碑通过：`ExpeditionId`/`ExpeditionStartRequest` 是纯值契约；只有 DungeonSelection 可返回 effect-only Applied，且不改 Hub Snapshot/revision/event。Activity 会 Busy 阻断确认；Transitioned 重入为 Busy，订阅者异常不回滚已提交状态且 finally 恢复后续请求。三个独立 probes PASS，编译错误/警告为 0。
- 2026-08-30：`AnkaluteGameLoopState` 顶层 happy path 里程碑通过：Title 的 NewGame/LoadedGame/ReenterAfterTitle 分别映射 Hub entry reason；Hub→Expedition、普通 Battle Victory→Settlement→原 Expedition、Escaped→原 Expedition、Completed→HubHome 已验证。主 fixture 精确 8 次顶层 transition，四个独立 probes PASS，编译错误/警告为 0；尚未与现有运行时接线。
- 2026-08-30：顶层终止/错误纪律里程碑通过：Retreated/PartyDefeated 回 HubHome 并清副本，Aborted 回原副本；ReturnToTitle 仅 Title NoOp/StrategicHub Applied，副本相关阶段阻断；Hub reset Busy/其他拒绝保持 top Snapshot；top/Hub subscriber 异常与 reentry 均有确定恢复语义。当前独立菜单输出 8 条 PASS，编译错误/警告为 0。
- 2026-08-30：Strategic Hub/GameLoop 纯核心通过三路独立终审。契约与隔离无 P0/P1；测试审查提出的 Transition.Before、完整非法路由图和 Shop typed payload 三个覆盖缺口均经可逆 mutation 先红后绿补齐。模块仍未接线现有运行时、场景或 Prefab。
- 2026-08-30：`strategic-hub-flow-foundation` 完成并释放全部租约。共享脏工作树最终编译 0 error/0 warning，专属菜单 8/8 PASS，最终 Console error=0；生产核心无 Unity/DRPG/legacy flow 依赖，7 个脚本及 13 组资产/meta 完整，未修改 DRPG、公共组合根、Scene/Prefab/ScriptableObject。
- 2026-08-30：`strategic-hub-runtime-integration` 启动规划；目标是让标题页开始游戏进入可见且冻结探索的 Strategic Hub，再由明确入口启动现有探索。当前仅审计公共 seam，未修改运行代码或 Unity 资产。
- 2026-08-30：`strategic-hub-runtime-integration` 三路 seam 审计收束并完成实施计划。已锁定：现有 `DRPGGameLoopRuntimeRoot` 作为唯一 Unity Adapter；Title/Hub 不自动启动 Hunt；现有 `DRPGGameLoopCanvas.prefab` 只做增量 Hub 扩展；旧 Menu/Backpack/Package 输入按新顶层阶段 gate。21:11 取得唯一 `unity-writer`，尚未开始代码 RED/GREEN。
- 2026-08-30：`strategic-hub-runtime-integration` 完成。开始游戏现进入可见且冻结探索的 HubHome，可进入 DungeonSelection 并通过 `dungeon-a` typed request 启动当前探索；现有 Canvas 只做增量扩展，未新增第二 Canvas、未保存场景。
- 2026-08-30：顶层回流与门禁完成：Victory/Settlement 回同一副本，Defeat/Completed/Retreated 回 HubHome；Hunt 显式激活，失败/异常可补偿；直接 Battle 和空 EncounterId 在突变前拒绝；三条 overlay 输入按 ExpeditionExploration gate。
- 2026-08-30：最终共享脏工作树验证通过：Unity 编译 0 error/0 warning，ANKALUTE Game Loop probes 13/13、DRPG probes 90/90，确定性双战斗 Play Mode smoke 覆盖 Victory+Defeat、激活失败、空 EncounterId 和同一玩家二次出击，最近 90 秒 Console error=0；16:9 与 4:3 Hub QA 无裁切。
- 2026-08-31：完成 P0 Break 语义清障：低血量演出统一为 `DamageBroken`，FreeStrike 收敛为身体 Rect 命中与确定性溅射，旧部位耐久/防御/空间弱点运行时和探针按规则注释归档。恢复测试安装器误覆盖前的完整战斗 UI Prefab；共享脏工作树编译 0 error/0 warning、DRPG probes 89/89、确定性 Victory+Defeat PlayMode smoke 通过。
- 2026-08-31：P1～P5 连续实现完成。主角/五人队伍不变量、PostureBreak 生命周期、不可变战斗事件与确定性进度、充能 FreeStrike 原子消费、主角同律身份层/队伍 slot 1/Expedition 生命周期/独立菜单与战斗 UI 已接线。安装器统一 Progression、调试启动器与场景遇敌 PartySetup；`测试场3_电车45` 已通过 Unity API 保存并确认磁盘依赖。共享脏工作树编译 0 错误、DRPG probes 101/101、Title→Hub→Expedition→Victory+Defeat PlayMode smoke 和 P5 资产审计全部通过。
- 2026-08-31：透视战斗舞台与技能冲突演出 P0～P7 已实施完成。新增语义配方编译、Spine exactly-once 分段命中、隔离透视相机/RenderTexture 舞台、世界空间角色阵型、透明点击/命中代理、脚下紧凑状态牌、HUD 分组与冲突恢复、固定入场和开发直达；Unity 编译 0、DRPG 116/116、Play Mode 世界目标攻击与 Console 无 error 通过。正式 Spine/VFX/音频/舞台美术留待二轮内容制作。

## 下一位任务执行者的起点

1. 按 [SYNC_PROTOCOL.md](SYNC_PROTOCOL.md) 完成启动读取。
2. 明确自己的单一任务目标和不处理范围。
3. 在 [TASK_REGISTRY.md](TASK_REGISTRY.md) 登记后再写入。
4. 若目标涉及当前“单场战斗乐趣循环”讨论，先区分上述已确定规则与仍在设计内容。
5. 若继续 Strategic Hub，下一任务可接 CityMap/Building/Facility UI adapter、Gal/商店 activity adapter 或真实副本数据源；继续复用现有顶层权威和 typed request，不新增第二套顶层状态机。


## 2026-09-20 紧凑战斗演出已接入

正式 BattleStandardPresentationBinding 现读取 CompactBattleTiming.asset：声明 0.04、聚焦 0.14、出手最短 0.10、回位 0.22 秒；空结算取消固定等待但保留渲染交接。技能名持续到回位开始，现有特写 shader 加强背景/旁观压暗模糊。保留原规则与事务系统。实测主体演出 0.966 秒，六次行动后无特写层残留。契约 57/59 通过，入场运动与测试装配两项既有失败仍在；不是整套全绿。Unity 已退出 Play、恢复保存场景，writer 释放。详见 tasks/2026-09-20-compact-battle-presentation.md。







## 2026-10-05 PBR 地牢表面独立样品
SC_PBRDungeon_Surface_Validation 已完成：10×8m主室，12盒体/144三角形，2套CC0 PBR石材、12个DBuffer贴花。新Renderer索引9只含Decal/SSAO，既有0–8/default0保留。四阶段/4单项关闭真实Play对照、保存重开/引用/Shader/编译与原5场景SHA验证通过。当前Edit/Full/clean，writer全部释放。操作1–4、D/N/O/P；本样品为标准URP/Lit，未复用体素RealToon/旧雾。详细结论与性能边界见docs/xinghai-pbr-dungeon-surface-validation.md。

## 2026-10-05 PBR墓室卡通对照
SC_PBRDungeon_Cartoon_Validation为独立副本，Renderer10采用暗牢当前五档RealToon、接触AO、冷雾及Volume并保留DBuffer。B照搬/墙面可读性适配，默认适配。12网格/相机/UV未变；源PBR与DarkDungeon资源259文件SHA不变，实机/重开/20轮切换验证通过。最终Edit/clean，writer释放。详情docs/xinghai-cartoon-dungeon.md。







## 2026-10-07 T7-A.3 连续局部体积雾

T7A3独立RendererFeature/密度场/最多2个AtmosphereLight/8-16步积分/Depth-aware Upsample与可逆场景根已完成。Gate F4未通过：D-F圆形灯区和D-D纵深未显著解决，Medium移动控制器CPU中位2.74ms。因此Low=T7A2最多8 Volume正式路线，BestCurrent=Low，Medium=S12/Half/SmoothUnion/单灯Experimental。按用户计划冻结地牢底层Atmosphere，未做T7B/C。

36正式+36消融+78参数+40调试；Medium82秒、Low82秒、Dual30秒实机验证，恢复/原生录像/性能归档。60轮状态恢复+20轮实际RT释放，保存重开36/36基线精确、旧协议18/18精确、1772旧资源SHA不变。场景仅新增控制根与2个注册组件。最终Edit/clean/无预览、编译ready；writer与路径已释放。报告`docs/render-research/T7-A3-continuous-local-volumetric.md`，Gallery`artifacts/xinghai-t7a3-continuous-volumetric/index.html`。

## 2026-10-07 T7-C.2 HD2D景深与镜头后处理交付到H2

独立`SC_HD2DPost_Validation_T7C2`、`SC_HD2DPost_Dungeon_T7C2`、`SC_HD2DPost_Sunset_T7C2`已完成，9后处理Profile提供Clear/Light/Expressive，2个翠绿材质修色副本保留环境与Grade。22主图+10回归+6诊断、两段合计54.001秒/2967帧录像已归档。Light为默认探索推荐，Clear为稳定基线，Expressive可选；用户美术选择Pending，不自动提升全局Look或进入C3，旧DungeonMedium仍Experimental。

本机12任务GPU实测证明原生Gaussian/Bokeh正交深度失真，因此仅正交启用一个有限深度感知备选，透视仍原生，互斥；轴向焦点、重建参数保持、Volume组件销毁、相机实例恢复RED→GREEN。Clear与基底144万像素完全相同；焦点变化0.8192m、最大平滑误差0.0282m，自动传送/转向/投影复位误差0。真实地牢导航94.97m；相机升降只模拟焦深，实际玩家跨台阶未验证，每段只抽查10录像帧，未认证整段无闪烁或角色/技能/UI战斗阅读。

40性能状态/3600独立GPU样本、20完整恢复循环、关窗/保存/退出Play/重开通过；冻结旧资源1202/1202 SHA不变、三场1398受保护几何/相机块不变。最终clean Edit停户外C2、原URP与镜头，C2自有RT/Volume/Feature/TEMP0；全局native/editor RT微增长未归因，透明水/云独立深度限制保留，不宣称全引擎无泄漏或低端/build认证。Gallery数据/54相对文件/JS通过，浏览器布局未验证。

引擎此前在旧Capture.Tick内OpenScene native SIGSEGV，恢复后采用逐场采集，未证明引擎根因已修复。当前此项目MCP实测`http://127.0.0.1:28230/`，旧28229监听仍占用；使用`E:/WORLDCREATOR/XingHaiHuiLang/work/renderlab-t7c2/Invoke-C2Mcp.ps1`并先核对项目身份，不杀未知监听、不改全局客户端配置。所有本轮Unity/路径租约已释放。报告`docs/render-research/T7-C2-hd2d-camera-post.md`；Gallery`artifacts/xinghai-t7c2-hd2d-camera-post/index.html`；任务交接`tasks/2026-10-07-xinghai-renderlab-t7c2-hd2d-post.md`。

## 2026-10-08 — 2D Outdoor v2.3 Gate O1

任务 xinghai-2d-outdoor-v23 completed/released。新独立场 SC_2D_OutdoorV23_Courtyard，源v2 OutdoorCourtyard，Renderer2D沿用13/global默认0未改。原素材/布局冻结3749文件零SHA变化，120层结构/原绑定/相机/灯参数一致，20次恢复材质41→0，真实卸载退出与保存重开无自有材质/Probe残留，零编译错误。主对照16张1600×900 Play、消融、55秒3080帧真实视频及三档性能交付 artifacts/xinghai-2d-outdoor-v23/comparison.html；12项结论 docs/render-research/2D-Outdoor-v23.md。Engineering PASS、Gameplay PARTIAL（录像编码/细边稳定性及正式游玩未全认证）、Art User Pending；BestStable保留N3、BestVisual候选Balanced、Presentation候选Light。无新纹理/Mask/3D、未读Script(s)、无旧地牢修改/提交推送/关机/自动下一阶段。网页离线链接检查通过，浏览器自动化初始化失败，交互UI未认证。

> 2026-10-18 OSSASTRA PRE00：旧源码实际Editor/Player编译列表均0、编译错误0；4个授权桥接/绘制器同条件隔离且主体/meta保留，SpinePixel核心保留。恢复点44052文件SHA已核验；1265既有场景/Prefab/meta一致，3个旧测试PNG meta有导入平台追加，严格完整性验收未全通过。用户并行Unity现场保留，空场Play进入/退出成功但URP错误及用户target.fbx使纯空场验收Pending。原.git/历史仍在，未提交推送，未M01；任务写租约释放。

> 2026-10-18 OSSASTRA PRE-00最终交付：旧源码实际Editor/Player0、编译无错、新独立相机场Play约50秒并退出；2384旧文件和1265场景Prefab/meta最终全同。用户批准根Git安全移出后新main零历史初始化，再批准13文件12374字节公开首发至https://github.com/huangjianhangxbx-star/OSSASTRA，根提交724408bb08376df0cd2dd45ff58d6d6efab295e0，远端/公开树完全核对，无Gitee写入。技术骨架未含Packages/旧渲染配置/商用插件，干净克隆/Player构建未验；旧备用Renderer OnValidate错误保留。用户原技术场模型/Spine已恢复Edit clean。全部写租约释放，未启动M01。

> 2026-10-18 M01用户确认：体素基本单位保持现状，2D角色/素材通过PPU及相应导入缩放适配；当前无正式场景。先做独立动作/命中核心及测试，正式移动平面、角色绑定、Spine接入待场景确认，不自动改样板场比例。

> 2026-10-18 M01纯核心候选已交付：Candidates/M01（Assets外，避免用户编辑中域重载），6个领域C#文件，动作资格/边沿/时钟/取消/单释放去重/唯一HP。真实RED到GREEN，最终29/29，netstandard2.1/C#8构建0警告0错误；复验修复60Hz Release晚一帧与AttackReady假可用问题。全部源码/逐行草稿/权威表/用户待审账本齐备，14文件审查ZIP另存且SHA全验。无Unity导入/场景写入或真实Spine/移动/碰撞验收，无提交推送，GitHub仍724408b；整体M01 PARTIAL，正式场景尚缺，所有写租约释放。

> M01场景候选已交付：用户target 1标准4x4体素世界1x1x.25，120块独立SC_M01_CharacterTest，rozeul原导入.001/Scale1；真实输入→移动Spine→单普攻范围→唯一HP/闪白链已接。核心29测试与场景14受控断言通过，Pause表现脱节RED→GREEN；旧RendererFeatures错误保留。源场/FBX/Spine数据SHA保持，旧2384/1265保护记录0差异。12生产C#/真实接线/全源码/逐行草稿/待审账本及46文件ZIP齐；虚拟输入/生命周期注入不代物理手感或用户审批。测试设备0，已退出Play，独立场Edit ready/clean，所有写租约释放；无M01 commit/push，GitHub仍724408b，不自动M02。

2026-10-19 M01候选推送完成：GitHub OSSASTRA main@fb4f781149093847611cd0411eb11705343eee16，远端ls-remote与本地HEAD一致。52文件，自有源码/测试/审查和交接文档；核心复跑29/29，场景沿用已验收ZIP快照，当前磁盘场景改动保留（25增1删），未覆盖。无Gitee写入，无第三方/用户媒体发布。候选已发布但架构所有区间仍待审。状态complete/read-only，仅发布子任务完成；全部写租约释放。公开交接入口docs/ossastra/M01-HANDOFF.md。


2026-10-19 解除禁编完成（用户明确授权，覆盖PRE00默认隔离约束）：隔离asmdef/meta已移出Assets到E:/WORLDCREATOR/Backups/OSSASTRA/Legacy-Reenable-20261019；四处PRE00首尾编译条件已撤销，旧源码回到原Assembly-CSharp/Assembly-CSharp-Editor归属。prepare_editor ossastra-legacy-reenable-20261019实测ready，编译错误0；旧Editor源1028，CharacterRoot和真正旧InputHandler已加载到Assembly-CSharp。VELGITA原Prefab的InputHandler SerializedObject可见503项属性（含InputAction绑定子项），配置可读。当前用户测试场1_卧室 dirty及VELGITA Prefab Stage均保持，不保存/切换/Play；磁盘场景及Prefab前后SHA保持。原隔离配置及四包裹文件备份齐备。仅本机变更，未提交推送，不写Gitee；不宣称旧场景运行功能已验。complete/read-only，全部Unity/源码写租约释放。公开仓库PRE00隔离说明是历史发布状态；本机当前已按用户后续授权解禁，后续任务不得误恢复禁编。

## 2026-10-19 角色控制第一轮修整完成
用户四项明确要求已执行。后续必须先读根OSSASTRA_RULES.md（命名、InputAction、旧VELGITA参考、脚本分区、动态记录规则与迁移/发布边界），现状详见docs/ossastra/character-control-refinement.md。旧代码当前按用户授权解禁，不得按PRE00历史结论重新禁编。共享控制移至Assets/OSSASTRA/Scripts/Characters/Core与Runtime，去M01类/命名空间；CombatTestTarget置Development。输入为独立MonoBehaviour+每实例可序列化InputAction，真实克隆VELGITA的移动/鼠标/摇杆/左手攻击绑定。新PlayerCharacter.prefab与既有SC_M01_CharacterTest实际连接；场景6引用齐/127 Collider/0MissingScript。领域6源/meta保持，29核心+10输入测试通过、Unity无编译错误；输入夹具非全场Play认证。用户旧Prefab/卧室磁盘SHA保持，最终旧PrefabStage/卧室dirty现场，未运行旧场；未自动提交推送。架构仍待审，无M02/角色技能连段AI完成声明。全部Unity/代码写租约释放，complete/read-only。

2026-10-19 用户架构问答/规则更新完成：新增R13 PascalCase脚本/类型与camelCase变量规则、R14避免协程偏好、R15 Orient/Orientation命名偏好。当前Actions()的IEnumerable/yield为同步生命周期枚举，不是StartCoroutine，不存在等待按键激活；InputFrame只是单次Update采样快照，AttackIntent/BasicActionRuntime负责按下边沿及动作资格，不是丢帧/限帧。当前CharacterInputFrame公开大写字段不符合用户新变量规则，AimAt/朝向拆分仍未改，保留待下一轮实施。CharacterController为Unity原生3D胶囊角色运动/碰撞组件，不是自写角色Root；独立于朝向拆分决策。本轮只更新规则/解释，无代码/Unity/提交推送。complete/read-only，文档租约释放。

2026-10-19 character-foundation complete/read-only：按用户批准完成变量camelCase、CharacterMotor→CharacterMovement保留GUID/原速度配置、独立CharacterOrientation.OrientTowards、每角色CharacterDataBus与Root初始化注入。Bus只读访问InputHandler.CurrentFrame/Movement/Orientation/BasicActionRuntime/ControlAuthority的真实状态，不复制HP/动作时钟；Movement/Spine/Melee实际使用Bus，非空壳。保留同步枚举器，无协程。共享Prefab+自有测试场接线完成。核心29、输入10、Bus/朝向12、Play场景14合同全通过；首轮空挥虚拟/真实鼠标冲突已定位为夹具问题，隔离指针binding后14/14，失败原件保留。运行测试设备0、InputSettings恢复、编译无错，退出Play后M01测试场Edit ready/clean。旧卧室/VELGITA磁盘SHA保持；测试开始仅自有M01场clean，无运行旧场。规则文件R13–R16、当前数据流/权威/审查账本已更新，后续先读docs/ossastra/character-foundation.md。当前本机候选未提交推送，架构仍待用户审定；全部Unity/文件写租约释放。

2026-10-19 character-candidate-publication complete/read-only：用户明确授权无下一阶段计划则推送候选并等待。GitHub OSSASTRA main@6fdd36ad2b90d7bb948c0be8df6c50649f331265已发布，远端ls-remote与本地HEAD一致；64文件变化（含保GUID迁移），共享源码/Prefab/测试场/规则/交接/证据。发布前代码及资产与最终测试manifest 0差异，核心复跑29/29；场景14/Input10/Bus12为已验收证据。新增R17：键盘可见翻转、攻击鼠标优先于键盘，当前未实现分离；R18：角色子系统接口架构偏好，当前仍具体类型Initialize，明确未实现。没有已批准下一阶段计划，不自动开始。未上传旧源码/第三方媒体/本机解禁删除，旧asmdef与meta本机删除保留未提交；无Gitee写入。原生Git直连443失败，使用用户当前已启用Windows代理的单次git -c http.proxy参数推送成功，不改全局配置；未通过连接器重建提交/改远端身份。交接入口docs/ossastra/M01-HANDOFF.md。所有写租约释放，等待用户后续计划。

2026-10-19 M01-R1-v05 TECH_VERIFIED/read-only：用户授权v0.5实施，基准6fdd36a。R17显示朝向/攻击方向分离、合法鼠标优先/释放帧方向快照，R18窄职责接口与每角色Bus连接完成；Root移出Development依赖，DebugControls独立，HitResolver无假Initialize。核心29/输入10/Bus12/Play14/R17物理15/接口12/同构双角色4均通过，完整源码行号SHA/审查图表/待审账本与外部审查包齐备。用户确认无场景编辑后Editor API清除1条旧debugTarget覆盖并保存，Edit ready/clean、0MissingScript、引用齐、Probe设备0。旧资产保护、解禁状态/原Git不改；无提交推送/Gitee/M02。全部写租约释放；仅人工手感与逐行架构审查待用户，候选优先级仍待审。

2026-10-19 github-default-publication complete/read-only：已更新R11，后续已授权任务成果验证后默认定向提交推送GitHub OSSASTRA，无需逐轮再确认，禁止Gitee；保留可发布范围/远端核验与用户架构审查边界。本次为接口说明与未来发布规则登记，未改生产源码/Unity、未执行新发布；全部文档路径租约释放。

2026-10-19 M02A-v06 complete/read-only：P0 4eca664、实施 a5a4a54e145e7a044c78aeb4773bcd2d55b183be、GitHub main 8925b76bc364784692bce324717627aa89b27bd6 已推送并核验远端一致。临时同Spine双实例控制闭环/中文规范已完成；真实29核心/10输入/12Bus/26双角色世界/15边界/1冲突/15单角色R17通过，18源全量行号SHA/逐段说明/图/权限/接线/注释表/失败原件已公开候选。Current唯一，移交当帧输入屏障、Held松键武装；旧动作继续，重复owner失败不干扰原owner。当前Dual场Edit ready/clean，0缺脚本/Probe，原InputSettings恢复。用户未批准架构/物理键鼠体验；停止M02A，不M02B/跟随/技能。旧代码禁编删除仍本机保留不入提交，无第三方/用户媒体/Gitee发布。全部Unity/代码/文档写租约释放。


2026-10-20 M02C-v10 complete/read-only：实现704a16d、发布记录653f0f175cda73e84062b0b16b9a3719e6917b0c 已推送GitHub OSSASTRA/main并核对远端一致。单一有效Frame选择Player/Companion/Neutral、场景级最小FollowDirector完成；原Root/Bus/Movement唯一运动和动作链保留，无AI攻击/寻路/传送。纯策略13、核心29、最终新场19、Selector13、M01 15通过；旧M02A26及其后定向帧回归的版本范围详见M02C-verification，失败原件保留。原M01/M02A场、共享Prefab、InputSettings及指定模型/Spine共7文件SHA一致。最终独立M02C场Edit ready/clean、角色2且各6引用齐、MissingScript0、Probe0。9份变化源码行号SHA/中文说明/待审账本与验证公开；用户架构和手感仍待审。旧解禁删除保留不提交，无第三方/用户媒体/Gitee；停止M02C不进入M02B/M03。全部Unity/代码/文档写租约释放。

2026-10-20 M02C-R1 active/unity-writer。用户明确执行v1.0通用跟随能力重构；基准653f0f1本机远端一致，旧禁编asmdef/meta本机删除不提交。独占Runtime/CharacterFollow新增、Contracts/Root/Selector必要接线、Director/DebugView缩窄、共享自有PlayerCharacter.prefab、M02C新R1验证场及必要迁移、Candidates/M02C/Tests本轮测试、docs/ossastra/M02C-R1-*、README/规则与本任务同步。原M01/M02A场、用户媒体/旧代码/渲染配置只读。当前自有M02C Play clean无PrefabStage，允许先测旧行为再退出。无并行Unity写租约，保持唯一Current/Frame/运动和动作执行权；无AI战斗/寻路/M02B/M03。

2026-10-20 M02C-R1 TECH_VERIFIED/docs-writer：运行时重构/Editor Prefab迁移已完成，Unity与代码写租约释放；仅保留本轮文档/README/规则/选择性发布租约。R1世界19、组件27、分配8、Selector13、M01 15、M02A26、原M02C19、纯策略13/核心29分别通过，源码/待审入口已写。最终R1 Edit clean/角色2各7引用齐/MissingScript0/Probe0，Prefab GUID保持；保护9项SHA不变，仅授权Prefab内容变化。准备R11发布，无Gitee/自动下一阶段。

2026-10-20 M02C-R1 CANDIDATE_PUBLISHED/complete/read-only。实现e5819ab、发布记录6263eda4b85c50a5b2232d31fd811e666fbef3c5 已推送GitHub OSSASTRA/main并原生核对远端一致。跟随策略/历史/参数归各角色CharacterFollow，Director只目标分配/绑定，Root单入口/原动作运动链保持；7变化源/Prefab增量/独立R1场/真实合同/集中待审账本已发布。功能和架构人工审查仍待用户，未M02B/M03/战斗AI。最终R1 Edit ready/clean，保护9项SHA一致；仅授权Prefab内容变化。旧解禁删除保留未提交，不Gitee/媒体/第三方发布。外部审查ZIP46payload逐项SHA核验，路径work/OSSASTRA/M02C-R1-20261020/OSSASTRA-M02C-R1-review-6263eda.zip（不是可独立运行完整工程）。全部Unity/源码/文档/发布写租约释放。

2026-10-20 M02B1 active/unity-writer。用户明确执行v1.0，基准6263eda本机/远端一致；当前R1 Edit clean，无PrefabStage/并行Unity writer。独占本轮必要Core/Runtime生命、近战、输入/控制接线、新Enemies/Runtime与Development战斗验证、新M02B独立场、共享PlayerCharacter.prefab、Candidates/M02B1、docs/ossastra/M02B1-*、README/规则与本任务同步。保护四旧场/媒体/旧代码/原稿/开发细则；旧解禁asmdef/meta删除保留不上传。只B1真实HP/双向攻击/死亡与交接，无B2/返场/M03/正式AI，R11验证后GitHub main发布，禁Gitee。

2026-10-20 M02B1 TECH_VERIFIED/docs-writer：B1真实世界最终22/22、边界15/15、核心战斗13/13、旧核心29/29、跟随13/13；R1世界19、M02A26通过；M01原14/15且过期来源名夹具定向适配1/1。反馈空block真实异常已修复，编辑器焦点/缓冲夹具失败原件保留；最终临时InputSettings完全恢复原资产、设备0。新B1 Edit clean/双角色各8引用齐/敌目标2/MissingScript0，四旧场/Prefab GUID/input/指定素材8项SHA不变。代码/Unity写租约释放，仅持有本轮文档/选择性发布租约；旧解禁删除保留，准备R11 GitHub，禁Gitee/B2/M03。

2026-10-20 M02B1 CANDIDATE_PUBLISHED/complete/read-only：实现f39ffef97e1f3b405429f7f93ceeddf7e24dadf7、发布记录a72a7f6414c7efe4c738c7b7ad1bd5526bcf78e8 已推送GitHub OSSASTRA/main并核对远端SHA一致。B1真实世界22/边界15/纯战斗13/旧核心29/跟随13、R1世界19/M02A26通过；M01原14/15+夹具定向1/1，真实失败记录保留。最终B1 Edit ready/clean，8项保护SHA不变，仅授权共享Prefab生命增量。12源行号SHA与待审账本公开，人工架构/手感仍待审。审查ZIP E:\WORLDCREATOR\XingHaiHuiLang\work\OSSASTRA\M02B1-20261020\OSSASTRA-M02B1-review-a72a7f6.zip 共66 payload逐项SHA通过，含本地截图不上传；非完整工程。既存旧解禁删除保留未提交，无Gitee/用户素材/第三方发布；停止B1无B2/M03/正式返场。全部Unity/源码/文档/发布写租约释放。

2026-10-20 entity-root-boundaries docs-writer：用户要求自机/其他NPC/敌方使用不同根与各自标准，参照灰烬之国。仅登记明确规则和只读核查，拥有OSSASTRA_RULES.md与本任务同步；不改生产源码/Unity，不自动启动B2/M03。基准a72a7f6；原有旧禁编删除保持。

2026-10-20 entity-root-boundaries complete/read-only：R21登记自机/NPC/敌方不同根与组装标准。当前敌人独立MonoBehaviour，Root[]只是玩家目标引用；共用Vitals/Melee/动作结算，仍耦合玩家目标类型。Cinderia本机CRAR01/CR04静态研究确认玩家输入与敌方AI分层、共享技能/伤害入口；NPC完整架构未证。B1§10明确B2/M03另行制定，本机未发现可直接执行的下阶段详细任务单。本轮只规则登记/答疑，无代码/Unity更改，未把建议当已批准实现；全部租约释放。

2026-10-20 ARCH01 active/unity-writer：用户批准v1.0及重开未保存B1现场。基准b596f97本机远端一致。拥有Combat新模块、Characters必要契约接线、EnemyRoot/Brain、NpcRoot交互、Development、新ARCH01场、受控迁移B1场、必要共享Prefab/测试/本轮文档。保留旧解禁删除。采用A路径；其他旧场、用户媒体、全局设置只读。R11 GitHub发布；不B2/M03。


2026-10-20 ARCH01 CANDIDATE_PUBLISHED/complete/read-only：按v1.0三根/中立战斗落地。实现a8a9fdf911e846961b8865c0efbe236340c749f1，发布记录718b2385204251b423fef7f0d38da8ba1a1de810，GitHub OSSASTRA/main远端完整SHA已核对。EnemyRoot独立唯一时钟+StationaryEnemyBrain，中立CombatVitals/Melee保GUID迁移；NpcRoot自身交互真实回执。B1场受控迁移A路径，非字节不变。技术：ARCH世界22/边界15；新增中立NPC初始20/21（source-disabled前摇未取消缺陷）修复后23/23及加严24/24；最终B1世界22；M01/M02A/M02C/R1共享定向3/5/5/5、R1真实世界19；NPC纯9。真实失败与测试/编辑器编排错误在verification分类，不声称一次全绿。最终新ARCH01场Edit clean，Missing0/Probe0/原InputSettings/仅原键鼠笔设备；三份移动meta、共享Prefab/meta、四早期场/PRE00/InputSettings共11项SHA不变，仅授权B1变化。12份源码行号SHA/待审账本与审查/验证/publication已发布，人工手感架构仍待审。本机旧解禁asmdef/meta删除保持未提交；不Gitee/用户媒体/第三方/binobj。释放全部Unity/源码/文档/发布写租约，停ARCH01不B2/M03。ChatGPT引用链接为登录墙，未读取不可见正文。截图仅本地work/OSSASTRA/ARCH01-20261020/arch-game.png。

2026-10-20 animation-direction complete/read-only：已核查新Root明确Read→方向→Request/Advance/命中→Movement→Present，当前三片段View无正式意图/路由/过渡。旧VELGITA真实Composer/Mapping/Override/Transition/Handle链路与CRAR01§5只读证据对照；记录R22正式能力要求、单一ActionId/时间线建议与原作未知范围。无代码/Unity更改，全部文档租约释放。

2026-10-21 M02B2 technical-complete/publication-pending：共享PostureState/CombatPosture接入两根唯一时钟和原Release/HP事实；独立B2场，旧场/Prefab78项SHA不变。纯23、旧核心29/战斗13、最终世界22/边界26、旧ARCH/B1各22、R1真实19、M01/M02A定向3/5均通过。根禁用新硬直漏洞真实RED后修复。NPC近远回执正确。最终B2 Edit clean、Missing0/Probe0、原输入设置与设备。10源码完整带行号/哈希待用户审查，verification明确未测项及B2-18仅继承敌方LOS范围。旧禁编删除保持未提交。只GitHub；不推进B3/M03。Unity写租约释放，发布文档租约暂持有。

2026-10-21 M02B2 CANDIDATE_PUBLISHED/complete/read-only：实现f4cc13287059b02b69b716ab5989b166fcd02724已推送GitHub main并远端核对；本轮50文件，不含跨项目历史同步总表。全部源码/Unity/发布租约释放。用户架构和手感待审；停B2。

## 2026-10-21 ROADMAP01 收口
四阶段实现已推送：cc07189 / 6adef5d / a70973d / e824e3d；集中入口docs/ossastra/ROADMAP01-review.md。保存的综合场Assets/OSSASTRA/ROADMAP01/SC_ROADMAP01_CombatLoop.unity包含场景返场、可选两段、语义动画、独立有限敌动及NPC。各阶段技术Gate通过；旧场/Prefab保护74项无变化，旧asmdef/meta删除保持用户现场。Unity Edit clean；全租约释放。用户集中审查未进行，正式媒体/完整旧系统未迁移，不追加下一阶段。


## ROADMAP02 收口
2026-10-21 ROADMAP02 CANDIDATE_PUBLISHED/complete/read-only：S5 35b9a5a / S6 1f53cc8 / S7技术03bee68 / S8 bf0ff977d3df65bd1131c65207fab8fcb3405422均GitHub main远端核对。新场SC_ROADMAP02_TeamCombat Edit ready/clean；Missing0/Probe0/测试设备0/原InputSettings/预警材质0，76保护SHA不变。S5纯10世界11边界8；S6纯8世界12；S7纯10世界11新场连段14；S8自然12保存回开再12、边界2；旧B2 22/R1 19/M01定向3/M02A定向5/核心29。15份源码行号SHA公开。S7正式敌美术ASSET_BLOCKED，本地Spine测试替身；人工手感和架构待审，未迁完整职业/技能/敌人。旧禁编两删除保留不提交；无Gitee/商业媒体。全部Unity/源码/文档/发布租约释放，停止ROADMAP02。

2026-10-21 companion-contracts：两个伙伴候选接口移至CharacterContracts，签名/行为保持；Unity刷新ready、无近期Error、反射接口检查通过，ROADMAP02场clean。小蓝已有4.1.23运行时导出3项SHA匹配，媒体本地只读。详见docs/ossastra/companion-contracts-20261021.md。

2026-10-21 ROADMAP03阶段暂停：S9共享防御网关7b2a104已GitHub发布；S10体力/Shift闪避保存本机（纯18、真实18、连段15、旧连段14），未发布。prepare_editor因OPERATION_JOURNAL_UNAVAILABLE/日志tmp共享占用受阻，退出Play/干净域重载未恢复。S11–S13未开始。119保护SHA不变，旧禁编删除保持；写租约释放，接手先读docs/ossastra/ROADMAP03-publication.md。

- 2026-10-21 ROADMAP03 收口：S9-S13完成技术Gate，角色资源/盾/独立炸弹与场景共享Y/一次性G篝火只装配新ROADMAP03场。最终Edit clean；用户旧禁编asmdef删除保持。详见任务与ROADMAP03-verification，人工体验待审。writer释放，不开启ROADMAP04。

2026-10-23 VC02A complete/released：产品 a5f2df8b6c5cc0c532c0b921447bbc432c321abc 已GitHub main核对，角色动作组/六技能/三根17分支10符文/独立装备Spine/正常HUD链路完成技术验收。127纯合同、56自然阶段、旧场15；保存重开/域重载9、定义无污染、writer释放。正式美术/人工审定未完成，ART_ENTRY_READY=false，停A不执行B。用户既有dirty保留；用户取消关机，不执行。详见docs/ossastra/VC02A-handoff.md。

2026-10-23 VC02B active：用户明确连续执行B0-B8。新独立SC_VC02B_IntegrationReady，库存36纯/初始化9/开包移动拖放真实输入20/药与五食物实际30通过；B4/B5新接线待验，B6-B8未完成。唯一unity-writer仍归本任务，四既有dirty保持；不Gitee/不关机。详见tasks/2026-10-23-VC02B.md。

2026-10-23 VC02B 技术完成/发布准备：B0-B8包内Editor闭环完成。库存唯一实例/后备消费者/地面全灭/独立Spine/Rig及作者工具，31目录与31有限初始实例；自然47/60/35、UI57、A纯127/A自然56/旧场15、域重载13通过。A213原生资产SHA不变。实际追加Player构建被旧Unity-Favorites-Tool Editor类型误入Player阻塞，未修改第三方。ART_ENTRY_READY=true仅Editor契约审查，正式素材未完、用户规范未冻；详见VC02B-handoff/readiness/verification。四既有dirty保持，发布完成后释放writer。


2026-10-23 VC02B complete/released：产品a2fa0cd9e9efa98eba3f3567c6704f417f6bfb16已GitHub main远端核对。347 payload指纹一致，351自有文件；Editor技术准入完成，Player旧第三方构建阻塞单列。最终SC_VC02B_IntegrationReady Edit clean/原输入设备，无MissingScript。四原dirty保持，全部writer与发布租约释放；不自动ART01，不关机。详见VC02B-publication.md。


2026-10-23 功能审查工作台：docs/ossastra/OSSASTRA_功能模块审查与落实清单.md 已建立24模块、72待审验收项和5跨模块复验链。候选功能实现与架构审查状态分离，正式审查均未代用户勾选；建议从M01-M04根/数据/输入/仲裁开始。仅文档，不是下一轮代码调整授权。


2026-10-23 input-layout：共享PlayerInputLayoutSO成为B角色与场景输入源；左右手字段RightHandAction/LeftHandAction具旧字段迁移兼容；Space/B闪避、QER/LB面键、Y/LB+A、Z/LB+RB、Tab/Start、Select非暂停菜单、LT+RT动作组切换、RB四格引用。默认扳机组合宽限0.08s可配，仍沿原模式切换资格；投掷瞄准快捷使用明确延期。26输入合同/8实际场景通过，真实硬件长测未做；旧输入override清理，全部writer释放。


2026-10-24 AM01 complete/AM02 active：ed74ae1af1698306cd44f5fdb7a871dbc1bbf3b7已GitHub main核对。正式StateArbitrator登记五状态，Combat实际拥有唯一Runtime/连段/事务受理转发/事实分派，运动和并行能力保持原时序；B与候选Prefab接线，旧参考不变。自然17/并行5/生命周期13/纯68/背包47通过，InventoryView销毁回归RED→GREEN；原动画链尚未迁入，继续AM02/03。用户已有dirty仍保留，唯一Unity writer由本任务持有。

> 2026-10-24 AM01–AM03技术完成：AM01 ed74ae1、AM02 67efa5c已GitHub；AM03会话级移动/瞄准独立、配置Action按设备采样、UI按下归属/真释放保护和实际能力目标接通。来源24、综合36、原生E目标7、双手9、举盾开关包13、生命周期13、背包20+手柄拖拽20通过；作者工具3项RED→GREEN。最终B Edit clean/用户VELGITA Stage clean/Missing0，身体双手与Enemy/NPC真实播放有效；旧代码参考及用户键位dirty保留，实体XInput设备保留但实机操控未测。交接docs/ossastra/AM01-handoff.md、AM02-handoff.md、AM03-handoff.md；AM03 faf2938已GitHub精确核对，整轮complete/全部租约释放，不Gitee。用户明确要求整轮验证发布后正常关机。

> 2026-10-24 AM复核定向修正technical-complete：F01-F06及Movement已RED→GREEN，完整快照/正式Router/装备条件/持续设备凭据/背包不补RT/同帧pulse落正式链；六技能25、混合36、举盾13、播放22等通过。仍为注册式主干，不宣称所有旧子状态对象已恢复。最终B Edit clean/Missing0，实体手柄体验与旧Player构建保留；GitHub定向发布中。见ossastra/AM01-AM03-directed-fixes.md，Unity租约释放，仅本任务文档/发布租约。

> 2026-10-24 AM定向修正complete：c7feaf14c5bd1e7542e79924529c24c712f1fe08已GitHub main精确核对（API逐Blob/树/Commit一致，expected-sha无强推）；F01-F06与Movement及相关回归完成，B Edit ready/clean，所有租约释放。不Gitee/不重复关机；用户旧dirty保持。交接docs/ossastra/AM01-AM03-directed-fixes.md。

> 2026-10-24 CONFIG-01技术完成：根Inspector实际view导航与只读已执行信息；Workbench schema3重导164SO/3模型755条，326依赖指纹/307引用GUID对账；当前配置四表/一页样板同步，72用户审查勾选原样。A/B导航13、保存恢复5、小流程19及导出日志RED→GREEN。仅Editor/资料，35原dirty保留，B Edit clean，非实体手柄验收；GitHub定向发布中。交接docs/ossastra/ArtIntegrationCandidate/ConfigurationHandoff.md。

> 2026-10-24 CONFIG-01 complete：产品448ccc5e16255c333455dee8fc033b7e18641d6e已GitHub main核对；A/B根已可直达实际Map/Router/Rig，当前配置说明与可操作样板已交接，末次19步确认身体与左右装备真实轨道播放。B Edit clean/Missing0/无临时输入设备，35原dirty保留，全部租约释放；后续由用户选定具体配置逐模块审查。

> 2026-10-24 DIR01开工：用户附件明确模型目标改为多面，局部替代R24，保留旧仲裁器与原式动画SO。仅研究/隔离可视工具/接入草案，不修改正式运行时或生产资产。

> 2026-10-24 DIR01研究技术完成：776骨/494slot/138动画，官方CLI副本拆图及4.3原工程读取；方向根位移＋原作Late矩形slot显隐已静态闭环，非仅源裁切。独立29+6检查/300姿态、192拆图通过，正式Composer清另一owner方向轨已隔离复现。仅报告/工具/DIR02草案，未接生产；D03/D11/D14未穷举或未正式接入。B Edit ready且dirty保留，35磁盘dirty保护；Unity/源码租约释放，仅文档定向发布。

> 2026-10-24 DIR01 complete：产品14715e763e4ab92936aa7f26b49ca34fb457c744已GitHub main精确核对。自有研究台与三方报告/DIR02草案交付；D03/D11/D14边界详列。B Edit ready/内存dirty保留，35原磁盘dirty与素材SHA一致；全部租约释放，不自动DIR02，不安装MCP/不Gitee/不关机。

> 2026-10-25 DIR02 technical-complete：小蓝4.3五面Variant与SC_CharacterPresentationCompare已实际接入，A小蓝/B原rozeul；原式动画链的合法结构层、每实例显隐、30条动态Rig与同装备换锚点完成。42输入/1200姿态/10生命周期/16实战/36混合/5原生E验证通过。仅候选，未覆盖主场，复杂穿插/近似动作PARTIAL，实体手感未代签。原36dirty与源SHA保护，最终Edit clean；GitHub定向发布中，Unity/源码租约已释放。见ossastra/DIR02-report.md。

> 2026-10-25 DIR02 complete：产品93eda092c894722a65beff6974217228c2f615dd已GitHub main核对。小蓝五面/rozeul候选与指南交付，原主场未替换、用户美术未代签；最终对照场Edit ready/clean，无临时设备，原36dirty与源SHA一致，全部租约释放。停在用户试玩与正式素材接入判断，不继续扩张。

> 2026-10-25 DIR02-FIX01验证进展：显式新规则替代DIR02静止右杆选面。Orientation统一最终显示方向，键鼠普通移动/鼠标攻击分开，手柄用移动/最近移动；普通未来投递消费同一政策，盾冲/跃击/闪避保留具名机动快照。小蓝身体0.08混合导致场外slot入中心已实际定位，Blue限定零mix A/B及1440连续帧验证通过，未全局改rozeul。历史攻击末帧目前NO_REPRO，不宣称唯一根因已修复。生命周期最终10项通过；最终动作组及重开交接仍在收尾，不沿用旧候选PASS覆盖新规则。

> 2026-10-25 DIR02-FIX01 complete：产品4fe18701e1c9ce6b3cd731b3ebd434c0eeabbcd0已GitHub main精确核对。键鼠移动/攻击鼠标分权、手柄移动定向及未来投递一致；Blue限定身体mix0修正场外分支滑入。历史末帧NO_REPRO/PARTIAL，不冒称已修；49动作组/29停止/43输入/36混合/15方向/10生命周期及1440注入帧定向证据已交接。原36dirty/素材不变，最终对照场Edit clean/ready，全部租约释放；仅用户复验，不自动DIR03。

> 2026-10-25 AUDIO-M01 technical-complete/publishing：独立Ossastra.Audio根/纯仲裁/SO/固定六源DSP与监控已在自有SC_BGM_AudioMigration验证。Core17/Mix12/Transport16/Host5与真实12项输入/音乐故事、原创三层/快速淡变、跨场与设置、缺媒体回退通过；原373保护路径/36dirty不变。Player仍被旧Unity-Favorites-Tool阻塞，用户听感及正式Title/Dialog/Boss未签。全部Unity/源码租约释放，仅定向GitHub发布记录继续，不推进AUDIO-M02。

> 2026-10-25 AUDIO-M01 complete：产品df90b9fe9ac7c831f791878408e495a6cc46e0bb已GitHub main精确核对，128文件只含独立BGM与原创诊断/证据。Editor自有新场Edit clean/监控已打开，全部租约释放、原36dirty保护。不代签用户听感，Player仍第三方阻塞；停于AUDIO-M01。
