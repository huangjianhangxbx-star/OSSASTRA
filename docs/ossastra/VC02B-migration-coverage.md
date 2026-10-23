# VC02B 范围与迁移对账

依据：VC02 双轮包 v1.0 的 B0–B8 与范围矩阵；原型固定 c21d4fc755db4fabb0227c690b4e55ebbec19829。A 产品 a5f2df8，接手 main 6c93183。本轮没有切换或改写原型工作树。源 inventory-model/world/operations、item-catalog、al-items、有效 CurrentRules/INV02 修订及最终 backpack CSS 已核对；访问采用后续 3 米、同层/遮挡检查，开包允许移动，重量仅显示。

## 必需内容

|矩阵|源内容与数量|目标与状态|直接证据（Candidates/VC02B/Evidence）|
|---|---|---|---|
|F01–F18、六常规技能、三实验根/17分支/10符文|完整逐源行见既有 VC02A-migration-coverage.csv|继承 A 的通用能力/来源/动作/战斗端口；不复制第二实现|本轮 regression-*.txt 共127；a-natural-regression.json 56；legacy-chain-final.json 15；A原专项证据保持其版本边界|
|I01|sample-al-regen，1|实际库存/缓回/伙伴使用|item-use-final、companion-item-retry、natural-supply-final|
|I02|sample-al-bomb，1|库存预留→能力→共享抛物投递→一次消费|companion-item-retry、natural-supply-final|
|I03|food-0…4，5|五个持续恢复配置，互斥、暂停、死亡清理|item-use-final 30、regen-green 10|
|I04|sample-seed/tool/crop，3|DATA_ONLY：定义、库存、图标/字形、引用；农田生产不迁|31目录、ui-boundary-final、Generated/SO-fields|
|I05|sample-field-lamp，1|DATA_ONLY：库存/展示；环境照明不迁|ground-input-safe-spawn、SO-fields|
|I06|sample-cr-active-upgrade/passive-upgrade，2|DATA_ONLY：合法物品定义/库存能力；新会话不赠送材料，不实现成长经济|SO-fields；A实验等级入口保留，不伪称已接材料消耗|
|I07|treasure-0…4，5|DATA_ONLY：五品质/价元数据/库存；不接商人|SO-fields、原型同源图标|
|I08|magic-0…4，5|DATA_ONLY：不可使用占位瓶，不编造补魔|SO-fields、物品 useKind=None|
|I09|trade-coin，1|实际可移动数量；无第二份 gold 余额|SO-fields、InventoryLedger|
|I10|Y共享瓶，独立服务|保留共享回复与篝火，不混入普通堆|A自然56、B自然全灭35、死亡边界21|
|I11|7个装备定义|右手近战/弹仓主手；左手两盾/射击器/投掷/弹仓射击器；原实例payload贯穿流转|shield-recipe-world、death-world-first、natural-death-final|
|I12|团队5×2、安全2条目、后备5条目、双人4手槽|InventoryLedger唯一权威；有限外部5×8|inventory-core37-green、b1-world-green|
|I13|拖放/拆合/交换/Ctrl/整理/重量|原子校验修订/权限/容量；有限贪心整理不声称最优|纯37、ui-boundary-final 57|
|I14|装配→能力→Spine→卸装|CharacterEquipment只读库存槽位，状态不重建|b6-spine-retry、b6-lifecycle-final、natural-death-final|
|I15|交付/收回/优先/伙伴使用|后备真持有；优先为引用；接管停止新请求|companion-item-retry 14、natural-supply-final 60|
|I16|箱/地面/拾取|鼠标世界合法落点、同Sprite、F与开包拖入|ground-input-safe-spawn 47、ui-boundary-final满包F|
|I17|单死/全灭/返场|单死不掉；全灭原子一次、安全保留、历史包保留、返场不补货|death-world-first 21、natural-death-final 35|
|I18|装备与消耗来源共享投递|装备实例费用与库存数量独立，原AbilityRun事务接入|companion-item-retry；A来源纯合同回归|
|I19|1–0/优先/Tab|仅引用/选中；不新增数字键战斗使用|ui-boundary-final、ground-input-safe-spawn|
|I20|同场生命周期|同场会话初始化有限授予；返场/HUD重开不授予|natural-death-final、final-contracts-reloaded|

普通定义共24，装备7，共31目录定义；初始实例也为31，但二者不是同一个计数：5个团队堆、4个已装备实例、22个箱内实例。两升级材料没有初始实例。后备/安全初始为空。

## 有来源的适配与外观差异

- 原型七个初始堆包含两个旧炸弹装备；B初始团队使用五普通堆，装备测试集合放有限箱子，双手读取原A运行实例。它不是原型开局背包逐格复制，更不是无限补给。
- A原型动作/技能数值与正式rozeul默认动作区分，沿 A 既有适配账本；新增一个同类作者配方不算第七个原型技能。
- B敌人出生点调整为 (4,.03,-3)，使初始箱子与界面可用；只改新B场摆位，未调行为/伤害/感知参数。首个墙后摆位导致故事失败，保留失败记录。
- 最终原型三尺寸真实浏览器参考与 Unity 三尺寸图均保留。窗体、装备行、格距、标题/重量/后备按最终源布局对照；内容长度和Unity字体栅格不声称像素级相同。箱是可滚动格，不通过扩大成满屏遮蔽世界。
- 两角色本体仍rozeul，敌人原僵尸；双手蓝色技术PROBE是独立Spine能力证明，不是正式刀盾美术。原商业模型仅本机引用，不随代码公开。

## 作者资料 D01–D08

D01完整三模型755条目录、D04逐157 SO字段和引用由 `ArtIntegrationCandidate/Generated` 实导；D02功能挂点与空间/握持/显隐/更新顺序、D03动作与事件合同、D05构筑和生效时机、D06四配方、D07规则分级、D08工具菜单和具名预览见该目录 README。`definition-checks.tsv` 111局部PASS、46明确INFO、0REJECT；INFO不代表该类型已有独立检查器。

本包明确排除家园/旅行/商人/农田/环境照明/完整存档/全敌库/正式美术量产/新成长经济。没有把这些排除项记作已迁移。正式挂点语义、穿插层、前后面与最终作者规范由用户下一阶段审查。
