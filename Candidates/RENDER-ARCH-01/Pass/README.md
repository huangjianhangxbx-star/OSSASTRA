# 房间世界与可选表现的边界合同

本目录是 RENDER-ARCH-01 的定向 Native 合同源码。由根执行者在当前工程的 Native C# 入口运行，不放入 Assets，不生成另一个场景。

## 验证入口

- `PassBoundaryContract.cs.txt`：当前房间 Pass 在接管前拒绝未知核心材质；已知材质可以准备；失败不改坏源引用；结束后还原源；Pass 不再直接持有角色覆盖/外缘缓存，独立阶段存在。
- `OptionalCommandsContract.cs.txt`：真实角色阶段的命令构造边界。构造中途故障后 CommandBuffer 必须为空；下一次有效构造能恢复；不执行 GPU。
- `EnvironmentCommandsContract.cs.txt`：真实环境阶段的同类合同，Surface 上传、天空、降雨共用此边界。

根执行者已确认实施前 RED：旧 Pass 的已知源准备、坏源不被修改及还原成立；未知源前移拒绝、缓存分离、独立角色/环境命令保护均不成立。最终 GREEN 与主场验证由根执行者统一记录，不将本目录的非 GPU 合同冒充 Player 证明。

## 生产接口与所有权

`CharacterLightingRenderStage` 按相机持有 `CharacterCoverageCache` 与 `CharacterOuterGlowCache`，负责创建、owner/Shader 变更、失效、配置修复及 Dispose。相机颜色、真实深度和标签许可只在同步调用期间借用，不获取或释放其所有权。

房间 Pass 保持房间/门户许可算法和核心资源。它只协调角色阶段的 `PrepareCoverage` 与 `AppendOuter`。每帧先撤销旧 Energy；只有本帧成功的合法能量才能交给 WorldLook。C 诊断实际产生了输出才停止后续 Look；不支持的可选外缘早退不撤销合法世界。

`RoomEnvironmentRenderStage` 不持有天空、反射、雨或预览资源；这些仍由 `EnvironmentSkyBinding` 管理。它隔离可选环境命令的 CPU 构造。Surface 上传构造失败只通过 `ClearSurfaceForCamera` 关闭本模块全局；天空/降雨构造失败只丢弃该缓冲。已完成的核心世界与标签不受影响。

委托按阶段实例缓存，不在每帧为独立命令构造创建闭包；借用 frame、RenderingData、RTHandle 引用在 finally 清除。

## 安全与恢复边界

- 未支持或混合的可绘制核心表面在剔除前明确拒绝；不通过无裁剪原画面回退，不强发不可信的 WorldLook 许可。
- 空 Mesh、无 Sprite、失活或强制不绘制的源不因不存在的几何而使世界失效。
- 角色构造异常释放该阶段自有资源，并按配置签名锁存；owner、尺寸、Shader、成员、材质/纹理身份、相关设置变化或 CoverageInvalidated 可解除锁存。
- 命令构造异常会清空尚未提交的半成命令。GPU 提交异常无法回滚，仍交由核心安全失权；不宣称已局部恢复。
- 每一批实际命令构造使用配对 `RenderCommandScope`，Restore 写入后才提交；Clear 后的新批重新进入 scope。原生 DrawRenderers 的实际适配 Pass 与 Full 源 Pass 使用窄 `RenderPassReadiness`；不永久修改全局异步设置、不全量 Warmup。
- 实际 Shader、PMA、房间许可、世界新深度、外缘算法、当前 45° 机位与认可参数均保留。
