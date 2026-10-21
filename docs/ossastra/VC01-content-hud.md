# VC01 内容、HUD 与审查地图

## 当前内容与固定来源
- 原型冻结 c21d4fc755db4fabb0227c690b4e55ebbec19829。Origin 本机86824bd为其祖先，未切分支。ChatGPT私有链接登录墙，未声称读到新增正文。
- 两位角色仍共享 rozeul（数据4.3.26）和动作/表现定义，各有独立Root、DataBus、动作时钟、体力、装备实例与技能冷却。
- 四段完整普攻 atk1–4；闪避 dodge_front；盾准备 ready_loop；受击 upper_hitted_all；破势 dizzy_loop。12候选实播目录见 baseline/catalog；原事件被烘焙到动作时间线，Spine回调不另结算伤害。
- 近战技能 HunterBreak_Adapted：参照原型 core/right-hand-skills.ts 的 hunter-break（实验·破势斩）职责，以 heavy 完整片段适配，20伤害/18体力/6秒CD。
- 派生技能 RangerRocket_Adapted：参照 al-native-E 火箭弹射的接受→释放→离手效果职责，以 xskill 适配为延时范围效果；40伤害/28体力/6秒CD，距离1.5、范围1.25、延时0.4秒。不是原火箭所有几何/职业逻辑的完整复刻。
- 以上动作时点来自模型事件；用途映射与伤害/距离/费用为明确 SAMPLE。主手技能不要求左盾。冷却按本体 + 主手实例 + 定义ID保存，换槽不重置；接受后取消不退款。
- Y 仍是原场景唯一共享瓶服务，ItemDefinitionSO仅保存名称与100点治疗定义。死亡、换装、切控和HUD重建均不生成新瓶。
- 左手盾为用户批准生成的新素材；炸弹为既有本机纹理，手持与离手飞行同源，接受时冻结sprite与尺寸。独立副手以手持/离手/落地反馈呈现，本轮未配置专门上身投掷轨，不据此宣称模型缺动作。手持视图由Root LateUpdate在Spine求姿态后更新Lhand挂点；死亡立即隐藏。
- 新候选敌HP600仅为本场可完成整段动作故事的SAMPLE配置；旧场72HP不改。复用原型 AL-01/assets/zombie 的同一类近战敌资源，仍是独立EnemyRoot。

## E1 实际兼容证据
初次不带图集的解析因找不到“前腿”区域失败，**不是版本不兼容的证明**。带正确图集后，Spine4.3运行库实际读取4.1.23 JSON，13段动画逐段Apply并求世界姿态成功。新场已绑定图集/材质/骨骼资产并实屏显示，移动/攻击仍消费原EnemyRoot事实。此为本资源和本机运行库的实测适配，不宣称任意4.1资源均正式兼容4.3，不升级插件，不改原媒体。

## HUD 视觉、绑定与未迁移业务
布局与图片来自冻结版 exploration-hud.ts + geometry-hud.css（包括文件末尾metal皮肤覆盖），不是旧UI-01C草图。
- 左上固定猎人、左下固定芙蕾茜娅；切控仅改变主控标记与右侧技能，不互换状态卡/头像。伙伴头像使用原型用户图。
- 六区以 min(宽/1920,高/1080) 缩放并锚到物理边缘。1920×1080 /1440×900 /1280×720各6个外框已测，容差1.5px，18项通过。三档原型与Unity图片在本机work目录，不公开媒体。
- 绑定：本体HP/架势/体力、左右手定义/实例/耐久、实控技能/CD/可用性、共享瓶余额；装备窗口只提供实际持有的盾和炸弹，空闲换装，拒绝不丢装备/修耐久。
- 世界/日历/天气、活力成长、被动、完整背包、优先物品、十格快捷物品尚未迁移；保留原位并显示“未接入”，不伪造业务数据。虚血未接入显示“—”。
- 采用现有uGUI，不安装新框架。界面走 ICharacterHudPort→Adapter→既有正式命令。Modal只捕获本地玩家输入并阻挡点击穿透，不暂停世界；有效实控身份与设备采样许可是不同契约，已接受副手/实控CD不会因窗口冻结；关闭要求真实松键重武装。固定卡与Current分别投影。

## 候选场键位对账
|操作|本轮候选|冻结原型依据|
|---|---|---|
|移动 / 普攻 / 副手|WASD / LMB / RMB|原型直接输入|
|闪避|Shift|原型Shift|
|切控|Z|exploration-app.ts KeyZ / role-interaction-panel.ts Z|
|共享瓶|Y|useHealingBottle|
|技能|Q/E；R未接入|右手三槽原位，当前只装两代表技能|
|近篝火一次补给|F|沿交互语义，不占原型G战术|
|背包|Tab保留但未启用|原型Tab背包，不再接成切人|
|战术 / 选路 / 成长|G / B / V均未接入|保留语义，不拿来当补瓶或切控|
|装备窗口关闭|Esc 或窗口按钮|原型模态窗口|
旧验证场Tab/G绑定保留。暂停/取消仍沿当前候选已有InputAction配置，不宣称已迁移完整设置菜单。

## 真实反馈
CombatFeedbackView只消费ActionObserved释放、Melee命中收据、盾挡收据、闪避身份、实际落地计数和共享瓶使用事实；不监听攻击键来伪造命中。原型 release/hit/hurt.wav本地接入，短时世界弧线/落地圈随事实生成并统一回收；拒绝原因来自能力/协调器结果。落地反馈不等于伤害命中计数。没有在本轮重建BGM/完整音频总线。

## 源码阅读顺序
1. Characters/Runtime/CharacterRoot：初始化与编号Update、装备LateUpdate、主动作配置接受端口。
2. CharacterContracts → CharacterActionCoordinator → Core/ActionAdmissionLedger：窄边界、资格查询、唯一接受/付款。
3. ActionDefinitionSO / CharacterAnimationProfileSO / CharacterDefinitionSO；Core/BasicActionRuntime / BasicChainState：定义与可配置连段；CharacterSpineView只提交播放。
4. EquipmentDefinitionSO → CharacterEquipment（原EquipmentState）→ CharacterEquipmentView：定义、身份/耐久、挂点表现分离。
5. SkillDefinitionSO → CharacterSkills → Core/SkillCooldownBook：槽、主手来源、冻结执行与派生；仍调用原CombatContactGateway。
6. UI/CharacterHudContracts → Adapter → View/HudSkinSO：固定身份快照、正式命令、原型素材布局；CombatFeedbackView只读事实。

停止在VC01候选集中试玩与架构审查，不自动开始VC02背包/商人、VC03旅行或完整职业库。审美、物理键鼠手感、扬声器听感和用户架构接受不由自动测试代签。