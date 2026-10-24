# OSSASTRA AM01–AM03 新版复核与定向修正清单

> 审查日期：2026-10-24（北京时间）
>
> 审查版本：`982925b49d773b9427fc7488c149e52d7b53c526`，分支 `main`。
>
> 本文依据固定版本的源码、实际配置资产、场景接线记录及仓库上传的验证夹具与结果进行复核。本轮未启动 Unity、未重跑夹具、未操作实体手柄，也未修改或推送生产代码。

## 1. 审查结论

**AM01–AM03 的迁移主体已经落地，建议在现有架构内完成一轮定向修正，再宣告完整验收。**

当前最需要先修的是可配置主手动作组的动画快照转交：完整表现计划在适配过程中被丢弃，随后在动作已经受理、费用已经提交后抛异常。其余主要缺口集中在正式战斗路由、条件配置刷新，以及三种混合输入边界。

本轮建议范围为下文 F01–F06 六项。状态层级与移动退出另列说明，避免把“已有注册式组织主干”写成“旧版每层子状态对象均已复原”。

### 1.1 版本与范围

| 内容 | 固定版本 |
| --- | --- |
| 上次方案对照基线 | `19625fc3d53f88781158b5b60cb777dc7974dd6f` |
| AM01 控制职责迁移 | `ed74ae1af1698306cd44f5fdb7a871dbc1bbf3b7` |
| AM02 原式动画链迁移 | `67efa5c1f1046f1335dc95469ceb4afc469f962a` |
| AM03 混合输入 | `faf2938e8434993d1a9a4dd1c542d988e464b65b` |
| 本轮审查 HEAD | `982925b49d773b9427fc7488c149e52d7b53c526` |

最新提交时间为 2026-10-24 05:04（北京时间）。本轮发布包含连续三轮实现及最终记录；仓库任务单已记录用户授权按序完成三轮。审查对象是最终组合版本。

## 2. 已经落实的成果

| 领域 | 源码和已上传证据支持的结论 | 本轮判断 |
| --- | --- | --- |
| 仲裁与控制 | `StateArbitrator` 注册 Movement、Combat、Dodge、Ability、WeaponPosture；Combat 实际持有和推进唯一基础动作运行时，处理请求、能力身体动作、换阶段、取消和连段；Coordinator 查询仲裁门禁 | 职责迁移成立，Root 已明显收敛。完整子状态对象层另见第 4 节 |
| 原式动画配置 | 原式 Mapping、Override、Router、Plan、Transition、Composer、Handle 已进入生产目录；当前角色与装备绑定新配置 | 熟悉的配置组织已建立；F01–F03 影响正式配置能力的完整性 |
| 表现运行 | 提交、合成、轨道竞争、过渡队列、播放去重与重绑有实际实现；上传的 Spine 播放验证使用真实 Spine 对象 | 有真实运行证据，仍需补正式动作入口的遗漏 |
| 混合输入 | 来源状态由玩家会话持有，移动与战斗瞄准分别记录；按设备读取配置 Action；静止鼠标不直接接管；指针初始 UI 归属有记录 | 基础方向正确，F04–F06 需要补齐 |

来源：[AM01 handoff][h1]、[AM02 handoff][h2]、[AM03 handoff][h3]、[战斗状态机][combat]、[Root 接线][root]、[仲裁器][arbiter]、[动作受理门禁][coordinator]。

## 3. 六项定向修正

### F01 · P1：动作组适配丢失完整动画快照，受理后抛异常

**触发路径**

在当前综合场中，给已有主手选择一个有效的可配置动作组，使 `CharacterMainAction.Sequence != null`，随后普攻。A、B 两个现有主手装备资产都已配置动作组，因此这是现有功能的正常可达路径。

**源码因果链**

1. `CharacterMainAction.TryPrepare` 第 89 行通过 `FreezeAbilityPhase` 取得带完整表现计划的快照。
2. 第 91 行为替换玩法时间线重新构造 `ConfiguredActionSnapshot`，仅传入 Timeline、Clip、Mix、Rate、Loop，遗漏 `copyPresentationPlan`。
3. `CharacterCombatStateMachine.RequestStage` 第 187–191 行已调用 `TryCommit`，动作运行时接受请求，费用事务完成。
4. 第 198 行再调用 `CapturePhase`；Controller 第 97–98 行因完整计划为空抛出 `ArgumentException`。
5. 后续 `CharacterMainAction.Accept`、连段接受及本次 Accepted 事实分派没有执行。

