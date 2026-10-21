# ROADMAP02 验证记录
基准9e10cb3，Unity2022.3.62t16，工作树含用户旧asmdef/meta删除，不包含本轮提交。
## S5
纯策略结构RED：CompanionCombatPolicy缺文件CS2001；GREEN 10/10。
真实InputSystem+Root+CharacterController：team-world 11项通过；实际敌HP72→60(玩家)→48(伙伴)，两方切控与只Stage1，暂停/资格撤回。
边界8项最终通过：敌Accepted入战、墙后候选、锚点、死者、失焦、死敌、平静脱战、权威禁用。
初始边界测试未形成有效攻击：重置与按下同帧未采到松键，敌冷却等待不足；失败原件保留，修正夹具后重测，不改攻击参数。
初次Unity编译因旧全局EnemyRoot命名遮蔽失败，限定Ossastra.Enemies.EnemyRoot后通过。
切控前Accepted动作仍可完成一次Release；不继承AI连段，AI无Stage2。手感/架构仍待用户。
S6/S7/S8未开始。

S5受影响旧ROADMAP01两段真实场14/14通过；新场保存回开；保护76项SHA一致。

## S6
纯语义结构RED后8/8通过；真实世界12/12：Accepted可视、关闭视图同伤害20、墙取消、暂停/禁用清除、两个LineRenderer复用。只读根快照，不增加时钟。
