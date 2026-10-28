# 鸭科夫交易与物品闭环：定向静态证据

状态：`DIRECT_CODE`（只证明所列条件分支中的静态代码与写入顺序）；2026-10-28 复核。本文件是自有分析摘要，不包含游戏 DLL、完整反编译代码、资源或存档。未启动游戏，未读取玩家存档或商店资源配置。

## 来源与复核方法

本机安装：`D:\steam\steamapps\common\Escape from Duckov`。R0 记录 Steam build `24013657`、`Info.ini` version `2.3.30`、Unity `2022.3.62f2`、Mono。2026-10-28 对原文件重新计算 SHA-256，与 [R0 基线](../r0-r1/00-基线/版本与取证.md)一致：

| 程序集 | SHA-256 |
|---|---|
| `TeamSoda.Duckov.Core.dll`（下文 Core） | `298D5D5885427632D5A94B2F3CE587F8EBC9528EC71E575A475158C326ECAE8F` |
| `ItemStatsSystem.dll`（下文 ItemStats） | `A276E15C022F71B2214BD05E1B9B0F2E620C16561DF576FED0B79C2FE4402E60` |
| `Assembly-CSharp.dll` | `E5F6F893763D3BFDD49C4C4E778F54562CAB2EF237023C2C87C3806A8895E8CF` |

方法均由现有只读 元数据/IL 扫描器（本机中间材料，未发布）以 `程序集 + SHA + MethodDef token` 定位，并逐个定点反汇编复看。扫描器不执行游戏代码，不解析异常处理表、Unity 序列化配置或运行时虚派发。因此 `leave`/异步异常路径只记可见调用，不把“没看到回滚调用”等同于已证实的运行故障。

## 商店配置、报价与刷新

| 结论 | 方法与 IL 锚点 | 范围 |
|---|---|---|
| 商店按 `merchantID` 从 `StockShopDatabase.GetMerchantProfile` 取配置并构造 Entry 列表；缺配置时记录信息后返回。 | Core `StockShop.InitializeEntries` `0x06001D78`：`IL_000B` 查 profile、`IL_0047` 建 Entry。 | 未读实际数据库资源，不能列举各商人商品/数值。 |
| 报价以物品总原值为起点。买入分支对匹配 Entry 应用 `PriceFactor` 后向下取整；卖出分支采用 `sellFactor`，若有覆盖条目则替换因子，再向下取整。 | Core `StockShop.ConvertPrice` `0x06001D86`：`IL_0013` 原值、`IL_003A–0045` 买入因子、`IL_004A–007E` 卖出因子/覆盖。ItemStats `Item.GetTotalRawValue` `0x06000198`：`IL_0001–0059` 基础值、耐久、堆叠数；`IL_0069–0103` 递归计入槽内及子库存物品。 | 价格是具体实例的计算结果，不能把基础价值直接当最终成交价；未读实际 `PriceFactor` / `sellFactor` 配置。 |
| 打开商店 UI 时先做定时刷新检查；若已超过间隔，`DoRefreshStock` 按概率、规则标签设显隐与库存，正常重置分支写 `MaxStock`。 | Core `StockShop.ShowUI` `0x06001D85/IL_000E` → `RefreshIfNeeded` `0x06001D7F/IL_0067–008E` → `DoRefreshStock` `0x06001D80/IL_0024–00A9`。`Start` `0x06001D7D/IL_000C–0027` 另有受 `refreshStockOnStart` 控制的刷新。 | 不能仅凭方法推断每个场景/商人都启用启动刷新。 |

## 买入：付款、发物与库存写入

正常商店 UI 的 `StockShopView.OnInteractionButtonClicked`（Core `0x06001DAA/IL_005C–0077`）在条目解锁时调用 UI `BuyTask`；其异步状态机 `0x06002C5A/IL_0020–002D` 向 `StockShop.Buy(itemTypeID, 1)` **固定传数量 1**。`StockShop.<Buy>d__57.MoveNext` `0x06002C4B/IL_0012–0034` 以 `Busy` 拒绝重入并设置 `buying`；正常返回后 `IL_008A–0091` 清除标志。异常时是否释放标志、UI 如何反馈，需要结合异常处理语义和运行观察再判。