**影响边界**

可以确定该分支会在受理后中断正常初始化。可见表现、后续伤害或恢复行为需在 Unity 中按该路径确认；本审查不把它扩大为“所有攻击都坏了”或“必定永久锁死”。

**修正要求**

- 在替换玩法时间线时保留完整表现计划及其深拷贝能力。可采用明确的 `WithTimeline` 或等价复制端口，避免再次降回单片段字段。
- 完整保留轨道、队列、清轨、播放参数和运行时逻辑身份。与动作时长绑定的速率按最终时间线一致计算。
- 将必要的表现准备和有效性检查放在受理／扣费之前；失败应正常拒绝，不能在付款后才暴露不可用快照。
- 延续现有唯一动作运行时和费用账本。

**最小复验**

通过实际输入切换现有动作组，执行首击和后续连段；检查无异常、完整方案存在、每个接受动作只扣一次费用、动作组的释放与结束正常。再用一个无效映射确认在受理前拒绝。

**证据**：[动作组转换][main-action]、[受理与捕获顺序][request-stage]、[费用提交][coordinator]、[完整快照端口][snapshot]、[动画捕获检查][capture]、[A 主手配置][gear-a]、[B 主手配置][gear-b]。

原冻结夹具 `freeze-world.cs.txt` 直接调用 `TryConfiguredAction`，绕过这条动作组转换路径；其通过结果不能覆盖本问题。[夹具][freeze-test]

### F02 · P2：正式普攻／身体技能冻结入口绕过 Router，来源上下文不完整

**触发路径**

在原式 Router SO 中，为正式攻击／身体技能配置一条应改变动画逻辑 ID 的来源或条件规则，然后通过正式动作入口执行。

**源码因果链**

- Controller 的 `ResolveAction` 按 `ActionDefinitionSO.animationLogicId` 直接调用 `ResolvePlan`，未先调用 `RouteIntent`。
- ConfigManager 将该 ID 写入 `plan.runtimeLogicId`，最终提交的意图带显式 `logicId` 和冻结计划。
- Composer 遇到显式 `logicId` 直接使用；有冻结计划则继续使用快照，后面不会补做 Router。
- Controller 准备上下文固定读取 `RightDefinition`，没有本次候选的完整意图集合；副手来源的身体动作也无法由该入口表达真实来源装备。

**影响边界**

普通映射与部分覆盖仍可工作，移动／防御等持续意图也存在正常路由入口。问题是正式战斗动作有一条未说明的路由例外，不能把原式 Router 的独立对象测试视为正式动作已经接通。

**修正要求**

- 在准备／冻结阶段建立真实语义、动作／阶段、来源槽及来源装备上下文，先路由，再做状态／装备／基础映射解析，最后冻结。
- 明确 Action SO 的逻辑 ID 与 Router 的关系。按本次迁移目标，建议把 Action 的 ID 保留为可配置默认选择，让适用的原式路由规则参与最终选择。
- `HasIntent` 等条件应读取具有明确定义的只读上下文；不得靠改写共享 Bus 或伪造右手来源满足条件。
- 保持既有冻结时点：若能力在接受前已经冻结全部阶段，后续换阶段使用先前的各阶段快照；只有原有约定允许在新阶段准备时冻结的路径才在该时点解析。持续姿态继续按当前配置解析。
- 闪避也使用一致的准备与冻结约定，防止同一个闪避身份因路由结果变化替换方案；无需额外建立动作运行时。

**最小复验**

在实际普攻／身体技能入口配置一条能映射到另一个有效片段的 Router 规则，验证真实播放跟随规则；当前动作中编辑配置不改旧快照，下一次动作采用新结果。实际存在副手身体动作时，再核对其来源装备。

**证据**：[Controller 准备／冻结][resolve-action]、[ConfigManager 解析][config-manager]、[Composer 正式路径][compose]、[路由对象夹具][config-test]。

### F03 · P2：更换无身体覆盖的装备，持续姿态条件可能不刷新

**触发条件**

角色持续 Idle 等姿态，配置 `HasWeapon`／`NoWeapon` 或依赖来源装备的规则；装备变更前后均没有 `bodyAnimationOverride`，同时其余已追踪事实不变。

