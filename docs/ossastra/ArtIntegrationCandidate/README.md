# 角色配置与美术接入入口

2026-10-24 · CONFIG-01；复核基线 `0b922064b217e46e274ec29b5c2349ac355aa2e1`。这是当前 Editor 候选的作者说明，不是用户已冻结的正式美术标准。唯一用户规则仍为工程根 OSSASTRA_RULES.md。

先打开 `Assets/OSSASTRA/Scenes/SC_VC02B_IntegrationReady.unity`，选择 `临时验证角色_A` 或 B 的根，查看 **身体动画与配置**。四个入口可定位身体、打开实际 Base Map、Logic Router、Rig。导航读取 Root 的序列化 `view`，不靠层级名字猜测，也不会初始化装备。下面仍保留根原有可编辑字段；只读引用旁可定位组件或 SO。

场景 A/B 的 Base Map 是各自的覆盖，Prefab 默认是 `Rozeul_Animation_Mapping.asset`；不要编辑通用 Prefab 默认来代替修改当前角色。Edit 中右手显示组件预置引用，左右实际手槽标为运行时库存绑定；Play 才显示真实当前装备。Play 的当前片段、来源、动作/阶段 ID 来自 Controller/Composer 已执行记录；未记录的最终逻辑 ID/规则不推算，不为绘制再发请求。

[一页操作样板](ConfigurationSample.md)包括普攻、主手动作组、身体技能、两种副手、真实挂点和可撤回 mix 修改。完整当前数据在 [Generated](Generated/export-metadata.json)。

## 组件与职责关系

A 的身体节点是 `临时验证角色_A/VisualFlipRoot/VisualRoot/MainBody`；B 同构，实际绑定以根面板为准。下表“根”均指 OSSASTRA 的新 CharacterRoot，不是旧代码同名类。

|实际节点/对象|输入|输出及状态所有权|谁推进|
|---|---|---|---|
|角色根 / CharacterRoot|控制权限、实际组件接口、配置|装配、生命周期、DataBus只读投影；转发动作端口|Unity Update调用StateArbitrator.ManagedUpdate，再CommitPresentation；LateUpdate推进表现|
|根 / StateArbitrator组件|本帧输入、状态与事务许可|注册Movement/Combat/Dodge/Ability/WeaponPosture普通对象；登记边沿与诊断，不复制Runtime|Root唯一调用；注册顺序和实际执行阶段见代码ManagedUpdate|
|MovementStateMachine普通对象|输入、最新移动许可|方向解析与CharacterMovement唯一位移|仲裁器在请求前解析方向、动作事实后更新位移|
|CharacterCombatStateMachine普通对象|主手/伙伴候选、准备好的动画计划|唯一BasicActionRuntime、连段、受理与释放事实、取消|仲裁器；Root公开请求转发到它|
|AbilityStateMachine普通对象 / CharacterAbilities组件|设备手势、装备绑定、AbilityDefinition|既有AbilityRun与费用/投递；装备实例拥有CD/弹仓；不另建身体时钟|仲裁器Prepare受理、Update推进效果|
|身体 / CharacterAnimationController|DataBus事实、已准备的动作快照|提交AnimationIntent，保存CurrentClip/CurrentSource与执行身份；不反写战斗|Root.CommitPresentation；LateUpdate.FlushSignals观察原生事件|
|身体 / CharacterAnimationConfigManager|实际Map/Router/Overrides、候选来源上下文|先路由逻辑ID，再解析完整计划；不拥有玩法状态|Controller准备请求/持续表现时调用|
|身体 / CharacterAnimationComposer组件|各来源Intent|仲裁表现层、同一动作/阶段播放身份|Controller.Present → Commit|
|身体 / AnimationHandleSystem|最终计划clips/queuedClips/clearTracks|唯一身体轨道写入与Spine事件转发|Composer提交；Spine负责动画求值|
|根 / CharacterEquipmentSpineView|实际库存手槽、EquipmentSpinePresentation、Rig、同语义/执行身份|每只手独立Skeleton/AnimationState/Composer/Handle与捕获计划|Root.LateUpdate在身体同帧骨姿态后读取挂点|
|根 / TransientItemView|真实道具消费Release与定义图标|短暂图标表现，无数量/治疗写入权|Root.LateUpdate；按本体SimulationTime结束|

