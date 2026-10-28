# Unity 依赖、唯一数据所有者与根结构

本文件是待审实施结构，不是当前工程已经接线的功能。现状锚点详见 [Unity 静态审计](evidence/unity-current.md)；版本与 dirty 现场见 [基线](01-资料基线与缺口.md)。

## 唯一权威与可复用端口

| 职责 | 当前权威/入口 | 对交易的结论 |
| --- | --- | --- |
| 物品实例、数量、格位、装备 payload | `Characters/Core/Inventory/InventoryLedger.cs:12–38,41–67`；`InventorySession.cs:31` 持唯一 Ledger。`Grant/Transfer/CommitSort` 在试算副本上验证后一次提交。 | 沿用此 Ledger；商店/Presenter 不创建第二份团队包、装备库存或币余额。现有单次命令无法把扣币与发物、货架库存做一个交易。 |
| 实体货币 | `Assets/OSSASTRA/VC02B/trade-coin.asset` 是 `category: currency` 的普通物品，`sellValue:-1`；无独立 Wallet 消费者。 | 默认保留 `trade-coin` 作为唯一支付数量权威，明确支付容器范围。旧原型的 `Economy.gold` 不迁入新钱包。改为账户余额须另有用户决定和一次性迁移。 |
| 物品定义与价格 | `Characters/Runtime/ItemDefinitionSO.cs:20–32` 有 `sellValue`，其 Tooltip 明确仅元数据。 | 把定义冻结成纯数据传给交易核心；买价倍率、拒收、货架、刷新由独立配置，不写回 SO 或用 UI 文本当报价。 |
| 当前角色、容器资格 | `InventorySession.cs:117–150` 验当前本体、同层/距离/视线、装备动作资格；`Coordinator.Current` 与 `ControlRevision`。 | 商店会话、取放和确认时再次校验；身份/距离是宿主适配提供的短时快照，核心不依赖移动、攻击或 Spine。 |
| 装备归属 | `InventorySession.Slots` 只读 Ledger 手槽；`CharacterEquipment` 绑定该投影。 | 带装备 payload 的交易须明确准入与销毁/归属；先只允许经 Ledger 绑定的合法实例，不能从未绑定的旧兼容装备复制一份。 |
| 一键整理 | `InventoryLedger.cs:158–194` 已有预算内 `PlanSort/CommitSort`、过期修订/代际/数量/装备校验；`InventoryView.cs:85,111,188–189` 已接团队与外箱按钮，并提示可行未证明最优。 | 这是已有可用入口。交易迁移要保留并对齐原型的取消、可行/最优状态与并发旧计划政策，不写成从零新增。 |
| 单键交互 | `InventoryInteraction.cs:15–51` 是现有 F 上下文，先拾取手动掉落，再容器/篝火；`NpcRoot.TryInteract` 目前仅开发按钮调用。 | 在一个上下文仲裁商人、对话、拾取、箱子，避免新增第二个 F 监听。优先级按 [UI 契约](04-UI布局与交互契约.md)验收。 |
| 输入捕获 | `CharacterControlCoordinator.SetInputCapture/SetInventoryCapture`，`CharacterInputHandler` 与 `InventoryView` 已提供 owner 模式。 | CommerceUI 使用既有捕获端口；关闭、死亡、换控、失焦、换场时只释放自己的 owner。角色控制算法和音频不改。 |
| 持久化 | OSSASTRA Scripts 未发现玩法 Save/Load；`InventorySession.Start` 每次新建、播种；第三方 Easy Save/Pixel Crushers 包存在。 | 增加统一快照及恢复编排，不把包存在或重新播种当存档。详细政策见 [保存契约](06-持久化与失败恢复契约.md)。 |

`InventorySession.cs:64` 强制协调器恰有两名角色，`ReserveOwnerId` 固定索引 1，`InventoryView` 固定画两人。普通商人不可作为第 3 名 `CharacterRoot` 塞进该会话；商人身份与货架状态应在独立商店域，并通过唯一 Ledger 的库存事务接入玩家。若未来扩队伍，另设有范围的会话/UI工作，不借交易隐式改角色控制。

## 三种根的职责

