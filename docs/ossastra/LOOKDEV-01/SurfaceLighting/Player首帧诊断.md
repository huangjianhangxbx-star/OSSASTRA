# P1 Player：FindPass -1 只读排查

2026-11-10。未修改源码或操作 Unity。实际 P1 日志已读：`artifacts/LOOKDEV-01/SurfaceLighting/PlayerRuns/20261110-085039-344-evidence/evidence/player-evidence.json`：RoomVariant_0、正确 wrapper 名称，Forward=-1 / Outline=-1，roof/full 声明0/2。此前 HasProperty 假阴性修正没有解决此第二个独立故障。

## 已核对的事实

- wrapper 只有一个 SubShader：RenderPipeline=UniversalPipeline，LOD300。七个声明 Pass 为 ForwardLit / ShadowCaster / GBuffer / DepthOnly / DepthNormals / Meta / Outline。D3D11在每个相关 only_renderers 列表中。不存在另一业务 SubShader 被误选的分支；末尾仅 InternalErrorShader fallback，isSupported 单独不能排除落到错误 fallback。
- GraphicsSettings 及全部六档 Quality 的 customRenderPipeline 指向同一 URP guid168d3997fc8e92a42bf1e6a604a87aee；Standalone 默认 Ultra。OSSASTRA 源码未找到 globalMaximumLOD、shader.maximumLOD、SetQualityLevel 或改 renderPipelineAsset 的写入。资产事实不能替代 Player 当前值。
- 本机 URP14.2.0-t1 `UniversalRenderPipeline.cs:299` 在构造 pipeline 时设置 Shader.globalRenderPipeline=UniversalPipeline，:352 Dispose 清空。首次 Update 未必已发生首个相机 render。
- `SurfaceLightingRecipe.cs:157` 将 pendingInitial=false 后只调用一次 TrySetState。失败后不会自动重试；Evidence.cs:37 只等 HasLease 最多30秒，没有再次调用。故30秒后的错误文本可能仍是首帧采样，不证明30秒后的 FindPass 仍为-1。

## 排序与最小证伪

1. **首次 URP 初始化时机**。直接用同 P1 Player，稳定显示数帧后重新点击 B/C；按钮确实会重新调用 RefreshPlayerRetention。若随后 F/O正常且切档成功，排除永久缺 Pass，确认一次性初始失败/等待不重试链。后续修复应等待真实 pipeline/目标相机完成至少一帧后有限重试，不固定睡眠、不松开 Pass gate。
2. **Material 的 selected-subshader/cache 未刷新**。若晚点击仍失败，在同一运行时记录实际 pipeline、全局shader tag、Material.passCount/GetPassName、Shader.subshaderCount/GetPassCountInSubshader/FindSubshaderTagValue，以及 F/O。以临时 clone 做同shader重赋前后比较；不重赋现场作者材质。若声明仍七Pass而材质只报fallback/零Pass，并且重赋恢复，才考虑缓存修复。
3. **Player 有效 RP / Shader LOD 拒绝**。仅当 Shader.globalRenderPipeline 不是 UniversalPipeline，或 globalMaximumLOD/maximumLOD实际小于300，才对应选择过滤；不要先改Quality资产。多业务 SubShader 假设已被源码排除。
4. **构建实际 Pass/变体依赖**。编译日志 F1152/Outline24 是生成证据，不直接证明运行时 selected Pass。若运行时在匹配 RP/LOD 后，Shader.GetPassCountInSubshader(0)或LightMode实际列表仍缺，继续审精确保留/构建输出；不能把此项写成已经证实 stripping。

最小事实集：frameCount；GraphicsSettings.currentRenderPipeline、QualitySettings.renderPipeline、RenderPipelineManager.currentPipeline类型；Shader.globalRenderPipeline；Shader.globalMaximumLOD与wrapper.maximumLOD；wrapper.subshaderCount、每SubShader的RenderPipeline tag/PassCount/LightMode；材质shader实例ID、isSupported、passCount/GetPassName、F/O、两个声明索引。

这些 API 已在本机 UnityEngine.CoreModule.xml 核对存在（FindPassTagValue有 subshader/pass overload；FindSubshaderTagValue；GetPassCountInSubshader）。本轮没有要求再全量构建，先用当前 P1 Player 晚点击这一最短可证伪路径。

Root随后实际同Player UI晚重试C成功，实际后端RealToon17槽且图正确。已证是首次URP初始化时机，不是变体被剥离；将自动initial尝试延后到明确目标相机首渲染事件后，非固定延时或循环retry。
