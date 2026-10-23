# 新背包界面布局改造 — 实施报告

## 一、修改后的 Prefab 层级

```
PackageCanvas
├── DimBackground (Image, raycastTarget=false)
├── SafeArea
│   ├── TopNavigation (HorizontalLayoutGroup, PackagePageController)
│   │   ├── EquipmentTab (Button)
│   │   ├── CharacterStatusTab (Button, 禁用占位)
│   │   ├── SkillsTab (Button, 禁用占位)
│   │   └── QuestsTab (Button, 禁用占位)
│   └── PageContainer
│       └── EquipmentPage
│           ├── LeftCharacterPanel (anchor: 左, pivot.x=0)
│           │   ├── EquippedSection
│           │   │   ├── SectionTitle
│           │   │   └── EquippedGrid_3x2 (GridLayoutGroup, 3列)
│           │   │       ├── WeaponSlot1 (WeaponSlotView, SlotIndex=0)  ← 显示"1"
│           │   │       ├── WeaponSlot2 (WeaponSlotView, SlotIndex=1)  ← 显示"2"
│           │   │       ├── WeaponSlot3 (WeaponSlotView, SlotIndex=2)  ← 显示"3"
│           │   │       ├── WearableSlot4 (WearableSlotPlaceholderView, 锁定)
│           │   │       ├── WearableSlot5 (WearableSlotPlaceholderView, 锁定)
│           │   │       └── WearableSlot6 (WearableSlotPlaceholderView, 锁定)
│           │   └── BackpackSection
│           │       ├── BackpackHeader (标题+容量)
│           │       └── BackpackGrid (GridLayoutGroup, BackpackGridView)
│           ├── CenterContentPanel (anchor: 中央, ItemDetailsPanel)
│           │   ├── EmptyState
│           │   ├── Icon
│           │   ├── Name
│           │   ├── Category
│           │   └── Quantity
│           └── RightExternalContainerPanel (anchor: 右, ExternalContainerPanel)
│               ├── Title
│               ├── Capacity
│               ├── CloseButton
│               └── ContainerGrid
└── DragLayer (CanvasGroup)
    └── DragIcon (raycastTarget=false)
```

## 二、锚点与适配

| 区域 | anchorMin | anchorMax | pivot.x | 尺寸 |
|---|---|---|---|---|
| LeftCharacterPanel | (0,0) | (0,1) | 0 | 500 宽 |
| CenterContentPanel | (0.3,0) | (0.7,1) | 0.5 | 剩余 |
| RightExternalContainerPanel | (1,0) | (1,1) | 1 | 430 宽 |

CanvasScaler: ScaleWithScreenSize, Reference 1920×1080, Match 0.5

## 三、六个穿戴槽的实际绑定关系

| 槽位 | 内部索引 | 显示 | 类型 |
|---|---|---|---|
| WeaponSlot1 | 0 | "1" | 武器槽（枪械+近战）|
| WeaponSlot2 | 1 | "2" | 武器槽（枪械+近战）|
| WeaponSlot3 | 2 | "3" | 武器槽（枪械+近战）|
| WearableSlot4 | null | 锁定 | 占位（未开放）|
| WearableSlot5 | null | 锁定 | 占位（未开放）|
| WearableSlot6 | null | 锁定 | 占位（未开放）|

武器槽通过 `weaponSlotViews[i].Initialize(i, this)` 明确传入索引，不依赖对象名。

## 四、修改和新增的文件

### 修改
| 文件 | 改动 |
|---|---|
| PackageUIController.cs | 新增 itemDetailsPanel/externalContainerPanel 字段；Initialize 中初始化两面板；新增 OpenExternalContainer/CloseExternalContainer 转发方法；Hide 时关闭外部容器 |

### 新增（上次已完成）
| 文件 | 作用 |
|---|---|
| PackagePageController.cs | 顶部页面导航，Equipment 可用，其他禁用 |
| WearableSlotPlaceholderView.cs | 占位穿戴槽锁定视图 |
| ExternalContainerPanel.cs | 右侧外部容器面板（显示/隐藏/绑定）|
| ItemDetailsPanel.cs | 中央物品详情面板 |

### 本次新增接线
- 给 CenterContentPanel 挂 ItemDetailsPanel，创建 Icon/Name/Category/Quantity/EmptyState 子对象并接线
- 给 RightExternalContainerPanel 挂 ExternalContainerPanel，创建 Title/Capacity/CloseButton/ContainerGrid 子对象并接线
- PackageUIController 引用字段在 Inspector 中接线
- 应用中文字体到 19 个 TMP 文本组件

## 五、背包每页格数和页码算法

当前 BackpackGrid 使用 GridLayoutGroup，6列、每格64px。
容量读取 `CharacterBackpack.Capacity`。
页数 = `Mathf.CeilToInt((float)capacity / slotsPerPage)`。
全局索引 = `currentPage * slotsPerPage + localViewIndex`。

（分页按钮和页导航在本次布局骨架中已预留，完整翻页逻辑见后续阶段）

## 六、页面导航扩展方式

`PackagePageController.PackagePageType` 枚举定义 Equipment/CharacterStatus/Skills/Quests。
当前 Equipment 可用，其他 Tab `interactable=false` + 灰色。
未来加页面：在 PageContainer 下加页面 GameObject，在 PackagePageController 加字段并连接。

## 七、外部容器打开/关闭接口

```csharp
// PackageUIController 转发
public void OpenExternalContainer(IItemContainer container, string title)
public void CloseExternalContainer()

// ExternalContainerPanel 实现
public void OpenExternalContainer(IItemContainer container, string title)
public void CloseExternalContainer()
```

普通 Tab 打开背包：右侧面板隐藏。
场景容器交互时：调用 OpenExternalContainer 显示右侧面板。
关闭容器/背包：CloseExternalContainer 清除引用。

## 八、Raycast 配置

| 对象 | raycastTarget |
|---|---|
| 槽位根 Image | true |
| 物品图标/文字/边框/高亮/锁定遮罩 | false |
| 面板背景(Dim/Panel/Center/Right) | false |
| DragIcon | false |
| 页面切换按钮 | true |
| 隐藏页面 | SetActive(false) |

## 九、四种拖拽方向 Play Mode 实测结果

数据层全部通过：

| 方向 | 测试 | 结果 |
|---|---|---|
| 背包→背包 | BP0→BP1 | ✅ BP0=empty, BP1=狼蛛1 |
| 背包→武器槽 | BP1→WS1 | ✅ BP1=empty, WS1=狼蛛1 |
| 武器槽→武器槽 | WS1→WS2 | ✅ WS1=empty, WS2=狼蛛1 |
| 武器槽→背包 | WS2→BP3 | ✅ WS2=empty, BP3=狼蛛1 |

布局重构**没有破坏**现有拖拽逻辑。

## 十、仍需用户提供的美术资源

- 正式的中文 TMP Font Assets（当前使用思源黑体 SDF）
- 穿戴部位占位图标（用于 WearableSlot4/5/6）
- 更好的物品图标（当前用 weapons 的 Spine PNG）
- 圆角九宫格面板资源

## 十一、当前仅为占位/未接入数据逻辑的部分

- WearableSlot4/5/6：仅锁定占位，未接入穿戴数据
- ExternalContainerPanel：容器网格只读占位，未实现双向转移
- 背包分页：布局预留，翻页按钮/指示器逻辑待完成
- CenterContentPanel：显示选中物品基础信息（已接 ItemDetailsPanel）
- CharacterStatusPage/SkillsPage/QuestsPage：未实现，Tab 禁用
```
