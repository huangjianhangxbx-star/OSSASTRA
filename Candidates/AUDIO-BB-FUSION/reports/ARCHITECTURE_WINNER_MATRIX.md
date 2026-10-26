# AUDIO-BB-FUSION：架构对照与采用矩阵

日期：2026-10-27。状态：**源码审计完成，当前融合实现是技术候选；本报告不代替运行验收或人工试听。**

## 对照对象和证据层级

- OSS 基线：`efdd6138c8f8dd41f421075e480b34de5d8f6008`。下文旧实现行号对应本轮改动前源码；融合过程中行号可能移动，不据旧行号认定当前实现已通过。
- OSS 音频源码根：`E:/WORLDCREATOR/new/Project_ANKALUTE/Assets/OSSASTRA/Scripts/Audio/`。
- Lab 源码根：`E:/WORLDCREATOR/XingHaiHuiLang/实验工程/BloodborneAudioLab/Assets/BB_AudioLab/Runtime/`。
- Lab 已封存交付：`E:/WORLDCREATOR/XingHaiHuiLang/参考游戏研究/血源诅咒/08_情境驱动AudioLab_V2/MASTER_FINAL/REFOCUS/`。
- 本次只读审计实际核验 `DELIVERY_SEAL.json` 列出的 **160 个文件路径和 SHA-256，全部匹配**。Seal 自身 SHA-256：`20DEFDC53A0E3812BBB29ED9EB976265E05E130A53B1399AB248513A73586856`。

策略只取 `KEEP_OSS` 或 `ADAPT_HYBRID`。`ADAPT_HYBRID` 表示借鉴已证明有用的策略，在 OSS 唯一根和现有合同内实现；没有整包复制 Lab、恢复原作 FMOD 或植入第二个管理者的决定。

## 逐项比较

| 主题 | OSS 已有能力 / 基线源码证据 | Lab 能力 / 源码证据 | 决策与理由 | 候选验证关口 |
|---|---|---|---|---|
| 根所有权 | `Runtime/GameAudioRoot.cs:63` 拒绝重复根，`:95` DDOL，`:153` 切世界释放来源，`:194` 禁用时清理 | `AudioDirector.cs:71` 场景代次，`:197` 禁用 StopAll；未提供 OSS 等价的全局所有权 | **KEEP_OSS**：保留唯一生产根、Bootstrap 和场景 Host | 单一根、单一 Listener；换场后无旧来源存活 |
| 精确事实和代次 | `Core/SfxArbitrator.cs:98` 校验时间、世界、来源、配置；`:114` 精确事实去重；`Runtime/SfxSourceHandle.cs` 租约 | `AudioDirector.cs:130` 语义事件到 cue，`:155` 选择是否 owner 分组 | **KEEP_OSS**：真实战斗事实与世界/来源代次更完整；声音不能反推命中 | 旧世界、失效来源、重复事实拒绝；多段真实命中可各自发声 |
| 变体和节奏 | `Core/SfxArbitrator.cs:120` 按来源代次/事件内容间隔；`:154` N−1 直接排除上次片段，接受后才记历史 | `AudioRules.cs:28` 固定整数混合选片；`AudioDirector.cs:162` seed 序列；owner 冷却可避免敌人互相吞声 | **ADAPT_HYBRID**：保留 OSS 算法；增加档案 seed 与可追溯选片，优先真正不同录音 | 相同事实/seed 可复验；拒绝不污染已接受历史；多录音身份去重 |
| 语义/材质映射 | `Definitions/CombatSoundProfileSO.cs:46` 按动作、阶段、通道、投递模式、实体身份匹配 | `AudioCueLibrary.cs:27` 先匹配地表；`WorldCueResolver.cs:101` 材质、阶段、房间、天气、距离条件 | **ADAPT_HYBRID**：在原 Profile 内扩展可信事实，不另建平行 cue 决策链 | 肉体/金属等只能来自明确目标事实；未知语义/材质显式报告 |
| 声部预算与抢占 | `Core/SfxArbitrator.cs:137` 来源上限优先，`:146` 软硬预算；`Runtime/SfxVoicePlayer.cs:41` 固定池，`:63` 仅许可播放 | `AudioVoiceArbiter.cs:41` 最低优先级、最老声判定；`AudioVoicePool.cs:299` 按空间类型动态增槽 | **KEEP_OSS**：固定 32 槽及来源约束更适合生产 | 24/32 策略不越界；同源约束不可靠抢别人绕过；无事件级 AddComponent |
| 音乐传输 | `Runtime/BgmTransport.cs:58` 两组共六源，`:305` PlayScheduled，`:411` DSP 对齐，内容身份连续 | `AudioVoicePool.cs:159` 普通 Play2D；音乐与短音共享一般容量 | **KEEP_OSS**：替换本地曲目/规则数据，不搬 Lab 音乐传输 | 同曲情境变化不重启；撤销旧预约；对话保持连续；不是短音偷声受害者 |
| 环境规划 | 基线没有完整连续环境声生命周期 | `V4LayerPlanner.cs:106` 纯快照协调，`:155` 迟帧不爆发补发，`:245` 每来源随机状态 | **ADAPT_HYBRID**：以真实区域事实驱动 OSS 来源和许可；本轮以持续层闭环为先 | 进入/离开、禁用、世界切换、拒绝/抢占后重试均需实测 |
| 空间/房间声学 | `SfxEventDefinitionSO` 已有 spatial/min/maxDistance；统一池设置世界坐标 | `V4LayerRuntime.cs:227` 不中断声源的调谐，`:292` 平滑增益/低通/空间混合，`:311` 跨房间 | **ADAPT_HYBRID**：每声部配置与平滑由 OSS 播放器执行；坐标适配 XZ | 远近、左右、遮挡/开放 A-B；不得误把 XY 样例作为 OSS 地面 |
| Mixer 所有权 | `Runtime/GameAudioMixService.cs:69` 唯一参数写入口，各用户因子只出现一次 | `AudioVoicePool.cs:619` 作者增益×淡变×category×试听轨 | **KEEP_OSS**：保留唯一 Mixer 写入口；试听状态独立于持久用户音量 | Master/SFX/分组静音有效；恢复 A0 不遗留额外增益 |
| Trace 和 A-B | `GameAudioRoot` 有 128 条短音历史，适合在线监控 | `V5TraceExporter.cs:22` 完整语义/规则/来源/clip/seed/状态；`:140` 同名素材歧义显式 unresolved；`V3AcousticABController.cs:111` 固定输入仅改变量 | **ADAPT_HYBRID**：另设开发取证采集和导出，保留生产日志上界 | 固定事实、seed、位置、音量；记录原始与归一化指标；实际 DSP 与离线预览分开 |

