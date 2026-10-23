# AM01–AM03 连续实施
- 日期：2026-10-24；状态complete；全部Unity/源码/文档/发布租约释放。
- 规格：E:/迅雷下载/OSSASTRA_仲裁器与动画系统迁移计划_AM01-AM03_v1.0.md，用户明确顺序连续授权三轮，各轮验证后独立GitHub提交并报告。
- 基线19625fc；当前B综合场Edit clean，用户VELGITA Prefab stage clean，均不擅自保存用户现场。
- 保护：旧场资源移动/删除、CharacterVerticalSlice、CharacterEquipmentView、CharacterInputHandler用户删除宽限、CharacterDebugControls、PlayerInputLayout.asset现有修改、新Action SO。相关输入后续AM03仅在当前用户版本上增量修改并区分提交，不恢复删除。
- 租约：正式Characters/Animation必要集成、相关UI/Inventory输入、选定新动画配置、B综合场自有必要装配、Candidates/AM01-AM03、AM交接/规则索引/本任务同步。不改旧参考、源商业媒体、第三方或用户开发细则。
- AM01步骤：[x] 权威/时序表和RED；[x] 注册仲裁器、移动/战斗/并行状态；[x] 迁移实际动作状态/Run推进/取消；[x] B综合场与模块验证；[x] 注释复核/交接/定向发布。
- AM02步骤：[x] 原类/枚举/配置迁移对应；[x] 原路由覆盖合成/过渡/播放/事件；[x] 身体与左右装备同时切换；[x] 原式SO实际配置/保存回读/真实Spine验证；[x] 单轮发布。
- AM03步骤：[x] 玩家会话分通道来源及分设备采样；[x] 方向/落点统一；[x] UI按下归属及生命周期；[x] 模拟设备场景流程/硬件状态说明；[x] 定向退役/发布。
- 已知限制：ChatGPT链接登录墙，附件为完整规格；现存Player构建旧Favorites插件错误不扩展修复；旧用户角色销毁MissingReference需若在本轮取消/订阅路径复现则处理。

## AM01 实施进展
- 战斗运行态/连段/Request/Rephase/具名取消/释放分派已实际迁到 CharacterCombatStateMachine；集中StateArbitrator+Movement/Dodge/Ability/WeaponPosture正式调度，事务协调器查询状态替换/并行许可。
- Unity编译通过；自然输入17、并行5、生命周期13、纯合同68通过。B/候选Prefab装配由Editor保存，用户VELGITA stage clean保持。
- 退出Play复现旧InventoryView销毁引用异常；最小回归RED→GREEN，修复释放捕获的存活端口遍历。当前补跑正常背包输入流程。
- 旧A并行夹具与B库存不同、生命周期墙钟断言失配，两次夹具失败已保存并订正，不掩盖。

- AM01最终：正常背包47/47，销毁回归GREEN，最终退出日志新增范围无MissingReference/Exception；准备独立发布，继续AM02。
- AM01已发布ed74ae1af1698306cd44f5fdb7a871dbc1bbf3b7，GitHub main精确SHA核对；AM02开始，只读旧参考与原式SO链盘点中。
- 最新用户明确指令：整轮AM01-AM03完成、验证并推送GitHub后关机；覆盖此前“不关机”。未完成前不得执行关机。

## AM02 当前进展
- 正式Ossastra.CharacterAnimation原式SO/完整枚举/来源契约已迁入；原参考/GUID不变。配置合同9/9、真实Spine播放16/16通过。
- B两角色、候选Prefab已由Editor切为CharacterAnimationController→Composer→Handle，身体和左右装备均用Handle；实际旧视图0。当前三角色映射/四装备映射/路由共8个新SO唯一GUID。
- 实际操作回归17/17，六技能25/25（含原多阶段动作、真实移动/HP/费用/单ActionId）。正在补冻结/左右手独立/幂等资产和生命周期验证，未发布AM02。
- 材料事实：rozeul有ready_start/loop/end，已接Guard架势进入/退出；无专用run_start/end，不用无关片段伪造跑步起停。共享NPC/Enemy动画模块保留。
- 首次资产迁移在默认Prefab无角色Definition处明确失败，已读回确认Prefab未改后适配显式Rozeul默认映射并重跑；A/B映射文件按源路径名区分（源SO内部name相同）。生产当前装配已成功，不宣称首次一次通过。

