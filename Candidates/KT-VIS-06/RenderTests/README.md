# KT-VIS-06 Rendering 验证与接线

本目录是当前房间可见性增量的候选验收工具，不含场景、业务状态或真实存档。

## 已完成的离线验证

- `FogTransition.Tests.csproj`：首帧30%、半秒完成、同一个portal的新覆盖点、反转续当前亮度、静态失权回暗、动态即时拒绝、仅向合法侧柔边、贴地下边界的地板不整体压黑。首次执行因功能缺失 RED；当前 13/13 GREEN。
- `Rendering.Compile.csproj`：链接本机真实 Tuanjie / UnityEngine 与现有 ScriptAssemblies，编译整个 Rendering 目录；当前 0 警告、0 错误。它不是 Native shader 或实景视觉验收。
- `FogRendererContract.cs.txt`：由唯一 Unity writer 经已有 Native 命令入口执行；只创建自有历史buffer，验证有界四快照、摄像机运动不重置、房间种子切换保留退场、epoch隔离、禁用/Dispose释放与重入。

## 精确边界

- 当前眼点域仍是业务/标签权威。`WindowSightDomains` 只为主相机真实孔洞投影内绘制室外片元，排除所有建筑体积；不改变 `SeedVolumes`、`AllowsPoint`、发现状态或业务。
- `CameraOpaqueBlockers` 是显式相机阻挡集；眼点与相机线段分开检查。窗口历史仍受当前孔洞硬限制。
- `StaticEnvironment` 必须由作者明确白名单登记。`IndependentDraw` 不表示静态；角色、敌人、Spine、动态Sprite、活动门窗片、动画与可破坏物不得进入静态退场。
- 启用 Fog 时，静态 Full 表面须在 Runtime 分配 draw masks 前改为 Partial，才能走现有受限 adapter。动态原材质数组只读；6 动态 Lit 源组、256 静态桶、原 EndScope 归还路径保留。
- 只有 normal final 绘制消费历史。标签许可与屋顶预览 pass 不消费历史或相机窗景。
- 3 RT 保留。另有每主相机 1 个 56,064 字节 StructuredBuffer，存 4 份世界域，未存旧屏幕颜色。统计增加 `OwnedBufferCount`、`EstimatedBufferBytes`，Dispose 后均为零。

## 时序与保守限制

4 份世界域按约 1/6 秒采样；每个世界点依据过去与当前的域内距离估计跨界时刻。普通房间种子切换记录事件前后双边界，不因房间切换清空历史。反转从当时亮度续接。连续转身的跨界时刻是有界插值近似，极快连续多次越界时以最新四快照为限。

主控身份、传送、拓扑/阻挡、观察有效性改变时增加 `VisibilityEpoch`。失去当前许可的动态表面立即裁掉；仅明确静态表面可短暂退场。Lit 静态退场不继续接收动态实时光/阴影，而显示其原材质颜色后回暗。当前合法的 Lit 表面继续原 URP 光照。

默认入场30%→0.5秒、退场0.3秒、扩散延迟0.12秒/半径8、内向柔边0.35世界单位。当前四快照预算接受 Gain≤0.5秒、Loss≤0.3秒（Runtime配置上限）。点阵使用世界坐标；默认关闭。房间体积硬许可仍检查XYZ；盒内表现柔边只沿XZ足迹，避免整面地板处于体积底面而压黑。

墙段/顶盖的权威显隐、碰撞、Caster 与业务保持现有规则；本轮过渡优先覆盖内容黑幕，不在这里改变墙/屋顶政策。

## Native 必查

真实窗洞内外动态像素隔离；相机45°转动后正确深度且无旧画面拖影；首帧/.25/.5秒与进出门反转；失权第一帧无敌人/特效/标签更新；真实洞口光照与屋顶遮光；原动态 sharedMaterials 引用、Manual Begin/End 对称、buffer释放/重入、3RT、零 unsupported/Shader错误。
