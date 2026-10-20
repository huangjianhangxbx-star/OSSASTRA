# M02-B1 发布记录

2026-10-20，候选实现提交 f39ffef97e1f3b405429f7f93ceeddf7e24dadf7 已推送 GitHub OSSASTRA/main，原生 ls-remote 与本机完整 SHA 一致。起点 6263eda4b85c50a5b2232d31fd811e666fbef3c5。仅提交本轮 63 个文件，未提交既存旧禁编 asmdef/meta 删除，未发布用户美术、第三方插件或同步 Gitee。

实际完成真实角色/敌人独立 Health、共享 Released→Melee→Health 双向伤害、单次真实死亡退场、死亡取消与唯一 Current 交接、全灭 Neutral 以及开发显式重放。普通 Tab 移动规则保留，死亡交接等待旧移动回中/攻击松键。

验证：B1 世界 22/22、边界 15/15、纯战斗 13/13、旧核心 29/29、跟随 13/13；R1 世界 19/19、M02A 26/26。M01 原集 14/15，过期来源身份夹具定向适配 1/1；没有声称原集已整体重跑全绿。首次真实反馈异常及编辑器输入路由/缓冲夹具失败原件和修复说明均保留在验证记录。

最终 Unity 为 SC_M02B1_CombatDeath Edit ready/clean，双角色各 8 引用有效、敌方 2 目标有效、MissingScript 0、测试设备 0，原 InputSettings 对象已恢复。8 项保护 SHA 不变；共享 PlayerCharacter.prefab 仅授权生命接线增量且 GUID 不变。

本轮源码、逐行说明、SHA 与待用户审查账本已公开；技术验证通过不替代用户手感或架构批准。停止 B1，不自动实施 B2/M03/正式返场/架势/连段/完整战斗 AI。本地审查包为基准以来增量与本地截图，不是可独立运行的完整工程，截图不上传 GitHub。