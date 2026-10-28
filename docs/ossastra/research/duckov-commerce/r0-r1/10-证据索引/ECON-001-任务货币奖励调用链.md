# ECON-001｜任务货币奖励到账、通知与保存边界

- 取证对象：本机 Steam 清单 build `24013657`、游戏标识 `2.3.30`；`TeamSoda.Duckov.Core.dll` SHA-256 `298D5D5885427632D5A94B2F3CE587F8EBC9528EC71E575A475158C326ECAE8F`。
- 证据级别：`DIRECT_CODE`，仅证明本机 DLL 的静态条件路径。未启动游戏，未核对具体任务预制体、奖励配置、实际领取结果或磁盘存档。
- 复核方式：用 `MetadataScan`（本机中间材料，未发布） 对上述 DLL 执行 `disasm <DLL路径> <MethodDef token>`；下列 token 均属于这份 DLL，偏移为方法内 IL 偏移。

## 领取条件与两个状态写入

以下调用链以运行时奖励对象确为 `Duckov.Quests.Rewards.QuestReward_Money` 为前提。`Reward.Claim` 的 `callvirt` 证明虚调用位置，不能单凭它证明某个具体任务配置了该子类。

| 步骤 | 方法签名与 token | 关键 IL 与静态结论 |
|---|---|---|
| 1 | `Duckov.Quests.Reward::Claim() -> void` `0x06001EBD` | `IL_0001–0010`：`get_Claimable()` 为 false 或虚调用 `get_Claimed()` 为 true 就返回。`get_Claimable()` `0x06001EB5/IL_0006` 读取所属 `Quest.get_Complete()`。通过后 `IL_0012` 虚调 `OnClaim()`。 |
| 2 | `Duckov.Quests.Rewards.QuestReward_Money::OnClaim() -> void` `0x0600209A` | `IL_0001–0008` 再查 `get_Claimed()`，已领取即返回。`IL_000A` 读 `amount:int`（字段 `0x04001718`），`IL_000F` 转 `long`，`IL_0010` 调 `EconomyManager.Add(long)`。若返回 false，`IL_0015–0017` 直接返回；若 true，`IL_001A` 写 `claimed=true`（字段 `0x04001719`）。子类 getter `0x06002094/IL_0001` 读此字段。 |
| 3 | `Duckov.Economy.EconomyManager::Add(long) -> bool` `0x06001D54` | `IL_0000–000E`：`Instance` 与 null 相等时返回 false。否则 `IL_000F–0016` 取原 `Money`、与参数相加、调用 `set_Money(long)`，`IL_001B–001C` 返回 true。这里是普通 `add`，未见金额非负或溢出校验。 |
| 4 | `Duckov.Economy.EconomyManager::set_Money(long) -> void` `0x06001D46` | `IL_0000` 取旧余额；`IL_0006–0013` 再查 `Instance`，为空则返回；`IL_001A` 把新值写入 `EconomyManager.money:long`（字段 `0x04001591`）。`get_Money()` `0x06001D45` 在实例为空时返回 0，否则读同字段。 |

因此，在实例存在且 `Add` 返回 true 的路径上，先写余额，再写该奖励的 `claimed`。`QuestReward_Money.get_AutoClaim()` `0x06002097/IL_0000–0001` 恒返回 true；此属性并不单独证明某个奖励实例在运行时已自动领取。

## 通知并不等于成功结算

`set_Money` 在字段写入后，于 `0x06001D46/IL_001F–002B` 检查静态 `OnMoneyChanged`；存在订阅者时以旧、新余额调用 `Action<long,long>.Invoke`。没有余额相等时的短路。扫描到 `Duckov.UI.MoneyDisplay`、`CostDisplay`、`ATMPanel`、`AchievementManager` 等订阅点；事件本身不证明 UI 已显示或数据已落盘。

