# OSSASTRA 交易闭环迁移：Unity 当前能力与接线证据

2026-10-28，只读静态审计。Unity 项目根：`E:/WORLDCREATOR/new/Project_ANKALUTE`；本机 `HEAD=f0e41a6ce9a6e66b0afd8a01d4e83dda1e3e830e`。以下 `Assets/...:行号` 均相对此项目根，指审计时工作树内容。未启动 Unity，未编译，未修改生产源码或资产；场景接线仅据保存的 YAML 判断，运行效果不在本证据范围。

## 工作树与同步边界

- 启动时 `git status --short --branch` 为 `main...origin/main`，既有 31 项删除、5 项修改：两份 `SC_CharacterVerticalSlice`、`SC_VC02B_IntegrationReady` 场景，`CharacterEquipmentView.cs`、`CharacterDebugControls.cs`、`PlayerInputLayout.asset`。审计登记使 `TASK_REGISTRY.md` 额外成为第 6 项修改；只读任务记录被项目 `.gitignore:92` 的 `/docs/*` 规则忽略，仍保留本机。所有既有差异按用户/其他任务现场保护。
- 已读 `AGENTS.md`、`docs/agent-sync/SYNC_PROTOCOL.md`、`PROJECT_STATE.md`、`TASK_REGISTRY.md`、`OSSASTRA_RULES.md`、`docs/ossastra/character-foundation.md` 和 `tasks/2026-10-22-equipment-boundaries.md`。当前登记的本任务为 `com00-unity-audit`、`read-only`、无独占路径；未申请 Unity writer。`OSSASTRA_RULES.md:14` 要求 Inspector 可配的 InputAction；`:20` 保护用户 Unity 现场；`:65-71` 规定装备与投递边界。
- 审计时 `TASK_REGISTRY.md` 的“活动任务”段没有列出仍占用中的 Unity writer；最新的 `AUDIO-LISTENING-FIX` 已在 `PROJECT_STATE.md:570-571` 和登记表尾部标明完成/释放。登记表夹有不少已完成的历史条目，故这只是本次快照，任何实施前都要重新读登记表及目标 diff 并正式取得唯一 Unity writer。
- `SC_VC02B_IntegrationReady.unity` 的已有差异仅触及相机层级/位置（`git diff` 约第 5294、18583 行），审计不将其归因于本轮。旧 M01/M02/ROADMAP 场景删除也不恢复。

## 可复用的库存与装备基础

| 能力 | 本机证据 | 边界 |
|---|---|---|
| 唯一库存实例、定义/容器、修订和访问代际 | `Assets/OSSASTRA/Scripts/Characters/Core/Inventory/InventoryLedger.cs:12-38`；`InventoryContracts.cs:10-81` | `Items` 为拷贝快照；位置和数量由 Ledger 管。 |
| 原子单次背包操作 | `InventoryLedger.cs:66-80` 的 `Grant` 在试算副本找位后提交；`:82-131` 的 `Plan/Query/Transfer` 校验来源/目标 revision、AccessGeneration、锁定与容量，单次 `Commit`。 | 这是**单个库存命令**原子性；现有 API 没有“扣币＋交货”作为一个提交、没有卖出销毁命令，不能把连续 `Grant`/`Transfer` 说成原子交易。 |
| 物品预留和释放消费 | `InventoryLedger.cs:132-143`；`Assets/OSSASTRA/Scripts/Inventory/InventoryItemUse.cs:56-90`。 | 现有预留/消费针对能力使用；交易需要独立的资格与提交协议，不能借能力释放伪装售卖。 |
| 装备唯一 payload | `InventoryContracts.cs:48-57` 将 `EquipmentItemState` 附到实例；`InventoryLedger.cs:41-48` 校验手别、Owner、数量、ID；`EquipmentItemState.cs:10-24` 持有 ID、耐久、能力状态。 | `InventoryLedger.cs:188-194` 整理也验证同一装备 payload 引用。 |
| 手槽只读投影 | `Assets/OSSASTRA/Scripts/Inventory/InventorySession.cs:42-58` 的 `Slots` 从 Ledger 手槽读同一 payload；`:70-80` 装配绑定。`Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterEquipment.cs:20-29` 绑定后经库存换装；`Assets/OSSASTRA/Scripts/Characters/Core/EquipmentState.cs:28-42,67-75` 绑定后透过槽位读身份/耐久并扣磨损。 | 未绑定的旧场仍有兼容实例（`EquipmentState.cs:43-65`）；交易准入应明确要求库存绑定，不能把旧兼容装备当可交易 Ledger 条目。 |
| 手动丢弃、全灭掉落 | `InventorySession.cs:110-115` 在全队覆灭时从团队/后备/四手槽转到 `drop:wipe`，安全栏保留；`:184-197` 手动落地与空地包回收；`InventoryLedger.cs:144-156` 全灭批次保留装备 payload、撤销预留；`InventoryInteraction.cs:34-47` 的 F 优先拾取手动掉落。 | 世界地面容器仍是当前会话内 `ground` 字典（`InventorySession.cs:26-28`），未见持久化。 |

