# CRAFT-01 Core交接

本子任务已完成DS本机研究、Root冻结的纯C# Core、回执Journal与Editor作者工具源码。Core不持有Inventory/角色/工位运行状态，不发布库存，不直接写盘、不操作Unity。所有作者/输入定义构造失败直接抛参数异常；Planner的正常拒绝返回Ok=false。

生产文件均在 `Assets/OSSASTRA/Scripts/Crafting/Core/`：

- `CraftRecipe.cs`：CraftIngredient、CraftOutput、CraftRecipe、CraftCatalog；同材料checked聚合、正数/ID/版本/能力/数量预算校验、目录ordinal唯一ID、冻结输入。配方单次总材料<=int.MaxValue、单次产物<=99、MaxBatch范围1..99；每批的产物上限还要由Planner判断。
- `CraftMaterialPlan.cs`：CraftMaterialStack、CraftMaterialConsumption、CraftMaterialPlan。Reserved是bool，整堆排除；IsEquipment排除；所有结果只读、快照不改。
- `CraftPlanner.cs`：Plan(CraftRecipe,int,IEnumerable<CraftMaterialStack>)；Runtime应先过滤合法团队来源。先校验所有输入ID唯一，再统计本配方定义的Available，按DefinitionId再ItemId的Ordinal分配；失败Consumptions始终为空。
- `CraftIdentity.cs`：冻结签名Fingerprint→64位小写SHA256。严格UTF8；每字段UTF8字节长用32位小端长度前缀、数字Invariant；固定域Ossastra.Crafting.Command.v1；消费源按DefinitionId/ItemId排序。修改任一请求事实会改变指纹（密码摘要固有碰撞性质除外）。指纹不是领域授权；Root负责当前preview准入和事务发布，Journal只保存已提交事实。
- `CraftReceiptJournal.cs`：Serializable字段DTO与最近128条不可变历史；PrepareAppend返回新Journal，Find同请求同hash给深拷贝，变hash返回RequestConflict，缺失返回false/null。Append不覆盖重复请求；Restore额外验证catalog配方版本/批量、材料及产物逐定义数量。Records与Find结果每次均深拷贝。普通合堆产物ID允许跨历史复用，同一回执内来源/产物各自唯一。Guid N需精确32字符且非空Guid，版本long字段非负。淘汰不是重放授权，Root仍只允许当前issued preview。

成功Reason为空字符串。失败Reason：InvalidRecipe、InvalidQuantity、InvalidMaterials、DuplicateItemId、Overflow、OutputBudgetExceeded、InsufficientMaterials。Required/Available仅包含本配方所需定义；缺料保留两者，其余无效输入失败返回空字典。不接受null来源/条目、重复来源即使已预留也拒绝；版本long字段必须非负，正int配方版本。

测试先于产品实现：`red.log` 0/41，反射真实缺失类型得到断言失败，无编译错误。首轮 `green.log` 41/41；独立Python canonical SHA256 golden、99批量/目录case/null来源再核对后 `green-final.log` **46/46**。运行：

```powershell
dotnet run --project E:/WORLDCREATOR/new/Project_ANKALUTE/Candidates/CRAFT-01/CoreTests/CoreTests.csproj --nologo
```

新增Journal先 `receipt-red.log` 46通过/27新增失败，再 `receipt-green.log` 73/73。随后发现.NET Guid.TryParseExact会接受两端空白，新增有效签名的空白Guid用例出现真实 `receipt-guid-red.log` 73通过/1失败；精确长度门修复后 `receipt-green-final.log` **74/74**。Json测试使用独立net9 System.Text.Json IncludeFields进行DTO roundtrip，Root实际Json.NET/共同存档仍需其集成验收。

`Assets/OSSASTRA/Scripts/Crafting/Editor/CraftingAuthoring.cs`：菜单 `OSSASTRA/Crafting/Validate Active Scene`；公开ValidateScene(Scene)返回错误数组；Root/Station Inspector“验证配置”；Station的TryGenerateInstanceId只在Edit场景实例、ID空且尚未登记、已有登记唯一合法时生成，Undo+Prefab override+MarkSceneDirty，不自动登记/保存。Recipe Inspector使用现有SerializedProperty中文分组，单配方按钮Freeze并提醒加入Root.recipes。严格同SO原库存目录引用；Transit允许0工位但全目录保留。旁路 `EditorCompile/EditorCompile.csproj` 对本机真实Unity模块与当前脚本程序集编译0warning/0error，`editor-compile.log`。曾纠正旁路引用集同时包含monolithic/module的冲突，以及全局ItemDefinitionSO同名遮蔽；最终验证不等于Native菜单/Inspector/Undo已执行。

net9离线exe，生产源码限制C#7.3、无Unity/NuGet/asmdef。`bin/obj`只为本地独立验证，不应发布。没有运行Unity编译或全工程测试；Root仍须验证目标程序集的集成、实际库存原子候选、最终容量/版本/工位/预留复核以及保存和UI。配方更改需显式更新version，Fingerprint按冻结签名不另取外部SO内容。

本子任务释放Core/、Editor/CraftingAuthoring.cs与CoreTests/写入路径，后续Root可接手。没有操作Unity、Git或其他共享生产文件。Journal校验输出定义/数量及ID格式/唯一性；冻结的CraftIdentity签名不包含产物ID本身，不能宣称它单独认证任意历史产物身份的不可篡改性。