更重要的是，基类 `Reward::Claim` 的 `OnClaim()` 返回类型为 `void`。**在它正常返回后**，基类不复核结算结果，就于 `0x06001EBD/IL_001E` 调用 `Quest.NotifyRewardClaimed(Reward)`，并于 `IL_002E` 在存在订阅者时触发 `OnRewardClaimed`。因此，若 `EconomyManager.Instance` 为空，`Add` 返回 false、货币奖励未写 `claimed`，但基类仍可发出“领取”通知。这是静态代码可见的失败路径，尚未证明实际游戏中会发生；异常中断不在此结论内。`Quest::NotifyRewardClaimed` `0x06001E59/IL_0019–0022` 会检查所有奖励是否已领取，全部已领才清 `needInspection`；其后仍有状态事件调用。

## 保存数据通道与实际触发

| 状态 | 注册、生成与写入锚点 | 可证明的范围 |
|---|---|---|
| 余额 | `EconomyManager::Awake()` `0x06001D3F/IL_0014–001F` 将 `OnCollectSaveData` 注册到 `SavesSystem.OnCollectSaveData`；该回调 `0x06001D40/IL_0001` 调 `Save()` `0x06001D43`。`Save` 用键 `EconomyData`，经 `GenerateSaveData()` `0x06001D59/IL_000A–000F` 把 `get_Money()` 写入 `SaveData.money:long`（`0x040023FA`），再于 `0x06001D43/IL_0010` 调 `SavesSystem.Save`。 | 余额具有独立保存数据通道；`Add` 和 setter 本身均不调用保存。 |
| 奖励 `claimed` | `QuestManager::RegisterEvents()` `0x06001E9C/IL_0023–002E` 将 `QuestManager.Save()` 注册到同一收集事件。`Save()` `0x06001E96` 以 `Quest`/`Data` 为键保存 `GenerateSaveData()`。后者 `0x06001E94/IL_0067,00A7` 遍历活跃与历史任务，调用 `Quest.GenerateSaveData()`；`Quest.GenerateSaveData()` `0x06001E56/IL_00E6–00FC` 虚调各奖励的 `GenerateSaveData()` 并按奖励 ID 加入 `rewardStatus`。货币子类 `0x06002099/IL_0001–000B` 读取、装箱 `claimed:bool`。 | 所属任务进入上述列表并发生数据收集时，该字段有保存通道；具体任务实例和磁盘内容未核对。 |
| 收集与落盘示例 | `SavesSystem::CollectSaveData()` `0x06001244/IL_000A` 调用已注册回调。`BaseSceneUtilities::Save()` `0x06000B37/IL_000A` 调收集、`IL_0010` 调 `SaveFile(true)`；`SaveFile(bool)` `0x06001234/IL_0059` 调 `ES3.StoreCachedFile`。 | 这是已见的保存触发路径，不证明每次奖励领取都会触发它，也不证明全部保存场景都已枚举。 |

`EconomyManager::Load()` `0x06001D42` 读取 `EconomyData` 并调用 `SetupSaveData()`；后者 `0x06001D5A/IL_0014–0019` 直接恢复 `money`。`QuestManager::Load()` `0x06001E97` 读取 `Quest`/`Data`；`Quest.SetupSaveData()` `0x06001E57/IL_013C` 虚调奖励的 `SetupSaveData()`，货币子类 `0x0600209B/IL_0011` 恢复 `claimed`。这些是静态恢复路径，不是存档实测。

`Quest.SetupSaveData()` 同一方法在 `0x06001E57/IL_0169–01A6` 还包含一段条件逻辑：任务已完成、奖励对象存在、尚未领取且 `AutoClaim=true` 时，调用 `Reward.Claim()`。货币子类的 `AutoClaim` 恒为 true，因此**恢复时可能触发新的领取尝试**；实际是否命中、与余额恢复的先后及重复入账防护，需要 R5/R6 按加载顺序继续核查。

## 尚待核验

1. 哪些任务预制体实际配置 `QuestReward_Money`、其 `amount` 值及是否可能为负。
2. 奖励领取、自动领取、场景切换和退出时，各保存触发何时发生；领取后未收集数据前的退出后果。
3. `EconomyManager.Instance` 为空导致通知先于结算的路径是否在实际生命周期可达，以及异常或其他事件订阅者是否改变结果。
4. 恢复已完成但尚未领取的任务时，`Quest.SetupSaveData` 的 AutoClaim 分支会否重复触发货币奖励，以及它与经济余额恢复的顺序。
