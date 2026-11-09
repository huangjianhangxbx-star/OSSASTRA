# VEGETATION-01A v1.2 最终定向只读审查

审查时间：2026-11-10。仅源码、已有候选与材质资产只读检查；没有调用 Unity、运行 Player、改变构建或生产文件。Root 的 R0–R4/GPU/实机结论不由本报告重复签署。

## 发现：两项 Player 基准入口／结果判定问题

### P2：空格形式的密度参数会被静默忽略

- 位置：`Assets/OSSASTRA/Scripts/WorldRendering/Vegetation/Development/GrassLabPlayerRun.cs:17`。
- 实际代码仅查找以 `--grass-density=` 开头的单个参数。启动 `--grass-benchmark --grass-density 54` 时，`densityArg` 为 null，直接跑推荐密度，输出名也是 `player-recommended-...json`，并不会生成 54 档。
- 本轮最小规避：实际进程明确传入 **`--grass-density=54`**。核对 JSON 两层 `dataDensity=54`、seed/maxDensity、placementsFingerprint 和草簇数，不能仅凭启动命令的文字说明认定高密度已测。
- 最小修正建议：统一解析等号与空格形式；遇到存在但无法解析、非有限或越界的参数明确失败。此处未修改。

### P2：基准错误或条件漂移也可能退出为成功码

- 位置：`Assets/OSSASTRA/Scripts/WorldRendering/Vegetation/Development/GrassLabPlayerRun.cs:24`；关联 `GrassLabCostSampler.cs:194`。
- Player 仅以 `File.Exists(output)` 决定 exit 0。Sampler 在中止、`sampling-error` 或 `comparable=false` 时也会写有限报告，因此文件存在不代表完成了四态、条件一致或结果有效。
- 本轮最小规避：独立读取 JSON，要求 **`completed=true`、`comparable=true`、四个 state 全部 completed、error 为空、cleanupWarnings 经核对**；GPU 另查有效样本，不将缺失写成 0。退出码只能证明进程结束且文件存在。
- 最小修正建议：Sampler 暴露只读结束状态，Player 按完成／可比较／写入结果返回不同退出码；保留失败报告供诊断。此处未修改。

## 重点路径：本轮未发现可确认的阻断错误

| 路径 | 只读结论与范围 |
| --- | --- |
| 运行 Profile 副本 | `GrassLabControls.OnEnable` 捕获作者引用，`SwitchCandidate` 只 Instantiate 运行副本；各层材质由 `GrassLayer.RebuildFromSaved` 独立复制；切换后旧 Profile 销毁，正常 OnDisable 还原作者引用再释放副本。未见本轮正常 A/B 切换把调参写回作者资产的路径。 |
| 单地形底色 | `RefreshAppearance` 必须单 surface、surfaceId 匹配、有效版本／尺寸才启用；`GrassTerrainSample` 使用未变形根的 world XZ 映射，并拒绝范围外 UV、alpha 无效区，回退 BottomColor。当前每层独立地图符合该合同；移动地形后仍需要显式重烘，不能将旧世界映射视为自动跟随。 |
| 项目环境受光 | Project 路线先令 `GrassVideoArt(...shadow=1...)` 生成未受光颜色，再调用一次 `EnvironmentShade`。没有先将主光阴影手工烘进颜色、之后又乘一次环境阴影的当前路径。Tutorial 路线独立使用手工阴影色组。 |
| Release 风变体 | 当前 Candidate A 材质 keyword 为空、Candidate B 材质保存 `_GRASS_WIND`；两者都由场景候选引用。没有仅在运行时启用、却完全无资产依赖的明显风变体遗漏。仍由实际 Player 动态检查确认最后构建结果。 |
| 正常 54 档生成 | 等号参数通过后，`Generate(54)` 使用显式表面／排除区；data 为 Instantiate 运行副本，作者放置资产不写盘；两层共享的是控制器运行 Profile，修改 density 不写作者 Profile。运行 data 副本到进程结束才回收，不宜借此声称任意运行中重启基准都已验证清理。 |
| Sampler 数据语义 | 主／渲染线程总时长明确含等待；GPU 只保留独立时间戳、有限且正数，无有效值是 null；条件漂移保留在 comparable 中。四态报告仍需逐项读取，不能由退出码代签。 |

没有为了凑满三项而增加推测性缺陷。当前两个发现可先按上述入口／报告核验规避，不要求中断正在进行的 Release BuildPlayer。