- AM02收口：保存重开A/B独立映射、旧视图0/旧孤立覆盖0；六技能final25/25、生命周期final13/13、真实Spine重建/启停扩展22/22通过。Odin Editor实际类型已核对，身份与94片段映射表已输出。AM02准备定向发布，继续AM03。

## AM03 设计与当前验证
- AM02独立发布67efa5c1f1046f1335dc95469ceb4afc469f962a，GitHub main精确核对。
- 不另建全局输入服务。控制会话持有PlayerInputSourceState，经IPlayerInputSourceSession给角色；移动/战斗来源独立，不递增控制代际。
- DeviceActionSampler从当前Action复制完整binding/composite/processors/interactions/override，以Map.devices限制真实设备；原Action保持作者配置。增删设备只增删该设备样本，配置变化才重建。
- 新按下/有意义轴变化提出接管；保持不抢回；同级按Action事件time及当前来源稳定裁决。普通移动允许回落，死亡/失焦/模态恢复要求全设备真实中立。
- Pointer在InputSystem初始按下回调中通过当时GraphicRaycaster/原IMGUI区域判UI归属，直到释放保留；不靠上一帧hover。手柄不受静止鼠标位置门禁。
- 原AbilityAimPoint无条件鼠标旁路改为当前来源；方向型目标使用该次真实executor AimRange/投递range，不追改已接受Run的direction/landing。
- 首次采样夹具在Edit模式用Dynamic InputSystem.Update导致设备断言失败，未当产品缺陷；Play+原有背景输入设置的实际采样24/24通过。夹具移除设备后再次发送的错误已修正，初始证据保留。
- 原自然操作17/17通过。正在跑综合场键鼠/虚拟手柄/UI连续流程。没有实体手柄实测，不据模拟器宣称实机通过。
- AM03最终行为：综合连续36/36，真实原生E目标7/7，普通背包20/20、拖拽中加入手柄20/20，来源24/24，普通操作17/17、双手9/9、生命周期13/13。
- 开包误终止举盾真实RED后修复：分离GestureGeneration与背包访问ControlRevision，补充实际举盾开关包13/13 GREEN。真正换控/失焦/模态仍Reset并释放保护；不删库存访问校验。
- 最后作者工具发现隐藏旧animationProfile仍被验证器强制要求，正式Map无验证；三个作者合同RED→GREEN，去旧Profile要求、正式Map检查、导出typed逻辑ID/schema2。本轮仅工具校验补漏，无新玩法。
- 夹具失败保留：F打开实际容器导致后续战斗被合法捕获；目标初版落在边界且要求穿入敌胶囊。改夹具用途/合法位置，不改产品碰撞或伤害。实体手柄未测，旧Player第三方构建阻塞未扩修。

## 最终发布
- AM01 ed74ae1af1698306cd44f5fdb7a871dbc1bbf3b7；AM02 67efa5c1f1046f1335dc95469ceb4afc469f962a；AM03 faf2938e8434993d1a9a4dd1c542d988e464b65b，均已推送GitHub OSSASTRA/main并精确核对远端SHA。
- 技术计划已完成，人工架构/手感审查仍待用户。旧Player第三方构建问题、实体操控未测与探针美术等边界见交接，不扩展本轮。
- 最终B Edit clean、用户VELGITA stage clean、Missing0、测试虚拟设备0，原InputSettings及既有XInput设备保留。受保护用户dirty保留未打包。
- 用户最新明确要求：完成后关机；下一步安排延迟正常关机，不使用强制关闭参数。
