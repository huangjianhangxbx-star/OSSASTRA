# AUDIO-M02 来源与所有权

依据用户附件 v1.0；链接可读取的本地同步最新内容主要为 Spine 制作，未冒称含本轮新增音频指令。Unity 产品基准 ddc3378，原型只读固定4d8e61d1b8fc4d7f3da332fdb9301bde8ba46365，不改变本机 Origin 分支/dirty。

| 原入口 | 已有职责 | 迁移边界 |
|---|---|---|
| GameAudioRoot | BGM 唯一生命周期 | 扩展组合 SFX 与共享 Mixer，仍只有一个根 |
| BgmArbitrator / Transport | 选曲 / DSP bank 时基 | 保留，不加入战斗判定 |
| BgmMixController | 完整最终系数 / scoped ducks | 兼容读数，加 local factor，Master/BGM 交唯一 Mixer writer |
| CombatFeedbackView | 信号观察、画线、计数、本地短音 | 画线计数保留；声音由独立 fact adapter 请求统一根 |
| CombatEngagementSession | 唯一交战资格与平静3秒 | 完全不修改，BGM 被动读取 |
| 原型 index/policy/catalog/monitor/audio | 所有权/准入/资源/监控/事实 | 语义参考，不复制 WebAudio ABI 或媒体包 |

新测试短音只使用有来源的现有可公开资源或本轮原创 PCM；原作 bank/受限解码音乐不进入 GitHub。详细事件事实权与媒体清单在实测后补全。

## 已确认事实权
- Root Released排除abilities.OwnsRelease；能力释放与模板释放只取一个。普通Melee和配置MainAction为不同投递权威；每个正回执保留独立音频观察序号，同一ActionId多段不吞。根不可变事实对象重复观察用有界128对象集合挡重，不把子击按父ID去重。
- 能力release/launch/reload→combat.release；hit→combat.hit；guard→combat.guard；land→delivery.land；accepted/cancel/end/miss静音。旧技能结算计数和副手落地只称land，不伪称命中。
- Actor与Enemy的真实HP下降→hurt；有效闪避新ID→dodge；实际瓶UseCount→heal；非空Current实际变更→UI switch。来源租约随禁用/世界/角色生命代次释放，实际Current而非暂时UI输入许可决定Player/Companion组。

## 媒体来源
- Editor AssetDatabase已核旧三GUID对应Art/AudioReferenceLocal/{release,hit,hurt}.wav，本机有效但未公开；源Origin/work/AL-01/audio-extraction.json证明来自Cinderia提取，不能把original录音误称原创。原序列化字段/meta保留兼容，不再播放它们。
- 新公开目录明确使用本轮独立数学合成8个PCM短音，非旧录音音色复刻；可由Candidates/AUDIO-M02/Tests/GenerateDiagnosticSfx.mjs完全重建。24kHz/16bit/mono，无外部采样。22事件SO共享这些片段，音色属于ENGINE SAMPLE。BGM既有受限媒体仍本机排除。
- 原型固定4d8e61d：共享GameAudio/policy/catalog/monitor语义参考；LabAudio独立实验播放器、40ms粗合并和备用振荡器不迁入正式短音根。
