# AM01–AM03 定向修正交接

审查基线：`982925b49d773b9427fc7488c149e52d7b53c526`。执行规格为用户提供的《OSSASTRA_AM01-AM03_新版复核与定向修正清单_982925b》。聊天链接为登录墙，未声称读到登录后的新消息。本轮沿用当前 B 综合场，不新增永久测试场。

## 修正对应关系

| 项目 | 正式实现 | 证据与验收边界 |
| --- | --- | --- |
| F01 完整快照／事务顺序 | `ConfiguredActionSnapshot.WithTimeline` 保留完整计划深复制工厂；动作组、弹匣时长调整均经此端口。Combat 在费用事务前准备并校验计划，接受后只捕获已验证快照。 | `f01-red.json` 是真实动作组付款10后抛异常且未派发Accepted；`main-group-world.json` 为实际LT+RT切组、RT首击及第二段、单次费用、释放／终结和无效映射0费用的12项。 |
| F02 战斗路由 | 准备上下文带真实手别／装备、候选语义／阶段与克隆意图字典；Router 优先，Action ID 为没有适用规则时的默认。再解析状态／装备／基础映射并冻结。候选不伪造已受理ActionId，不改共享Bus。 | `f02-route-red/green.json` 为正式普攻；`formal-context.json` 验证正式身体技能、HasIntent、当前冻结和下次刷新、真实箱子盾B换装及左装备专属覆盖、同一闪避冻结。 |
| F03 条件失效 | Composer 将实际来源装备SO身份、有无装备、槽位实例身份纳入失效依据；即使身体覆盖为空也刷新持续条件。 | `f03-equipment-red/green.json`：站定、真实库存卸装、同一持续Idle输入、独立真实Spine输出；没有通过移动／时钟事实掩盖条件失效。执行中冻结计划仍优先。 |
| F04 持续手势来源 | 配置Action仍按设备克隆；输入帧携带设备、起始控制、手势序号与边沿。能力绑定分设备识别手势；已接受Run只关联自己的凭据，不用其他设备OR后的保持代持。 | `input-red/green.json`：LT→鼠标重叠后，释放LT／断开原手柄均结束原举盾；一次逻辑同帧最多申请一次。原来源合同验证无关设备保持不被清空。 |
| F05 主攻跨背包 | 主攻保持／释放事实与申请许可分开。开包消费新申请，不伪造RT释放；关包不制造新按下。 | 同一真实输入流程验证Accepted数量不增加，真松开再按才新增；原13项举盾跨UI流程保持通过。 |
| F06 同帧快按快松 | 按下资格在初始回调记录，Pressed／Released与帧末Held独立。正式主攻有显式边沿，能力手势支持完整pulse，闪避和交互沿原消费者使用边沿。UI-down归属保留到判断结束。 | `pulse-consumers.json`：世界快点闪避、实际F开箱、UI-down阻挡、重武装和四类Gesture语义及实际技能快点11项；最终24项真实故事含集中更新闪避、同帧双设备去重和同次update松开重按。 |
| Movement退出 | Root死亡／停用立即经仲裁器停止运动事实、清速度并观察Exit；死亡早退路径也清理。没有移动尸体或新增运动权威。 | `movement-red/green.json`：移动→禁用→恢复→移动→死亡4项，IsActive与Enter/Exit配对。 |

完整快照包含 clips、queuedClips、clearTracks、轨道、循环、混合、播放参数及运行时逻辑／来源身份；只有 `scalesWithAction` 的片段随最终时间线重算速率。连续能力仍沿原有时点预冻结全部阶段，之后 Rephase 消费原快照；没有新增动作Runtime、费用账本或能力Run。

正式接通Router后，回归发现旧默认 `Ability → Ability` 规则会覆盖 Rush／Rocket 专用ID，而基础映射没有 `Ability` 计划。仅通过Unity移除此条无来源、无条件、priority0的冗余规则；其余作者规则和SO字段组织保留。没有适用规则时，已有Action ID仍作为默认。该回归失败记录为 `six-skills-route-red.json`，不得冒充原能力全部已经通过。

