# C独立会话与保存合同

`VoxelSessionContract.cs.txt`通过Native `execute_code`执行 `Run()`；旧版本应返回 `missingFeature`，而不是编译错误。生产实现后检查新字段、有限校验、会话克隆、A/B切档保留、C0复位、诊断剥离和Shader参数上传。

`VoxelSessionSaveContract.cs.txt`仅在Edit运行：在已有Profiles目录创建带GUID的三个私有作者资产，最终以AssetDatabase精确删除。检验候选独立保存、C0/B/无关路径拒绝、回执可追溯，以及保存不自动改变默认。不会停Play、打开或保存主场。

场景默认持久化必须另用当前主场的显式按钮验收：Play中请求应准确报告“待退出Play”，而非已固化；退出后以GlobalObjectId、场景路径、完整绑定顺序验证同一个作者组件。dirty场景不自动保存。C0、A/B和材质作者引用保持。
