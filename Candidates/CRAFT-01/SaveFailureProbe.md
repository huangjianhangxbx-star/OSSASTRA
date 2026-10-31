# CRAFT-01 真实文件写锁失败探针

`SaveFailureProbe.cs.txt` 是由 Root 通过 Native `execute_code` 编译的一次性 `IFunplayCommand`，不放入 Assets、不改变生产程序集。执行结果必须单独保存；源代码存在不代表验证通过。

前置：只允许 Main GUID `d694fa04240ccaf40808408e233c70ba`、正常 Play、`crafting-fault-01`、本轮外部 profiles 目录、唯一原保存宿主，且当前角色真实可访问工位。由 Root 独立记录焦点/位置夹具。本探针不传送、不给材料、不重新绑定输入，不接管已开的 UI。

1. 执行整份源码，读取成功回执和返回的 `pollAssembly`。
2. 在同一运行程序集里反射调用静态 `CraftSaveFailureProbe.Observe()`，直到 `stage == "failure-held"`。不要因为初次 `RUNNING` 就调用重试。
3. 立即调用静态 `ReleaseAndRetry()`，再轮询 `Observe()` 到 `status == "PASS"` 或 `"FAIL"`。
4. 任意异常、退出或取消时调用该程序集原类的静态 `Cancel()`；不得重新编译另一个同名类来试图释放旧类锁。100 秒主线程 watchdog、退出 Play、AssemblyReload 回调也会释放本类自己的锁。

受测链是原 Root Preview/Confirm → 原共同快照 → 原 `CommerceFileSaveStore` 实际 `.write.lock` 独占冲突 → 原 `CommerceService.RetrySave` → 原 `CommerceSaveCoordinator.FlushAsync` → 原文件 Load/Codec 与宿主完整校验。不会重放制作命令、不会直接写主档、不会调用 PublishRestore。故障时同一请求再次确认必须只读返回原回执。材料布局、成品实例和回执在内存保持，旧主档/备份字节保持；重试成功后实际主档同一回执和唯一成品一次，旧主档成为备份。

为了能精确证明单次 File.Replace 的备份，探针暂时保存并延后这个隔离 fault 宿主的私有自动 checkpoint deadline；结束时恢复原 deadline，原自动检查点后续可继续。该运行时夹具会显式记录，不代表生产保存节奏，也不改 checkpointSeconds 或任何源资产。文件锁使用既有 `.write.lock` 的 FileMode.Open，失败和清理均不删除锁文件、不改权限、不改其他 profile。上次已提交的 fault 事实及本次成功制作会保留在该隔离档；取消不回滚已提交的业务事实。

本探针不是自然采集/键盘/手柄/Player 或编辑器进程重启验收。若门、材料、容量或窗口条件不满足，它会清理并报告 FAIL，Root 不得把失败改记成成功。

Root实际验证（2026-11-01）：首轮因既有fault包容量不足拒绝，保留iofault-held.json；原Session.Move一把既有测试石刃team→safe腾位后，iofault-green.json为PASS，命令284c109c56a74543a6eda8147a655c4d，新装备bb993b8705184366a1caee7d3e0f4497。实盘共享锁错误、内存成功保留/原请求去重、原RetrySave及Flush、同回执/唯一新ID/旧主档保留bak均已验证。锁/deadline租约已释放；末态两场demo、Main Edit clean/pristine见final-editor-20261101.json。这里只签显式API/位置/焦点夹具，不代自然采集或硬件。
