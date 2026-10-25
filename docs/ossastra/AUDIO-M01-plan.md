# AUDIO-M01 Implementation Plan

**Goal:** 将已核验原型 AUDIO-01/FIX01 的 BGM 策略与连续性迁入独立 Unity 子系统。

**Architecture:** 规则/身份使用纯选择器与SO；Root独占固定六个2D AudioSource，两组可各含三条原创诊断层。Integration只读已成立场景事实，不改角色、交战或动画。Mixer固定单位增益，Transport唯一写各音源最终volume。

**Spec:** 用户AUDIO-M01 v1.0附件；源远端4d8e61d，目标02b1174。已明确执行授权，不再重复请求设计审批。真实可用技能为项目记忆、brainstorming、writing-plans、TDD及本地Unity MCP；未冒称使用不存在的执行插件。代码分工已登记本任务租约，只有根任务操作Unity。

## 全局约束

原36dirty、角色/Spine/动作配置、原场景、旧Audio和CombatFeedbackView不改；新场仅SC_BGM_AudioMigration。无协程，不改全局输入/音频设置。原作WAV/银行/输出录音仅本地。原声音频未知BPM，整曲循环为评估政策。仅定向GitHub，不Gitee，不扩展SFX/配音。

## Task 1：选择与身份、混音

Files：Scripts/Audio/Core、Definitions；Runtime/BgmMixController；Candidates/AUDIO-M01/Tests/CoreContracts.cs.txt、MixContracts.cs.txt。

接口：BgmArbitrator.Choose(context,rules) → Choice；Track.PlaybackKey不含ruleId；Mix.AcquireSnapshot(token,gain)/ReleaseSnapshot(token)，AcquireDialogueFocus() → IDisposable。

- [x] 运行反射合同，确认缺失模块真实失败；再实现最小模块。
- [x] 核对原型boss100/combat60/title40/far1/explore0与强度1/1/0/.32/.25，普通对话不选曲。
- [x] 绿测同home、段落/循环变更、非法key、文化格式、令牌乘法、精确撤销；新增标题语义而非显示名字的合同。

## Task 2：DSP播放与生命周期

Files：Runtime/BgmTransport、GameAudioRoot、BgmContextHost；TransportContracts、RootOwnershipContracts。

接口：Request(track,dspNow)、Tick(dspNow,unscaledDelta,gain)、Stop/Resume/InvalidateTarget/Dispose。Root读取Host快照并独占停止锁存及原型hold。

- [x] 先红同key轮询、快速重入、固定音源、Stop晚到资源；绿后增加预约第三曲和增益边界红测。
- [x] 预约以实际DSP起点，未知+.08秒/秒级淡变，已知诊断100BPM/下一小节；真正开始前保留旧声。
- [x] Stop锁存、场景目标失效撤预约、重复Root拒绝，验证真实源输出及相位，不用计数代替有声音。

## Task 3：自有配置与场景适配

Files：Audio/BGM/Definitions、Mixer、Diagnostics；SC_BGM_AudioMigration；Integration三个适配器；Tools/GenerateDiagnosticAudio.cjs与BuildAudioMigration.cs.txt。

- [x] 生成12份原创100BPM/4小节单声道诊断，三首原PCM只拷入排除目录；Unity导入与SO/场景仅Editor API。
- [x] 从保护的比较场复制独立联合测试夹具，新增音频根/Host/远室区域；消费原Engaged，无新战斗判定。
- [x] 真实Accepted→交战→原3秒calm→探索→再交战；标题/对话仅明确诊断与端口，不冒称正式流程存在。

## Task 4：监控与交接

Files：Editor/BgmMonitorWindow；AUDIO-M01-review/verification/source-map/publication；同步记录。

- [x] 菜单XingHai/OSSASTRA/Audio/BGM Monitor只读真实Root，显式标记开发覆盖；窗口关闭精确撤自己的覆盖/duck。
- [x] 完成20项定向矩阵、场景往返与重开、原路径SHA、RMS/同步证据；用户主观试听单列未验。
- [x] 精确自有文件清单发布GitHub，核远端SHA，释放租约，停在AUDIO-M01。

2026-10-25：20项矩阵及真实DSP/输入/跨场证据已交付；Player实际复核仍被旧Unity-Favorites-Tool阻塞，听感未代签。定向GitHub产品df90b9f发布及远端SHA核对完成，未进入AUDIO-M02。
