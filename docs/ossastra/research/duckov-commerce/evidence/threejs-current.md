# Three.js 现行交易与物品能力只读审计

审计日期：2026-10-28（北京时间）。本文件只读比对现有源码与已提交任务记录，没有运行项目、修改 Origin、拉取远端或复验浏览器。以下“可达”指代码中的正式页面入口和事件路由已接通，并有既有验证记录；不是本轮实机通过证明。

## 版本与判读口径

- 本机 `Origin` 的工作树分支为 `main`，HEAD `86824bdf358bd666072809cac6a7d3491b4c384d`（2026-10-14）；本地状态显示比 `github/main` 落后 163 个提交。工作树共有 773 项状态（467 个已跟踪文件改动、306 个未跟踪项），其中 `game/src` 和本审计所引的 `docs/tasks` 文件未见本地改动。不得覆盖这些并行状态。
- 本机现有 `github/main` **远端跟踪引用**为 `bb969cc88591fd8e93e4c939cb52719dd652a948`（2026-10-25）。未执行 fetch；随后在 2026-10-28 使用 GitHub 连接器比较该提交与在线 `main`，结果 `identical`、ahead/behind 均为 0。所有 `github/main:路径:行号` 均对应该固定提交，可用 `git show bb969cc:路径` 复核。
- `work/OSSASTRA/VC01-20261021/prototype-c21` 是 10 月 21 日的迁移快照。补充 Git blob 逐文件比较表明，其中 `game/src` 的 245/245 个文件与固定提交 `bb969cc:game/src` 字节相同；它与本机较旧 `Origin/main` 工作树不同。快照的依赖、配置、测试与运行环境未据此证明等同，当前交易源码仍以固定提交作引用锚点。
- 本机旧 HEAD 只有轻量团队库存与历史经济，缺少较新跟踪引用中新增的 `inventory-model`、`item-commerce`、`merchant-shop`、`dialogue` 等。若只看本机工作树会错误地判定商人买卖和实体钱币不存在。正式入口是 `game/src/main.ts:3–7` 与 `game/src/core/game-session.ts:5–8`；无查询参数的普通探索走 `exploration-app`，地图正式开关见 `github/main:game/src/exploration-app.ts:68–70`。

## 当前能力矩阵

