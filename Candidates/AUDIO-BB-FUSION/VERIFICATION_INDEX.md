# AUDIO-BB-FUSION 验证索引

2026-10-27，按本目录已落盘 JSON 核对。此索引只统计本轮结果；同一合同的重复运行不相加，修复前 RED 不混入最终失败。源码、播放许可、实际输出、自然输入与主观听感分别列证据。

## 实景、真实输入与实际输出

| 项目 | 已记录结果 | 证据与边界 |
|---|---|---|
| 四档 A/B | A0/A1/A2/A3 均完成，error 空、恢复原档案成功；不是“4 个玩法断言通过” | [AB_SUMMARY.json](results/AB_SUMMARY.json)。同一已记录游戏事实重放，经真实 OSSASTRA Mixer 捕获；不是重新执行四次自然输入，也不是 AudioLab 离线预览 |
| 主故事自然输入 | **20/21，1 项未过** | [real-input-output.json](results/real-input-output.json) 汇总及 [main-story.json](results/main-story.json) 完整文本报告；失败详情见下节 |
| 主故事独立 PCM 捕获 | 70 秒、48kHz 双声道；peak `0.228223965`，clipped `0`；记录峰值声部 8 | 同上。237 个声音事实、153 个播放、13 个上下文；捕获时长不等于约 99.75 秒主故事的完整覆盖，也不证明主观音色合适 |
| 真实枪械故事 | **7/7**，done、error 空；敌 HP `600→416`，测试设备剩余 0 | [gun-story.json](results/gun-story.json)。真实库存装备、LT 弹仓射击、自动装填、变体不连续重复及实际 Projectile 元数据；不以授予弹药或直接改 HP 假装成功 |
| 世界探索故事 | **8/9，1 项未过**，done、error 空；测试设备剩余 0 | [world-story.json](results/world-story.json)。真实区域/循环重试、跑步地表、墙体静步、Tab 开关、无地面静步、三环境区及离区释放；步行问题见下节。后续补证由 root 收口 |
| Player 构建探测 | **Failed，14 errors，0 warnings** | [player-build-probe.json](results/player-build-probe.json)：`Unity-Favorites-Tool/Editor/QuickAccessWindow.cs` 的 EditorWindow、MenuItem 等编辑器类型编译错误；不能用 Editor 合同代称 Player 构建通过 |

四档捕获的原始指标来自 `AB_SUMMARY.json`，PCM 本体只在本机保存：

| 档案 | 捕获秒数 | 原始 peak | 原始 RMS | clipped | 记录峰值声部 |
|---|---:|---:|---:|---:|---:|
| A0 | 56.1707 | 0.2531052 | 0.00529353 | 0 | 5 |
| A1 | 56.1493 | 0.285223961 | 0.01477569 | 0 | 5 |
| A2 | 56.1493 | 0.243570283 | 0.01571101 | 0 | 9 |
| A3 | 56.1493 | 0.2582144 | 0.01562272 | 0 | 9 |

盲听副本采用**录后标量增益**统一 RMS，原始捕获未改写；不能称为引擎内部响度已经相等。归一化副本和 raw 的 SHA 均在报告中。尚无用户盲听结论，不把无削波或较高 RMS 当成更好的打击感。

## 本轮增量合同与边界回放

