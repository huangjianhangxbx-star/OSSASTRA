# Three.js 贸易与背包 UI 静态证据（COM-00）

## 范围和复核方式

- 固定源码提交：`bb969cc88591fd8e93e4c939cb52719dd652a948`，路径均相对于 `E:\WORLDCREATOR\XingHaiHuiLang\Origin`。可用 `git show bb969cc88591fd8e93e4c939cb52719dd652a948:game/src/<文件>` 复核。**以下是源码可证实的界面契约，不是本轮运行截图，也不是《逃离鸭科夫》原版 UI。**
- 本轮另一份只读 QA 将 `work/OSSASTRA/VC01-20261021/prototype-c21/game/src` 的 245 个文件与固定提交的 `game/src` 逐个比较，内容均为相同 Git blob。因此下述 UI 源码也代表该本地快照的 `src`；它的依赖、构建配置和运行结果不能由此推定相同。当前提交与线上 `main` 的对应性见同目录 `threejs-current.md`。
- 读取了 `backpack-panel.ts`、`inventory-layout.ts`、`inventory-panel.css`、`merchant-view.ts`、`role-interaction-panel.ts`、`role-interaction.css`、`dialogue-panel.ts`、`dialogue.css`、`core/role-interaction.ts`、`core/dialogue/content.ts`、`core/merchant-shop.ts`、`core/item-commerce.ts` 和版本内测试。本文的源码审计没有依赖运行画面；后续浏览器取景另记在[定向观察](prototype-ui-observation.md)，整个过程未改生产代码。

## 布局和视觉的源码事实

| 层/区域 | 当前源码契约 | 固定提交锚点 |
|---|---|---|
| 背包窗口组 | `#backpack-panel` 是全屏固定定位透明覆盖层；窗口绝对定位，可独立显隐。通常有玩家背包、安全栏、详情、外部容器四个区域。 | `game/src/backpack-panel.ts:25-30`; `game/src/inventory-panel.css:1-3` |
| 普通背包 | 以 `min(width/1434,height/804)` 缩放；玩家窗口基准 `(12,20,440×770)`，安全栏 `(456,20,97×208)`，详情 `(565,20,448×350)`，外部容器 `(width-391×scale,92×scale,365×217)`；具体 `x/y` 与窗口大小的缩放由 `layout()` 设置。 | `game/src/inventory-layout.ts:1-4`; `game/src/backpack-panel.ts:59` |
| 贸易三列 | 贸易时重排为玩家 `(18,25,435×740)`、详情 `(475,25,440×440)`、货架 `(941,55,475×470)`，坐标乘同一个 scale；右边缘在基准宽 1434 时为 1416。安全栏、装备行、后备和商人状态条隐藏。 | `game/src/backpack-panel.ts:59`; `game/src/inventory-panel.css:83-89` |
| 左列内容 | 标题“背包”、团队背包“整理/取消”、5×2 团队格、负重条和状态反馈。贸易模式只隐藏装备、安全、后备，团队格与负重仍在。团队网格逻辑宽 414 CSS 像素，`414 / columns` 为格距；贸易模式并未换掉原背包数据结构。 | `game/src/backpack-panel.ts:25-29,82,85-87`; `game/src/core/inventory-model.ts:10`; `game/src/inventory-panel.css:84-85` |
| 中列内容 | 无选择时详情整个隐藏；选货架商品或己方物品后出现“交易”详情。含图标、品类标签、库存/重量/描述、可选效果、数量框、总价与 `F` 购买/出售按钮。图在左、事实/描述在右；交易详情最高 440px。 | `game/src/backpack-panel.ts:88-95`; `game/src/merchant-view.ts:6`; `game/src/inventory-panel.css:95,101-111` |
| 右列内容 | 商人名、下次刷新的 `mm:ss`，下面为独立货架；五列商品网格，每格有图、名称、库存、买价，按稀有度着色。示例商人初始 5 种食物与 5 种贵重品，刷新周期为模拟时间 300 秒。芙蕾茜娅个人商店会显示“暂无商品”。 | `game/src/backpack-panel.ts:92`; `game/src/merchant-view.ts:5`; `game/src/core/merchant-shop.ts:7-10`; `game/src/core/role-interaction.ts:11-16`; `game/src/inventory-panel.css:89-94` |
| 视觉风格 | 背包基本色为暗底和绯红边，商品背景使用稀有度色；贸易详情为蓝灰暗底，货架为蓝绿色，确认按钮为绿色；格子有圆角，但源码注释明确逻辑格距与命中矩形不变。属于原型当前视觉事实，不应推断为原版游戏皮肤。 | `game/src/inventory-panel.css:1-26,29-54,64-75,83-111` |

`merchant-ppt.spec.ts:2` 的版本内测试意图是三列互不重叠、购买数量 2、库存减少、直接出售、库存刷新和截图；本轮常规测试入口曾停在加载页，未跑通该完整故事。随后按 `?scene2d=1` 定向取景成功，见[原型 UI 观察](prototype-ui-observation.md)；它只证明三张指定界面实际出现，不证明买卖已提交。

## 可见状态卡与交互