1. **`CommerceService`（候选名，场景中只绑定一次）**：组装目录、商店状态、交易命令、持久化参与者；读取现有 `InventorySession.Ledger`。场景已经有唯一会话，不再新建另一份 `InventorySession` 或同职责 `CommerceRoot`。如果后续发现已有服务组合根能承载注册，就在该组合根注册此服务，名称不构成新增理由。
2. **`NpcRoot` + `MerchantShop` 能力**：保留原 NPC 身份和 `TryInteract`，商店组件仅提供店铺模板、稳定场景实例 ID、交互锚点和店铺入口。一个 Prefab 可配目录与显示；复制两个时店铺状态/存档键独立，显式共用配置只共用静态定义。
3. **`CommerceUIRoot`**：面板、焦点、提示和输入捕获生命周期；只持短时会话 token、只读快照和选中项。芙蕾在现有角色身份下挂 Dialogue/Shop 能力，不能新增一名芙蕾实体。

服务逻辑可在无角色模型/音频的纯规则测试中运行；正常地图的 `InventorySession`、角色查询和 F 输入仍是一次性宿主依赖。所谓“只放商人 Prefab”是指**地图已完成公共安装之后**，不是任意空场景零配置。

## 目录与编译方向（候选）

```text
Assets/OSSASTRA/Scripts/Commerce/
  Core/              CommerceCommand, Quote, MerchantStock, DialogueGate 等纯规则
  Runtime/           CommerceService, MerchantShop, 角色/NPC/库存桥接
  Presentation/      CommerceUIRoot, 商店/对话/数字输入 Presenter
  Persistence/       CommerceSnapshot, 版本验证与文件适配
  Editor/            商店实例 ID 和缺配置校验
Assets/OSSASTRA/Commerce/
  Definitions/       店铺目录、价格和对话条件资源
  Prefabs/           商人/货柜、箱子、掉落和 UI 示例
  UI/                星骸自有素材与布局资产
  Scenes/            独立验证场
```

现有唯一 `.asmdef` 是 `Characters/Core/Ossastra.CharacterCore.asmdef`，`noEngineReferences:true`。建议让新增纯 `Commerce.Core` 程序集**单向引用** `Ossastra.CharacterCore`，运行时/Unity 资源适配留在默认程序集；`CharacterCore` 不引用 Commerce，`ItemDefinitionSO` 先冻结为中立定义再传给核心。要给 Ledger 增交易批次能力，在其原程序集定义不含商店概念的最小原子变更入口；不可由纯核心反向调用 MonoBehaviour。实际 asmdef 名称、自动引用与 Unity/Tuanjie 编译兼容性需在实施时先用小编译门验证，不为目录整齐移动既有库存文件。

## 预计必要变更与保护范围

| 既有文件 | 最小必要改动 | 不应顺手做的事 |
| --- | --- | --- |
| `InventoryLedger.cs`、必要的 `InventoryContracts.cs` | 增通用试算/提交批次，支持指定数量扣除、授予、容器修订/请求去重、容量与装备 payload 复核；向商店状态提供可与 Ledger 同次提交的协调点。 | 另建镜像库存；直接串联 `Grant`/`Transfer` 当交易；修改角色动作/装备磨损算法。 |
| `InventorySession.cs` | 注册恢复入口并在播种前选择加载或初始授予；公开受限的当前访问/币容器/落地记录适配。 | Prefab 启用即重置库存；绕过双角色校验；改旧角色控制策略。 |
| `InventoryInteraction.cs` 与现有 `NpcRoot` 边缘 | 接入单一 F 仲裁与商店能力，保持正常箱/拾取路径。 | 额外全局键盘监听、让商人继承玩家根。 |
| `InventoryView.cs` / UI 组合 | 保留现有背包与整理能力，增交易上下文和输入捕获、详情/数字框。 | UI 自己改币/货物或让点击穿透攻击。 |
| `ItemDefinitionSO.cs` 与 VC02B 内容 | 仅在现有元数据不够表达拒售/价格时新增字段或独立 shop 定义；维持旧资产兼容。 | 改现有物品身份/装备数据，批量重写 SO。 |
| 场景/Prefab | 用 Unity Editor/MCP 在专属验证场装好服务、UI、商人，之后对正常地图做一次公共接线。 | 文本批量改已有 dirty 场景，覆盖用户编辑器现场。 |

正式实施前重新读取项目 `AGENTS.md`、`docs/agent-sync/TASK_REGISTRY.md` 与目标 diff，由唯一 Unity writer 占用必要路径；本轮只有只读审计。保护 `CharacterInputHandler`、`CharacterControlCoordinator` 的行为策略和 `GameAudioRoot` 等音频代码；若实测发现现有公开捕获端口不足，先列具体缺口与最小文件变更，不暗中扩大范围。