核心状态机 Core `StockShop.<BuyTask>d__58.MoveNext` `0x06002C4D` 的条件顺序如下：

1. `IL_0039–005E` 查找商品 Entry 并检查当前库存至少为 1；`IL_0077–0080` 对不可堆叠实例把请求数量改为 1；`IL_008B–009A` 比较请求数量与库存。`IL_009F–00AB` 存在空样本检查，但此前 `IL_0077` 已读取样本的 `Stackable`，不能断言空样本会被稳健拒绝。入口用的是 `Enumerable.First`，缺 Entry 时也可能以异常结束。
2. `IL_00B4–00C6` 计算样本报价、构造纯金额 `Cost` 并调用 `EconomyManager.Pay(Cost, accountAvaliable, true)`；false 在 `IL_00CB–00CF` 结束为 false。`EconomyManager.Pay(Cost)` `0x06001D52/IL_0002–0026` 先检查足额，再付货币、消费 Cost 中的物品；本商店路径的 Cost 由金额构造。`Pay(long)` `0x06001D51/IL_001A–0075` 可按允许范围使用账户 Money 和物品 ID `451` 的 Cash；现金消费调用结果在 `IL_0055–005A` 被丢弃，这是静态一致性检查点，尚未证明在正常配置可失败。
3. 付款成功后 `IL_00DA–013B` 异步实例化物品，`IL_0144` 写来源标记 `UI_Trade`。`IL_014C` 尝试送主角包；若该方法返回 false，则 `IL_0153–016C` 根据 `LevelConfig.SaveCharacter` 送玩家仓库或在主角处生成地面物。满包并非此方法的直接拒单条件。
4. 发物路径之后 `IL_0178–0184` 调 `Entry.set_CurrentStock(old - amount)`；setter Core `0x06002C3F/IL_0002` 对 `currentStock` 直接 `stfld`，并在 `IL_0012–0018` 通知订阅者。`IL_0194–01C4` 触发购买相关事件/提示，`IL_01C9–0203` 返回 true。

此静态顺序显示付款在异步实例化和入包/入库/掉落之前；本方法没有显式的失败补偿分支。实例化异常、送仓库/掉落失败、保存失败的实际可达性及最终归属均未实测。`BuyTask` 对数量大于 1 的参数只实例化一次且报价不乘数量，库存却按 `amount` 扣减；**当前普通 UI 固定传 1**，其他入口与配置未确认，故不称为已复现的玩家流程缺陷。

## 卖出：物品处置、返款与拒收

Core `StockShop.<Sell>d__59.MoveNext` `0x06002C53` 在 `IL_0015–0041` 检查 Busy、物品非空与 `CanBeSold`。ItemStats `Item.get_CanBeSold` `0x06000113/IL_0001–001D` 对 `Sticky` 或 `NotSellable` 标签返回 false。通过后：

- `IL_0056–005B` 用 `ConvertPrice(item, true)` 计算价格；`IL_0066` 调 `Item.Detach`；`IL_0071` 调 `ItemTreeExtensions.DestroyTree`。本方法自身未把售出物放进商人库存，也未增加货架 Entry；事件订阅者效果未追。
- 若 `returnCash` 为 true，`IL_008B–00AC` 构造物品 ID `451`、数量为卖价的 Cost 并等待 `Cost.Return`。其异步状态机 Core `0x06002C34/IL_0085–01A9` 按堆叠上限创建返还物，`IL_01EE–0208` 尝试送主角包或仓库；`IL_0243–025B` 再返还 Cost 的金额分量并丢弃 `EconomyManager.Add` 的 bool。此分支的金额分量是否为 0 由 Cost 构造参数决定，不把它记作第二次货款。
- 若 `returnCash` 为 false，`IL_010C–0118` 调 `EconomyManager.Add(sellPrice)`，并 `pop` 其 bool；`IL_0119–0138` 随后发出售事件并清 `selling` 标志。`EconomyManager.Add` `0x06001D54` 在 Instance 缺失时可返回 false（见 [ECON-001](../r0-r1/10-证据索引/ECON-001-任务货币奖励调用链.md)）。因此本方法没有用返款成功与否控制售出物的先前销毁；是否会在正常场景出现返款失败，未证实。

