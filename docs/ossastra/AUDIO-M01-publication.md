# AUDIO-M01｜发布范围与核验

目标仅 `huangjianhangxbx-star/OSSASTRA` 的 GitHub main；起点 `02b11746637a78605f0ce4018a5b4a8e767f2c57`，发布前连接器再次核对一致。不推 Gitee。

本轮技术验证完成，正在执行定向提交/发布；确切产品SHA与远端核验在随后收口记录追加，当前不先声称已推送。

允许范围：新增 Scripts/Audio及meta、Audio/BGM自有SO/Mixer/12份原创诊断WAV与meta、SC_BGM_AudioMigration及meta、Candidates/AUDIO-M01测试/工具/数值结果、四份交接文档与实施计划、规则索引和本任务同步记录。实际逐文件清单为 `Candidates/AUDIO-M01/publish-files.txt`。

禁止范围：`Assets/OSSASTRA/LocalMedia/**`及meta、原作title/home/chapter1 WAV、BANK/FSB/解码依赖、含原作音频录音、PlayerProbe输出、原dirty36项与角色/Spine/原场/旧Audio/Feedback/第三方/Packages/ProjectSettings。没有媒体再分发授权；本轮不上传这些内容。

Candidate SO公开的是自有身份/元数据与本机clip GUID引用，**不包含原作音频**。干净克隆这些引用会缺失，Transport显示ASSET_BLOCKED并播放自有Diagnostic回退；需明确合法本机供给才可试听原曲候选。不能把克隆仅有诊断内容描述成正式原声发布完成。

基础验证：373个受保护路径SHA/缺失状态一致，Feedback另查HEAD精确diff，原36dirty保持；媒体SHA/来源及限制见source-map。测试结果只含时钟、采样位置、RMS与增益数值，无PCM。用户听感仍待签，Player因旧第三方Editor引用错误阻塞。
