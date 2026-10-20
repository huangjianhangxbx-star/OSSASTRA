# M02-C-R1 执行计划

目标：保持M02C玩法语义，把跟随能力和运行态归还各角色。用户明确执行附件v1.0；SHA256 B8512F38A89184B541C894195C888F5998FE3F5981936019493BDEC14D1714FD。基准653f0f1本机/远端一致。

本轮应用project-ankalute-memory、Funplay MCP workflow、brainstorming、writing-plans、test-driven-development及writing-good-tests。已有批准的架构和实施计划直接执行，不重新请求相同设计批准。缺少superpowers执行子技能时按本机实际工作流逐项内联执行，不冒称调用不存在的技能。Chat链接仅用户消息可读，助手正文为content-reference；依据完整附件与真实源码。

## 状态迁移与调用
| 原所有者 | 数据 | 新唯一所有者 |
|---|---|---|
| Director.Entry | policy/lastPosition/revision/state/reason | 每角色CharacterFollow |
| Director字段 | 起停/受阻/重试参数 | 每角色Follow Inspector；旧字段隐藏保留只供历史迁移识别 |
| Coordinator | Current/成员/控制代际 | 不变 |
| Selector | 唯一有效Frame | 不变；Root只接自己Follow |

Coordinator→Director.TryGetFollowTarget→本角色Follow.ReadCompanionIntent→Selector→Root/Bus/Movement。Director只存已取得绑定引用用于归还；无角色策略字典。可选Follow通过Root显式Initialize，未分配绝不跟随。Follow禁用清自身历史/有效帧；再次启用后从仍合法的分配源重新查询目标并全新采样。Director解绑则移除分配，重新绑定才可恢复。目标身份及ControlRevision任一变化均清历史。Observe不推进策略。

## 步骤与验证
- [x] 接手保护、真实旧路径RED：A/B实际共享场景Director provider，未满足独立可禁用能力；旧阈值2.4/1.6/.5/.5回读。
- [x] 新增Runtime/CharacterFollow.cs与窄ICharacterFollowTargetSource；原纯Policy/Movement/Bus/动作/输入不改。
- [x] Root可选Follow接线；Director预检全部成员，逐项取得自己的绑定，失败回滚自己；Debug读本体并提供单角色开关。
- [x] Editor API共享Prefab加Follow/Root引用；复制干净M02C为R1场，旧场字节不改；回读阈值迁移并验证保存重开。
- [x] 纯策略/核心，真实R1场T01–T14及M01/M02A/原M02C定向回归；成功/失败日志分别保留，成本C级定向按B级规模。
- [x] 变化源码完整行号/SHA/中文说明、集中审查入口、待审账本、保护与Edit清理。
- [x] 选择性GitHub main发布、远端SHA、释放租约；不Gitee、不自动M02B/M03。

实现不写新动作时钟、不直接位移、不寻路、不攻击；用户媒体/旧代码/用户原稿/开发细则不改。共享Prefab的变化明确允许，M01/M02A未分配组件保持原行为。
