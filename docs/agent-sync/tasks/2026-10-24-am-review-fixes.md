# AM01–AM03 定向修正实施记录

- 规格：E:/迅雷下载/OSSASTRA_AM01-AM03_新版复核与定向修正清单_982925b.md。用户已授权理解并执行；不另开架构路线，不新增状态执行权。
- 基线：982925b49d773b9427fc7488c149e52d7b53c526，本机与直连GitHub main一致；旧代理失效，后续使用直连。
- 当前唯一unity-writer；拥有F01-F06必要Characters/Animation源、InventoryInteraction接线、Candidates/AM-FIX、本轮handoff及同步。B综合场仅临时验证并复原，不建立永久新场。
- 保护：原31旧场/材质删除、SC_CharacterVerticalSlice、CharacterEquipmentView、CharacterDebugControls、PlayerInputLayout.asset；不回滚、不暂存。不修改旧参考、第三方、用户开发细则。
- 场景以MCP实际回读为准；当前Edit ready、无PrefabStage。聊天链接登录墙不可读，附件为本轮规格。
- 之前关机请求已在上一轮履行，本轮不重复安排关机。

## 实施计划与验证
- [x] F01：现有动作组TryPrepare→正式RequestStage复现；Snapshot增加WithTimeline深复制/速率重定时端口；所有正式表现准备先于TryCommit；实际模式切换及连段、无效映射不扣费。
- [x] F02/F03：正式冻结准备携带来源/候选只读意图，Router覆盖默认ID后再解析/冻结；保持既有多阶段预冻结时点；闪避受理前冻结；装备身份参与持续条件失效。实际路由改片段、冻结隔离、站定换装验证。
- [x] F04/F05/F06：采样分别保留真实按下/最终保持/释放和手势身份；持有者不被别的设备代持；主攻物理保持与申请许可分离；同次update down/up沿正式消费者触发一次；UI初始归属/真释放保护保留。分别做RED→GREEN。
- [x] Movement移动死亡/停用清理与退出配对；文档区分注册式主干和具体子状态对象，不新建竞争状态副本。
- [x] 复用B综合场相关正常操作/动画/能力/背包/生命周期；原错误证据保留，记录硬件未测。定向GitHub提交并核对SHA，无Gitee；更新状态并释放租约。

## 关键复验进展
- F01 RED：真实动作组TryPrepare丢完整计划；RequestStage付款10后抛异常且Accepted未派发。WithTimeline保存完整深复制方案，准备/校验前移；MagazineShot同类替换也改走端口。
- F01 GREEN：实际LT+RT切现有组、RT首击/第二段、各一次付款、唯一释放/结束、无效映射0付款，12/12。不是只调用TryConfiguredAction。
- F02/F03：正式身体技能路由/HasIntent/当前冻结与下一次刷新、实际箱子换盾B+副手身体动作+左装备专属覆盖、单个闪避冻结6项；站定真实库存卸/装3项转换通过，后者用独立真实Spine输出隔离额外运动事实。
- 输入RED：LT释放与断开均被鼠标代持；RT跨背包多出一次Accepted；同次update闪避down/up丢失。GREEN真实输入流程18/18，世界闪避/实际F开箱/UI-down与重武装/四手势快点10/10；原24来源合同、36混合流程、13跨UI举盾通过。
- Movement RED：移动中禁用和死亡均保留IsMoving、缺Exit。GREEN：4/4，立即清事实/速度并配对Exit；一次贴墙方向不满足移动前置的夹具失败另存，不算产品回归结论。
- 夹具边界：真实InputSystem虚拟设备、正式B综合场/实际Spine；非实体手柄操控。第一次盾B试图装入5x2团队包因尺寸被合法拒绝，改为真实箱子→手槽原交换；重复执行技能时冷却按原规则拒绝，夹具重置自己的冷却后复验。失败记录保留。
- 原生命周期夹具反射调用FreezeAbilityPhase需传新增可选准备参数；适配副本在Candidates/AM-FIX，不回改旧证据。
- 最终技能回归定位：既有通用路由Ability→Ability(无来源/条件，priority0)没有身体基础映射，会覆盖Rush/Rocket专用默认ID。纳入唯一必要配置修正Assets/OSSASTRA/Configs/Animation/CharacterLogicRouter.asset：仅移除这条冗余空目标默认规则；常规Ability无显式ID仍走原语义转换，其余作者路由完整保留。修改前该资产git diff为空。此为正式Router接通所需适配，不扩大技能/美术目标。

## 技术收尾
- F01-F06及Movement均技术完成；全量实际结果见docs/ossastra/AM01-AM03-directed-fixes.md最终表。正常17/双手9/生命周期13/六技能25/目标7/冻结5/Spine22、纯核心29/能力34通过。
- 最终输入扩展24/24；同时双设备一次申请、同update松/再按新手势额外覆盖。pulse正式消费者11/11，含技能快点不被最终Held=false抹掉。
- 最终B Edit ready/clean、无PrefabStage；两Root各5机初始化1、Missing0、两身体+四装备Spine有效；测试设备/临时物0、InputSettings复原。最终新日志范围NullReference/MissingReference/CS错误0。
- 发布：本地github.com:443不可达，GitHub官方API可达。核对远端仍982925b；改用API上传精确Git对象、核对树/提交SHA，更新main前做expected-sha保护。仅发布本任务清单；技术租约释放，仅保留文档/发布租约至完成。


## 发布完成
- 产品与全套证据提交c7feaf14c5bd1e7542e79924529c24c712f1fe08；79个Blob逐一SHA一致、完整树6bd4b68413e59cb41ecc6dc09b8989167643adc6、Commit SHA与本地完全一致。官方GitHub API上传，连接器expected_sha=982925b且force=false更新main；远端main已重新读取核对为c7feaf1。无Gitee。
- 本轮全部技术任务complete，Unity/源码/配置/文档路径租约释放；只做本条发布完成记录的最后同步。用户旧dirty完整保留，不安排关机。
