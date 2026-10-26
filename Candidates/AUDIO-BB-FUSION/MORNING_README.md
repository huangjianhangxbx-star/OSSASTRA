# 醒来后从这里看

本轮已把参考音声接入原有唯一音频根，增加内容档案原子切换、四段攻击/材质/受伤死亡映射、枪械装填、真实距离脚步、三区环境循环、墙体低通/混响和音声监视。保留原控制、动画、战斗与统一混音架构。没有将 Lab 引擎复制进来。

本机项目：`E:/WORLDCREATOR/new/Project_ANKALUTE`。打开唯一新增入口 `Assets/OSSASTRA/Scenes/SC_AUDIO_BB_Fusion.unity`，Play；最终停在该场 Edit clean。旧场未保存、未删除。

菜单 `XingHai/OSSASTRA/Audio/Unified Audio Monitor`：查看当前档案、声部、来源和具体 clip；Play 中可切 A0 / A1 / A2 / A3。A0=原内容，A1=仅换录音，A2=语义/材质/环境组合，A3=同A2再加声学；每档相同用户音量。同曲不重启，切档撤销旧SFX代次。监视器 A0 按钮可以立即回退；本机默认是 A3。卸掉 BloodborneLocal 会明确报告参考内容不可用。

沿现有绑定试玩：WASD/左摇杆移动，鼠标左键/RT主手，右键/LT副手，Space/B闪避，Q/E/R或LB组合技能，Y回血，Tab背包；切人/其它键以现有 PlayerInputLayout Inspector 为准（这份用户已有修改未被本轮覆盖）。枪械需通过现有库存/外部箱真实装备，测试没有授予装备或弹药。

已录四档实际Unity Mixer对照，每档约56秒、相同事实和种子、均无削波；枪械7/7。主故事20/21及世界探索8/9的原始未过保留，分别是短窗口敌击未观察到，以及小摇杆被现有输入源归一后没有自然步行。它们没有通过写HP或改玩法“刷绿”。

仍需你听：`FINAL_AUDITION.md`。存在真冰声等素材缺口，不宣称全面过渡或听感签收。Player仍被既有Unity-Favorites-Tool的14个编译错误挡住；Editor可测试。详见 `BUILD_VALIDATION.md` 和 `VERIFICATION_INDEX.md`。

参考素材和含声录音只留本机：`Assets/OSSASTRA/Audio/BloodborneLocal/` 与外部 `E:/WORLDCREATOR/XingHaiHuiLang/work/OSSASTRA/AUDIO-BB-FUSION-20261027/`。四份盲听副本叫 LISTEN_A/B/C/D_MATCHED.wav；不是离线合成，原始录音另存。公共GitHub只发布代码、配方、文本报告和无私有依赖的新场。

你的36项原有未提交修改全部保持。下一轮优先听审并逐条替换不合适录音、补冰/金属/食物等缺口；不要把素材数等同于覆盖率。未自动进入其它迁移任务。