| 状态 | 可见/动作 | 切换、关闭、失败反馈证据 |
|---|---|---|
| 近距离角色提示 | 角色附近出现 `F` 交互提示；菜单有商店、对话、送礼物、角色装备四项。普通商人只开放商店；芙蕾茜娅开放四项，但后两项点选只显示“后续开放”。菜单为 220×320px 弧形，靠屏幕边缘翻转，位置随目标更新。 | `game/src/core/role-interaction.ts:6-28`; `game/src/role-interaction-panel.ts:15-36`; `game/src/role-interaction.css:1-20` |
| 芙蕾茜娅对话 | `SAMPLE` 标记；台词、可选项、`F/Enter` 继续，方向键切选，`Escape` 关闭。位置跟随角色，靠近屏幕上沿时气泡改在下方。内容是当前样例对话，不是完成的角色剧情。 | `game/src/dialogue-panel.ts:8-21`; `game/src/dialogue.css:1-13`; `game/src/core/dialogue/content.ts:2-19` |
| 普通背包/外部容器 | `Tab` 开关背包；近处外部容器以右侧格子显示，可“整理箱子”。选中物品显示常规详情、转移数量、使用/卸回/交给伙伴等条件操作；`Ctrl` 快速转移仅团队包与当前容器。 | `game/src/backpack-panel.ts:25-42,52-68,83-95` |
| 商店初始 | 角色菜单点击商店后打开背包窗口组、右列货架；未选物时中列隐藏。个人商店为空货架。按 `Escape` 关闭背包；商人距离/身份失效时 render 清除商人会话。 | `game/src/role-interaction-panel.ts:25-28`; `game/src/backpack-panel.ts:44,53,84,92-95`; `game/src/merchant-view.ts:5` |
| 购买详情 | 点击右列商品建立有商店 revision/报价的 quote，数量重置为 1；中列显示商品库存、单价×数量及购买键。可点击按钮或在无输入焦点时按 `F`；成功提示“购买完成，物品已入背包”，失败显示底层原因。无库存/无正价时按钮禁用；底层再次校验钱币、报价、空间。 | `game/src/backpack-panel.ts:32,40-45,57,92-95`; `game/src/merchant-view.ts:6`; `game/src/core/merchant-shop.ts:12-13` |
| 出售详情 | 选中左列己方物品进入出售详情；可输数量并点击确认或按 `F`。成功提示“出售完成，钱币已入背包”；不收购物品的确认键禁用。实际命令只出售选中的具体实例。 | `game/src/backpack-panel.ts:40-45,57,81,88-95`; `game/src/merchant-view.ts:6`; `game/src/core/item-commerce.ts:17-24` |
| 一键整理 | 左列“整理”触发 Worker，显示“正在整理 · 外界照常运行”和“取消”。1 秒内 Worker 未完成会用有界求解器回退。提交前检查容器 revision、物品数量守恒及摆放合法性；成功提示最优/普通完成，失败或取消提示“库存保持”或旧结果丢弃。 | `game/src/backpack-panel.ts:69-78`; `game/src/core/inventory-pack-commit.ts:4-5`; `game/tests/inventory-sort-fix.spec.ts:2-23` |
| 拖拽/手动丢弃 | 6px 移动后出现幽灵与合法/非法放置预览；放到格子按所填数量转移，放到场景会丢到鼠标对应世界点。拖到商店区不会出售，而提示“点击物品后在交易详情出售”。 | `game/src/backpack-panel.ts:36-39,60-63`; `game/src/inventory-panel.css:25-26` |

## 迁移时要显式处理的差异与缺口

1. **当前售卖 UI 是选中单件直接卖。** `inventory-panel.css:79-81` 尚有 `.inv-sale-row`/footer 样式，`core/item-commerce.ts:32-42` 尚有 `sellBatch`，但当前 `backpack-panel.ts:25-45,92-95` 没有待售区 DOM 或批量出售调用。不得把历史样式/函数写成当前已接通的交互。
2. **出售数量提示与实际实例限制可能不一致。** `merchant-view.ts:6` 用团队/安全/后备全部同定义数量设置出售输入的 `max`，而 `item-commerce.ts:21-24` 的 `sellItem`/`exchangeItem` 只处理选中实例；输入超过该实例数量会被底层拒绝。Unity UI 契约应决定显示选中实例数、合并售卖，或批量报价，不应照搬这个矛盾。
3. **界面还没有原版动态对照。** 本文件仅说明 Three.js 当前源码；没有原版《逃离鸭科夫》的交易窗口运行截图、状态切换录屏、拖拽/取消与极端失败画面，也没有核对到用户提及的 PPT 原文件。贸易三列使用了源码中“PPT”注释与测试名，不能推断 PPT 内容已被复核。
4. **测试与本轮观察范围不同。** `game/tests/merchant-ppt.spec.ts` 与 `game/tests/role-interaction.spec.ts` 提供既有测试意图及预期状态；它们的文件字节与固定提交相同。本轮 `?scene2d=1` 已观察背包、商店和芙蕾菜单，但未执行完整买卖/对话故事，也未量出 DOM 矩形。源码中的 `SAMPLE`、示例目录与 30kg 实验参考容量不能作为成品配置。

