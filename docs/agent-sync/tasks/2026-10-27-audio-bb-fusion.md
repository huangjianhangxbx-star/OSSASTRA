# AUDIO-BB-FUSION

- 状态：active；写入模式：unity-writer；唯一 Unity writer：root。
- 用户授权：2026-10-27 执行 Bloodborne 融合总计划 F00–F10，完成后休眠；R11 定向 GitHub、不 Gitee。
- 基线：main/origin efdd6138c8f8dd41f421075e480b34de5d8f6008；36 项原 dirty 保护，外部原始清单及哈希见 work/OSSASTRA/AUDIO-BB-FUSION-20261027。
- 独占：Assets/OSSASTRA/Scripts/Audio、Audio 公共配置及新增 BloodborneLocal 私有内容、新 SC_AUDIO_BB_Fusion 场、Candidates/AUDIO-BB-FUSION、docs/ossastra/AUDIO-BB-FUSION*、本任务记录、同步摘要、.gitignore。共享旧场、玩法、输入、Spine、36 dirty、源库/AudioLab/原游戏只读。
- 子代理仅只读源库与架构，不操作 Unity/Git；root 独占原生资产写入。
- 已读：AGENTS/项目记忆/同步协议及此前完整读取且基线未变的 PROJECT_STATE/TASK_REGISTRY；OSSASTRA_RULES、DEPENDENCIES、README、Origin 开发细则、计划包七文档；真实 brainstorming/writing-plans/codebase-design/TDD/Funplay workflow。
- 实施：采用用户已批准的附件设计；不重复技能审批仪式，不把文档中的原始“只出计划”描述覆盖本次执行委托。
- F00：包 SHA 校验成功；原 dirty 快照完成；当前无 Unity 进程，E 盘约 2.9GiB 空间，限制定向拷贝；环境就绪核验继续。
- F01–F10：未完成；进展与实际 Gate 以 Candidates/AUDIO-BB-FUSION/PROGRESS.json 为准。
- 真实听感必须 PENDING_HUMAN；技术验证不代签；受限 PCM/派生/私有 SO 不公开。
- 租约未释放。

委派细分：lab_architecture仅写Integration/LocomotionAudioAdapter.cs、AudioEnvironmentZone.cs、AudioSurfaceFact.cs与WorldAudioFactsContracts测试；facts_coverage仅写InterfaceAudioAdapter.cs与InterfaceFactsContracts测试。root独占其余Audio现存源码、Unity/Git/原生资产；两子代理不操作Unity/Git，bb_content_audit只写外部调查输出。所有代码等root实际RED后实施。

F05必要事实出口：登记CombatContactBatch/Gateway新增可选只读EffectSemantic及保留旧5参的6参重载，CombatIceWorld仅两调用补ice.pulse/ice.shatter元数据，不改伤害/时间/算法。已跑EffectSemanticContracts RED，旧Cause不能被当冰属性猜测。AcousticFadeContract实际复现区域淡变覆盖遮挡gain，修正独立因子组合。

F00–F10工程收口：technical-complete；唯一Unity/源码writer释放，转docs-writer仅本任务文档和定向发布。技术证据以VERIFICATION_INDEX/FINAL_AUDITION/BUILD_VALIDATION及results为准。新增入口SC_AUDIO_BB_Fusion已Edit clean，无私有依赖。完整保护36/36；源67/FSB5/派生67哈希一致。首攻、材质、淡变/遮挡、Host单双向绑定/duck、死亡人声和无地面脚步均RED→GREEN。
实机主20/21、枪7/7、世界8/9，缺步行/短敌击窗不抹除；P0素材真冰等CONTENT_INCOMPLETE，人工NEEDS_LISTENING。Player14旧插件错，未修改第三方。四档真实Mixer事实回放完成，录音留外部本机。接下来仅审定文本/native公共树并发布GitHub，完成后现有S3睡眠；S4未启用且C空间不足以容纳全休眠文件，不改系统配置。
