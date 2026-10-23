# AM01 仲裁器与角色状态组织迁移

基线：19625fc3d53f88781158b5b60cb777dc7974dd6f。规格：用户提供的 AM01–AM03 v1.0；只实现本轮角色状态组织，不声称 AM02 原动画链已迁入。

## 实际职责与唯一写入者

| 运行态 | 迁移前 | AM01 创建／请求／推进／取消／消费 |
| --- | --- | --- |
| BasicActionRuntime | Root 创建、Request/Advance/Cancel | CharacterCombatStateMachine 创建及唯一 Advance；RequestStage/TryAbilityBodyAction 请求；具名 Cancel；DataBus/表现只读 |
| BasicChainState | Root Request→Advance→Poll | 战斗状态机保持相同顺序和有限缓存；命中回执订阅成对释放 |
| 主动作冻结计划与阶段 | Root Freeze/Capture/Rephase | 战斗状态机实际受理与 Capture/Rephase；Root 兼容方法仅转发；同 ActionId、PreviousTerminal 语义保留 |
| AbilityRun／投递实体 | CharacterAbilities | AbilityStateMachine 分阶段调用原 TickInput/TickEffects；原账本、占用、来源和释放仍唯一 |
| 闪避 | CharacterDodge | DodgeStateMachine 在请求前后查询同一绝对模拟时间；不增加时钟，不取消合法主手收尾 |
| 旧 Guard／Offhand 兼容 | Root 分支 | AbilityStateMachine/WeaponPostureStateMachine；新能力路径存在时互斥，不重复处理 |
| 移动／转向 | Root 调当前端口 | MovementStateMachine 请求前解方向，动作事实后查询最新 MoveReady／覆盖再调用唯一 CharacterMovement.Tick |
| 接受费／释放费／资源 | 原协调器、Run、Stamina | 保留原权威；协调器在提交前实际查询仲裁器的 Combat/Dodge 状态规则；失败不进入、不退出、不扣费 |

## 调度与生命周期

Root.Update → StateArbitrator.ManagedUpdate → Root.CommitPresentation；Root.LateUpdate 仍仅同步装备及临时道具表现。

明确顺序为：刷新动作组 → 唯一输入源 → 模式候选／资源／架势 → 方向 → 闪避候选 → 能力／旧盾候选 → 普攻请求 → 旧副手 → 库存／伙伴候选 → 唯一 Runtime.Advance 与释放 → 能力效果 → 主手世界效果与连段 Poll → 闪避绝对时间维护 → 最新许可实际运动 → 体力恢复 → 表现。

集中注册 Movement（Idle/Move 两个请求名映射同一实例）、Combat、Dodge、Ability、WeaponPosture。注册顺序稳定；同 owner 重绑幂等，不能换 owner。所有实例初始化一次，Idle 维护与 Enter/Exit 边沿分开。同步外部身体请求也在事实产生时记录 Combat 边沿；Enter 不重新提交动作。Inspector 可查看五个状态及初始化/进入/退出次数和最近拒绝原因。

旧 CanInterrupt 组织方式迁为事务前的 AllowsRequest 合同；Combat 的 AttackReady、Dodge 的 MoveReady 与当前能力真实占用共同决定替换/并行。未迁入无消费者的旧 Jump/Reload 请求、回调或假优先级字段，不恢复 Rigidbody2D。

死亡、失焦、OS 应用暂停、禁用、普通切人仍走各自既有政策。并行表现/状态退出不会销毁已经离手的世界弹体。旧参考目录未改。用户 VELGITA Prefab Stage 保持打开且 clean。

## 装配与临时入口

只用 Editor API 装配保存 B 综合场两个角色和 OSSASTRA/Prefabs/Characters/PlayerCharacter.prefab；不新建测试场。RequireComponent 与 Awake 校验共同保证组件依赖。

CharacterSpineView、CharacterEquipmentSpineView 与当前 Profile 保留为 AM01 临时表现接口。AM02 将统一迁移 Freeze/Capture、最终合成、身体/左右装备播放，不将临时接口标为正式动画系统完成。

## 验证

- Unity 编译通过；B 综合场实际输入 17/17：单次待机钟、初始化、边沿、移动、接受、释放恢复运动、收尾并行闪避且保持主动作身份、技能、切人、陈旧控制拒绝。
- 当前真实副手库存下的并行世界流程 5/5：同时接受主手/射击副手，具名取消主手后已释放弹体独立命中并清理。
- 生命周期夹具 13/13：拒绝无费用/取消、OS 冻结与恢复、Rephase 保持身份、不重复接受/释放、外来取消无效、失焦取消、重启/幂等初始化、真实死亡交接与平衡边沿。
- 纯合同：Abilities 34、MainActionProfile 10、StaminaReservation 10、Chain 14，合计 68 通过。
- 失败保留：旧 A 并行夹具假设 A 持有 shooter，与 B 当前库存不符，首次运行失败；改为 B 实际持有的射击副手后通过。生命周期初版按编辑器墙钟断言 Finished，2 条误报；改为等实际 Finished 并按 ActionId 统计取消后通过。原始 JSON 保留，未标产品回归。
- 本轮不是隔离 checkout：用户既有输入、旧场移动、DebugControls 等改动始终保留且不随本轮暂存。
- 未代用户完成架构审查、真实手柄手感长测或第三方阻塞下的 Player 构建；AM02/AM03 尚待执行。

证据：Candidates/AM01/Tests 与 Evidence。发布 SHA 在提交成功且远端复核后记录。

生命周期追加修复：实际退出 Play 复现 InventoryView.SetOpen 在角色已销毁时 GetComponent 抛 MissingReference。新增最小销毁顺序回归，先 RED 后 GREEN；关闭捕获现允许会话/窗口先消失，菜单与背包共用存活输入端口遍历。不是吞异常；仍正常释放所有存活角色的捕获。旧 Console 历史错误保留，最终判断基于新增日志范围与回归结果。

正常背包真实输入回归47/47通过（开关包、移动、捕获战斗、拖放、F拾取、拆分目标位置）；最终退出新增日志范围未出现MissingReference或新编译/运行异常。