| 能力 | 判定 | 证据与准确边界 |
| --- | --- | --- |
| 芙蕾茜娅角色交互 | 正常 UI 可达，条件门控 | `github/main:game/src/core/role-interaction.ts:11–24` 定义 companion 四项和同层、距离、视线、存活及脱战门；`github/main:game/src/role-interaction-panel.ts:25–28` 在近身 F/点击后开菜单。家园初始驻留、可后续组队见 `github/main:game/src/core/home-resident.ts:15–27`。主控她本人时不能把她作为交互目标。 |
| 芙蕾茜娅对话 | 正常 UI 可达的 SAMPLE 内容 | 菜单 `dialogue` 分支调用对话，`github/main:game/src/role-interaction-panel.ts:28`；正式应用接线 `github/main:game/src/exploration-app.ts:115–116`。内容池有默认三句、二选一、条件后续句，`github/main:game/src/core/dialogue/content.ts:3–19`；会话要求探索空闲及有效角色状态，`github/main:game/src/core/dialogue/game-adapter.ts:21–31`。选择只落地标记和提示；不是任务、传送或商店剧情事务，见 `github/main:docs/CurrentRules.md:720–721`。 |
| 芙蕾茜娅个人商店 | UI 可达；货架为空的占位 | companion profile 的 `shopCatalog:'empty'`，普通 merchant 为 `'sample'`，`github/main:game/src/core/role-interaction.ts:11–16`。`ensureShop` 对空 catalog 返回空库存，`github/main:game/src/core/merchant-shop.ts:9–10`；货架空态见 `github/main:game/src/merchant-view.ts:5`。因此可开独立空商店，当前没有可购买商品。`merchantAccess` 仅检查有 shop 能力，`sellItem` 不要求 catalog 有货；依源码推断近身可向她出售合法有价物，见 `github/main:game/src/core/item-commerce.ts:20–24`，本轮未实机验证此细节。 |
| 送礼、角色装备菜单项 | 仅入口／占位 | companion profile 列出四项；`github/main:game/src/role-interaction-panel.ts:28` 对 gift/equipment 只显示“后续开放”。与背包里的真实装备拖放能力不同。`github/main:docs/CurrentRules.md:715–721` 亦明确非商店项范围。 |
| 普通巡旅商人购买与出售 | 正常 UI 可达的 SAMPLE 闭环 | 商人实体由 `github/main:game/src/core/world-containers.ts:17–23` 建立，角色菜单 shop 分支进入背包商店 `github/main:game/src/role-interaction-panel.ts:28`、`github/main:game/src/exploration-app.ts:116`。`github/main:game/src/backpack-panel.ts:44–46` 选货、点击或 F 执行买卖；买卖身份、报价、库存、容量、金额在 `github/main:game/src/core/merchant-shop.ts:10–13` 和 `github/main:game/src/core/item-commerce.ts:20–24` 校验。5 种食物各 5、5 种贵重品各 2，300 模拟秒刷新；定价为卖价 2 倍，均是 SAMPLE。既有记录称定向 28 项核心和 2 条浏览器故事通过，`github/main:docs/tasks/ITEMS-MERCHANT-01/ppt-trade.md:7–13`；本轮未复跑。 |
| 实体钱币 | 已实现为库存物品；与旧金币账本分离 | `github/main:game/src/core/item-catalog.ts:2,10` 的 `trade-coin` 是 1×1、可叠至 999999、0 重量的物品实例；出售按指定实例扣物并原子加入团队包钱币，钱币装不下则不成交，`github/main:game/src/core/item-commerce.ts:8–15,21–24`；购买从团队／安全／后备中已携带的实体钱币扣款、所得入团队包，失败不提交，`github/main:game/src/core/merchant-shop.ts:12–13`。另有历史 `Economy.account/carried.gold` 数字账本和页面兑换入口，`github/main:game/src/core/economy.ts:3–13`、`github/main:game/src/economy-ui.ts:8`；交易代码不读取该 gold。不可将两个“金币/钱币”自动合并为一个 Unity 余额。 |
| 背包与容器 | 正常 UI 可达，实例和有限容量 | `github/main:game/src/core/inventory-model.ts:2–10` 定义唯一实例、位置、数量、容器修订；默认团队 5×2 网格、安全 2 条目槽、芙蕾后备 5 槽、外部试验箱 5×4。贸易样本投放将外部箱扩到至少 6 行，`github/main:game/src/core/world-containers.ts:17–19`。`github/main:game/src/core/inventory-operations.ts:31–39` 对世界、访问、主控、修订、同层/距离/LOS、装备资格重新验证，再提交并同步装备。`world-inventory` 的旧 `stacks` 和 `alReserve.slots` 由唯一实例注册表做只读投影，`github/main:game/src/core/inventory-world.ts:5–15`。 |
| 叠堆、拆堆、交换 | 正常背包交互 | `github/main:game/src/core/inventory-model.ts:22–35` 对数量、上限、位置、可交换尺寸、武器单件以及失败不提交做事务检查；`github/main:game/src/backpack-panel.ts:28,38,54,61–62` 提供数量输入与拖放。默认空数量表示整堆，显式数量可拆分；既有合同见 `github/main:docs/tasks/INV-02/contract.md:7–13`。没有物品旋转和容器套娃，见同合同第 19 行。 |
| 一键整理 | **正常 UI 可达**，有求解状态区别 | 团队包“整理”和外部箱“整理箱子”按钮在 `github/main:game/src/backpack-panel.ts:26,29,40`；`:69–78` 调 Worker，支持取消、超时后短预算回退；`github/main:game/src/core/inventory-pack.ts:5–25` 返回 `optimal`／`feasible_unproven`／`cancelled`／`infeasible`，合法堆可合并。主线程 `github/main:game/src/core/inventory-pack-commit.ts:3–5` 验修订、数量守恒、武器与坐标后一次提交。不可把一次“整理完成”均写成数学最优。`github/main:docs/tasks/INV-02/validation.md:7–14` 有既有核心与浏览器验证记录。 |
| 拾取、主动丢弃、全灭掉落 | 正常 UI 可达，生命周期有别 | 背包物品拖到场景空地调用 `dropInventoryItem`，`github/main:game/src/backpack-panel.ts:38`；后者检验同层合法地面与视线，失败回收临时容器，`github/main:game/src/core/inventory-manual-drop.ts:4–10`。Ctrl 点击地面物或 F 就近拾取、F 就近开箱，`github/main:game/src/backpack-panel.ts:36,47–48`、`github/main:game/src/exploration-app.ts:340`。手动掉落在地图切换时清除，`github/main:game/src/core/map-travel.ts:25`、`github/main:game/src/core/inventory-manual-drop.ts:12`；双人全灭另把普通包／装备／后备放进掉落包，安全包留下，`github/main:game/src/core/inventory-death.ts:6–11`。不要混同二者持久性。 |
| 重量 | 正常 UI 派生显示，当前不施加负重限制 | `github/main:game/src/core/inventory-weight.ts:4–9` 从实例计算已携物重量；`github/main:game/src/backpack-panel.ts:85` 明示 30 kg 是实验参考，只提示超重，不限制行动。 |
| 交易、背包与角色交互输入 | 正常 UI 已接线，需保留优先级 | `github/main:game/src/backpack-panel.ts:32`：Tab 开关，商店打开时 F 成交，数字键仍关联快捷栏；`github/main:game/src/exploration-app.ts:221,340`：点击角色，或 F 先角色菜单，再手动掉落拾取、再容器。`github/main:game/src/role-interaction-panel.ts:25–28` 会话身份有效性门；背包 `github/main:game/src/backpack-panel.ts:51–53` 持世界/访问/控制/输入代际 token。`github/main:docs/CurrentRules.md:705–717` 记录 V 寻路、F 交易优先与同页输入取舍。 |
| 持久化 | 仅页面内世界状态；刷新存档未实现 | 背包、商店库存与场景地点保存在 `WorldSession`／`InventoryModel`，`github/main:game/src/core/world-session.ts:27,44`、`github/main:game/src/core/inventory-model.ts:6`、`github/main:game/src/core/merchant-shop.ts:10`。对话标记存于 `WeakMap<GameState,...>`，`github/main:game/src/core/dialogue/game-adapter.ts:12–14`。正式页面的“查看开发记录”仅将 JSON 放入只读文本框，`github/main:game/src/exploration-app.ts:281`；没有加载/恢复入口。页面明确“刷新不存档”，`github/main:game/src/economy-ui.ts:8` 与 `github/main:game/src/core/world-session.ts:44`。源码范围搜索未见 localStorage、sessionStorage 或 IndexedDB 写入；此为静态范围观察。 |

