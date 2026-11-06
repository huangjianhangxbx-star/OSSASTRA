# 环境接收方定向合同

生产范围：EnvironmentSkyBinding、EnvironmentSurfaceWorkingSet；旧Editor装配仅将panel引用改名previewConsumer。

## 接口

- `SetPreviewRequested(object owner, bool requested)`：消费者显式申请/撤销预览；生产只发布`SkyPreview`，不调用Development面板。旧panel引用通过FormerlySerializedAs保留为隐藏MonoBehaviour迁移字段。
- `TryPrepareSurfaceData(Camera, out string reason)`：单实例工作集准备。
- `EnvironmentSkyBinding.PrepareSurfaceForCamera(Camera, out string reason)`：供RoomPass准备阶段使用；失败只暂停环境表面和雨，不能撤销合法世界。
- `SkyReady`、`ReflectionReady`、`RainReady`、`PreviewReady`表示各资源能力；`SurfaceReady`及分项Status独立报告。实际draw仍需当前帧、观察许可、非Truth和实际雨量等原条件。

## 工作集边界

有效点灯先去重并过滤null/禁用/非Point/非正能量，再按灯范围AABB与相机视锥相交取保守工作集，最多8。房间最多32个相关体积；除相机可见体积外，也保留相关灯所属的离屏体积，防止室内灯被误编码为室外灯。雨棚遮蔽其下方柱体，因此只过滤null/禁用/重复项，最多16，不错误地按屋顶薄片剔除。

超限或无效矩阵/体积明确返回false，清本环境Shader计数与开关，不在已清除世界后抛容量异常；不截断数组。源Frame、原生灯、天空与反射不因该表面能力失效而撤销。Shader引用恢复后独立重建对应能力。

## 验证

`EnvironmentViewContract.cs.txt`为旧实现Native RED入口：无消费者预览占用、缺雨同时拒绝天空、9个null登记抛异常、缺准备/恢复合同。Root已确认RED后实施。

`EnvironmentViewBoundaryContract.cs.txt`覆盖可选天空/反射恢复、按需预览、32/8/16边界及离屏灯所属室保护；私有临时对象finally释放，不修改主场登记。由唯一Unity writer在实际编译后执行GREEN。

`dotnet build Candidates/RENDER-ARCH-01/EnvironmentView/ViewCompile.csproj -v:q`离线覆盖真实源码，实施后0错0警。Native结果以Root最终任务交付为准，不以离线编译代签。
