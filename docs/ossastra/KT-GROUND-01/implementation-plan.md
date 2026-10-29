# KT-GROUND-01 Implementation Plan

**Goal:** 在新独立研究场交付可编辑、可保存且有效面积为旧样地3倍的参考地面，保留现有双角色/单敌/观察链。

**Architecture:** 来源证据先决定表达；若自然地面确认Tilemap，则优先原生Tilemap作为唯一作者数据、原生Undo/Palette和规则邻居刷新。若来源是Terrain/混合网格，则按实际通道选择最小适配，不将现有DS网格作为预设答案。原系统只作为消费者。

**Tech Stack:** 现有Tuanjie2022.3.62t16、URP14.2、Tilemap1.0/extras3.1.3、只读UnityPy1.25.4；不升级或更换共享管线。

**Spec:** 用户KT-GROUND-01_v1.0 zip及其01执行计划/02验收/两图；完整批准副本在本机研究root/plan。

## Constraints

- 先实际测32×32基准，新增有效面积3072m²候选64×48，不放大纹理/角色。
- 研究≤2GiB，白名单导出；SOURCE_VERIFIED/OBSERVED/INFERRED/UNITY_ADAPTATION/UNKNOWN逐项分清。
- 原Scene、基础Prefab、角色伤害/输入/动画与全局管线只读，新场从已保存DS-MAP-02承接。
- Scene/Prefab/SO/导入设置只用Editor API；仅root写Unity。禁止公开受限素材/原代码/可逆图集派生表。
- 用户已批准本计划连续执行，不在常规参数或阶段边界重复询问。

## Tasks and checks

- [x] M0：只读MCP取得项目/场景/PrefabStage/dirty，地面Mesh世界XZ并集与地面Collider交叉校验，保存精确基准。原scene不保存。
- [x] M1–M2：单容器关联自然草地、路与复杂交界；有界联系图及通道回读，先明确主路线和必要依赖，再实现。
- [x] M3：小样先验来源/尺度/边缘，未满足时不扩图；只用独立材质/源码。
- [x] M4：优先运行真实原生编辑行为的特性检查，再对自有笔刷事务/目标约束/增量更新写失败用例；通过后接入实时SceneView。图块规则采用源数据，不猜邻接。一次stroke一次原生Undo，取消回滚，失焦明确提交已绘部分；原生行为若不同以调查后明确策略为准。
- [x] M5：首次创建新场且拒绝覆盖；示范与笔刷同一作者数据；边界与地面覆盖64×48，原actors/bridge/camera保留、地表观察时显式关闭本场敌攻击。
- [x] M6：真实拖刷/擦除/填充/跨块/UndoRedo/保存重开与稳定重建；八向镜头、边角行走与一次真实交战/复位，原场定向兼容；同条件1倍/3倍与纯地面/带角色有限统计。
- [x] M7：保存后回读、缺依赖入口、用户指南/实际组成与原创替换规格、有限证据及保护审计、仅自有成果GitHub发布、全部租约释放。