`CharacterStateMachine.Enter/Exit` 基类只累计边沿计数；当前五个注册机没有覆盖它们去启动另一套行为。Movement 的 Move/Idle、Combat 的 Action/MoveReady/AttackReady 等名称由真实运动/Runtime事实推导，并非旧版每个子状态对象都已迁回。实际副作用在 Movement.Update（移动）、Combat 的事务/Update（时线和释放）、Dodge.Prepare/Update（唯一闪避端口）、Ability.Prepare/Update（Run）、WeaponPosture 的旧兼容推进/死亡维护。根死亡/停用通过StopMovement清运动并立即配对退出；Dispose取消/清理原Runtime和已进入状态。不要把 Enter/Exit 观察边沿误当成第二执行器。

## SO 引用链

当前正式入口是 typed `ActionDefinitionSO.animationLogicId` + ConfigManager 的原式 Mapping/Override/Router。隐藏的 `CharacterDefinitionSO.animationProfile`、`ActionDefinitionSO.animationKey` 与旧 CharacterAnimationProfile 保留历史兼容；不是当前身体配置推荐入口。独立装备 `animationMap` 非空时，旧 `animations` 列表也仅兼容。当前B依赖没有已挂载的Override资源：Status/Item及装备bodyAnimationOverride为空，如实导出空字段；不为凑目录创建或声称命中过覆盖。

以下是本轮实际导出中的代表性引用。完整逐字段路径、源/目标 GUID 在 [SO-references.tsv](Generated/SO-references.tsv)；场景覆盖和 Prefab 默认在 [scene-bindings.tsv](Generated/scene-bindings.tsv)。

|实际资产 / 源GUID|类型 / 引用字段|目标资产 / GUID|用途与生效时机|
|---|---|---|---|
|`Assets/OSSASTRA/VC02B/Character_temporary-a.asset` / `bd8669d01185c48419f8378c75088e17`|CharacterDefinitionSO / `rigProfile`|`Assets/OSSASTRA/VC02B/Rozeul_RigProfile.asset` / `20fce0c717e4814458cdc8c91c74e046`|Rig解析时定位身体语义挂点|
|`Assets/OSSASTRA/VC02B/Equipment_temporary-a.vc02.main.asset` / `7e065cb27e2567141aa0fb6317301f85`|EquipmentDefinitionSO / `mainActionProfiles.Array.data[0]`|`Assets/OSSASTRA/Configs/Abilities/MainProfiles/hunter_I.asset` / `7970af2da9218f0439ee633cf6fef1f1`|换模式选中动作组，受理前准备|
|`Assets/OSSASTRA/Configs/Abilities/MainProfiles/hunter_I.asset` / `7970af2da9218f0439ee633cf6fef1f1`|MainActionProfileSO / `stages.Array.data[0].action`|`Assets/OSSASTRA/Configs/Abilities/MainProfiles/hunter_A1.asset` / `2d5c07e2e85878b45ad8f9bd611bdec1`|动作组第一段准备/冻结|
|`Assets/OSSASTRA/Configs/Abilities/hunter-break.asset` / `47b447c37302f284bb0988416c4ed795`|AbilityDefinitionSO / `action`|`Assets/OSSASTRA/Configs/Abilities/hunter-break_Action.asset` / `aa2d6855ae3f06d45afee2a36064b857`|身体技能准备与受理|
|`Assets/OSSASTRA/VC02B/Equipment_vc02.shield-b.asset` / `8762fd5ab56fe3b4081496ea5d9cbf14`|EquipmentDefinitionSO / `abilityBindings.Array.data[0].ability`|`Assets/OSSASTRA/Configs/Abilities/ShieldB_PressAttack.asset` / `d759fb867904a634abf74066b359cca4`|左手Press绑定，Run受理|
|`Assets/OSSASTRA/VC02B/Equipment_vc02.shield-b.asset` / `8762fd5ab56fe3b4081496ea5d9cbf14`|EquipmentDefinitionSO / `spinePresentation`|`Assets/OSSASTRA/VC02B/EquipmentSpine_Left_PROBE.asset` / `766057dd7472e89468813de8c5a20ce5`|左手装备实例重绑表现|
|`Assets/OSSASTRA/VC02B/EquipmentSpine_Left_PROBE.asset` / `766057dd7472e89468813de8c5a20ce5`|EquipmentSpinePresentationSO / `animationMap`|`Assets/OSSASTRA/Configs/Animation/EquipmentSpine_Left_PROBE_Mapping.asset` / `0cd27de3c02bd5f43ae28fa1286efa72`|独立左手表现计划|
|`Assets/OSSASTRA/Configs/Animation/temporary-a_VC02_Animation_Mapping.asset` / `357b1c0d9000e19479b83ae04bc5a8f5`|AnimationPlanMappingSO / `skeletonDataAsset`|`Assets/随便尝试/rozeul_SkeletonData.asset` / `8de59581980c83e4e91f8a7e5bc92f46`|校验身体实际片段|

