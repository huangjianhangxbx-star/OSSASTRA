# Inspector 输入配置修正（2026-10-23）
- 状态：complete；read-only，全部租约释放。
- 用户要求：直接在角色Inspector配置组合；暴露既有场景输入；右手RT/左手LT。
- 实现：CharacterInputHandlerEditor在共享SO存在时内联绘制19个实际Action，隐藏会被覆盖的本地动作；保留本角色F1-F4开发配置。共享引用为空仍显示原组件本地配置。
- 在Inspector内提示组合创建：Action的+ → Button With One Modifier Composite，展开Modifier/Button；LB+X为leftShoulder+buttonWest。
- 共享SO原生回读：右手Mouse leftButton+Gamepad rightTrigger；左手Mouse rightButton+Gamepad leftTrigger。实际CustomEditor类型命中，19 Action公开字段可绘制；Unity重载编译ready。
- 现有用户修改已移除handler中的triggerChordGraceSeconds/宽限逻辑；只清理SO无效字段和引用使本机编译，不恢复用户删除，不提交用户handler改动。旧26项输入合同是上一产品证据，不作为当前本地逻辑的重测声明。
- 场景清理继续暂停；不改旧场移动/删除、用户角色控制编辑、HUD等其他现场。
