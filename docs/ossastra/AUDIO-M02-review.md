# AUDIO-M02 架构复核

扩展已有 `Ossastra.Audio.GameAudioRoot`，未增加竞争 DDOL 管理者。BgmArbitrator/Transport 的选曲、内容身份、DSP epoch 和 bank 逻辑保留；Transport 仅更新职责注释。新增专业 SfxArbitrator、固定池 SfxVoicePlayer、唯一 Mixer writer GameAudioMixService，SO 只放定义。

核心门禁以 world/source generation/event/fact 的结构身份去重，近期记录最多1024/2秒，活跃许可在整个存活期仍防重复。soft24 是开始抢占的阈值，hard32 是绝对防御上限；本轮立即停止旧短音，不实现预算外淡出。全部借出许可即使尚未起播也计并发。回收幂等，不按播放请求创建声音对象。

BGM 原完整 Gain API 仍可报告 .24×.75=.18，对话为 .081；共享路由使用新 LocalGain，唯一 Mixer 的 Master/BGM 分别乘用户因子。原本地短音 .3 不受 master，现 .3×.75=.225，下降约2.50dB 是明确工程样本。用户音量变化现在直接由 Mixer 应用，音乐失焦与 duck 仍显式平滑。

CombatFeedbackView 保留视觉事件、计数、Arc/LineRenderer 的参数和回收，只移除声音源、PlayOneShot 与每帧4声局部限制。原 clip 字段/meta 保留序列化兼容，但没有隐藏回退播放器。新适配器增加 MainAction 正收据和 Enemy 释放/受击观察，仍不改变释放、HP、输入或动画；声音失败不会回滚结算。

适配器是独立音频场景宿主，不依附可关闭的 HUD。根每场只允许一个事实观察者，拒绝重复组件抢来源代次；关 HUD 不静音全场。Single 切场清旧 SFX，Additive 无关场不撤销当前世界；BGM 已起播内容仍遵循原跨世界政策。无根旧战斗场由明确 Resources 启动配置创建同一个根，其他场不自动接管。

实际 RED→GREEN 覆盖新类型缺失、同源更小限额、动态 SO 优先级、极小音高、重复不可变释放事实、禁用根重启空世界、无关附加场、HUD 宿主和重复观察者竞争。运行中 SO 优先级改动不会倒改已有许可。

边界：HP 没有通用损血事件，本轮使用真实 HP 差分，故同帧多次下降合一；先损血再治疗且净值不降可能不可见。旧 Skills/Offhand 的落地计数不证明 Applied，只命名落地而非命中。声音观察序号不冒充玩法子击 ID。正式语音、环境音、声学遮挡、完整原作音效迁移和主观听感仍未完成。
