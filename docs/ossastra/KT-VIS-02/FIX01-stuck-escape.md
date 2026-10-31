# KT-VIS-02 FIX01：薄墙微量重叠导致无法离墙

状态：定向修复已完成，实际 Tuanjie Play 回归通过；未扩展为任意墙体与完整游戏验收。

## 已确认的原因

固定环境身体的初始恢复逻辑跳过了 `distance <= 0.005m` 的有效 MTD（最小分离向量）。角色实际受击用的原 CharacterController 可以处于合法位置，而较大的环境净空身体仍与薄墙存在不足 5mm 的重叠。

随后从该重叠位置执行完整半径的 CapsuleCast，命中会表现为零距离、法线与扫描方向相反。原滑动逻辑据此把请求位移全部消去，连离墙方向也被当成撞墙方向，形成持续卡死。该处属于净空身体恢复与扫描起点的衔接错误，不是角色输入缺失。

Unity 的 [Physics.CapsuleCastAll 官方说明](https://docs.unity3d.com/ja/2022.3/ScriptReference/Physics.CapsuleCastAll.html)描述了起点重叠时的零距离、反扫描法线和零命中点语义。本例使用的 NonAlloc 扫描中已实测出现同样行为；不能将此法线直接当作真实的离墙法线。

## RED 证据

证据目录：`E:/WORLDCREATOR/XingHaiHuiLang/研究/可视遮挡/KT-VIS-02-FIX01/`。

- `06-escape-grid-red.json`：当前场景原有 3 个 Box 墙体 × 4 个面 × 7 个间距，共 **84 例**；其中 **30 例向外离墙请求被约束为零水平位移**。
- 代表点：前薄墙旁，角色根位置 `x=-1.5499, z=-13`，原 CharacterController 合法；MTD 返回 `true`，距离 `0.00009995699m`，法线 `(-1,0,0)`。
- 同点请求 `-0.08m X` 离墙却得到零水平位移；结果为 `Constrained`、`SlideIterations=1`、`LastContactNormal=(+1,0,0)`，恰好与实际分离法线相反。
- `09-input-red-result.json`：原 InputAction → Root → 唯一 Move，153 帧非零移动请求，153 帧环境侧阻，实际位移 **0m**。输入为 InputSystem 虚拟键盘事件，含原焦点回调夹具，不代签实体键鼠。

## 旧验收漏测

既有验证覆盖了约 `0.15m` 的初始重叠恢复、正常扫描和贴墙滑动，但没有覆盖 **0～5mm 微量重叠下主动向外／沿墙移动是否仍能脱离**。单独证明不穿墙或没有异常，不能证明角色可以离开接触位置。

## 最小修复

修改点仅限 [CharacterEnvironmentClearance.cs](../../../Assets/OSSASTRA/Scripts/Characters/Environment/CharacterEnvironmentClearance.cs) 的初始恢复判定：

1. 对返回成功、距离非负且有限、法线有效的侧向 MTD，不再跳过 0～5mm；即使距离为零，也补足既有 `contactMargin`，使后续扫描起点真正分离。
2. 保留非侧向接触、未知 MTD、无效数值／法线的失败关闭；不增加“无法求解就放行”的逃生分支。
3. 每次修正后重新查询；所有修正及 margin 均计入累计恢复上限。保留恢复次数、最终验证轮和滑动次数上限，避免内角或多墙重叠时无限推出。

## 必须保持的边界

- `Constrain` 只返回位移，不直接移动角色；[CharacterMovement.cs](../../../Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterMovement.cs) 仍由原移动链合成普通／闪避／受迫位移后，调用唯一一次 `CharacterController.Move`。
- 不扩大原 CharacterController，不修改受击形状、伤害候选、动画或输入所有权。
- 没有水平请求时不主动推根，纯镜头旋转仍不得改变角色根位置；原环境侧阻反馈继续参与受迫运动停止。
- 不通过关闭墙体、关闭净空、传送角色或放宽未解重叠来掩盖问题。

## 实际 GREEN 与范围

| 检查 | 结果 | 原始证据 |
|---|---|---|
| 相同 84 个墙面/间距向外请求 | 84/84 可退出，原先 30 个失败归零 | `14-escape-grid-green.json` |
| 原 InputAction 同点向外行走 0.8s | A 走出 2.4149m；B 切控后走出 2.4115m | `17-input-green-result.json`、`26-other-result.json` |
| 原输入持续入墙 | 保持墙外 x=-1.565，仅向外补 0.0151m；151 帧持续受阻 | `20-inward-result.json` |
| 原输入切向行走 | 沿墙走出 1.835m，终点 z=-11.165 受下一面原小墙阻挡 | `23-tangent-result.json` |
| 84 位置×退出/入墙/切向/无水平请求 | **336/336**；独立墙面间距、原生终点 MTD、明确成功状态联合检查；Idle 不推根 | `27-contact-regression.json` |
| 双墙初始穿入 0.15m 及 0.003m | 两项均恢复分离；微穿入在 3 轮内结束，两方向各恢复约 0.018m | `28-module-regression.json`、`29-micro-corner.json` |
| 正常 sweep/slide、非侧向拒绝、溢出拒绝、禁用释放/重启、原CC/受击几何边界 | 原夹具 8 项断言全部通过；函数不直接移动 Root | 同上 |
| 退出与保护 | Edit ready，原 scene dirty=true 保留且未保存；单场、私有物理场0、标记资源/诊断输入设备/捕获租约0；Console 0error/5warning | `31-finish-ready.json`—`34-protection.json` |

生产源码仅修改 `TryRecover` 的这一条件及注释。原 Movement 只有一次 `.Move(`，原 CC r=.22/h=1.5/skin=.02/step=.2/slope45 未变；197 个保护源 SHA 保持，原 37 项 dirty 状态保持。编译完成且实际加载的新代码已由同一 RED 用例转 GREEN 验证。

原场景运行时卡点坐标在开始诊断前已随用户退出 Play 丢失，因此以上是截图同组真实薄墙上的确定性复现，不声称保留了截图那一帧精确坐标。所有 Teleport 只用于测试布置/恢复；实际逃脱的证据来自原 InputAction 移动链，修复中没有传送逻辑。

原模块夹具在此位置未命中原平台 MeshCollider（`FIXTURE_PASS_MESH_NOT_COVERED`），本轮不据此宣称新的 Mesh 全域验收；渲染、闪避与受迫执行未因本次不涉及的逻辑重跑。已有相应验证继续参考 [VALIDATION.md](VALIDATION.md)。未知 MTD、无法恢复的深重叠仍按原合同拒绝；本次未加入宽松穿墙逃生。

可复跑原生脚本见 [verification/fix01](verification/fix01/)；`EscapeGrid.cs.txt` 为同一 RED/GREEN 用例，`ContactRegression.cs.txt` 为完整接触回归。需当前 Lab 正常 Play、现有角色空闲，Root 独占 Unity writer；不要直接在其他场景执行。原输入事件脚本沿用 KT-VIS-02 有界诊断夹具，完整回执在上述本机证据目录。
