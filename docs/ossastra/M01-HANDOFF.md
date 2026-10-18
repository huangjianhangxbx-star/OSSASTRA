# M01 接手状态 — 2026-10-19

用户明确指令：“执行推送吧，这样方便chat那边也消息互通”。此发布是候选共享，不是架构审查签字。所有代码审查区间仍待审，不自动开始M02。

## 移植进度

已接通单个rozeul验证角色：输入→移动/Idle/Run→单次普攻请求/接受→释放→真实空间命中→唯一HP扣减和反馈；具有MoveReady/AttackReady分离、具名取消、owner权限、暂停/失焦/禁启Held清理。六个领域C#加六个Unity适配器。时序、伤害等为临时配置。

尚未完整移植原型小黄/小蓝的角色差异、连段、技能/被动、资源、装备和队友AI。当前不是两个原型角色的完整复刻。

## 证据与审查

- 本次发布前核心测试复跑29/29通过；源码与上一轮验收包逐项SHA一致。
- 上轮Unity场景受控自动化14/14通过，44步骤，虚拟InputSystem设备和显式生命周期注入；不等于物理键鼠手感认证。
- 暂停时Spine继续播放的实际缺陷已修复并验证。旧备用RendererFeatures OnValidate错误保留。
- [实际接线与限制](M01-scene-review.md)、[逐行审查账本](M01-user-review-ledger.md)、[核心权威表](M01-core-authority.md)。建议从M01CharacterRoot沿实际调用链审查。
- [场景自动化原始结果](evidence/M01-integration-sweep.json)；[核心复跑日志](evidence/M01-core-tests.txt)。

## 发布场景与本机现场

本机SC_M01_CharacterTest.unity在上次验收后已变化。本次仅将审查ZIP中的已验收场景快照写入Git索引，不覆盖当前磁盘现场。已发布场景SHA256：91A9D7A8D624AF08267CF762945F798C59160739C4BF4F737A45DECFB44E52E4；发布前本机场景SHA256：CD66340175F84DE6705B2D67C28AA5A193B66C01E31851CEC32F507F91B7E159。

因此发布后本机场景可能显示为未提交修改，这是保留现场的预期结果。不要git reset/checkout覆盖。场景快照依赖不随仓库分发的用户FBX/Spine和插件，不能直接在干净克隆试玩；见根DEPENDENCIES.md。

## 同步方式

GitHub提供可读取的源码与持久交接文档，不会自动向另一个Chat对话注入消息。让另一边读取本仓库README和本文件即可核对当前进度。没有向其他对话发送消息；没有同步Gitee。