**硬边界：当前会话只适配两角色。** `InventorySession.cs:64` 强制 `coordinator.Characters.Count != 2` 时拒绝初始化；`:68-80` 创建固定团队 5×2、安全 2、后备 5、外部 5×8 和两个角色的四手槽；`:41` 把后备 Owner 固定为索引 1，`:121` 据此判断后备可用；`Assets/OSSASTRA/Scripts/UI/InventoryView.cs:76-95` 固定画两名角色与 5 格后备。相对地 `CharacterControlCoordinator.cs:49-54` 只要求至少两名，可管理更多角色。若 COM 迁移要支持商人库存或更大队伍，不能直接把商人当第 3 名 `CharacterRoot` 塞进此会话；商人应有独立库存域/接入适配，队伍规模扩展需另改会话和 UI 假设。

访问约束可复用但要复核：`InventorySession.cs:117-150` 校验当前存活本体、容器、同层距离、`CombatDeliveryQueue.Clear` 视线、战斗/角色动作时换装拒绝及 Spine 表现定义；`:151-158` 才提交移动；`:184-193` 落地再次检查代际、来源 revision 与合法地面。交易不能绕开这些角色/容器资格，也不应为商人库存复用 `external` 固定 5×8 而无设计决定。

## 币、价格与商人：已有数据和明确缺口

- `Assets/OSSASTRA/VC02B/trade-coin.asset:12-27` 是 `definitionId: trade-coin`、`category: currency`、`maxStack: 999999`、`unitWeightGrams: 0`、`sellValue: -1` 的**普通物品定义**。`docs/ossastra/VC02B-migration-coverage.md:18` 明言它是实际可移动数量、无第二份 gold 余额。当前没有独立 Wallet 事实所有者；新交易设计须先确定货币仍走 Ledger 条目还是新增余额，不可双写。
- `Assets/OSSASTRA/Scripts/Characters/Runtime/ItemDefinitionSO.cs:20-32` 提供尺寸/堆叠/重量、`sellValue`、装备与使用定义；其中 `sellValue` Tooltip 明言“只保留元数据，不接交易消费者”。现有 `food-0.asset:27` 到 `food-4.asset:27`、`treasure-0.asset:27` 到 `treasure-4.asset:27` 有非负卖价；其他许多定义为 -1。对 `Assets/OSSASTRA/Scripts` 的 `sellValue|Trade|Merchant|Shop|Wallet|Currency` 源码检索仅命中 `ItemDefinitionSO.cs:22`，未发现定价/买卖/补货/回购或商人实现。
- `Assets/OSSASTRA/Scripts/Npcs/Core/NpcInteraction.cs:18-43` 仅做身份、可用与距离判断、成功计数及事件；`Assets/OSSASTRA/Scripts/Npcs/Runtime/NpcRoot.cs:11-39` 是独立 NPC 根，`TryInteract` 以三维平方距离和传入 available 复核，代码注释明确没有商店系统。`Assets/OSSASTRA/Scripts/Development/Combat/EntityRootsDebugView.cs:42-56` 是当前找到的 NPC `TryInteract` 消费者，且为 Development IMGUI 按钮，不是正常 F 交易入口。
- 保存的候选场景 `Assets/OSSASTRA/Scenes/SC_VC02B_IntegrationReady.unity:13126-13155` 有 `ARCH01_NonCombatNpc`、`npcId: npc-arch01-01`、距离 2；该对象是 NPC 样板，未见商人身份、货架、交易 UI 绑定。对 `Assets/OSSASTRA/Scenes/*.unity` 的商店/商人名称搜索没有命中；这是保存资产静态范围的阴性结果，不能证明运行时不存在外部动态生成对象。

