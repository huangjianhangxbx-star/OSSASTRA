# VC02A 交接

本轮完成A0–A8的候选技术闭环；人工手感、架构审阅和正式美术准入仍待后续。ART_ENTRY_READY=false。没有执行VC02B。

入口：Assets/OSSASTRA/Scenes/SC_VC02A_CombatMigration.unity。默认两人四段；右手面板选原型I/II、配置Q/R和试验来源，E固定；左手面板切换两盾、射击器、投掷装备与弹仓射击器。默认图像保持rozeul，独立盾为原创Spine技术探针。

交接接口：CharacterRoot唯一动作钟/接受，CharacterMainAction冻结主手动作组，CharacterAbilities通用绑定/Run/来源，EquipmentAbilityState稳定实例资源，CombatDeliveryQueue/ContactGateway中立结算，CombatIceWorld与CombatStatusEffects中立持续效果，EquipmentSpinePresentationSO与CharacterEquipmentSpineView装备表现，原HUD窄端口/事实反馈。详细所有权、时序和差异见architecture；54行来源覆盖见migration-coverage；24合同组见verification。

保持用户现场：SC_CharacterVerticalSlice的命中层改动、CharacterEquipmentView行尾变化、旧禁编asmdef/meta删除保留在工作区，不纳入本任务提交。不修改用户维护区、开发细则、原型、商业媒体、插件或Gitee。角色默认与原型动作组分开，换槽不复制资源，E与别名共享规范CD。

下一步可以基于本轮代码进行用户架构审查；VC02B详细计划已有，但本任务明确停止A，未代替用户审定正式标准。

GitHub main发布SHA以实际提交/远端结果为准；产品SHA256清单为VC02A-source-manifest.tsv。2026-10-23用户已取消完成后关机要求，不执行关机。
