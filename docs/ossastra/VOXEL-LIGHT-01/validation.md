# VOXEL-LIGHT-01 验收

实现、Editor合同、主场保存重开和Windows图形Player已通过。最终保存主场保留A启动；B为推荐校准候选，C轻分面可选，不批量覆盖整地图。

| 检查 | 实际结果 |
|---|---|
| DX11浮点GPU公式 | 80项GREEN，先真实数值RED |
| 配置、所有权与恢复 | 23项GREEN |
| 新家族许可路由 | 18项GREEN |
| 普通相机环境上下文 | 17项GREEN |
| Player存档隔离 | 10项Native GREEN，不操作主场Store |
| 两Shader四Pass | 同步SetPass无消息；SRP Batcher兼容码均0 |
| 主场保存重开/最终回读 | 22组原引用、两Profile、45度、1.99/.02/2、MissingScript0；Edit clean |
| 连续录像 | 26.01秒、396捕获帧；实际太阳/相机/移动冷暖点灯 |
| Windows Player | A/B/C×12/18/0九组及原主控跟随角色见证全部通过 |
| Player实际绘制 | 冷启60帧、各阶段21帧、共270真实主相机帧；7.554秒；RTX4080SUPER/DX11 |
| Player错误/退出 | Error/Assert/Exception 0；OS进程退出码0 |
| 原角色同屏 | 两独立批准Profile、覆盖2draw/2MiB、外缘7draw/25,804,800bytes有效 |
| 状态归还 | 环境、两角色Profile、体素Profile、作者材质、跟随/档位恢复，缓存有界 |

## 本机证据

根目录：E:/WORLDCREATOR/new/Project_ANKALUTE/artifacts/VOXEL-LIGHT-01/

- Player_20261106_160709/reports-character-witness/result.json 为最终完整见证；同目录九图和CharacterWitness.png均已查看，无洋红/统一鲜青蓝占位，三维分面和两原角色同屏正常。
- final-player-process.json、final-player-exit.json 记录可见Windows窗口和实际退出码。
- player-abc-contact.png 为九图对照；Fixed/、Diagnostics/保留Editor固定太阳和七分项。
- sun-camera-local-light.mp4 为26秒真实连续录像；早先因重载中断的录像不作完成证据。

最终构建0错误、43警告、529,672,347bytes、62.131秒；前次首次完整构建245.983秒。警告包含已有RoomSightTruthColor、URP后处理、旧Light2D及旧资源模板MissingScript，不声称全工程零警告。当前主场自身MissingScript为0。

## 资源口径

1280×720实际Player正午样本，以下不是模块GPU耗时或整段平均：

| 指标 | A | B | C |
|---|---:|---:|---:|
| 正向许可surface draws | 464 | 464 | 464 |
| 正向阶段自有材质 | 211 | 223 | 223 |
| 源材质桶 | 45 | 48 | 48 |
| 比较器缓存 | 0 | 12 | 24 |
| 正向纹理/buffer | 3/1 | 3/1 | 3/1 |
| 正向纹理估算bytes | 66,355,200 | 同A | 同A |
| 正向buffer估算bytes | 56,064 | 同A | 同A |
| 全局最近完整帧Draw/SetPass | 681/605 | 681/609 | 681/609 |

B/C按作者Material/Profile对数有界缓存；切A/禁用释放由专项合同另验。正向阶段额外12材质对应新源家族分桶，体素模块没有自有RT或逐体素Update。全局已加载Material为1089/1113/1125，不等于模块自有对象；全局RT在A首阶段19、后续20，变化在B之前已发生，不冒作体素RT增量。

GPU耗时未测。没有穷举Mixed/Subtractive、Clustered、所有平台及未来Normal/Alpha/Detail/Emission非默认关键词。当前12份作者材质均无局部关键词，本轮Player验证覆盖当前主场默认组和运行时uniform A/B/C；未来新关键词需补构建保留和目标Player验证。

## 存档与过程记录

Player仅在显式-voxel-light-smoke下，于sceneLoaded、库存Start前把真实CommerceService定向到报告目录isolated-commerce，profileId不变。复制4个源文件，不覆盖、不写回原目录，退出沿隔离Store。实际Store路径、Player内部前后SHA、外部紧贴最终启动前后的4文件SHA均相同。

较早整轮基线与最终原档不是同一字节：Editor原Play退出的最后保存发生于23:40，早于首个Player日志23:57。没有将Editor正常写入伪称Player破坏，也没有回滚用户进度；两个隔离Player各自前后原档不变。

首次构建曾正常取消（165.109秒、Shader13/96）；之后复用缓存完成。普通环境合同误用GetGlobalInt读整数槽、隔离合同直接比较混合斜杠路径，均经Native实证纠正，没有为测试错误改生产逻辑。MCP构建调用超时后只观察持久结果，没有重复执行同一回调。

场景、作者资产、媒体、原档、二进制仅本机；R11只定向发布源码、Unity源码meta、重放候选与文档，其他dirty保留。
