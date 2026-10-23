# VC02B 美术接入候选资料

这是可运行的工程候选，不是用户已冻结的正式素材标准。唯一用户规则仍为工程根 OSSASTRA_RULES.md。不要重命名源骨骼、修改商业模型或把 PROBE 盾当成正式装备。

## 从哪里检查

打开 `Assets/OSSASTRA/Scenes/SC_VC02B_IntegrationReady.unity`。Unity 菜单 `XingHai > OSSASTRA > Character Content Workbench`：选择 SO 后检查、逐个点击引用、导出候选依赖；Play 时显式指定当前 CharacterRoot，Scene 中显示实际功能挂点、执行 ID。物品预览输入真实实例 ID，走实际使用事务，**会消费该 B 测试会话数量**；不授予物品，不修改 SO。普通玩法仍由 HUD/Tab/F 操作。

`Generated/` 是 Editor API 对候选场实际依赖的导出：

- `model-inventory.tsv`：模型 GUID/版本、每个骨骼/父级、插槽/骨、皮肤/附件/point、约束、动画和逐动画原生事件。装饰骨没有被猜成玩法挂点。rozeul 实测 169 bones / 88 slots / 110 animations；敌人 Zombie 为 21/10/13；技术装备为 3/2/8。
- `SO-fields.tsv`：逐资产、类型、GUID、序列化字段路径与实际值/引用路径，包含数组的每个下标，不是空白构筑表。
- `asset-manifest.tsv`：候选依赖路径/GUID/文件 SHA256。媒体仅在本机，不因导出而取得公开分发许可。
- `action-contracts.tsv`：每个实际依赖动作及事件时点。非空 eventTimeline 才是 Freeze 的时点来源；releaseAt 等兼容字段不覆盖它。
- `definition-checks.tsv`：各资源局部校验结果。依赖可浏览不等于所有行为已验收，需结合运行证据。

重新导出会覆盖本工具自己生成的报告，不会保存或修改资产。模型版本依据当前安装 Spine 4.3 API，而非旧 4.2 教程。导出 schema=1；Unity 实测 2022.3.62t16。资源内容变化必须重导与回归，不能沿用旧 SHA 的证据。

## 实际功能挂点及空间约定

|语义|角色/真实对象|消费者|空间、更新、镜像与错误政策|
|---|---|---|---|
|equipment.right|B 两角色定义 → Rozeul_RigProfile；SlotBone `Z-Rhand1` 的关联骨 `Rhand`|CharacterEquipmentSpineView 右手|Root LateUpdate 在身体更新后读 AppliedPose；独立装备 grip 对齐骨架局部坐标，再经身体 Transform 到世界。SO localScale/rotationOffset/localOffset；负行列式分离，避免双镜像。缺 slot/bone/semantic 拒绝，不挂根兜底。|
|equipment.left|同 Rig；SlotBone `Z-Z_Lhand1` → `Lhand`|同组件左手|同上；颜色、附件显隐、骨旋转是独立开关。slot 不是 bone；不按 slot 字符串寻找同名 bone。|
|item.use|同 Rig；Bone `Rhand`|TransientItemView|真实消费 Release 后显示定义 icon，按本体 SimulationTime 结束；Root LateUpdate 读取同帧骨姿态。关闭表现不取消或复制消费。|
|装备内部握持点|EquipmentSpine_*_PROBE → ProbeSkeleton 的 `grip`|左右 SlotView|装备自身骨架求值后逆掉 grip 位置/角度，再匹配身体目标。左右与两本体各自独立 Skeleton/AnimationState。|
|战斗发射/落点/命中|AbilityDefinition.delivery、A 既有 CombatDeliveryQueue/Feedback 服务|能力执行器、投递/效果/音画消费者|实际玩法几何在世界平面，来自接受时冻结的 aim/方向及来源；不由附件位置反向改变命中。完整字段见 SO-fields。音画使用同 execution/node 身份，不额外扣费或结算。|

