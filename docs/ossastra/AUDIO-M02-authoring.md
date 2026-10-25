# AUDIO-M02 音效配置入口

运行入口：`Assets/OSSASTRA/Scenes/SC_AUDIO_M02_SoundIntegration.unity`。Play 后打开 `XingHai > OSSASTRA > Audio > Unified Audio Monitor`。该场采用可公开的原创诊断 BGM/SFX；旧 BGM 场仍使用其原本机配乐配置。诊断素材用于确认逻辑，不能代替正式音色。

唯一启动配置：`Assets/OSSASTRA/Audio/Resources/OssastraAudioBootstrap.asset`。它引用现有 BGM 规则/混音、SFX 目录、统一 Mixer 和各输出组。旧场已有根优先沿原规则，不因启动配置覆盖选曲；无根但有战斗反馈消费者的场景由 Audio 模块补同一个根，没有 Host 时不擅自启歌。其他无消费者场不自动生成音频根。

短音目录：`Audio/SFX/Definitions/CombatSfxCatalog.asset`。22 个事件 SO：Player/Companion/Enemy 各七类及 UI 切控。修改事件 clip、变体、音量、音高、优先级、同源并发和空间政策即可换声音。角色切控时适配器选择角色语义前缀，SO 媒体可共享，bus 不在运行时改写。不得把播放声部、已播放事实或冷却写入 SO。

| 字段 | 约束与用途 |
|---|---|
| eventId | 稳定、非空、全目录唯一；业务发语义请求，不直接给Clip播放 |
| clips | 非空且每个有效；按可控随机选择；未就绪媒体拒绝实际起播，不迟到补响 |
| volume | 0..1；本轮 .3 对照旧本地 .3；master .75 后最终 .225，为明确工程样本 |
| pitchMin / Max | .01..3，min≤max；默认 .97..1.03；禁止无限/NaN |
| priority | 0..100，签发时快照；高者可立即抢较低者 |
| maxPerSource | 同来源同代次总并发上限；较小上限不能靠单次替换绕过 |
| maxAgeSeconds | 请求有效期限；默认 .5 秒，过期声音不补播 |
| bus / spatial | 固定分组；默认2D保持旧语义，可显式开3D及min/max距离 |

统一 Mixer 树：Master → BGM / SFX → Player、Companion、Enemy、UI、Interaction、Voice、Ambience。最后两组只是路由语义，本轮没有语音/环境服务。`GameAudioMixService` 是这些暴露参数的唯一 writer。Master 和 BGM 用户值各只乘一次，Transport 仅乘音乐 baseline/focus/duck、淡变与强度；SFX 自己仅乘定义音量。不要再往源音量重复乘 Master。

监控滑块可保存用户设置。沿用 `ossastra.audio.master/.bgm/.muted`，新增 `.sfx` 及 `.sfx.player/companion/enemy/ui/interaction/voice/ambience`。普通对话 token 仅把音乐乘 .45；窗口关闭只撤自己的 token/开发试听来源，不撤别人。开发试听按钮提交 `DEVELOPMENT_ONLY` 请求，不改变角色/HP/技能。

新增业务先找真实事实出口，在 Audio/Integration 建只读适配，取得 `SfxSourceHandle` 后提交事件及具体事实身份；源禁用/换代/退场 Dispose。技能和模板普攻已存在释放权排除关系，不能另加按键声音。一个父 ActionId 可以有多个正命中回执，须保留子事实/观察序号，禁止按整动作或全局40ms吞声。

缺资源、无根、错误 bus、超预算和过期均看 Monitor 原因；Accepted、PlayIssued、实际 SourceStarted、混音后输出、人耳听感是不同验收层次。Editor 全局静音是用户状态，不由业务或工具自动清除。
