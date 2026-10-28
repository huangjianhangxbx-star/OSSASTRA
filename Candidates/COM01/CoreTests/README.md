# Commerce 纯领域验证

从工程根运行：

```powershell
dotnet run --project Candidates/COM01/CoreTests/Commerce.Core.Tests.csproj -c Release
dotnet run --project Candidates/COM01/CoreTests/Commerce.Core.Tests.csproj -c Release -- --process-proof
dotnet run --project Candidates/VC02B/Tests/Inventory.Tests.csproj -c Release
```

第一条当前验证 37 个真实领域行为（29 个交易/恢复、8 个对话图）；项目引用 `AssemblyGate/CharacterCore` 与 `AssemblyGate/CommerceCore`，分别生成真正的 `Ossastra.CharacterCore` 与 `Ossastra.Commerce.Core`，验证单向引用和受控友元提交，不用合并源码冒充程序集隔离。

覆盖实体币多数量买卖、最终容量/扣空币腾位、指定实例出售、预留/访问/容器/装备资源变化重验、成功及失败请求身份绑定、观察者异常、双店与模拟刷新、独立字段快照、坏数据/溢出拒绝、装备破损耐久/CD/弹序/霜寒恢复和原子整箱首次授予。原 VC02B 库存 37 个合同保持通过。

第二条启动真正的 writer 进程，用本轮真实交易、Ledger 与保存器保存；writer 正常结束后启动全新 reader 进程。验证币 14、food 3（箱 2/地面 1）、装备 0 耐久/3 发/霜寒 2/剩余 8 秒 CD、两商人库存 3/5、模拟时钟 10 与刷新期限 30，以及宿主对话/世界 DTO 字段。测试数据只落在本测试输出目录下的 `economic-process-proof-*`；不打开或损坏用户档案。

这些是纯领域/文件层与实际进程边界的证据，不替代 Unity 正常地图、uGUI 输入、世界代理卸载/重建或用户体验验收。宿主仍须预验证并恢复自己的 `InventoryRuntimeState`，在完整就绪后开放经济命令。生成的 `bin/obj` 不发布。

`DialogueTests.cs` 的文本夹具对照固定原型 `bb969cc` 的 `game/src/core/dialogue/content.ts`：三句到A/B选择、各自标记/提示/末句、持久标记重访两句；同时验证无效选择不提交、取消、通知异常、效果环和缺节点诊断、优先级平手稳定。生产会话机不包含这些剧情台词。

新 `CommerceDialogueSO.cs` 可额外通过 `dotnet build Candidates/COM01/CoreTests/AssemblyGate/DialogueSO/DialogueSO.Compile.csproj -c Release` 对真实 Unity CoreModule 验证类型/属性编译；这不是Editor运行验证。

模板作者工具可用 `dotnet build Candidates/COM01/CoreTests/AssemblyGate/TemplateEditor/TemplateEditor.Compile.csproj -c Release` 对本机真实 Unity 模块和当前 `Library/ScriptAssemblies` 宿主类型检查。它只编译 `CommerceTemplateInstaller`，不运行Editor、不创建Prefab；实际模板引用/空ID、重复调用不覆盖、登记地图及Undo仍由唯一Unity writer验收。
