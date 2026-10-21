# ROADMAP02 验证记录
基准9e10cb3，Unity2022.3.62t16，工作树含用户旧asmdef/meta删除，不包含本轮提交。
## S5
纯策略结构RED：CompanionCombatPolicy缺文件CS2001；GREEN 10/10。
真实InputSystem+Root+CharacterController：team-world 11项通过；实际敌HP72→60(玩家)→48(伙伴)，两方切控与只Stage1，暂停/资格撤回。
边界8项最终通过：敌Accepted入战、墙后候选、锚点、死者、失焦、死敌、平静脱战、权威禁用。
初始边界测试未形成有效攻击：重置与按下同帧未采到松键，敌冷却等待不足；失败原件保留，修正夹具后重测，不改攻击参数。
初次Unity编译因旧全局EnemyRoot命名遮蔽失败，限定Ossastra.Enemies.EnemyRoot后通过。
切控前Accepted动作仍可完成一次Release；不继承AI连段，AI无Stage2。手感/架构仍待用户。
S6/S7/S8未开始。

S5受影响旧ROADMAP01两段真实场14/14通过；新场保存回开；保护76项SHA一致。

## S6
纯语义结构RED后8/8通过；真实世界12/12：Accepted可视、关闭视图同伤害20、墙取消、暂停/禁用清除、两个LineRenderer复用。只读根快照，不增加时钟。

## S7
纯播放身份/回退10/10；真实Spine世界11/11，Idle去重、攻击/受击/硬直/死亡映射、死亡不反复重播、恢复、NPC真实交互、碰撞保留及胶囊隐藏。首次夹具NpcReply.Accepted大小写错误，修正为实际accepted后运行。技术PASS；正式敌美术ASSET_BLOCKED，见assets。
新场玩家两段真实InputSystem/Root/Physics14/14，第二段实际atk2无fallback；保存回开通过。

## S8
真实自然交战12项通过：玩家两段和伙伴Stage1协同击杀；敌方真实攻击致死、切控、10秒返场，敌HP不刷新，无开发直接扣血用于这条自然验证。场景重测通过既有Development Restart而非正式世界复活。
边界实际RED：重复交战Session未拒绝、禁用视图仍被根调用；修复后同Unity探针2/2 GREEN。
旧B2姿态世界22/22、旧R1跟随世界19/19、原M01纯核心29/29通过。旧ROADMAP01两段14在S5后通过；新场两段14在S7后通过。并非全项目或所有旧版本全量回归。
没有手动体感确认，没有正式敌人素材认定，没有新增技能/装备/导航/永久世界存档。

最终新场保存回开后自然路径再次12/12；M01定向共享路由3/3、M02A定向5/5（不是各历史整套）。最终Edit clean、MissingScript0、2角色/1Current协调者/1交战资格/1敌/1NPC、4本地Spine、虚拟设备0、Probe0、预警运行材质0、原InputSettings恢复。保护76项SHA不变；近180秒缓存无error。
