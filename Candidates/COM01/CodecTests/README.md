# COM-01 生产快照编码回归

本夹具直接引用生产 `CommerceSnapshotCodec.Encode/Decode` 所在的 `RuntimeCompileCheck.dll`，没有复制/替换编码实现。Newtonsoft 使用项目已有 `com.unity.nuget.newtonsoft-json@3.2.2/Runtime/Newtonsoft.Json.dll`，不下载其他包。

```powershell
dotnet build Candidates/COM01/RuntimeCompileCheck.csproj -c Release --no-restore
dotnet run --project Candidates/COM01/CodecTests/Codec.Tests.csproj -c Release
```

先刷新生产DLL，再执行回归，避免误测陈旧编码。CoreGate两个程序集需已构建。当前结果 `result.txt`：**12通过、0失败**；`tested-assemblies.json` 记录此次实际引用四个程序集的SHA256。

## 真实失败来源与验证范围

root的实际Unity读档RED保留在 `E:/WORLDCREATOR/XingHaiHuiLang/work/OSSASTRA/COM01-20261028/restore-play-state.json`：旧JsonUtility路径将普通物品的null装备造出默认对象，随后的完整恢复以“弹仓快照无效”拒绝，库存/Commerce未Ready。主任务据此替换为schema2的保null编码。本回归是对这一已发生故障的生产Codec检查，没有声称在独立.NET中运行Unity JsonUtility，也没有把纯往返称作完整Editor/游戏进程重启。

使用真实Ledger及装备能力建立快照：普通物品、破损耐久0的装备、3容量迫击炮弹仓消耗后剩2、未完成装填、CD、霜寒、技能显式清空、来源/射击增益、真实交易回执、两个商人、外箱/掉落、快捷栏null和对话标记。完整public字段逐项比较，随后交给新Ledger/Economy的真实Restore；恢复后检查没有修复耐久、免费补弹或丢失交易结果。

另覆盖解码DTO与原快照/冻结字节脱离、null/旧schema拒绝、根与嵌套的`$type/$ref/$id`拒绝、未知根/装备字段拒绝。测试只写本目录结果，不接触任何真实档案或Unity场景。宿主恢复、实际正常入口重启和新旧档政策仍由主任务实机证据负责。
