# AUDIO-BB-FUSION：融合迁移决策

日期：2026-10-27。依据：用户执行本轮附件计划的明确授权、OSS 当前代码、Lab 已封存证据。此文记录技术选择；最终测试结论由 root 记录，不以意图冒充完成。

## D01：保留生产所有者与事实路径

策略：**KEEP_OSS**。

保留 `GameAudioRoot`、Bootstrap、场景 Host、`SfxSourceHandle`、`SfxArbitrator`、固定池及 `GameAudioMixService`。Lab 不生成第二个 DDOL 根，不把按钮模拟敌人状态、暂停或命中延迟模型带进玩法。

理由：OSS 已有精确事实、世界/来源代次、同源上限与真实回执；Lab 的优势集中于内容和实验编排。替换根会丢失更强的现有保障。

验证要求：唯一根/Listener、同源隔离、多段回执不吞声、失效来源拒绝、没有越过许可的播放入口。

## D02：内容档案整体替换，显式回退

策略：**ADAPT_HYBRID**。

候选 `AudioContentProfileSO` 一起声明目录、战斗语义和音乐规则，以及世界声音/空间声学开关和确定性 seed。Root 完整校验后原子应用；无效档案保留当前有效配置。切换撤销旧 SFX 来源和世界，不重建 BGM 传输。

本机有效 Bloodborne 档案可作为本地参考默认；未安装或缺失时应显示 `REFERENCE_CONTENT_UNAVAILABLE` / `MISSING_SEMANTIC`，而不是把旧媒体或诊断音标成 Bloodborne。A0 通过明确的回退入口保留。

验证要求：失败不产生半档案；正常切换清旧代次；同内容 BGM epoch 连续；公共干净克隆没有隐式受限媒体依赖。

## D03：以录音身份和真实语义提高内容质量

策略：**ADAPT_HYBRID**。

保留现有动作/阶段/通道/投递/身份匹配；引入明确的目标接触材质及地表事实。多个真实录音才能算多个录音变体；同一音频改音高不能冒充四个源。

战斗瞬态、主体、硬接触是否分层由具体语义配置决定，不能把命中、敌人受击和接触音无差别叠三次。guard/miss/reload/throw/skill 只依赖已发生的正式出口；未知出口或未知素材在覆盖表中明确缺口。

验证要求：源文件/派生 SHA 和同用途录音身份；事实与事件一一可追溯；材质未知保持未知；`P0_CONTENT_GAP` 不通过旧声偷补成“100%覆盖”。

## D04：真实位移脚步，环境声与许可同步

策略：**ADAPT_HYBRID**。

Lab 的环境规划值得采用，脚步计时器和 Loop 乐观状态不能照搬。

- 脚步结合角色碰撞后运动事实和实际 XZ 距离，排除死亡、不可模拟、闪避、强制运动、传送；每帧最多一个候选。
- 地表由真实向下射线命中的 `AudioSurfaceFact.surface` 读取；无声明使用 generic。
- 持续环境声使用真实区域和当前实控位置，每区域来源租约；进入/离开淡变，禁用/换世界释放。
- 播放被预算拒绝或被抢占后，以实际许可存活判定，延迟重试，不能停留在“已经启动”的假状态。

候选文件：`E:/WORLDCREATOR/new/Project_ANKALUTE/Assets/OSSASTRA/Scripts/Audio/Integration/LocomotionAudioAdapter.cs`、`AudioEnvironmentZone.cs`、`AudioSurfaceFact.cs`。

行为检查脚本：`E:/WORLDCREATOR/new/Project_ANKALUTE/Candidates/AUDIO-BB-FUSION/tests/WorldAudioFactsContracts.cs.txt`。缺类型 RED 保留；现存 `results/WorldAudioFactsContracts-green.json` 的17项检查全部为true。它证明采样边界和真实区域几何；不单独证明完整试玩或最终听感。

## D05：音乐内容融合，传输 KEEP_OSS

策略：**KEEP_OSS**，仅内容作者配置 **ADAPT_HYBRID**。

