# DS-MAP-01 验证夹具

通过本项目Funplay execute_code执行.cs.txt，不会编入正式运行程序集。先prepare_editor并核对ready。

- ViewSpaceContracts、ViewSpaceLifecycleContracts、RootFrameHistoryContracts、OrbitContracts、PropClockContracts：Edit的自有瞬态附加场执行，完成后只关闭该瞬态场，不保存原场。
- CameraInputContracts：自有研究场Play执行；仅建立自己的虚拟Mouse/Gamepad，finally精确清理并恢复原current，后置清空MapRoot.cameraInput待处理滚轮。不得在原正式场中运行。
- LabMovementPlay、LabCombatPlay、LabLifecyclePlay、FinalMotionPlay、EditorPerformancePlay：自有研究场Play中的限时、具名临时GO/coroutine。用真实InputSystem事件让PlayerLoop采样，不手工调用InputSystem.Update。Json/Done字段通过只读反射查询。夹具会受控安排初始位置，不把此安排算作真实走动验收；步进/攻击/闪避均经原控制链。
- OriginalSmokePlay：仅用于已确认无用户编辑的SC_CommerceIntegration；保留原角色/绑定，不安装ViewSpace模块，不保存该场。

退出Play会清理所有临时GO。证据保留实际失败：LabCombatPlay的“按住自动四段”预期不符合既有点击连段规则，48通过/1失败不能记成整夹具49通过；LabLifecyclePlay随后以四次真实点击完成四段，31/31。Edit显式Suspend与真实Play禁启分别记录。

外部StaticCompile.csproj、游戏媒体、原作脚本、图集帧表、截图/录像不随此目录公开。性能数字为Editor GameView采样，不能换算为独立Player最终帧率。
