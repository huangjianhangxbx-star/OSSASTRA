# OSSASTRA

《星骸回廊》受控迁移工程。最新进度：**M02-C-R1 角色通用跟随能力重构候选**，等待用户架构及手感审查。

## 接手与试玩

先读 [用户规则](OSSASTRA_RULES.md)、[A/C/R1集中审查入口](docs/ossastra/M02C-R1-review.md)、[本轮验证](docs/ossastra/M02C-R1-verification.md)及[7个变化源码的完整行号说明](docs/ossastra/M02C-R1-source-review.md)。本轮逐文件审查状态见[R1集中入口](docs/ossastra/M02C-R1-review.md)，[M02C账本](docs/ossastra/M02C-review-ledger.md)保留历史，发布不代表批准。

本机打开 `Assets/OSSASTRA/M02C/SC_M02C_R1_CharacterFollow.unity` 后Play：WASD移动，Tab切换当前角色，原InputAction攻击绑定保留；非实控角色通过同一Root→DataBus→Movement链跟随。场景调试面板显示来源、距离、状态和原因。遇墙停止，无绕路/传送/自动攻击；领队移动后可重试。仍为两个临时同Spine实例，不是完整小黄/小蓝移植。

已具备：基础移动、显示/攻击方向分离、单普攻命中、控制权切换、最小跟随及暂停/失焦保护。未迁移连段、技能、资源、装备、完整战斗AI；不自动开始下一阶段。

- 编辑器：团结2022.3.62t16 / 1.10.4，Spine4.3.26。
- 自有脚本：`Assets/OSSASTRA/Scripts/Characters/Core`、`Runtime`；开发辅助在`Scripts/Development`。
- 共享Prefab本轮新增可选CharacterFollow；M01/M02A/M02C旧场文件保持不变，未分配目标时不跟随；历史入口：[M01](docs/ossastra/M01-HANDOFF.md)、[M02A](docs/ossastra/M02A-review.md)。
- 纯策略复验：`dotnet run --project Candidates/M02C/Tests/Ossastra.Follow.Tests.csproj -c Release`。
- 领域核心复验：`dotnet run --project Candidates/M01/Tests/Ossastra.CharacterCore.Tests.csproj -c Release`。

## 克隆限制

仓库发布自有源码、测试和场景引用快照，不是本机完整工程备份。未发布商用Spine运行库、用户Spine媒体、target 1模型、Packages清单及全局渲染设置；干净克隆不能保证直接运行。依赖见[DEPENDENCIES](DEPENDENCIES.md)，中文规范见[代码规范](docs/ossastra/代码规范.md)。仅同步GitHub，不同步Gitee。