**源码因果链**

Composer 的 `FactsKey` 没有装备身份或有无装备；配置增量只纳入非空的装备身体覆盖。相同持续意图又可提前返回，因此单纯这次换装没有使组合结果失效，条件要等其他事实变化才重新求值。

**修正要求**

将实际来源装备身份／有无装备纳入失效依据，或在正式装备变更处调用已有刷新端口。刷新持续姿态的条件选择，保持执行中动作的冻结快照。

**最小复验**

站定不移动，使用不含身体覆盖的装备执行空手→持有→空手；相应条件映射应及时更新。验证时避免通过移动等额外操作恰好触发刷新。

**证据**：[意图去重与配置失效][composer-cache]、[条件上下文][context]。这是正式使用条件配置前的缺口，不表示每次换装都会失败；其他事实变化可能恰好掩盖它。

### F04 · P2：持续操作没有保留发起设备，另一设备可以代持

**触发步骤**

1. 用手柄 LT 开始举盾并保持。
2. 在合法世界区域按住鼠标右键。
3. 松开 LT，或断开该手柄，同时继续按住鼠标右键。

**源码结果**

`CharacterInputHandler` 把不同设备的副手／技能保持用 OR 合并；能力手势和 Run 只读取这一合并布尔值。原设备释放后合并值仍为真，原手柄发起的持续操作没有收到正确结束。

**修正要求**

- 保留同帧同一逻辑新请求只提交一次的规则，同时给已开始的持续操作保存最小必要的设备／控制／手势身份。
- 发起设备释放、断开或失效时结束其操作；另一设备的新按下按原动作资格独立处理。
- 普通 UI 辅助操作不更换已有手势身份，也不凭空释放合法保持。
- 继续用原 InputAction 资产配置，设备身份不等于另外硬编码一套按键表。

**最小复验**

按上述双设备交叠步骤分别验证释放和断开，原 Run 正常结束；无关设备的有效保持不被清空。补测后者可复用现有断开用例。

**证据**：[设备保持合并][input-merge]、[能力手势读取][ability-gesture]、[Run 保持推进][ability-tick]、[GestureRecognizer][gesture]。

现有举盾开关背包夹具验证的是同一设备保持，不覆盖两台设备交叠。[夹具][guard-test]

### F05 · P2：主攻保持跨越开关普通背包，关闭时产生虚假新按下

**触发步骤**

1. 按住 RT 完成一次普攻，并继续保持。
2. 用 Tab／Start 打开普通背包。
3. 等当前动作恢复可接受状态，RT 一直不松。
4. 仍保持 RT，用 Tab／Start 关闭背包。

**源码结果**

背包捕获期间把 `attackHeld` 强制置为假；`AttackIntent` 据此记录了一次释放。关闭后原来一直保持的 RT 又输出真，形成不存在的 false→true。`SuppressNewPresses` 只抑制本帧新按下，没有消除开包前的原保持。

**修正要求**

分开物理保持／真实释放与“是否允许产生新主攻请求”。普通背包捕获应阻止新请求、消费被拦的新边沿；不能用伪造释放让旧保持在关闭后重新成为按下。若采用等待释放策略，只约束相关旧手势，保留现有合法持续副手的表现。

**最小复验**

完整执行上述流程，关包后没有额外 Accepted；真实松开再按才出现下一次攻击。复用原举盾跨 UI 用例，确保该合法保持仍连续。

**证据**：[背包捕获遮罩][input-capture]、[SuppressNewPresses][sampler-suppress]、[主攻边沿识别][attack-intent]、[战斗采样][combat-input]。

### F06 · P2：同一次输入更新里的快速按下／释放被丢弃

**触发条件**

低帧率或输入事件集中处理时，同一次 Input System 更新处理到了按钮 down 和 up；游戏读取时按钮已经释放。

**源码结果**

`DeviceActionSampler.Pressed` 使用 `Held(source) && WasPressedThisFrame()`。`Held` 根据最终 `IsPressed()` 返回假，因此真实存在的按下边沿丢失。闪避、交互及部分来源接管使用此入口。

Unity 官方文档将 `WasPressedThisFrame()` 定义为本帧任意时刻跨过按下阈值；它与帧末是否仍保持不是同一事实。[官方 API][unity-press]

**修正要求**