## 输入捕获与唯一交互上下文

- `Assets/OSSASTRA/Scripts/Characters/Runtime/PlayerInputLayoutSO.cs:33-46,56-66` 已有场景 `interact`、背包、菜单与手柄/键鼠 Action 定义；`PlayerInputLayoutInstaller.cs:17-27` 在消费者 Awake 前将共享 SO 克隆到控制、物品、交互、背包等实例。候选场景 `SC_VC02B_IntegrationReady.unity:12435-12451` 保存了 installer 与相应引用。
- `Assets/OSSASTRA/Scripts/Inventory/InventoryInteraction.cs:15-51` 是当前候选的 F 上下文：手动掉落优先，随后合法外部容器/篝火，单次按键只走一个分支；`:3` 明确“不替商人/对话占位”。未调用 `NpcRoot.TryInteract`。交易接线需要纳入这一**同一按键仲裁**，不能让商人另挂第二个 F 监听造成一键双响应。
- `Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterControlCoordinator.cs:23-46,91-105` 区分模态 `SetInputCapture` 与背包 `SetInventoryCapture`，并阻止切人；`CharacterInputHandler.cs:57-66,200-245` 的背包捕获可只阻新战斗申请而保留移动与已接受长按，系统菜单可再捕移动；`Assets/OSSASTRA/Scripts/UI/InventoryView.cs:23-41,156-174` 是实际使用范例，`CharacterHudAdapter.cs:18` 提供另一个模态端口。交易面板应选择并成对释放捕获 owner，明确移动、战斗、切人和已接受动作的政策。

## 保存入口核查：OSSASTRA 玩法目前未接入

- `InventorySession.cs:60-83` 每次 `Start` 新建 Ledger、注册 catalog/容器、从角色装备和 `initialTeam`/`initialChest` 播种；类直到 `:199-200` 销毁只撤事件，**没有导出/恢复状态接口**。`InventoryLedger.cs:14-20` 的实例、请求去重、全灭去重和预留仅存内存；`EquipmentItemState.cs:10-24` 有运行时 ID/耐久/能力状态但无导入导出。`ground` 位置和快捷栏/优先引用也只在 `InventorySession.cs:26-36` 的内存字段。
- 对 `Assets/OSSASTRA/Scripts/Inventory`、`Characters`、`Npcs` 中 `ISave|SaveData|SaveGame|SaveFile|LoadGame|LoadFile|PlayerPrefs|JsonUtility|ES3` 的源码检索，唯一结果是 Editor 作者工具 `Inventory/Editor/CharacterConfigurationCatalog.cs:37` 的 JSON 导出；没有找到 OSSASTRA 玩法存档入口。扩大到全部 `Assets/OSSASTRA/Scripts` 后，`PlayerPrefs` 仅出现在音量设置 `Audio/Runtime/GameAudioRoot.cs:87-89,192-193` 与 `GameAudioMixService.cs:56-66`，不是库存或贸易存档。
- 项目含第三方 `Assets/Plugins/Easy Save 3/`、`Assets/Plugins/Pixel Crushers/Common/Scripts/Save System/`，旧 DRPG 也有独立 `Assets/旧代码/Script/GameFlowManager/DRPGSystem/Equipment/Save/DRPGEquipmentSaveCodec.cs`。其**存在不等于** OSSASTRA 库存/币/交易已接入保存。COM 计划必须显式列出保存边界、版本化快照/恢复顺序和失败回滚；不能把 Unity 场景重开或域重载后重新播种当存档验收。

## asmdef、候选场景与 Prefab

