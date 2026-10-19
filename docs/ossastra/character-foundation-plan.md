# 角色基础职责与DataBus修整计划
2026-10-19，用户已批准直接执行；使用现有brainstorming/writing-plans/TDD流程，在当前会话完成。
1. 为朝向独立/每角色DataBus所有权写真实模块探针；旧版运行RED（缺独立职责与数据访问机制）。
2. AssetDatabase重命名CharacterMotor→CharacterMovement，保留GUID与speed/groundStick配置；Root motor字段用FormerlySerializedAs迁移到movement。
3. CharacterOrientation独立掌管Direction/FacingX，OrientTowards只更新XZ方向、不移动物体。
4. CharacterDataBus为每个Root创建一次，持有现有输入/移动/朝向/动作runtime/权限的强类型引用，公开只读结果，不公开任意Set或可写runtime，不复制HP或动作时钟。InputHandler存唯一CurrentFrame；Movement从DataBus.Input读取移动；SpineView与MeleeHit从DataBus读取表现/出手方向，Root保留顺序与生命周期。
5. CharacterInputFrame字段与DebugPanel字段camelCase，保留动作属性PascalCase与同步IEnumerable；输入探针同步改字段名。
6. 在自有共享Prefab添加Orientation并显式接线；参考旧资产/用户dirty场保护，不保存其现场。仅保存自有Prefab/验证场。
7. 核心29项、输入10项、独立朝向/实时许可/输入快照/双角色隔离探针；读回Prefab/scene引用、GUID/旧文件保护。当前用户场不运行；编译图和实际模块验证不冒充全场Play认证。
8. 更新规则、真实数据流/读写权威与待审账本，备份新候选，不自动提交推送。

执行完成：核心29/29、输入10/10、DataBus/朝向12/12、实际场景14/14，最终Edit ready/clean；完整证据与范围见character-foundation.md。所有受影响代码仍待用户审查，未提交推送。
