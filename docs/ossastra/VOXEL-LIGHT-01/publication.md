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

功能提交cffa8c52f57ed5a64f50544e5e5ee6fb7b50d09f已推GitHub OSSASTRA/main，独立ls-remote完整SHA一致；35精确白名单文件。源码、候选重放与文档完成，场景/C0与候选作者资产/所有媒体/Player/其他dirty仅本机。没有同步Gitee或强推。

## 2026-11-07 v1.2定向增量

材质+响应完整Look会话与保存、显式默认写保护、既有体素面板入口，以及可重放Native安装/诊断/合同/短程Player候选。当前实际候选资产、场景、几何、美术参考、截图/录像、Player及存档证据仅本机；Shader数学、全局环境/WorldLook/2D均未改。源清单及SHA见v1.2-source-manifest.json。

必要验证已完成：会话17/保存15（含默认别名RED→GREEN）、持久化重开与默认生命周期、实际Windows121帧/六状态/两角色/OS0/后续原档隔离。首次Editor隔离失败及原档checkpoint变化见v1.2.md，不作整轮原档无变化的表述。最终面板提示/完整候选读取为后续小修，编译通过，没有重跑整套Player。推送后核对远端SHA，再补发布回执。
