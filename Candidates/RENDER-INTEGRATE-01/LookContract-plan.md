# 主场 Look 快照接入计划

目标：读取旧 T7C2 展示档、T7B 空气/调色档及 C1 的环境调色参数，为已通过房间裁剪的世界输出单相机参数；不调用旧预览控制器，不改灯、管线、材质或角色权威。

1. 先写 `LookContract.cs.txt`，在 Unity 原生命令中确认新入口不存在，记录 RED。
2. 新增 `WorldLookSettings` 不可变快照和 `WorldLookEffectOptions`：使用相机前向投影计算焦点；显式开关、有限数值钳制、源 Profile 只读。Green 的逐材质角色色板不冒充已接入。
3. 新增 `WorldLookCameraRegistry`：明确 owner / camera 登记，其他 owner 不可替换或释放；释放事件只给对应相机。
4. 新增 `WorldLookController`：只订阅已有 `RoomMaskCameraRegistry.FramePublished`，在有效帧后读 `ObserverPosition` 与 `ExteriorView` 选室内/外档。禁用、相机改绑或输入失效时释放；不查全场，不订阅第二条相机绘制回调。
5. Native 合同检验投影焦点、室内档选择、源参数只读、开关、异常参数、owner 隔离、禁用及同帧失效清理；Root 负责编译/运行/GPU与生命周期实测。

接口：`WorldLookSettings.Build(Camera, Vector3, HD2DPostProfile, OutdoorAtmosphereProfile, RegionalLookProfile, WorldLookEffectOptions)`；`WorldLookCameraRegistry.TryGet(Camera, out WorldLookSettings)`；`Released: Action<int>`。Feature 只读快照，不修改其字段。