| 合同 | 最终记录 | 证据边界 |
|---|---:|---|
| FusionContracts | 8/8 | [结果](results/FusionContracts-green.json)：完整档案、根应用入口、同池循环、来源停止、世界适配器及空档案拒绝；结构/API 合同，不等于全部实景 |
| ProfileLifecycle | 14/14 | [结果](results/profile-lifecycle.json)：非法档案原子拒绝、旧代次失效、循环所有权、同曲 Transport/Epoch 连续、源数有界、A1 保留旧规则与 A2/A3 媒体/增益/种子一致 |
| WorldAudioFactsContracts | 17/17 | [结果](results/WorldAudioFactsContracts-green.json)：距离采样、站立/纵向/无资格/死亡重采/传送/跑速/迟采样边界；真实区域几何与无私有 AudioSource。包含纯采样器，不等于自然步行绑定可达 |
| InterfaceFactsContracts | 15/15 | [结果](results/InterfaceFactsContracts-green.json)：初采与重连安静、状态不变安静、开关单次、换容器先关后开。纯状态边沿，不伪造库存事务 |
| EffectSemanticContracts | 12/12 | [结果](results/EffectSemanticContracts-final.json)：明确 `ice.pulse / ice.shatter` 元数据与真实唯一 HP 回执；旧四/五参数兼容，未知 Periodic 不猜冰 |
| MaterialContracts | 2/2 | [最终结果](results/MaterialContracts-final.json)：第四段已知金属优先于通用重击，未知材质保留通用重击；[早期基础 1/1](results/MaterialContracts-green.json) 不另加总 |
| AcousticContracts | 4/4 | [结果](results/AcousticContracts-green.json)：32 固定源与预分配滤波器、声学配置；不是距离/空间听感验收 |
| AcousticFadeContracts | 2/2 | [结果](results/AcousticFadeContracts-final.json)：真实标记墙衰减，循环淡出保留遮挡系数；记录 `0.68→0.34` |
| GroundPresenceContracts | 2/2 | [结果](results/GroundPresenceContracts-start.json)：**文件虽名 start，内含两条已完成 true 断言**；无真实地面不出脚步，未标记真实地面为 generic |
| WalkRunDefaultsContracts | 1/1 | [结果](results/regression-WalkRunDefaultsContracts.json)：现有满速 `3u/s` 可进入 run 语义；不等于低幅摇杆自然步行已可达 |
| DeathSoundBoundary | 3/3 | [结果](results/death-boundary-green.json)：真实致死、保持交接前 player 身份、致死不叠 hurt 与 death 人声；使用真实网关自有开发边界，不是整个自然死亡流程 |
| FirstAction | 1/1 | [结果](results/first-action-green.json)：实际首动作 accepted、1 次释放及 1 个声音；未再漏首击 |
| InvalidMusicProfile | 3/3 | [结果](results/invalid-music-green.json)：空 ID、NaN 强度、未知条件在世界修改前拒绝 |
| LateHost | 4/4 | [结果](results/late-host-green.json)：Host 先于根、就绪后绑定、真实发布、根重新启用后重连；夹具要求之后退出 Play 并重开自有场 |
| AdditiveHost | 5/5 | [结果](results/additive-host-green.json)：非活动附加场不抢 Host、主动切场才绑定、归还原对话作用域、卸载次场不破坏主 Host、恢复活动场 |
| FusionSceneOwnershipContracts | 7/7 | [结果](results/FusionSceneOwnershipContracts.json)：声音宿主独立于 HUD、拒绝重复观察者、关闭 HUD 不撤租约、不改变声音世界或 BGM；租约 `6→6` |

## 本轮实际执行的旧合同回归

以下计数来自本目录 `regression-*.json`，是本轮真实复验文件；没有用前轮历史通过数代替。

| 回归 | 结果 | 证据与范围 |
|---|---:|---|
| BGM CoreContracts | 17/17 | [结果](results/regression-CoreContracts.json)：仲裁、内容身份、优先级与非法边界 |
| MixContracts | 12/12 | [结果](results/regression-MixContracts.json)：增益、duck、令牌、静音与非有限边界；纯策略 |
| TransportContracts | 16/16 | [结果](results/regression-TransportContracts.json)：原 Transport 合同 |
| SfxCoreContracts | 47/47 | [结果](results/regression-SfxCoreContracts.cs.txt.json)：纯准入、预算与精确事实身份；不证明出声 |
| SfxRuntimeContracts | 32/32 | [结果](results/regression-SfxRuntimeContracts.json)：实际源/路由/池生命周期；报告明确 `sourceOnly=true`、`outputWaveformNotMeasured=true` |
| SoundProfileContracts | 14/14 | [结果](results/regression-SoundProfileContracts.json)；同目录带 `.cs.txt.json` 的重复运行不再加总 |
| VariationContracts | 21/21 | [结果](results/regression-VariationContracts.json)；带 `.cs.txt.json` 的重复运行不再加总 |
| FactProducerContracts | 13/13，gated=0 | [结果](results/regression-FactProducerContracts.json)：自有受击体/零费用真实来源事务，不是自然输入验收 |
| ContactCauseContracts | 9/9 | [结果](results/regression-ContactCauseContracts.json)：接触原因、执行身份和唯一 HP |
| AudioLifecycleContracts | 3/3 | [结果](results/regression-AudioLifecycleContracts.json)：无消费者附加场、根重启和旧来源失效 |

