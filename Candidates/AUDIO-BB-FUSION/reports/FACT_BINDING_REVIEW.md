# 真实事实接线复核与缺口

2026-10-27。只读源码核对及本轮受控增量记录。文中“已接线”指源码已实现，不替代 Unity 运行、实际 PCM 录制、媒体身份或人工听感验收。

## 可复用的事实入口

统一源码根：`Assets/OSSASTRA/Scripts/`。

| 语义 | 真实 API 与位置 | 当前接法或约束 |
|---|---|---|
| 玩家/伙伴动作 | `Characters/Runtime/CharacterRoot.cs:89`，`event Action<ActionFact> ActionObserved` | 接受时冻结动作组/阶段；释放时出声，不按点击预测 |
| 敌人动作 | `Enemies/Runtime/EnemyRoot.cs:41`，同签名事件；`:103-105` 发布 Released | `enemy.basic` 是适配器 selector；媒体身份必须独立核对 |
| 能力反馈 | `Characters/Runtime/CharacterAbilities.cs:66`，`event Action<AbilityFeedbackFact> FeedbackObserved` | 读取定义、执行 ID、通道、投递方式、EmissionIndex；忽略 accepted/cancel/end 的通用战斗发声 |
| 接触回执 | `Combat/Runtime/CombatContactGateway.cs:16`，`event Action<CombatContactBatch> Resolved` | 使用真实 Applied/Guarded、目标身份及位置；不重做伤害判定 |
| 单次死亡 | `Combat/Runtime/CombatVitals.cs:38`，`event Action<HitOutcome> Died` | 本轮已订阅 `target/death`；不把 HP 轮询下降冒充死亡事件 |
| 玩家死亡身份 | `Characters/Runtime/CharacterRoot.cs:128`，`event Action<CharacterRoot> Died` | 已有会话处理，音频不接管换人或返场 |
| 闪避成功 | `Characters/Runtime/CharacterDodge.cs:20,23`，`Guid ActionId`、`bool IsActive` | 适配器观察新的成功动作身份，SO 映射 `movement/dodge` |
| 共享回血成功 | `Characters/Runtime/HealingBottleService.cs:18,24`，`int UseCount`、成功使用事务 | 适配器观察 UseCount，映射 `item/heal`；满血/余额失败不发成功声 |
| 消耗品消费提交 | `Inventory/InventoryItemUse.cs:23`，`event Action<CharacterRoot,Guid,ItemDefinitionSO> Released` | `:88-89` 在 `CommitConsumption` 成功后通知；与能力 release 不得双播 |
| 恢复类能力 | `Characters/Runtime/RegenerationAbilitySO.cs:50`，`Context.NotifyFeedback("release")` | 真正开始恢复后通知；定义为 `food-0.use` 至 `food-4.use`、`sample-al-regen.use` |
| 界面打开状态 | `UI/InventoryView.cs:57,59`，`bool IsOpen`、`string OpenedExternal` | 本轮 InterfaceAudioAdapter 观察实际状态边沿；初始/重连不补播 |
| 容器位置 | `Inventory/InventorySession.cs:195`，`Vector3? ContainerPosition(string id)` | 关闭读取旧容器，打开读取新容器；位置未知不猜坐标 |
| 库存刷新 | `Inventory/InventorySession.cs:37`，`event Action Changed` | **不是成功事务**，本轮不直接用来播放拾取/装备声 |
| 玩家实际移动 | `Characters/Runtime/CharacterDataBus.cs:24-25`，`IsMoving / Velocity` | 来自 `CharacterMovement.cs:53-57` 的碰撞后位移；禁止用按键代替 |
| 敌人实际移动 | `Enemies/Runtime/EnemyMovement.cs:15-16,29`，`Velocity / HasMovementRequest` | 真实速度与移动请求共同筛选；受迫位移不是脚步 |
| 破势与恢复 | `Combat/Runtime/CombatPosture.cs:32-33`，`event Action<string> Broken`、`event Action Recovered` | 真实出口存在；不应仅凭受击动画推定破势，是否配声须另核接线/内容 |
| 交战 | `Combat/Runtime/CombatEngagementSession.cs:20`，`bool Engaged` | 原 BGM 适配只读该事实，不按区域位置重新判定遇敌 |

## 已有覆盖与本轮补齐方向

旧公共 `OssastraCombatSoundProfile.asset` 有 72 条绑定、15 种 cue。此数字描述 AUDIO-SFXPASS 基准，不作为本轮 Bloodborne 本地目录条数：

```text
blade.swing.1 / blade.swing.2 / blade.swing.3 / blade.swing.4
blade.fast / blade.heavy / blade.impact / blade.impact.heavy
blast.small / body.hurt / guard.metal
gun.fire / gun.reload / launcher.fire / control.switch
```

旧基准实际覆盖普通四段释放、部分主手动作/技能、枪械/装填、明确投掷落点爆炸、玩家/伙伴呼痛、格挡及换人。它没有因为存在四个 swing cue 就拥有四份不同原始录音；素材变体数量必须核对本轮来源清单。

