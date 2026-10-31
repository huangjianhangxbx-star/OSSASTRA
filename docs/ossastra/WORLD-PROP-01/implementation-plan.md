# WORLD-PROP-01 Implementation Plan

**Goal:** 原双角色攻击可破坏物，经原命中/唯一Health、真实地面库存与唯一交互键进入原背包，保持破坏/物品同档一致性。
**Architecture:** WorldProps独立实体与场景组装；Core负责稳定身份/Health/一次终结/固定计划；Runtime经Combat接口，Integration只接Inventory和Commerce participant。2D/3D共享业务，表现只读。Root唯一Unity writer。
**Spec:** 本机计划包v1.0，00-baseline/plan/00—05；用户本次要求执行。具体样本/无再生/临时美术按工程默认，不伪作历史定案。
**Tech Stack:** Tuanjie2022.3.62t16，新InputSystem1.14.4，原Health/CombatContactGateway、Ledger/Session、Commerce串行writer、URP14。

## 约束与文件责任

保留原37dirty、原Lab未保存现场/源图、原Health/库存/Current/输入与保存唯一写入权。源素材本机只读/定向提取，不发布媒体。仅新集成/中转两场与隔离profile，不加采矿工具/生态/自然再生/碎裂/联网。

- Root：WorldProps/Core/WorldPropState.cs、WorldPropStore.cs（身份/阶段/固定DropPlan/快照）、Health.FromSnapshot严格恢复；Runtime/WorldPropRoot.cs、WorldPropsRoot.cs（组装与有界推进）；Presentation/WorldPropView.cs；Integration/WorldPropPlacement.cs（库存/保存窄适配由WorldPropsRoot组合）；Editor/WorldPropAuthoring.cs，定义/原生样板。
- 库存代理：InventoryContainerGrant.cs原子publish、InventoryGroundRewards.cs、InventorySessionRuntimeState.cs的明确kind/surface，InventoryInteraction/InventoryWorldView接自然散落；CommerceSnapshotParticipants.cs与Service/RuntimeSnapshot窄保存参与者，不依赖WorldProps。接口冻结见下。
- 研究代理：只读两个原作，各写独立外部source/asset证据。

## 接口

`ICommerceSnapshotParticipant`: SnapshotKey、Changed、Capture()、TryPrepareRestore(section,aggregate,out object prepared,out string error)、PublishRestore(prepared)。Publish只切换已准备状态，不throw/通知。新participant对旧缺section明确拒绝，无participant旧schema2继续。
`InventorySession.CommitGroundReward(containerId,mapId,Vector3 position,surfaceId,displayName,InventoryContainerGrant[] grants,Action publishReceipt) -> InventoryResult`。一Break一容器整批原子授予；Ledger/worldrecord/receipt先共同发布，之后通知。奖励receipt留WorldProps同一section，不新增第二物品权威。
WorldProps于Awake在InventorySession.Start前注册participant；物件Health受实际网关扣减，Applied后冻结Pending/DropPlan，下一有界推进交付；保存损坏/缺定义不重播种。落点无合法同层支撑保留Pending。

## 执行与验收

- [x] M0 保存实际scene/HEAD/dirty基线；只从保存样板复制新场，保留用户dirty检查点；核对源依赖与原背包装配。
- [x] M1/M2 两原作精确调用链与2D/3D证据、取舍/少量资产；动态不可安全隔离则逐项标受阻，不动原档。
- [x] Core RED→GREEN：Health严格snapshot，实例隔离、多段/重复终结、固定计划/重试/耗尽/未知坏快照；dotnet run --project Candidates/WORLD-PROP-01/CoreTests/CoreTests.csproj。
- [x] Seam RED→GREEN：原子批次、异常观察者捕获、满包不丢、同section恢复/缺失拒绝、原存档失败重试。代理定向使用原COM01候选测试，不启动Unity。
- [x] M3 原生新场配置+原角色InputAction攻击→原Health下降→一次落物→原F拾取→原背包UI；失败保留真实回执。至少一个需多击。
- [x] M4 同定义多实例/2D植物石头/自有或有证据原作3D/不可破坏对照；合法同层落点、原音声事实、视觉晃动不动逻辑根。
- [x] M5 同快照半血/破坏未取/已取，真主场→中转→返回，结束Play从磁盘恢复；保存拒写/奖励拒绝同意图重试，坏档不授予。
- [x] M6 16左右物件及约200静态候选压力；透视活跃中破坏、collider/代理注销/可走过，静态无逐物Update、必要资源计数稳定。限定B模块与C一致性接缝，不重跑无关历史。
- [x] M7 Prefab/SO/Inspector作者教程、离线HTML来源/对照/连续实机录像与通过/失败/未测；中文注释核对，自有文件按R11定向发布核SHA。

授权来自用户“理解较新对话并执行”与本包本轮推荐默认；已有连续执行、不小阶段询问偏好沿用。公开聊天正文不可读，未当作已获取。

完成交接：功能提交 697c3db94d01926880cdc93191b8dcc67e5921af 已GitHub精确核验，共51项自有文件。全部Unity/源码/文档writer和路径租约释放；最终Main Edit ready/clean，demo档保留实际验证进度。默认档事故独立披露且未自动回滚，不电源操作。
