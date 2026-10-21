# 伙伴候选契约归档（2026-10-21）

用户批准将ICompanionMoveCandidate、ICompanionActionCandidate从CompanionCombatBrain.cs移入CharacterContracts.cs，与角色其他契约集中描述。

仅声明位置与中文说明变化；完整类型名、方法签名、实现、调用时序及Unity脚本GUID保持。CombatVitals类型与OnAccepted协议未在本轮重构。动画退出策略属于后续计划。

验证：Unity prepare_editor刷新至Edit ready；缓存近120秒无Error；已加载程序集反射确认Brain实现两个接口，TryGetCombatMove/TryGetAttackTarget/OnAccepted均存在。当前ROADMAP02场景clean，未修改场景对象；git diff --check通过。此变更不新增行为测试。

小蓝已有本地Spine导出位于Origin/work/AL-01/assets/blue，unit.json、unit.atlas、小蓝.png共3文件SHA256逐项匹配AL01-asset-manifest.json。导出版本4.1.23；这是运行时资源，不是Spine编辑工程源文件。未重新提取、未导入Unity、未上传第三方资源。

按R11仅发布本轮两份自有源码与本记录至GitHub OSSASTRA/main；用户旧禁编asmdef/meta本机删除保留未提交。
