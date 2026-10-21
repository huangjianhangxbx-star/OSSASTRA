# ROADMAP02 审查入口
新增本体CompanionCombatBrain(策略/候选)，场景CombatEngagementSession(Accepted/平静窗口)，纯CompanionCombatPolicy；Root阶段4b唯一请求，Selector只消费战斗move，未放开attack/cancel/pause。
EnemyRoot删除强制StationaryBrain依赖，保留旧实例与显式选择策略；ActionObserved只读事实，无全局总线。
CombatMeleeHit只暴露空间配置供候选/预警读取，结算算法不变。
共享PlayerCharacter.prefab、ROADMAP01和更早场不改；新场显式加能力。所有SAMPLE均待实际手感审查。

## 最终架构阅读顺序
1. CombatEngagementSession：订阅两根真实Accepted；同一Coordinator重复组件拒绝；平静计时只影响资格。
2. CompanionCombatBrain + CompanionCombatPolicy：本体目标/锚点/冷却，声明窄移动/动作接口；不读设备、不执行位移、不扣血。
3. CharacterIntentSelector：Player/BattleMove/FollowMove单一移动帧；伙伴attack/cancel/pause仍被剥离。
4. CharacterRoot：阶段1先采本体候选，阶段4b仅非实控合法请求Stage1，仍进唯一Runtime；AI命中不存玩家连段。阶段5原Release/HP链，阶段6单运动，阶段7玩家表现。
5. EnemyRoot / AttackWarningState / EnemyAttackWarning：根真实事实投影、当前追踪圆；无锁定机制或新增时钟。EnemyRoot.LateUpdate在动作/运动后调用独立EnemySpineView。
6. EntitySpinePresenter / ClipFallback：中立单主轨播放；EnemySpineView和NpcSpineView独立适配，不借CharacterRoot/玩家Bus。NpcRoot.LateUpdate只提交Idle。
7. 新场实例配置：玩家atk2及Hit/Break/Death映射；敌/NPC TEMP_2D_VISUAL；共享Prefab、旧场、原素材不变。

需人工审查：有限目标策略与3单位锚点是否符合手感；现有Root请求编排的可读性；三根呈现适配边界；现有本地替身仅验证技术。没有迁移完整职业AI或正式敌人。
全部本轮变化生产源带行号与SHA见ROADMAP02-source-review.md / source-manifest.json。