## 不原样迁移的行为

1. **脚步计时器。** Lab `WorldAudioModel.cs:29–30,550–585` 按 Walk/Run 状态每 0.48/0.30 秒发脚步，没有证明角色真实行走。OSS 应结合碰撞后 `DataBus.IsMoving` / `EnemyMovement.Velocity`、实际 XZ 距离、闪避/强制运动过滤与传送过滤。
2. **提前认定 Loop 已播放。** `V4LayerPlanner.cs:135–145` 在真实播放前置 `ActiveLoop=true`；`V4LayerRuntime.cs:433–443` 拒绝只记 Trace。融合版以真实许可/声部存活为准，丢失后有界重试，不靠布尔状态假装仍活动。
3. **动态声源组件。** `AudioVoicePool.cs:299–308,353–370` 会动态增池及 Destroy/AddComponent；活动容量限制不等于固定分配。保留 OSS 预分配池。
4. **Lab 暂停和演示世界模型。** 不把实验 Pause/Resume、模拟敌人状态、按钮推算命中带回故意不可暂停的正式玩法。

## 已有证据的实际限制

- Lab `PLAYMODE_SUMMARY.json`：113 项，110 通过、0 失败、3 个旧 V4 场景跳过；本次审计没有重新运行该工程。
- Lab `SCENARIOS/CAPTURE_MANIFEST.json`：E01/E02/E03/E04 声部峰值 6/7/7/8，证明决策和运行声源状态，不代表设备输出效果。
- Seal 明确 `runtimeAudioCaptured=false`。六段 A-B WAV 为 `OFFLINE_PREVIEW`，不等于 Unity DSP、遮挡、声卡混音或人工试听。
- `DELIVERY_REPORT.md` 明确警觉/发现缺声、敌攻击人声单片、脚步各组 2–3 片等内容限制。不得用既有通过数字掩盖这些缺口。

## 本轮候选状态

Root 已接入 `AudioContentProfileSO`、档案原子切换、Loop、空间声学及真实世界适配器。`LocomotionAudioAdapter.cs`、`AudioEnvironmentZone.cs`、`AudioSurfaceFact.cs` 是本轮新增，不属于上述基线的既有通过能力。

对应自动化位置：`E:/WORLDCREATOR/new/Project_ANKALUTE/Candidates/AUDIO-BB-FUSION/tests/` 与 `results/`。审计代理已读取落盘结果：世界脚步/几何17项、profile生命周期14项、Additive Host 5项、晚出现Root/Host 4项、死亡边界3项均为true；首个新档案动作实际accepted、释放和声部分别为1；第四段材质2项、遮挡×淡变2项、坏音乐规则3项均有保存的成功结果，对照RED未删除。具体修复路径见 `MIGRATION_DECISIONS.md` D09。

本轮 `AB_SUMMARY.json` 标识为 `OSS_REAL_MIXER_RECORDED_GAME_FACT_REPLAY`：同一录制游戏事实通过4档案重放，经Unity Mixer采集，各约56.15秒，clipped均为0；原始与采集后标量归一化版本分开。它与上文Lab的 `OFFLINE_PREVIEW` 证据不同，也不能称四遍真人操作或人工听感验收。

**整轮最终试玩/回归汇总、构建和发布闭包仍由唯一 Unity writer/root 记录实际结果。人工听感始终 `PENDING_HUMAN`，需用户签收。**
