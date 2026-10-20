# ARCH-01 执行计划与P0 Gate

用户已批准附件v1.0；基准b596f9728f2666e2c7d13966fe9f22d45c5297d8，本机/远端一致。聊天链接当前登录墙，未声称读取不可见最新正文。本机AGENTS、同步协议及既有B1文档沿用；当前dirty B1经用户明确批准重开，现为Edit clean。

采用附件A路径：旧B1受控迁移，新ARCH01场同含双玩家/EnemyRoot/NpcRoot。旧StationaryMeleeEnemy保GUID变为EnemyRoot，行为决策提取StationaryEnemyBrain；不保留两套生产执行器。Vitals/Melee保GUID移动到Combat/Runtime并改中立名，纯Health/Release当前位置与命名空间暂保留；不改变算法或重建Health。四个早期场只复验不保存。

|所有者|本轮职责|依赖|
|---|---|---|
|CharacterRoot|原单Frame/动作/运动/死亡通知|中立ICombatLifeState/ICombatHitResolver；原玩家/Follow接口|
|EnemyRoot|唯一身份、时钟、Release、死亡/暂停取消|中立Vitals/Melee及自身Brain；无玩家类型|
|StationaryEnemyBrain|目标数组、距离/LOS、最近选择、间隔|ICombatTarget与CombatVitals可序列化引用；不Advance|
|NpcRoot|唯一NpcId、显式交互模块持有/销毁|NpcInteraction纯交互资格和事件回执；无HP/时钟|
|CombatVitals|唯一Health/阵营/反馈，活跃有效目标资格|中立契约；保持单通知|
|CombatMeleeHit|物理候选、活体身份、重复ID拒绝、单Release结算|IHitTarget/ICombatTarget；无玩家Root|

- [x] P0 接手/保护SHA/用户现场处理/小型Gate。
- [x] P1 RED：现有敌方targets类型不接受真实Vitals目标，结构耦合失败已记录。
- [x] P1/P2 中立目标、保GUID共享迁移、EnemyRoot/Brain；假null/禁用/过期身份/同ID明确拒绝。
- [x] P3 NPC公开请求经自身交互资格/事件回执；重复请求每次有效调用计一次，无自动对话。
- [x] P4 Editor API迁移B1并保存重开；新ARCH01场真实交互消费者。
- [x] P5 ARCH20合同/B1世界/定向旧场；失败分类原件保留。
- [x] P6 中文源审查/SHA/证据/README/GitHub main发布，释放租约停ARCH01。

初始共享GUID/Prefab/InputSettings/原场SHA已保存在外部work/OSSASTRA/ARCH01-20261020。只迁移本轮授权B1/共享自有引用，不碰用户媒体、旧禁编删除、全局渲染。已批准计划包含具体设计，直接顺序执行，不另要设计批准。
