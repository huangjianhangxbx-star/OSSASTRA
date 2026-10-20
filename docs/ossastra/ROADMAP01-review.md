# ROADMAP01 集中审查
## S1 调用链
CombatVitals.Died → PartyRespawnCoordinator → RespawnSchedule（本场模拟时间）→ IRespawnPlacement（同层地面+胶囊障碍+批保留）→ CharacterRoot.Respawn（owner/原实例）→ CombatVitals/Health本体恢复 → 唯一CharacterControlCoordinator复选。
角色Root新增的是恢复接线，不承接全局队伍时钟。死前动作经原Cancel终结；按键ResetPlayerInput(true)要求重新武装。姿态仅清自身限制。

## 本轮源码索引
详见ROADMAP01-source-manifest.json，逐文件SHA可与发布提交比对。脚本均位于Scripts，测试场位于ROADMAP01，未改共享Prefab。

## 待用户审查
- 单死/全灭等待的手感、候选落点间距；此为场内样板，不是完整LT01。
- 后续动画/连段/追击各自Gate后在此追加；不把临时胶囊当正式2D敌人。

## S2 调用链
Root运动后CommitPresentation → ICharacterAnimationFacts采样本体事实 → CharacterSpineView逐帧采集AnimationIntent → AnimationResolvePolicy过滤Actor/Generation和优先级 → 可序列化AnimationClipMap覆盖默认映射 → AnimationPlaybackIdentity → 单次Spine SetAnimation/Mix。动画不回写HP、权限、Release。死亡先由Vitals隐藏，Death意图只记录退场语义。
重点审查Animation/Core/AnimationRouting.cs、Animation/Runtime/AnimationClipMap.cs、CharacterSpineView.cs与Root的第7阶段。取消推进动画generation，旧请求无法续用。Spine自行推进内部骨骼，无伪造LateUpdate控制。

## S3 调用链
InputAction新边沿 → BasicChainState（有限资格，不累计delta）→ Root.RequestStage → 原BasicActionRuntime唯一模拟时钟 → Released → CombatMeleeHit唯一物理/LOS/HP批次 → ICombatHitReceiptSource只读Applied回执 → 同ActionId允许接续。阶段2映射独立语义、暂回退atk1。
BasicChainProfile只在本场装配；切控、暂停、失焦、破势、死亡清待用授权，已Released不回滚。重点看Root第4/5阶段、Core/BasicChainState、BasicActionRuntime.SimulationTime和CombatSight。

## S4 调用链与最终审查
EnemyRoot → IEnemyBrain（旧Stationary或新Chase）→ 中立候选/CombatSight/ChasePolicy → 原Runtime Request/Advance/CombatMeleeHit → IEnemyMovement.Tick → CharacterController.Move。Brain无Input/Current/玩家根，不扣HP；Movement不选择目标。Home只用于返程，不治疗、不传送。受阻判断采用上次真正提交给物理层的移动请求，攻击停步不算卡墙。

[全部变化源码、行号与SHA](ROADMAP01-source-review.md)；[清单](ROADMAP01-source-manifest.json)；[分阶段测试与失败](ROADMAP01-verification.md)。
用户重点审查：Root第4至7阶段的连段与动画连接是否清晰；场景生命服务与角色本体边界；IEnemyBrain/IEnemyMovement的独立性；10秒/0.5秒返场、两段节奏与1.8速度追击的SAMPLE手感。发布不等于用户验收。

正式敌人仍待2D素材；当前胶囊只验证物理和动作，不迁移整套原型敌人，也不宣称复刻《灰烬之国》内部实现。结束于S4，无追加阶段。
