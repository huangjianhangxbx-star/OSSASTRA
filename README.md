# OSSASTRA

《星骸回廊》受控迁移工程。最新进度：**ARCH-01 三类实体根与共享战斗边界候选**。技术定向验证通过，等待用户功能和集中架构审查；未开始B2/M03。

## 接手与试玩

先读[用户规则](OSSASTRA_RULES.md)、[ARCH01审查入口](docs/ossastra/ARCH01-review.md)、[真实验证](docs/ossastra/ARCH01-verification.md)、[本轮12份变化源码](docs/ossastra/ARCH01-source-review.md)和[发布记录](docs/ossastra/ARCH01-publication.md)。A/C/R1既有待审条目保留，发布不代表批准。

本机打开 `Assets/OSSASTRA/ARCH01/SC_ARCH01_EntityRoots.unity` 后Play：WASD移动，Tab切人，左键单次普攻，非实控按原规则跟随。红色几何敌人固定站位、近距无遮挡会主动攻击。A/B与敌人各自有真实Health；角色死亡立即消失并交接存活者，全灭保持Neutral。死亡交接后的旧移动/攻击键须先释放，再重新按下。

开发面板读取真实HP/来源/命中/死亡，可关闭敌人攻击、注入受击或致死边界；“Development Only：同实体重新开始测试”仅用于回放，不是生产复活。没有友方头顶血条。起停/受阻沿原Follow，无寻路或自动队友攻击。

已实现基础移动、方向分离、单普攻、切控/跟随、双向HP/死亡；未迁移正式架势、完整死亡返场和资源延续、连段技能、装备、正式敌人AI。停ARCH-01，不自动B2/M03。

- 编辑器：团结2022.3.62t16 / 1.10.4，Spine4.3.26。
- 自有脚本：`Assets/OSSASTRA/Scripts/Characters`、`Combat`、`Enemies`、`Npcs`；开发辅助`Scripts/Development`。
- 灰色圆柱为独立非战斗NPC，靠近2单位内用开发面板交互，可见真实回应/拒绝。EnemyRoot独立，无玩家输入；共享CombatVitals/Melee仅负责战斗能力。
- B1场经授权迁移为EnemyRoot+Brain；四早期场、共享Prefab、InputSettings字节保持；三份共享脚本移动meta/GUID不变。
- 核心命令：`dotnet run --project Candidates/M02B1/Tests/Ossastra.Combat.Tests.csproj -c Release`；旧领域/跟随合同仍在Candidates/M01、M02C。
- 历史集中审查：[R1](docs/ossastra/M02C-R1-review.md)、[M02A](docs/ossastra/M02A-review.md)、[M01](docs/ossastra/M01-HANDOFF.md)。

## 克隆限制

仓库发布自有源码、测试和场景引用快照，不是完整本机工程备份。未发布商用Spine运行库、用户Spine媒体、target 1模型、Packages清单及全局渲染设置，干净克隆不能保证直接运行。依赖见[DEPENDENCIES](DEPENDENCIES.md)，中文规范见[代码规范](docs/ossastra/代码规范.md)。仅同步GitHub，不同步Gitee。
