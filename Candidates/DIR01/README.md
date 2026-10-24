# DIR01 本机研究台

这是隔离研究工具，不接正式角色或战斗。商业骨架、贴图、官方 runtime 及截图均不随仓库发布。

本机运行 `StartPreview.ps1`，再打开 [研究台](http://127.0.0.1:5187/)。脚本复用现有服务或启动隐藏 Node 进程，重启电脑后需再次启动；不是开机服务。端口仅监听 127.0.0.1，不提供资产写入接口。不要向外网转发这个研究端口。

推荐观察顺序：

1. A 全分支，观察五个中文方向根、八个拼音辅助根和原点。
2. B 只播动作，选择 a1 或 a2，检查动作同时影响多个子树。
3. C 只播方向，逐个选面，观察哪个根移到原点。
4. D 双轨＋源裁切，仍能看见场外分支。
5. E 开启“原作矩形剔除的独立等效对照”，选择“角色近景”；再单独比较原型清理、辅助分支、约束开关。
6. F 连续转向，分别比较 mix=0 与 0.4。主动作进度可独立固定。

“原作矩形剔除等效”用临时清附件实现画面对照，原作实际通过自定义 Slot alpha 隐藏，二者不是逐指令等同。“可绘附件”是显隐后的几何候选数，不是 GPU draw call 或裁切后像素数。手骨标签是实际同名骨候选，尚未证明每个动作都应挂到中文同名手骨。

运行测试（使用已有 Edge 与原型内 Playwright，不下载依赖）：

```powershell
node Scripts/VerifyPreview.cjs E:/WORLDCREATOR/XingHaiHuiLang/Origin/game/node_modules/@playwright/test E:/WORLDCREATOR/XingHaiHuiLang/work/OSSASTRA/DIR01-20261024/evidence preview-validation.json
node Scripts/VerifyBoundaries.cjs E:/WORLDCREATOR/XingHaiHuiLang/Origin/game/node_modules/@playwright/test E:/WORLDCREATOR/XingHaiHuiLang/work/OSSASTRA/DIR01-20261024 boundary-validation.json
```

完整结论见 `docs/ossastra/DIR01/`。从别的机器克隆并不能自动得到受限本机资源；需要合法素材与匹配版本 runtime，并通过参数指定路径。4.3 原始工程副本已经用官方 CLI 单独读取，不向这个 4.1 播放器喂入 4.3 JSON。
