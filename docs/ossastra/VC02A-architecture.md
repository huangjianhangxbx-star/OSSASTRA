# VC02A 当前架构（随实现更新，不是最终完成声明）

## 已落地的所有权
- CharacterRoot仍拥有原BasicActionRuntime及模拟钟、按编号顺序调度。新CharacterAbilities端口与旧Skills/Guard/Offhand路径互斥，普攻/闪避继续原门禁。
- CharacterActionCoordinator/ActionAdmissionLedger仍唯一接受与体力付款事务；来源预留在同一接受回调内进行，执行端拒绝时回滚预留。
- EquipmentState按定义ID持有独立实例/耐久，同类两盾不再共用一个槽位实例；legacy:Shield/Bomb仅供旧枚举入口。
- AbilityDefinitionSO只组合配置；CompiledAbility冻结释放/几何/费用/执行策略，策略为每次请求建立独立Run。ActionDefinition仍负责原身体Runtime阶段与动画映射。
- GestureRecognizer不读取设备，以原模拟时间和控制/绑定/捕获/暂停代际将操作解析为单一手势结果；Hold立即开始，Tap/LongPress竞争至多一个胜者。
- CharacterAbilities按实例+规范化能力ID保存CD，按输入绑定调用专职执行器；Root不增加具体装备ID判断。现在仅Timeline与方向防御执行器落地，冲刺/火箭/装填等尚未迁完。
- CombatDeliveryQueue为中立共享接触消费者，不引用CharacterRoot/装备/库存；AttackRelease派生批次保留ParentExecutionId/EmissionIndex，CombatContactGateway仍唯一防御与伤害结算。
- IAbilitySource区别来源：生产装备端口提交实际实例磨损；A2测试库存端口在接受时预留、释放时扣数量、取消释放预留。测试库存不是B的正式物品系统。

## 当前可运行入口
Assets/OSSASTRA/Scenes/SC_VC02A_CombatMigration.unity（复制已保存VC01，不覆盖用户命中层设置/原场）。
两个同类盾定义分别绑定Hold方向防御与Press攻击；另有射击器/投掷装备，通过同一Secondary操作路由。
独立Spine装备、六完整技能、三根/17分支/10符文及正常三槽配置仍为后续A阶段，不能把本阶段当作A_TECH_COMPLETE。

## 当前适配合同及待完成项
- Timeline身体能力必须使动作阶段与释放计划一致；仍走原Runtime，原释放被本能力消费，不让模板伤害重复打一次。
- 派生队列在暂停时不推进；切控取消尚未释放Run，已离手队列不靠输入重发，死亡/禁用清理。
- 当前普通CD以Root模拟时间计；原型副手仅实控推进的CD政策尚待A3/A4细化，旧场逻辑不受新候选路径影响。
- RayLandingArc是直线合法落点+弧线表现+落地范围的组合，不能称连续抛物线碰撞；Parabolic采用逐帧段扫掠。边缘几何/动态碰撞合同仍需完整A3实证。
- 新增定义/执行器脚本均有中文职责说明，Root调度/身体事务/冻结/派生身份已同步注释；最终审查清单在A8生成。

第二增量：30纯能力合同、21资源合同、25六技能输入/命中/阶段检查通过；四装备15、不同来源11、虚血世界15重新通过。新增Root拥有的同ID阶段转换、动作映射接受冻结与独立表现重播ID，能力只提交运动速度，唯一Movement执行。候选A/B主手职业目录及Q/E/R已配置，E固定。持续接触单独ForContactWindow，不放宽旧瞬时单次批次。具体差异/红绿证据见verification；原型动作组/形态、射击/装填消费者、实验来源、独立Spine和全套A8尚未完成。

## 最终候选集成（2026-10-23）
后文替代上方历史增量的“尚未迁完”状态；保留前文仅作迭代记录。

CharacterMainAction持有主手动作组及本次接受快照，Root在空闲刷新候选，仍只由原BasicActionRuntime接受/推进。默认两角色四段不变；正常右手面板可选hunter I4/II2、ranger I3/II2，空选恢复默认。有限预输入、末段恢复、动作位移和扇区投递由冻结配置驱动。凿冰仅通过原型I动作组消费，不暗改默认候选。

CharacterAbilities绑定实际手别/操作槽，UI与键盘共路由；右手实例payload保存三槽、规范化CD身份、弹序列/容量、霜寒、来源集合及装填增益。接受预留释放体力，释放将来源提交与预留扣款合为一次事务；来源失败不扣体力。占用与按身份取消不会清掉另一通道已释放的效果。

三实验根复用专职Run：冲刺准备/释放/冲锋；火箭起跳/实际碰撞运动/落地；精准装填同一弹仓。native-E与rush/rocket别名共享规范CD。EffectSourceSet求值active/dormant/superseded/unresolved，接受冻结；撤回清理仍依赖持有的延迟副作用，不改已经接受的直接伤害快照。ShotEnhancementState的装填伤害比例与最后一击同层相加，随后乘一次性2.25暴击；仅Applied射击回执消费暴击。普通闲置装填将精装六发容量回到普通四发。

CombatStatusEffects由各实体根推进，无Update/Move；眩晕撤销未释放动作，受迫位移/减速仍由各自原Movement的唯一CharacterController.Move执行，保留墙碰撞。穿透只租用敌方碰撞对并在终结恢复。CombatIceWorld由来源模块推进，冰区按来源+目标合并脉冲；冰柱拥有4HP中立ICombatTarget，首个合法区域/近战接触碎裂，原产生动作及普通子弹不触发，碎片经共享网关结算。冰区与柱不引入第二HP/移动权威。

独立装备Spine为原创技术盾：自己的Skeleton/Animation、握持骨骼及八种语义片段，身体挂点/插槽颜色显隐/镜像/握持对齐/排序分别实现。Root LateUpdate推进；接受捕获装备映射，事件不回写玩法。装备仍允许Sprite技术兼容表现，正式装备美术尚未接入。

现代能力事实接入原CombatFeedbackView：接受、释放、命中、取消、结束、格挡、起跳/落地与装填完成分别处理，拒绝不冒充命中。HUD沿原六区，弹药只在实际弹仓绑定时显示，霜寒沿蓝职业来源显示；技能资源资格与正式请求门一致。技术音画复用既有本机资源，未声称正式美术完成。
