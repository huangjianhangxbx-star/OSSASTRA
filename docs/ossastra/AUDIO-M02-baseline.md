# AUDIO-M02 基线

本地与 GitHub main ddc3378；索引空；36 个既有 dirty 保护。任务前文件与缺失状态的 SHA256 在外部工作目录 AUDIO-M02-20261025/protected-before.json。旧场景/角色Prefab/模型/玩法/输入/Packages/ProjectSettings 不改。

2026-10-25 MCP 读取：Application.dataPath=E:/WORLDCREATOR/new/Project_ANKALUTE/Assets；SC_BGM_AudioMigration，Edit，clean，compiling/updating=false，audioMasterMute=false。此前记录 true 属旧任务状态；本任务不切换用户全局静音。

当前自有声音播放权：BgmTransport 两 bank 各3源，DSP PlayScheduled；CombatFeedbackView.Start 自建 AudioSource，2D/volume .3，PlayOneShot，局部每帧4声音。其 release/hit/hurt 音源与全部视觉事件保留对账；旧 BGM Mixer 仅 Master。BGM 完整增益 baseline×.24×.75×focus×tokens；普通对话 token=.45。本轮移至唯一共享 Mixer 后同一因子只乘一次。

RED：Candidates/AUDIO-M02/Results/01-core-red.json，五种新核心类型不存在，passed=0 failed=1。任务基线为真实断言失败，不是编译错误。