安全/探索/危险/战斗等采用真实场景事实和本地参考曲目；没有正式入口的 title/story 不虚报已迁移。仍使用原仲裁、内容身份、DSP 调度和对话 duck。

Lab `playback2D` 适用于试听音乐候选，不能替代生产 BGM 传输。Boss 曲、研究曲的本轮场景安排是实验决策，不声称还原原作地图触发机制。

验证要求：同曲情境变化连续、离战真实结束、死亡敌人不维持遇敌 BGM、旧预约撤销、无第二个音乐写入者。

## D06：空间声学在唯一播放池内执行

策略：**ADAPT_HYBRID**。

参考 Lab 持续调谐已有声部、平滑低通和房间变化的方法，在 OSS 固定池中实现。真实世界坐标使用 XZ；房间/遮挡/区域只能读实际声明或检测。用户 Master/BGM/SFX 因子仍各自只应用一次。

验证要求：左右/远近、开放/遮挡可对照；不重启循环；复用槽清滤波/loop；来源停止不泄漏额外 AudioSource。

## D07：四条件 A-B，技术和听感独立签收

策略：**ADAPT_HYBRID**。

| 条件 | 变化 | 保持 |
|---|---|---|
| A0 | 原内容、原规则、原混音 | 作为可恢复基线 |
| A1 | 仅换 Bloodborne 内容 | 原作者规则、原混音 |
| A2 | Bloodborne 内容 + 审查后的变体/分层/语义作者策略 | 原混音 |
| A3 | A2 + 本轮空间/混音方案 | 同一事实脚本与采集条件 |

固定事实脚本、seed、武器、动作、敌人、位置、时序、用户音量和采集设备；记录峰值、RMS/响度指标、归一化方法，避免把更响当更好。保存原始结果，不能只留下归一化音频。

源状态 Trace 不等于 Unity DSP；离线导出必须标 `OFFLINE_PREVIEW`。人工盲听和最终偏好为 **PENDING_HUMAN**，机器不代签。

## D08：保护与发布

策略：**KEEP_OSS** 的现有工作树保护与定向 GitHub 流程。

不修改原始 Bloodborne、AudioLab、原型只读副本、用户维护区或无关玩法；不替换旧验证场或清掉既有用户 dirty。受限 PCM、派生及直接引用这些资源的本地 SO/场景/预制体留在明确忽略的本机目录，公共报告只保存来源标识、哈希和配方。

最终发布前 root 核对索引、私有依赖闭包、保护清单和公开构建边界，再按用户授权仅同步 GitHub。具体清单见同目录 `LICENSE_BOUNDARY.md`；本报告不声称这些发布关口已经通过。

## D09：候选代码审计发现与定向修正

本节来自本轮只读复核，记录实际调用路径、定向修正及已落盘的运行证据。审计代理读取了下列实际结果JSON，没有自行调用Unity；**这些边界检查不等于整轮最终验收，也不代替人工试听**。行号是发现问题时的位置，修复后会移动。

源码根：`E:/WORLDCREATOR/new/Project_ANKALUTE/Assets/OSSASTRA/Scripts/Audio/`。

