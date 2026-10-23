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

## 第二增量：资源与六常规内容

- 纯能力合同现在30项通过：新增同身份阶段切换、外来身份拒绝、阶段释放，以及移动接触窗口跨批次目标去重。资源合同21项通过（虚血/夺回/衰减、霜寒、实际弹仓/取消装填）；后两种资源的完整技能消费者尚未迁完。
- recovery-world.json：15项实际世界检查通过，真实损血生成虚血、暂停冻结、HUD读取、整次父执行夺回5%上限、普通治疗钳位、周期零夺回、死亡清理及交接。当前仍需最终旧场回归。
- six-skills.json：25项通过，六个定义逐个使用真实InputSystem的Q/E/R输入，验证接受、实际伤害、身体恢复；盾冲包括真实后退、松键、两个阶段转换沿用同一ID/一次付款、实际前冲；火箭跳包括真实碰撞运动和落地85伤害。此证据不替代A4的形态/常规装配/代表拒绝与A5实验来源验收。
- 候选角色A主手目录hunter，角色B目录ranger，Q/R可在原右手装备窗口修改，E固定。只修改自有候选与克隆定义，原场及原角色定义保持原样；现默认双人四段普攻也未被覆盖。
- 新策略编译通过。首次生成火箭配置暴露float(.9)与double(.9)比较边界，验证改为相同float作者单位后重跑成功，未篡改实际0.9秒阶段。
- 真实失败与修复：盾冲持续窗口重复提交瞬时AttackRelease，第二次扫描抛出`Attack release already resolved`，使Root在运动前异常退出。诊断追踪见rush-trace.json（红证据）；新增显式ForContactWindow入口持有本波次目标集，普通瞬时Resolve仍严格一次性，纯合同先红后绿；六技能世界测试重跑25/25。Root临时字符串逐帧追踪已移除，保留Movement的无分配只读请求/许可诊断。

仍未完成：原型动作/形态组、弹仓的实际射击与装填消费者、三实验根/17分支/10符文、独立装备Spine、完整资源HUD与配置反馈，以及A8全套复验。禁止以六技能测试通过宣布VC02A完成。

## 本轮最终技术验收（2026-10-23）
以下状态替代上方历史增量的未完成清单。VC02A技术范围完成；用户手感/架构审定仍PENDING。停止于A，ART_ENTRY_READY=false；没有执行VC02B或接入正式美术。

最终纯合同127项：能力34、资源33、释放体力预留10、动作组10、射击增益12、中立状态10、冰接触9、装备动画9，见pure-final.txt。

|合同组|主要实证（Candidates/VC02A/Evidence）|结果|
|---|---|---|
|A-T01 身份/控制代际|final-contracts-reloaded、natural-story|PASS|
|A-T02 持续方向闪避/重武装|held-axes（18）、natural-story|PASS；虚拟设备进入真实InputSystem，物理键盘人工待验|
|A-T03 手势一次性|pure-final、bound-equipment|PASS|
|A-T04 同类不同能力/同操作换装|bound-equipment（15）|PASS|
|A-T05 独立通道并行/取消|parallel-world（5）|PASS|
|A-T06 接受/释放/持续费用|pure-final预留10、source-reuse（11）、bound-equipment|PASS|
|A-T07 多波/父执行预算|ability contracts、recovery-world（15）、six-skills|PASS|
|A-T08 实际空间投递/遮挡|magazine-world（16）、bound-equipment、ice-world（22）|PASS|
|A-T09 六常规技能|six-skills（25）|PASS|
|A-T10 原生冲刺/火箭阶段|six-skills、rush-effects-world、rocket-effects-world|PASS|
|A-T11 实际弹仓/装填|magazine-world、precision-world（20）、precision-cooldown-world（21）、pure资源33|PASS|
|A-T12 17分支|rush-effects-world（60）、rocket-effects-world（29）、precision系列、shot-enhancement-world（25）|PASS；计数是断言/故事阶段，不是独立技能数|
|A-T13 10符文/互斥/撤回|rush-effects-world八蓝符文、shot-enhancement-world两黄符文|PASS|
|A-T14 真实冰路/冰柱/回充|ice-world、rush-effects-world、rocket-effects-world|PASS|
|A-T15 虚血/夺回/治疗/死亡|recovery-world、pure资源33|PASS|
|A-T16 连段/形态/恢复|main-world（21）、natural-story|PASS|
|A-T17 三槽/固定E/规范CD|six-skills、precision-world、natural-story、final-contracts|PASS|
|A-T18 身体/装备映射与重播|spine-world（20）、pure装备9、six-skills|PASS|
|A-T19 独立装备Spine|spine-world、原创ProbeSkeleton/Atlas|PASS；最小技术装备，非正式美术|
|A-T20 双实例/定义无污染|final-contracts、definition-integrity|PASS|
|A-T21 原六区/正常反馈|natural-story（56）、main-world、shot-enhancement-world、已有skills-ui-local截图|PASS；正式美术质量未审定|
|A-T22 暂停/死亡/切人/来源撤回|spine-world、recovery-world、natural-story、rush/shot来源边界|PASS|
|A-T23 保存/域重载/旧场|final-contracts-reloaded（9）、legacy-chain-final（15）|PASS；旧场为ROADMAP03_DefenseLoadout，未保存改写旧场|
|A-T24 配置扩展/合法资产|同类两盾、六技能SO、三实验根、四组动作、final-contracts全定义编译|PASS|

正常操作故事56阶段使用真实输入和HUD按钮，不传送、不直接扣HP/补资源；包括持续移动闪避、四段连击、换装投掷/盾挡、Q/E、Y、真实死亡切控/返场。边界探针的摆位、资源准备和运行中撤回单列，不能冒充该自然故事。

修复与失败保留：即时装填不再误发模板近战；来源提交失败不扣释放体力；长手势跨阈值释放不误触短按；弹体首帧/射程限制；绑定必须匹配实际手别与槽，native/常规目录ID不可冲突；最后一击通道统一，伤害比例相加后乘2.25暴击；死亡清装填增益；六发弹仓缺一发也触发闲置补普通四发；Spine纹理名与atlas页名一致。早期冰探针误假定100HP、主手夹具忽略切控屏障、旧回归跑错600HP场等红证据保留，未为迎合夹具放宽玩法。自然故事早期死亡发生于配置时站在敌旁，改为普通键盘退开再配置后通过。

局限：媒体/动画/音效为本机复用或明确技术替身，未发布商业媒体。冰霜余震unresolved、火箭缺失第4分支、正式成长/背包经济/VC02B均不在本轮执行。完整全项目退休系统与干净克隆运行不作为本轮承诺。实际产品文件SHA见VC02A-source-manifest.tsv，Git提交覆盖该清单。
