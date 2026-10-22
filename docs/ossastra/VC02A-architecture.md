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
