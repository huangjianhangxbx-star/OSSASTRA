# AUDIO-M02 实施计划

2026-10-25；附件 v1.0 G0–G8；基准 ddc3378f4a319d553146214f1ec076eed08d0571。用户已授权执行与定向 GitHub main 发布，禁 Gitee。保护清单见工作区 protected-before.json / dirty-before.txt。

## G0 已核查

唯一 BGM 根与 DSP Transport 保留；现有 CombatFeedbackView 是唯一自有短音直播入口。其 .3 本地音量尚未受全局 master 控制。36 项既有 dirty 不处理。实际 Editor 为 SC_BGM_AudioMigration，Edit/clean，编译更新停止；本次读取 audioMasterMute=false（此前任务为 true，未由本任务修改）。输出测试以每次真实状态为准。

## 执行顺序与文件所有权

1. G1：Candidates/AUDIO-M02/Tests 反射缺功能 RED，然后真实 typed 合同。已执行核心 RED：五类型缺失，断言失败而非编译失败。
2. G2：Core/SfxContracts.cs、SfxArbitrator.cs；Definitions/SfxEventDefinitionSO.cs、SfxCatalogSO.cs。纯准入/目录校验、精确事实去重、source/world generation、时效、同源限额和全局 soft24/hard32。达到 soft 后高优先抢最低，立即终止旧声，不做短音淡出；hard 防御上限。序列只属音频观察，不改战斗事实。
3. G3–4：现有 Runtime/GameAudioRoot.cs 组合服务；Runtime/SfxVoicePlayer.cs 固定池、GameAudioMixService.cs 唯一 Mixer writer。保留 BgmMixController 的旧最终增益读数并增加只含 baseline/focus/duck 的 LocalGain。共享 Mixer 独占 Master 与 BGM 用户系数，杜绝双乘。脚本 Runtime/GameAudioBootstrap.cs 与 Definitions/GameAudioBootstrapSO.cs 在音频模块内为有 CombatFeedbackView 的旧场提供同一根；不创建业务局部播放器，不修改旧场。
4. G5：Integration/CombatSfxFactAdapter.cs 从真实已有信号接音；Scripts/UI/CombatFeedbackView.cs 仅移除 AudioSource/PlayOneShot/本地限声，保留画线与计数。先审计能力/普通攻击的实际双路因果，独立 subhit 使用观察序号，禁止按整个 ActionId 吞合法多击。无敌资格仍由 CombatEngagementSession 唯一判定。
5. G6：Audio/Mixer/UnifiedAudio.mixer、Audio/SFX/Definitions 与原创 Diagnostics、Audio/Resources 启动配置均用 Editor API 创建/回读。新 SC_AUDIO_M02_SoundIntegration 从干净已保存 BGM 场建立，不保存旧场。扩展原 BgmMonitorWindow（租约、世界、分组、声部、拒绝、调试试听）。
6. G7：T01–T32 合同、实际输入故事、来源/路由/最终增益、输出、生命周期及旧 BGM/FIX01 回归。Editor 用户静音保持不改。Player 有界一次探测，旧插件阻塞单列。测试恢复用户 prefs 与现场，仅保存自有测试场。
7. G8：baseline/source-map/review/verification/authoring/handoff 与 source-manifest；保护哈希/索引/权限复核，定向提交发布 GitHub，记录真实最终 SHA。不进入 M03。

core 子任务只写纯核心/定义及合同；runtime 子任务只写池/混音及合同；facts 子任务先只读，明确因果后独占适配器。root 唯一 Unity/Git writer，负责根/兼容/监控/视觉接管/资产和验收。无第三方/ProjectSettings/角色玩法改动。

## 判定标准

所有事实结论附可重放证据，区分 PASS-PURE、PASS-EDITOR-SOURCE、PASS-OUTPUT、PENDING-HUMAN-LISTENING、ASSET_BLOCKED、PLAYER_BLOCKED_EXISTING_PLUGIN。成功准入不等于已起播，逐源采样不等于混音后输出；没有听感签字不宣称正式混音完成。
