# KT-VIS-01 实施顺序与技术取舍

本文件保留实施顺序；各步骤的实际完成状态、失败历史与范围以 [validation.md](validation.md) 为准，不用计划文字替代验收。`diagnosis-and-fix.md`说明最终因果与修法。

**目标：** 关闭效果时可独立定位两类截身；正常角色后方的登记模型可局部柔边透视、扩大、有限整组淡出并恢复，保持人物和玩法权威。

**架构：** 角色侧窄目标适配只提供脚点、稳定身体范围、存活与原Current优先级；SceneVisibility维护未淡出代理的多点多命中与逐目标/组状态；相机渲染适配拥有自己的显示资源和当前GameplayCamera作用域。已有Tilemap仍唯一作者，新场沿同一平台构建/保存链。真实渲染路线在单墙Native反馈通过后冻结，不以草稿等同成品。

**技术：** Tuanjie2022.3.62t16 / URP14.2.0-t1 / Spine本机现有版本 / D3D11 / Unity新InputSystem。当前Renderer0 Forward、DepthPriming Disabled、CopyDepth AfterTransparents、无RendererFeature/SSAO，Base Camera无栈/后处理，1600×900、35°/near.05/far180、HDR及MSAA4。

**规格：** 用户KT-VIS-01 v1.0包，SHA256 `49F631A4BDD3A41A0D213AEDA613CA96161B3B80CB17D8D34BA02ECBEA214F4C`；要求映射见requirements.md/V01–28见validation.md。本轮已获执行授权，按推荐局部取舍连续推进，不重复询问阶段确认；用户原稿与开发细则不修改。

## 约束

- 保留3b3c403上的FIX01统一平面深度；附件6f80776只作历史基准。
- 每个症状先取得正确反馈信号再修，不用开启透明为截身签收。
- 不改人物alpha/置顶/碰撞/攻击/动作/Camera控制；必要可选视觉校准必须沿原Root/适配所有权且旧场默认无效。
- 不改全局管线/Renderer列表/Packages，不新增冲突快捷键；所有Unity资产由Native API写。
- 原37dirty/源场和原Map只读保护；本轮新Map、材质、Boundary、缓存独立归属。原角色Prefab/第三方媒体不改。
- 只测相关模块与必要旧场，不跑无关退休系统；真实新场保存重开/重建/三次Play不可省。

## M0：隔离场与资格链

文件：GroundSurfaces/Editor中新`GroundSurfaceSceneAccess.cs`及Authoring/Updates/Persistence窄修改；SceneVisibility/Editor构造编排、Candidates/scene-access合同。

- Availability Native RED：缺资格helper，0pass/1fail，源场不改。
- 在源场仍clean时Native另存副本；保存目标path与源不同则回调不得持久化源Map。记录源scene/Map SHA及原路径/dirty。
- 只打开本轮新快照，复用真实注册服务一次事务复制三Map、Tile/Mat/Theme/装饰身份；Boundary及可变模型材质新场私有。
- 验源文件SHA未变；应用精确资格helper，只接受两个既定Lab，保留旧ScenePath常量。
- Native新场GetBindings/实际SaveLab、错误注册/无权场拒绝为GREEN；新场保存路径为当前scene.path。

注意：原服务脚本重载会自动重建已打开源Lab并标dirty。因此先保存副本并在源clean时切入新快照，再导入旧服务代码；不为清理自动dirty替用户保存原场。

## M1：截身反馈

文件：Candidates/KT-VIS-01/diagnostics正式网格事件探针；如因果确认需要，才改MapResearchIntegration可选显示校准与对应角色侧窄组织。

