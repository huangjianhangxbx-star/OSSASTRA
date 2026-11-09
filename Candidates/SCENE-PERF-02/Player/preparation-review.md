# Default playtest / dynamic route 准备复核

2026-11-08，候选目录内执行；无 Unity、Player、Git 或生产写入。

## 默认试玩真实文件合同

候选 `ScenePerformanceDefaultPlaytestIsolation.cs.txt` 已把 Loaded 实际使用的文件事务抽到内部 `EnsureProfile(source,destination,profile)`，Boot 使用同一内部 CLI 判定。实际入口仍固定原批准路径和 profile；没有增加CLI任意路径入口。

运行：

```text
dotnet build Candidates/SCENE-PERF-02/Player/DefaultPlaytestContract.csproj
dotnet Candidates/SCENE-PERF-02/Player/bin/Debug/net9.0/DefaultPlaytestContract.dll
```

结果 `DefaultPlaytestContractRuns/20261108_135208_4bbc461ad09241da857c8ba7165ca83f/result.json`：6项PASS。实际调用真实EnsureProfile，仅读取批准test-commerce-v2，全部写入本候选目录的唯一沙箱：

- CPU／run CLI由Boot的同一判定提前排除；无flag为默认候选。
- 首次复制四文件，目标SHA与冻结源一致，冻结源不变。
- 用原CommerceFileSaveStore.WriteAsync写出一个不同的、领域Load可读的新检查点；第二次EnsureProfile返回false且四文件逐字保留，没有冻结源覆盖。
- 已存在主档坏字节时拒绝，坏档原字节保留。
- 源只有三文件时拒绝，不发布新目标。
- 错误profile／源目标重叠时拒绝，全部案例后批准源四SHA均未改变。

DefaultPlaytestCompile及上述合同编译均0警告0错误。当前候选SHA：`3C9B47F13365CCB9821B4904B6B59EC29F2058D26BADB4D50BD74E44DB26860B`。旧1698...候选已被此可测窄seam版本替代。合同没有模拟Unity成功：实际sceneLoaded先于Start、禁库存／安全路由、无flag首次与第二次Player启动仍需Root验收。

## Dynamic路线可执行性复核

`dynamic-route-example.json` 是27步有界循环：12段移动、8次同向45°转镜头、2次真实切人、门／窗各开关一次及等待。世界坐标来自实际作者构建脚本：外侧(-16,.1,-18.2)，前门(-16,z=-16)，A中心(-16,-12)，A→C门(-12,-12)，走廊(-11,-12)。先开A→C门，再走门到C并返回，不进入有原敌和箱体的B。门API实际接的是portal-a-c；外入口不是该可开关门。角色基础speed=3，最远已列线段约3米，18秒步deadline足够暴露堵路而非悄悄跳点。

输入链已核：DeviceActionSampler.Synchronize会发现启用的虚拟Keyboard/Pointer，并给每设备克隆动作；driver从双方实际movement composite的up/down/left/right解析KeyControl、从实际switchAction解析绑定键、从MapRotateWheel解析Mouse，随后只有QueueStateEvent进入原InputSystem。滚轮只发一次±120，原MapResearchCameraInput将同帧合并成至多一档。它不直接Move／SwitchNext／写动画。

启动只在warmup且sampling=false时允许一次已有Lab.PlaceCurrent(exteriorPosition)，记录前后位置／frame／warmNeverSampling；循环内从不定位。由实际Current行走距离、进入／退出、完整8扇区mask、门窗状态双值判覆盖；不以循环次数或输入请求数假充动作完成。

运行前仍须Root核当前Native门洞／高度／同伴跟随是否实走通。跟随者可能因真实碰撞留在门外，切人后不能用传送兜底；计时不足、死亡、失焦、UI抢输入或到点超时均应失败。作者默认面板：光感Rect右锚offset(-404,-63)、width350；环境offset(-12,-63)、width380；lab位于Screen.width-400，均不覆盖1600×900的中心指针(800,450)。实际作者覆盖或当前UI导航选择仍需检查；不静默关UI。Player必须可见且真正聚焦。

动态候选当前未变更，仍是CFDAEB48...SHA；没有宣称已实跑120秒或性能通过。
