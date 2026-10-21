# ROADMAP02 Implementation Plan
Goal: 双人有限自主战斗、只读预警、二维呈现与独立综合场。
Spec: 用户ROADMAP02 v1.0；本地work/OSSASTRA/ROADMAP02-20261021/spec.md。
Architecture: 保留三根/单Current/单Frame/单动作时钟/HP/Posture。场景Engagement订阅Accepted，本体CompanionCombatBrain仅产出移动和窄Stage1候选；Root统一请求。预警/二维呈现只读。
Tech: Tuanjie2022.3.62t16 / InputSystem / 3D CharacterController / Spine4.3.26。
P0: 本地远端main 9e10cb3一致；ROADMAP01 Edit clean；旧禁编两删除不动；原场/Prefab/素材保护哈希保存在本地work。外链登录墙。项目适用技能已读取，未使用不存在的executing-plans。
- [x] S5：纯策略RED/GREEN；CombatEngagementSession与CompanionCombatBrain，窄接口接Root/Selector；新独立场；真实输入/命中/切人/生命周期Gate。
- [x] S6：Accepted观察与预警策略RED/GREEN；Enemy只读快照、复用几何渲染；真实取消/释放/资源生命周期Gate。
- [x] S7：本机窄资产核验；独立敌/NPC二维适配，不改玩家播放或物理；技术/素材Gate分报。
- [x] S8：综合自然交战、保存重开、受影响旧场定向回归；保护SHA、源审查、GitHub定向发布。
Non-goals: 技能、导航、经济、正式美术定稿、SharedPrefab变动、第三方修改、Gitee。
Validation: C风险/B规模，纯策略和真实Root/InputSystem/Physics分别记录；失败保留并分类，人工手感待审。

S7技术完成，正式敌美术ASSET_BLOCKED；测试替身不代替正式艺术验收。