## 动作到动画关系

此表描述当前静态默认链，不声称每次运行都命中同一规则。运行来源/阶段可在根只读面板核对。路径前缀：动作组 `Assets/OSSASTRA/Configs/Abilities/MainProfiles/`，能力 `Assets/OSSASTRA/Configs/Abilities/`，映射 `Assets/OSSASTRA/Configs/Animation/`。

|动作/阶段|语义与来源|默认逻辑ID / 当前Router|最终基础映射与实际片段|冻结时点|
|---|---|---|---|---|
|hunter_I → hunter_A1/A2/A3/A4|Attack_Normal_1…4；MainHand|同名typed ID；共享CharacterLogicRouter.rulesOther同名规则，不筛特定来源|A的temporary-a_VC02_Animation_Mapping：atk1/atk2/atk3/atk4|TryPrepare在费用事务前准备完整计划；接受捕获该快照，WithTimeline保留轨道/队列/混合|
|hunter_II → hunter_II_A3/A4|Attack_Normal_3/4；MainHand|同名typed ID，Router优先|同一A Map的atk3/atk4|同上；动作组的实际时线另见合同，不用动画长度当许可|
|hunter-break_Action（默认Q能力）|由动画准备端产生候选；MainHand身体技能|Attack_Normal_3；当前Router同名规则|A Map Attack_Normal_3 → atk3|能力准备并通过受理前校验后，原Run/Runtime保存同一计划|
|ShieldB_PressAttack → ShieldAttack_Action|副手身体动作；OffHand与实际左手来源|HeavyAttack；当前Router无专属规则，走默认ID|当前无装备bodyAnimationOverride，默认HeavyAttack回落A Map的heavy；独立左手Map的Ability → strike|同一执行来源进入身体准备、费用与左手表现；不借右手身份|
|盾A持续防御|Guard；OffHand|Guard → Guard|A Map Guard → ready_loop，GuardEnter/Exit → ready_start/ready_end；独立左手Map Guard → guard|持续表现按既有版本失效约定刷新，不伪装成一次普攻快照|
|移动/待机|Jog/Idle；Movement|Router Jog/Idle|A Map对应run/idle1_1|持续意图按配置版本重建；不修改已受理动作|
|蓄力/分阶段能力|各Phase的既有准备上下文|按各阶段准备的ID和来源|准备阶段的完整计划|如ChargedRush已预冻结蓄力与释放阶段，Rephase使用已存计划，不会每次重读SO|

选择顺序是 **Router规则（按其优先级/条件）→默认逻辑ID/语义转换 → 状态覆盖 → 实际来源装备bodyAnimationOverride → itemOverrides逆序 → Base Map**。来源装备覆盖只在body目标合入，追加在itemOverrides后，因此逆序时先于普通itemOverrides；Status仍最高。装备独立身体是另一套Map与轨道，不用身体覆盖去替代独立装备映射。

Release、MoveReady、AttackReady、Finished 改 ActionDefinition 的 `eventTimeline`（非空时每种标记恰一个）或空列表时的 `releaseAt/moveReadyAt/attackReadyAt/finishAt`。动作组还会按自身时线规则组装最终合同。动画观感则改 Mapping 计划的 `clips`/`queuedClips`/`clearTracks`，包括animaName、track、loop、mixDuration、playbackRate、scalesWithAction/sourceDuration。调整播放速率不自动移动伤害/解锁时点；Spine原生事件不是第二个伤害时钟。当前 [action-contracts.tsv](Generated/action-contracts.tsv)导出的是实际Freeze时点，不是被eventTimeline覆盖的旧标量。

已接受动作保持深复制完整快照。作者改Map/Router/Override影响下一次按现有准备边界提出的请求，不追改当前执行；已预冻结的后续阶段同样保持原快照。持续待机/移动另按版本失效刷新。修改源骨架或动作合同后应针对该路径重验，不能只看面板文字。