- 新场关闭可见性功能，按原输入/原相机恢复截图yaw180及270及合法邻域坐标。
- 在SkeletonRenderer.OnMeshAndMaterialsUpdated之后记录当前引用三角形的身体/装备世界几何、材质/alpha和脚点/支撑；隐藏面/未引用顶点不计身体。
- 使用真实Move/接触、身体单独/地面/墙/顶盖/装饰逐层draw建立对照；分别判断物理错误、视觉平面相交或合法遮挡。
- 确认存在异常再实施有界校准：固定物理脚点、沿现有视觉写者接入，不新增第二次Move/相机/角色根写者。
- 同一反例GREEN并跑受影响旧场；无法确认项明确保留，不伪称全部穿模消除。

## M2：先冻结单墙渲染闭环

文件：Candidates/rendering私有技术候选；通过后生产SceneVisibility/Rendering中的捕获与合成，以及Runtime中组/目标最小契约。

反馈：同正式身体网格与同机位，独立body-only提供预期身体颜色；普通厚墙遮挡为RED，效果核心应显露原身体且外围保持墙。羽化多档必须与单个最前可见表面的独立混合参考一致，中心/身体alpha未被本功能写入。

首选试验：有限组Color+Depth近表面捕获，两显示proxy分别承担opaque外侧和late smooth feather；真实逐像素深度与LEqual，中心不写遮挡深度。源组只在指定主相机作用域抑制原颜色draw，SceneView/结构相机归还；不改全局Renderer。多层先在组内正常opaque层序选择最前可见表面，避免叠成多张半透明膜。

该路线仍需实证：beginCameraRendering早于URP相机/灯光设置，捕获自带明确矩阵和RT参数，不能读上一相机状态；当前墙真实Lit，普通自有Unlit小样不能代签原Lit的光照。阴影按实际启用状态与自有影子代理/原渲染策略处理，不能让透视圈驱动光源视角。若路线不能正确混合/遮挡，不扩成全场，先修单墙；dither只留显式对照，不替换smooth目标。

## M3/M4：检测、状态、高台与双角色

预期生产责任：`SceneVisibilityRoot`组织；`VisibilityTarget`提供脚点/身体/存活；`OccluderGroup`登记Renderer/检测代理/支撑/有限整组许可；检测与状态独立于渲染，不读已挖洞的Depth来关洞。

- 先测试实际遮挡资格/多重命中、正交parallel rays、起点在代理内、缓冲溢出；再实现共享缓冲/多点查询，切人只变原Current优先级。
- 程度随覆盖/进入量而变，静止不随时间持续增长；逐帧平滑进入、半径/强度及退出滞回，不每帧造材质。
- 核心覆盖稳定身体，真实viewport等距像素度量，不由长武器/隐藏面扩成满屏；两目标需求并集。
- 高台源Binding动态解析新缓存，墙/顶盖/封口/遮挡附件同组，显式重建后无旧Renderer悬挂。
- 对每目标进行支撑资格保护；主控站立平台同时遮伙伴时禁整体淡出并退到局部前景，文档说明冲突政策。
- 验双组前后遮挡、角色在模型前/模型在角色后、切人、失活/死亡、离屏与恢复。

## M5/M6/M7：用户入口与有界交付

- 中文Inspector配置及当前新场诊断面板：总开关、检测/渲染状态、强度/半径、组预算/溢出、重置/检查入口；不另加冲突按键。
- 新场真实保存、关闭、重开；显式顶盖重建后继续可用；三次Play/禁启/切场资源收支核对，只恢复本模块仍持有的状态。
- 两种窗口比例/两档zoom、35°八方与连续变化、原InputSystem行走/攻击/实体墙受阻；真实源控制权和碰撞/结算不变。
- 同内容关/仅检测/局部/整件四态的CPU/RenderThread/Draw/SetPass/RT/Mat/分配；GPU不可取得就未测，不由Editor计时推FPS。
- 连续正常游戏录像、同机位四阶段图；记录事件驱动还是实体输入。原受限媒体仅本机，不能以AI概念图代验。
- 源场/原Map/用户dirty指纹、相关旧场冒烟、中文注释审查与模块接口复核；定向GitHub发布并fresh远端核SHA，释放全部租约，无电源动作。
