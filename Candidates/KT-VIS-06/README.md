# KT-VIS-06 候选与合同

这些是当前 Lab 的开发工具，生产实现位于 `Assets/OSSASTRA/Scripts/SceneVisibility/Rooms/`。只有唯一 Unity writer 能经已核对的 Native 项目入口运行会改变场景或运行姿态的候选。不要直接重放带旧 instanceID 的装配历史。

- `CoreTests/PortalNaturalSight.Tests.csproj`：20 个自然/窗口几何合同。
- `CoreTests/LegacyRegression.Tests.csproj`：原 20 个有限行为回归。
- `RenderTests/FogTransition.Tests.csproj`：13 个时序/柔边行为。
- `RenderTests/FogRendererContract.cs.txt`：Native 自有历史 buffer 8 项合同。
- `NativeVisionContract.cs.txt`：真实帧 10 项窗景、距离、实体板和 epoch 合同。
- `NativeTransitionContract.cs.txt`：真实 RGB 两轮入退场，非静态自有片失权立即消失与资源清理；采样不足不代签。
- `NativeLightingContract.cs.txt`：原局部灯 ON/OFF 真实地板颜色及 Caster/原灯状态恢复。
- `ScopedSourceReferenceContract.cs.txt`：原副手 Sprite 引用跨手工 Begin/End 和真实帧保持。
- `FinalLifecycleContract.cs.txt`：V5，包含原 V4 恢复与实际历史 buffer Release/Reinit。
- `VerifyContracts.cs.txt`：N00 只读快照，不是完整验收。
- `PrePlayGuard.cs.txt` / `FinalReopen.cs.txt`：精确独立档预检和场景保存重开读回。

Play 中原系统会创建两个无渲染/无业务的私有物理 scene；合同只允许唯一带资源路径的 Lab，不能把这些旧物理 helper 当第二游戏场景。Edit 预检仍要求单场景。

执行用已验证 endpoint 的 `mcp_call.py`（复用05 transport，不自动探测/重试未知修改）。所有异步合同先返回 running，应读取静态 Result 或本地完成 JSON 后才能进入下一个修改型候选。所有测试素材、PNG、JSON、编译产物仅本机，不属于代码发布。

本轮证据目录：`E:/WORLDCREATOR/XingHaiHuiLang/研究/可见性修复/KT-VIS-06/`。纯 .NET 合同可用 .NET 9 运行；Rendering.Compile 与 FXC probe 是本机真实编辑器 DLL/Windows SDK 验证入口，不宣称其他硬件兼容。
