# M02-C v1.0 实施取舍与顺序
用户当前执行指令批准附件。附件SHA256 1E6F45D8C3E514F1B17B13CC56ACA43E8B2E3354962D11DA84DCA32DDD2012FC；基准8925b76本机/远端一致，自有M02A Edit clean，无PrefabStage，7个保护文件已存前SHA。
本轮沿用project-ankalute-memory、Funplay MCP workflow、brainstorming、writing-plans、TDD（已有同版原文）；这是已批准的架构接续，不重问局部名字。出现实际失败再定向诊断。当前Chat链接read_thread只取得用户消息和正文引用占位，不声称已读不可见全文，以完整附件与真实源码为实施依据。

## 一页权威/接线
- 原唯一Current保留在Coordinator；仅增加只读会话许可/代际接口，不增加选中真值或另一个成员名单。
- Root Awake构造一个plain C# CharacterIntentSelector(player)，Bus持有该同一输入源；未绑定Follow时委托原玩家源，旧场不动Prefab。
- FollowDirector是并列场景模块，Start后显式绑定Root的选择器（避免Awake先后依赖）。选择器惰性Read按Player/Companion/Neutral只选一源，不增加Update。
- 会话只读接口含Current、Characters、IsIntentSamplingAllowed、ControlRevision。现有Coordinator实现；输入Handler保留原玩家门控和物理松键。
- Director实现一个最小伙伴帧源接口；它只读取原会话，按每角色策略产生move，其余Frame字段全零。不新增CompanionIntentSource MonoBehaviour，同职责不机械拆两层。
- 纯CharacterIntentPolicy负责角色源选择，纯CompanionFollowPolicy只维护距离滞回/受阻诊断；无Unity引擎依赖。Root/Bus/Movement仍唯一执行位移与动作。
- 切人/失焦/暂停代际变化后CurrentFrame读出即失效；当帧共享屏障Player/AI都neutral，下一Root帧再采样。不假造物理松键，不取消旧动作。
- 非实控动作前摇不可移动、暂停/禁用/无Current/失焦均neutral；旧动作独立Advance，AI绝不attackHeld/cancel/pause/debug。
- 候选start2.4/stop1.6，沿原速度3；半径.22，安全间距足够。Blocked：有前帧实际跟随命令且水平位移近零累计.5s，停试；目标移动.5才重试。无寻路/绕墙/加速/瞬移；墙移开但目标不动需领队移动后恢复，明确有限样板边界。

## 对照与差异
原型T015/016以及CurrentRules后续EC09.5/T035强调唯一实控、玩家优先、无传送、普通自治可让权。完整原型包含软阵型、加速档、战斗/职业自治、寻路和死亡，均不本轮迁移。M02C沿本轮明确的固定速度、直接追随、Tab/Select和已接受动作继续，不搬旧Z键/赶队加速/本体互穿。

## 执行清单
- [x] 接手与保护；原型开发细则和本轮跟随/让权相关记录只读核对。
- [x] 先纯策略行为RED与旧双场不跟随的真实RED；再完成权威/路由/跟随。
- [x] Core策略→Runtime选择器→Coordinator只读接口→Root最小注入→Director场景策略；中文注释同步。
- [x] 新M02C场从M02A保存副本，绑定Director与Development面板，不改源场/Prefab/相机/素材。
- [x] 纯策略/旧核心、世界P01–P12与M01/M02A必要回归；保留失败原件，定时测试不并发阻塞Unity。
- [x] 本轮修改源码行号SHA/实际图/权限/注释/待审索引、保护SHA复验，Edit clean。
- [ ] R11选择性提交GitHub main/远端SHA核对；停M02C，不M02B/跟随战斗AI/连段。
