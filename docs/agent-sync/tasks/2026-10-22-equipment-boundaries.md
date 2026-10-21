# 副手能力与装备表现边界核查（2026-10-22）
- 状态：complete / released；仅记录用户新规则及本机源码事实，不重构玩法、不操作Unity。
- 独占：本任务记录、OSSASTRA_RULES.md新增R29-R32、PROJECT_STATE顶部摘要、TASK_REGISTRY追加。
- 源码事实：ShieldGuard是sealed的方向防御实现，明确要求LeftKind==Shield且只处理held；CharacterOffhand明确要求Bomb，固定20体力/4.5射程/.45飞行/1.25爆炸范围；Root按Bomb分支转发。两者虽由根显式推进，但不是通用副手定义系统。EquipmentDefinitionSO目前只有worldSprite/boneName/offset/worldScale及动作引用；CharacterEquipmentView创建SpriteRenderer跟随Spine骨骼，没有装备独立SkeletonAnimation或语义映射。
- 用户要求：副手具体能力、触发手势与表现由装备SO及映射实现决定；同盾可防御或攻击、同右键可短按或长按。投掷的来源（装备/消耗品）与投递方式（抛物/射线/混合）分离。正式装备为独立Spine模型、SO选择骨骼/插槽跟随及语义动画联动；消耗品允许临时贴图。
- 后续设计建议（尚未实施）：输入语义→装备绑定/触发策略→原动作协调/接受事务→具体能力执行；能力调用独立投递器、效果结算及装备表现端口。设备键不直接等价于盾防或炸弹；SO不持有本体CD/执行状态，角色根不按每种物品增加分支。沿用唯一HP/动作/费用权威。
- 现有ShieldGuard可以演化为可组合的方向防御执行器；CharacterOffhand需拆来源适配、投递与效果，不能将现状称为已完成通用化。装备动画共用已接受动作的语义/执行身份与事实，映射到各自片段，不要求双方动画同名或等长，也不另结算一次伤害。
- 范围：用户允许当前过渡，本轮只记录和解释，不擅自展开完整装备/道具重构。CharacterEquipmentView的既有行尾变化及旧禁编两删除保留不提交。
