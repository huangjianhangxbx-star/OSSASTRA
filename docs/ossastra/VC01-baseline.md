# VC01 G0 对账与决策

Unity本地/远端基准47cfc64；Origin本机86824bd是c21d4fc的祖先（首次误称分叉，现更正）。用户已确认按c21d4fc，隔离导出，不切换源工作目录。只读副本127.0.0.1:5177；截图与本地商业资源仅存work，不公开。

## 动画
实际读取110动画、169骨骼、88插槽、default皮肤。原始事件/时长清单见VC01-animation-catalog.md。12个片段已在双实例上实际播放并录制：atk1—4、dodge_front/back、heavy、xskill、ready_loop、upper_hitted_all、dizzy_start/loop；视频本地work/OSSASTRA/VC01-20261021/rozeul-preview.mp4。元数据存在不代替视觉确认，未实播全集。
选定候选：四段普攻组；前/后闪避；重击；xskill；准备姿态匹配盾；受击/眩晕组。命名中的front/back不自动证明正背视角；当前单视角翻转可用，完整逻辑方向不量化。武器附件Z-Rknife1/2/3及相关骨骼实际存在；盾/投掷替换资源仍待G3逐项匹配，不能称已完成。
事件hit_start/hit_end/hit_to_idle确实存在，不能直接按字面认定伤害权威；G2将显式配置语义/时间线映射，实际运行只提交一次Release。

## 控制冲突草案
- 普通移动+闪避：许可/体力合法后由唯一Movement应用覆盖速度。
- 普攻恢复+运动：MoveReady后实际移动可让出主轨，但不开放AttackReady、不取消已释放效果。
- 主副手：保留现行独立接受、费用、身份、CD与合法并行；拒绝不能先取消旧动作。
- 切控：清玩家缓存/未释放副手；原主手可继续一次释放；在途弹体保来源。
- 死亡/破势/禁用：依原生命周期清未释放与相应派生，不退已接受费用。
- Y：原共享服务独立请求，不伪装技能CD；真实余额不由HUD持有。

## HUD 基线与接线表（1920×1080）
| 六区 | 几何基线 | 资源/消费者 | Unity真值/本批状态 |
|---|---|---|---|
| 主角 | 34,41,529,163 | exploration-hud.ts partyCluster；geometry/bar/square | 固定身份Root/Vitals/Stamina/Posture/Equipment/共享瓶；待绑定 |
| 伙伴 | 3,860,377,220 | 同上；portraits/florecia-user.png、portrait/frame.png | 固定伙伴身份，Current标记变化；待绑定 |
| 世界 | 1648,17,259,117 | geometry-hud.css world-cluster | 当前候选场/暂停；天气日历未迁移具名禁用 |
| 主控技能 | 1665,760,254,273 | diamond/preview.png；skill-metal/*.svg；rightSlots | Current技能配置/运行时CD；待接两代表技能 |
| 生命力入口 | 1568,1050,350,30 | vitality-cluster | 相关系统未迁移；保留原位禁用 |
| 背包快捷栏 | 550,965,866,110 | quick-reserved；geometry/square | 完整库存VC02，本轮保留原位禁用，不伪造10物品 |
缩放为min(width/1920,height/1080)，锚点保留。1440×900、1280×720已实屏采集；源码底层主角无大头像，伙伴有芙蕾茜娅头像，不能反置。正式输入待按冻结版本实际命令对账，Tab不再在新场冒充背包的切人键；历史场不改。

## 缺陷复现
- 六组持续移动后Shift虚拟输入18项通过，尚未复现物理键盘报告。首个改写旧综合夹具出现位移阈值失败（其它接受/费用正确），因加入移动前段使旧绝对位置假设不适用；失败原件保留，不改业务门限规避。
- 攻击恢复真实RED：ActionActive/MoveReady/IsMoving均true但主轨atk1。显式呈现占用政策修复后同一真实输入3项通过，纯4项通过，AttackReady未改。

## 参考采用边界
旧VELGITA真实Root、StateArbitrator、ConfigManager/Composer/TransitionRouter的职责与接口为参照：语义→配置覆盖→过渡→单帧合成，避免旧即时Submit造成多次中间派发。保留新XZ单运动入口，避免搬入旧2D物理/全局当前人物。
CRAR01已核实动画事件资料、Unit.Play主轨/方向轨、技能Attack派生链；未证实通用Composer，原型按先打断再最终资格检查存在副作用，本项目不照搬该顺序。

状态：G0资源/基线/政策证据齐备；G1仅恢复呈现缺陷已修，完整协调器/输入情境矩阵仍在实施。未宣称VC01完成或用户接受。
