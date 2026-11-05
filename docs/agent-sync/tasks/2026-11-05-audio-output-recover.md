# AUDIO-OUTPUT-RECOVER
状态 active，Root唯一Unity写者；只读子任务audio_silence_audit。
当前RoomLab Play，用户无声。工程DisableAudio=false，EditorMute=false，Listener.pause=false/volume1、唯一OwnedListener有效。所有播放中Source时间0；两次跨真实时间DSP固定15666.7946667，构成音频输出停滞反馈信号。
假设依次：设备/输出从睡眠未恢复（预测同配置Reset后DSP推进）；系统音量/路由（预测DSP推进但输出静音）；Mixer用户静音（预测参数-80dB但DSP应正常）；源Clip/加载错误（不应让全部DSP停滞）。先仅同配置Reset，不改场景/源档/持久音量。

完成：同配置AudioSettings.Reset=true，DSP从15666.794恢复推进；Reset使旧AudioSource停止，随后调用既有root.StopMusic/ResumeMusic重新按当前场景请求BGM。复查DSP15931.392、BGM time47.4667/volume.42、Listener peak.0015955/RMS.0007002（非零）。前后Mixer各Gain一致、用户Muted=false。SFX旧短音由现有Tick回收，后续正常意图重新播放。
未改生产代码、共享资产、工程设置、PlayerPrefs、存档或Play现场；不重播恢复前积压的短音。休眠唤醒/设备失效为符合现象的推断，确认事实是DSP停滞；本轮仅恢复当前输出，未新增自动设备恢复功能。
状态complete；全部Unity/只读租约释放。无本轮新电源动作。
