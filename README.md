# OSSASTRA

最新候选：**VC01 角色纵向闭环一期，供集中试玩与架构审查**。

入口：`Assets/OSSASTRA/Scenes/SC_CharacterVerticalSlice.unity`。四段普攻、移动中闪避、盾/炸弹手持与UI换装、两种SO技能、共享Y瓶、原型六区HUD、事实音画及原型近战敌人已接入。WASD移动，LMB普攻，RMB副手，Shift闪避，Z切人，Q/E技能，Y共享瓶；Tab背包及G/B/V相关业务仍未接入。

[审查入口](docs/ossastra/VC01-review.md) · [内容/键位/源码地图](docs/ossastra/VC01-content-hud.md) · [验证及限制](docs/ossastra/VC01-verification.md)。手感、听感与最终架构接受待用户审查；停VC01，不自动进入VC02。

以下为历史候选记录。

入口：[集中审查](docs/ossastra/ROADMAP02-review.md)、[15份完整变化源码](docs/ossastra/ROADMAP02-source-review.md)、[验证](docs/ossastra/ROADMAP02-verification.md)、[发布](docs/ossastra/ROADMAP02-publication.md)。

本机打开 `Assets/OSSASTRA/ROADMAP02/SC_ROADMAP02_TeamCombat.unity` 后 Play。WASD移动、Tab切人、左键每段普攻。非实控角色在真实攻击Accepted建立交战后有限接敌并自动Stage1；玩家接管即停止AI新请求。敌人红色地面线圈是当前追踪方向的预计范围，不是锁定落点。原生命/架势/死亡交接与计时返场继续生效。

S5/S6/S8技术PASS；S7技术PASS，**正式敌美术ASSET_BLOCKED**。新场玩家实际映射atk2/受击/硬直；敌和NPC使用已有本地Spine测试替身，不是正式职业或敌人素材迁移。开发UI不是正式HUD。人工手感和架构仍待审查；媒体/商业依赖不公开，干净克隆不保证直接运行。只GitHub，不同步Gitee。

ROADMAP01旧场与交接仍可用于回归：[记录](docs/ossastra/ROADMAP01-review.md)。
## 历史阶段（供回归）
最新候选：**M02-B2 共享架势、破势与硬直**。技术定向验证通过，等待用户集中代码/架构及手感审查。

从 [B2审查入口](docs/ossastra/M02B2-review.md)、[验证](docs/ossastra/M02B2-verification.md)、[10份完整变化源码](docs/ossastra/M02B2-source-review.md)、[发布记录](docs/ossastra/M02B2-publication.md) 开始。

本机打开 `Assets/OSSASTRA/M02B/SC_M02B2_PostureStagger.unity` 后 Play。WASD移动、Tab切人、左键普攻。A/B/敌方各自拥有架势，破势打断并短暂限制行动；滚动开发面板显示真实HP/架势/来源。敌人胶囊仅测试占位，正式视觉仍为2D。未进入B3/M03或正式动画重构。

以下保留上一阶段入口供回归，不代表最新范围：
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

## ROADMAP02 最新技术候选
独立试玩场：`Assets/OSSASTRA/ROADMAP02/SC_ROADMAP02_TeamCombat.unity`。
伙伴有限Stage1自主普攻、敌方追踪预警、独立二维呈现、原两段攻击/死亡切控/安全返场。S5/S6/S8技术PASS；S7技术PASS，正式敌美术ASSET_BLOCKED（本地现有Spine替身）。
阅读 [审查](docs/ossastra/ROADMAP02-review.md)、[验证](docs/ossastra/ROADMAP02-verification.md)、[发布](docs/ossastra/ROADMAP02-publication.md)。媒体/插件不公开，干净克隆不保证直接运行；手感与架构待用户审查。

## VC01 接续记录
正式候选场：Assets/OSSASTRA/Scenes/SC_CharacterVerticalSlice.unity。本轮已形成集中审查候选，技术证据与边界见本页顶部入口。集中入口[VC01审查](docs/ossastra/VC01-review.md)、[资源与原型基线](docs/ossastra/VC01-baseline.md)。
