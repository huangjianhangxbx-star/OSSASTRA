# RENDER-ARCH-01 临时产物清理

- 状态：partial / read-only；临时Assets已清理，Player目录删除被策略拒绝，全部写租约释放。
- 用户明确要求继续删除上一轮临时产物，确认已体验完、可以停Play并清理。
- 独占：Assets/OSSASTRA/RenderArchitectureSmoke、Assets/OSSASTRA/Scripts/Development/RenderArchitectureSmoke、artifacts/RENDER-ARCH-01/SmokePlayer_20261106_061100及本记录。
- 保留：主场、现行参数/作者Profile、存档、Candidates模板、研究目录报告截图日志，以及所有其他dirty。
- 先保存现场参数；Native读取真实GUID并核引用后，通过Unity API删除临时资产；构建目录验证绝对路径/无reparse后精确删除；最后导入与当前主场检查。无需复杂玩法测试。

## 执行结果
- 用户再次明确允许停止Play后，Unity现场已处于Edit；保存当前场/环境/相机/角色参数快照到研究/RENDER-ARCH-01/cleanup-current-parameters.json，未恢复旧值或保存主场dirty。
- Native核6个真实GUID，主场与BuildSettings依赖树无临时资产；只读子代理核非临时OSSASTRA引用零命中。Build目录397文件/434,964,217字节，无重解析点，原报告/两PNG完整。
- 通过工具支持的显式可信调用执行AssetDatabase.DeleteAsset：两个临时Assets目录与meta已删除（5资源+1Probe），主场dirty前后均true，不保存/重开主场。
- Player构建目录的literal绝对路径Remove-Item删除再次被自动审批拒绝：blocked by policy。未换API/命令绕过；剩余约414.81MiB构建产物完整保留。
- 主场磁盘、两角色Profile与BuildSettings四SHA保持；Candidates测试模板、研究报告/截图/日志保持。
- 清理后Editor Edit/ready、无编译错误，当前主场45°、MissingScript=0；原dirty=true保持。仅本记录与验证报告定向发布，不包含任何其他dirty或媒体。
