# AUDIO-M01｜发布范围与核验

目标仅 `huangjianhangxbx-star/OSSASTRA` 的 GitHub main；起点 `02b11746637a78605f0ce4018a5b4a8e767f2c57`，发布前连接器再次核对一致。不推 Gitee。

产品提交 **df90b9fe9ac7c831f791878408e495a6cc46e0bb** 已发布，GitHub连接器读取main精确核对一致。128文件、12份原创诊断WAV；本文件随后的收口提交只更新文档/任务状态，产品代码不变。

允许范围：新增 Scripts/Audio及meta、Audio/BGM自有SO/Mixer/12份原创诊断WAV与meta、SC_BGM_AudioMigration及meta、Candidates/AUDIO-M01测试/工具/数值结果、四份交接文档与实施计划、规则索引和本任务同步记录。实际逐文件清单为 `Candidates/AUDIO-M01/publish-files.txt`。

禁止范围：`Assets/OSSASTRA/LocalMedia/**`及meta、原作title/home/chapter1 WAV、BANK/FSB/解码依赖、含原作音频录音、PlayerProbe输出、原dirty36项与角色/Spine/原场/旧Audio/Feedback/第三方/Packages/ProjectSettings。没有媒体再分发授权；本轮不上传这些内容。

Candidate SO公开的是自有身份/元数据与本机clip GUID引用，**不包含原作音频**。干净克隆这些引用会缺失，Transport显示ASSET_BLOCKED并播放自有Diagnostic回退；需明确合法本机供给才可试听原曲候选。不能把克隆仅有诊断内容描述成正式原声发布完成。

基础验证：373个受保护路径SHA/缺失状态一致，Feedback另查HEAD精确diff，原36dirty保持；媒体SHA/来源及限制见source-map。测试结果只含时钟、采样位置、RMS与增益数值，无PCM。用户听感仍待签，Player因旧第三方Editor引用错误阻塞。

## 发布复核
普通git push因github.com:443连接超时未成功；改用既有GitHub Git Data API上传，128个blob、tree e617333ee85ad3e4dcccad4d114731733321f392 与本地精确匹配，再以expected_sha=02b1174、force=false更新main，连接器回读df90b9f。不是另建一套实现。

收口文档提交可由本文件Git历史直接定位；最终远端HEAD另由执行回执精确提供，避免在提交内自引用本提交SHA。所有代码/Unity/媒体租约已释放，原36dirty保持，索引清空，无Gitee操作。不自动推进后续音效任务。
