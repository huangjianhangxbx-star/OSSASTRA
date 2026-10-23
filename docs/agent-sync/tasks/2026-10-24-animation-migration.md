# AM01–AM03 连续实施
- 日期：2026-10-24；状态active；唯一unity-writer。
- 规格：E:/迅雷下载/OSSASTRA_仲裁器与动画系统迁移计划_AM01-AM03_v1.0.md，用户明确顺序连续授权三轮，各轮验证后独立GitHub提交并报告。
- 基线19625fc；当前B综合场Edit clean，用户VELGITA Prefab stage clean，均不擅自保存用户现场。
- 保护：旧场资源移动/删除、CharacterVerticalSlice、CharacterEquipmentView、CharacterInputHandler用户删除宽限、CharacterDebugControls、PlayerInputLayout.asset现有修改、新Action SO。相关输入后续AM03仅在当前用户版本上增量修改并区分提交，不恢复删除。
- 租约：正式Characters/Animation必要集成、相关UI/Inventory输入、选定新动画配置、B综合场自有必要装配、Candidates/AM01-AM03、AM交接/规则索引/本任务同步。不改旧参考、源商业媒体、第三方或用户开发细则。
- AM01步骤：[ ] 权威/时序表和RED；[ ] 注册仲裁器、移动/战斗/并行状态；[ ] 迁移实际动作状态/Run推进/取消；[ ] B综合场与模块验证；[ ] 注释复核/交接/定向发布。
- AM02步骤：[ ] 原类/枚举/配置迁移对应；[ ] 原路由覆盖合成/过渡/播放/事件；[ ] 身体与左右装备同时切换；[ ] 原式SO实际配置/保存回读/真实Spine验证；[ ] 单轮发布。
- AM03步骤：[ ] 玩家会话分通道来源及分设备采样；[ ] 方向/落点统一；[ ] UI按下归属及生命周期；[ ] 模拟设备场景流程/硬件状态说明；[ ] 定向退役/发布。
- 已知限制：ChatGPT链接登录墙，附件为完整规格；现存Player构建旧Favorites插件错误不扩展修复；旧用户角色销毁MissingReference需若在本轮取消/订阅路径复现则处理。

## AM01 实施进展
- 战斗运行态/连段/Request/Rephase/具名取消/释放分派已实际迁到 CharacterCombatStateMachine；集中StateArbitrator+Movement/Dodge/Ability/WeaponPosture正式调度，事务协调器查询状态替换/并行许可。
- Unity编译通过；自然输入17、并行5、生命周期13、纯合同68通过。B/候选Prefab装配由Editor保存，用户VELGITA stage clean保持。
- 退出Play复现旧InventoryView销毁引用异常；最小回归RED→GREEN，修复释放捕获的存活端口遍历。当前补跑正常背包输入流程。
- 旧A并行夹具与B库存不同、生命周期墙钟断言失配，两次夹具失败已保存并订正，不掩盖。

- AM01最终：正常背包47/47，销毁回归GREEN，最终退出日志新增范围无MissingReference/Exception；准备独立发布，继续AM02。
