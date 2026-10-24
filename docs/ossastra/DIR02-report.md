# DIR02 小蓝五面候选：实施交接

日期：2026-10-25。接手基线 `a7efd119294d0d6f3a6093e7ae89924d298bbf7a`。附件DIR02 v1.0为规格；私有Chat链接未能读取正文。本文记录本机实际结果，不复用DIR01浏览器测试作Unity验收。

## 试玩入口

- 场景：`Assets/OSSASTRA/Scenes/SC_CharacterPresentationCompare.unity`。
- A：`PlayerCharacter_BlueFiveFace.prefab`，当前玩法身份保持temporary-a；B：原rozeul，仍为temporary-b。Z切控，其他键位读取原Input Layout。角色/库存/敌人/HUD服务沿原集成场。
- 新候选是原PlayerCharacter的Prefab Variant；只有一套Root和身体Controller。原主集成场、原Prefab、原SO、Build Settings均不覆盖。
- A身体节点的`CharacterDirectionPresentation` → `Weapon Display Mode`：SourceReference显示模型内置武器、隐藏额外装备；IndependentEquipment可逆隐藏具名内置武器slot并显示项目独立Spine技术探针。它不改变真实装备/技能资格。默认SourceReference。
- `Show All Branches`仅研究诊断，正常保持关闭。角色根Inspector可定位方向Profile、Rig、模型覆盖和原Map/Router；Play显示face/mirror/revision、来源、隐藏slot数、左右实际手骨。Workbench也可读取当前模型方向与真实挂点。

## 实际状态

| 状态 | 结果 |
|---|---|
| BODY_FIVE_FACE_PLAYABLE | true；Unity4.3实际输入、四段空挥/移动闪避/格挡/实战技能通过 |
| EQUIPMENT_MULTI_FACE_READY | true，限所选动作/技术探针；复杂手臂躯干内部穿插仍PARTIAL |
| ROZEUL_REGRESSION_OK | true，定向切控/普攻/混合输入回归，非重跑全部历史系统 |
| FORMAL_SCENE_REPLACED | false |
| FULL_CINDERIA_GAMEPLAY_PORTED | false |
| USER_ART_APPROVED | false，等待用户比较 |

## 架构落点

有效输入/AI及已经接受的动作事实 → 原Controller → 动作Intent + StructuralDirection Intent → 原Router/Mapping/Override/Composer/Handle → Spine4.3姿态 → 每实例显隐/动态Rig → 独立装备。没有第二个输入、动作钟或伤害权威。

- Direction Profile只存面/扇区/镜像/根/逻辑ID/结构轨/边界/内置武器slot。当前面与修订在每个实例，玩法向量不量化。
- 小蓝轨0动作、轨9结构是配置；只有该Root绑定的合法Profile、正确source/layer/logic/track、零时长/零mix结构片段能豁免全身独占。普通高轨仍被清。
- BeforeApply恢复上帧临时alpha，只复位五个方向根的X/Y；UpdateComplete读取最终AppliedPose并额外mask；网格完成后恢复原alpha。保留全部辅助骨/约束/附件及clip生命周期。
- 新`modelOverrides`使用原Override规则类型，解决原装备身体片段名属于rozeul的问题。模型覆盖位于装备覆盖之后、状态覆盖之前；原装备定义/ID及玩法数值不复制不改。
- Rig编译时解析名字，运行时只选择缓存索引。30条面/阶段绑定包含a4辅助分支接管。换面不重建装备/清捕获/重置装备动画。装备资源或Skeleton真正改变才重绑。
- 修复旧语义Rig一个实际缺陷：Rig明确给出空slot时，表示不跟随slot，不再回落到另一模型的历史slot。rozeul有slot的绑定保持原行为。

## 资产与本机依赖

原 `E:/1画画/星骸回廊/小蓝/xl.spine` 未改，SHA256：`CEA88B22BD2C1539AEF081D0FA0B76720B3A0AC6F6C22ED779430049B22F3196`。真正unit：4.3.26，776骨/494slot/138动画；192图片找到。源4.1旧导出不用于正式运行。

官方4.3.26 CLI处理副本，重新打非PMA/bleed图集，4049×1545，Unity NPOT=None、无mipmap、不压缩。项目保持Linear。旧PMA警告由用户Ignore后已解除，未切全项目Gamma。真实本机模型在`Assets/OSSASTRA/LocalMedia/DIR02Blue/`，明确发布排除。受限骨架/图片/图集/视频、许可证日志均不上传GitHub。

公开仓库是代码与合法配置引用，干净克隆仍依赖原工程第三方插件及获准本机商业模型，不承诺开箱可跑。生成/导入脚本在`Candidates/DIR02/Tools`；同路径重复导入保留已生成GUID，Variant/场景生成器遇已存在资产拒绝覆盖作者配置。新机器补齐媒体后需在Editor中重新指定模型引用，见替换指南。

## 验证与限制

见`DIR02-acceptance.md`、`DIR02-action-rig-coverage.md`、`DIR02-model-replacement-guide.md`。本机证据在`Candidates/DIR02/LocalEvidence`与外部work目录，不公开商业媒体。55秒无声实战视频已完成并抽帧检查；另有同一a4 42%进度八向离屏姿态图，明确不是输入故事。

保留源矩形slot骨原点规则：长附件可伸出矩形，不等于精密几何裁切；特定姿态的边缘/内部遮挡仍需正式美术验收。独立武器目前沿既有Spine技术探针，未声称复刻源游戏全部武器造型或全部138动作。实体手柄未代签，InputSystem虚拟手柄测试已明确标注。

原36项dirty（31删除+5修改）受保护；五份修改文件与接手SHA逐项一致，源Spine SHA一致。用户删除的旧场未恢复、未提交。本轮只选择自有代码、配置、候选与报告发布GitHub，不Gitee、不关机。
