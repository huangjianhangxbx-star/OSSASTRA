# VC02A 验证（进行中）

状态：IN_PROGRESS，不是A_TECH_COMPLETE；ART_ENTRY_READY=false，未执行B。

|项目|证据|目前结果|
|---|---|---|
|手势/冻结/阶段/唯一账本/同类实例|Candidates/VC02A/Evidence/ability-contracts.txt|26项通过；先缺接口红，再实现；绝对到期边界5.1-5舍入问题已以绝对时间比较修复|
|旧装备/防御/体力/副手纯回归|ROADMAP03 Equipment/Defense/Resources/Offhand项目|11+10+18+11通过|
|同操作四种装备行为实际输入/结算|bound-equipment.json|15项通过，虚拟Mouse真实InputSystem采样；两盾同类别不同实例，方向防御真实挡伤/磨损、盾击20、飞行射击15、弧线投递45，切回实例/耐久保持|
|相同投递器、不同来源费用|source-reuse.json|11项通过；测试库存预留不扣、释放扣1、取消不扣、外国来源拒绝；沿同一投递器实际伤害，未磨损当前盾。仅A2测试来源，不算B库存|
|来源表计数|source-definitions.json|TS AST定向提取六常规定义、三根、17分支、28来源（17分支+10符文+1 unresolved）。仅来源登记，不代表全部Unity消费者已迁|

A-T01—A-T24尚未全部执行。以上结果对应本轮当前增量，后续接口改变需重新运行受影响项；不复用过期结果作A8最终证明。

失败记录：初次新共享投递直接调用AttackRelease内部构造器编译失败；改为Core内校验的ForEmission工厂并保留父/索引身份后Unity刷新通过。不是绕开结算网关。测试脚本首次缺InputSystem扩展using导致脚本未执行，补充后正常；没有失败脚本副作用。

定义编译的9项世界程序集校验见ability-schema.json：合法配置、缺ID/执行器、NaN费用、非法几何/投递/释放时间、身体阶段不一致与接受快照隔离。尚不等于全A1校验关口。