| 缺口 | 当前状态 | 不得代替它的推断 |
|---|---|---|
| 闪避、回血瓶 | 原真实观察已经存在，本轮补内容映射并待回放 | 按键按下不等于合法释放 |
| 敌方释放/受击 | 真实出口已存在，本轮可按敌方角色映射声音 | 不能把玩家人声默认套到所有敌人 |
| 死亡 | 本轮新增 Vitals.Died 接线；仍待生命周期/真实死亡回放 | 一次普通 HP 下降不是死亡 |
| 食物/持续恢复 | 已有真实能力 release，本轮可按定义配置 | 库存刷新不是消费提交 |
| 脚步 | 本轮新增只读距离适配，待实际墙体/传送/失焦/死亡验证 | 输入 held、动画名或每秒计时不能代替实际移动 |
| 背包/容器开关 | 本轮新增状态边沿适配；纯合同先 RED，后续 GREEN 与实景验证由 root 执行 | Toggle 请求、UI refresh 不等于打开状态变化 |
| 拾取/装备/移仓成功 | Ledger 能提供真实变化，但通用 Changed 不足以给出操作类型；本轮不能据此泛播成功声 | 不能从 `LastResult` 文案猜业务事务 |
| 敌方 alert | **未实现：没有明确真实警觉/察觉事件入口** | `HasCombatTarget`、距离或 AttackAccepted 都不自动等于原作 alert 语义 |
| 标题/故事音乐入口 | 依赖真正场景/业务 Host，不能只创建曲目 SO 就宣称接入 | 开发切档按钮不是正式标题/剧情入口 |

## 顺序、身份和重复风险

### 同帧释放与接触

同步投递可能在一次调用内先完成接触，再返回 release 反馈。适配器保留原玩法结算顺序，只在自己的待发声队列中把 release/launch 排在 contact 之前，不能为了声音先后移动伤害、付款或动作时钟。

同一执行下的多目标/多子击必须保留 `ActionId / ParentExecutionId / EmissionIndex / TargetId`。不能以“父技能刚播过”为理由吞掉后续合法接触，也不能让周期冰路套用一次普通挥刀。

### 死亡与自动换人

`CombatContactGateway` 在原结算分发时调用目标 `OnHitResolved`，`CombatVitals.Died` 因而先发生；整批 `Resolved` 在原分发循环结束后发布。角色死亡回调可能在这一帧切换 Current。

本轮 `CombatSfxFactAdapter.Pending` 已保存 `prefix` 和原 `lease`，Queue 冻结角色身份，Flush 使用冻结身份并检查原租约。它修正的是已排队事实在 Flush 时被重新标成另一角色的问题；并不声称所有跨领域事件在死亡前都拥有同一 Current 快照。真实死亡、换人和旧租约回放仍必须验收。

### 格挡出口重复

真实格挡会同时到达 `DirectionalGuardAbilitySO` 的能力反馈 `"guard"` 与网关 `target/guard`。公共接触反馈应由一个出口负责，或按真实执行/目标身份去重，不能无意叠两份金属接触声。现有 `guard.metal` 是作者选声，不代表项目已有独立“金属盾材质”业务事实。

### 消耗品出口重复

`InventoryItemUse.Released` 的真实消费与 `RegenerationAbilitySO` 的真实 release 描述不同观察层面的同一次使用。若同时接线，必须先决定前者只做物件展示/操作音、后者做能力效果音，或只选择一个声音入口；不能把两个通知无条件当成两次使用。

### 来源与状态重采

首次发现已打开 UI、世界变更、内容档案更换或租约重建应采样当前状态，不补播旧操作。旧队列不能换上新来源代次。停止/关闭某一 UI 表面只释放本来源，不得停止唯一音频根或其他实体的声音。

## 材质与空间事实边界

`AudioSurfaceFact.surface` 用于明确地表，`impactMaterial` 用于明确接触目标材质。`CombatSoundProfileSO` 新增可选 `material` 条件；普通近战回执仅在真实目标存在标记时提供该值。

- 未标记地面：`generic`，不是 stone。
- 未标记接触目标：未知/空白，不是 flesh。
- 名字含“盾”、金属色贴图、某个固定坐标均不是材质事实。
- 目标并非 CombatVitals 的物件、墙面拦截与空落不一定拥有可用目标材质出口，不能宣称当前所有碰撞均已完成 wood/stone/metal 分类。
- `AudioAcousticObstacle` 明确声音遮挡几何；声学反馈不修改战斗 LOS、移动碰撞或寻路。

## 验证状态与后续核对

本子任务未运行 Unity、没有记录实景通过数。`InterfaceFactsContracts` 的缺类型 RED 已由 root 实际执行，结果 `passed=0 / failed=1`；之后实现 `InterfaceAudioEdges` 与 `InterfaceAudioAdapter`。纯合同计划覆盖 15 项，但只有 root 的结果文件可以确认 GREEN。

root 应统一核对：正常输入开关与容器交互、A0/A1 不增加世界音、A2/A3 合法状态出声、同帧死亡/换人身份、墙体停步、闪避/击退不发脚步、来源重连/换场后没有循环或旧队列泄漏、混音实际输出及本地素材来源。源码通过和 PCM 无削波都不能代签用户的主观听感。
