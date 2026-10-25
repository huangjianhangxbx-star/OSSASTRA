# AUDIO-M01｜独立背景音乐迁移审查与使用

2026-10-25；基准 `02b11746637a78605f0ce4018a5b4a8e767f2c57`；本轮只迁移原型 AUDIO-01/FIX01 的 BGM。私有 ChatGPT 链接未返回可读取正文，以用户附件及真实源代码为实施依据。

## 使用入口

打开 `Assets/OSSASTRA/Scenes/SC_BGM_AudioMigration.unity`，进入 Play。菜单 **XingHai → OSSASTRA → Audio → BGM Monitor** 观察真实根；窗口不创建播放器。当前编辑器停在此场、Edit clean，已打开监控。

- 正常探索为 home；角色进入场景中 `BGM Far Room` 范围只改变规则和强度，不重新播放 home。攻击获真实 Accepted 后，原 `CombatEngagementSession` 成立交战，音乐转 chapter1；既有约3秒 calm 后返回 home。
- 监控的 title/explore/far/combat/boss 按钮均为 **DEVELOPMENT ONLY** 覆盖。撤销覆盖回到场景事实；Boss、标题和对话暂无线上的正式玩法提供方。
- 对话焦点只将现有音乐增益乘 .45。未来对话端持有 `BgmContextHost.AcquireDialogueFocus()` 的 IDisposable，在结束、取消、目标失效、销毁全部路径 Dispose；Host 失效兜底释放。不能拿场景兜底冒充 NPC 对话已接线。
- Stop/禁用后不自动恢复；需要显式 Resume。用户默认 BGM .24、master .75，失焦再乘 .25；本轮测试保存/重建后已恢复 .24/.75/不静音。

## 职责与配置

`Ossastra.Audio.GameAudioRoot` 是唯一生产 BGM 根，持有纯规则选择器、混音器和 DSP Transport。两个固定 bank 各有三条2D AudioSource，最多分配六条；原曲实际只使用一条，原创诊断使用三条。脚本均在独立 `Scripts/Audio/{Core,Definitions,Runtime,Integration,Editor}`。

`Audio/BGM/Definitions` 提供三首本机 Candidate、四组原创 Diagnostic、两套规则与混音 SO。正式场用 Prototype_Local_Candidates，Engine_Diagnostic_Only 为明确诊断配置。未知 BPM 的整曲按秒淡变，不假称作者 stems 或精确 loop；100BPM 三层仅用于原创诊断。独立 Mixer 保持单位增益，Transport 是 AudioSource.volume 唯一写入者。

同内容 key 不含 ruleId，先于 hold 短路；变版本/段落/素材/循环才可换源。加载就绪只有可用性，不有选曲权。显式停止、销毁和世界失效撤销旧代次。世界失效仍允许已开始的旧曲继续，未来预约不能插入；实时 DSP 时钟参与此边界。旧监控覆盖也随世界失效撤销。

本轮不新增 asmdef：Integration 依赖现有默认 Assembly-CSharp 中的战斗/协调器，强拆会反向影响现有程序集图；采用明确目录和单向引用隔离。

## 审查和修正

先红后绿覆盖了身份、混音、Transport 以及 Host 作用域。独立审阅进一步发现并修正：标题重新启用、已释放令牌保留、旧 Host 抢新世界 duck、迟到有效原曲被错误回退缓存挡住、预约第三曲/刚淡入快速重入的旧声保留、世界失效沿用旧诊断覆盖、失效时钟晚于上一帧 Tick 的边界。没有为这些问题改角色或原型源代码。

原373个保护路径 SHA/缺失状态全部一致，原36项 dirty保留；CombatFeedbackView及meta另以 HEAD精确diff和SHA确认不变。用户维护区、开发细则、Spine/动画/角色配置、旧场景、旧音频、第三方和工程设置未修改。测试在带原dirty的工作树中进行，并非隔离克隆。

## 明确未验和限制

Editor 技术合同、真实输入链、真实音频 API 输出和场景往返已验证；**用户主观听感未签**。逐源输出采样没有观察到切换空窗，不等于硬件端无相消、无爆音或音色满意。正式 Title/Dialog/Boss 生命周期与完整 SFX/语音不在本轮。

独立 Player 构建已实际复核：3.46秒失败，14条错误来自已有 Unity-Favorites-Tool EditorWindow/MenuItem 等引用；未修改第三方规避阻塞，Player 音频没有验收。原作 WAV/BANK 与任何受限录音不发布，干净克隆只能用明确诊断回退。细节见 source-map、verification、publication。完成 AUDIO-M01 即停止。
