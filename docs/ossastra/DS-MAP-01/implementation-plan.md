# 饥荒地图样地与角色视角适配实施计划

> **For agentic workers:** 采用本任务已分配的并行源码/转换执行，root唯一Unity writer；阶段之间按真实测试结果接续，不重复请求实施许可。

**Goal:** 在正式工程新建可操作研究场，使用少量真实饥荒地表/物件和现有完整小蓝角色，验证45°观察旋转、原动作链与空间结构。

**Architecture:** 地图根与碰撞固定，场景相机先更新实际姿态；可选角色视角模块由原CharacterRoot初始化/调度/释放，只转换玩家有效意图与派生显示方向。地图程序集仅依赖角色窄契约，不依赖具体Root或原作代码。

**Tech Stack:** 团结2022.3.62t16、URP14.2.0-t1、InputSystem1.14.4-t1、Spine4.3。只用现有运行库。

**Spec:** 用户v1.1完整计划，SHA256 B326076AF9A0F9E73AB53C56528000EDE098E3C0DC7F330457710D0D62418D8E；最新用户修订滚轮/箭头代替Q/E。本机计划位于外部研究根plan-v1.1。

## Global Constraints

- 原作只读，只提取地表/场景物件所需文件；不提取人物/音频、不写原存档。
- 角色仍由原输入、仲裁器、移动/朝向、动画SO与战斗唯一驱动；不新增人物Motor。
- 地图与人物源模型/基本单位不全局调整；默认场景、旧Prefab和正式Q/E技能绑定保持。
- 媒体/原作源码/派生图像及几何仅本机；不进入BuildSettings或Resources。
- 验证B级，V01–V19按实际证据列出，不代签用户美术/架构验收。

## Task 1：来源研究与最小转换

外部`00-baseline/01-research/02-selected-source/03-converted/tools`；先核实安装身份、相机/地形与物件定义，转换一个完整静态物件核对图像/锚点，再扩大至2–3地表、6–8物件。输出来源/输出SHA、比例、帧/pivot/依赖manifest。先核对魔数、版本、工具源码/许可证，不全量解包。

## Task 2：可选角色视角能力

新增`Scripts/Characters/ViewSpace`独立程序集；共享改动限`CharacterRoot/CharacterIntentSelector/CharacterDirectionPresentation`及必要动画只读消费点。先执行`tests/ViewSpaceContracts.cs.txt`，缺能力应产生明确FAIL而非夹具编译错误；实现后同夹具转绿。

```csharp
// 地图程序集可见的窄契约；无具体Root、场景或饥荒类型依赖。
ICharacterViewContext: GameplayCamera, IsValid, ViewRight, ViewForward, ViewRotation, FreezePresentation
ICharacterViewParticipant: CharacterTransform, ViewDiagnostics,
    BindViewContext(owner, context), ReleaseViewContext(owner), SetViewInputCapture(owner, captured)
```

在Selector的Player分支取得唯一有效帧后，转换move；仅Gamepad来源的aimDirection需要转换，AI/跟随和鼠标已世界向量不转换。Root在ManagedUpdate前缓存实际相机基，表现从世界方向派生观察相对方向。禁用/无模块/无上下文回退旧路径，攻击受理快照不受相机改写。

## Task 3：研究相机、物件与观察模式

新增`Scripts/MapResearch/Runtime`独立程序集，依赖ViewSpace窄契约、InputSystem。先执行`tests/OrbitContracts.cs.txt`红测，再实现八档/实际平滑角、短路径和输入生命周期。

滚轮上下或未占用手柄DPad左右一次45°，Q/E保留技能；PageUp/PageDown缩放、Home重置、F6结构模式、F7侧看、F8俯看。动作均Inspector配置。场景根顺序-300先更新正常相机和视角快照，人物Root-100再推进原链；观察相机不成为攻击相机。结构模式冻结正常显示平面，经原owner机制捕获输入，退出精确恢复；不改TimeScale。

## Task 4：单一新场景与素材配置

root通过Editor API创建专用材质、对象Prefab、手工样地与`Scenes/Research/SC_DontStarveMapLab.unity`。人物继承既有BlueFiveFace与其已保存完整角色模块配置，排除队伍/贸易等无关场景所有者。地形保留真实纹理与至少一个可解释边缘交界；物件世界根、可转显示、脚点、碰撞和来源分开。少量自有3D灰模只用于混合遮挡诊断。

## Task 5：真实运行与交付

V01–V19：八角度移动/静止旋转/过渡、攻击与连段/适用闪避、费用和命中不重复、冻结观察平面、保存重开、3轮出入、自有资源计数、原主场定向回归。录制一段旋转/移动/动作短片，核对实际帧；性能记录区分Editor和最终构建。缺实体手柄/原作实机证据分别标未验，不以虚拟设备代称。

最后保存新场景并核对保护指纹、媒体忽略及暂存范围；自有代码/配置/研究记录按R11定向推GitHub，本轮结束后释放全部租约。
