# 当前用户代码审查账本

2026-10-19角色控制修整后，全部区间仍待用户审查。上一版GitHub发布不构成架构批准。

| 当前路径 / 类 | 用户结论 |
|---|---|
| Scripts/Characters/Core/ControlAuthority.cs | 待审，逻辑未改，仅迁移目录 |
| Scripts/Characters/Core/AttackIntent.cs | 待审，逻辑未改，仅迁移目录 |
| Scripts/Characters/Core/BasicAttackDefinition.cs | 待审，逻辑未改，仅迁移目录 |
| Scripts/Characters/Core/BasicActionRuntime.cs | 待审，逻辑未改，仅迁移目录 |
| Scripts/Characters/Core/AttackRelease.cs | 待审，逻辑未改，仅迁移目录 |
| Scripts/Characters/Core/Health.cs | 待审，逻辑未改，仅迁移目录 |
| Scripts/Characters/Runtime/CharacterRoot.cs | 需复审，Root创建/注入DataBus、移动与朝向拆分、动作调用次序及生命周期 |
| Scripts/Characters/Runtime/CharacterInputHandler.cs | 需复审，可配置InputAction、CurrentFrame发布、camelCase字段和生命周期 |
| Scripts/Characters/Runtime/CharacterDataBus.cs | 新增待审；每角色只读访问入口，无重复动作/HP状态 |
| Scripts/Characters/Runtime/CharacterOrientation.cs | 新增待审；独立方向与左右朝向 |
| Scripts/Characters/Runtime/CharacterMovement.cs | 需复审，位移职责独立、从DataBus读取输入 |
| Scripts/Characters/Runtime/CharacterSpineView.cs | 需复审命名/引用，通过DataBus读取动作/移动/朝向，原播放和暂停规则保持 |
| Scripts/Characters/Runtime/CharacterMeleeHit.cs | 需复审命名/引用，通过DataBus读取出手方向，仍为验证靶命中适配 |
| Scripts/Development/Combat/CombatTestTarget.cs | 开发验证靶，非完整正式角色Health组件 |
| Candidates/M01/Tests/Program.cs | 待审，29核心合同测试 |

Scripts路径均相对Assets/OSSASTRA/。当前实际构成和限制见[基础职责与DataBus报告](character-foundation.md)。