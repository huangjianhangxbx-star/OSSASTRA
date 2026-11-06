# 发布范围

本轮仅发布体素受光模块、其许可路由与环境相机接缝、开发比较入口、可重放验证候选和文档。主场/作者材质/Profile/模型/贴图/截图/录像/Player及原档仅本机。

5个既有文件的变更仅为明确新家族与普通相机上下文：EnvironmentSkyBinding、RoomMaskRendererFeature、RoomPositiveMaterialBuckets、RoomPositivePass、RoomPositiveRenderingLayers。未包含其他历史场景删除、输入、角色装备、战斗调试、BuildSettings及共享同步文件的旧改动。

共享Input/Forward代码按本机URP14.2.0-t1适配，源头与Unity Companion License见代码头及既有docs/ossastra/ENV-01许可文件。不是第三方美术资源发布。

v1.0技术验收与限制见validation.md。v1.1用户明确偏好C，已覆盖旧B推荐；具体控制、保存及验证见v1.1.md，不自动展开下一阶段。

功能提交：57957db500d876cfbeb94dbf996f9201d83bfa92。已推送GitHub OSSASTRA/main，独立ls-remote核验远端完整SHA一致。范围58文件：33源码/meta、20候选、4交付文档、1任务记录。构建二进制/媒体/场景/作者资产、其他工作树dirty均未发布。
本回执只补发布见证；功能源以该SHA为准。

## 2026-11-07 v1.1定向增量

独立明暗设置、C会话/保存、显式Light身份适配与原Feature两行接缝、原比较面板、显式Player探针、重放合同及交付文档。C0/候选/主场接线仅本机保存；无场景、美术、二进制、存档或其他dirty发布。

Native/GPU/保存重开、真实Windows375帧/14阶段/两角色见证/退出0、原存档隔离校验已通过。五档缓存24、Draw682/SetPass610稳定；两Shader SRP码0；GPU耗时未测。精确源清单与SHA见v1.1-source-manifest.json。远端回执在推送核验后补充。
