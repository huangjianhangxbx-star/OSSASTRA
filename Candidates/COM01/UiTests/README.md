# COM-01 展示验证

`UiTests.csproj` 直接编译生产 `CommerceWindowState` 与 `CommerceMenuGate`，覆盖正整数/空白/负数/溢出/全角/实例上限、换物/改数量废弃报价、Esc分层、菜单Installer先执行及精确owner释放。首次RED实测5项失败（合法数量、三层取消和目标替换缺实现），实现后13项通过。root随后在真实Play复现Esc同帧系统菜单抢占和隐藏owner泄漏；新增门禁的4项RED后实现，当前18项通过，真实修复仍由root复验。

```powershell
dotnet run --project Candidates/COM01/UiTests/UiTests.csproj -c Release
dotnet build Candidates/COM01/UiTests/UiCompileCheck.csproj -c Release
```

编译门引用本机现有Unity DLL及本任务新CharacterCore程序集，包含实际Presentation、InventorySession及运行字段、InventoryView及其声音/贸易partial、指针组件，忽略重复导入类型与Unity序列化未赋值告警；不是Editor完整编译或运行证据。不会写Unity场景或SO。

`CommerceUiWindowProbe.cs.txt` 是提供给唯一Unity writer的正式窗口事件探针。前提为自有Play场、已通过正常入口打开非空商店。它调用真实uGUI按钮/数量事件但不成交，验证拒绝旧报价、恢复布局和捕获owner。没有运行此探针前不得报“真实UI通过”；它也不替代F/手柄、数字焦点、Esc跨帧及自然买卖/重启故事。

## 宿主接线

1. `ui.Initialize(inventory, inventory.Skin, host)`，无全局找服务。
2. 单一交互路由打开 `ui.OpenRoleMenu(targetId,name,screenPoint,allowDialogue)`；调用返回后再记录会话控制代际，因为捕获按现有机制递增代际。
3. 已打开窗口时，同一路由调用 `ui.TryConfirmInteraction()`，返回true即消费本次F；打开同帧/数字焦点不成交。
4. `ICommercePresentationHost` 负责真实报价/提交/保存/对话。报价token只映射真实TradeQuote，UI不写Ledger。
5. 普通商人 `allowDialogue=false`；芙蕾使用现有女主身份，`true`。送礼/角色装备菜单仍明确占位，背包的既有功能保留。
6. `ui.SetPersistenceStatus(message,canRetry)` 在关店后保留非模态保存告警；空字符串清除，不捕获世界输入。

所有视图/手感仍需要root的Editor运行、正常输入和截图核验。
