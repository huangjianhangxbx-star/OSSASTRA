# 武器空槽恢复、数字键切换与 R 键装备修复报告

## 修改的文件

### 1. WeaponEquipStateMachine.cs — IsActive 修复
- **旧**: `return dataBus != null && dataBus.isAlive && (HasActiveWeapon || hasPendingSlotSwitch)`
- **新**: `return dataBus != null && dataBus.isAlive`
- 状态机始终活跃，空槽时能消费数字键请求

### 2. CharacterWeaponInventory.cs — 统一激活槽所有权 + 内容同步
- Initialize: 从 `packageSystem.WeaponSlots.ActiveSlotIndex` 初始化 `activeSlotId`
- Initialize: 订阅 `OnWeaponSlotChanged` + `OnActiveWeaponSlotChanged`
- 新增 `OnPackageActiveWeaponSlotChanged`: 镜像 PackageSystem 的激活槽变化，触发 `ActiveWeaponChanged`
- 重写 `OnPackageWeaponSlotChanged`: 不再自动找下一槽，允许空激活槽，只有激活槽变化时触发事件
- 新增 `_runtimeDataCache`: 按 `instanceId` 缓存 `WeaponRuntimeData`，拖拽换槽不丢失弹药/耐久
- 新增 `FindOrCreateRuntimeData`: 复用缓存的 RuntimeData
- `TrySetActiveSlot`: 有 PackageSystem 时路由到 `packageSystem.SetActiveWeaponSlot`，由回调完成镜像

### 3. CharacterPackageSystem.cs — SetActiveWeaponSlot 加固
- 越界检查
- 空槽拒绝
- 相同槽直接返回 true 不重复发事件

### 4. CharacterEquipmentController.cs — 订阅 ActiveWeaponChanged
- Initialize: 订阅 `weaponInventory.ActiveWeaponChanged`
- 新增 `OnActiveWeaponChanged`:
  - 新武器 null: 卸载视觉，设 Cancelled
  - 新武器非 null: 挂载模型，设 Cancelled（保持隐藏，由数字键/R取出）

## Play Mode 验证日志

```
=== Initial State ===
PS ActiveSlotIndex=0
INV ActiveSlotId=Slot1
Slot0: PS=狼蛛1 INV=has
Slot1: PS=empty INV=empty
Slot2: PS=empty INV=empty
IsActive (WeaponEquipSM): True
weaponEquipState=Cancelled
ActiveWeapon=狼蛛1

=== TEST: Request Slot2 (empty) ===
After request: weaponSwitchInputRequestType=SelectSlot requestedWeaponSlot=Slot2

=== After 1 frame ===
weaponSwitchInputRequestType=None       ← 请求被消费
weaponEquipState=Cancelled              ← 拒绝切换（空槽）
PS ActiveSlotIndex=0                    ← 激活槽未变
INV ActiveSlotId=Slot1                   ← 镜像一致

=== TEST: Request Slot1 (has weapon, current, Cancelled) ===
After request: requestType=SelectSlot slot=Slot1

=== After Slot1 request consumed ===
requestType=None                        ← 请求被消费
weaponEquipState=Equipped               ← 取出武器
ActiveWeapon=狼蛛1

=== TEST: Move weapon from Slot1 to backpack ===
Before: WS0=狼蛛1 activeSlot=0
Unequip result: True
After: WS0=empty activeSlot=0           ← 激活槽保持不变
  INV ActiveSlotId=Slot1 ActiveWeapon=null  ← 允许空激活槽
  weaponEquipState=Cancelled

=== TEST: Press 1 (Slot1 empty) ===
After request: requestType=SelectSlot
weaponEquipState=Cancelled              ← 拒绝，不切换
activeSlot=0                            ← 仍为 Slot1
INV ActiveSlotId=Slot1

=== After empty slot request ===
requestType=None                        ← 请求被消费

=== Put weapon back in Slot1 ===
[ExternalWeaponMount] Weapon sorting applied: MainBody=5, Weapon=7, Hand_L2=10
[CharacterEquipmentController] 挂载武器完成。 Weapon=狼蛛1
Equip result: True
WS0=狼蛛1
INV Slot1=has
ActiveWeapon=has

=== Press 1 (Slot1 has weapon, Cancelled → equip) ===
requestType=None
weaponEquipState=Equipped              ← 成功取出

=== VERIFICATION SUMMARY ===
1. IsActive always True: True           ✅
2. Empty slot request consumed: None    ✅
3. Active slot stayed at Slot1 when empty: activeSlot=0  ✅
4. Weapon put back in Slot1: WS0=狼蛛1  ✅
5. Press 1 → Equipped: state=Equipped   ✅
6. PS and INV active slots match: PS=0 INV=Slot1  ✅
```

## 编译结果
0 error, 33 warnings（全部为项目中已有的无关警告）
