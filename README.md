# OSSASTRA

最新候选：**ROADMAP01：场景内返场、动画路由、两段手动普攻、有限敌人追击**。技术Gate通过，等待用户集中代码/架构和手感审查。

入口：[集中审查](docs/ossastra/ROADMAP01-review.md)、[全部变化源码](docs/ossastra/ROADMAP01-source-review.md)、[真实验证](docs/ossastra/ROADMAP01-verification.md)、[发布记录](docs/ossastra/ROADMAP01-publication.md)。

本机打开 `Assets/OSSASTRA/ROADMAP01/SC_ROADMAP01_CombatLoop.unity` 后 Play。WASD移动、Tab切人、左键独立请求每段普攻；第一段命中后，在合法接续窗口再按可出第二段，长按不自动续段。非实控仅跟随。死亡退场并按场内SAMPLE计时安全返场。红胶囊敌人可感知、追击、停距攻击、受阻和归位。开发面板读取生命/姿态/返场时刻/段数/动画/敌动，不代表正式游戏UI。

正式2D敌人、专用第二段/受击动画、完整死亡世界规则、正式多职业技能与复杂AI均未迁移。仓库不是可直接运行的完整商用依赖包，见下方克隆限制。只GitHub，不同步Gitee。

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