分别保存按下边沿、最终保持状态和释放边沿，并沿正式输入帧传到需要它们的消费者；下游不得再用最终保持值抹掉已发生的合法边沿，也要避免按下事件与保持转换重复发起请求。重武装与 UI 初始归属判断该事件是否合法。修正后仍须保留 UI-down 的阻挡，不能简单去掉 `Held` 调用就让旧保持、失焦恢复或 UI 点击绕过保护。

**最小复验**

对已经初始化并完成重武装的设备，先排入 down 和 up，再执行一次输入更新及读取：世界闪避／交互应各接受一次；同样的 UI-down 不应产生世界操作。该用例与普通跨帧按住／松开互补。

**证据**：[采样实现][sampler-pressed]、[闪避／技能消费者][input-merge]、[交互入口][interaction]、[现有来源夹具][source-test]。

## 4. 状态组织的验收边界

### 4.1 已恢复主干，子状态对象层还需准确描述

当前已经形成 Root → StateArbitrator → 注册状态机 → 既有动作／能力模块的实际职责链。Combat 承担了业务控制，唯一基础动作运行时也由它推进。

但基类的 `Enter/Exit` 目前主要是计数，Movement 的 Idle/Move、Combat 的若干状态名称主要由运行事实推导；不能因此声称旧版每个具体子状态对象的 Enter／Update／Exit 生命周期均已完整迁回。[状态契约][state-contract]、[Movement][movement-state]

建议在交接文档明确区分“注册式组织已恢复”和“具体子状态对象的复原程度”。如果用户熟悉的某个子状态需要真实的进入／退出行为，应在对应状态机中落实该责任；唯一动作时钟、费用与能力 Run 继续由现有领域运行时持有。避免为了显示状态名而再增加会竞争执行权的状态副本。

### 4.2 次要生命周期补漏：移动中死亡／禁用

Movement 的 `IsActive` 依赖 `CharacterMovement.IsMoving`。死亡分支提前返回、不再更新移动时，旧值可能使 Inspector 保持 Move/active，缺少对应 Exit 边沿。禁用也应核对对称清理。

**这里能确定的是状态记录与退出生命周期缺口，不能据此断言尸体仍在物理移动。**

可随收尾显式清理并补一个移动中死亡／禁用用例，检查 IsActive 和 Enter/Exit 配对即可。[Movement][movement-state]、[死亡调度][arbiter]

## 5. 现有验证证据怎样使用

上传证据包括真实 Spine 播放与恢复 22 项、冻结 5 项、AM03 来源合同 24 项、连续混合输入流程 36 项，以及举盾跨背包、生命周期、原生 E 目标等验证。它们支持对应覆盖范围内的实现；本轮没有重新运行这些结果。[AM02 handoff][h2]、[AM03 handoff][h3]

本轮发现的缺口主要是入口与事件组合覆盖不同：冻结夹具绕过动作组适配；路由夹具直接测试规则对象；保持夹具未覆盖双设备交叠与主攻跨 UI；输入夹具把 down/up 分别推进，未覆盖同次更新。

下一轮直接补足这些具体路径，在原有综合场和夹具上做最小扩展。修复后复验相关既有用例，记录修复前的可复现结果与修复后的结果即可。

### 建议执行顺序

| 顺序 | 工作 | 结束标准 |
| --- | --- | --- |
| 1 | F01 完整快照与受理前准备 | 实际动作组首击／连段正常，费用一次，无异常 |
| 2 | F02、F03 正式路由与条件刷新 | 战斗规则和空手／持有条件在真实入口生效，当前快照保持稳定 |
| 3 | F04–F06 输入手势与事件边沿 | 原设备释放正确；关包不补攻击；快速点击不丢且 UI 不穿透 |
| 4 | 状态交接说明与移动退出 | 如实标记子状态组织程度，清理已定位的退出遗漏 |
| 5 | 用户实体手柄体验 | 探索、辅助键鼠、返回战斗自然衔接；对实际漂移与阈值作判断 |

实体手柄自然接续、漂移和手感尚无实操验收；检测到 XInput 设备不等于已做硬件测试。现有 handoff 也保留了旧 Favorites Editor 插件导致的 Player 构建阻塞和部分装备探针素材，这些不应改写为本轮已解决。[保留项][h3]

## 6. 可直接交给 Codex 的任务说明