## 容器、堆叠、拆分、拾取与丢弃

| 路径 | 精确锚点 | 已证实的最窄结论与缺口 |
|---|---|---|
| 物品入/出库存 | ItemStats `Inventory.AddAt` `0x060000E4/IL_0157,016F`；`Item.NotifyAddedToInventory` `0x0600015A/IL_0002`；`Inventory.RemoveAt` `0x060000E5/IL_0042,004D,00A2–00BC` | 入库修改 `content` 的索引和 `Item.inInventory`；出库把索引置空、通知移除、重算重量。两者均有容量/位置/归属条件；事件不等于已经写盘。完整条件见 [ITEM-001](../r0-r1/10-证据索引/ITEM-001-拾取入包调用链.md)。 |
| 合堆与数量 | ItemStats `Item.Combine` `0x06000185/IL_0000–00F0`；`Item.set_StackCount` `0x06000162/IL_0080–00CD` | `Combine` 自身拒绝空、自身、不可堆叠和 TypeID 不同，计算目标可容纳量并分别写两件的 StackCount；源计数归零后脱离/销毁。此方法未直接比较耐久/附加变量；其他调用前置条件仍需追，不把“同 TypeID 即可合法合堆”写成全局规则。`set_StackCount` 超上限会截断，小于 1 时走 `DestroyTree`。 |
| 拆分 | ItemStats `Item.<Split>d__249.MoveNext` `0x0600028F/IL_0036–00BB,00C9–01E9` | 拒绝非正数、超现存数与等于整堆；`IL_00C9` **先减少原物数量**，再 `IL_00D4` 异步创建同 TypeID 新物；创建失败在 `IL_013F–0161` 返回 null，方法内未见恢复原计数；成功后复制 Variables 原始数据、设置新堆数量与 inspected。具体失败可达性、调用方补偿需再核。 |
| 地面拾取 | Core `InteractablePickup.OnInteractStart` `0x060007E4/IL_000C` → `CharacterMainControl.PickupItem` `0x0600035C/IL_001D` → `CharacterItemControl.PickupItem` `0x060002CA/IL_0068` → `ItemUtilities.SendToPlayerCharacterInventory` `0x06000934/IL_0054` → `AddAndMerge` `0x06000931/IL_00A1` → ItemStats `Inventory.AddAt` | 仅在主角、`TryPlug=false`、合法空位、`AddAt` 通过的分支证实背包写入。`AddAndMerge` 于 `IL_00A6–00A8` 丢弃 `AddAt` bool 后返回 true，是待验证风险，不宣称已复现丢物。详见 [ITEM-001](../r0-r1/10-证据索引/ITEM-001-拾取入包调用链.md)。 |
| 丢弃 | Core `ItemExtensions.Drop(Item, Vector3, ...)` `0x060008D9/IL_0015–005A,0093–00E1`；`Drop(Item, CharacterMainControl, bool)` `0x060008DA/IL_0026–0043` | 非空物品先 `Detach`，再调用 `CreatePickupAgent` 并处理场景/投掷显示。方法返回 `DuckovItemAgent`；主角重载丢弃其返回。未见此方法中的归属回滚或保存调用；地面代理创建失败后的真实状态仍待运行和资源证据。 |
| 仓库兜底 | Core `PlayerStorage.Push` `0x06000959/IL_0038–00D5,0100–0126`；`PlayerStorageBuffer.SaveBuffer` `0x0600096B/IL_003E–0044` | 可合堆/有空位时推入仓库库存；否则把物品转 `ItemTreeData` 放入 incoming buffer，再脱离/销毁原实例。buffer 另用 `PlayerStorage_Buffer` 保存。`Push` 返回 void，不能把调用成功等同于已入主库存。 |

## 保存与恢复的静态通道

