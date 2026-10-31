# CRAFT-01 退出保存日志：只读诊断与最小补丁建议

日期2026-10-31。状态：真实宿主RED后，Root明确确认Edit ready并授权单文件补丁，已实施且9/9 GREEN；未操作Unity或Git。RED受测`CommerceService.cs` SHA256：`5997F3474BBA65E8A267104AFE6BDDF35E7C48B556084464559D93E0A081C8B3`；GREEN SHA256：`F0F9CA33664D5744EE9260BC2D5F875A30BD5113099089930B73429CF001A794`。

## 确认结论

现有退出日志不能单独证明最后存档丢失。至少三种“已有完整最新检查点，却缺退出专用缓存”的路径会误报；新增脏修订而没有冻结新快照，以及真实writer失败，仍是真实风险，必须继续拒绝关闭。

直接因果：`CommerceService.RequestSave:195`冻结并排队完整共同快照，但不写`lifecyclePayload`；`FlushBeforeSceneChangeAsync:202`只调用该RequestSave及原Flush。`FinalizePersistence:315`在Inventory先释放时要求`lifecyclePayload!=null`，于是即便原writer已确认最后完整payload、`StateRevision==PersistedRevision`、`IsSaved=true`，仍报“最后快照的库存/经济依赖已释放”。同类错误也会出现在最新快照仍在写、稍后由退出Flush成功排空的路径；不能在进入Finalize时先要求IsSaved。

## 真实RED回路

```powershell
dotnet run --project Candidates/CRAFT-01/LifecycleTests/Lifecycle.Tests.csproj -c Release
```

补丁前退出码1，**6 PASS / 3 FAIL**；补丁后同9例退出码0，**9 PASS / 0 FAIL**，均零编译warning/error。完整实际数值及日志：[baseline-red.log](baseline-red.log)、[green.log](green.log)。三个FAIL均是上述假失败，GREEN没有放宽原业务断言。

项目直接编译生产`CommerceService.cs`、原`InventorySession*.cs`、Ledger/全部CharacterCore、CommerceEconomy/Core、SaveCoordinator、快照Codec/参与者及保存合同。只替代磁盘端口为MemoryStore、Unity/UI/商人SO外壳。LifecycleHostStubs来自原COM01夹具的本目录独占副本，新增元数据用于编译Service；没有修改原夹具。真实退出调用的是生产私有`OnDisable`和`OnDestroy`，没有复制/改写Finalize算法。

| 真实触发 | 受测结果 | 判定 |
|---|---|---|
| 显式Flush已成功，保存端口payload与当前Capture编码逐byte相同；然后Inventory引用不可用 | host/state/persisted修订均1，seq2，IsSaved=true，缓存null；OnDisable报错，OnDestroy仍关闭writer；stone仍3 | 假失败，RED |
| Flush后原Ledger.Grant令stone3→4，产生host2/state2，但persisted仍1，无新RequestSave；然后依赖丢失 | 退出报错，writer不关闭，端口仍stone3 | 真实末变更未保存，PASS，补丁不得吞掉 |
| 最新stone4快照已RequestSave，但MemoryStore用真实异步Task gate阻止完成；依赖释放后Finalize排空，25ms后放行 | 原Flush/Close完成、stone4真实写到端口、IsSaved/Closed=true，仍残留捕获失败日志 | 假失败，RED；缓存匹配即可等待原writer，不得提前要求IsSaved |
| 依赖仍在，Flush后有新stone4事实 | OnDisable真实Capture并排新序号，stone4被保存，writer正常关闭 | 现有正确行为，PASS |
| 更晚正常RequestSave已完成stone4，之后依赖丢失 | 最新payload确实保存，仍因生命周期缓存null误报 | 假失败，RED；普通保存也必须刷新缓存见证 |
| 早先Flush成功，后续真实WriteAsync注入IOException | HasFailure=true、不关闭、退出错误保留，旧完整stone3不被改写 | 真实writer失败，PASS |
| 没有Notify的Economy.Advance(.25) | hostRevision仍1且IsSaved=true，但Economy.StateRevision0→1、真实Capture字节已变 | PASS性质测试：host修订不能证明完整payload逐byte未变；不是断电零丢保证 |
| 依赖仍在，原装备Wear及Magazine.Consume，同host revision | 退出Capture追加新序号，真实保存99耐久/3发弹 | PASS；不能以revision相同跳过活依赖捕获 |
| 协调器已关闭，同步拒绝新RequestSave | 原cache引用、revision/sequence见证、lastSavedSimulation与实际SaveSequence不变 | PASS；未接受的新字节不能先安装为退出见证 |

