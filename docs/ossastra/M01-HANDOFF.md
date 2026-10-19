# 当前候选交接 — 2026-10-19

用户要求：没有已批准下一步计划时，推送当前成果，等待后续计划。本次发布只共享候选，不代表用户完成逐行架构审查，也不授权自动开始M02。

先读根[用户规则](../../OSSASTRA_RULES.md)、[角色职责与DataBus](character-foundation.md)、[待审账本](M01-user-review-ledger.md)，再读当前真实C#及Prefab关联。不要按历史M01逐行文档还原旧路径/旧命名。

## 已有实现
- 通用控制去M01命名，脚本在Assets/OSSASTRA/Scripts/Characters/Core、Runtime；开发靶在Scripts/Development。
- Root/InputAction输入/Movement/Orientation/Spine/Melee明确分工；每角色独立DataBus，只读访问原数据所有者，不另存动作时钟和HP。
- VELGITA实际绑定参考：移动WASD/左摇杆，鼠标位置/右摇杆分开，当前单普攻左键/右扳机。同步枚举器管理动作生命周期，无协程。
- PlayerCharacter.prefab及自有SC_M01_CharacterTest已接线；rozeul为验证素材，原型小黄/小蓝完整连段、技能、资源、装备与AI尚未迁完。
- 核心29、输入10、DataBus/朝向12、实机场景14合同通过。场景测试为虚拟InputSystem设备与显式生命周期回调，不能代替用户手感认证。物理CharacterController方案保持。

## 用户新指出的缺口：尚未实现
1. 键盘左右移动当前不能独立驱动可见翻转；Orientation目前仍主要跟随鼠标/手柄瞄准。显示翻转与真实攻击方向尚未分别建模。后续必须满足键盘也能翻转、实际攻击鼠标优先于键盘，不能把此缺口视作无关的早期简化。
2. 角色子系统当前用具体组件类型和显式Initialize方法连接，没有统一角色生命周期/更新接口。用户重视接口架构，下一计划应参考旧Root子系统约定，明确需要哪些接口、谁调度、谁读写；不能只批量创建没有实际作用的接口。
3. 架构所有修改区间仍待用户核查；下一阶段玩法与优先级尚无已批准计划。等待用户提供计划，不自行决定技能/AI等迁移顺序。

## 运行、资源和本机差异

公开仓库包含自有源码、测试命令源码、共享Prefab和验证场引用；没有Spine商用运行库、用户Spine媒体、target 1模型或完整Packages/渲染配置。干净克隆不能保证直接运行，见根DEPENDENCIES.md。

本机旧代码已按用户后续授权解除禁编，供查看旧Prefab配置；这不是当前新游戏的实现依赖。公开仓库仍保留PRE00历史隔离asmdef，因为旧代码不随仓库分发，本次不把整套旧代码/本机解禁改动上传。原目录的两个本机删除会保留为未提交差异，不要用git reset覆盖。

GitHub上的文件供另一边Chat读取，不会自动向其他对话发送消息。没有Gitee写入。