- `Assets/OSSASTRA/Scripts/Characters/Core/Ossastra.CharacterCore.asmdef:1` 是本机 `Assets/OSSASTRA` 下唯一 `.asmdef`，`noEngineReferences:true`、无引用；`InventoryLedger` 与合同在其目录下，依赖的 `EquipmentItemState` 也在 Core。其余 `Inventory/`、`Npcs/`、`UI/`、`Characters/Runtime/` 均未发现独立 asmdef。新纯交易账本若放 Core 可保持无 Unity 依赖；MonoBehaviour/SO/世界入口留在运行时目录，并先确认编译引用方向。
- `SC_VC02B_IntegrationReady.unity:12016-12045` 有场景级 `InventorySession_VC02B` 与 coordinator；`:12046-12135` 保存 31 个目录引用、团队/箱子有限初值和 3 单位访问距离；`:12151-12163,12208-12225,12402-12451` 有背包视图、物品使用、HUD bridge、F 交互及共享输入安装器接线。`:16178-16222` 的控制会话保存恰好两名角色。`:11755` 为 `VC02B_TestChest`；`:13136-13155` 为 NPC 样板。另有 `SC_CharacterPresentationCompare` 和音频候选场复用了库存会话，但这些都是候选场，不是独立的商店场或正式持久化入口。
- 本机 `Assets/OSSASTRA/Prefabs/Characters/` 存在 `PlayerCharacter.prefab` 与 `PlayerCharacter_BlueFiveFace.prefab`。基础 `PlayerCharacter.prefab:438-460` 的 `CharacterRoot` 绑定主要组件，`characterDefinition` 默认空；`:679` 保存共享 `inputLayout`。双角色的装备定义和 `CharacterEquipment` 是候选场中的实际装配（`SC_VC02B_IntegrationReady.unity:10987-10990,18806-18809`），不能仅凭基础 Prefab 宣称完整交易角色已装配。`PlayerCharacter_BlueFiveFace.prefab:102` 是基于基础角色的变体引用。对场景/Prefab 任何后续改动须由唯一 Unity writer 通过 Unity MCP/Editor API 完成。

## COM 接线清单（建议；尚未实施）

| 既有文件/资产 | 必要接线或新增边界 | 原因 |
|---|---|---|
| `Characters/Core/Inventory/InventoryLedger.cs`、`InventoryContracts.cs` | 增加可试算、一次提交的交易批次/库存扣出和版本校验，或在同层新纯服务实现等价原子提交；保持装备 payload 身份与请求去重。 | 当前 `Grant`/`Transfer`/消费相互独立，不能组成原子买卖。 |
| `Inventory/InventorySession.cs` | 将交易与现有唯一 Ledger、当前控制本体、访问代际和容器权限相连；明确双角色限制和商人库存属于哪个所有者。 | `:64` 恰好两角色、`:68-80` 固定容器、`:117-150` 资格门。 |
| `Characters/Runtime/ItemDefinitionSO.cs` 与现有 VC02B 内容资产 | 定义可售/不可售、买价/卖价、溢出及未定价处理；保留 `trade-coin` 单一数量权威或经用户决定迁移。 | `sellValue` 目前只是元数据，币是普通库存物。 |
| `Npcs/Runtime/NpcRoot.cs`、`Npcs/Core/NpcInteraction.cs` 与新商人组件 | 将合法 NPC 回执关联商人身份/店铺配置；不要把店铺状态硬塞入通用 NPC 根。 | 当前 NPC 只回应、计数；无交易资格/存货。 |
| `Inventory/InventoryInteraction.cs`、`Characters/Runtime/PlayerInputLayoutSO.cs`、`PlayerInputLayoutInstaller.cs`、UI 捕获端口 | 在唯一 F 上下文加入商人优先级、开关交易 UI 和捕获释放；共享可配置 Action。 | 目前 F 只拾取/容器/篝火；开发 NPC 按钮不是玩法入口。 |
| `SC_VC02B_IntegrationReady.unity` 或新独立候选场；可能的角色 Prefab/商人资产 | 用 Unity 编辑器接入商人组件、配置、UI、货架和已有会话引用，并做真实输入验证；保护现有 dirty 场/Prefab。 | 当前只有库存/箱/普通 NPC 样板。 |
| 新 OSSASTRA 保存协调层（先定义，不假定可复用旧 DRPG 或插件接口） | 明确币、队伍/商人库存、装备 ID/耐久/能力 payload、地面包、请求去重/未完成预留的保存与恢复契约；加载应先于 `InventorySession.Start` 播种或明确替代播种。 | 当前没有 OSSASTRA 玩法存档入口。 |

上述“必要”指完整交易闭环实施时必须解决的接线点，不是本次 COM-00 已实现内容，也不是对具体实现形式的最终批准。静态检索阴性结果仅覆盖本机 `HEAD` 加上述既有 dirty 状态；后续实施须重新核状态并做 Editor/Play 实测。
