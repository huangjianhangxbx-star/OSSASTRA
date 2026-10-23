# AM03 混合输入与最终收口

基线：AM02 `67efa5c1f1046f1335dc95469ceb4afc469f962a`。交互规则采用本轮计划的默认取舍，不把实现细节追记为用户历史要求。正常入口仍为 `Assets/OSSASTRA/Scenes/SC_VC02B_IntegrationReady.unity`，没有新增永久测试世界。

## 来源、操作与写入边界

`CharacterControlCoordinator.InputSources` 持有同一玩家的 `PlayerInputSourceState`，经 `IPlayerInputSourceSession` 给当前角色读取。移动设备和战斗瞄准设备独立；未控角色不消费设备，不抢来源。连接状态、指针位置、控制身份和来源记忆分开，来源改变不递增 ControlRevision、不 ResetPlayerInput、不重播动画或取消已接受动作。

`DeviceActionSampler` 从当前 InputAction 复制完整 bindings/composites、interactions、processors、groups、override 和 mask，用每个 Map.devices 限定设备。原 Action 仍是唯一作者配置，没有隐藏 WASD／按钮表。克隆只用于采样，不派发第二次 UI 点击或玩法请求。增删设备仅退休对应样本，不重建另一设备的长按。配置改变才重建样本并等释放。

| 用途 | 接管与输出 |
| --- | --- |
| 移动 | 新有效按下／明显轴变化接管，保持值不反复抢回；不相加，普通回中可回落到另一合法保持 |
| 瞄准 | 新右杆方向或有方向用途的手柄战斗边沿接管；鼠标世界战斗边沿当帧接管；普通键盘新移动可建立键鼠探索上下文 |
| 指针／辅助 | 初始指针、纯鼠标移动、UI操作、热栏／交互／键盘技能不凭设备类型抢战斗瞄准 |
| 无新瞄准 | 保留来源；手柄使用合法右杆、移动方向或最后方向；静止鼠标不自动复活 |
| 目标 | 鼠标所有者必须有合法世界射线；手柄方向目标按本次执行器 AimRange 或投递 range 构建，不猜统一距离 |
| 同帧／组合 | 各设备同一逻辑边沿合并为一份角色帧；配置 composite 只消费同设备的基础按钮，其他逻辑是否并行仍由 AM01 决定 |
| 恢复 | 失焦／死亡／模态恢复要求所有设备真实中立；原普通切人保留移动语义，攻击仍须释放后重武装 |

CharacterInputHandler Inspector 暴露处理后轴门槛、明确方向变化门槛及运行时来源设备。已使用配置中的 processor，不再额外做一遍 deadzone。当前数值不是实体手柄漂移调优结论。

## 方向与目标入口对账

| 正式入口 | AM03结果 |
| --- | --- |
| Handler.Read→Frame.move/aimSource/aimDirection/hasPointer | UI分类后解析同一会话来源，再输出唯一帧 |
| MovementStateMachine→CharacterOrientation | 读取已筛选帧，鼠标点与方向输入不再同时竞争；仍保留键盘移动的可见翻转 |
| CharacterRoot.PointerDirection/PointerGroundPoint | 仅在该帧鼠标拥有合法点时投影，UI背后的点不合法 |
| CharacterRoot.AbilityAimPoint | 按来源分支；无条件读取鼠标的旁路已移除 |
| CharacterAbilities.AcceptCompiled | 点选／抛物线使用实际 executor/投递范围；原合法性、地面、遮挡和资源事务继续复验 |
| AbilityRunContext.direction/landing | 原接受时冻结策略不变；切输入源不会追改飞行中的目标 |
| 旧 WeaponPostureStateMachine 兼容端口 | 继续使用已筛选 hasPointer，正式能力存在时仍互斥；不新造旧副手的统一射程 |
| InventoryInteraction | 相同配置按设备采样，已消费组合键不会挡住另一设备F，也不增加交互请求次数 |

## UI、长按与生命周期

