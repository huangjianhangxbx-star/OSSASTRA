# Registration：真实引擎 helper 合同

`RegistrationCompile.csproj` 已离线编译：0 warnings / 0 errors；每次编译前检查最新 Player PID 未在运行。**Native 尚未执行，未声称 GREEN 或性能收益。** Root / cache 正在完成 helper，Native 应使用最后冻结并编译的 Overlay。

Root 执行：

```powershell
& 'Candidates/SCENE-LOOK-01/InvokeUnity.ps1' `
  -CodeFile 'Candidates/SCENE-PERF-02/V11/StaticTerrain/Tests/RegistrationContract.cs.txt' `
  -Receipt 'artifacts/SCENE-PERF-02/v11-static-terrain-registration-receipt.json'
```

命令 `StaticTerrainRegistrationContract` 固定从 `StaticTerrain/Overlay/bin/Debug/netstandard2.1/ScenePerf02StaticTerrainOverlay.dll` 获取 helper，核对 AssemblyName / Location，记录实际 loaded MVID 和磁盘 DLL SHA。它不会从 AppDomain 第一个同名类型或旧 MpbCandidateCompile 程序集取 helper。Frame / Surface 的具体类型来自该 helper 的方法签名，因此允许 core Overlay 与当前生产公共结构类型不同。先冻结该 DLL 再执行；如重编过 DLL，Root 应核对 loaded MVID，避免把已加载旧程序集当成新代码。

真实 fixture 为一个明确 Installation、两个 MeshRenderer、各自私有 VoxelLit Material、两段 triangle submesh、两个禁用 Camera。没有角色 / 敌人 / NPC / WorldProp 业务根，没有 Camera.Render、事件订阅、场景切换或素材写入。Spine 用真实 SkeletonRenderer 派生组件，但覆盖缺失 SkeletonData 的初始化，仅验证组件存在性。

覆盖的实际 API / 断言：

- Bind → CaptureBuild → TryBuild → CapturePrepare → TryPrepared → End / Clear，连续调用发生于同一实际帧。
- 稳定 Row、事实 Materials 数组 identity；完整成员 / 顺序 / Surface / Family 字段；仅受影响源的 generation 改变。
- 同一 chunks 数组元素修改、重复、删除 / 恢复，Build 与 publication 后的 Prepare 两个阶段均验证。
- 同 Material 引用的实际颜色、keyword、Opaque / Transparent queue、Shader、Texture 修改；先确认实际 CRC 或明确 shader / queue 身份发生变化，不捏造 cache-hit 计数。
- 多槽逆序、null source、零槽、17 槽预算；每次读完整当前布局。
- 活动 EnvironmentMaterialResponse 新增、字段原位修改、renderers 同数组改元素、禁用 / NativeLit 最邻近早退；四个 tag 输出还通过当前生产真实 ApplyEnvironmentTag 逐项比较。
- 叶 / 祖先上实际新增 disabled Animation、Animator、LODGroup、Spine；Prepare 前立即退出、下一 Build 不复用旧负结论、移除后恢复。
- renderer enabled / active / forceRenderingOff / sharedMesh，installation enabled，parent / matrix 的 publication 边界。
- Camera A/B/A、EndPrepare、null frame / camera、销毁原 Renderer 包装器后登记全新引用。
- 实际 renderer / slot MPB foreign 值、另一 Renderer 的空块、槽引用、私有源 CRC 全部保持，不假装 helper 已覆盖 PropertyScope 合并路径。
- Clear 的 rows / nodes / sources / layouts、四个 scratch lists、installations / camera 引用确实清空。

持久 `Terrain_Grass.mat` 仅只读取模板 / CRC，永不改写。如它本身为 VoxelLit，则额外执行实际持久 VoxelLit 捕获；否则 `persistentVoxelCoverage` 明确 unavailable，不为凑分支创建 `.mat`。主场实际 Comparison 私有源是本合同的主要能力覆盖。

重要范围限制：helper 的 fresh Build 可以读取一个已经移动的 fixture 矩阵；**原始静态登记矩阵资格属于 Module 集成 gate**。本合同明确区分“Build 后移动使旧 Prepare 失效”与“移动后是否允许作为生产静态对象”，不以 helper 测试代签后者。它也不代签 MPB 模板共享 / 还原、材质计划租约、完整主场 camera 生命周期、GPU 像素或 Player 净收益。

真实 baseline RED 已在独立回执 `artifacts/SCENE-PERF-02/v11-static-terrain-repeated-calls-20261109-012500-7910829.json` 成立：语义 / 槽 / 还原 / source / scene 均 PASS，实际旧布局与计划未保留。Registration 不拿“baseline 没有新 helper 类型”假造 RED，也不要求 frame.PositiveSurfaces 数组 identity。
