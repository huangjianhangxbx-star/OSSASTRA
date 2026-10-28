# 《逃离鸭科夫》代码架构研究

本目录是 R0/R1 原创报告的发布副本。下文保留当时的本机取证语境；用户提供的研究计划、元数据扫描器与原始扫描输出只在本机存档，未随本目录发布。本目录没有原游戏文件或图片。CSV 中的 `report_path` 以此 `r0-r1/` 目录为根解析。

- 本机只读来源：`D:\steam\steamapps\common\Escape from Duckov`
- 研究成果根：`E:\WORLDCREATOR\XingHaiHuiLang\参考游戏研究\逃离鸭科夫`
- 本机基线：Steam 清单 build `24013657`；`Info.ini` 标识 `2.3.30`；Unity `2022.3.62f2`、Mono。Core DLL SHA-256 `298D5D5885427632D5A94B2F3CE587F8EBC9528EC71E575A475158C326ECAE8F`，ItemStats DLL SHA-256 `A276E15C022F71B2214BD05E1B9B0F2E620C16561DF576FED0B79C2FE4402E60`。
- 已完成轮次：**R0 PASS、R1 PASS（仅首轮系统导航与静态主链）**。R2–R8 尚未执行。
- 研究方式：只读检查本机托管程序集的 PE/CLI 元数据与 IL；未启动游戏、未读取玩家存档、未修改游戏或 `Origin` 工程。

## 阅读入口

1. [R0/R1 交接](09-交接/R0-R1-交接.md)：完成范围、重要证据、阻塞、下一轮的精确入口。
2. [版本与取证](00-基线/版本与取证.md)、[程序集清单](00-基线/程序集清单.csv)、[工具与限制](00-基线/工具与限制.md)：本机文件和指纹；换版本须另立基线。
3. [系统地图](01-总览/系统地图.md)、[类型与调用入口索引](01-总览/类型与调用入口索引.csv)、[研究问题及假设](01-总览/研究问题及假设.md)：任务、物品、容器、战利品、商人、撤离、保存的 R1 导航。类型索引是**筛选后的候选入口**，不是全游戏已完成解析。
4. [拾取入包调用链](10-证据索引/ITEM-001-拾取入包调用链.md)：从地图交互到 `Inventory.content`、`Item.inInventory` 的条件路径，以及返回成功与实际写入不一致的静态疑点。
5. [任务货币奖励调用链](10-证据索引/ECON-001-任务货币奖励调用链.md)：到账、领取标记、通知、保存通道与失败分支。
6. [主张登记](10-证据索引/claim-ledger.csv)、[方法索引](10-证据索引/method-index.csv)：可按 `claim_id`、程序集 SHA、方法 token 和 IL 偏移检索；`DIRECT_CODE` 仅指静态代码证据，不等于运行时复现。
7. 用户提供的首轮执行单、研究总计划和证据模板仅保存在本机，未随本目录发布；它们是任务资料，**不是本机取证结论**。

## 当前结论与盲区

- 已用实际 IL 追到一条**有条件的**地图拾取入背包路径：`InteractablePickup` → `CharacterMainControl` → `CharacterItemControl` → `ItemUtilities` → `Inventory.AddAt` → `Item.NotifyAddedToInventory`。`AddAt` 写入 `content` 并设置 `inInventory`，随后发内容变化通知；保存时机和具体 UI 订阅未确认。
- `ItemUtilities.AddAndMerge` 丢弃 `Inventory.AddAt` 的布尔结果，因此它返回 true 不能单独证明物品已写入；实际可达条件和后果留给 R2。
- 已核对 `QuestReward_Money` 在成功分支写 `EconomyManager.money` 和 `claimed`，并定位各自的保存数据通道；具体任务配置和领取后的保存触发未实测。
- 战利品随机时机、装备/仓库所有权、商店购买出售的失败原子性、死亡与撤离结算、重载恢复等仍是 R2–R6 的研究题，不能把目前的类型导航当作这些机制已研究完成。

`_work/` 是本机元数据扫描器、样例验证和原始扫描输出的存放处，未随本目录发布。此处只收录必要的原创研究报告与索引，没有原游戏 DLL、存档、完整反编译源码或资源。