## 状态组织的真实程度

当前已落实的是 **Root → StateArbitrator → 注册状态机 → 既有动作／能力模块** 的职责链和显式阶段顺序。Combat 推进唯一 BasicActionRuntime，能力仍持有各次Run，费用仍由原事务提交。

这不等于旧版每个具体子状态对象都已迁回：基类Enter／Exit主要报告边沿和计数；Movement的Idle／Move、Combat若干子状态名仍由真实运行事实推导。本轮只补明确缺失的移动退出责任，没有为了显示状态名称复制一套执行权。以后某个子状态需要独立进入／退出行为，应在所属状态机落实，不能竞争现有时钟与费用。

## 验证、现场及保留项

测试代码及结果在 `Candidates/AM-FIX`。真实综合场、InputSystem、Spine与库存接口参与验证；使用虚拟键鼠／手柄，不代表已完成实体手柄漂移、手感和自然接续体验验收。工作树包含既有用户改动，测试不是干净隔离克隆。

原验证夹具保留。生命周期复验副本只适配新增准备上下文参数的反射调用，未改变断言。失败证据分别保留：贴墙未满足移动前置、盾B无法放进5×2团队包、重复技能尚在冷却，均没有通过放宽产品碰撞／库存容量／冷却迎合夹具。

用户旧场删除、SC_CharacterVerticalSlice、CharacterEquipmentView、CharacterDebugControls、PlayerInputLayout.asset的既有改动不纳入本次发布。用户维护区／开发细则／旧参考／第三方不修改，不同步Gitee。旧Favorites插件Player构建阻塞、装备探针素材和实体手柄体验待验继续保留。

最终回归及发布核验见本任务记录和证据索引；技术修正完成不代替用户架构／手感审定。本轮没有新的关机授权，之前关机请求已在上一轮履行。

## 最终复验清单

| 结果 | 证据 |
| --- | --- |
| 正式动作组12、完整计划/速率/深复制5 | main-group-world.json、snapshot-plan.json |
| 正式技能/副手来源/冻结6、站定换装转换PASS | formal-context.json、f03-equipment-green.json |
| 输入边界最终24、快点消费者11、Movement4 | input-final.json、pulse-consumers.json、movement-green.json |
| 原来源24、混合36、举盾跨UI13 | source-regression.json、mixed-regression.json、guard-regression.json |
| 正常输入17、双手9、生命周期13 | natural-input-regression.json、hands-regression.json、lifecycle-regression.json |
| 六技能25、目标7、冻结5、真实Spine播放22 | six-skills-regression.json、targeting-regression.json、freeze-regression.json、playback-regression.json |
| 纯核心29、原能力合同34 | core-regression.txt、abilities-core-regression.txt |
| 最终A/B各五机只初始化一次、身体/四装备真实播放、Missing Script0；增量日志未见NullReference/MissingReference/CS错误 | final-runtime.json、final-log-check.json |

旧M01独立csproj只链接Core顶层文件，遗漏后续Abilities子目录，单独构建会报缺命名空间；本轮没有修改旧工程。新的CoreRegression.csproj复用原29项断言、递归链接当前Core后通过；这属于旧夹具构建清单问题，不是Unity生产编译失败。

最终现场为 `SC_VC02B_IntegrationReady`，Edit ready/clean、无PrefabStage、无测试临时对象；原InputSettings及既有XInput设备保持。Git发布连接直连github.com失败、api.github.com可达，定向发布通过GitHub官方API，核对树和提交对象SHA后才允许更新main；不改变Git远端或使用Gitee。

发布：产品与证据c7feaf14c5bd1e7542e79924529c24c712f1fe08，GitHub OSSASTRA/main已读取核对；79个Blob／完整树／Commit与本地SHA精确一致，通过expected-sha且force=false更新。全部租约释放，后续由用户检查架构与实体手柄体验。
