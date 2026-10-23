# 输入与无主动暂停修正 2026-10-23
- 删除CharacterInputFrame的玩家pause/cancel字段、Root消费分支、手动SetPaused入口与开发重置调用；删除用户注释残留。保留系统挂起/失焦与内部动作打断，不提供玩家暂停键。
- 恢复InventoryCaptured分支返回，保证开包战斗输入捕获。
- OffhandAction为副手通用操作，保留并增加Inspector说明；具体行为由装备SO绑定。
- 原因：用户正编辑PlayerCharacter.prefab阶段，Dodge=space/buttonWest；B场两个测试实体为独立序列化配置，仍是leftShift。通过Unity API将两实体Dodge同步为当前用户配置，保存仅B场；未保存或关闭用户Prefab阶段。
- Unity刷新编译ready；两实体实际绑定回读匹配。内存InputSystem探针首次space=false（编辑态新组件未启用移动Action，未满足Read门禁），该探针不作为玩法通过证据；完整Play键鼠/实体闪避未完成验证。
- 以后修改Prefab不会自动传播到这两个独立测试实体；需要后续把输入定义统一为显式共享配置，当前仅修复本次绑定差异，不能声称任意外部配置已自动联动。
- 旧历史测试的SetPaused调用属于旧合同，后续运行前需适配无玩家暂停规则。
