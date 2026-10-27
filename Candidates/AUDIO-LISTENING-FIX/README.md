# 音量、位置浑浊与持续循环修复

入口和完整解释见 `docs/ossastra/AUDIO-LISTENING-FIX-handoff.md`。游戏内Esc/Select → 声音设置；保存推荐1/.65/1/0，环境默认关闭。世界继续运行。当前用户已确认不适持续声消失。

| 证据 | 本轮结果 |
|---|---|
| ListeningUsabilityContracts-red/green.json | 4项从全RED到4/4：实际听点14.17→.8u、菜单入口、默认无环境层、边界不遮挡 |
| OcclusionContracts-red/green.json | 3/3：有效墙保留低通、遮挡不自动叠Room、禁用声学标记透明 |
| SettingsInteractionStory-complete.json | 14/14：真实uGUI指针/导航、唯一Mixer、Prefs、mute、推荐、环境实际开关、面板复用、输入捕获释放且不暂停 |
| PositionTimbreContracts-result.json | 2/2：z=3/-4.6均22000Hz、无混响、相同增益；此探针在最终留余量前记录，绝对gain=.64是中间版本，最终作者gain=.512 |
| ListenerLifecycleContracts-final.json | 最终4/4：禁用恢复原Listener、重启唯一、实控切换跟随、无角色保留最后听点。早期green.json的切人未过为失焦前提，原结果保留 |
| InitiallyDisabledRootContracts-green.json | 2/2：初始禁用不接管，显式启用才接管；RED保留 |
| scene-cycle.json | Single实际往返6/6、活动Listener=1、音频根=1 |
| regression-SfxRuntimeContracts/SoundProfileContracts/ProfileLifecycleContracts.json | 32/14/14通过，未遍历退休模块 |
| output-metrics.json | 中间×2增益70s记录：peak1.088917/2削波样本，是实际发现的余量问题，不当最终PASS |
| output-metrics-final.json + GunStory-final.json | 最终28.096s真实游戏Listener输出，48k双声道，peak.749935、RMS.055738、0削波、峰12声部，枪械7/7、设备0 |
| final-editor.json / protection.json | Edit clean，原相机监听恢复，临时听点/设备0；36/36原dirty不变 |

最初216个私有一次声定义试调×2后测出削波，`calibration-final.json`记录净×1.6校准。`CalibrateLocalMix.cs.txt`只通过Editor API修改本机A2/A3，按版本标记幂等，PCM/A0/A1不变。重新生成本机内容时，在原AuthorLocalProfiles/Refine之后执行本配方。所有.raw/WAV/带引用SO只留本机，不公开。

实机录音：`E:/WORLDCREATOR/XingHaiHuiLang/work/OSSASTRA/AUDIO-LISTENING-FIX-20261027/game-output.wav`；UI实拍：`Library/FunplayMcp/Screenshots/audio-listening-settings.png`。图片已实际查看，控件在屏内且文字可读。无主观全音色排名、无Player构建通过声明。用户的“已经消失”仅对应持续不适声。
