# v1.2 定向验证入口

- `dotnet run --project Candidates/CHAR-LIGHT-01-v12/Settings/SettingsContract.csproj`：真实Settings缺省、Clone和Sanitize。
- `Math/CharacterEnvironmentFusionGpuContract.cs.txt`：现有正式WorldTile路径的基础染色、保底、A兼容、D/L及非法输入。
- `Math/CharacterEnvironmentAlphaGpuContract.cs.txt`：实际Spine/DepthPlane、24个texture/slot透明度样本。
- `Source/EnvironmentTintSourceContract.cs.txt`、`EnvironmentTintSourceBoundaryContract.cs.txt`：实际SkyBinding源及色彩空间/连续性。
- `PanelContract.cs.txt`：独立A/B方法不会覆盖其他设置。
- `MainUiContract.cs.txt`：本轮主场两人专用集成合同；真实保存按钮会将当前运行参数显式保存到已绑定的独立Profile，运行前先确认隔离场和待保存参数，不用它代替只读检查。

Native `.cs.txt`由当前工程Funplay MCP `execute_code`执行，不复制到Assets；GPU合同在finally归还借用全局与私有资源。源/颜色测试不修改主场、作者Profile或业务档。主场验证由唯一Unity writer在隔离档进行。

RED：原Shader enhancedWarmer/floorHasTint失败、自然源/色度下限/诊断3项失败；旧缺省和UI入口缺失失败。GREEN及实际画面对照见`docs/ossastra/CHAR-LIGHT-01/v1.2-交付.md`。v1.1非法回退默认2/.22已因用户新批准有意迁移到4/2，原合法Profile不批改。
