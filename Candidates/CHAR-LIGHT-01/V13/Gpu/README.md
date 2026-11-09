# v1.3 有界GPU诊断候选

子租约：2026-11-09 `/root/char13_gpu` 仅此目录，Root是唯一Unity/Assets/生产/Git writer。本目录只准备候选与离线编译，不运行Unity。

`ImportProbe.cs.txt`由Root单次Native导入临时诊断Shader；拒绝已有目标。`Probe.cs.txt`要求实际隔离Play、当前主场唯一CharacterLightingSystem及两个真实接收者，不要求总sceneCount=1（Play包含私有物理场）。

探针调用正式 `EnvironmentSkyBinding.UploadForCamera` 与实际 `CharacterLightingSystem.Bind`，紧随float RT绘制回读 controls/shoulder，能证明实际绑定路径上传，不把相机结束后已Clear的全局零当漏绑。

body/保护/原图乘光使用正式HLSL函数。环境房间/点灯数据来自当前实际Frame；主灯方向和颜色由当前明确登记的RenderSettings.sun重建。**不重放URP阴影采样，sampleReceiveShadow=0，因此仅是当前环境数据下的无遮挡样点，不是主场精确片元、实际阴影或最终显示截断证明。** 内侧R输入为0；Root主场单变量分别关内边/外晕的证据另行负责。

实际atlas只在单一world/normal、white slot tint下重采样；不声称复原当前骨架附件/slot分布。源alpha>=.95样本的输出峰值及>1比例是辅助，非审美门槛。颜色/遮挡/阴影/最终RT定位由Root实际主场对照补齐。不得将私有RT/Shader留在正常运行依赖。

全局标量/向量/有界数组和RenderTexture.active在finally恢复；材质、mesh、读回纹理、RT、命令缓冲释放。若已有环境数组尚未初始化，开始前拒绝，避免无法恢复空数组的上传副作用。

离线编译：`dotnet build ProbeCompile.csproj --no-restore`（首次先restore）。真实GPU执行和资源读回仍由Root完成。

## 冻结候选与已做验证
2026-11-09 独立.NET编译0 warnings/0 errors，未触发Unity编译、未执行GPU。Probe SHA256 9E8AE7B093053E5D64E6891D85EF37CE8FC7952D3590A1BA4CACCD709529F521；Shader SHA256 60B3EBA28A317F0B245E9C85A35218F6E13295E773AFA01ED8F554EE641ACC09；Importer SHA256 0606519184A8606464CD640BDB2F73654BF21471AB88DD319D943A126B9DCEBF。Mode7会确认EnvironmentUpload实际启用；完整输出finite、所有全局恢复及receiver Settings未改共同进入pass。ReturnValue只返回短回执，详情从其path读取，不为取证重放Native调用。

先恢复Root所需A或保留具体候选后再运行；本探针永不偷偷写K/C，也不把当前strong(.4/.8)命名为旧A。每行均记录真实GPUcontrols/shoulder。
