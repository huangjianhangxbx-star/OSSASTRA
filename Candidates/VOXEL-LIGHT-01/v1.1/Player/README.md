# v1.1 Windows 主场烟测候选

`VoxelSurfacePlayerProbe.cs.txt` 是既有显式命令行探针的定向增量，仍只在 `UNITY_STANDALONE_WIN && !UNITY_EDITOR` 生效。Root在退出Play安全点审查后再替换生产源；子任务未修改Assets。

- 保留原Commerce启动前隔离、原档哈希、实际gameplayCamera冷启60帧、A/B/C×三时刻、两原角色见证、60秒超时和退出码合同。
- C阶段读取独立 `SessionSettings`，B新增两项强制按中性0核对；实际私有材质的11项控制均读回。
- 新增同机位午间 `C0_12`、`C_Lit10_12`、`C_Lit20_12`、`C_Back10_12`、`C_Balanced15_10_12` 五档；每档至少20个真实主相机帧且独立文件名，不覆盖旧九组。
- 五档暂时禁自动天气并等待已有过渡结束；云速临时0，保证五组的相机矩阵、太阳方向/颜色/强度、环境底光、时刻、云参数和天气相同。结束恢复原作者字段；不改变全局光源参数。
- 每档检查来源材质与2D Profile未变、实际私有cache有界；五档额外核对私有材质实例ID不变。
- 原会话完整快照（含诊断）经一次性私有Profile载体复原，再恢复原档位；没有测试侧作者Profile写入。

编译：`dotnet build Candidates/VOXEL-LIGHT-01/v1.1/Player/PlayerProbeCompile.csproj`。该步骤只做真实Unity/当前工程程序集引用的编译检查；Windows构建和实际图形运行须由Root完成，不能以编译代替运行通过。
