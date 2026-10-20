# ROADMAP01 实施计划
基准108ea250928f8a8f1a338c8fbf0c976488598e61。四阶段已获本轮连续实施授权。采用原任务单S0–S4合同，逐段真实Play Gate和GitHub提交；不跨过失败Gate。

## S1 场景内生命循环
1. 先写RespawnSchedule纯时钟合同（单死10、全灭0.5、停钟、失败重试、唯一完成），观察RED。
2. Characters/Core/RespawnSchedule.cs只计算待返场资格；Runtime/PartyRespawnCoordinator拥有会话服务与模拟时间。
3. Runtime/CapsuleRespawnPlacement实现IRespawnPlacement；射线验证地面/同层，胶囊验证障碍与批次保留间距；全灭先检查全部点再恢复。
4. CombatVitals/Health复用私有恢复，Root绑定服务owner并恢复同实例；Coordinator保留唯一Current并真实输入重武装。
5. 新ROADMAP01场复制已保存B2，添加本轮组件，旧场/Prefab不写。验证真实死亡、持键、阻挡、全灭及状态身份，提交S1。

## S2 单帧动画路由
Animation/Core只读语义、优先级和代际；Runtime可配置映射与实际Spine适配。Root运动后只收集和提交一次。攻击优先移动；同ActionId不重复播放，新ID同片段重播，缺片段具名fallback。无动画伤害回调。纯与Spine真实验收后提交。

## S3 两段手动攻击
BasicChainState只拥有段序/有限预输入；原BasicActionRuntime保持唯一时钟。可选本场Profile，Melee只读命中回执，未命中不续段。切控/受控/死亡清缓存，每段新边沿和ID。真实InputAction两段/长按/空挥/切控验证后提交。

## S4 独立敌动
旧StationaryBrain保留；新Brain仅决策，EnemyMovement真实CharacterController，EnemyRoot唯一动作与推进。检测/LOS/停距/受阻/返回/死亡清理；新场墙体和联动验证。胶囊仅技术占位，正式2D素材不在本轮。通过后提交并停止。

风险C，测试成本B：每阶段纯合同+真实Unity关键流程；历史只复验受影响路径。失焦/暂停/服务生命周期、同一HP/Posture/Current/ActionId为共同底线。禁止修改旧场、媒体、原型、细则、Packages；不Gitee。
