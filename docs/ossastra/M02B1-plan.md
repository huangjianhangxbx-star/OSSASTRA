# M02-B1 执行与控制架构Gate

用户明确执行附件v1.0，SHA256 C5EC8272B64D369CE32615D02BF174684DB7F1D226429D4D9CF1D05C457C8CD5。基准6263eda本机/远端一致；旧解禁删除保留不提交，R1 Edit clean。Chat最新用户消息可读，助手正文仍为引用占位，不冒称读到不可见正文。

本轮沿用已读同版project-ankalute-memory、Funplay MCP、brainstorming、writing-plans、TDD/writing-good-tests；已有批准的架构计划直接实施，不重复索要批准。实际失败再定向诊断。原型LT01 v0.3只读核对：真死亡消失、存活者交接、旧持键不转发；10秒/.5秒返场本轮不做。

## 一页权威/风险差异（现状已核对，无需整体架构翻修）
| 路径/唯一所有者 | 现行 | 本轮增量 | 保护/冲突 |
|---|---|---|---|
| Coordinator | 唯一Current/成员/代际 | 订阅Root死亡事件，排除死者并立即选择存活者；全灭null | 不新增队伍死亡名单；无冲突 |
| InputHandler/Selector | 单Frame、切换屏障和攻击松键 | 生命失效即时Neutral；死亡交接增加移动中立重武装 | 普通Tab不改变Held移动旧语义；无冲突 |
| Root | 单动作推进/运动组合根 | 接可选生命只读契约；死亡具名取消未释放动作并通知会话 | 不存HP/isDead；无冲突 |
| Vitals/Health | 目前只有开发靶HP | 每真实角色/敌人一个Health，命中结果回调一次死亡/视觉碰撞退场 | ShowHit不扣血；重置保同一Health，仅Development显式调用 |
| DataBus/Follow/Director | 原状态只读/本体策略/目标分配 | 从Root生命资格自然阻止死者；不增生命镜像 | 不改跟随数学和运动写入权 |
| Released→Melee→Health | 开发靶单释放/去重 | 纯阵营资格前置、真实目标回调，敌方复用相同Melee入口 | Accepted不扣血、已释放不回滚、死亡前摇取消 |

调用：Root/Enemy唯一BasicActionRuntime→Released→Melee空间/阵营过滤→AttackRelease.Resolve→同一Health→Vitals.OnHitResolved→本体退场/Root.CancelAction("death")→Coordinator死亡交接。生命状态只从Health.Current读取；死亡回调标记仅去重通知，不是第二死亡状态。敌人固定站位、近距可见且存活才请求，出圈/遮挡具名取消；工程SAMPLE非正式AI。

## 逐步实施
- [x] P0接手、租约、四旧场/Prefab/input/媒体保护SHA和上述有限Gate。
- [x] P1真实旧路径RED（角色无HP接收敌方Release），纯阵营/生命合同先红；新增Vitals及实际消费窄契约。
- [x] P2同一Melee/Release双向伤害；固定几何敌人独立动作时钟/资格/受击反馈。
- [x] P3死者停意图/取消/退场/单通知，Current安全交接，攻击和移动真实松键，全灭Neutral。
- [x] P4Editor API共享Prefab增量、R1干净副本建M02B1场，Development读数/正式入口受击与显式重置。
- [x] P5纯核心与真实B1世界、相关R1/M01/M02A必要回归；C级风险按B级定向成本，不PlayerBuild或全旧项目扫描。
- [x] P6变化源中文说明/行号SHA/待审与验证记录，选择性GitHub main/远端SHA，释放租约停B1。

默认不动BasicActionRuntime/运动/朝向/Spine/纯跟随数学。共享Prefab只增生命/受击资格及明确引用；四旧场文件不保存。无正式返场/架势/连段/技能/掉落/世界持久化；候选通过不是用户架构批准。
