# 开发消费端与Look验证回调合同

本增量只修改两个Development面板和WorldLookController。主场、Profile和已认可表现值均不改。

- 环境面板保存实际申请的天空绑定；换源、清空、折叠、禁用或销毁时撤销自己的旧预览申请。
- 角色面板只持有自己建立的诊断委托；服务换源或清空后立即更新，禁用精确撤销。
- WorldLookController的OnValidate只写原子标记；Update或房间帧回调在主线程消费，必要时撤销旧相机Look。已销毁的配置适配器按Unity空引用拒绝。

`ConsumerLifecycleContract.cs.txt`由Root用Native执行。Root已在修复前复现：previewRebindReleasesOld、previewNullReleasesOld、diagnosticsRebind、validateOnlyMarks为false。该合同只使用临时对象和临时相机登记，finally释放。

离线编译直接包含实际三个修改文件及其UI叶组件，借用当前Library程序集；不代替Unity最终导入与Native GREEN：

```powershell
dotnet build Candidates/RENDER-ARCH-01/Architecture/ConsumerCompile.csproj -v:q
dotnet build Candidates/RENDER-ARCH-01/Architecture/ConsumerCompile.csproj -v:q -p:DefineConstants=UNITY_EDITOR
```

两个分支均已通过，0错误、0警告；git diff --check通过。WorldLookSettings.Build算法方法体经规范化换行后与原版相同；旧Profile映射覆盖展示8项、空气15项、色板8项，无字段遗漏。

最终在独立Smoke场Play运行ConsumerLifecycleContract，9项全部GREEN；包含真实禁用回调。完整验收见docs/ossastra/RENDER-ARCH-01/验证与限制.md。ShaderScopeGpuContract另使用临时Shader实际绘制常量色，检查Pass准备与命令作用域，无全局永久改变。