## 身体与装备挂点关系

|功能语义|真实模型/绑定|装备内部握持点|空间、镜像、偏移与排序|
|---|---|---|---|
|equipment.right|Rozeul_RigProfile.model → rozeul；SlotBone `Z-Rhand1`关联骨 `Rhand`|Right_PROBE → ProbeSkeleton 的 `grip`|独立装备自身求值后逆掉grip位置/角度，再匹配身体AppliedPose与Transform；当前localOffset(0,0,0)、localScale=1.2785964、rotationOffset=0、orderOffset=4|
|equipment.left|同Rig；SlotBone `Z-Z_Lhand1` → `Lhand`|Left_PROBE同骨grip|同空间约定；当前offset(0,0,0)、scale=1.2785964、rotation=0、orderOffset=3|
|item.use|同Rig；Bone `Rhand`|图标，无独立骨架grip|真实消费后短暂显示，跟同帧姿态；无数量或结算权|
|Point能力|Rig支持显式Slot+Point Attachment|按具体装备定义|当前功能挂点未采用Point；不为装饰骨编造用途|

左右跟随开关当前followBoneRotation=true、followSlotColor=true、followSlotAttachmentVisibility=false；负行列式镜像单独处理，避免双镜像。slot不等于bone，缺语义/slot/bone拒绝，不静默挂根。整Renderer沿身体sortingLayer加orderOffset，**尚无身体内部slot穿插**。正式素材若要求穿插，应另审SkeletonRenderSeparator/额外Renderer方案；不以技术PROBE代替正式美术验收。完整真实数值见 [rig-equipment-bindings.tsv](Generated/rig-equipment-bindings.tsv)。不能由dodge_back名称推断模型具备独立背面。

## 当前目录与检查边界

菜单 `XingHai > OSSASTRA > Character Content Workbench` 保留检查、引用定位、具名预览与导出。选SO可查看字段/引用；Play显式选择当前角色可观察装备与挂点。物品预览输入真实实例ID，走实际使用事务，**会消耗测试会话物品**，不授予物品、不修改SO。

导出schema=3，Unity 2022.3.62t16。只在Edit导出已保存B综合场；已开目标场若dirty则拒绝，避免混入未保存值；其他情况只读加法加载并finally关闭。导出不保存场景或作者资产。metadata标记UTC日期、复核基线、生成时本机gitHead；当前工作树可能有用户修改，不能把gitHead当作干净声明。

- `SO-fields.tsv`：164 SO逐字段/下标/值；enum同时写名字与数值；role区分历史兼容。
- `SO-references.tsv`：真实源/目标GUID与字段；`scene-bindings.tsv`：A/B实际view/Map/Router与Prefab默认对比。
- `model-inventory.tsv`：3实际依赖模型，755目录条目；rozeul169骨/88slot/110动画、Zombie21/10/13、技术装备3/2/8；逐动画原生事件与动作合同分开。
- `rig-equipment-bindings.tsv`：当前实际装备/Rig/grip/变换；`action-contracts.tsv`：Freeze时间、typedID、时线来源及旧兼容key。
- `asset-manifest.tsv`：路径/GUID/SHA256；资源改变须重导，不沿用旧指纹。媒体导出不赋予分发许可。
- `definition-checks.tsv`：局部校验；PASS/INFO/REJECT分别看，不把可导出当作行为已验收。

## 已有配方与边界

此前同类近战作者配方、两盾不同手势、双人双手独立Spine、消耗投掷物及自然补给的历史运行记录继续保留于VC02B交接。本轮只复演一个当前配置小流程，不重复声称跑完所有历史合同。

SO是定义，不保存当前HP/CD/弹仓/数量；装备payload、InventoryLedger、AbilityRun与费用账本各自保持唯一状态。新同类内容优先填现有SO；新行为类型需新增AbilityExecutorSO/CompiledAbilityExecutor及效果消费者，补受理/释放/取消/费用证据，不向Root增加装备名字分支。道具和投掷装备来源不能混扣。

正式素材、正背面、遮挡穿插、握持姿态、动画节奏与实体手柄手感均待用户具体审查。当前Editor准入不等于Windows Player已可构建，详见 [readiness](readiness.md)。本轮不改源骨骼、不迁新系统、不替用户勾选正式验收。
