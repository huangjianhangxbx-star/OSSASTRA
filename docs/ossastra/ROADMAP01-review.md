# ROADMAP01 集中审查
## S1 调用链
CombatVitals.Died → PartyRespawnCoordinator → RespawnSchedule（本场模拟时间）→ IRespawnPlacement（同层地面+胶囊障碍+批保留）→ CharacterRoot.Respawn（owner/原实例）→ CombatVitals/Health本体恢复 → 唯一CharacterControlCoordinator复选。
角色Root新增的是恢复接线，不承接全局队伍时钟。死前动作经原Cancel终结；按键ResetPlayerInput(true)要求重新武装。姿态仅清自身限制。

## 本轮源码索引
详见ROADMAP01-source-manifest.json，逐文件SHA可与发布提交比对。脚本均位于Scripts，测试场位于ROADMAP01，未改共享Prefab。

## 待用户审查
- 单死/全灭等待的手感、候选落点间距；此为场内样板，不是完整LT01。
- 后续动画/连段/追击各自Gate后在此追加；不把临时胶囊当正式2D敌人。
