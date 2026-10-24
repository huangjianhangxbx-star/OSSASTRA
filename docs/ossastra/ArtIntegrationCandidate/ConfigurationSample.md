# 一页操作样板：A / rozeul / 主手动作组 / 两盾 / 身体技能

适用当前B综合场：`Assets/OSSASTRA/Scenes/SC_VC02B_IntegrationReady.unity`。日期2026-10-24，CONFIG-01。路径前缀：动画目录=`Assets/OSSASTRA/Configs/Animation/`，能力目录=`Assets/OSSASTRA/Configs/Abilities/`，实例定义目录=`Assets/OSSASTRA/VC02B/`。先看[当前配置链与四张关系表](README.md)。所有样板数值可调整，未形成用户永久标准。

1. **找实际配置。** Edit选`临时验证角色_A`根，展开“身体动画与配置”，按“打开基础映射”。应打开动画目录`temporary-a_VC02_Animation_Mapping.asset`；B应为`temporary-b_VC02_Animation_Mapping.asset`。旁边显示场景覆盖与Prefab默认`Rozeul_Animation_Mapping.asset`。四按钮可返回身体节点、共享`CharacterLogicRouter.asset`、`Rozeul_RigProfile.asset`。不要把Prefab默认误作A当前值。
2. **找到普攻。** 主手定义`Equipment_temporary-a.vc02.main.asset.mainActionProfiles[0]`→能力目录`MainProfiles/hunter_I.asset.stages[0].action`→`hunter_A1.asset.animationLogicId=Attack_Normal_1`。Router当前同名规则输出同ID，再由A Map的`plansMelee`同ID计划播放`atk1`。动作时点改动作SO；混合和速率改Map；仅需要按来源/条件选不同逻辑ID时才改Router。普攻II的动作是hunter_II_A3/A4，勿改错。
3. **可撤回调整。** 先记下A Map `plansMelee[Attack_Normal_1].plan.clips[0].mixDuration=0.08`，暂改`0.12`并保存。下次普攻接受时读取新计划；已经开始的动作仍用旧快照，已预冻结阶段也不重新取SO。检查后恢复`0.08`保存。不要只改隐藏的animationKey/旧Profile。本轮确实完成0.08→0.12保存、重新导入回读、恢复，资产与meta字节一致；另在运行副本验证当次旧mix/下次新mix。
4. **正常操作。** Play：手柄LT+RT切到`hunter.prototype.I`，松开后RT普攻；键鼠普通攻击为鼠标左键。Q用当前`hunter-break`身体技能，身体为atk3，右手装备与身体具有同一次执行ID。LT/鼠标右键按住当前盾A防御，松开停止。Tab/Start开背包，靠近箱子打开外部容器，把现有`vc02.shield-b`拖进A左手（自动交换旧盾），关包并松开旧输入，再按LT/右键：现在是盾击，不再是持续防御。没有授予新道具；从同一会话真实箱内实例换装。
5. **区分三个装备配置。** `Equipment_vc02.shield-b.asset.abilityBindings[0]`是Secondary/Press→`ShieldB_PressAttack.asset`，实际能力→`ShieldAttack_Action.asset`，默认HeavyAttack→A Map的heavy。`bodyAnimationOverride`当前为空；要做装备专属身体替换，应在该装备字段挂`AnimationPlanOverrideRuleSetSO`，写目标ID/条件/替换计划，不改其他角色共用身体Map。独立装备则走`spinePresentation=EquipmentSpine_Left_PROBE.asset`→`animationMap=EquipmentSpine_Left_PROBE_Mapping.asset`，其Ability→strike；左右有独立轨道/状态。副手身体准备携带OffHand和左手实际装备来源，不因“是身体动画”改记右手。
6. **看握持。** 根打开Rig：`equipment.left`是SlotBone `Z-Z_Lhand1`关联`Lhand`，右手`Z-Rhand1`关联`Rhand`。Left_PROBE的内部骨名`grip`；当前localOffset(0,0,0)、scale1.2785964、rotation0、orderOffset3，右手orderOffset4。Scene工作台可显示已绑定挂点，Play根面板可定位实际装备表现SO。颜色/旋转/附件显隐分别配置，当前不是身体slot内部穿插。PROBE装备是技术样板；本轮未改源模型、未指定正式握持审美。

|主题|当前实现值|建议调整|用户确认|
|---|---|---|---|
|A第一段|Attack_Normal_1→atk1；track0、非循环、mix0.08、rate1|先比较0.08/0.12混合，再结合正式片段调节|待审|
|hunter_A1时点|Release0.0333 / MoveReady0.2333 / AttackReady0.2 / Finished0.7333秒|按招式设计调整合同，不能仅靠播放速度改变解锁|待审|
|Q身体技能|hunter-break→Attack_Normal_3→atk3；当前实验技能|保留逻辑链，后续按正式技能语义分配片段/效果|待审|
|盾A / 盾B|Hold防御 / Press盾击；同输入、不同SO绑定|按具体装备确定Hold/Tap/LongPress及费用|待审|
|副手身体覆盖|shield-b.bodyAnimationOverride为空|需要装备专属姿态时挂Override；当前无需新造资产|待审|
|握持与排序|真实SlotBone+grip；左右orderOffset3/4|正式装备替换Spine资产后调握持；若需slot穿插另审方案|待审|
|正背与方向|当前rozeul动画+翻转；无本轮确认的独立背面规范|以正式素材实有内容决定路由|待审|
|状态组织|注册五机；Enter/Exit观察边沿；部分子状态由事实推导|具体子状态需行为时逐模块设计，勿复制另一份Runtime|待审|
|手柄|配置动作与虚拟InputSystem流程通过|真人手柄探索→键鼠→战斗连续体验后调参|待审，未测实体手感|

本轮验证边界：导航A/B四入口调用及选择回读13项；保存/恢复和Prefab缺绑定5项；小流程19步（含等待/释放步骤）。输入用隔离的虚拟InputSystem设备，uGUI换装走实际Pointer/Drag处理器与EventSystem命中，未直接调用技能请求或伪造库存。为稳定观察关闭了测试会话威胁/伙伴策略并恢复体力，靠箱子使用测试传送；不冒称真人硬件或无辅助实战。当前动作快照验证使用运行时Map副本，保存回读验证使用A真实Map；两者分别记录，未遗留永久配置修改。

证据：[导航](../../../Candidates/CONFIG-01/Evidence/navigation-green.json)、[保存恢复](../../../Candidates/CONFIG-01/Evidence/configuration-edit.json)、[小流程](../../../Candidates/CONFIG-01/Evidence/configuration-story.json)。功能已能从根定位和逐项试配；美术效果、具体架构认可和数值均由用户随后审查，不需要先补整套正式资产才能开始。
