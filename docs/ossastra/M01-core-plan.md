# M01 Core Candidate Implementation Plan

**Goal:** 完成不依赖Unity/Spine/场景的动作请求、释放、取消和唯一命中HP候选；不冒称M01实机闭环完成。
**Architecture:** 少量直接调用、无静态总线；ControlAuthority唯一管理具名owner禁令；AttackIntent只采样边沿；BasicActionRuntime唯一接受与推进动作；AttackRelease保存单次释放及目标去重；Health唯一管理目标HP。调用方通过返回事实读取表现，不以动画事件驱动伤害。
**Tech Stack:** C# 8/netstandard2.1库；.NET9无第三方测试包的合同测试可执行程序。仅测试宿主使用net9，核心不使用net9专属接口。
**Spec:** 用户v0.4/02_M01实施蓝图与04审查规程；2026-10-18用户将当前批次限定为核心/测试，保留体素尺度、无正式场景。
**Execution:** 已获当前批次实施授权，当前会话逐任务执行；不使用未安装的执行技能、不自动提交计划或候选。

## 固定合同
- 按下边沿请求单普攻；未到AttackReady时拒绝；到达AttackReady后的新按下可替换旧动作，返回旧动作的具名Cancel，不做缓存/连招。动作来源带名，接受才生成唯一Guid，关闭日志不能关闭身份。
- MoveReady、AttackReady、Release和Finish是独立配置节点；时间有限、非负且Release不晚于Finish。AttackReady/MoveReady不早于Release，不晚于Finish；AttackReady后接收下一次实际按下时，旧身体动作以replaced-by-next-attack取消，已提交的释放结果保留；未请求时旧动作到Finish自然结束。
- Tick以调用方提供的有限非负delta推进；暂停不前进；大delta跨节点仍按时间/同刻固定顺序输出，Release恰一次。请求不在同方法释放，Release=0在下一次Tick触发。
- 已完成/已取消只允许一个终结；取消必须具名。释放前取消不产生命中凭据；释放后取消不撤销已提交结果。生命周期挂起清空Held并取消当前动作，解除挂起后要重新按下，不恢复旧Held。
- 控制禁令按owner叠加，释放一个owner不解开另一个owner禁令；移动和普攻通道独立。
- 每个AttackRelease只允许同一Health目标结算一次；HP唯一写入Health；拒绝非法伤害和已死亡目标；目标ID非空且同释放内唯一。空间范围、阵营与视线过滤留在尚未实现的Unity检测调用方，不能将此核心当成真实命中检测。
- 配置数值只在测试中采用临时样本：Release .25、MoveReady .4、AttackReady .6、Finish .8、伤害12，不是游戏平衡定稿。

## 文件与步骤
### 1. 请求与生命周期合同
Files: Core/ControlAuthority.cs, Core/BasicActionRuntime.cs, Core/BasicAttackDefinition.cs; Tests/Program.cs.
- [x] 先写26个真实行为断言，以无行为的临时编译表面取得0/26 RED，再删除临时表面并实现。
- [x] 加真实合同测试：owner隔离、两权限独立、busy拒绝、暂停、取消前后、大步长节点顺序、身份唯一、invalid delta。
- [x] 实现最小直接调用的权限/时钟/返回事实；不引入继承层或事件总线。
- [x] 测试确实先失败再通过；核对同刻节点与零时点。
### 2. 释放与HP
Files: Core/AttackRelease.cs, Core/Health.cs; Tests/Program.cs.
- [x] 测试先要求释放结果能产生一次HP100→88，同目标重复不变、另一目标独立、空挥无结算、取消后不回滚/补释放。
- [x] 实现释放凭据和单次目标去重，Health的实际减血只在其内部方法发生。
- [x] 加死亡、过量伤害、无效数值、重复目标身份碰撞、不同动作同目标等边界测试。
### 3. 交接与用户审查
Files: docs/ossastra/M01-core-review.md, M01-core-source-lines.md, M01-core-authority.md.
- [x] 输出全部实际C#列表、逐行中文解释、真实调用/状态权威表、精确Diff和SHA。
- [x] 每行用户审查均为待审，AI测试不能代签；发布状态仍未批准。
- [x] 明确未做Unity输入/位移/Spine/碰撞/实机HP反馈、暂停失焦事件适配、Inspector参数及Debug入口；不会用核心测试替代场景Gate。