- **商店库存**：Core `StockShop.Awake` `0x06001D77/IL_0001–0029` 初始化 Entry、订阅 `OnCollectSaveData → Save` 与 `OnSetFile → Load`，并立即 Load。`get_SaveKey` `0x06001D79/IL_0000–0010` 为 `StockShop_` + `merchantID`。`GenerateSaveData` `0x06001D87/IL_000D,003A–004B` 采样上次刷新时间及每个 Entry 的 `itemTypeID/currentStock`；`Save` `0x06001D7B/IL_001A–0026` 调 `SavesSystem.Save`。`SetupSaveData` `0x06001D88/IL_000B–00D7` 还原时间和匹配商品库存，并受规则/标签影响重新设置可见状态。相同 `merchantID` 是否用于多个场景实例、是否有共享库存意图，只能查配置/运行，不能由键格式推断。
- **账户货币**：Core `EconomyManager.GenerateSaveData` `0x06001D59/IL_000A–0033` 采样 `money`、解锁商品 ID 和待确认解锁 ID；`Save/Load/SetupSaveData` `0x06001D43/0x06001D42/0x06001D5A` 通过键 `EconomyData` 恢复。它与商店库存使用不同键；静态证据没有跨键原子提交证明。物品 ID `451` 的现金属于物品保存链，不应与账户 Money 重复计余额。
- **仓库与物品树**：Core `PlayerStorage.Save` `0x0600095A/IL_000E–0013` 以键 `PlayerStorage` 调 `ItemSavesUtilities.Save(Inventory,key)`；后者 `0x06001206/IL_0001–0013` 先用 ItemStats `InventoryData.FromInventory` `0x0600024B/IL_000D,0038–006B` 采集容量、格位物品树、锁定索引，再交 `SavesSystem.Save("Inventory/", key, data)`。Core `PlayerStorage.<Load>d__52.MoveNext` `0x06002976/IL_0022–0093` 调 `LoadInventory`；其状态机 `0x06002A78/IL_002C–00DE` 查键、清旧内容并调用 ItemStats `InventoryData.<LoadIntoInventory>d__5.MoveNext` `0x060002AE/IL_0049–00D5`，逐件异步实例化并调用 `AddAt`；实例化/入库失败仅有错误日志，未见整包回滚。ItemStats `ItemTreeData.FromItem` `0x06000244/IL_001B–01C6` 收集子树、类型、变量、槽/子库存关系；不能把 Unity `GetInstanceID` 用作跨会话全局稳定 ID 的已证据。
- **缓存与落盘**：Core `SavesSystem.CollectSaveData` `0x06001244/IL_0000–000F` 调订阅者；`SavesSystem.Save(string,T)` `0x06001239/IL_0029–0035` 调 `ES3.Save` 写当前缓存文件；`SavesSystem.SaveFile(bool)` `0x06001234/IL_0054–005F` 才调 `ES3.StoreCachedFile`。这些通道证明静态保存入口，不证明买卖当场写盘、异常恢复、磁盘一致性或重启后正确复现。

## COM-00 仍须显式保留的缺口

1. 没有动态观察、Unity Prefab/资源配置、真实存档读取；具体商人商品、价格参数、`returnCash`/`accountAvaliable`/刷新开关，以及现金 ID `451` 的实际资源表现待验证。
2. 买入付款后异步创建/仓库/掉落的异常与补偿；卖出销毁后返款失败及异步异常；拆分创建失败；`AddAndMerge` 与 `PlayerStorage.Push` 的 `AddAt` 返回处理，均是**静态一致性调查点**，不得写成已复现玩家 Bug。
3. 商店库存、账户货币、玩家包/仓库/缓冲、地面物各自如何在一次交易后被收集与落盘，以及跨键部分失败后的恢复，需追全局保存调度与运行证据。当前仅证明局部方法和保存通道。
4. 普通 UI 固定单件买入，但其他代码或 Mod 是否传数量大于 1、卖出实际货物去向是否由事件订阅者改变、重复提交和存档键冲突是否可达，都未证实。
