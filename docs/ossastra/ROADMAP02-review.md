# ROADMAP02 审查入口
新增本体CompanionCombatBrain(策略/候选)，场景CombatEngagementSession(Accepted/平静窗口)，纯CompanionCombatPolicy；Root阶段4b唯一请求，Selector只消费战斗move，未放开attack/cancel/pause。
EnemyRoot删除强制StationaryBrain依赖，保留旧实例与显式选择策略；ActionObserved只读事实，无全局总线。
CombatMeleeHit只暴露空间配置供候选/预警读取，结算算法不变。
共享PlayerCharacter.prefab、ROADMAP01和更早场不改；新场显式加能力。所有SAMPLE均待实际手感审查。
