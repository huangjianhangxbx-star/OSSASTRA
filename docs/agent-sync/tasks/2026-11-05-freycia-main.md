# FREYCIA-MAIN
状态：active / unity-writer=root
目标：原位替换主场女主模型，保留身份、双角色切控、库存与光照/遮挡。
独占：SC_RoomVisibilityLab、Configs/Characters/FreyciaMain及本记录。
计划：备份场景；独立配置与库存引用；隔离存档验证；恢复路由并保存。
不改：小蓝、原媒体、已有存档及原dirty。

## 完成（2026-11-05）
- 原位替换 PlayerCharacter_B_MapParty 身体为 FREYCIA，保留 temporary-b 身份与两角色协调者。小蓝不改。
- 主场独立 Character/Dagger/DaggerItem/Rig 配置；库存相同 definitionId 引用专属装备，避免绑定库存后退回旧动画。原共享装备不改。
- idle、Move、五段短刀-a1；方向沿用已修复的每面定位。新 Rig 使用真实五面 HandL/HandR，保留原主场深度和角色受光接线；移除旧 Rozeul 材质替换。
- Native Play：库存 Ready=true；双向切控成功；鼠标输入实际经历 AttackStage 1..5，全部短刀-a1；八方向覆盖五面，活动面根Y为0或0.001；验证窗口无运行错误。
- scale=.001；bodySkeleton引用正确；所有Rig路由通过真实骨架验证。
- 验证存档隔离于研究/FreyciaMainValidation；退出运行后恢复 kt-vis06-natural-01 与原目录并保存主场。没有迁移或改写旧存档。
- 主场修改前备份：E:/WORLDCREATOR/XingHaiHuiLang/研究/FreyciaMain-before.unity。
- 用户预览场未保存现场先保存副本：E:/WORLDCREATOR/XingHaiHuiLang/研究/FreyciaPreview-unsaved-checkpoint.unity，再关闭预览，防止双场景运行；原预览场磁盘不覆盖。
- 限制：源素材没有独立闪避/受击/死亡/技能动画，本轮不伪造这些专用动画。角色主流程和既有系统仍保留。
- 主场与模型媒体按既有本机资产忽略规则保留；发布范围为新增专属配置与本记录。
- 状态 complete，全部Unity与配置独占释放。