当前排序政策是 **身体 Renderer 的 sortingLayer + 可配 orderOffset**，不是身体内部 slot 间拆层。左右技术资源使用明确 offset，未声称实现任意插槽穿插。若正式武器需在身体片段之间穿插，需用户审查 SkeletonRenderSeparator/额外 Renderer 的候选策略并补实播验收。当前模型未证明正背切换，不能由 `dodge_back` 名称推断存在背面骨架。Point 解析能力存在；本轮实际角色功能只用 Bone/SlotBone，目录如无 Point 就如实为空。

## SO 作者构筑与状态所有权

|定义（真实 Create 菜单前缀 OSSASTRA）|必需及合法内容|运行状态/生效时机|
|---|---|---|
|Characters/Character Definition|ID、AnimationProfile、四段动作、可选 Rig；闪避时间/距离/费用及无敌窗口合法|本体 Root/DataBus/Runtime 持有状态；初始化读取；改角色定义需重建/复验。|
|Characters/Animation Profile|语义/内容 key → 实际 clip、mix、loop、sourceDuration；具体名字只在 SO|接受时冻结动作映射，身体 View 拥有轨道/重播身份；不能运行中追改已经接受动作。|
|Actions/Action Definition|animationKey、合法 Release/MoveReady/AttackReady/Finished；eventTimeline 非空则每种标记恰一个|Freeze 产生不可变时线；动作 Root 顺序驱动，Spine 原生回调不是第二个伤害时钟。|
|Abilities/Ability Definition|schema=1、稳定 ID、executor、delivery、费用/CD、releases、occupancy、action|Compile 快照；AbilityRun 保留执行/来源/阶段；CD、Q/R、装填属于实际装备实例，非 SO。|
|Abilities/Main Hand Loadout|professionId、regularChoices、固定 nativeAbility、合法 defaultQ/R、可选实验来源|同定义可共享；选槽保存于主手实例 payload。常规新技能加入数组，不改 Root。|
|Equipment 定义|主副手身份、行为绑定的 Press/Hold/LongPress 及 executor、实际 SpineProfile、主手技能目录|InventoryLedger 持有唯一装备 payload，跨位置保存 ID/耐久/CD/弹仓/选择；外观无消费权威。|
|Equipment/Spine Presentation|独立 SDA、semanticSocketId、gripBone、所有当前语义映射与实际片段、比例/偏移/排序|角色自身 SlotView 拥有实例与播放，contentVersion 变化重绑；正在接受动作使用捕获映射。修改实时时点不属于单纯换美术。|
|Characters/Rig Profile|schema=1/contentVersion≥1、指定模型 SDA、唯一语义、显式 Bone/SlotBone/Point 字段|Resolve 对当前模型作精确匹配；缺映射拒绝。contentVersion 必须随绑定变化增加，重绑后复验。|
|Items/Healing Definition（既有菜单名，类型 ItemDefinitionSO）|ID、尺寸≥1、stack≥1、rarity/icon/字形、重量；装备 stack1；useKind≠None 时真实 ability；临时挂点可选|SO 无数量。Ledger 唯一条目；InventoryItemUse 每实例状态/预留，接受不消费，Release 一次性提交；Y 仍独立服务。|

SO 字段的实际值、引用目标及菜单类的全文均可由窗口跳转。数值与素材是 SAMPLE，非正式平衡。新行为类型不是任意填表：新增 AbilityExecutorSO/CompiledAbilityExecutor 及必要效果消费者，声明通道、来源费用、准备/释放/取消、共享 HP 网关与音画身份，再补纯合同、真实世界和正常 UI 故事；不得向 Root 增加装备名/片段名 if 分支。

## 已运行的四份配方

