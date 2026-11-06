# VOXEL-LIGHT-01 配置与显式比较合同

Root在安全Edit点临时执行`VoxelSettingsContract.cs.txt`，调用`VoxelSettingsContract.Run()`。
此目录不进入Unity自动编译。生产类型尚未存在时，应返回`pass=false`且四项`missing*=true`作为有效RED。
该结果由Root确认后才开始生产实现；新Shader未导入造成的`prerequisitesMissing`不计GREEN。
Root已回传有效Native RED：四项missing为true、pass=false且无编译错误，随后才创建生产四文件。
扩展合同23项随后由Root真实执行，恰`missingTargetDoesNotEraseValidOriginal`与
`removedBindingExternalCloneIsHandedOffBeforeRefresh`为false；其余21项true。
两处最小修复已完成并离线编译0错误0警告；Root已真实复跑同一合同，23项全GREEN。

合同使用私有GameObject/Material/Profile并finally精确释放，不加载或修改主场资源。
验证九个明确Shader属性的中性与独立响应、非法值、作者材质只读、多槽复用、Profile变更显式刷新、
逐binding外部写入让权及持续固定、Original切档、归还与仅自有材质释放。
另覆盖禁用、重绑、无Profile时直接使用作者材质，以及外部部分引用自有clone后的移交隔离。

配置不会连接灯、天气、相机、业务、角色或Canvas；不会每帧写Material/PropertyBlock。
生产公开接口见当前任务Root确认的配置子租约；主场作者工具与Native交付由Root集成。

生产入口：

```csharp
comparison.ApplyMode(VoxelSurfaceComparisonMode.Original);
comparison.ApplyMode(VoxelSurfaceComparisonMode.Calibrated);
comparison.ApplyMode(VoxelSurfaceComparisonMode.Faceted);
comparison.RefreshParameters(); // 修改Profile后显式调用，不逐帧轮询。
comparison.RestoreOriginal();   // 精确归还仍属于自身的数组并释放正常缓存。
```

`bindings`中每项是`VoxelSurfaceComparison.Binding`，明确保存`renderer/original/calibrated/shaped`。
Profile仅用于B/C参数覆盖；为空时不创建副本。存在覆盖时，同一作者Material与同一Profile共用一个私有材质，
重复刷新保持其身份。作者材质、Profile数值不会被组件回写。切换配置或移除binding会释放不再需要的缓存。
若外部把数组改成“自有clone + 外部Material”，该binding固定让权，外部使用的clone在参数刷新前移交，
从组件缓存中移除且不再修改/销毁；Status明确报告`ownership handed-off`。仅保留累计数量，不持有移交引用。

Editor Inspector在Edit只编辑配置/提供归还按钮，A/B/C预览和参数刷新限Play；没有ExecuteAlways或自动Edit预览。
普通禁用/销毁精确还原原组并释放私有缓存。外部改写之后即使再次切档也不接管，直到显式重建绑定生命周期。

离线验证命令：

```powershell
dotnet build Candidates/VOXEL-LIGHT-01/Settings/SettingsContractCompile.csproj --nologo -v:q
dotnet build Candidates/VOXEL-LIGHT-01/Settings/SettingsProductionCompile.csproj --nologo -v:q
```

两个入口均已0错误0警告；配置Native 23项GREEN已由Root回传，实际主场/Player由Root另行验证，本文不代签。

最小开发面板位于`Assets/OSSASTRA/Scripts/Development/VoxelSurface/VoxelSurfacePanel.cs`，
只需明确接线`comparison`。默认折叠，`SetExpanded(bool)`可显式开合；不创建Canvas或扫描场景。
A/B/C为消费者按钮；诊断0..7、当前Profile间接漫反射/环境反射比例及Status只读显示，不回写Profile。
只处理自身IMGUI矩形事件，不注册全局输入租约。面板已纳入ProductionCompile，0错误0警告。
