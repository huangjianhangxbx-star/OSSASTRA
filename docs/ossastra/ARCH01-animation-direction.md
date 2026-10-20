# 正式动画方向与现状核查

2026-10-20，用户要求记录后续正式动画能力；本轮仅源码核查和文档，不改代码/场景。R22为明确要求，下面实现方案是建议，尚非批准的详细实施计划。

## 当前实际状态

CharacterRoot.Update明确依次调用：有效输入源Read → 暂停/取消 → ResolveDirections → 动作Request → 唯一Advance/释放命中 → 重新查MoveReady并Movement.Tick → Presentation.SetPaused/Present。主逻辑已集中调度，但未统一包办所有Unity更新：Spine底层更新、Vitals闪白等仍有各自生命周期；没有旧版完整Update/FixedUpdate/LateUpdate及骨骼IK阶段编排。

CharacterSpineView只有idle1_1/run/atk1三条配置；由ActionActive/IsMoving选择，ActionId控制同名重播，写轨0。没有正式状态机动画意图、多来源仲裁、过渡路由、动画库/装备覆盖、上下身轨道方案。这是已知原型能力边界。

## 旧VELGITA实际链路

核对Project_VELGITA/Assets/Script/Character/Core/CharacterRoot.cs及当前工程旧代码副本：Root显式Tick输入、光标、攻击、枪械、朝向、StateArbitrator、Composer、装备；LateUpdate再处理骨骼/IK/朝向/输入清理，FixedUpdate推进状态仲裁。动画Handle事件驱动。

旧Composer按来源保存AnimationIntent，调用ConfigManager.RouteIntent → ResolvePlan（baseMap/itemOverrides/statusOverrides）→ AnimationTransitionRouter → 轨道优先级/Play-Clear合成 → AnimationPlanRequest → HandleSystem.SetAnimation/AddAnimation。存在oneShot过期、replayId、来源和角色校验。配置扩展比三片段选择器强；并非字面无限：语义/逻辑枚举和路由规则仍需维护。转身过渡部分调用被注释，不能宣称所有预想过渡已启用。

旧代码SubmitIntent可即时RebuildAndDispatch，因此“Root统一Tick”不等于整帧仅合成一次。迁移时应检验是否先收齐同帧意图再统一提交，减少中间方案/同优先级依赖提交顺序。全局事件再按Root过滤可以运作；本体接口/实例事件也是可选解耦方式，不能仅因层数多认定更优。

## 灰烬之国：已证与未知

本机只读研究Origin/work/参考研究/Cinderia/24935935-CRAR01/CRAR01-report.md §5：角色/技能选择动画→Unit.Play；覆盖/队列/inheritTime分支；所选主轨0、方向轨9。Hit/Break/End事件时间资料用于技能等待与门控，随后逻辑Attack→发射器/Bullet→真实结算。不能把它简化为Spine回调直接扣HP。

已证链路不等于证实有旧VELGITA同型的通用意图Composer。没有完整原作PlayerLoop/所有技能动态验证，也无性能对照，无法判原作全局系统绝对优于旧版或相反。

## 建议的后续接入边界

- 面向本项目多角色、装备替换、分层动作与过渡需求，优先沿旧VELGITA“语义意图→路由/映射/覆盖→过渡与合成→执行器”的分层方向设计，保留新项目每实体所有权与接口纪律。
- 玩法状态/动作资格与动画呈现状态分离，避免增加第二套动作身份、释放或伤害权威。动画计划携带当前ActionId/版本；取消/切换后的旧回调不能复活攻击。
- 不把当前固定releaseAt等值永久冻结。动画标记、技能配置、攻速/时间缩放应在后续明确生成同一权威动作时间线；换clip/覆盖/过渡后需定义时点映射。Spine回调可作为经身份校验的信号，不能与Runtime同时独立结算伤害。
- Root显式定义输入/玩法推进/移动结果/动画意图收集/单次合成播放以及骨骼后处理阶段；不把所有模块实现重新塞回Root，也不假定FixedUpdate与Update次数一一对应。
- 共享低层播放/配置能力不要求敌人和NPC继承玩家根；三类根继续独立组织各自意图生产者。

当前适合保留最小播放适配器。应在引入多段普攻、攻击移动分层、武器动画覆盖或正式受击动画之前制定动画专项详细计划与验收，避免把三片段分支扩张成正式系统。
