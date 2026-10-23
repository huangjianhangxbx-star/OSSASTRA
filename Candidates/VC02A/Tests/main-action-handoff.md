# A4 主手动作组接续（2026-10-23）

## 已落盘

- `MainActionSequence` 冻结组、连段参数、阶段几何/突进与来源组合。凿冰先替换第三段，迅猛前缀再替换两段；普通与强化保持来源优先级求值后的唯一消费者。
- `MainActionProfileSO` 持作者配置和语义动作 SO 引用。`CharacterMainAction` 持当前接受快照/投递，没有第二动作钟、第二体力/HP，也不直接写 Transform。
- `CharacterRoot` 继续唯一接受/付费与 `BasicActionRuntime`；空闲时才刷新组，接受时冻结动画与玩法。位移由根绝对时钟导出、经 DataBus 进入唯一 `CharacterMovement`。
- 同一 `CombatDeliveryQueue`/`CombatContactGateway` 结算扇区，动作有效窗 .1 秒（Hunter）/.15 秒（Ranger）；同目标同动作不会每帧重伤。
- 选择保存在 `EquipmentAbilityState.MainActionProfileId`，不重建装备实例或 CD/资源。正常右手配置窗口可选默认、原型 I、原型 II；卡片 I/II 来自 SO 的 `formLabel`。角色默认仍四段。
- 凿冰接受快照保留来源 ID，释放支付原 FrostReserveState 的 1 霜寒；不足则不投递、不生柱。柱沿 `CharacterAbilities.ScheduleIceColumn` 接原父身份，并另查延迟时来源仍持有。
- Root 的 OnDisable/Respawn 补 `CombatStatusEffects.Clear`，保留父代理已有 status / SpineView 接线。

## 冻结来源及明确差异

读取 `prototype-c21/game/src/core/hunter-combat-profile.ts`、`basic-definition.ts`、`main-form-spec.ts`、`trial-combo.ts`、`ability-trial.ts`、`basic-runtime.ts`。

- Hunter I 四段、Ranger I 三段；II 取 Hunter 原第三/四段、Ranger 原第二/三段，范围分别 +.25/+.2，ready/move 各 +.18，末段额外恢复 .4，架势系数 1.35。
- 默认候选四段与原型组独立；来源凿冰/迅猛只在明确选择支持这些替换的 Hunter I 中组合。II 保持自身两段 SAMPLE 规则。
- Ranger 有 dash 事件但源码普通攻击消费者未执行位移，所以配置距离为零，不根据事件名发明突进。
- 未找到“普攻首次命中 +1 霜寒”的源码合同；真实源码是 rush 每波首次有效命中 +2，凿冰释放 -1，被动 .8/s 且支付后延迟 2s。本模块未新增 +1。
- 动画只复用已有 rozeul 语义映射，属于 `PRESENTATION_PARTIAL`，没有写入源八方向动画片段名或修改旧模型片段。

## 验证与构筑

- `dotnet run --project Candidates/VC02A/Tests/MainActionProfile.Tests.csproj --no-restore`：10/10。拓扑/冻结8项与突进积分2项均有先红后绿记录。
- 由唯一 Unity writer 在自有 `SC_VC02A_CombatMigration` Edit 执行 `configure-main-profiles.cs.txt`：创建/更新独立 SO，给两个候选角色挂组件，默认动作不改。场景有 dirty 时先保存恢复副本。
- 首次构筑曾暴露 Unity 内联类 null 变空对象：`chisel.action==null && stageId空` 视未配置，半配置仍拒绝。rapid 数组中的空项仍拒绝。需保存重开后复验。
- Fresh Play 执行 `main-world.cs.txt`，结果 key `VC02_MAIN_ACTION`。约十秒，普通 HUD 按钮/普通 InputSystem 受理；场地和资源设置明确属于边界测试夹具，不能代替 A8 无注入自然故事。
- 第一轮实际失败已保留 `Evidence/main-world-first-failed.json`：正常切控屏障正确拒绝同帧配置，夹具后续索引越界；另预输入已经消耗时仍断言 buffer 存在。已改为切控后等帧，以及逐帧记录预输入再核对仅一次消费；没有放宽产品权限。**本文件写入时等待主代理重跑结果，不能据此报告 A4 完成。**

## 修改路径所有权

子代理只修改 MainAction 新模块/测试、Root/EquipmentDefinition/DataBus、HUD 三文件的窄接线及装备实例选择字段。未调用 Unity MCP、未手改序列化资产、未提交 Git。其他已释放功能、主代理新增 status 与 Spine 表现路径均保留。
