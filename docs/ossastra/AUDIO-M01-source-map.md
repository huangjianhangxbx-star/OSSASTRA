# AUDIO-M01｜原型 BGM → Unity 来源覆盖对账

日期：2026-10-25（Asia/Shanghai）。本文只对照背景音乐迁移；不代表整个 AUDIO-01、全部音效或主观试听验收。

## 1. 精确基准

- **本轮读取的源 GitHub main：** `huangjianhangxbx-star/-@4d8e61d1b8fc4d7f3da332fdb9301bde8ba46365`。由 GitHub 连接器分支 GET 核验，也与本机 `refs/remotes/github/main` 一致。AUDIO-01 初版相关提交为 `4f7638b64fe5b1c88409448b098a23f32ceca90a`；连续性 FIX01 为 `61e1052c0fb01bd8e335b9e0b39a615dd2456d4c`，当前 main 已包含。
- **本机 Origin 工作树不是此版本：** `E:/WORLDCREATOR/XingHaiHuiLang/Origin` 的 main HEAD 为 `86824bdf358bd666072809cac6a7d3491b4c384d`，`git status --porcelain` 当时列出 773 项（默认未跟踪目录折叠口径）；该旧 HEAD 没有 `game/src/game-audio/`。未切换、恢复、清理或提交这份脏现场。
- 实际阅读基于已有 Git 对象的定向只读导出：`E:/WORLDCREATOR/XingHaiHuiLang/work/OSSASTRA/AUDIO-M01-20261025/source-audit/source-4d8e61d/`。无源码 checkout，无 Gitee 同步。下表源行号均固定于此 SHA。
- 来源目录：[原型 game-audio](https://github.com/huangjianhangxbx-star/-/tree/4d8e61d1b8fc4d7f3da332fdb9301bde8ba46365/game/src/game-audio)。

## 2. 行为 → C# 承载

目标类均位于 `Assets/OSSASTRA/Scripts/Audio/`，不写入 Characters、Spine、角色 Bus 或动作系统。

| 原型精确位置 | 迁移合同 | Unity 承载 |
|---|---|---|
| `policy.ts:25–35` | boss 诊断100、combat60、title40、far1、explore0；dialogue 不独立选曲 | `Core/BgmContext`、`BgmContracts`、`BgmArbitrator`、`Definitions/BgmRuleSetSO` |
| `music-content.ts:2–7` | title→title、explore 与 far→同一 home、combat→chapter1；版本/段落/循环进入 key，规则名不进入 | `BgmTrackSO`、`BgmContentIdentity`；Candidate/Diagnostic TrackSO 与 RuleSetSO |
| `index.ts:17–23,120–121` | 单正式所有者持有音源、缓存、混音和 transport | `Runtime/GameAudioRoot`；重复根拒绝；独立 BGM Mixer/AudioSource |
| `index.ts:89–98` | 同 key 先于 hold 短路；只更新规则、原因、强度，不重建 source/epoch/transport | `GameAudioRoot`、`BgmTransport.Request/SetIntensity` |
| `index.ts:85–88,94` | 加载仅报告可用资源；当前有效目标、显式 stop、disposed 守卫决定播放 | `BgmTransport` 素材就绪轮询与目标代次；`GameAudioRoot` Host 有效性/停止门控 |
| `index.ts:99–109` | 不同内容合法淡变；战斗可抢占低优先；本机原曲按秒、已知诊断按 DSP 小节；未来 envelope 不被强度更新取消 | `GameAudioRoot` hold/priority、`BgmTransport` 调度/有限来源/淡变 |
| `index.ts:12,33–42`; `policy.ts:21–22` | BGM 用户量 .24、master .75；用户量、mute、临时 token 独立，所有 snapshot 因子相乘 | `BgmMixController`、`BgmMixProfileSO`、Root PlayerPrefs；Transport 唯一写 AudioSource.volume |
| `exploration-app.ts:100–105` | 交战只消费既有权威；普通对话只 duck .45；背包只影响 UI 提示，不改选曲 | `Integration/BgmCombatContextAdapter` 只读 `CombatEngagementSession.Engaged`；Host 对话 lease；无背包选曲逻辑 |
| `exploration-app.ts:96–102` | 世界/标题/区域是宿主事实；原型 actor.x>20 是其局部空间实现 | `BgmContextHost`、`BgmZoneMarker`、`BgmSceneContextAdapter`；不复制 x>20 全局规则 |
| `index.ts:43,89,94` | 世界失效后旧曲可以继续，晚到素材不得抢播；新有效 context 才可重新解析 | Host Bind/Publish/Unbind 与 `BgmTransport.InvalidateTarget` |
| `index.ts:116–117` | stop/disable/dispose 不因 tick、普通 context 或旧加载自行复活 | Root Stop/OnDisable/OnDestroy、Transport Stop/Dispose；显式 Resume 见差异表 |
| `index.ts:13,106,109,111` | 原创三层、100BPM、4拍/小节、4小节、9.6s；同 epoch，强度只改层 gain | 合法 Diagnostics WAV + TrackSO synchronizedLayers；Transport 共享 PlayScheduled 起点 |
| `index.ts:8–10,92,112–114`; `monitor.ts:6–9,19–24` | 真实所有者的 rule/key/epoch/elapsed/start/transport、待播/当前/淡出、token、有限历史；监控不能另建播放器 | `Editor/BgmMonitorWindow`，开发覆盖具名；生产事实不被监控改写 |

## 3. 已明确的平台适配差异

| 项目 | 原型 | 本轮 Unity 适配与边界 |
|---|---|---|
| 设备后端 | AudioContext/一次性 BufferSource/浏览器解锁 | AudioSource、AudioSettings.dspTime、PlayScheduled；无浏览器手势解锁，不改 Unity 全局输入/音频设置 |
| 内容 key 编码 | JSON tuple `[content,version,section,start,end]` | 长度前缀 key；增加稳定 assetId 与 loop 布尔，区分真实素材和一次/循环；SO end=0 表示整曲，由 Transport 验证实际范围。不是逐字节复刻，但仍不含 rule/瞬时进度 |
| 淡变形状 | 旧曲 timeconstant .1s、停止新起点+.28s；新曲 timeconstant .12s | 当前样板采用 **.28s 线性 crossfade**，SO 可配置；是显式后端适配，未宣称听感相同或更优 |
| 混音平滑 | 各 bus .035s、master .025s 分开 Web 节点 | BGM 最终乘积统一以 .035s 指数平滑，Mixer 单位增益；保留用户默认 .24/.75、对话 .45 和焦点 .25；不迁移八总线/SFX |
| 未知 BPM 原曲 | 本机候选整段评估重播、秒级淡变 | 候选 knownBpm=0；不伪造拍点、原作 loop point 或 stems |
| 已知 BPM 诊断 | 原创3层100BPM，共同 DSP/音频时钟起点 | 原创诊断片段用于同 epoch/下一小节/强度证明；不是原作音乐分轨 |
| 明确停止后恢复 | 普通 context 不清停止；只有非空诊断覆盖显式清停止 | 新增具名 `ResumeMusic()`，仍需有效目标；Resume 是 Unity 生命周期入口，不是加载回调权限 |
| 对话 token | 固定 token Map，重复同名替换；按 open true→false 释放 | 每次 `AcquireDialogueFocus()` 返回独立 IDisposable lease；调用方 Dispose 精确释放，Host 禁用作场景兜底；不覆盖其他快照或用户音量 |
| 缺素材回退 | 诊断内容按 rule 生成，可能 explore/far 不同 | Unity 可明确共用 Diagnostic_home，确保同 fallback 内容同 key；仍显示 ASSET_BLOCKED，不冒认原作完成 |
| 正式提供方 | Web 已有标题/对话/空间上下文 | 当前 Unity 缺正式 title/dialogue 生命周期提供方；只保留适配口和独立诊断验证。现有 CombatEngagementSession 是真实战斗事实源；不宣称实际 NPC 对话已接线 |

### 对话结束／目标失效的原始语义

`dialogue-panel.ts:6,10,13,16–18,21`：正常结束、Escape、显式关闭、异常、目标校验失败、blur/hidden 均让 open 变 false。`DialogueGame.valid()` 还核验同一个 GameState、说话者有效、role session 身份有效及玩家/目标当前安全条件（探索平静、可交互、存活/HP/架势、动作就绪等）。`exploration-app.ts:372,392,104` 先验证对话，再在本帧 sync 捕获 open→false、释放 `dialogue-focus`。

因此 **玩法/对话所有者负责生命周期，BGM 不自行推断目标死亡或距离**。未来 Unity 真对话提供方须在 end/cancel/invalid/dispose 全部路径 Dispose lease；Host.OnDisable 只覆盖场景退出，不能替代同场目标失效通知。当前只能证明端口的 acquire/dispose/disable，不可描述成正式对话流程已验。

## 4. 素材分层与精确本机依赖

**可发布层：** 原创 C#、SO/meta、BGM Mixer、原创明确标识的 ENGINE_DIAGNOSTIC_ONLY 短测试 WAV、测试与文档。

**受限本机层：** 以下原作候选 WAV、原始 bank/FSB、解码工具及带原作音乐的录音；没有重新分发证据，禁止 GitHub/Gitee/公开构建。目录名/样本名只说明来源，不证明官方场景绑定、BPM、段落、loop 或作者 stems。三 WAV 均为 PCM16/48kHz/双声道，已重新读取 RIFF 并计算 SHA256。

候选目录绝对路径：`E:/WORLDCREATOR/XingHaiHuiLang/Origin/work/SCENE-2D-01/checkout/work/AUDIO-01/music/`。

| 文件（上述目录下） | 字节 | 秒 | SHA256 |
|---|---:|---:|---|
| title.wav | 28,555,820 | 148.728 | `DD17354C33C0808C91A4FCB32800503DC3D2887680BE0999822BC6B9237D5D58` |
| home.wav | 19,008,044 | 99 | `1468CB17CCEE32C0250806CB7DC72A6D1BEE3A2039FC9F13D73FC905DC97038D` |
| chapter1.wav | 61,092,908 | 318.192 | `C154C8B55ED6A8B8B7DAF47084DC5CB59E42E164045CAB70BB6529228BFC1A19` |

原银行绝对目录：`D:/steam/steamapps/common/Cinderia/Cinderia_Data/StreamingAssets/`。

| 文件（上述目录下） | 字节 | SHA256 |
|---|---:|---|
| 音乐.bank | 95,148,928 | `F79F7CFCE11773D414E4EA650A840EBD1C4831AABA72FA82E0615E0370D64BA6` |
| 音乐.strings.bank | 100,002 | `3318E523270CCD21A38D36F9C460AEEDBBCBD03ACED41DCC78C15F175D14D30C` |
| 音效.bank | 54,793,248 | `E7249E09808DBF09874933AE4090BB92401ECA6C22B27834CF8D9A6A44E77C08` |

历史已解出的子块：`E:/WORLDCREATOR/XingHaiHuiLang/Origin/work/SCENE-2D-01/checkout/work/AUDIO-01/probe/music.fsb`，95,068,768B，历史指纹 `9A4BE52663836228B3E5649E83FE310D9D5C8D44F4A94BDED372B4738E40202F`（本次未重新提取）。源宿主白名单见 `game/action-lab-assets.ts:10–16,25–31`：仅回环 Vite serve 可读三首固定文件，不进入静态生产包。完整本机清单在外部调查目录 `local-media.json`、`local-banks.json`。

## 5. 不放大历史验收结论

`docs/tasks/AUDIO-01/critic-round3.md:5–14`：整个初版音频系统7.4/10，未达到最终验收。`fix01-critic.md:7–9,39–50`：FIX01 连续性定向技术9.3/10，不替代整体结论；原型22/22浏览器、4/4策略是历史记录，30s检查是模拟时钟，短 loop 是夹具。主观听感未签。

本次调查实际执行了原型纯策略六个情境和 `.33 × .45 × .7 = .10395` 的快照乘法；其余 Unity 音频输出、场景/生命周期证明须以本轮 verification 文件登记，不从源码阅读推断通过。用户试听、正式配乐版权及完整 SFX/语音迁移均在本轮之外。