Pointer 初始按下在 InputSystem 回调中使用当时位置进行 GraphicRaycaster／已有 IMGUI区域判断，并锁住该次手势的 UI 归属直到释放。拖出不会变世界攻击，关闭按钮的点击不会穿透；没有鼠标唤醒首击或强制隐藏指针计时器。

普通背包捕获新战斗申请，允许移动；数量文本额外捕获移动；菜单继续走既有模态捕获，不暂停世界。实际启用的 UI 导航按选中对象、UI move Action 与该设备的重叠控制分配，并等移动回中后恢复世界移动。静止鼠标在 UI 上不阻断合法手柄方向。

补充 RED→GREEN 修复：背包开关会改变访问校验 ControlRevision，旧 AbilityGesture 将其误当成松键／换控，提前终止举盾。现在 CharacterInputHandler.GestureGeneration 只在真实 ResetPlayerInput 时变化，能力手势使用该代际；背包访问校验仍保留。捕获期间仅延续先前已开始的合法保持，新按下被消费且不会在关包后补发；真正释放仍结束原 Run。

用户删除的双扳机宽限没有恢复。旧依赖合并 activeControl 的 GamepadInputPolicy 已无调用并退役；组合规则读取原配置。键位资产的用户未提交修改保持原样，本轮不将其偷偷替换或上传。

## 验证证据

证据与可重跑夹具见 `Candidates/AM03`，均使用同一个真实 B 综合场及实际 Input System／Spine。虚拟设备不会冒充实体手柄。

- 配置与来源合同24/24：真实分设备Action读取、组合／processor保留、独立移动、接管／保持／回落、UI-down锁定、断开仅退休本设备、释放重武装。
- 综合连续流程最终36/36：手柄探索／首击→鼠标首击→辅助键→移动交替→实际背包关闭点击→切人→失焦恢复；来源变化不替换动作身份。
- 原自然输入17/17；身体／双手9/9；举盾开关背包补充13/13；生命周期最终13/13。
- 原生E真实目标7/7：无鼠标所有权时按执行器6米范围生成，飞行中键盘接管不改冻结落点，真实运动和85伤害结算；只有一份接受。
- 真实背包输入20/20，拖放中加入虚拟手柄输入再20/20：原物品移动与数量不变，无点击穿透或拖拽丢失。
- 作者工具补漏：角色不再被迫引用隐藏旧动画Profile；正式Mapping支持片段检查，动作导出用typed逻辑ID。有效角色／有效映射／非法片段三项RED→GREEN。

失败分类保留：Edit环境用Dynamic Update的输入夹具不成立，改在Play验证；首次综合夹具的F实际打开容器，战斗捕获正确，改用不打开面板的辅助键继续；原生E初版把目标放在边界上并要求角色中心进入敌胶囊，改为边界内合法路径、按真实碰撞截断核对。没有以修改产品伤害／碰撞来迎合这些错误夹具。

## 保留项与使用入口

实体手柄实机自然接续、漂移和手感未测试；完整手柄背包、种田、快捷投掷流程没有扩展。旧 Favorites 插件的 Player 构建阻塞仍保留。装备美术仍有探针素材、rozeul缺专用跑步起停片段等限制见 AM02；不宣称正式美术已完成。

配置输入仍打开角色 Inspector 内联共享 PlayerInputLayout；配置动画仍使用 AM02 原式 Mapping／Override／Router。源脚本在专门 Scripts 文件夹，接口在 CharacterContracts。历史场、用户媒体、未提交键位与用户开发细则没有纳入本轮清理。

最终现场：B综合场Edit/clean，用户VELGITA Prefab Stage clean、未保存用户Prefab，Missing Script为0；A/B各自映射保持。真实启动读取两身体、四装备、敌人`_stand`与NPC`idle1_1`，Skeleton与Renderer均有效。测试虚拟设备均已移除，原InputSettings资产复原；检测到既有XInput设备并保留，但未对其做实体操控实测。最终新增日志范围未检出MissingReference/NullReference/CS编译错误，见final-log-check.json。
