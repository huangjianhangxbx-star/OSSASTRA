# SCENE-PERF-01 定向修复交付与验证索引

本轮重复成本的定向修复已通过实际Player出口：240帧中位24.5944ms、p95 26.1613ms，约40.7FPS。编辑器固定机位仍34.2965ms（约29.2FPS）；未达到稳定60FPS。真实成本和限制以cost-attribution.md与本机artifacts/SCENE-PERF-01为准。

## 已落实的定向修复

1. `RoomPositivePass.DrawTruth` 同一 Execute、完整 MaterialKey 首用刷新，原逐槽绘制与许可/深度顺序保留。实测复制次数1826→155；10帧实际合同RED→GREEN。
2. `RoomPositiveMaterialBuckets.Refresh` 使用Source/Clone/Adapter原生CRC识别变化和借用污染，稳定状态跳过两份重复材质复制，私有队列独立同步。真实生产方法fixture：稳定/变化ratio 1.0243→.1261，16项GREEN。
3. PositiveSurfaces查重使用每Build同步清理的HashSet，成功登记才加入；实际Dedup约.987→.018ms，16项GREEN。
4. Truth跨Execute缓存显式校验源/副本CRC、派生float/color和keyword，变化或污染才复制。最初仅CRC版本曾失败，最终20项GREEN后才保留。
5. 仅标准硬件深度3D Truth使用本相机有限包围体预筛，mesh/Renderer包围体取并集且保留触边余量；2D、未知与失效边界保留原绘制。16项GREEN，尚非完整像素等价证明。
6. Preview与Permission分别复用原材质桶的原生DrawRenderers，仅接管已证明同一合同的静态opaque槽；混合/动态/2D/屋顶/LOD等回退原路线。全槽实际已提交才提前省去fallback准备，Owner仍通过原MPB，原Shader与Caster保持。分类及拒绝帧16项GREEN。
7. 同一次Validate共享已读取材质数组和分类结果，重复校验的拒绝条件不变；前后各11项GREEN。

有限原生ProfilingSampler只记录原阶段；没有增设渲染管理器。曾尝试同范围三个global去重，局部52→16bytes但整帧未见收益，已撤回。

## 未改变的产品条件

原材质来源与像素许可、屋顶遮光和Collider、双角色/45°、灯光/阴影/天气/角色轮廓、源美术与体素地形均保留。未通过降分辨率、删模型、关阴影或停止世界获得结果。业务档使用明确独立副本；原主场用户dirty不覆盖。

## 性能证据的边界

历史Editor 69.533/47.051/44.491ms是不同阶段的真实帧耗时，不是可直接相减的固定机位A/B。最后一组镜头225→45°；前两组缺少pose字段。均仍超33.33ms，不能把局部合同GREEN当整场完成。

CPU有效工作/等待、RHI与GPU分列；RHI旧20树约50.35ms中38.69ms具名等待、11.66ms非等待/未分类，不能与Main线程相加，也不称全部为有效工作。GPU缺数据记unavailable。PBZ并行干扰那轮排除；FrameDebugger没取到事件记unavailable，非零成本证明。

## 本机验证入口

- `Candidates/SCENE-PERF-01/FixedViewBaseline.cs.txt`：原相机输入捕获owner，30暖后240真实帧，无强制重绘；逐帧核机位/Screen，结束释放capture。
- `Candidates/SCENE-PERF-01/TruthRefreshContract.cs.txt`、`BucketRefreshContract.cs.txt`：局部实际调用合同，非整场FPS替代。
- `Candidates/SCENE-PERF-01/CpuTimeline.cs.txt`、`GpuStages.cs.txt`：有限阶段归属；开启Profiler的时长不混入正常帧时长。
- Player Queue/Probe/Run入口详见 `Candidates/SCENE-PERF-01/Player/README.md`。唯一requestId关联queued/build/run，不使用旧exe代签新版本。

本轮第一份实际Player已成功构建（约45分钟，持续Shader编译而非卡死），240帧p50 36.254/p95 41.696ms、GPU200有效样本14.552/16.763ms，正确性0错误；OS退出码未及时捕获。它只包含前两项修复，不能证明后续修改。

固定同机位的control/truth-only/前两项优化三组分别74.492/49.393/45.839ms。后续第五项固定240帧40.375/45.844ms，约24.8FPS，Batches4127/SetPass1560/Caster1630；仍未解决。最后一次未保留global实验40.745/45.774ms。

关键整链ON/OFF/ON为42.564/5.673/42.353ms。OFF仅用于归属，已恢复；差异同时包含依赖角色/环境阶段，不能全算Shader成本。主线程PresentWait约.0002ms；最新Main CPU树48.664ms中具名等待.050ms，RHI约48.664ms中等待35.881ms，非等待不等于全有效工作。详细证据与限制见cost-attribution.md。

最终原生分桶已启用。外景三轮遮罩对照及室内label对照，在预设排除动态物体后的比较区域RGBA零差；未生成的室内preview明确跳过。未覆盖baked occlusion与共面depth tie的普适等价，不将本机限定验证代签所有未来地图。

最新guarded构建dfef5596642a4d50a501605c5e17f4c8约35秒完成，Run_20261108_000246_9745637：240完整、GPU240正样本10.6680/12.0422ms、SetPass722/Batches4126、正确性零错误、OS退出0。主场及采样画质保持1600×900/45°/Ultra/原阴影；start在暖机前仍有约.08m相机落定，非逐帧姿态锁定的Player A/B。完整固定机位对照由Editor单独提供。

保留本机可运行版本：artifacts/SCENE-PERF-01/Player_20261108_000013_9118486/ScenePerformanceMain.exe。构建使用本轮独立test-commerce，传入--scene-perf-probe时另建隔离档并自动采样退出。二进制、素材与原始测量未发布。

五份本轮临时Scene和临时PlayerProbe已通过Native删除；正常业务路由已归还，原主场dirty=true保留且不保存原场。普通业务四文件与business-checkpoint匹配。后续优先继续测每帧静态名单/材质及MPB准备；GPU实际成本亦须继续按阶段量化，不能把所有FrameTiming时长当有效着色工作。
