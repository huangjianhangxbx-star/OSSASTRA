# AUDIO-M02 交接

本轮 G0–G8 的技术实施、Editor 验证与定向发布收口。只有原 `Ossastra.Audio.GameAudioRoot` 拥有正式 BGM 与本轮短音池；两类仲裁分开，共用唯一 Master/Mixer。没有改角色根、输入、动作/技能数据、HP、Spine、交战判定或旧场磁盘；原36项dirty保持，不同步Gitee。

试玩场：`Assets/OSSASTRA/Scenes/SC_AUDIO_M02_SoundIntegration.unity`。已建立独立音频事实宿主，关HUD不关声音。Play 后监控入口 `XingHai > OSSASTRA > Audio > Unified Audio Monitor`；原 BGM Monitor 菜单也保留。配置入口和新事件步骤见 `AUDIO-M02-authoring.md`。此新场使用原创诊断音色；旧 BGM 场保持原本机配乐规则。旧无根VC02B独立启动也由同一 Audio bootstrap 接管，未保存或篡改旧场。

核心47、真实混音/池32、事实9、根生命周期3、观察者7、混音后输出8均通过；原BGM实际故事12、FIX01七路径15断言、Single切场6与无根独立启动通过。自然故事真实观察四类总线和46起播/46归还；场景总源数38固定。有效投掷目标API补测消费2→1及敌HP600→555，两种短音分别由释放/命中事实触发。详细T01–T32及两条未观察窗口/诊断hold差异保留在verification，不覆写失败历史或冒称全轮每一行都绿。

媒体说明：原三段本机短音实际来自参考游戏提取，旧GUID和字段保留但不再由Feedback播放；公开新目录使用八个可重建数学原创短音，22事件定义复用它们。该替换是明确工程样本，不称原音色已迁入或正式混音完工。受限BGM及参考素材不发布。source-manifest记录本任务公开资产/源的实际指纹，publish-files列出本任务精确提交范围。

测试基于本机实际保存状态，包括受到保护的输入SO、角色表现脚本和场景本地变更；本任务不把这些用户改动夹带发布。干净克隆仍受既有Spine/模型/商业媒体等依赖边界约束，参见仓库DEPENDENCIES。不得以本机Editor成功代签克隆或Player发行。

Player仅做一次有界构建：3.53秒，仍被旧 `Assets/Unity-Favorites-Tool/Editor/QuickAccessWindow.cs` 的EditorWindow/MenuItem等14错误阻塞，未改插件；状态 `PLAYER_BLOCKED_EXISTING_PLUGIN`。人耳音色、主观混音、实体手柄长时体验仍 `PENDING-HUMAN-LISTENING`。没有完整Voice/环境服务，不自动开启AUDIO-M03。

发布基准 ddc3378f4a319d553146214f1ec076eed08d0571，提交仅使用自有清单，远端以expected SHA无强推更新。产品与回执精确SHA在发布后追加；自引用提交SHA不写入其自身内容。

产品 **e79be1f1d5b4d03b8843b2d9a184971897bcfe7a**（164文件）已发布GitHub main并精确回读SHA；所有blob/tree/commit均与本地一致，expected-sha无强推。随后仅5个回执/同步文件更新，不改产品或资源指纹。原36dirty逐行身份保持、645非目标指纹不变，全部租约释放。最终Editor为自有新场Edit ready/clean，0 Missing Script/0临时设备；未关机。
