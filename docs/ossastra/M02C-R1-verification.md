# M02-C-R1 验证与待审状态

基准653f0f1，团结2022.3.62t16、Spine4.3.26；本机工作树含用户此前解禁旧代码的asmdef/meta删除，保持原状且不纳入本轮发布。不是干净克隆或全项目认证。验证等级C（共享Prefab/控制权/生命周期），按B级规模定向执行。

## 合同矩阵
| 计划项 | 状态 | 实际证据/边界 |
|---|---|---|
| T01 每角色能力归属 | PASS | ownership RED→GREEN，实际provider由同一个Director变成两个本体Follow；component合同确认策略独立 |
| T02 A玩家/B跟随 | PASS | R1实际键鼠/Spine/CharacterController世界合同 |
| T03 B玩家/A跟随 | PASS | 世界切换合同，唯一Current沿用 |
| T04 同帧屏障 | PASS | 世界末步SwitchNext当帧两个Bus均零/Neutral；Selector来源合同 |
| T05 Held/旧动作 | PASS | 世界真实Held切换不补打、真松键再按、旧ActionId/HP一次 |
| T06 MoveReady | PASS | 世界前摇不可跟随、Ready后原运动链接管 |
| T07 目标取消/换人 | PASS | component目标A→B→A、无目标/自目标/解绑/暂停，清旧受阻历史 |
| T08 独立Blocked | PASS | component实际本体策略A受阻不影响B；世界实际墙体碰撞、停止/目标移动重试，无绕墙/传送 |
| T09 禁启/销毁 | PASS | 世界Director/Coordinator/Root禁用；component单Follow/目标/Root销毁及暂停；assignment分配者/会话销毁，立即零帧 |
| T10 第二Owner | PASS | assignment第二Director/失败Coordinator均拒绝且保留原合法Frame；外来角色读源及解绑不能破坏owner |
| T11 M01旧场 | PASS | 保存的原M01场实际15项世界合同通过；继承Follow但未分配，无自动跟随 |
| T12 M02A旧场 | PASS | 保存的原M02A场实际26项世界合同通过；无Director/无assignment，非实控不跟随 |
| T13 配置/序列化 | PASS | 旧参数实际回读等值迁移，Prefab GUID保持、R1保存重开、三旧场字节保持；最终审计见evidence |
| T14 Debug观察 | PASS（定向） | 组件Observe反复读取不推进策略；Inspector真实自定义Editor及面板本体状态读回；按钮启停通过相同组件入口测试，未用物理鼠标逐个点击认证 |

## 分项结果，不相加为“全套总数”

- 原纯跟随策略13/13、原领域核心29/29。本轮没有修改策略数学、Movement、Bus、Orientation、Spine、InputHandler、Coordinator、动作核心或Health。
- R1场世界19项通过；原M02C场19项也通过，旧场只是同一新Prefab/源码下的序列化兼容回归，不是假装独立旧程序。
- 角色组件27/27、分配生命周期8/8、Selector13/13分别保存日志。角色合同使用真实共享Prefab/Follow/Selector，假会话仅提供目标和代际；世界合同使用实际Coordinator与物理运动。手动连续采样用于受阻状态边界，不冒称实际墙体；实际墙体另由世界合同验证。
- 原M01 15项、M02A 26项在本轮运行时源码通过；随后只新增Editor只读Inspector，不改变运行时。没有反复全跑不受影响的旧输入/Bus合同或所有旧场。
- RED是实际旧provider共用导致“不具备独立可禁用角色能力”的失败，不是缺类编译失败。另有assignment测试命令漏InputSystem扩展命名空间、一次错误SessionState键和不适用日志过滤参数；均为夹具/查询失败，保留原回执，不计业务RED或PASS。参数名target_mode误用只得到默认Edit，实际Play均按target=play并核对ready/is_playing后执行。

## 保护、范围与人工待审

共享Prefab是明确授权变化：只加Follow组件/Root引用/4阈值。其meta GUID、M01/M02A/M02C旧场、InputSettings、指定FBX/Spine数据/旧VELGITA以及Core策略SHA保持，共9个保护项不变。原asmdef/meta删除始终保留。未改人工Excel、Origin开发细则、URP/媒体/第三方包。

自动设备、绑定override、临时对象均由各合同清理；最终Edit clean/引用审计另存。无Player构建、全项目扫描、物理手柄/键鼠体验或所有热修改场景认证。新Follow/Director没有直接Transform写入、Move/Translate、动作时钟或HP/Spine调用；源码定向检查及真实运动链合同互相印证。

所有本轮变化源码/接口/阈值/调试布局仍待用户审查。[集中入口](M02C-R1-review.md)。不将“功能正常”提升为架构批准，不自动开始M02B/M03。
