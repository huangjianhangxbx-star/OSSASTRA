# M02B2 验证记录

2026-10-21，同一最终生产源码。证据位于 Candidates/M02B2/Evidence，测试代码位于 Tests。真实Unity物理/InputAction/EnemyRoot流与纯状态精确delta分开记录。

## 实际执行

|检查|最终结果|
|---|---|
|Posture纯状态/冻结载荷|23/23|
|既有M01纯核心（直接编译Assets内Core）|29/29|
|B1纯战斗|13/13|
|B2真实世界输入/释放/硬直/切控/跟随/死亡|22/22|
|B2边界/生命周期|26/26|
|旧ARCH01 / B1真实世界|各22/22|
|旧R1真实切控/跟随|19/19|
|旧M01 / M02A共享影响定向|3/3、5/5（不是全部历史回归）|
|NPC实际近距/超距请求|接受/拒绝符合预期|
|最终Console最近300秒错误缓存|0；不代表整个编辑器历史无错误|
|受保护既有场景/Prefab/meta快照|78项SHA一致|

最终场审计 MissingScript=0、Probe=0；旧8个角色引用完整；原InputSettings和原设备已恢复。B2新场保存并重开后3个独立架势实例满值。截图仅本地work，不上传用户媒体。

## 真实失败保留

- 纯RED为新增源文件尚不存在的结构性编译失败，非伪称业务断言RED。原实际物理流HP72→60而无Posture，记录world-red。
- 世界第一轮21/22：测试Reset触发Focus held保护后立即同帧Tab，测试脚本改成真实分帧释放/再按；第二轮22/22。
- 边界第一轮18项后因测试创建非法NaN定义提前抛异常；改为断言构造拒绝，未为测试放宽生产验证。
- 边界第二轮25/26：发现根禁用后仍可接收新架势冲击，留下无时钟的硬直。修复为显式注入驱动Behaviour并检查isActiveAndEnabled，最终世界22/22、边界26/26。
- 编辑器一次prepare被EDITOR_RESTARTED中断；通过新readiness请求恢复，未将中断当成功。退出Play的未保存现场经用户确认未编辑后重开磁盘版本。

## 任务合同映射

|合同|结果与证据|
|---|---|
|B2-01|PASS：最终3实例审计、NPC边界|
|B2-02|PASS：纯冻结与world Accepted无效果|
|B2-03|PASS：world A真实释放HP/姿态|
|B2-04|PASS：world B切换后独立来源|
|B2-05|PASS：world敌方Update两击|
|B2-06|PASS：纯与world当次归零|
|B2-07|PASS：boundary硬直再伤不续期|
|B2-08|PASS：纯精确0.6与world owner保留|
|B2-09|PASS：纯精确等待/恢复/边界|
|B2-10|PASS：纯主动作恢复门、boundary暂停失焦、world切控|
|B2-11|PASS：world玩家前摇实际取消|
|B2-12|PASS：world敌方前摇实际取消|
|B2-13|PASS：boundary已释放不回滚/不重复|
|B2-14|PASS：world持键无补攻击、移动重采样|
|B2-15|PASS：world硬直切走及切回|
|B2-16|PASS：world伙伴停止/恢复；R1真实阻挡回归|
|B2-17|PASS：world致死交接/全灭、boundary无死后恢复|
|B2-18|定向PASS：boundary多Collider/空挥/同阵营/同ID/死者；旧世界敌方LOS墙后门。没有新增或验证玩家普攻通用穿墙阻断，不作此声明|
|B2-19|PASS：boundary重复Release/错身份/禁用来源|
|B2-20|PASS：boundary禁用/销毁/根禁用及独立owner；真实失败已修|
|B2-21|PASS：同Health实例重置及多轮运行|
|B2-22|PASS：npc-final近远回执、boundary三根独立|
|B2-23|PASS：上述旧场同版定向回归|
|B2-24|本地审计PASS；远端SHA见publication，不以本地commit代替远端验证|

## 未执行/范围限制

未作硬件手柄/长期手感验收、完整构建/干净克隆运行、正式动画/美术验收、格挡闪避0伤姿态、生产复活/持久化或正式敌人AI。截图显示开发滚动面板，无新正式HUD。原媒体/全局渲染无修改，但78项保护快照并非整个磁盘媒体的完整哈希基线。已有旧禁编asmdef及meta删除保留不提交。

ChatGPT链接是登录墙，本轮依据完整本地任务单和当前代码，不声称读到登录后最新对话。

序列化差异检查：Unity自动保存的新场景含空YAML字段尾空格，按场景禁止文本改写规则保留。源码/文档/测试排除Unity序列化文件后的staged diff --check通过。最终编辑器停B2测试场Edit、无未保存标记。
