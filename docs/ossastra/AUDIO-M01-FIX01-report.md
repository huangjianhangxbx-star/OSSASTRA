# AUDIO-M01-FIX01｜清场后误触发战斗音乐

2026-10-25；基准93d126c。用户报告右侧点位即使已无敌人仍转遇敌BGM。只修复被复现的交战资格错误，未给坐标添加例外。

## 复现和原因

在自有BGM场将唯一敌人真正结算到HP0并调用原死亡处理，再将主控置于(5,.03,-3)。两次独立红测均为：

1. 静站3.5秒：Engaged=false，explore-far，仍为home。区域本身没有选择战斗音乐。
2. 经正式InputSystem的RT空挥，角色真实Accepted=1：Engaged=true，combat，chapter1——即使全部敌人已死。
3. 原平静约3秒后恢复home。

根因是 `CombatEngagementSession.OnFact` 把所有动作Accepted无条件视作交战。音乐忠实读取了错误的上游事实；原坐标附近有敌人不代表“坐标触发器”存在。单纯无输入站立的自动误触发在此复现未出现，不把它描述成已经复现的独立根因。

候选排查还覆盖隐藏活敌、旧目标和脱战计时：死亡/禁用可用性及区域同home映射是现有真实代码；本例由Accepted明确触发。尚有其他未复现的纯站立情形时须以现场新事实继续定位，不能凭本次推断包治。

## 最小修复

生产仅修改 `Assets/OSSASTRA/Scripts/Combat/Runtime/CombatEngagementSession.cs`：Accepted启战/重置平静窗口之前，场景显式敌方表须至少有一个启用、存活、Vitals可用、非Neutral且与有效队员敌对的EnemyRoot。维持威胁采用同一资格。复用既有身份/阵营判定，不复制HP。

空挥/练习仍正常受理；全死、Root禁用、Vitals禁用、友方、中立、销毁引用均不能凭空启动交战或不断重置calm。原3秒quietDuration、Time.deltaTime计时及CanSample暂停语义保持。BGM代码、角色根、AI、动画/模型/输入/装备、场景和SO没有改动，也没有第二套音乐战斗判定。

边界：仍有远处活敌时，原Accepted启战策略仍保留；本轮没有改成距离、LOS、伤害、仇恨的完整交战模型，也未同步修改旧原型仓库。

## 定向验证

证据 `Candidates/AUDIO-M01-FIX01/Results`，测试源码 `Tests/*.cs.txt`。

- NoEnemyPointRepro：修改前两次同样失败；修改后3/3通过，死敌点位的RT仍Accepted但Engaged=false，explore-far/home保持。
- EncounterAvailabilityContracts：7条实体路径共15项断言，15/15通过，均使用真实RT受理，不伪造OnFact/Engaged。死/禁用Root/禁用Vitals/友方/Neutral/销毁均不入战；活敌附近真实命中HP600→588，转combat；撤威胁后QuietTime=3.00013733返回home。
- 已修正上轮AudioLiveStory夹具：两次RT前放近活敌，之后移远等原calm。本轮两次交战、3.00144053秒退战、同home、UI、duck、TimeScale和Stop链均通过。主输出两个RMS断言失败：实际只读确认EditorUtility.audioMasterMute=true、listenerVolume=1、source.volume=.0756、源持续播放。保持用户静音；不将本轮输出称为重新听音/音频输出验收通过。
- 修正上轮07/08验收结论：旧夹具将敌人移到100米外，空挥Accepted不能证明真实遇敌。保留历史原始结果，以本轮附近活敌/清场反例替代对应结论。
- 原372个非目标保护路径SHA/缺失状态保持，原36dirty保留；音频源码/资源/新音频场/Feedback与HEAD无diff。结束后恢复自有场Edit clean、原敌人对象(4,.03,-3)、零测试Gamepad，无资产保存；用户全局静音保持true。

中文注释核对：生产1份文件已同步文件头和关键门控解释；测试2份及修改的旧夹具有中文职责/边界说明。未改开发细则、第三方或用户区。Player旧第三方构建阻塞沿用前轮结论，本轮无Player代码/配置变更，未为此重复构建。

## 发布

按R11仅定向GitHub main发布本任务源、测试、数值证据及记录，不Gitee、不含原作媒体。产品SHA在发布后回执核对；完成即释放所有租约，不扩大下一阶段。
