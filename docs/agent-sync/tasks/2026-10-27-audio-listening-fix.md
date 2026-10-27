# AUDIO-LISTENING-FIX

- 状态 complete；read-only，全部Unity/源码writer释放；基线9ff9fc9。
- 用户报告：整体小声/无调节入口，角色在画面下方声音浑浊，不明持续循环使人不适。按既有连续修复授权做定向排查与修正，不再次执行电源动作。
- 独占：Audio源码、必要现有UI设置入口脚本、本任务Candidates/AUDIO-LISTENING-FIX与docs/ossastra/AUDIO-LISTENING-FIX*、自有SC_AUDIO_BB_Fusion及本机BloodborneLocal作者配置（仅Editor API）。原36dirty、用户绑定/角色/Spine/旧场/规则不改。
- 子代理lab_architecture只读审查；facts_coverage经单独授权只写InventoryView.cs与InventoryAudioSettings.cs，已完成释放；均未操作Unity/Git。
- 路线：先读取当前Play音量/声部/Listener/位置与最小RED复现；单变量识别衰减、遮挡/混响与循环来源；局部修复和实际回归，补可见可保存的分组音量控制。人耳验收不代签。
- 原始症状、测试/保护及GitHub定向发布另记，不Gitee。

完成：实际听点14.17→.8u、边界移除声学标记、关闭marker不挡声、遮挡不叠Room，新增运行期听点精确原Listener启停恢复/切人/死亡尾音/初始禁用。原菜单声音设置14/14并保存1/.65/1/0；默认环境0会真实释放loop，用户明确确认迎风/魔法持续声“已经消失”。一次声原融合档净×1.6，最初×2测出2削波样本后已回收余量，最终28.096s实机枪械7/7、peak.749935、0削波、峰12声部。定向合同、跨场与保护见交接；原36dirty保持，Edit clean、原相机Listener恢复、临时设备/听点0。全部writer释放，仅本次GitHub定向发布收口；没有电源动作或下一迁移。
