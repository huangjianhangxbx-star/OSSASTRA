# M02B2 架势与破势
- ID: M02B2
- 状态: CANDIDATE_PUBLISHED；技术完成，用户审查待办
- 模式: unity-writer
- 日期: 2026-10-21
- 基准: bc34538b5296eff4cd0f7b88bb45d874d10920ba，本地/远端一致；较计划基准仅新增R22动画方向文档。
- 独占: Assets/OSSASTRA/Scripts/Combat、Characters/Core必要载荷与Runtime/CharacterRoot、Enemies/Runtime/EnemyRoot、Development/Combat本轮开发视图；新M02B/SC_M02B2_PostureStagger；Candidates/M02B2；docs/ossastra/M02B2-*；README/OSSASTRA_RULES与本任务同步。
- 保护: 所有既有场景/PlayerCharacter.prefab及meta、InputSettings、用户媒体/Spine/渲染；旧asmdef/meta删除不提交。
- 现场: ARCH01 Play且Paused，场景clean，无PrefabStage。先退出自有测试现场，不保存旧场。
- 依据: 用户附件B2 v1.0全部P0–P7、R22、本轮明确执行；链接登录墙不可读。
- 计划: RED纯状态/原命中 → 共享状态与不可变冲击 → 两根时序与权限 → 独立场 → 定向世界/生命周期回归 → 完整源码审查 → R11 GitHub。
- 验证: C级权威覆盖/B级集中执行；未执行项不冒称通过。
- 非目标: 正式动画系统/敌人追击/2D物理迁移/B3/M03。
- 交接: 技术实现/定向验证与发布完成；全部写租约释放。

## 完成结果
P0–P7已按任务执行，详见M02B2-review.md与verification.md。设计无关键冲突：旧场可选接入、三根边界、唯一HP/动作事实、2D正式视觉保持；精确计时纯模型，世界时钟由各根推进。下一步只发布与用户审查，不自动B3/M03。