> 以当前远端 main 为起点，先核对它相对审查 SHA `982925b49d773b9427fc7488c149e52d7b53c526` 的新增变更。继续完成 AM01–AM03 的定向收尾：按本文顺序修复 F01–F06，明确子状态对象的实际复原程度，并处理已定位的 Movement 退出遗漏。
>
> 继续沿用唯一 BasicActionRuntime、现有能力 Run 和费用账本，保留旧式动画 SO 类型、字段组织、配置入口，以及玩家会话级的移动／瞄准来源记忆。修正应落实到正式动作／输入调用链。
>
> 使用当前综合场及既有夹具补足本文列出的具体路径，分别报告实际运行结果、仅静态确认项和实体手柄待验项。测试结束后更新相应 handoff 与任务记录，按项目已有授权范围定向提交发布并核对远端 SHA。发布完成不代替用户对架构与手感的验收。

## 7. 当前可以开始做什么

可以查看新 Mapping 的分组、轨道、队列、Override 与 Router 配置入口，试玩已经覆盖的默认基础闭环。建议在 F01–F03 修复后再批量定稿正式动作组和条件路由配置，以免遇到配置不生效而重复调整素材。

本轮推荐的下一步就是上述定向收尾。完成后进入正式动画配置与实体手柄体验确认。

---

## 固定版本源码与证据

[h1]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/docs/ossastra/AM01-handoff.md
[h2]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/docs/ossastra/AM02-handoff.md
[h3]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/docs/ossastra/AM03-handoff.md
[combat]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterCombatStateMachine.cs#L80-L157
[root]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterRoot.cs#L236-L291
[arbiter]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/Scripts/Characters/Runtime/StateArbitrator.cs#L73-L107
[coordinator]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterActionCoordinator.cs#L22-L53
[main-action]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterMainAction.cs#L83-L99
[request-stage]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterCombatStateMachine.cs#L175-L206
[snapshot]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/Scripts/Characters/Runtime/ConfiguredActionSnapshot.cs
[capture]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/Scripts/Animation/Character/CharacterAnimationController.cs#L73-L99
[gear-a]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/VC02B/Equipment_temporary-a.vc02.main.asset#L29-L31
[gear-b]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/VC02B/Equipment_temporary-b.vc02.main.asset#L29-L31
[freeze-test]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Candidates/AM02/Tests/freeze-world.cs.txt
[resolve-action]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/Scripts/Animation/Character/CharacterAnimationController.cs#L57-L108
[config-manager]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/Scripts/Animation/Character/CharacterAnimationConfigManager.cs#L72-L90
[compose]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/Scripts/Animation/Character/CharacterAnimationComposer.cs#L103-L123
[config-test]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Candidates/AM02/Tests/config-contracts.cs.txt
[composer-cache]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/Scripts/Animation/Character/CharacterAnimationComposer.cs#L49-L99
[context]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/Scripts/Animation/Character/AnimationComposeContext.cs
[input-merge]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterInputHandler.cs#L155-L202
[ability-gesture]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterAbilities.cs#L258-L279
[ability-tick]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterAbilities.cs#L395-L410
[gesture]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/Scripts/Characters/Core/Abilities/GestureRecognizer.cs#L24-L50
[guard-test]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Candidates/AM03/Tests/guard-capture.cs.txt
[input-capture]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterInputHandler.cs#L220-L228
[sampler-suppress]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/Scripts/Characters/Runtime/DeviceActionSampler.cs#L94-L97
[attack-intent]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/Scripts/Characters/Core/AttackIntent.cs#L15-L25
[combat-input]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterCombatStateMachine.cs#L49-L64
[sampler-pressed]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/Scripts/Characters/Runtime/DeviceActionSampler.cs#L54-L60
[interaction]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/Scripts/Inventory/InventoryInteraction.cs#L31-L44
[source-test]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Candidates/AM03/Tests/source-contracts.cs.txt
[state-contract]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterStateContracts.cs#L19-L38
[movement-state]: https://github.com/huangjianhangxbx-star/OSSASTRA/blob/982925b49d773b9427fc7488c149e52d7b53c526/Assets/OSSASTRA/Scripts/Characters/Runtime/MovementStateMachine.cs#L5-L19
[unity-press]: https://docs.unity3d.com/Packages/com.unity.inputsystem@1.14/api/UnityEngine.InputSystem.InputAction.html#UnityEngine_InputSystem_InputAction_WasPressedThisFrame