纯宿主用`service.inventory=null`显式驱动与Unity destroyed-object-null同一个条件分支；不声称模拟了Unity对象所有销毁行为。Root自然跨场日志、当前冷读和重复身份恢复属于独立Native证据，本夹具不代签。MemoryStore确认的是原保存端口收到且完成了冻结payload，不冒称真实磁盘/进程测试。

## 已授权实施的最小单文件补丁

精确租约：`Assets/OSSASTRA/Scripts/Commerce/Runtime/CommerceService.cs`。Root明确“Edit ready，可写”后才修改；未改保存协议、Coordinator、场景、Inventory生命周期或公共API。实际方法位置RequestSave:196、QueueFrozenSnapshot:203、HasCurrentFrozenCheckpoint:210、Finalize缓存门:327—344。

新增私有字段与窄帮助函数：

```csharp
private long lifecycleRevision = -1;
private long lifecycleSaveSequence = -1;

// 仅接收已在主线程完整Capture/Encode的值；成功排队之后才安装缓存见证。
private void QueueFrozenSnapshot(CommerceRuntimeSnapshot snapshot, byte[] bytes);

// 可接受“当前最新bytes仍在写”，完成与错误仍由原Flush/Close作决定。
private bool HasCurrentFrozenCheckpoint(CommerceSaveStatus status);
```

`QueueFrozenSnapshot`先执行`saves.RequestSave(snapshot.revision, bytes, 2, snapshot.economy.definitionFingerprint)`并取得返回的saveSequence，成功返回后同时设置`lifecyclePayload=bytes`、`lifecycleRevision=snapshot.revision`、`lifecycleSaveSequence=sequence`及`lastSavedSimulation=snapshot.economy.simulationTime`。失败不得把未接受的字节写成缓存。调用者持有的bytes为Codec新分配且不对外暴露，不应后来修改。

普通`RequestSave`和`FinalizePersistence`依赖尚在的分支都调用该函数。后者继续保留完整Capture与逐byte比较：同revision但内容变化仍排队新序号，不能为节省退出工作删掉这一层。若缓存修订/序号见证不匹配当前状态，即使bytes相等也不能跳过重建见证。

`HasCurrentFrozenCheckpoint`仅在缓存非null，且`lifecycleRevision == revision == status.StateRevision`与`lifecycleSaveSequence == status.SaveSequence`时为true；不以“最近保存过”、宽松大小比较或仅`status.IsSaved`证明缓存新鲜。

依赖先释放时：缓存见证不匹配仍报捕获失败；匹配则沿原Flush/Close排空。**不要在等待前要求IsSaved**，否则最新请求排队/写入中仍会假报。等待返回失败或仍非IsSaved时，照常报告真实写盘/脏版本失败。日志去重继续沿原lifecycleFailure，不增加吞错分支。

## 不能夸大的边界

上述补丁证明“当前服务修订对应的最新已排队完整检查点已落盘”，可以消除本次确定的假失败。它不能证明依赖释放前所有未通知的瞬时装备时钟/资源都与检查点逐byte相同：原系统本就存在Equipment payload或Economy模拟时间不逐帧推进宿主脏版本的情况。依赖仍在时完整Capture必须保留；依赖已经不可用后，不应靠revision声称最后瞬时状态零丢失。

若用户另要求更强的“释放前最新全部状态必捕获”，需要Inventory释放前冻结回调等额外窄接缝及Native销毁顺序证明，属于另一项生命周期保障，不能借这次日志修复暗自扩张。本轮自然跨场刚成功Flush、装备ID/回执恢复一致的结论仍是完整检查点一致性，不是任意帧断电零丢保证。

## 精准验收

原三条RED已转PASS，原六条保护性断言未变。另通过本目录`PersistenceRegression.Tests.csproj`直接链接原COM01持久层测试源，41/41通过，含真实两个已退出子进程写入/恢复、单writer、序号、失败与备份隔离：[persistence-regression.log](persistence-regression.log)。该项目bin/obj/Artifacts均隔离在本任务目录，未向原COM01目录写输出或触碰默认profile。

Root仍需安全Native验证Main→Transit→Main，两次Flush成功、同装备ID/材料/回执恢复、无该退出假错误，并独立记录退出时场景/profile/路径；真实写失败和脏版本缺快照仍保留失败门。不清Console来伪装错误消失，不覆盖原报告中的历史错误。纯宿主GREEN不代签Unity销毁次序或实际CRAFT磁盘冷读。

2026-11-01 Root补充：上述Native双向跨场门后来已完成，见验收报告§4及patched-lifecycle-transit/return/console.json；另实盘写锁失败→原保存重试已通过，见报告§11。此历史交接不再代表当前待办。
