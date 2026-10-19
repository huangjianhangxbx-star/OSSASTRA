# M02-C 验证记录（候选，2026-10-20）

基准8925b76；用户v1.0计划SHA256：1E6F45D8C3E514F1B17B13CC56ACA43E8B2E3354962D11DA84DCA32DDD2012FC。验证发生在本机工作树，既有旧禁编asmdef/meta删除保留且不发布；不是隔离的完整工程认证。Chat链接只取得用户消息与正文引用占位，以完整附件和真实源码为实施依据。

## 分项结果
| 检查 | 结果 | 原始证据 |
|---|---|---|
| 纯跟随/源选择策略 | 初始6/13，最终13/13 | evidence/M02C/policy-red.txt、policy-final.txt |
| 既有动作领域核心 | 29/29 | core29-final.txt |
| 旧双角色场不跟随RED | 水平不跟随；约.01竖直贴地不算跟随 | world-red.json |
| 新独立场实际输入/物理/动作/切换 | 最终19/19 | world-final.json，第二条日志为完整结果 |
| 选择器实际接口替身合同 | 最终13/13 | selector-final.json |
| 原玩家来源owner冲突 | 1/1 | owner-conflict-final.json |
| 失败的第二会话不得借Director抢来源 | RED→1/1 | invalid-director-red.json、invalid-director-final.json |
| 无Director旧场切换当帧外层有效帧清零 | RED→1/1 | legacy-frame-red.json、legacy-frame-final.json |
| 既有M02A真实世界回归 | 26/26 | m02a-final.json |
| 既有M01单角色真实世界回归 | 最终15/15 | m01-final.json |
| 7个指定保护资产 | SHA全部一致 | protected-final.json |
| 最终编辑器审计 | Edit clean、角色2、每角色6引用齐、MissingScript0、Probe设备0、原InputSettings | edit-final.json |

各项独立，不合并成“全套总数”。M02A 26项、owner冲突与失败Director检查运行于最后一次有效玩家帧补丁之前；该补丁只增加可选ICharacterInputAvailability即时门控，随后旧场同帧RED/GREEN、M01 15项、新场19项和选择器13项在最终源码通过。没有把此前26项冒称最终源码重跑。

## 失败与修复
- world-first三项失败：测试夹具把第二靶胶囊放在跟随路径上，实际碰撞正确阻挡。仅修夹具位置和物理同步，没有削弱碰撞；world-fixed及最终world-final均19项通过。
- 真实发现失败的第二Coordinator仍可经Director占用角色来源：绑定前检查Coordinator启用状态，原来源保持。
- 真实发现无Director旧场切换后玩家原始帧已归零、外层Selector仍短暂保留：增加实际被消费的可选输入可用性接口，使外层同帧失效。
- 退出测试后的dirty标记先保留，用户明确确认未编辑后，才重新打开已保存的自有场景；reopen-approved保留回执。

## 覆盖与限制
场景检查覆盖：真实移动跟随、起停滞回、近距稳定、无自动攻击、双向切换、前摇MoveReady门控、已接受动作继续且单释放、Held松键再武装、失焦/暂停/禁用/无Current中立、Director启停、墙体阻挡与目标移动后重试、切换同帧屏障。测试通过虚拟Input System设备和生命周期注入，不等于用户物理键鼠体验或架构批准。

采用计划C级权威路径检查和B级成本控制：没有扫描所有旧场、没有Player构建、没有全项目认证；旧输入10项/Bus12项不因未改实现而机械全重跑。本轮未迁移连段、战斗AI、寻路、技能、装备或角色差异。墙移开而领队不动仍等待；领队移动达到重试阈值才恢复。当前仍是同Spine双临时实例。

源码审查仅本轮9个变化文件，完整行号和SHA见M02C-source-review及manifest；全部仍待用户审查。测试阈值不是永久用户规则。原M02A审查账本未改。