# COM-01 纯保存适配验证

范围：`Assets/OSSASTRA/Scripts/Commerce/Persistence/`。不加载Unity，不读用户存档，不修改库存或角色策略。测试档只在本目录 `Artifacts/` 下按运行ID隔离生成。

## 已执行

```powershell
dotnet run --project Candidates/COM01/PersistenceTests/Persistence.Tests.csproj -c Release --no-restore
```

初始RED：缺失保存API导致编译失败。补强同档案代际时取得行为RED：`FAIL late old session cannot overwrite reopened same profile`；随后加入持久化session标记与最终写门校验，36项GREEN，零编译警告/错误。

最终本轮证据：`Artifacts/20261028-144803-61df2ee7fa854f00bfc15dfc2893b6e3/`；真实独立子进程writer PID 56704结束后，reader PID 54300恢复先前写入的固定经济payload。

覆盖：

- 延迟A落盘时B/C更新：顺序写入并合并未开始快照，A完成不能标C已保存；调用方/读取方改byte[]不能改冻结内容。
- 同profile新session与不同profile切换，旧异步写入不能覆盖新代际；已关闭协调器拒旧请求。
- 真实Windows文件共享拒绝删除，使临时文件写入/刷盘完成后的替换失败；上一主档保留、临时文件回收、最新内存重试，不重放成交。
- 校验失败保留坏档，不自动播种或覆盖；孤立未提交临时文件不作为存档加载；显式备份恢复保留损坏主档证据。
- 同序号同内容重试幂等，同序号不同内容拒绝；脏但尚未捕获的新修订不能被Flush/Close标为完成。
- 实际终止writer进程后独立reader进程恢复不可变payload。该项只证明文件层重启，不冒称正常地图完整领域故事已运行。

## Runtime接线合同

1. 创建`CommerceFileSaveStore`时传入宿主明确的模块存档目录。`PathForProfile(profileId)`给出实际文件位置，档案ID哈希化防路径越界。
2. 调用`Load(profileId, validator)`，明确处理`Missing/Loaded/Invalid`。只有Missing允许宿主首次授予。validator在主线程解码和校验领域schema、定义指纹、重复ID/格位/引用；返回null表示通过，字符串表示拒绝。
3. 用已验证领域DTO一次启用恢复状态。新会话每次建立新session GUID；构造`CommerceSaveCoordinator(store, profileId, sessionId, loaded.StateRevision, loaded.SaveSequence)`。无档案时两个计数为0。构造会激活新代际，目录拒写/同档写锁占用可抛异常，宿主须显示诊断，不能播种覆盖。
4. 领域事实变化使`stateRevision`递增，调用`MarkDirty`。主线程Capture并编码成UTF-8/其他稳定bytes后，调用`RequestSave(revision, bytes, payloadSchema, definitionFingerprint)`；之后原数组可复用，后台不访问Ledger/Unity。
5. UI轮询`Status`：`IsSaving/HasFailure/IsSaved/LastError/CanEdit`。CanEdit只限制新Commerce主动编辑；既有合法动作消费/全灭不得由文件失败冻结，继续标脏并提交最新待写快照。
6. 写失败后`RequestSave`仍可更新最新待写快照，但不无限自动重试；明确按钮调用`RetryAsync()`。换档/退出先等待`CloseAsync()`；返回false时保留内存并提示风险，不能假称已保存。
7. 只在明确选择备份恢复后调用`RestoreBackup(profileId, validator)`。它保留`.corrupt-*`证据，再恢复已验证备份；之后重新加载领域并建立新session，不能继续使用旧UI令牌。

API均在`Ossastra.Commerce.Persistence`，无新NuGet包。文件包使用版本头、完整元数据/字节SHA256；同目录临时文件`Flush(true)`后`File.Replace`，保存上一有效`.bak`。该平台实测不等于断电绝对安全承诺。

## 当前限制

- 没有测试物理断电或在文件写入中强杀进程；孤立临时文件用明确夹具模拟中断遗留。
- 域DTO完整性由Commerce Core/Runtime校验；本适配不理解“钱”“装备CD”或“商店”。完整正常地图进程重启由主任务另验。
- 文件层激活新session后拒旧代际落盘；同一档案写入正在持锁时，新session激活直接报错，宿主应先Close旧协调器，不抢占旧写入。
- 不实现自动云同步、自动坏档修复或后台Unity回调。

所有三个生产脚本包含中文职责头、生命周期/提交/恢复注释；测试同样有中文范围说明。未提交或操作Unity资产，meta由唯一Unity writer导入生成。