1. **同类新近战技能**：B `AuthorRecipe_Melee.asset` 克隆现有 hunter-sweep 的成熟执行/动作/反馈，改稳定 ID 为 `authoring.same-type-melee`；`AuthorRecipe_Loadout.asset` 在原两项常规技能后追加它，B 的主手定义引用该目录，原默认 Q 与固定 E 保持。通过普通 HUD 右手面板 `Configure0_2` 选择新技能，再 Q 受理/释放，身体和右手独立 Spine 同执行 ID。见 `author-recipe-world.json` 6 项；仅隔离威胁的测试夹具，选择和激活用真实设备输入。没有把它冒称第七个原型技能。
2. **同类两盾**：`Equipment_vc02.shield-a.asset` 按住防御与 shield-b 按下攻击分别引用不同 AbilityBinding/Executor，实例先由箱子移入包再装左手；右键源相同、手势/功能不同。A 原有配置是迁移源，B 的 shield-recipe-world.json 10项通过：实际右键按住防御/松开停止、有限箱内盾B经包换手、同按键按下攻击且不进入防御。不能只靠枚举盾类推定功能。不要把 ShieldGuard 旧兼容组件当成所有盾必须采用的行为。
3. **独立 Spine 装备**：B Equipment → Left/Right_PROBE → ProbeSkeleton + RigProfile + `grip` + 每语义映射。`b6-spine-retry.json` 24 项涵盖双手/双本体独立、同执行/重播身份、换装销毁、翻转、暂停和空手拒绝。PROBE 是接口证明，不是正式刀盾素材。
4. **消耗投掷物**：`sample-al-bomb.asset` → `sample-al-bomb_Use.asset` → 共享投递/伤害执行器；InventoryItemUse 的来源是实际库存实例，数量接受预留、释放扣一。交付 reserve 并设优先后伙伴按资格投递；装备 throw 的来源则为装备 payload/耐久/CD，二者不相互扣费。`companion-item-retry.json` 验证实际命中、接管让权、清引用保留数量；natural-supply-final.json 60步正常操作实际完成交付、优先、伙伴投弹及治疗。

## 动作/动画/装备/反馈合同阅读方式

动作实例的权威顺序是 Root 的显式输入→意图/仲裁→运行时→能力/投递→表现，LateUpdate 再读取身体实际姿态并推进装备/临时物品。接口集中于各领域 contracts，具体调用次序仍写在根。动画播放不写战斗状态。

- 普攻四段/动作组 I、II：CharacterDefinition.basicAttacks / 主手 MainActionProfile → ActionDefinition.animationKey → CharacterAnimationProfile 的实际片段。前摇、命中、后摇可在一段素材，MoveReady/AttackReady 是独立授权点；移动恢复时用语义切换，不必拆成三个 Spine 动画。
- 主手 Q/R/固定 E：Loadout→AbilityDefinition→Executor；身体动作、三表现端、投递和效果具体引用均在 SO-fields。释放节点/通道/费用见相应 AbilityDefinition 的字段行，接受时冻结；取消不退款已提交消费，不重复 Release。
- 副手：Equipment.abilityBindings 的 gesture/resourceOwner→Executor；防御持续费用、射击/装填、投掷、盾攻的身体占用不同，不由“右键”硬编码唯一功能。
- 闪避：角色定义时间/距离/无敌窗口与语义映射；不是装备动画驱动位移。受击/破势/死亡/返场由 Combat 网关和本体状态决定表现。
- 食品/药：Support 占用，RegenerationExecutor 发起真实持续恢复；临时图标仅观察相同执行 Release，不产生治疗。暂停用本体 SimulationTime，死亡清理持续恢复，不复活。
- Spine 原生事件完整目录和动作派生事件分开导出。事件名称的用途只有实际映射才成立，未使用事件不被自动注册；重复回调不二次收费或伤害。装备片段可以比身体短/长，按同语义和 replay identity 切换，各自有速率与 mix。

## 冻结边界

用户硬约束：唯一实例/状态、输入控制权、主副手来源独立、SO/实例分离、HP网关、去重消费、独立装备 Spine、数据映射、不改人工维护区/源模型、GitHub 而非 Gitee。工程候选：contracts 组织、Rig字段、当前整 Renderer 排序、检查工具与临时表现。可调内容：数值、时点、混合、技术盾、字体比例。等待用户下一阶段确认：正式骨骼语义/握持标准、前后面、slot拆层/轨道、正式作者规范和导出验收规则；本轮不替用户永久冻结。

官方核对来源：[Spine 组件与4.3变化](https://esotericsoftware.com/spine-unity-main-components)、[BoneFollower/PointFollower](https://esotericsoftware.com/spine-unity-utility-components)。以本机对应 Runtime 源码为实际 API 准绳，不升级插件。