## 历史能力和不能误用的入口

1. 旧卡牌变卖和旧塔防入口保留在代码，但正式探索会话退休 `sellCard` 等命令，`github/main:game/src/core/game-session.ts:3–8`。卡牌余额／旧金币账本不能作为普通商人交易实现证据。
2. `sellBatch` 与 `quoteSale` 仍有定义，`github/main:game/src/core/item-commerce.ts:32–42`，但在 `game/src` 中没有其它调用点；现行 UI 使用单选物品的 `sellItem` 和 `buyFromShop`，`github/main:game/src/backpack-panel.ts:44–46`。旧待售区流程已被交易 PPT 流程覆盖，`github/main:docs/CurrentRules.md:697–708`。
3. 本机旧 `Origin/main` 不代表当前交易闭环；`prototype-c21/game/src` 已证实与固定提交源码字节相同，但其完整运行环境未证实等同。迁移取证以固定提交和实际可达调用链为准；2026-10-28 在线 `main` 已核对与该提交一致。

## Unity 迁移对账需要保留的缺口

- 芙蕾茜娅有可达的 SAMPLE 对话与空个人货架；送礼和角色装备菜单仍占位。不要把“四项菜单可选”写成“四套业务已完成”，也不要把通用出售通路误写成她有个人商品库存。
- 实体钱币与旧数值金币并存，当前购买只认实体钱币；Unity 迁移需先定唯一支付／兑换合同，不能凭名称直接合并。
- 整理和库存事务已有明确容量、修订、原子性与失败保持，但 Worker 允许 `feasible_unproven`；游戏中也无物品旋转／容器套娃。迁移验收需按实际状态码和约束，不用“按钮存在”代替提交验证。
- 世界与商店跨同一页面会话保留，手动掉落跨地图清除；刷新无持久存档。若 Unity 目标要持久化，需要独立制定存档格式、版本迁移和恢复一致性，不能从当前 Three.js 原型推断已有实现。
- 本文件只验证源码路径与既有交付证据，未重新运行 UI 或测试；在线远端已通过 GitHub 连接器比较提交，未改变本地工作树。迁移前应重新冻结准确提交，并在 Unity 实际场景验证 F/Tab、角色近身、交易、掉落和保存边界。
