# ENV-01 验证与装配入口

本目录是当前主场环境系统的开发验证入口，生产代码位于 `Assets/OSSASTRA/Scripts/EnvironmentSimulation`。不作为游戏运行依赖，不含源模型、音声、场景、存档或测试二进制。

## 运行边界

- Root 是唯一 Unity/Git writer。临时配置/运行根合同须在稳定 Edit 执行；Panel 两组合同必须在已经装配、使用隔离存档的主场 Play 执行。
- 先准备编辑器并确认真实模式/编译完成，再执行合同。一次失败的编译不能用旧程序集代替新实现验收。
- Native 入口通过 `IFunplayCommand` 调用；每份合同只清理自己创建的对象。GPU 合同要求有效的非黑对照，保留 alpha、权限及资源恢复检查。
- `AssembleMain.cs.txt` 是本轮隔离测试装配脚本，会改为 `env01-runtime-isolated` 测试路由；正式增量装配 API 是 `EnvironmentMainSceneSetup.Apply`，自身不改业务存档路由。交付前恢复原路由。
- `mcp.py` 是本次编辑器重启后原连接仍指旧端口的本机恢复适配，调用相同 MCP 工具，不是额外 Unity 后门。当前地址仅供本次已验证会话；其它会话先确认编辑器工程/端口。

## 合同分组

| 分组 | 入口 |
| --- | --- |
| 独立数学 | `RuntimeHost/RuntimeHost.csproj`，18 项 |
| 时刻/快照/生命周期 | RuntimeContract、RuntimeAdvancedContract、RuntimeLifecycleContract |
| 天气调度 | ClimateDurationBudgetContract、ClimateDeterminismContract |
| 对照与空气 | ContributionContract、WorldLookEnvironmentContract、NightLookContract |
| 实际受光/PMA/覆盖 | LightingContract、LightingExtensionsContract、NativeCloudContract、PmaContract、ManualUniformContract、WetnessContract |
| 天空/雨权限 | SkyContract、SkyWeatherGpuContract、RainProjectionContract、SkyLifecycleContract |
| 表面和信息分类 | FullSurfaceEnvironmentContract、EnvironmentSurfaceRoleContract |
| 界面与装配 | PanelContract、PanelDiagnosticsContract、SetupContract、RenderingFinishContract |
| 植被风 | VegetationWindContract |
| 原生底光与退出 | NativeAmbientContract、FinalLifecycleContract、FinalSceneReadback |

`ShaderAudit.cs.txt` 提交实际 pass 并检查本机 Shader 编译消息。Native GPU 合同与真实主场截图/像素对照互相补充，不能只用 `isSupported` 或 Inspector 数值代替实际画面。

`SetView.cs.txt`/`ProbeShadow.cs.txt` 只在隔离主场 Play 中取同机位证据。根目录的临时 JSON 请求和 PNG/MP4 回执保存在项目外研究目录或 `Library/FunplayMcp`，不发布到 Git。

`FinalRestoreAndSave` 只在隔离会话结束后的稳定Edit执行一次，恢复原业务入口；之后不得再用该原路由启动验证Play。`check_protection.py` 只读核对1432个原源档/存档、42项原dirty及同步前缀。最终主场已是正午、6分钟周期、自动昼夜/自动天气关闭、非真值显示。

## 来源与许可

原生光照函数的最小主灯适配基于本机 URP14.2.0-t1 与 Spine URP4.3.25。升级包时需复核函数差异；保留 [Unity URP 声明](../../docs/ossastra/ENV-01/Unity-URP-LICENSE.md) 和 [Spine Runtimes 声明](../../docs/ossastra/ENV-01/Spine-Runtimes-LICENSE.md)。
