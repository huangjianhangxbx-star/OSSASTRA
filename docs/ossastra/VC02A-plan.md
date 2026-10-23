# VC02A Implementation Plan

Goal：完成批准VC02A有限内容及通用能力基底，ART_ENTRY_READY仍为false。
Architecture：定义编译冻结→原动作事务→每实例执行/投递→唯一Combat结算→事实表现；旧场具名兼容，新候选互斥切换新能力路径。时基仍Root.SimulationTime，运动仍CharacterMovement。
Tech Stack：本机Tuanjie2022.3.62t16、Spine4.3、新InputSystem、uGUI，原型c21只读。
Spec：本机work/OSSASTRA/VC02-20261022/spec下00/01/03；02仅接手边界参考。

## Global Constraints
- 角色/敌人/NPC独立根；唯一Current、HP、架势、体力、运动、实例/CD所有者。
- SO不可含运行状态，接受前冻结；来源/代际/请求/执行/释放/脉冲/目标身份有真实消费者。
- 不修改源模型、插件、设置、旧场、用户修改、AGENTS/Skill/开发细则；仅GitHub main。
- 本轮用户明确不等待疑问/现场确认；推荐处理需留恢复证据。原型未实现内容不发明。

## Tasks（按批准A0-A8原关口，不合并内容计数）
- [ ] A0：覆盖CSV逐条F/S/X/B/RN，源文件及SHA、状态/所有者/差异/验收；冻结消费者参数账本。
- [ ] A1：Core/AbilityContracts、GestureState、AbilityTimeline、定义校验；Runtime/AbilityDefinitionSO扩展已有Equipment/Skill；先纯契约红绿，拒绝坏ID/时间/循环/重复付款/非法映射。
- [ ] A2：CharacterAbilities绑定来源和手势、CharacterRoot编号调用、原CharacterActionCoordinator可消费占用状态；盾A/B、射击/投掷与测试库存同路由，具名legacy互斥。
- [ ] A3：Combat共享Delivery/Effects；多释放、冲刺、起落、弹仓/装填、霜寒、区域、虚血/夺回，实际技能驱动；定向纯/世界物理合同。
- [ ] A4：六常规SO、常规Q/R配置E固定、原型与现候选动作组分离；各自实际输入/动画/付款/命中与拒绝。
- [ ] A5：来源求值/冻结/撤回，三实验根、17分支、10符文真实消费者，未实现余震/火箭4排除；按行证据。
- [ ] A6：角色意图映射/过渡与独立装备Skeleton，技术Spine探针仅本轮自有；挂点/翻转/显隐/排序/取消/生命周期。
- [ ] A7：原型uGUI六区与三槽、配置/形态/装备界面；实际资源/拒绝/反馈，不用开发OnGUI代替。
- [ ] A8：24合同组、普通输入故事、保存重开/旧场定向、source-manifest及实际覆盖/交接；推送测得SHA，停A。

实际接口以每阶段源码/architecture记录为准。本地缺superpowers执行技能，按现有工具内联TDD执行，不虚构已调用。

首增量检查点：新候选与四个装备能力配置已运行，26纯契约/9定义校验/15真实装备输入/11来源复用通过。旧装备11、防御10、体力18、副手11纯回归通过。A0源定义计数核验完成，消费者账本仍需补全；A1/A2仍有完整生命周期/手势与作者校验关口，不把此增量报为阶段全完成。

2026-10-23：A0–A8技术验收完成，具体24组证据/限制见verification最终章节；ART_ENTRY_READY=false，停A，不执行B。发布前核对清单和远端SHA。
