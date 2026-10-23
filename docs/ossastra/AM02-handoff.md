# AM02 原式动画链迁移

基线 ed74ae1af1698306cd44f5fdb7a871dbc1bbf3b7。采用独立生产命名空间 `Ossastra.CharacterAnimation` 与新 GUID 路线；旧参考内容保留不变。综合入口仍为 `Assets/OSSASTRA/Scenes/SC_VC02B_IntegrationReady.unity`。

## 迁移与权威

| 原职责／类 | 当前生产实现 | 适配边界 |
| --- | --- | --- |
| AnimationIntent、Clip、QueuedClip、Plan | Animation/Character 同名文件 | 保留原作者字段和克隆，增加 actor/target/action/phase/replay/presentation/life 身份 |
| AnimationPlanMappingSO、OverrideRuleSetSO、LogicRouterSO | 同名新类型与 Odin Editor | 保留分组和历史列表；空项不覆盖合法方案，clear-only／queue-only 是有效操作 |
| AnimationComposeContext | 同名只读适配器 | 真实 Root/DataBus/当前来源装备；未知条件失败并诊断，不反射写入旧 Bus |
| AnimationArbitrator、ConfigManager | 同名生产实现 | 状态覆盖→装备覆盖→基础映射，返回私有深拷贝；来源装备分别解析 |
| AnimationTransitionRouter | 同名生产实现 | 保留移动／武器过渡与队列归属，队列使用实际 Spine TrackComplete 与速率 |
| CharacterAnimationComposer | 同名生产实现 | Submit 只收集；Root 表现阶段一次 Commit，逐轨竞争与全身独占，不中间播放 |
| AnimationHandleSystem、CharacterSpineEventSystem | 同名生产实现 | 每个 Skeleton 唯一写入者；完整身份去重，事件只转发表现，不结算伤害／费用 |
| 新 Root 表现端口 | CharacterAnimationController | 接受时冻结完整方案，阶段重播有独立 PhaseId；持续姿态实时解析 |

身体 Skeleton 由 Spine 自身推进，装备 Skeleton 仍由原装备视图手动推进一次；Handle 不再次 Update Skeleton。装备保留原骨骼／插槽／握点／翻转／排序／颜色跟随；各手独立 Composer/Handle，另一手重播不会重启本手。AbilityRun 的表现来源与费用来源分开记录，避免副手消耗主手弹药时误归属主手。

基础动作与能力时钟、命中、费用继续归 AM01 状态／既有事务账本。动画完成或事件不释放玩法锁。来源变更不创造第二个动作运行时。

## 资产身份及配置

新 SO 位于 `Assets/OSSASTRA/Configs/Animation/`：默认 Rozeul、A、B 三个身体 Mapping，四个装备 Mapping，以及 CharacterLogicRouter。完整 GUID／类型／Skeleton／作者 JSON 见 `Candidates/AM02/Evidence/identity-final.json`；逐片段表见 `animation-mapping.tsv`，枚举表见 `enum-values-final.tsv`。旧完整枚举显式固定数值，新实际动作 ID 从1001起、细分能力从1101起；共享 NPC/Enemy 语义使用具名转换。

A、B 原 Profile 内部同名，迁移按资产路径区分，不能按 SO.name 合并。两个角色分别绑定 A/B Mapping；候选 PlayerCharacter 无 Definition，使用显式默认 Rozeul Mapping。37个当前动作 SO 的隐藏旧字符串兼容字段保留，但正式配置用 typed animationLogicId。四个装备表现 SO 已绑定新映射。用户自建未跟踪 Action SO 未被修改。

B 与候选 Prefab 的旧 CharacterSpineView 已移除，正式每个目标只有一个 Handle。旧 CharacterSpineView／Profile 继续服务尚未迁移的历史测试场和只读迁移，不与正式角色并行。EntitySpinePresenter／AnimationCore 仍服务敌人与 NPC，不能整目录删除。

## 验证与限制

配置合同9/9；真实 Spine 播放及重建／停用恢复22/22；正常操作17/17；六技能最终复验25/25；动作中修改映射冻结5/5；副手持续与主手操作9/9；AM01生命周期最终13/13。证据和可重跑命令在 `Candidates/AM02`。正常操作覆盖真实移动／HP／费用与多阶段同 ActionId，而非只检查列表。工作树包含受保护用户改动，不是隔离克隆。

迁移重复执行后原7 Mapping GUID不变；保存重开确认A/B各1控制器、0旧视图、0旧孤立覆盖。用户 VELGITA Prefab Stage 原为clean，保存回读夹具临时退出并恢复它，未保存用户 Prefab；最终仍clean。

rozeul有 ready_start/ready_loop/ready_end，已接防御架势进入／退出；缺专用跑步起停，不以其他动作假装跑步过渡。装备仍为既有测试模型／素材，未声明正式美术完成。实体手柄未测试，留 AM03 说明。旧 Favorites 插件 Player 构建问题不在本轮范围。早先迁移失败及过期夹具记录保留，最终证据以标明 final 的回读为准。

## 一页配置入口

1. 在角色 MainBody 的 CharacterAnimationConfigManager 查看 baseMap 与 logicRouter。打开对应 A/B Mapping，在原式分组条目选择逻辑 ID、Spine片段、轨道、loop、mix、playbackRate；只有需跟随动作时间线的片段勾 scalesWithAction。不要写脚本动画名。
2. 新动作在 ActionDefinitionSO 选择 animationLogicId，玩法时间线仍在动作 SO；映射中添加相同ID。本次已受理的动作保持旧快照，下次才使用新映射。
3. 装备身体覆盖写入 EquipmentDefinitionSO.bodyAnimationOverride；独立模型映射写入 EquipmentSpinePresentationSO.animationMap。覆盖为私有方案，不修改共享基础 SO。
4. LogicRouter 条件是 AND，最高 priority 胜，同优先级保留列表顺序；覆盖规则第一条匹配生效。上下文条件只读已公开事实，未知键会给诊断，不按 false 误命中。
5. 架势过渡在原式 Map 中配置对应 Enter/Exit逻辑；TransitionRouter 选择合法过渡后排队目标。普通 queuedClips 保留作者 delay 约定，自动过渡等待实际上一段结尾。
6. Play 查看 Controller.CurrentClip/CurrentSource/FallbackReason 与各 Handle.LastResult/CommitCount/QueueCount。未知片段、无上下文队列会明确拒绝；不要用动画事件补伤害或扣费。
