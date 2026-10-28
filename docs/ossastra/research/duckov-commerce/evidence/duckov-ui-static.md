# 鸭科夫交易与容器 UI 的静态证据

状态：`DIRECT_CODE` 仅用于下面的字段、调用和分支；**没有运行画面、Prefab 布局或输入设备实测**。来源为本机 `TeamSoda.Duckov.Core.dll`，SHA-256 `298D5D5885427632D5A94B2F3CE587F8EBC9528EC71E575A475158C326ECAE8F`。用上一轮 `_work/metadata-scan` 对固定 token 做只读 IL 检查；偏移只适用于此指纹。

| 代码锚点 | 可以支持的最窄结论 | 不能由此推出 |
|---|---|---|
| `StockShopView` 字段 `0x040015BB–0x040015D4` | 该 View 有物品详情、玩家/宠物/仓库 `InventoryDisplay`、商店条目、库存文本、商人名、交互按钮、现金限制和拒收提示、价格/锁状态、倒计时等绑定位。 | 这些区域在屏幕上的矩形、锚点、层级或全部实际启用状态。 |
| `StockShopView.Setup(StockShop)` `0x06001DB7/IL_0023–012E, 0147–01BB` | 从主角物品取得背包，按条件设置宠物/仓库显示；遍历 `Entry.Show` 生成商店条目，设置 `TradingUIUtilities.ActiveMerchant`。 | 三个玩家容器在所有关卡都同屏显示。 |
| `StockShopView.OnInteractionButtonClicked` `0x06001DAA/IL_001F–0086` | 选中玩家物品时调用 `StockShop.Sell`，选中商店条目时走 `BuyTask` 或解锁确认。 | 按钮点击一定完成交易；业务成功仍要看商店方法返回和后续状态写入。 |
| `StockShopView+<BuyTask>d__58.MoveNext` `0x06002C5A/IL_002C–002D` | 此 UI 的该买入调用给 `StockShop.Buy(int,int)` 传数量 `1`。 | 其他 UI 或代码入口也只允许单件；库存底层的多件能力需单独追。 |
| `StockShopView.RefreshInteractionButton` `0x06001DB8/IL_003D–0223` | 按 `CanBeSold`、解锁、价格、资金是否足够、库存数等更新按钮可交互和显示文本。 | 这就是提交时的最终资格校验，或具体错误提示样式。 |
| `InventoryDisplay.OnSortButtonClicked` `0x060022A2/IL_0001–002B` | 显示可编辑、目标存在且未 Loading 时调用 `Inventory.Sort()`。 | 整理算法最优、可取消或不会覆盖并发变化。 |
| `InventoryEntry.OnDrop` `0x060022D5/IL_0001–017C` | UI 放下事件读 `IItemDragSource`、Sticky/目标容器/按键；可能打开 `SplitDialogue.SetupAndShow`，或走 `Detach`、`Inventory.AddAt`、`Item.Combine`。 | 所有失败路径都守恒；需要逐分支研究结果返回和原归属。 |
| `LootView.OnOpen` `0x06002546/IL_001F–01EA` | 设置角色装备槽、玩家/宠物与目标容器显示，条件显示排序和筛选，刷新详情/容量。 | 具体布局或容器内容已经保存。 |
| `LootView.OnPickAllButtonClicked` `0x06002554/IL_0001–00B8` | 对目标容器项目尝试 `TryPlug` 或 `SendToPlayerCharacterInventory`。 | 满包时具体哪些项目成功，或按钮会给统一结果回执。 |
| `LootView.OnStoreAllButtonClicked` `0x0600253C/IL_0001–0085` | 在目标不是 `PlayerStorage.Inventory` 时，对角色包末尾至起始格项目调用 `AddAndMerge`。 | 失败时有事务回滚；与上一轮 ITEM-002 的疑点需合看。 |
| `LootView.OnClose` `0x06002547/IL_0028–005E`、`InventoryView.OnClose` `0x060024DC/IL_0022–003F` | 关闭会话时均检查并取消活动的 `SplitDialogue`；LootView 还停止箱子交互并清目标引用。 | Esc 在所有弹层中的精确退栈顺序。 |
| `View.OnCancel(UIInputEventData)` `0x060025CC/IL_0001–0039` | Cancel 事件转给当前活动 View；未被消费时尝试 `TryQuit`，随后 `Use()`。 | 具体键位、同一帧世界输入是否被阻断。 |

这些锚点可作为原作**交互结构**参照，不构成“高度还原布局”的视觉证据。COM-00 的 UI 状态卡应把原作矩形、焦点、Esc/拖拽/数量输入、错误反馈与重开表现标为待动态观察；不得凭字段名补画成原作界面。
