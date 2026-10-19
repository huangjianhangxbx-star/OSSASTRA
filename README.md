# OSSASTRA

《星骸回廊》受控迁移工程。当前公开进度：**M01 单角色动作闭环候选，可供架构审查**。

## 给 Chat / 后续任务的接手入口

先阅读 [用户规则](OSSASTRA_RULES.md) 与 [最新角色基础与DataBus](docs/ossastra/character-foundation.md)。2026-10-19按用户授权发布共享控制、InputAction与DataBus修整候选；下一阶段等待用户计划。

先读 [M01 交接状态](docs/ossastra/M01-HANDOFF.md)，再读 [当前实际数据流与接线](docs/ossastra/character-foundation.md)、[审查账本](docs/ossastra/M01-user-review-ledger.md)及实际源码。

已实现：rozeul 验证角色的移动、Idle/Run、单次普攻、真实范围命中/HP、取消与暂停/失焦保护。尚未完整移植原型小黄/小蓝的连段、技能、资源、装备和队友AI。架构逐行审查仍待用户进行。键盘翻转/攻击朝向区分与角色子系统接口仍未完成，详见交接状态。

- 编辑器：团结 2022.3.62t16 / 1.10.4。
- 领域源码：Assets/OSSASTRA/Scripts/Characters/Core。
- Unity适配器：Assets/OSSASTRA/Scripts/Characters/Runtime；共享预制体：Assets/OSSASTRA/Prefabs/Characters/PlayerCharacter.prefab。验证场仍在Assets/OSSASTRA/M01。
- 核心测试：`dotnet run --project Candidates/M01/Tests/Ossastra.CharacterCore.Tests.csproj -c Release`（.NET 9 宿主，核心 netstandard2.1）。
- 核心29项测试通过；场景此前14项受控自动化通过。自动化不代替用户手感或架构批准。

## 克隆限制

本仓库发布自有源码、测试和场景引用快照，不是本机完整工程备份。没有商用Spine运行库、用户Spine媒体、target 1模型、Packages清单及全局渲染设置；干净克隆不能保证直接运行。不得将缺失引用误认作已分发资源。

依赖见 [DEPENDENCIES.md](DEPENDENCIES.md)，历史起点见 [PRE00.md](docs/ossastra/PRE00.md)。仅同步GitHub，不同步Gitee。