旧 `SceneAdapterOwnershipContracts` 首次被场景名守卫挡住：[原始 blocked 结果](results/regression-SceneAdapterOwnershipContracts.json)，`passed=0 / failed=1`、`OWN_INTEGRATION_SCENE_PLAY_AND_READY_ROOT_REQUIRED`。这不是 7 个断言已执行后失败，也不是旧 7/7 自动继承。本轮仅适配自有场名称的 `FusionSceneOwnershipContracts` 已实际执行原 7 条并通过，原 blocked 记录保留。

## 真实未过、前置条件与待补

1. **主故事 20/21**：`真实敌击导致队伍HP下降` 在约 `21.10s` 的指定观察窗口未观察到新下降，窗口前后均为队伍总 HP `190→190`，状态 `NOT_OBSERVED_OR_REJECTED`。完整报告该刻角色 HP 是 `[100,90]`，已有损血并不能让该窗口断言通过。保留原失败，不改期望、HP 或输入使它虚假全绿。该失败不自动证明敌方伤害或音频完全失效；原因与补证应另列。
2. **世界故事 8/9**：`walk across wood stone dirt` 未过，跑步三地表通过。root 已核现有玩家输入源会把本次 `.55` 摇杆输入归一成满速，实际 player 脚步均为 run，因此本场自然步行覆盖尚不可达。保留失败，不在音频任务改玩法归一化假装通过；纯距离采样器的 walk 检查不能替代自然输入证据。
3. **Player 构建失败**：本轮实际构建探测返回 14 条错误，集中为旧第三方 `Unity-Favorites-Tool` 的编辑器 API 编译问题以及最终构建失败汇总；本轮未修第三方插件，不能声称可分发 Player 已交付。
4. **最终补证**：`scene-cycle.json` 实际Single往返6/6，`original-replacement.json` 原创诊断替换3/3；`final-editor-audit.json` 确认Edit clean、零测试设备/对象/缺脚本，418依赖无私有路径；`source-protection-final.json` 核67源目录项、5FSB及67派生哈希不变，36原dirty保护完整。`WorldExplorationStory-start.json` 只证明启动，完成结果仍8/9。主观听感仍待用户。

## RED 与资产核对如何阅读

`contracts-red`、`acoustics-red`、`acoustic-fade-red`、`effect-semantic-red`、`interface-red`、`material-red`、`material-stage4-red`、`ground-red`、`WalkRunDefaultsContracts-red`、`first-action-red`、`death-boundary-red`、`late-host-red`、`AdditiveHostContracts-red` 与 `additive-focus-red` 保留了缺能力或真实缺陷的修复前证据。最终对应结果见上表，不把这些 RED 重复算作未修复缺陷。`DeathSoundBoundary-red.json` 本身只是启动记录，真正断言在 `death-boundary-red/green.json`。

- [原有 dirty 保护](results/original-dirty-protection.json)：36/36 unchanged，0 changed；不等于整个工程所有文件均未变化。
- [自有场作者配方结果](results/scene-authoring.json)：保存 `SC_AUDIO_BB_Fusion`、120 个表面、3 区、dirty=false、privateReferences=false；它是作者配方结果，不是源码已覆盖全部玩法。
- [精修语义结果](results/refined-semantic-profile.json)：117 条绑定、VALID、猜测 ID 已移除、实际冰语义元数据启用；不是所有语义都已自然触发。
- [事件轨迹](results/EVENT_TRACE.csv)、[声部轨迹](results/VOICE_TRACE.csv)、[Mixer 指标](results/MIXER_TELEMETRY.csv)、[BGM 周期](results/BGMCYCLE.csv) 是追溯资料，不额外虚构一批通过数。

没有将这些异质证据合计成一个“总通过率”。局部合同全绿、实际 PCM 无削波与用户觉得声音合适，是三个不同结论。
