# M02B2 审查入口

2026-10-21，技术候选完成；用户架构/手感审查待办。基准 bc34538b5296eff4cd0f7b88bb45d874d10920ba。范围仅共享架势与破势硬直，不推进 B3/M03。

## 实现与边界

- 一实体一个 PostureState，CombatPosture 只接收同一次合法 HP 结果。CharacterRoot / EnemyRoot 各自推进；NPC 无战斗组件。
- BasicAttackDefinition 在接受动作时冻结冲击；AttackRelease 携带原 ActionId，不创建第二命中查询或伤害事实。HP 与死亡先结算，只有存活者接收架势冲击。
- 归零当次硬直，取消未释放动作，禁止自身 Move/Attack；只释放自己的 authority owner。期间再受伤不延长硬直，致死优先。禁用/销毁/重置清理自己的状态。
- SAMPLE：最大100；玩家冲击55、敌人60；硬直0.6秒，结束恢复50%；自然恢复等待1.5秒，之后每秒最大值25%。主动作暂停自然恢复；暂停/失焦传零时间。
- 原场未配置 CombatPosture 默认不启用；共享角色 Prefab 未增加强制架势引用，独立 B2 场配置3个实例。

## 源码阅读顺序及职责

完整带行号源码：[source-review](M02B2-source-review.md)，10文件 SHA：[manifest](M02B2-source-manifest.json)。所有文件均待用户审查。

|文件|重点方法/分段|审查要点|
|---|---|---|
|PostureState|构造/ApplyImpact/Tick/Reset|唯一状态、跨阶段剩余时间、恢复边界|
|CombatPosture|Initialize/ReceiveImpact/Tick/生命周期|显式注入驱动根，合法HP回执门；无自主Update|
|BasicAttackDefinition|构造与只读PostureImpact|接受时冻结配置，旧调用默认0|
|BasicActionRuntime|Advance的Release构造|沿用唯一动作时钟和ActionId|
|AttackRelease|构造/Resolve|冲击只读，原一次结算保护不变|
|CombatContracts|ICombatPostureReceiver|窄接收契约，实际消费者为Melee|
|CombatMeleeHit|Resolve结果循环|先HP死亡，再通知同目标架势；不二次查询|
|CharacterRoot|Update第112–155行；OnPostureBroken/Recovered|输入→架势→朝向→请求→动作释放→移动→表现；私有owner清理|
|EnemyRoot|Update及破势/恢复处理|本体唯一时钟；Brain不获得第二动作系统|
|CombatDeathDebugView|DrawPosture/Reset|读取真实状态，可滚动开发面板；不是正式HUD/复活|

CharacterRoot 的顺序不是靠 DefaultExecutionOrder 推断，而是 Update 中明确逐项调用。现阶段没有本体IK/骨骼后处理，不为形式添加空 LateUpdate；Spine 插件内部动画更新独立。R22 的状态/动画意图、路由映射、过渡和可扩展池仍是后续正式要求，本轮没有声称已迁移。

敌方胶囊只是占位表现。自机/NPC/敌方正式视觉均为2D；当前XZ行走和3D碰撞不因此切换成2D物理。

## 验收与后续

见[验证和限制](M02B2-verification.md)、[发布记录](M02B2-publication.md)。技术通过不等于用户批准；旧 M01/A/C/R1/ARCH 待审条目保留。用户可集中审查本轮10个文件，先看状态→接收器→两根接线。不自动追加敌人移动、连段或正式动画重构。
