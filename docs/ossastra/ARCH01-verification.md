# ARCH-01 真实验证记录

2026-10-20，本机团结2022.3.62t16。基准b596f97，技术候选通过，人工试玩/架构待审。测试针对本轮共享生命周期、目标契约及场景迁移；未跑完整历史全套或Player Build。运行时测试夹具位于Candidates/ARCH01/Tests，需通过Unity MCP execute_code执行`.cs.txt`；不是生产脚本。

## 分段结果与实际证据

|阶段|结果|说明/证据|
|---|---|---|
|结构RED|预期失败|[intake-red](evidence/ARCH01/intake-red.json)：原敌人目标字段要求玩家根，合法非玩家Vitals不能作为目标|
|NPC纯逻辑|初始3失败，最终9/9|[RED](evidence/ARCH01/npc-red.txt)、[GREEN](evidence/ARCH01/npc-green.txt)，命令`dotnet run --project Candidates/ARCH01/Tests/Ossastra.Arch.Tests.csproj -c Release`|
|ARCH真实世界|22/22|[arch-world](evidence/ARCH01/arch-world.json)，虚拟InputAction→原Root/Controller/Released→实际HP；修复source-disabled取消之前运行|
|ARCH B1边界|15/15|[arch-boundary](evidence/ARCH01/arch-boundary.json)，重复/身份/死亡/暂停交接；修复source-disabled取消之前运行|
|新增中立/NPC边界|20/21→23/23→24/24|[真实缺陷RED](evidence/ARCH01/neutral-boundary-red.json)、[修复GREEN](evidence/ARCH01/neutral-boundary-green.json)、[最终](evidence/ARCH01/neutral-boundary-final.json)，后两轮增加前摇遮挡/死亡、非Unity假目标拒绝；不是删失败或冒称一次全绿|
|迁移后B1实际场|22/22|[b1-world-final](evidence/ARCH01/b1-world-final.json)，最终产品源码，保存重开B1后运行|
|M01共享兼容|3/3|[legacy-m01](evidence/ARCH01/legacy-m01.json)，原入口启动、真实空间命中/一次Release、Missing0|
|M02A共享兼容|5/5|[legacy-m02a](evidence/ARCH01/legacy-m02a.json)，A/B各自实际命中和原协调器切控清空|
|M02C共享兼容|5/5|[legacy-m02c](evidence/ARCH01/legacy-m02c.json)，同范围定向检查|
|R1共享兼容|5/5|[legacy-r1](evidence/ARCH01/legacy-r1.json)，同范围定向检查|
|R1实际跟随世界|19/19|[r1-world](evidence/ARCH01/r1-world.json)，新输入、原Root、移动/单攻/切控/跟随/受阻及同帧屏障|
|场景Play/最终Edit重开|PASS|[Play](evidence/ARCH01/scene-play-audit.json)、[Edit clean](evidence/ARCH01/scene-edit-audit.json)：两玩家8引用齐、两中立目标、MissingScript0、Probe0、原InputSettings、无虚拟测试设备残留|
|保护SHA|PASS|[保护对比](evidence/ARCH01/protected-after.json)：12项中仅授权B1场改变；Prefab/meta、InputSettings、3份移动meta、四早期场及PRE00均字节保持|

真实GameView截图本地`work/OSSASTRA/ARCH01-20261020/arch-game.png`已检查：灰圆柱NPC、红胶囊敌人、双角色和三个开发观察区域可辨。含用户Spine媒体，不上传。最近运行窗口Console Error查询为空；不宣称旧工程历史日志全部清零。

## AR01–AR20 对照

|合同|状态|证据/边界|
|---|---|---|
|AR01|PASS|成员恰为两玩家，世界Tab换A/B；NPC/Enemy不在成员表|
|AR02|PASS|Enemy仅Vitals/Melee/EnemyRoot/Brain，审计无玩家输入或根|
|AR03|PASS|NPC仅NpcRoot，无强制Health/攻击|
|AR04|PASS|Brain消费ICombatTarget，生产Enemy/Combat无CharacterRoot依赖|
|AR05|PASS|最终中立边界中独立无玩家Root目标，被真实Enemy Update/Release命中100→80|
|AR06|PASS|世界A Accepted不扣血，释放72→60一次|
|AR07|PASS|切B实际普攻60→48，来源为B|
|AR08|PASS|真实敌方分别命中A/B，100→80；释放0.5s、请求间隔1.8、距离1.7保持|
|AR09|PASS|B1边界self/friend/source identity/dead；新增disabled/fake/null/destroyed/removed/duplicate ID拒绝|
|AR10|PASS|世界多Collider一次，新增重复同Release明确抛拒绝|
|AR11|PASS|世界出圈；新增前摇中遮挡和目标死亡具名取消|
|AR12|PASS|B1边界敌人死亡取消并隐藏，不能再释放；source组件禁用新增修复|
|AR13|PASS|真实旧Held死亡交接即时Neutral，释放后新输入可用|
|AR14|PASS|非Current死亡不打断当前动作、已释放不回滚、全灭Neutral|
|AR15|PASS|源码唯一Vitals.Health；实际世界三对象Health独立且开发重置引用不变|
|AR16|PASS|CharacterRoot原唯一Advance；EnemyRoot唯一Advance，Brain无Update/时钟；实际伤害一次|
|AR17|PASS|NPC公开入口→自身模块→真实消费者事件；合法逐次，距离/禁用/无效来源拒绝|
|AR18|PASS|独立非战斗NPC无HP启动；敌人无InputHandler启动|
|AR19|PASS（定向）|上述五旧入口，B1完整关键世界；四早期场只验本轮影响点，R1另19项世界；未重跑每个历史测试|
|AR20|PASS（发布SHA另见publication）|保存重开、Prefab/元数据GUID、保护SHA、Missing0/Probe0、最终Edit clean；GitHub回执在发布记录|

## 失败分类与修复

1. 产品边界：新增21项中`disabled-source-cancels-pending-action`失败。EnemyRoot先前对不可用Vitals直接return，留下前摇；改为Cancel("source-unavailable")。保留RED，后续23和最终24均通过，保存后的B1真实世界22通过。未改变HP/时序期待。
2. 类型接线编译：旧解禁代码有全局EnemyRoot，Development最初解析到旧类型。字段改全限定Ossastra.Enemies.EnemyRoot后编译通过。不是业务RED，没有改旧脚本。
3. Editor夹具：初始Undo注册方法参数数目错误，未运行；随后字段名triggerRange误写导致仅添加Brain、配置尚未应用。读回确认targets空后，使用真实triggerDistance完成同一组件配置，未重复加组件，B1保存重开验证通过。
4. Editor通讯：Play域重载数次短暂断开，按同一operation key读取最终ready和当前模式后继续，未以HTTP可达冒充就绪。最后收尾曾复用旧请求key，旧操作ready而当前仍Play，OpenScene被编辑器拒绝、没有资产修改；新唯一key退出并核实Edit clean。此为工具编排错误，不计产品失败。
5. 测试路由：继承B1已验证的临时InputSettings克隆（IgnoreFocus + AllDeviceInputAlwaysGoesToGameView），完成恢复原对象、移除虚拟设备。没有改工程InputSettings资产。R1夹具仅新增相同路由隔离，不放宽业务断言。

NPC当前是可信最小结构演示，不是正式对话/商店/生活系统。正式B2架势、B3/返场、M03连段、追击/寻路、动态热改身份/成员、联网权限验证与Player Build均NOT RUN/未实施。用户手感、命名和集中架构审查继续待审。