| 问题 | 实际触发路径 | 修正与已保存的定向证据 |
|---|---|---|
| P2：环境淡变覆盖遮挡衰减 | `Runtime/SfxVoicePlayer.cs` 的 `SetVoice` 每帧写作者音量×淡变，覆盖 `ApplyAcoustics` 每0.1秒写的遮挡因子；Zone 在 Root 之后 LateUpdate，因此持续层几乎失去遮挡音量衰减 | `VoiceState.acousticGain` 统一组合作者音量×淡变×声学因子。`AcousticFadeContracts-final.json` 2/2：遮挡0.68、再淡至0.5后实际源音量0.34；RED仍保留 |
| P2：第四段明确材质被通用重击覆盖 | `Definitions/CombatSoundProfileSO.cs` 原 stage 权重高于 material；作者新增材质行 stage=0，因此第四段金属/木石会命中通用 heavy | 同动作明确材质优先通用stage。`MaterialContracts-final.json` 2/2：第四段已知metal选材质，未知材质仍保留heavy；不是移除第四段规则 |
| P2：主控死亡后声音变成伙伴身份 | `CharacterRoot` 更早接收 Vitals.Died 并触发 Coordinator 交接；声音后接收死亡事实时重新读 Current，已变成另一人 | 死亡前任Current例外及入队冻结prefix。`death-boundary-green.json` 真实致死后记录 `player.body.death`、`SourcePlayIssued=true`；未更改玩法交接顺序 |
| P2：档案切换后的首个动作丢失 | Root 撤销来源后，适配器原先直到 LateUpdate 才重连；下一帧角色 Update 的 Accepted 被旧租约丢弃，后续释放/接触缺动作身份 | `ContentProfileApplied` 同步通知并立即Connect；`first-action-green.json` 为真实 `accepted=true`、`releases=1`、`voices=1`，对照RED保留 |
| P2：坏音乐规则通过原子校验 | 原 `AudioContentProfileSO.Validate` 仅检查 track；空 ruleId、非法 condition、NaN intensity 能 APPLIED，但仲裁器跳过规则 | ruleId、枚举、有限且范围内intensity校验。`invalid-music-green.json` 3/3：空ID、NaN、非法条件均在世界变更前拒绝 |
| P2：非活动 Additive Host 夺音乐上下文 | 新加载 Additive Host 的 OnEnable 曾可直接 Bind，覆盖仍在活动场的音乐世界 | `BindHost` 只接受ActiveScene，活动场变化显式Attach；`additive-host-green.json` 5/5，包含旧对话scope释放和返回/卸载后主Host保留 |
| P2：致死接触同时播放受伤和死亡呼声 | 同笔正式致死回执会先触发 Died，又经接触适配器发 hurt；原轮询也会把HP降至0当hurt | 接触和HP轮询hurt仅存活者；`death-boundary-green.json` 3/3：真实死亡一次、死亡前主控身份、未叠hurt cry；事件列表只有一个已发播放的死亡事件 |

### Host 时序修正后的实机证据

1. **切ActiveScene释放旧Host scope。** 原先先清host引用，绕过了旧Host对话令牌释放。现 `UnbindHost` 在清引用前 `ReleaseWorldScope`，活动场重绑不再遗留旧duck。`results/additive-host-green.json` 实机5/5：非活动Host不夺权、显式活动场切换绑定新Host、清旧对话scope、卸载次场后保留主Host、恢复原活动场。
2. **晚出现/恢复的Root双向补绑。** 原先Host无根时OnEnable返回，sceneLoaded后创建Root不会再收到已过去的活动场事件。现Root完成Awake及重新启用时主动定位活动Host，经 `BgmContextHost.AttachAudioRoot` 同时更新Host根引用、Bind和Publish；没有每帧自愈轮询。`results/late-host-green.json` 实机4/4：Host先于Root仍绑定、Host保存实际根、后续真实上下文可转送、Root禁用/恢复后显式Resume可继续。该测试明确要求退出Play重开自有场景清理测试现场。

Root 的 `Awake` 只有实际所有者订阅活动场事件，重复根在订阅前拒绝；`OnDestroy` 的所有者分支解除订阅。对最新 `Awake/BindHost/OnActiveAudioScene/OnEnable/OnDestroy` 和Host Attach调用顺序的静态复查，未发现新的实质Host缺陷。

### 此节没有替代的验收

- `profile-lifecycle.json` 的14项为true，证明该脚本覆盖的旧租约拒绝、Loop持有/单独停声、固定声源数、A1作者参数保留和A2/A3媒体/编排/seed一致；不意味着所有未来内容配置自动正确。
- `AB_SUMMARY.json` 已保存4条件经真实Unity Mixer输出的录制游戏事实重放，约56.15秒/条件、全条件clipped=0；这是实际引擎取证，不是Lab离线WAV，也不是四遍真人操作。归一化仅采集后标量处理，原始录音保留。
- 最终场景试玩、全局回归汇总、发布索引/私有依赖闭包、构建限制和源库/用户dirty保护以root最后交付记录为准。**人工听感、偏好及整体表现签收仍为 `PENDING_HUMAN`。**
