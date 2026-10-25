# AUDIO-M01｜20项定向验证

2026-10-25。证据为 `Candidates/AUDIO-M01/Results/*.json`；测试源码为同目录上级 `Tests/*.cs.txt`，通过本机已配置的 Funplay MCP execute_code 执行。原型来源固定4d8e61d，目标起点02b1174。测试在保留原36dirty的本机Editor中运行。虚拟DSP合同和真实DSP输出明确分开，未沿用历史原型评分当成本轮通过。

| ID | 结果与证据 |
|---|---|
| 01 | PASS：RootOwnership合同、scene-cycle，唯一持久根；六条固定生产音源，重复根在分配前拒绝。 |
| 02 | PASS：CoreContracts 17/17，final-editor-state读回实际五条SO优先级100/60/40/1/0。 |
| 03 | PASS：CoreContracts身份/文化格式/冲突分隔/非法边界；Transport实际裁切循环样本，非只改字符串。 |
| 04 | PASS：live-story真实移入/移出Zone，home key/epoch/transport/starts不变；合同100次轮询亦不重播。 |
| 05 | PASS端口：真实播放duck .18→.081并持续原home，释放恢复；Scope 5/5验证精确释放、换Host、无旧Host再取得。无正式NPC对话提供方，未声称接线完成。 |
| 06 | PASS技术：真实title/home/chapter1输出；real-crossfade 6.016秒、2075次观察，A→预约B→改C→实际淡入后返回A→B，固定6源、4组/12次PlayScheduled；无观察到无可听源的采样。听感待用户。 |
| 07 | PASS：live-story只用虚拟Gamepad的RT经既有输入→主控ActionObserved.Accepted→既有Engaged→combat，未直接写Engaged或调用私有OnFact。 |
| 08 | PASS：quietTime 3.006818秒后explore，第二次RT取得第二个主控Accepted后combat。夹具暂将敌人移远，避免伙伴/敌人持续交战污染calm，结束恢复；未改资产/战斗代码。 |
| 09 | PASS API测量：原创100BPM三层真实6.016秒；1994个播放时钟观察，最大采样离差342、一个实际DSPbuffer容限343（片段16k/输出48k、buffer1024）。强度期望1074.160，观测1074.176，提前变更0次。非逐采样精确锁相/硬件延迟认证。 |
| 10 | PASS：三首Candidate knownBpm=0，使用+.08秒起播和.28秒fade；不凭空按原曲小节量化。 |
| 11 | PASS：Mix 12/12，默认乘法、mute、focus、快照叠加、精确撤销、0..2定义范围。保存.27/.63/muted后退出并重入Play恢复；测试后恢复原.24/.75/unmuted。 |
| 12 | PASS：100次同key合同；实际far/UI/duck/timeScale保持同home的starts、epoch和运行身份。 |
| 13 | PASS定向：Transport 16/16含迟到资源、错误fallback后晚到原曲、目标代次；Scope撤旧诊断覆盖；实时失效DSP越过epoch保留已开始源。状态注入验证与真正异步网络故障不是一回事。 |
| 14 | PASS：live-story真实帧Stop锁存、禁启不复活、显式Resume；Transport Dispose释放自有对象。 |
| 15 | PASS：BGM场→原比较场→BGM场，唯一根、六源、Backend/Transport/epoch保持；世界Host失效旧声继续，返场新Host恢复；再退出并重入Play配置恢复。原场文件未保存/改动。 |
| 16 | PASS：真实InventoryView SetOpen开关不改音乐；timeScale=0继续DSP；focus .25通过Mix合同，恢复焦点通过原应用回调。未模拟硬件断开/OS睡眠恢复。 |
| 17 | PASS：MissingMediaLive临时克隆SO取消clip，不改磁盘资产；真实ASSET_BLOCKED_FALLBACK且Pending原曲/Current诊断分开；far保留同诊断home身份。 |
| 18 | PASS静态+现场：监控实际Instance，显示context/rule/key/素材/version/section/loop/epoch/elapsed、Backend/Transport、组与源起播、Pending/Fading/真实Scheduled、gain/token/history；关闭只释放自己的覆盖和令牌，不创建播放器。 |
| 19 | PASS：isolation 373保护条目一致，原36dirty保留；Feedback+meta独立diff/SHA，无角色或工程设置变更。 |
| 20 | 发布核验见publication；合法原创诊断可发布，原作媒体与输出不发布，不Gitee。 |

## 输出与测量边界

真实主输出 title RMS .001247、home .001352、对话期间 .001079、Resume后 .001232。AudioListener采样可含现有场景声，不能独自证明某条音轨。独立诊断测试另外逐源得到三层最大RMS `.011400/.009301/.003715`，全部实际播放并读到非零输出；完成Dispose、临时对象和Editor update回调清理。

Crossfade以逐源volume/isPlaying/timeSamples/能量观察，最大连续无可听观察跨度0秒，最大update间隔.021333秒。能量之和不是post-mixer总声压，不代表相位抵消检查、声卡录音或人耳无缝结论。没有录制/发布原作PCM主输出。

最初live-story夹具进入far靠近敌人，伙伴自然Accepted让UI阶段转combat；其calm采样也早于3秒。保留外部原始失败记录，收窄独立夹具后最终12项均过。没有把正确战斗行为作为音频缺陷修改。

## 构建和最终现场

player-build实际 BuildPipeline 报告Failed/14errors，首要为 `Assets/Unity-Favorites-Tool/Editor/QuickAccessWindow.cs` 找不到EditorWindow/MenuItem等。第三方未改，Player未验；Editor编译/更新状态均false，SC_BGM_AudioMigration为Edit clean，零临时输出探针，监控已打开。人耳听感、版权定稿、正式Title/Dialog和完整音效均未代签。
