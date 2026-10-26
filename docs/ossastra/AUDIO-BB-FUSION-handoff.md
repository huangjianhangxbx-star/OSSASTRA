# AUDIO-BB-FUSION 交接（2026-10-27）

从 `efdd6138` 基线完成 F00–F10 工程实施及定向验证。入口：`Assets/OSSASTRA/Scenes/SC_AUDIO_BB_Fusion.unity`；本机默认 A3。先读 [醒来后说明](../../Candidates/AUDIO-BB-FUSION/MORNING_README.md)、[验证索引](../../Candidates/AUDIO-BB-FUSION/VERIFICATION_INDEX.md)、[试听关口](../../Candidates/AUDIO-BB-FUSION/FINAL_AUDITION.md)。

保留唯一 GameAudioRoot/BGM Transport/SFX仲裁/统一Mixer，固定6音乐源+32短音源；增加原子A0–A3档案、可追溯语义/材质/死亡、明确冰效果元数据、距离脚步、三区循环、声学及监视。实测修复首攻丢声、材质优先级、淡变覆盖遮挡、附加场抢Host/duck泄漏、Host早于Root、致死hurt重复及无地面脚步。

本机67派生媒体、111个A2/A3事件定义、117条绑定（原72保留）、3音乐和3环境循环。163行覆盖表不等于已自然触发数或100%覆盖。真冰、金属脚步、食物等仍有候选缺口，匿名c1050身份、原作FEV/BPM/循环/地图音乐图未还原。

四档同237冻结事实/13上下文/种子的实际Mixer捕获均无削波、声部峰5/5/9/9；录后等RMS副本只留本机。主输入20/21（短窗敌击未观察）、枪7/7、世界8/9（归一输入未覆盖自然步行）；Single6/6、附加Host5/5、晚Host4/4、死亡3/3、原创诊断替换3/3。其余证据逐项见索引，不合成虚假总通过率。

最终新场Edit clean，零临时设备/对象/缺脚本；418资产依赖不含私有音频。36原dirty/67源目录项/5FSB/67派生保持；旧场、用户输入SO及开发细则未保存。额外Combat三文件仅向后兼容只读效果语义，不改HP/时序/动作权。Player被既有收藏插件14错阻挡，未修无关插件。

当前级别 `TECHNICAL_INTEGRATION_COMPLETE + CONTENT_INCOMPLETE + PENDING_HUMAN`；未宣称严格无P0缺口READY、用户签收或可分发。仅GitHub发布通用代码/配方/文本和无私有依赖的新场；素材/SO/录音/Player不公开、不Gitee，回执另记。下一轮先人工听审和补素材，没有自动进入新迁移。

发布索引额外清理：旧AudioReferenceLocal目录及hit/hurt/release共4份孤立.meta原已被旧提交跟踪，本轮只撤销它们在当前Git树的跟踪，磁盘原文件和历史不改；没有删除本机素材。原36dirty照常保护。
