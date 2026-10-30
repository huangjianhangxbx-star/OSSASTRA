# ANKALUTE 共享项目状态

## KT-VIS-02 已完成（2026-10-31）

- 固定环境身体经可选ICharacterEnvironmentClearance接原Movement唯一Move前；原CC受击形状不变，原forced链消费环境侧阻。两场景角色组件仅显式5实墙，数学primitive在独立PhysicsScene且精确释放。
- 既有有限组颜色/深度后端新增独立时序、空间羽化细点阵及共享最低opacity；保留原SO、只读Original与两个候选，同机位切换保留State/资源。新CPU26/GPU40合同、实际移动/坡道/闪避/受迫/八向零推根/双角色支撑切控及保存重开已验，细节及未覆盖边界见docs/ossastra/KT-VIS-02/VALIDATION.md。
- 功能13522ad已GitHub核验；最终单一Lab Edit ready/clean、所有标记自有渲染/物理查询资源与输入租约0、37dirty/197保护源保持，全部writer释放。GPU耗时/Player/实体键鼠与美术最终调优未认证。

## 项目边界

> 2026-09-09 11:40连续实施更新：真实普通攻击按钮/取消/非空target proxy与FlowStateMachine敌方平A已通过，FlowRoot创建前冻结Standard策略、绑定后startup、BattleStarted先于actor已通过；磁盘配置仍关闭，非F8切换。Legacy Defend已通过唯一Inbox接续TurnEntry；Legacy Skill致死→整批Wave实景RED→GREEN，一敌一批及两敌部分清场均通过，禁止WholeWave模式单体补员。修复后基础121、入口55、startup23、Root/结算/Wave等36项回归通过。**目前正在用原始敌人build/正常AI连续验证，已发现并正在验证修复Legacy演出期间AI重复提交；不是全部正式验收通过。** 原SceneDirty保留、正式资产未改、同一active writer继续。详细接续见Task11记录。

> 2026-09-09 02:54正在连续实施：正式绑定已加入共享Wave factory/冻结Formation槽位/同池Sprite交接，显式CreateWaveSession已走通Startup→首批Wave→候选→连续5笔平A（enemy2、impact/AC各5），以及Root末敌退场→第二批（2→2，AC3）实际PlayMode验证。联测修复退场后Enemy.IsDeployed残留，56项窄回归通过。**仍测试basic-only与直接目标入口，F8保持Legacy；TurnEntry致死、故障、正式AI/UI等剩余继续active，不能宣称完整交付。**未改正式资产，原SceneDirty保留；writer仍由同一任务持有。详见Task11新记录；下面是历史节点。

> 2026-09-09 01:12最新：新增实际 `BattleWaveSpritePresentationPort`，复用宣告栏，冻结整批身份/槽位、同池暂存、DOTween横向OutCubic入场≤.5s总错峰、全员home+2render帧ready、normal cleanup后由外层原实例adopt。与真实Wave Run/Lifecycle/State/Inbox联测正常/故障通过；新9项含取消重入ack、就绪后角色丢失/home漂移保护，既有回归与Foundation121、Entry55、Startup23通过。实际同Session连续5笔平A再次PASS（3敌方，impact/AC各5）。**正式Session/PresentationRoot/代际port slot尚未绑定此port，冻结Formation供给、adoption与continuation调用点仍待接；F8仍Legacy，无新Wave录像/布局验收。**新测试用正式Sprite资源但手动时钟与独立投影fixture，不等于正式波次可玩交付。最终EditMode、SceneDirty=True保留、compile0、Console error0/warning42；无正式资产修改，writer释放，无后台任务。详情见Task11记录01:10/01:12。

> 2026-09-09 00:49最新：Wave角色池新增同池暂存scope，只有该pool当前Wave projection可创建；暂存view不进active池，Collider2D禁用；所有已stage演员均规则部署后才能原实例整体adopt，取消/投影关闭回收。实际绑定回调中取消导致回收后重激活、碰撞体提前启用两例RED→GREEN。新9项含正式Sprite prefab/profile身份和位姿读回通过，既有projection/Wave/Root/Stage9及Foundation121、Entry55、Startup23通过；实际Sprite同Session连续5笔再次PASS（3敌方、impact/AC各5）。**这是暂存交接组件，未接完整Sprite Wave port/Session driver/adoption调用点，没有新Wave动画或录像，F8仍Legacy。**最终EditMode、测试场3 SceneDirty=True原样保留、compile0、Console error0/warning42；未改正式资产，writer释放、无后台任务。详见Task11记录00:49。

> 2026-09-09 00:15最新：新增Wave演出协议Run，按宣告时长+render帧→stage→aggregate及全member ready→normal cleanup确认→原子部署一次Inbox提交；复用watchdog并在port pump前处理超时，晚ready/晚cleanup不能抢先提交，取消走撤回确认且不代外层释放rule lease。受控port+真实State联测6项及既有回归PASS，Foundation121/Entry55/Startup23通过。**仍未接正式Sprite Wave port或Session driver，无新Wave画面，F8仍Legacy；超时degraded/placeholder及永久cleanup恢复仍缺。**本轮无PlayMode/录像，00:02是最后实际连续5笔证据。最终EditMode、SceneDirty=True保持、compile0、Console error0/warning16；未改正式资产，writer释放、无后台任务。详见Task11记录00:15。

> 2026-09-09 00:02最新：Composition.Build已能在显式wavePlan下持有唯一Wave规则Lifecycle，将分批计划→全ready原子部署→一次Inbox完成→exact typed全退场→下一批连起来；真实State 2+1部署/仅清空规则列表仍拒绝/party defeat优先/取消迟到回调4项通过。Entry55、Startup23、基础121及Wave/Root/Stage9回归通过；实际Sprite连续5笔再次PASS。**这是dormant规则组合，不是完整Wave presentation Module或正式入口：宣告/入场/placeholder/watchdog/cleanup、Legacy前驱、startup Wave1和Factory/F8仍未接。**F8仍Legacy，没有新录像；最终EditMode、SceneDirty=True保留、compile0、Console error0/warning42、host0；未改正式资产，writer释放、无后台任务。详情见Task11文件00:00/00:02。

> 2026-09-08 23:42最新：TurnEntry completion新增最终提交后的immutable revision并纳入fingerprint，已覆盖eligible消费scheduler与skip重新排程；Wave退场ledger现在也接受真实TurnEntry→Inbox封存记录，旧revision/重复/同批存活成员保护通过。真实TurnStart中毒致死模块集成RED→GREEN；候选/TurnEntry55、Startup23、Foundation121及Wave/Root/Stage9相关回归PASS。**仍非实际Wave入场/F8交付：WaveModule/port/composition/Legacy precursor未接，TurnEntry空敌场保护仍保留，F8仍Legacy。**本轮无PlayMode或新录像，最后实景证据仍22:54。最终EditMode、SceneDirty=True保留、compile0、Console error0/warning16；未改正式资产，writer已释放、无后台任务。详见Task11记录23:42。

> 2026-09-08 22:54最新：Task12补Wave immutable completion→唯一Inbox→后续Pump原子TurnEntry转交；真实State整批ready提交时封存completion，不从live状态重建；新增本批exact authored→runtime退场集合（当前只支持已封存Root source，尚未装入Wave生命周期）。新增Wave continuation7与既有wave11、Inbox/Root/Stage9等回归通过；Entry54、Startup23、Foundation121/121 PASS。真实Sprite同Session连续5笔再次PASS（3敌方，impact/AC各5）。**完整Wave Module/实际入场/TurnEntry和Legacy wave precursor/初始波接线仍未完成，F8仍Legacy；这是核心接续基础，不是生产波次交付。**最终EditMode、原SceneDirty=True保留、compile0、Console error0/warning42、formal host0；未改正式资产、无新录像、全部writer释放、无后台任务。详细证据和下一步见Task11任务22:53/22:54记录。

> 2026-09-08 22:23最新：Task11补partial candidate scope失败后的引用/清理保留，entry54通过；Task12新增确定性WavePlan/versioned只读旧配置迁移、必填BattleSeed与真实State原子部署ready门，wave6+5通过，两次可逆行为mutation准确失败后恢复GREEN。Foundation121和Root/Stage9等回归通过，实际同Session连续5笔再次PASS。**这些是Task12核心primitive，尚无完整Wave module/typed清场集合/实际入场/跨模块接续；F8仍Legacy，完整反馈、失能、Task11收尾和Task13/14仍未完成。**用户明确录像不是最终效果标准，不能因此跳过原规划。最终EditMode、SceneDirty=True保留、compile0、Console error0/warning42、host0；未保存正式资产，无新录像，全部writer释放、无后台任务。详见任务22:20/22:23记录。

> 2026-09-08 21:09最新：已修复实景Root阶段9完成后、continuation校验拒绝造成的永久持锁（真实RED→GREEN）。保留原lease等待cleanup，Session封技术终态后释放并一次BF；不重算HP或改已封存完成记录。实景接续前/后故障均PASS（impact1；AC分别0/1；BE/next actor0；BF/host Aborted1），正常连续5笔再次PASS。Foundation121、entry52、startup23及Finalization8/Root11/Settlement8/SafeEvent3/Standard1通过。最终EditMode、SceneDirty=True保留、host0、Console error0/warning96。**仍非F8验收：Task11永久cleanup失败恢复与无环composition等边界、Task12整批Wave、Task13正式AI/UI/F8及己方独立失能绑定待做；F8仍Legacy。**未改正式资产，无新录像；Task11 paused/read-only、全部writer释放、无后台任务。详细证据/hash见任务21:09记录；下方为历史。

> 2026-09-08 20:47最新：Startup/TurnEntry技术fault已补清理后独立BattleFaulted→FlowStateMachine host Aborted；延迟ack、交接后factory失败、scope清理抛错重试和观察者/restart重入4项RED→GREEN。正式Sprite入场port丢失/首候选actor丢失两例通过，AC/BE/actor发布均0、BF/host各1。连续5笔再次通过；Foundation121、candidate/entry52、startup23、finalization/Root/settlement/safe-event回归通过。EditMode、SceneDirty=True保留、probe空、编译无错误、真实Console error=0。**Task11仍未全部完成：Root完成后continuation拒绝、永久cleanup失败恢复、无环对象图待做；己方失能资源/绑定、整批Wave、正式AI/UI/F8未接，F8仍Legacy。**本轮未改正式美术/资产或保存Scene，未借Hit/Broken图充当独立失能资源；详细下一步和hash见Task11记录20:47。全部writer释放，无后台工作；下方为历史。

> 2026-09-08 19:52最新：Root故障已封Faulted并等待cleanup确认再释放projection/rule；Session独立BattleFaulted，实际FlowStateMachine一次Aborted交回host，不伪造领域BattleEnded。真实演员丢失命中前/后、抛错/重入观察者通过。正式Prefab缺失X引用已增量补齐，真实敌方致死完成X/dim→原演员回Home→退场→己方中毒/再生→Victory；最终录像`C:/Users/Administrator/Documents/Codex/2026-09-02/new-chat-2/artifacts/ankalute-2026-09-08-defeat/05-enemy-defeat-readable-x.mp4`已抽帧核验。连续5笔、Foundation121/入口48/启动23/Finalization7等通过；编译0/0、Console无error、EditMode、probe空。**Task11未全部完成：非Root故障/cleanup拒绝或丢失ack的恢复/无环composition待收口；己方失能模型、整批Wave及正式F8/AI/UI未接，F8仍Legacy。**只保存X增量Prefab，SceneDirty=True保留未保存、不清脏树。Task11 paused/read-only、全部租约释放、无后台任务。详见任务19:47/19:52；下方为历史。

> 2026-09-08 19:02最新平A节点：Root最后Stage9屏障→关闭projection→唯一typed Inbox→后续Pump完成通知并转交TurnEntry已接；同一个显式Standard Session真实资源连续5笔平A（3笔敌方），impact=5、ActionCompleted=5。录像在`C:/Users/Administrator/Documents/Codex/2026-09-02/new-chat-2/artifacts/ankalute-2026-09-08-continuation/03-five-consecutive-actions.mp4`。真实Abort-on-completion及观察者异常隔离通过；基础121/候选入口48/启动接续23及新专项通过，最终EditMode、SceneDirty=False、编译0/0、Console无error、probe host空。**F8仍Legacy，测试直接提交目标且enemy使用显式测试basic-only构筑；不等于正式AI/UI验收。真实致死因Defeat marker/world dim绑定缺失失败；Task11故障收尾、死亡资源、整批增援与正式入口仍待实施。**未保存正式资产、未清理脏树。Task11 paused/read-only、全部租约释放、无后台工作，详情见对应任务19:02记录。下方15:33及更早条目为历史。

> 2026-09-08最新平A节点：同Session Startup→首TurnEntry、延迟Abort及正式PresentationRoot显式Prepared绑定已实现；正式战斗Prefab增量绑定宣告栏与Sprite演出。已使用真实F8队伍/敌人/背景录得己方3–9、敌方3–9及己方中毒→己方再生→敌方中毒的顺序结算。短片位于`C:/Users/Administrator/Documents/Codex/2026-09-02/new-chat-2/artifacts/ankalute-2026-09-08/`。基础121、入口48、启动接续23、新adapter2项通过。**F8仍Legacy；最终完成/下一事务、整批增援与正式失能资产接线仍未完成。** 实景动作由集成测试驱动，不是UI输入验收；不以此计算总完成率。已达用户允许的可见验证节点并释放writer，无后台任务，场景未保存修改。详细证据/剩余项见Task10D任务文档15:33记录。

- 本项目是 **异界地宫 / ANKALUTE**。
- `E:\WORLDCREATOR\Project_AMIAS` 只作为冻结的旧版回合制参考，不得修改。
- `E:\WORLDCREATOR\new\Project_VELGITA` 是异度结弦，不得因 ANKALUTE 任务而顺手修改。
- 长期架构、主角规则、Break 规则和废弃资产规则以 `$project-ankalute-memory` 为准。

## 当前代码版图

- 2026-09-08 13:45：Task10D接通同Session的TurnEntry BattleResult/Defeat交付：source精确一次性terminal→Inbox下一Pump→清表现scope→Session.CompleteBattle→通知结束释放lease；不生成ActionCompleted。修复真实BattleEnded回调清空Session后Flow.Update空引用。43专项、Startup23、Foundation121通过；强化Console异常即FAIL后的Flow实帧玩家/敌方/延迟战败3例及原9例通过。修正测试导演未绑定的两条异常，不把首次PASS当无错误；最终EditMode/测试场3/SceneDirty=False、编译0/0、Console无error。**Startup/PresentationRoot首次创建和factory绑定、关闭生命周期、Root最终化、整批wave及正式F8仍未接，F8仍Legacy。**未保存正式资产或清理脏树，Task10D paused/read-only、全部租约释放，无后台工作；接续以任务记录13:44–13:45为准，不重做已完模块。

- 2026-09-08 13:04：Task10D实际Flow.Update已驱动TurnEntry module→唯一Inbox→下一Pump→Session普通actor/token发布，回调结束前domain input保持关闭；补非平A选择入口的锁检查。旧/新候选准备共用helper，敌方Windup/Scheduled只准备不触发TurnStart，同time先处理准备；SelectAgain自动原子转移至新候选并通过factory创建后续module。新增真正Sprite projection/adapter factory及scope清理；42专项、Startup23、Foundation121通过，新增Flow/Session两侧实帧2例和旧TurnEntry9例通过，最终编译0/0、EditMode/测试场3/SceneDirty=False、Console无error。**正式Startup/PresentationRoot首次创建与factory绑定、BattleResult接收及关闭生命周期仍未接；Root最终化/整批wave/正式F8仍待做，F8仍Legacy。**本轮未保存正式资产或清理脏树，Task10D paused/read-only、全部租约释放，无后台工作；准确下一接续见任务记录13:04，不将专项数量当总体完成率。

- 2026-09-08 12:25：Task10D普通玩家/敌方skip及死候选续选接通到source terminal；Session与source共享pending时间线效果，敌方skip复用原AI准备/换位/Wait策略，成功后一次重排程。typed TurnEntry完成进入唯一Inbox/Router：重复幂等、原physical lease校验、下一Pump转交，SelectAgain期间无输入间隙。35专项、原Startup23及Foundation121通过，编译0/0，EditMode/测试场3/SceneDirty=False，缓存Console无error。**正式Composition/Session/Flow接收端及自动Tick/Submit尚未接；旧计划准备/玩家响应语义仍需迁移，空敌场wave/outcome上下文仍待做。F8保持Legacy，不能将纯模块分发通过当正式入口完成。**Task10D paused/read-only、全部租约释放，无后台任务；详情见Task10D记录12:25。

- 2026-09-08 11:39：Task10D新增部分候选terminal并纠正中断代码的旧队首消费/敌方计划丢失。正常候选一次签发token；真实TurnStart把候选time10推至35后仍消费原actor、logical time保持10并递增revision；敌方转Executing保留计划/目标；己方全灭优先Defeat（含互灭）；Uninterruptible强制skip保留原计划并SelectAgain。candidate/module30项及Foundation121通过，编译0/0，EditMode/测试场3/SceneDirty=False，MCP缓存Console无error。**普通skip重排程、其他队友存活时的死候选续选、wave/outcome上下文、typed inbox/Session.CurrentActor及正式对象图/input接线未完成；空敌场目前保护性拒绝且保持锁，不能接F8。F8仍Legacy。**项目Skill已1.0.4，MCP v0.6.7实际连接ANKALUTE；Project_ANOIR是配置的Product name。任务paused/read-only、全部租约释放，无后台工作。详情见Task10D记录。本轮不增加production25矩阵或总体完成率。

- 2026-09-07 15:59：新增仅Editor的一键观看入口 `Tools/ANKALUTE/DRPG/Basic Attack/Watch Current Visual Demo`，不替换F8、不保存正式资产。它自动进入Play并串行播放当前TurnEntry 9例、Stage9/Root 14例、命中/反应/死亡/双方回位34例；状态查询与停止菜单同组。2427先RED→2432 GREEN，2433–2437真实菜单整链57例全部PASS且SceneDirty=False；2440 EditMode、2441编译0/0、2442 Console无error。该入口是当前成果演示，不代表正式Session/Flow/F8接线完成；Task10D剩余不变。任务complete/read-only、全部租约释放，细节见`tasks/2026-09-07-basic-attack-visual-demo.md`。

- 2026-09-07 15:35：Task10D候选收尾预检已接到真实module最终屏障。State-owned source仅在原State/physical lease/exact reservation/有效plan pin、真实formation完成及live scope/revision/clock仍有效时冻结最终候选事实；保留原候选与结算后时间线，不改CurrentActor、不消费候选或放开输入。入口17项（含5项新增先RED后GREEN）、原candidate8、Foundation121/Motion7与shared/startup/Root回归通过；强化后的9个真实Sprite帧改用State真正reserve并验证逐帧禁止早取收尾facts，全部PASS。**仍仅preflight：outcome/skip重排程/唯一consume-discard/typed completion/actor-token publication及Composition/Startup/Flow/domain输入未接；F8仍Legacy。**manifest42/59不变，registered矩阵本节点未重跑。2420最终EditMode/SceneDirty=False/runner空，2418编译0/0；**2419播放退出新增inactive GameFlowManager上的StopMusic协程错误，音频退出清理未修，不能宣称Console无error**。详见Task10D记录15:35与4源码hash。全部租约释放、paused/read-only、无后台工作；519脏项保留，无正式资产保存/提交/清理，Sprite不等Spine。

- 2026-09-07 14:56：Task10D候选source迁移节点完成。State-owned `DRPGTurnCandidateAuthority`为旧Session和新TurnEntry提供同一reservation/plan表，纯reserve、exact reservation与plan引用、原physical lease pin；已锁候选不能被旧接口消费/丢弃，plan被替换后规则提交与演出停止并保留owner。8专项RED→GREEN，入口/Root/shared/startup回归及Foundation121/Motion7通过，新adapter9、旧Stage9/Root/widgets14、原34真实帧通过；固定manifest42/59不变，registered矩阵本轮未重跑，不增加上次5/25计数。**仅source/pin完成；新候选terminal（consume/discard/skip重排程/plan refresh）、outcome/typed completion/actor-token publication及Composition/Startup/Flow/domain输入仍待实施，不能提前将未完成的pin生命周期用于正式F8；F8仍Legacy。**Task11–14未完成。2364–2367最终正确项目/测试场3、EditMode、SceneDirty=False、编译0/0、真实Console无error、runner Phase空。精确5源码hash与接续见Task10D记录14:56；全部租约释放，paused/read-only，无后台工作；519脏项保留，无正式资产保存/提交/清理，Sprite不等Spine。以下为历史。

- 2026-09-07 14:21：Task10D生产TurnEntry表现适配节点完成。新`BattleTurnEntryPresentationAdapter`从冻结合法Home捕获顺序，复用真实状态cursor/TMP/HUD、独立生命周期提示(.45秒+新帧)、typed death及整体formation，修正生命周期插入后的final ordinal；运动随projection创建。9专项检查及9个正式adapter Sprite实帧通过，原Stage9/Root/widgets14与原34实帧、旧规则/候选/Startup回归、Foundation121/Motion7全通过；固定manifest42/59、registered5 Passed/20 Pending/0 Failed不变。**adapter尚未绑定正式Session/Composition/Flow，候选source/exact enemy plan pin/skip重排程、outcome/typed terminal/consume-discard/actor-token publication和单domain输入仍待做；F8仍Legacy。**不要重做已完成module/adapter；接续及8源码hash见Task10D记录14:21。最终EditMode、SceneDirty=False、编译0/0、真实Console无error，无后台工作；全部租约释放，Task10D paused/read-only。519折叠脏项保留，无正式资产保存/提交/清理，Sprite方案不等待Spine。以下条目为历史状态。

- 2026-09-07 13:33：Task10D候选原子生命周期及入口模块规则/演出屏障节点完成。`State.TryCommitTurnEntry`一次提交清Defend→TurnStart闭包→PostureBreak skip/recovery/regen与logical-time；新`DRPGBattleTurnEntryModule`由后续Tick驱动serial→必要lifecycle(.45s)→typed death→共用reconcile→formation(.30s+render fence)，只到ReadyForFinalization，原owner/input/reservation仍锁定，无ActionId/ActionCompleted。20新检查和5个新module真实Sprite帧案例通过，旧规则/Foundation121/Motion7及7/14/34实帧通过；manifest仍42/59，registered5 Passed/20 Pending/0 Failed。**仍待生产TurnEntry port/lifecycle widget与ordinal适配、candidate source/exact enemy plan pin/skip重排程、outcome/typed terminal/正式actor publication、Composition/Startup/Flow/input；F8仍Legacy。**不能从未消费reservation直接peek同候选。2218–2221正确项目/测试场3，EditMode、SceneDirty=False、编译0/0、真实Console无error、runner Phase空。13源码/meta/4文档租约释放，Task10D paused/read-only，无后台工作；519折叠脏项保留，不保存正式资产/提交/清理。最新接续/hash见任务记录13:33/13:30，优先于下方历史。

- 2026-09-07 12:43：Task10D共享死亡规则与整体重排节点完成。State-owned唯一post-settlement reconcile只接受本State已提交ledger+原始physical lease+typed rendered departure，一次移除死亡候选/整体压缩阵型；Root委托同一store。TurnEntry共享冻结journal、full-owner motion/layout及至少.30秒同批平滑重排+新Home渲染门。新增13检查、7实帧通过；原34实帧、Stage9/Root/控件14实帧与Foundation121/Motion7通过；固定manifest仍42/59，registered5 Passed/20 Pending/0 Failed。**TurnEntry module、候选Defend/PostureBreak/一次logical-time、正式候选发布/结果续流及Session/Flow/输入仍未接；F8仍Legacy。**2103–2106实读EditMode、SceneDirty=False、编译0/0、真实Console无error、runner Phase空。10脚本/meta及全部租约释放，Task10D paused/read-only，无后台工作；519折叠脏项保留，无场景/资产保存或commit/push/清理。最新接续/hash见任务记录12:43，优先于下方历史。

- 2026-09-07 09:26：Task10D新增共享TurnStart ledger/因果闭包、串行receipt和真实Sprite/HP/状态/死亡primitive，完整TurnEntry owner、不借ActionId；非空逐项≥.45秒、空队列≥.30秒+render fence，死者保留原view/X/dim，新HomeRendered后才失能或敌方release。新增23检查2000、原基础23检查2001、Foundation121/Motion7/manifest42/59通过；实际原34帧案例2011、Stage9/Root/控件14案例2013、新TurnEntry5案例2015通过；注册矩阵2017仍5 Passed/20 Pending/0 Failed。**共享primitive已完成，但TurnEntry module、死亡后的规则/整体formation、候选Defend/PostureBreak/时间线生命周期、Session/FlowRoot/唯一输入接线未完成，F8仍Legacy；不可把这些测试换算为总体完成率。**2018–2021实际EditMode、编译0/0、真实Console无error、SceneDirty=False、runner Phase空；无场景/资产保存、无commit/push/清dirty，519折叠脏项保留。Task10D paused/read-only，13脚本/meta/文档租约全部释放，无后台工作；精确接续与hash见任务记录09:26节点，优先于下方08:17历史说明。

- 2026-09-07 08:17：Task10D基础节点新增候选/正式turn共用单调状态边界，身份分配与matching lease激活分离；typed completion inbox/Pump不inline启动下一runner、校验精确lease、防重复/失权/重入；Startup独立模块Prepare→Enqueue→后续Tick签发entry，取消等待已创建handle清理后才释放。23项新检查与Foundation121/Motion7/manifest42/59通过，最终原34真实帧1901、新14真实帧1903、registered5 Passed/20 Pending/0 Failed1905通过。**新Startup/TurnEntry尚未接Session/FlowRoot，TurnStart可复用结算与真实演出、单输入权威、Task11–14仍未完成，F8仍Legacy。**1906–1909实际EditMode、SceneDirty=False、编译0/0、真实Console无error、runner Phase空；无保存/清dirty/commit/push，519项脏树保留。Task10D paused/read-only，9脚本及所有租约释放，无后台测试；详见任务记录08:17交接与精确下一接线点。Task10C已complete，不能把23项基础测试或42/59换算成总体完成率。

- 2026-09-07 07:48：Task10C代码与回归完成。Root自动Stage3→9并停ReadyForFinalization；三种HUD/world状态控件按冻结cursor更新，延迟canonical/LateUpdate不泄漏最终值；武器损坏的HP/架势夹取在最后可见结果投影。1809固定42/59、Motion7、Foundation121通过；1817注册production5 Passed /20 Pending /0 Failed；1780原34真实帧、1822新14真实帧通过。可逆断线mutation1802准确失败后已恢复且GREEN。1824–1827实读EditMode、SceneDirty=False、无编译error/warning、当前缓存无error、runner Phase为空，未保存/清dirty。共享树519折叠项（130M/20D/369??），未commit/push/归档。Task10C租约释放，主代理继续Task10D startup/TurnEntry/单输入权威；Task11最终推进、Task12波次、Task13正式模板/F8/完整反馈、Task14整体验收仍未完成。正式流程完成率不能用42/59或5/25代替；Sprite正常无需Spine资源。

- 2026-09-07 03:00：Root已自动串联Stage3→9：Stage8完成后冻结当前Home顺序、一次结算、Player→Enemy串行结果、新死亡可见/退场及最终formation，停在ReadyForFinalization，原actor/输入/Root锁仍持有。1688新Root7项、1683原Root11项、1689十组回归、1690Foundation121/Motion7通过；1695补充真实帧12格通过（8项Stage9+4项敌我/普通致死完整Root3→9）。1697确认EditMode、不编译；未保存场景或清dirty。状态控件/部分资源投影、Task10D/11/12/13/14与F8切换仍未完成；正式矩阵本组尚未重跑，不据此提高完成计数。唯一unity-writer继续本任务；下面02:39及更早停点为历史。

- 2026-09-07 02:39：Task10C真实Stage9 Adapter已具备逐项结果（.45秒、空阶段.30秒）及独立新死亡HomeRendered→ally失能/enemy退场→一次规则/视觉formation。旧Stage8 permit不能授权Stage9新死亡；最新Home替代旧LockBaseline检查；死亡屏障超时不能Degraded通过。12项专项、3项serial、8格补充真实Stage9帧、原34格真实帧与Foundation121/Motion7通过。**Root尚未自动接Stage9，当前继续接线；完整状态控件、资源投影、最终化/波次/F8仍待做。**正式矩阵计数不据此增加，F8仍Legacy。1646正确项目/测试场3、EditMode/SceneDirty=True，未保存或清dirty；当前唯一unity-writer仍本任务，详细证据见Task10C记录02:39。

- 2026-09-07 01:31：Task10C新增只读PresentationPlan及串行receipt sequence（可见项>=0.45秒/空阶段>=0.30秒，跨渲染门、完整身份、watchdog、重入/失权/清理异常保护），完成后仅ReadyForDefeatFinalization；投影baseline在ViewModel生成时冻结完整HP/架势/状态实例，逐项before匹配，旧HP-only更新保留语义。新增10+6项通过。注册回归曾真实复现宣告0.994秒但仅1帧；TDD修复为>=0.75秒且>=2渲染帧，敌我同规则，不弱化测试；相关fixture只对齐模拟帧输入。最终Root11、Prelude17、Motion7、Foundation121、补充34真实帧通过，manifest仍38/59，registered4 Passed/21 Pending/0 Failed。**真实Stage9 Presenter/UI、Stage7状态/架势cursor衔接、新死亡最终屏障及Root pump仍未完成；Adapter.PresentSettlement仍Unavailable，F8仍Legacy**。仅有冻结元数据不等于可见状态图标已完成。1563最终EditMode/SceneDirty=False/无pending runner，1561编译0/0、1562当前Console无error，未保存/清dirty，不关闭历史DOTween退出风险。Task10C paused/read-only，13脚本及全部租约释放；细节、hash、直接续做入口见任务文末01:31交接。Sprite正常，不等Spine资源。

- 2026-09-06 23:56：Task10C因果规则节点完成：status roots的Immediate/OnDefeat在同一新workspace按父先子后完整求值，Player→Enemy lane不被跨side child重开；真实participant sides另冻结。Stage7→9共享passive/parent去重、事件序号与32步/8层预算，晚期错误整笔丢弃且retry不消耗预算；child typed事实进入fingerprint。实际PreparedRuleAction新增仅post-return允许的TryCommitSettlement，保留Stage7封存continuation、不重复primary/costs/events。新增13项、可逆丢失passive继承mutation、原Settlement14、Reaction13、状态RunAll、资源整组、Root11、Foundation121、最终源码真实帧34通过；manifest38/59及registered4 Passed/21 Pending/0 Failed不变。**Root pump尚未调用新结算seam；可见cursor、逐项receipts、新死亡最终屏障、Task10D/11/12/13/14仍未完成，F8仍Legacy**。最终1444 EditMode/SceneDirty=False、1445编译0/0、1446当前Console无error；未保存/清dirty。Task10C paused/read-only，全部租约释放，7脚本hash及下一大节点见任务记录。

- 2026-09-06 23:07：Task10C普通status roots结算规则组完成：实例消费随HP/Posture一次State journal提交；Player→Enemy、真实owner HomeSlot/timing/priority/instance稳定排序，stack合并、跳过项不扣duration、普通治疗不复活、显式source死亡后持续配置冻结；ActionIdentity缓存保证重复提交不增加revision。14项新检查及lane反转mutation、状态13项、Root11/11、Foundation121/121、补充真实帧34/34通过，manifest38/59与registered production4/25不变。**Immediate/OnDefeat因果children、Stage9可见cursor/receipts/死亡最终屏障和Root接线仍未完成**；F8仍Legacy，不宣称完整阶段9或总任务完成。1357最终EditMode/SceneDirty=False（1300本轮启动实读即False，未保存/清dirty），1358编译0/0、1359当前Console无error。Task10C paused/read-only，全部租约释放；10文件hash与承接接口见`tasks/2026-09-06-basic-attack-task10c-settlement.md`。

- 2026-09-06 22:23：Task10B状态实例/触发边界组完成。不同来源独立实例，同源叠层保持一项；Root应用冻结TurnSerial边界，普通新状态等待下一匹配边界；duration按实例单次消费；旧快照/删空高水位保留，source/身份/边界纳入State校验和ledger指纹。13项状态检查（含6格身份防伪及可逆mutation）、manifest38/59、Foundation121/121、Root11/11、补充真实帧34/34通过；registered production仍4/25。阶段9ledger/cursor尚未接，F8仍Legacy；Sprite正常不依赖Spine。最终EditMode/SceneDirty=True继承未保存，编译0/0、当前Console无error；全部Task10B租约释放，下一独立组Task10C。详情及14文件hash见`tasks/2026-09-06-basic-attack-task10b-status-identity.md`。

- 2026-09-06 21:39：Task10A显式headless测试组合前置组完成，31 foundation脚本57处及NineStage9处构造迁移；test-only immediate port不参与真实节拍，生产constructor无fallback。新增固定contract与编译依赖防回退审计通过，manifest37/59，Foundation121/121、Root11/11、真实帧34/34、注册production4/25通过。阶段9状态/ledger/可见队列未实施，Root仍ReadyForSettlement，F8仍Legacy；Sprite正常继续，无Spine资源阻塞。最终EditMode/SceneDirty=True（继承基线未保存），编译0/0、当前Console无error。全部Task10A路径/meta/文档租约释放，下一组10B；详细hash见独立Task10A记录。

- 2026-09-06 21:04：Sprite新事务根已串联Stage3 prepare/投影锁→宣告/构图/前摇→一次规则commit→Stage7队列→Return/HomeRendered→typed departure→规则/表现formation，停在ReadyForSettlement并保留actor/Root锁。新增11项、补充34格真实帧、Foundation121/121通过；manifest36/59、注册production4/25不变。阶段9/完整反馈/正式反应策略与F8接线仍待做；Spine模型不属当前阻塞。1194最终EditMode/SceneDirty=True（本轮临时fixture清理Undo标脏，未保存正式场景；不得自动保存/宣称dirty=false），1195编译0/0，1196当前Console无error。全部本轮租约已释放，详情/hash见任务记录最新节点。

- 2026-09-06 20:19：Sprite/环境恢复组完成补充验证。全场survivor姿态/颜色/alpha、未位移旁观者和CanvasGroup/背景同批恢复；败退保留X/dim到HomeRendered。修复不同尺寸/pivot静态攻击图在回位中脚底下沉/放大的渲染级问题，私有Sprite mesh归一并保留原UV，不改原资产。新8项、补充真实帧30/30、Foundation121/121通过；manifest36/59、注册production4/25仍保持。1123 EditMode/SceneDirty=False、1124编译0/0、1125当前Console无error。Spine恢复/完整反馈、Root串联、阶段9和正式F8接线仍待完成，不宣称Task8/9整体完成。全部本轮租约释放，详情/hash见任务记录2026-09-06 20:19。

- 2026-09-06 18:57：第三方Counter/Pursuit来源可在当前ledger步骤加入冲突，四人+camera同批回位；反应先>=0.12秒/渲染帧紧凑名准备，再独立>=0.45秒结果，增加无缩放横向Dodge。新增5项检查通过，补充真实帧矩阵26/26通过（含2格第三方回位）；Foundation121/121，manifest仍36/59、注册production4/25，无新失败。最终EditMode/SceneDirty=False、编译0/0、当前Console无error。正式Root/F8未切换，完整renderer/pose/UI/environment恢复、VFX/SFX/flash/hit-stop/反应姿态、Root串联和阶段9仍待完成。本组交接paused/read-only、全部本轮租约释放；详情和hash见任务记录18:57。

- 2026-09-06 18:40：Task9真实adapter增加冻结全场布局与独立formation表现屏障，typed departure完成后全体survivor同批>=0.30秒平滑重排、跨渲染门并更新当前Home checkpoint；失能anchor已禁止挂active actor子树。Formation4、DefeatTransfer9及补充24格真实帧主链（含3+3）通过。manifest仍36/59、注册production4/25，Foundation121/121，编译0 error/warning；退出后EditMode、SceneDirty=False、当前Console无error。Task8/9整组未完成，Root仍Prelude ReadyForImpact、F8仍Legacy；第三方反应位移、完整反馈/恢复、Root串联和阶段9/后续资产接线仍待做。阶段性交接为paused/read-only，全部租约释放，详见任务记录最新节点。早先“继续持有unity-writer”记录仅为当时历史。

- 2026-09-06 18:05：Task9已接真实adapter的HomeRendered之后ally独立失能模型/X移交、enemy精确View permit释放，以及独立规则退场名单/原子candidate removal+occupied-row compaction。7项规则检查+8项交接检查、补充12格真实帧完整命中/回位/退场/规则重排链通过；完整manifest36/59和Foundation121/121仍通过。整体world formation动画屏障、Root完整接线、正式失能/X资产与F8迁移尚未做，Task8/9不标完成。大节点继续，详见任务记录。

- 2026-09-06 17:28：Task9新增双方+camera同批平滑回位、HP0保留X/dim/原演员、Home后至少一个真实EndOfFrame的死亡可见门。12格真实帧命中/回位镜像（含致死）通过；注册manifest仍36/59、production4/25，Foundation121/121最近通过。尚无失能transfer/enemy departure/formation完整链，Root仍停Prelude，F8仍ProductionLegacy。详情见任务记录；同一Task7→8→9大节点继续，不是交付。

- 2026-09-06 16:52：Task7投影保护已整组通过；Task8新增真实ledger驱动的主命中/反应串行表现，≥0.45秒/项，同批HP/world数字/横向recoil、敌我命中/防御/闪避镜像、非命中治疗cursor、关闭立即清理自有label均通过。808整组contracts+运动7项+Foundation121/121通过，manifest36/59，production仍4/25；Unity EditMode、场景不脏。此为在途检查点，不是Task8/9大节点完成：Root尚未越过Prelude接rule commit，完整VFX/SFX/flash/hit-stop/逐write parity和Task9死亡/回位屏障仍未完成；F8仍ProductionLegacy。主代理继续唯一unity-writer。

- 当前已按用户要求连续完成 Task6A～6D 大节点：宣告≥0.75秒、构图协议/渲染门、前摇≥0.30秒/真实播放身份、限定事务的超时恢复，以及 Director 叶节点隔离。17项窄检查与Foundation121/121通过；manifest完成32/59，真实production场景通过3/25（其余22待实施）。下一大节点从Task7表现投影租约开始，衔接Task8命中/运动和Task9死亡/回位；不再每个小切片交接。F8仍为Legacy，九阶段最终视觉尚未验收。
- 当前场景退出风险：2026-09-05 19:43 production矩阵退出后出现“Some objects were not cleaned up … [DOTween]”；随后不运行任何Task6场景、临时Task6对象为0的空PlayMode周期也复现（日志计数4→5）。具体退出组件尚未定位，未做全局Tween清理或场景改动，不得宣称Console全干净或全量smoke已通过。

以下是当前工作树中已经存在的主要方向，不代表每个方向都已完成或通过验收：

- DRPG 主体：`Assets/Script/GameFlowManager/DRPGSystem/`
- ANKALUTE 顶层/Strategic Hub：`Assets/Script/GameFlowManager/AnkaluteGameLoop/`（纯核心、运行时协调器和探索阶段门禁已验证并接线；`DRPGGameLoopRuntimeRoot` 是唯一 Unity adapter / legacy facade）。
- 已出现的子域：配置、战斗核心、回合调度、表现层、交互战斗、编队、状态/反应、游戏循环、结算、网格背包、装备、成长和编辑器测试探针。
- DRPG 资源：`Assets/GameResources/DRPG/`
- 总控菜单：`Assets/Script/GameFlowManager/MenuSystem/`
- 开发调试工具：`Assets/Script/Development/`
- 旧角色 PackageSystem 仍存在；不要仅凭目录名认定旧系统已经安全废弃。

任何“已经完成”的判断都必须回到代码、Unity 引用和测试结果验证，不能只根据文件存在作结论。

## 已确定的近期规则

- 2026-09-08用户最新澄清：此前满意录像只代表“终于有了正确样子”，不是最终效果验收，更不是冻结演出。不得因此跳过已批准的反馈/演出/波次/正式入口工作；继续完整批准范围，不在小节点反复请求继续。

- 2026-09-06用户明确当前以Sprite替代Spine；没有Spine模型可注入是正常资源状态。平A事务闭环按Sprite路径推进，Spine正式资源/骨骼恢复延后，不作为当前开发阻塞。九阶段顺序和死亡/回位/最终推进验收不变。

- `AnkaluteGameLoopState` 已接线为 ANKALUTE 唯一应用级阶段权威；现有 `DRPGGameLoopRuntimeRoot` 负责 Unity 适配和 legacy facade，`GameFlowArbitrator` 仅继续仲裁 Hunt/DRPG Battle，不能据此直接删除旧兼容类型。
- 单场 DRPG 内部采用唯一分层流程权威：`DRPGBattleFlowOrchestrator` 只负责战斗生命周期、行动事务阶段与合法转换；`DRPGBattleSession` 是领域门面/组合根，不再拥有可直接写入的 `Phase`。回合调度、行动解析、效果、反应、进度等仍是深模块，不应被膨胀为伪状态或取得流程推进权。
- 一个行动严格区分“规则解析/命中提交”“ActionSettled 结算表现”“最终发布与轮转”。领域结果可以先提交，但 `ActionCompleted`、`BattleEnded` 和下一行动者只能在同一 action identity 的表现 `WhenFinished` 门完成后发布；表现 Coordinator 只持有门控句柄，不得自行决定下一回合。陈旧 binding、generation 或 action 回调必须被拒绝。
- 当前可玩顶层路径是 Title -> StrategicHub/HubHome -> DungeonSelection -> ExpeditionExploration。Title/Hub 不生成或推进 Hunt；typed 副本确认成功后才显式激活 Hunt 并开放探索输入。
- Victory -> BattleSettlement -> 原 Expedition；Escaped/Aborted 直接回原 Expedition；Defeat、Completed、Retreated 回 StrategicHub/HubHome 并清副本上下文。Hunt 激活返回失败或抛异常时也会补偿回 HubHome，探索门禁保持关闭。
- Title/Hub 的应用阶段门禁统一控制 Hunt pause、角色 gameplay input、拾取与 CharacterControlAuthority；Battle/BattleSettlement 继续使用既有 battle suspension。Game Menu、团队背包和 legacy Package 只允许在 ExpeditionExploration 打开。
- 顶层目标循环是“标题/读档 → Strategic Hub（SLG）→ 副本探索 ↔ 普通战斗”；普通战斗结算后回当前副本探索，副本完成、撤退或队伍全灭后回 Strategic Hub，并默认落到 `HubHome`。
- 城市地图、建筑、设施、商店和副本选择属于 Strategic Hub 内部导航，不增加顶层游戏阶段。Gal 是覆盖宿主阶段的剧情演出，结束后返回原宿主上下文。
- 主角永久驻场，占五人队伍中的 1 号位；取消可选支援位抽象。
- 运行时队伍顺序以 `DRPGPartyCompositionState` 为权威：主角固定 slot 0；同律对象固定显示/菜单 slot 1；战斗只消费冻结快照。同律对象导致的 HUD 重排不改变阵型或调度顺序。
- 旧手动瞄准部位破坏点/破坏后暴露空间弱点的方案已经取消。
- 核心方向是攻击造成架势/防御破坏，进入 `PostureBreak` 弱点暴露期；它与低血量立绘状态 `DamageBroken` 是两个独立概念。
- `PostureBreak` 已形成明确生命周期：中断可中断预备、跳过一次行动、暴露期间提高受击/异常收益，恢复后返还部分架势并进入短期稳定保护；敌我具体参数仍可二轮调优。
- 特殊攻击不与 Break 强制绑定；只有具体职业或被动明确监听 Break 时才建立关联。
- 充能型特殊攻击从每场战斗 0 开始，由统一不可变战斗事件增加进度，受武器容量限制，并在每次被接受的 FreeStrike 输入上原子消费充能/姿势/耐久；失败预检不得产生部分 mutation。
- 主角拥有独立于武器职业的同律身份层。当前仅亲友可成为对象；对象选择会归一到队伍 slot 1；每个亲友分支可自动贡献已解锁被动，并装备一个不占职业四技能槽的主动同律技能。
- 同律主动技能进度属于 Expedition：同一副本跨战斗保留；重新选择相同对象不清空，切换对象清除旧对象进度；副本结束、失败或返回标题时清空。对象失能只禁用技能，不自动换人。
- 完全弃用的 Unity 内容采用 `Assets/废弃内容/` 归档流程，不直接删除。
- 普通敌方 `Standard` 攻击采用“先规划技能、后在预警 0 承诺目标”的 2/1/0 意图生命周期；逻辑时间只在离散调度/行动结算中推进，不在玩家思考期间连续流逝。
- 预警 0 必须先展示在一次玩家可操作回合，并由全队任意一名可行动己方角色完成一次正常行动后才可执行；同批多个就绪敌人随后可以连续行动。
- 行动条是混合预测：平时展示己方行动预测，仅把已经预警 0/承诺执行的敌方头像按顺序插入；敌方卡不附加技能名、目标或倒计时信息。
- 普通敌方目标默认隐藏；只有技能显式选择 `RevealAtCommit` 才使用独立战场反馈公开锁定。目标承诺后失效时默认落空/取消，不静默换目标。
- 每名存活、已部署的普通敌人必须始终持有显式行动计划。无合法攻击时优先选择已有合法标准辅助技能，其次尝试合法换位；完全无事可做时执行无倒计时、无表现帧的静默 `Wait`，完成后重新进入调度，禁止回退到无计划 legacy 路径。
- `TelegraphedSpecial`、`FreeStrike`、现有特殊攻击释放时序和手动特殊防御继续走 legacy 旁路。本轮未实现防御技能熟练度或 AI 自动完美防御概率，但保留在现有视觉流程前接入策略层的边界。
- 用户已正式允许 DRPG 战斗表现使用独立透视相机与 2.5D 世界空间代理；探索、规则与物理仍保持纯 2D，探索相机不得被永久改写，战斗表现不引入 3D 物理。
- 探索 `ProCamera_World` 继续保持 Orthographic。默认 DRPG 战斗由顶层场景空间 prefab `PF_DRPGBattleWorld` 承载：独立 Perspective Camera 直接输出屏幕，舞台、敌我演员与世界 VFX 使用普通 Transform 下的 SpriteRenderer/Spine，并统一处于 `DRPGBattleWorld` Layer。`PF_DRPGBattlePresentation` 只承载 HUD、覆盖演出和透明屏幕点击代理；RenderTexture/RawImage 仅保留为可选配方输出，不再作为常规战斗默认承载。默认普通攻击的构图作用域从 Windup 保持至 Impact，并仅在 Settled/取消/故障后统一恢复。
- 技能冲突演出不采用强制的低/中/高成本枚举。`DRPGSkillSO.PresentationKey` 关联独立语义配方，每个 UI、舞台、镜头、攻击者、目标、背景、Spine、反馈和退出模块使用“继承/覆盖/显式关闭”；未配置模块继承默认冲突模板。
- Spine Event 仅是风、命中、HitStop 和结束的时间信号，不得直接修改 HP 或战斗状态。多段命中必须由规则层预解析并通过 exactly-once ledger 分段提交；缺失事件必须由 watchdog 确定性补交或退出，不能死锁。
- 冲突作用域恢复必须同时检查表现总结果与 Action Flow Gate：非阻塞尾部提示在下一阶段开始时被替换可以使总结果为 `Cancelled`，但 Gate 已完成代表核心冲突演出已合法交棒，此时不得把攻击姿态提前回滚。敌方世界演员的受击帧由语义世界表现层唯一拥有，旧 UGUI 反馈层只负责伤害数字。
- 敌方表现目标是去卡牌化：全身 Sprite/Spine 使用世界空间代理和前中后纵深排布，UGUI 仅承载轻量浮动状态与透明屏幕命中代理。默认视觉测试为前 3 + 后 3，15 敌人仅作怪海压力测试。
- 战斗入场首版采用固定约 1.7 秒流程：HUD/输入先隐藏与锁定，镜头注视生成区，怪物分批生成，镜头回默认机位，角色 UI 与其他 HUD 分层进入后开放输入。

## 条件门槛功能的调试可达性准则

- 凡是依赖资源积累、解锁、关系进度、战斗状态、随机结果、冷却或其他前置条件才能生效的功能，实施计划必须同时规划可插拔调试项。
- 调试项至少覆盖：直接构造有效条件、构造失败/边界条件、清除或复位状态、显示当前关键状态。
- 调试入口应通过明确的领域调试接口或测试 seam 改变真实运行状态，不得只改 UI 文案或表现层伪造成功。
- 每个调试项应注册到 `Assets/Script/Development/` 的开发调试系统，并提供简短按钮说明与详细悬停说明；不得污染正式发布行为或普通存档。
- 验收必须同时覆盖“调试直达路径”和“正常游玩达成路径”。仅有底层 probe 能构造状态，不等于功能已具备实际可测试性。
- 当前同律系统的后续调试补强至少包括：指定同律对象、填满/清空已装备同律技能条件、令对象失能/恢复，并最终通过真实战斗 UI 释放技能。

## 仍在设计、不可当成定论

- 敌我双方是否采用完全对称的 Break 后果。
- 同律身份层的正式关系条件来源、具体亲友分支内容与剧情法则演出映射。
- 特殊攻击最终采用爆发条、连携奖励、职业被动条件还是组合条件。
- 单场战斗乐趣循环的最终数值节奏和具体技能量级。

## 当前工作树风险

2026-08-30 检查时，工作树包含大量已修改和未跟踪的 DRPG 源码、测试、资源与场景文件。这些是既有开发成果，不属于同步层建立任务。

后续任务必须：

- 修改前检查目标文件 diff；
- 不清理、不回滚、不覆盖不属于自己的改动；
- 将 Unity 编译/运行结果标注为“在共享脏工作树上验证”；
- 使用 [TASK_REGISTRY.md](TASK_REGISTRY.md) 防止并行写入重叠。

## 当前活动任务

2026-09-13 当前无活动写入任务；`basic-attack-task11-finalization` 已完成并释放全部租约。最新依据本页“最近里程碑”首条和TASK_REGISTRY；以下旧在途记录仅作历史，不再代表当前完成率。

2026-09-06 00:27补充验证：标准平ATask7投影层17个窄行为及3个manifest contracts通过；完整manifest35/59、production矩阵Passed4/Pending21/Failed0，Foundation121/121通过。Root在宣告前持有全战场表现租约，旧刷新不能改投影，世界敌方HP有独立cursor，关闭一次canonical刷新；演员/Surface丢失Root fault。Task8/9命中运动/死亡回位尚未完成，F8仍Legacy；Spine内部恢复快照仍待确认，不能据此宣称生产九阶段已完成。主代理继续大节点。

以 [TASK_REGISTRY.md](TASK_REGISTRY.md) 为准。`basic-attack-transaction-flow-completion` 已完成 Task0～5C 与 Task6A～6D（12/24叶节点），当前 active/code-writer 连续推进 Task7→8→9 大节点。Task5C 已验证唯一原子规则/反应 ledger；Task6 已验证宣告、构图命令、Spine/静态帧前摇、watchdog与旧叶节点旁路隔离。正式已完成 manifest32/59、production3/25；Task7新增投影租约正在实施验证，尚不提高整组验收计数。Task8～14的命中运动、死亡回位、阶段9逐项结算、波次、最终推进与正式入口仍未完成，F8保持ProductionLegacy并在Task13统一切换。当前场景空PlayMode退出也能复现DOTween残留，作为独立Task14风险保留；不能宣称全量smoke或Console无错误。最新证据与活动精确路径见任务记录及登记表。

## 最近里程碑

- 2026-09-13：标准平A批准范围完成。正式PlayMode生产矩阵 `25/25`（pending/failed/watchdog/incomplete/infrastructure均0），九阶段合同账本18组 `59/59`，Foundation `121/121`，最终编译0错误0警告。补齐敌我单结果节拍、纯横向受击、双方同批回位、致死X/压暗/HomeRendered后退场、Stage9一笔一完成再换人、波次宣告/就绪/撤回/身份及各层watchdog；修复重复canonical bind造成的回合刷新与事务运动冲突，并预热首命中反馈资源。MCP 0.6.7的serverInfo仍显示旧Project_ANOIR，但编辑器进程与`Application.dataPath`双重确认实际为ANKALUTE；不影响结果。保持Sprite正式路径，Spine资源缺失不构成当前阻塞。未保存`测试场3_电车45.unity`（磁盘时间保持2026-09-12 12:08:32），未触碰用户新Spine/美术，未commit/push/clean。

- 2026-09-05 18:26：Task5C整体规则边界完成。连续新增PostureBreak7、Expedition同律原子journal4、配置进度来源4、真实Session规则核10、生产武器跨行动2，共27项新检查通过；全组/旧四类probe/Foundation121/121通过，编译无当前error/warning、Console error=0。正确项目与测试场3保持EditMode/dirty=false。38份代码/测试文件的最终hash与完整RED→GREEN见任务记录；脏树聚合501（124M/20D/357折叠未跟踪项）不变，未commit/push/清理/归档或改序列化资产。下一大节点Task6A～6D，之后Task7～9与10～14。

- 2026-09-05 17:15：Task5C治疗/护盾/架势恢复共同ledger子检查点完成。7项真实Unsupported行为RED→GREEN，另补7类篡改、Shield overflow、敌方来源同路径，总10/10通过；有序typed恢复事实、精确指纹与RuleOnly分流，伤害在effect实际位置核对HP/Shield/Posture，普通恢复不复活归零目标。完整既有组/Foundation121/121通过，manifest仍27 completed/32 pending。编译无当前error/warning、Console error=0，正确项目/场景EditMode/dirty=false，未切F8或改序列化资产。PostureBreak完整生命周期与独立PostureDamage仍待接入，不宣称Task5C整体或九阶段实机通过。

- 2026-09-05 16:37：Task5C即时行动条delta子检查点完成。6项初始行为RED→GREEN，另实证“先timeline后lethal被拒”和“近过去时间在写HP/MP后抛错”；补typed TimelineChange、有序投影/对账/指纹与预构建队列整体apply。新增11/11（含7类伪造、exact int优先级、RuleOnly、晚期资源失败）；既有反应/资源/状态组与Foundation121/121通过，manifest仍27 completed/32 pending。编译无当前error/warning、Console error=0，正确场景EditMode/dirty=false。未切F8、未改序列化资产，整体九阶段仍未完成。
- 2026-09-05 15:18：Task5C 纯状态/零 hit 反应检查点通过。先复现5项“必须有Damage”的RED与无效状态仍展开后代的RED，再实现显式非命中上下文、独立目标指纹、零hit状态事实对账和RuleOnly分流。新增9/9（含7类篡改、死亡不重触发/不提前移除、深度与总数预算、失败全回滚）；既有完整反应/资源/状态组与Foundation121/121通过，manifest保持27 completed/32 pending。编译无当前error/warning、Console error=0，正确项目/场景且EditMode/dirty=false。未切F8或改序列化资产；下一步仍为Task5C剩余范围。
- 2026-09-05 14:55：Task5C 状态 workspace 与单命中反应内 ApplyStatus 共同 journal 子检查点通过。补齐 staged apply/remove 对后续伤害的影响、独立 Fork、状态预计算后 primitive 提交、持续时间 overflow/巨大 stack/定义 drift 拒绝及冻结图标元数据指纹；完整账本逐项对账状态前后快照、明确 lethal 后跳过状态且不移除部署、敌我同路径/闪避无附带状态。新 workspace7/7、status-ledger9/9；既有反应/武器/成本/容量组与Foundation121/121通过，manifest仍27 completed/32 pending。编译无当前错误/警告、Console error=0；未切F8、未改序列化资产。纯状态/零hit反应及timeline等仍未完成。

- 2026-09-05 14:13：Task5C 武器断裂后的 Battle 进度容量/数值共同提交通过。进度定义保留基础容量与装备修正来源，不重读 mutable SO；完整行动成本/进度变动之后生成只读容量前后/溢出事实，和伤害、耐久、碎片一次 apply；禁止缺项、重复、伪造来源/数值或同显示容量但不同 base 的替换。补齐容量请求全部语义字段的 fingerprint，重放不遗漏请求差异。新容量11/11、原武器10/10、成本9/9、ledger8/8、资源8/8、Foundation121/121；manifest仍27 completed/32 pending，编译无当前错误/警告、Console error=0，未改序列化资产或切F8。Task5C整体、生产事件消费者和九阶段实机仍未完成。

- 2026-09-05 13:52：Task5C 武器断裂装备结果共同 journal 子检查点通过。普通武器最后耐久→卸装/释放占用/同名碎片累加；专属武器零耐久保留；构筑回退平A、移除武器技能但保留配置槽与饰品，HP/Posture 上限收缩均在完整 State journal 一次提交。新增不回退 mutation epoch 防 equipment/inventory ABA 恢复；补齐 use/event/completion 精确覆盖、完整行动资源对账和私有 capability 门禁。新武器10/10、成本9/9、ledger8/8、资源8/8、Foundation121/121，manifest仍27 completed/32 pending，编译无当前错误/警告、Console error=0；共享脏树与 Unity 场景未清理/改写、未切 F8。完整 Task5C、生产接线和九阶段实机验收仍待完成。

- 2026-09-05 12:43：Task 5C 的 MP/Posture/Action Progress reservation 生命周期已共同接入完整 ledger。工作区 Fork 保留同一预留证明，主攻击/反击前先暂存成本，提交后只读映射真实 child transaction 状态/receipt；取消和失败不消费 live state。测试验证架势 `100 → 成本后90 → 反击后83`、MP `30→25`、Progress `10→7`、武器 `100→99`、State revision 只增1，重放不再消费。补齐完整 ledger gate、captured actor/stable progress owner 与提交前 owner drift 拒绝。新成本9/9、完整ledger8/8、资源8/8、Foundation121/121；manifest仍27 completed/32 pending，编译无当前错误/警告、Console error=0。未修改 Scene/Prefab/SO、未切 F8，完整 Task 5C 与实机演出仍未完成。

- 2026-09-05 12:06：Task 5C root ledger 一次提交与不可变事实子检查点通过。主命中/反应/typed resources 共享 cache 与 journal；相同请求重放不重复扣费，反应/资源/武器事件内容变化拒绝。补齐本 State lease 校验、逐步 HP/架势/护盾对账、scoped immutable facts 与 Impact Trace。新 ledger 8/8、上轮资源 8/8、Foundation 121/121，编译无当前错误/警告、Console error=0；manifest 仍 27 completed / 32 pending。未迁完整生产资源生命周期/事件消费者、未切 F8、未验收九阶段视觉，不能将此子检查点当作 Task 5C 全部完成。

- 2026-09-05 11:32：Task 5C 的真实 Battle Progress/Weapon Use journal 子检查点通过。HP/MP、主攻击/即时反应、进度和武器消耗可在同一 State 私有 capability 下共同提交；捕获值及 revision 漂移（含 ABA）、错误 owner、重复/冲突和 prepare 晚期故障均有零部分写入检查。新资源 8/8、既有反应 2/2、主命中 4/4、Foundation 121/121；固定 manifest 仍为 27 completed / 32 pending。编译无当前错误/警告，Console error=0；未切换生产入口、未改序列化资产。完整 ledger、生产资源生命周期、普通断裂/碎片等仍未完成，不能据此宣称九阶段或实机通过。

- 2026-09-05 10:57：Task 5C 的独立反应闭包已通过：稳定类别/优先级/Home Slot 顺序、深度8/步骤32边界、整笔被动与同父去重、完整 root/parent identity、死亡后取消普通反应且只允许真实 OnDefeat、故障整链丢弃；修正闪避读取旧 HP/Posture 的问题。反应 2/2（12 个细分行为），主命中 4/4、Foundation 121/121；固定 59 项 manifest 当前 27 completed / 32 pending，编译无错误、Console error=0。尚未完成 Task 5C 的资源/进度/武器与完整 ledger 提交，更未进入新演出实机验收；未切换生产入口或修改序列化资产。

- 2026-09-05：标准平A已完成 Task 3～5B：revisioned workspace/capability、唯一 Router/composition、冻结快照与不可变主命中 ledger。重启后恢复 Unity/MCP，新增真实行为 RED→GREEN 修复零成本夹带 mutation、冻结威力不匹配、journal 重放冲突、重复 Trace ID、第二 primary ordinal 和双向资源跨 Legacy 边界。主命中 4/4、路由 5/5、workspace 7/7、identity 5/5、Foundation 121/121；编译诊断无错误、Console error=0（仍有旧 Spine/未使用字段警告）。未切换生产入口、未修改序列化资产；完整九阶段、反应、阶段9、波次与实机验收仍未完成。

- 2026-09-04：标准平A实施 Task 2 完成。战斗 Session 现拥有 process-monotonic BattleGeneration、完整 ActorTurn/Selection/Action identity、固定 generation 的唯一事务 arbiter 与 typed root receipt；Action Trace 从 Stage 1 开始并精确关联 TurnBegin -> SelectionOpened -> ActionLocked，每个 receipt 返回 action-scoped snapshot。Task 2 六项契约、Unity 编译、Console 与 Foundation 121/121 全绿，最终只读复审 GO；下一步进入 scheduler 纯查询与 revisioned mutation workspace。

- 2026-09-03：获批标准平A规格的严格 TDD 实施计划已完成：15 个顶层章节拆为 24 个独立 GREEN checkpoint，显式锁定18组 deterministic contract 与25项 production-adapter scenario；三路最终复核 GO/无 P0/P1。计划从脏树 baseline 开始，依次建立 identity/arbiter、workspace、双 ledger、九阶段表现、Return/Settlement/TurnEntry、Wave、生产入口与三层验收；尚未修改运行代码或 Unity 资产。
- 2026-09-02：用户整体批准 `docs/superpowers/specs/2026-09-02-standard-basic-attack-transaction-flow-design.md`。标准平A采用 A+ 深事务模块、唯一 BasicAttack seam、双不可变 ledger、全战场 Projection Lease、Return/Settlement 屏障、敌我同构表现、延迟失能呈现与独立 Wave Transition。既有 `121/121` 只证明当前门控与局部配方，不代表九阶段实机验收。
- 2026-09-02：完成敌方普通攻击冲突演出时序对齐。根因是 Action Flow Gate 已完成后，尾部非阻塞提示被下一阶段替换使总结果成为 `Cancelled`，旧恢复策略误把它当成核心演出取消；现已改为 Gate-aware 恢复，并移除敌方世界受击后的旧 UGUI 二次闪帧。另确认 Unity 曾持续运行陈旧 `Assembly-CSharp.dll`，强制重编译后正式游戏流中敌方攻击姿态跨 `PreparingAction -> ExecutingAction` 多帧保持。DRPG probes `121/121`、最终 Console error=0，实机截图为 `Library/FunplayMcp/Screenshots/enemy-basic-executing-verified.png`。
- 2026-09-02：单场 DRPG 底层流程收敛为 `GameFlowArbitrator -> DRPGBattleFlowOrchestrator -> action transaction` 的分层权威结构。`DRPGBattleSession` 只通过仲裁器转换生命周期；`TryCompleteAction` 已拆成领域提交和显式最终推进，ActionSettled 表现完成前不会轮转、发布胜负或清空事务身份。Unity 编译 0、DRPG probes `121/121`，Play Mode 聚焦生命周期/表现事务/行动事务探针全部 PASS。全局 smoke 仍受既有 World Host 延迟销毁探针的 Play Mode 立即计数问题阻挡，该问题不属于本次流程重构。
- 2026-09-01：DD1 式战斗表现首轮闭环完成。默认战斗采用浅透视固定侧视舞台，三名己方与六名敌方均为场景空间 Sprite/Spine 演员；旧卡牌式敌方 UI、重复背景和右侧当前角色大立绘在该模式下关闭。常驻 UI 收敛为顶部混合行动条、左下队伍状态和右下 3x2 指令区；普通攻击通过现有 Windup -> Impact -> Settled 事务驱动 DOTween 前冲、静态攻击帧、一次性命中结算和安全回位。原创临时攻击帧已接入第二名队员及两类敌人。Unity 编译 0 error/0 warning、DRPG probes `119/119`、连续多回合 Play Mode 与最终 Console error=0 通过；验收截图为 `Library/FunplayMcp/Screenshots/dd1-runtime-final-multiturn.png`。
- 2026-09-01：DD1 阵型与基础攻击对称性二次验收完成。前/中/后仍是战斗逻辑数据，但每一侧的场景表现统一投影为一个不可重复的横向视觉槽序列，避免各逻辑排独立居中造成重合；Sprite 意图切换统一遵守脚底基线契约，后续 Spine 驱动也应实现同一视觉锚点协议。普通攻击只走对称的世界冲突路径，玩家专属旧 `AttackCutIn` 不再混入基础攻击，但该执行器仍保留给明确配置的高成本技能。重启后正式游戏流与 3 己方/6 敌方实机通过，Unity 编译 0、DRPG probes `120/120`。
- 2026-09-01：完成战斗世界/UI 架构纠正。新增顶层 `PF_DRPGBattleWorld`、场景空间默认舞台、`DRPGBattleWorld` Layer 与运行时世界宿主；Perspective Camera 默认直接输出，UGUI 仅保留 HUD/覆盖/点击代理，RenderTexture 为可选路径。全量探针 `118/118`、Victory+Defeat Play Mode smoke、截图与最终 Console error=0 通过。
- 2026-08-30：建立跨 Codex 对话的共享状态、任务登记、独占路径和交接协议。
- 2026-08-30：确认 ANKALUTE 顶层应用流、Strategic Hub/Expedition 术语及旧 `GameFlowArbitrator` 的迁移定位；当前仅完成设计定论，尚未修改运行代码。
- 2026-08-30：完成并获准以 `docs/superpowers/specs/2026-08-30-strategic-hub-game-loop-design.md` 作为实施基线；首轮实现只新增 `Assets/Script/GameFlowManager/AnkaluteGameLoop/` 纯 C# 核心和专属 probes，避开既有 DRPG GameLoop、公共组合根与 Unity 资产。
- 2026-08-30：完成 `docs/superpowers/plans/2026-08-30-strategic-hub-game-loop-foundation.md` 严格 TDD 实施计划并通过最终契约审查；执行任务 ID 为 `strategic-hub-flow-foundation`。计划尚未执行，运行代码与 Unity 资产仍未修改。
- 2026-08-30：普通敌方 2/1/0 意图生命周期、最低一次玩家响应、混合行动条和选择性目标公开完成；基础探针 `90/90`、Play Mode 冒烟、最终编译与 Console error 检查全部通过。验证报告：`Logs/DRPGPlayModeSmokeTest.txt`。
- 2026-08-30：修复后排/增援敌人无计划导致的战斗停滞；补齐普通敌方 `Attack / SupportSkill / Reposition / Wait` 显式计划闭环，其中纯待机不显示倒计时且不触发表现帧。基础探针 `90/90`、Play Mode 冒烟、编译和 Console error 检查通过。
- 2026-08-30：`strategic-hub-flow-foundation` 启动执行；审计确认最近 DRPG 战斗开发不改变 Strategic Hub/GameLoop 契约且目标路径、公开类型、菜单、默认程序集均无冲突。取得唯一 `unity-writer` 后，在零新增脚本状态完成共享脏工作树 preflight 编译，错误/警告为 0。
- 2026-08-30：Strategic Hub 纯核心首个里程碑通过严格 TDD：已验证 `HubHome -> CityMap -> Building`、精确 Back/history、copy-on-publish 快照、同路由/根 Back/干净 reset NoOp、深层 reset 与拒绝不变量；独立菜单输出 `PASS Strategic Hub route stack`，全项目编译错误/警告为 0。尚未接线运行时。
- 2026-08-30：Strategic Hub Activity 里程碑通过严格 TDD：Gal、Shop、DialogueEvent 作为宿主 Route 上的阻塞活动，使用单调正 token；导航/第二活动 Busy，matching Failed/Completed 完成，旧 token Stale，特权 reset 清活动但不复用 token。route/activity 两个独立 probes PASS，编译错误/警告为 0。
- 2026-08-30：Strategic Hub typed 副本确认与事件安全里程碑通过：`ExpeditionId`/`ExpeditionStartRequest` 是纯值契约；只有 DungeonSelection 可返回 effect-only Applied，且不改 Hub Snapshot/revision/event。Activity 会 Busy 阻断确认；Transitioned 重入为 Busy，订阅者异常不回滚已提交状态且 finally 恢复后续请求。三个独立 probes PASS，编译错误/警告为 0。
- 2026-08-30：`AnkaluteGameLoopState` 顶层 happy path 里程碑通过：Title 的 NewGame/LoadedGame/ReenterAfterTitle 分别映射 Hub entry reason；Hub→Expedition、普通 Battle Victory→Settlement→原 Expedition、Escaped→原 Expedition、Completed→HubHome 已验证。主 fixture 精确 8 次顶层 transition，四个独立 probes PASS，编译错误/警告为 0；尚未与现有运行时接线。
- 2026-08-30：顶层终止/错误纪律里程碑通过：Retreated/PartyDefeated 回 HubHome 并清副本，Aborted 回原副本；ReturnToTitle 仅 Title NoOp/StrategicHub Applied，副本相关阶段阻断；Hub reset Busy/其他拒绝保持 top Snapshot；top/Hub subscriber 异常与 reentry 均有确定恢复语义。当前独立菜单输出 8 条 PASS，编译错误/警告为 0。
- 2026-08-30：Strategic Hub/GameLoop 纯核心通过三路独立终审。契约与隔离无 P0/P1；测试审查提出的 Transition.Before、完整非法路由图和 Shop typed payload 三个覆盖缺口均经可逆 mutation 先红后绿补齐。模块仍未接线现有运行时、场景或 Prefab。
- 2026-08-30：`strategic-hub-flow-foundation` 完成并释放全部租约。共享脏工作树最终编译 0 error/0 warning，专属菜单 8/8 PASS，最终 Console error=0；生产核心无 Unity/DRPG/legacy flow 依赖，7 个脚本及 13 组资产/meta 完整，未修改 DRPG、公共组合根、Scene/Prefab/ScriptableObject。
- 2026-08-30：`strategic-hub-runtime-integration` 启动规划；目标是让标题页开始游戏进入可见且冻结探索的 Strategic Hub，再由明确入口启动现有探索。当前仅审计公共 seam，未修改运行代码或 Unity 资产。
- 2026-08-30：`strategic-hub-runtime-integration` 三路 seam 审计收束并完成实施计划。已锁定：现有 `DRPGGameLoopRuntimeRoot` 作为唯一 Unity Adapter；Title/Hub 不自动启动 Hunt；现有 `DRPGGameLoopCanvas.prefab` 只做增量 Hub 扩展；旧 Menu/Backpack/Package 输入按新顶层阶段 gate。21:11 取得唯一 `unity-writer`，尚未开始代码 RED/GREEN。
- 2026-08-30：`strategic-hub-runtime-integration` 完成。开始游戏现进入可见且冻结探索的 HubHome，可进入 DungeonSelection 并通过 `dungeon-a` typed request 启动当前探索；现有 Canvas 只做增量扩展，未新增第二 Canvas、未保存场景。
- 2026-08-30：顶层回流与门禁完成：Victory/Settlement 回同一副本，Defeat/Completed/Retreated 回 HubHome；Hunt 显式激活，失败/异常可补偿；直接 Battle 和空 EncounterId 在突变前拒绝；三条 overlay 输入按 ExpeditionExploration gate。
- 2026-08-30：最终共享脏工作树验证通过：Unity 编译 0 error/0 warning，ANKALUTE Game Loop probes 13/13、DRPG probes 90/90，确定性双战斗 Play Mode smoke 覆盖 Victory+Defeat、激活失败、空 EncounterId 和同一玩家二次出击，最近 90 秒 Console error=0；16:9 与 4:3 Hub QA 无裁切。
- 2026-08-31：完成 P0 Break 语义清障：低血量演出统一为 `DamageBroken`，FreeStrike 收敛为身体 Rect 命中与确定性溅射，旧部位耐久/防御/空间弱点运行时和探针按规则注释归档。恢复测试安装器误覆盖前的完整战斗 UI Prefab；共享脏工作树编译 0 error/0 warning、DRPG probes 89/89、确定性 Victory+Defeat PlayMode smoke 通过。
- 2026-08-31：P1～P5 连续实现完成。主角/五人队伍不变量、PostureBreak 生命周期、不可变战斗事件与确定性进度、充能 FreeStrike 原子消费、主角同律身份层/队伍 slot 1/Expedition 生命周期/独立菜单与战斗 UI 已接线。安装器统一 Progression、调试启动器与场景遇敌 PartySetup；`测试场3_电车45` 已通过 Unity API 保存并确认磁盘依赖。共享脏工作树编译 0 错误、DRPG probes 101/101、Title→Hub→Expedition→Victory+Defeat PlayMode smoke 和 P5 资产审计全部通过。
- 2026-08-31：透视战斗舞台与技能冲突演出 P0～P7 已实施完成。新增语义配方编译、Spine exactly-once 分段命中、隔离透视相机/RenderTexture 舞台、世界空间角色阵型、透明点击/命中代理、脚下紧凑状态牌、HUD 分组与冲突恢复、固定入场和开发直达；Unity 编译 0、DRPG 116/116、Play Mode 世界目标攻击与 Console 无 error 通过。正式 Spine/VFX/音频/舞台美术留待二轮内容制作。

## 下一位任务执行者的起点

1. 按 [SYNC_PROTOCOL.md](SYNC_PROTOCOL.md) 完成启动读取。
2. 明确自己的单一任务目标和不处理范围。
3. 在 [TASK_REGISTRY.md](TASK_REGISTRY.md) 登记后再写入。
4. 若目标涉及当前“单场战斗乐趣循环”讨论，先区分上述已确定规则与仍在设计内容。
5. 若继续 Strategic Hub，下一任务可接 CityMap/Building/Facility UI adapter、Gal/商店 activity adapter 或真实副本数据源；继续复用现有顶层权威和 typed request，不新增第二套顶层状态机。


## 2026-09-20 紧凑战斗演出已接入

正式 BattleStandardPresentationBinding 现读取 CompactBattleTiming.asset：声明 0.04、聚焦 0.14、出手最短 0.10、回位 0.22 秒；空结算取消固定等待但保留渲染交接。技能名持续到回位开始，现有特写 shader 加强背景/旁观压暗模糊。保留原规则与事务系统。实测主体演出 0.966 秒，六次行动后无特写层残留。契约 57/59 通过，入场运动与测试装配两项既有失败仍在；不是整套全绿。Unity 已退出 Play、恢复保存场景，writer 释放。详见 tasks/2026-09-20-compact-battle-presentation.md。







## 2026-10-05 PBR 地牢表面独立样品
SC_PBRDungeon_Surface_Validation 已完成：10×8m主室，12盒体/144三角形，2套CC0 PBR石材、12个DBuffer贴花。新Renderer索引9只含Decal/SSAO，既有0–8/default0保留。四阶段/4单项关闭真实Play对照、保存重开/引用/Shader/编译与原5场景SHA验证通过。当前Edit/Full/clean，writer全部释放。操作1–4、D/N/O/P；本样品为标准URP/Lit，未复用体素RealToon/旧雾。详细结论与性能边界见docs/xinghai-pbr-dungeon-surface-validation.md。

## 2026-10-05 PBR墓室卡通对照
SC_PBRDungeon_Cartoon_Validation为独立副本，Renderer10采用暗牢当前五档RealToon、接触AO、冷雾及Volume并保留DBuffer。B照搬/墙面可读性适配，默认适配。12网格/相机/UV未变；源PBR与DarkDungeon资源259文件SHA不变，实机/重开/20轮切换验证通过。最终Edit/clean，writer释放。详情docs/xinghai-cartoon-dungeon.md。







## 2026-10-07 T7-A.3 连续局部体积雾

T7A3独立RendererFeature/密度场/最多2个AtmosphereLight/8-16步积分/Depth-aware Upsample与可逆场景根已完成。Gate F4未通过：D-F圆形灯区和D-D纵深未显著解决，Medium移动控制器CPU中位2.74ms。因此Low=T7A2最多8 Volume正式路线，BestCurrent=Low，Medium=S12/Half/SmoothUnion/单灯Experimental。按用户计划冻结地牢底层Atmosphere，未做T7B/C。

36正式+36消融+78参数+40调试；Medium82秒、Low82秒、Dual30秒实机验证，恢复/原生录像/性能归档。60轮状态恢复+20轮实际RT释放，保存重开36/36基线精确、旧协议18/18精确、1772旧资源SHA不变。场景仅新增控制根与2个注册组件。最终Edit/clean/无预览、编译ready；writer与路径已释放。报告`docs/render-research/T7-A3-continuous-local-volumetric.md`，Gallery`artifacts/xinghai-t7a3-continuous-volumetric/index.html`。

## 2026-10-07 T7-C.2 HD2D景深与镜头后处理交付到H2

独立`SC_HD2DPost_Validation_T7C2`、`SC_HD2DPost_Dungeon_T7C2`、`SC_HD2DPost_Sunset_T7C2`已完成，9后处理Profile提供Clear/Light/Expressive，2个翠绿材质修色副本保留环境与Grade。22主图+10回归+6诊断、两段合计54.001秒/2967帧录像已归档。Light为默认探索推荐，Clear为稳定基线，Expressive可选；用户美术选择Pending，不自动提升全局Look或进入C3，旧DungeonMedium仍Experimental。

本机12任务GPU实测证明原生Gaussian/Bokeh正交深度失真，因此仅正交启用一个有限深度感知备选，透视仍原生，互斥；轴向焦点、重建参数保持、Volume组件销毁、相机实例恢复RED→GREEN。Clear与基底144万像素完全相同；焦点变化0.8192m、最大平滑误差0.0282m，自动传送/转向/投影复位误差0。真实地牢导航94.97m；相机升降只模拟焦深，实际玩家跨台阶未验证，每段只抽查10录像帧，未认证整段无闪烁或角色/技能/UI战斗阅读。

40性能状态/3600独立GPU样本、20完整恢复循环、关窗/保存/退出Play/重开通过；冻结旧资源1202/1202 SHA不变、三场1398受保护几何/相机块不变。最终clean Edit停户外C2、原URP与镜头，C2自有RT/Volume/Feature/TEMP0；全局native/editor RT微增长未归因，透明水/云独立深度限制保留，不宣称全引擎无泄漏或低端/build认证。Gallery数据/54相对文件/JS通过，浏览器布局未验证。

引擎此前在旧Capture.Tick内OpenScene native SIGSEGV，恢复后采用逐场采集，未证明引擎根因已修复。当前此项目MCP实测`http://127.0.0.1:28230/`，旧28229监听仍占用；使用`E:/WORLDCREATOR/XingHaiHuiLang/work/renderlab-t7c2/Invoke-C2Mcp.ps1`并先核对项目身份，不杀未知监听、不改全局客户端配置。所有本轮Unity/路径租约已释放。报告`docs/render-research/T7-C2-hd2d-camera-post.md`；Gallery`artifacts/xinghai-t7c2-hd2d-camera-post/index.html`；任务交接`tasks/2026-10-07-xinghai-renderlab-t7c2-hd2d-post.md`。

## 2026-10-08 — 2D Outdoor v2.3 Gate O1

任务 xinghai-2d-outdoor-v23 completed/released。新独立场 SC_2D_OutdoorV23_Courtyard，源v2 OutdoorCourtyard，Renderer2D沿用13/global默认0未改。原素材/布局冻结3749文件零SHA变化，120层结构/原绑定/相机/灯参数一致，20次恢复材质41→0，真实卸载退出与保存重开无自有材质/Probe残留，零编译错误。主对照16张1600×900 Play、消融、55秒3080帧真实视频及三档性能交付 artifacts/xinghai-2d-outdoor-v23/comparison.html；12项结论 docs/render-research/2D-Outdoor-v23.md。Engineering PASS、Gameplay PARTIAL（录像编码/细边稳定性及正式游玩未全认证）、Art User Pending；BestStable保留N3、BestVisual候选Balanced、Presentation候选Light。无新纹理/Mask/3D、未读Script(s)、无旧地牢修改/提交推送/关机/自动下一阶段。网页离线链接检查通过，浏览器自动化初始化失败，交互UI未认证。

> 2026-10-18 OSSASTRA PRE00：旧源码实际Editor/Player编译列表均0、编译错误0；4个授权桥接/绘制器同条件隔离且主体/meta保留，SpinePixel核心保留。恢复点44052文件SHA已核验；1265既有场景/Prefab/meta一致，3个旧测试PNG meta有导入平台追加，严格完整性验收未全通过。用户并行Unity现场保留，空场Play进入/退出成功但URP错误及用户target.fbx使纯空场验收Pending。原.git/历史仍在，未提交推送，未M01；任务写租约释放。

> 2026-10-18 OSSASTRA PRE-00最终交付：旧源码实际Editor/Player0、编译无错、新独立相机场Play约50秒并退出；2384旧文件和1265场景Prefab/meta最终全同。用户批准根Git安全移出后新main零历史初始化，再批准13文件12374字节公开首发至https://github.com/huangjianhangxbx-star/OSSASTRA，根提交724408bb08376df0cd2dd45ff58d6d6efab295e0，远端/公开树完全核对，无Gitee写入。技术骨架未含Packages/旧渲染配置/商用插件，干净克隆/Player构建未验；旧备用Renderer OnValidate错误保留。用户原技术场模型/Spine已恢复Edit clean。全部写租约释放，未启动M01。

> 2026-10-18 M01用户确认：体素基本单位保持现状，2D角色/素材通过PPU及相应导入缩放适配；当前无正式场景。先做独立动作/命中核心及测试，正式移动平面、角色绑定、Spine接入待场景确认，不自动改样板场比例。

> 2026-10-18 M01纯核心候选已交付：Candidates/M01（Assets外，避免用户编辑中域重载），6个领域C#文件，动作资格/边沿/时钟/取消/单释放去重/唯一HP。真实RED到GREEN，最终29/29，netstandard2.1/C#8构建0警告0错误；复验修复60Hz Release晚一帧与AttackReady假可用问题。全部源码/逐行草稿/权威表/用户待审账本齐备，14文件审查ZIP另存且SHA全验。无Unity导入/场景写入或真实Spine/移动/碰撞验收，无提交推送，GitHub仍724408b；整体M01 PARTIAL，正式场景尚缺，所有写租约释放。

> M01场景候选已交付：用户target 1标准4x4体素世界1x1x.25，120块独立SC_M01_CharacterTest，rozeul原导入.001/Scale1；真实输入→移动Spine→单普攻范围→唯一HP/闪白链已接。核心29测试与场景14受控断言通过，Pause表现脱节RED→GREEN；旧RendererFeatures错误保留。源场/FBX/Spine数据SHA保持，旧2384/1265保护记录0差异。12生产C#/真实接线/全源码/逐行草稿/待审账本及46文件ZIP齐；虚拟输入/生命周期注入不代物理手感或用户审批。测试设备0，已退出Play，独立场Edit ready/clean，所有写租约释放；无M01 commit/push，GitHub仍724408b，不自动M02。

2026-10-19 M01候选推送完成：GitHub OSSASTRA main@fb4f781149093847611cd0411eb11705343eee16，远端ls-remote与本地HEAD一致。52文件，自有源码/测试/审查和交接文档；核心复跑29/29，场景沿用已验收ZIP快照，当前磁盘场景改动保留（25增1删），未覆盖。无Gitee写入，无第三方/用户媒体发布。候选已发布但架构所有区间仍待审。状态complete/read-only，仅发布子任务完成；全部写租约释放。公开交接入口docs/ossastra/M01-HANDOFF.md。


2026-10-19 解除禁编完成（用户明确授权，覆盖PRE00默认隔离约束）：隔离asmdef/meta已移出Assets到E:/WORLDCREATOR/Backups/OSSASTRA/Legacy-Reenable-20261019；四处PRE00首尾编译条件已撤销，旧源码回到原Assembly-CSharp/Assembly-CSharp-Editor归属。prepare_editor ossastra-legacy-reenable-20261019实测ready，编译错误0；旧Editor源1028，CharacterRoot和真正旧InputHandler已加载到Assembly-CSharp。VELGITA原Prefab的InputHandler SerializedObject可见503项属性（含InputAction绑定子项），配置可读。当前用户测试场1_卧室 dirty及VELGITA Prefab Stage均保持，不保存/切换/Play；磁盘场景及Prefab前后SHA保持。原隔离配置及四包裹文件备份齐备。仅本机变更，未提交推送，不写Gitee；不宣称旧场景运行功能已验。complete/read-only，全部Unity/源码写租约释放。公开仓库PRE00隔离说明是历史发布状态；本机当前已按用户后续授权解禁，后续任务不得误恢复禁编。

## 2026-10-19 角色控制第一轮修整完成
用户四项明确要求已执行。后续必须先读根OSSASTRA_RULES.md（命名、InputAction、旧VELGITA参考、脚本分区、动态记录规则与迁移/发布边界），现状详见docs/ossastra/character-control-refinement.md。旧代码当前按用户授权解禁，不得按PRE00历史结论重新禁编。共享控制移至Assets/OSSASTRA/Scripts/Characters/Core与Runtime，去M01类/命名空间；CombatTestTarget置Development。输入为独立MonoBehaviour+每实例可序列化InputAction，真实克隆VELGITA的移动/鼠标/摇杆/左手攻击绑定。新PlayerCharacter.prefab与既有SC_M01_CharacterTest实际连接；场景6引用齐/127 Collider/0MissingScript。领域6源/meta保持，29核心+10输入测试通过、Unity无编译错误；输入夹具非全场Play认证。用户旧Prefab/卧室磁盘SHA保持，最终旧PrefabStage/卧室dirty现场，未运行旧场；未自动提交推送。架构仍待审，无M02/角色技能连段AI完成声明。全部Unity/代码写租约释放，complete/read-only。

2026-10-19 用户架构问答/规则更新完成：新增R13 PascalCase脚本/类型与camelCase变量规则、R14避免协程偏好、R15 Orient/Orientation命名偏好。当前Actions()的IEnumerable/yield为同步生命周期枚举，不是StartCoroutine，不存在等待按键激活；InputFrame只是单次Update采样快照，AttackIntent/BasicActionRuntime负责按下边沿及动作资格，不是丢帧/限帧。当前CharacterInputFrame公开大写字段不符合用户新变量规则，AimAt/朝向拆分仍未改，保留待下一轮实施。CharacterController为Unity原生3D胶囊角色运动/碰撞组件，不是自写角色Root；独立于朝向拆分决策。本轮只更新规则/解释，无代码/Unity/提交推送。complete/read-only，文档租约释放。

2026-10-19 character-foundation complete/read-only：按用户批准完成变量camelCase、CharacterMotor→CharacterMovement保留GUID/原速度配置、独立CharacterOrientation.OrientTowards、每角色CharacterDataBus与Root初始化注入。Bus只读访问InputHandler.CurrentFrame/Movement/Orientation/BasicActionRuntime/ControlAuthority的真实状态，不复制HP/动作时钟；Movement/Spine/Melee实际使用Bus，非空壳。保留同步枚举器，无协程。共享Prefab+自有测试场接线完成。核心29、输入10、Bus/朝向12、Play场景14合同全通过；首轮空挥虚拟/真实鼠标冲突已定位为夹具问题，隔离指针binding后14/14，失败原件保留。运行测试设备0、InputSettings恢复、编译无错，退出Play后M01测试场Edit ready/clean。旧卧室/VELGITA磁盘SHA保持；测试开始仅自有M01场clean，无运行旧场。规则文件R13–R16、当前数据流/权威/审查账本已更新，后续先读docs/ossastra/character-foundation.md。当前本机候选未提交推送，架构仍待用户审定；全部Unity/文件写租约释放。

2026-10-19 character-candidate-publication complete/read-only：用户明确授权无下一阶段计划则推送候选并等待。GitHub OSSASTRA main@6fdd36ad2b90d7bb948c0be8df6c50649f331265已发布，远端ls-remote与本地HEAD一致；64文件变化（含保GUID迁移），共享源码/Prefab/测试场/规则/交接/证据。发布前代码及资产与最终测试manifest 0差异，核心复跑29/29；场景14/Input10/Bus12为已验收证据。新增R17：键盘可见翻转、攻击鼠标优先于键盘，当前未实现分离；R18：角色子系统接口架构偏好，当前仍具体类型Initialize，明确未实现。没有已批准下一阶段计划，不自动开始。未上传旧源码/第三方媒体/本机解禁删除，旧asmdef与meta本机删除保留未提交；无Gitee写入。原生Git直连443失败，使用用户当前已启用Windows代理的单次git -c http.proxy参数推送成功，不改全局配置；未通过连接器重建提交/改远端身份。交接入口docs/ossastra/M01-HANDOFF.md。所有写租约释放，等待用户后续计划。

2026-10-19 M01-R1-v05 TECH_VERIFIED/read-only：用户授权v0.5实施，基准6fdd36a。R17显示朝向/攻击方向分离、合法鼠标优先/释放帧方向快照，R18窄职责接口与每角色Bus连接完成；Root移出Development依赖，DebugControls独立，HitResolver无假Initialize。核心29/输入10/Bus12/Play14/R17物理15/接口12/同构双角色4均通过，完整源码行号SHA/审查图表/待审账本与外部审查包齐备。用户确认无场景编辑后Editor API清除1条旧debugTarget覆盖并保存，Edit ready/clean、0MissingScript、引用齐、Probe设备0。旧资产保护、解禁状态/原Git不改；无提交推送/Gitee/M02。全部写租约释放；仅人工手感与逐行架构审查待用户，候选优先级仍待审。

2026-10-19 github-default-publication complete/read-only：已更新R11，后续已授权任务成果验证后默认定向提交推送GitHub OSSASTRA，无需逐轮再确认，禁止Gitee；保留可发布范围/远端核验与用户架构审查边界。本次为接口说明与未来发布规则登记，未改生产源码/Unity、未执行新发布；全部文档路径租约释放。

2026-10-19 M02A-v06 complete/read-only：P0 4eca664、实施 a5a4a54e145e7a044c78aeb4773bcd2d55b183be、GitHub main 8925b76bc364784692bce324717627aa89b27bd6 已推送并核验远端一致。临时同Spine双实例控制闭环/中文规范已完成；真实29核心/10输入/12Bus/26双角色世界/15边界/1冲突/15单角色R17通过，18源全量行号SHA/逐段说明/图/权限/接线/注释表/失败原件已公开候选。Current唯一，移交当帧输入屏障、Held松键武装；旧动作继续，重复owner失败不干扰原owner。当前Dual场Edit ready/clean，0缺脚本/Probe，原InputSettings恢复。用户未批准架构/物理键鼠体验；停止M02A，不M02B/跟随/技能。旧代码禁编删除仍本机保留不入提交，无第三方/用户媒体/Gitee发布。全部Unity/代码/文档写租约释放。


2026-10-20 M02C-v10 complete/read-only：实现704a16d、发布记录653f0f175cda73e84062b0b16b9a3719e6917b0c 已推送GitHub OSSASTRA/main并核对远端一致。单一有效Frame选择Player/Companion/Neutral、场景级最小FollowDirector完成；原Root/Bus/Movement唯一运动和动作链保留，无AI攻击/寻路/传送。纯策略13、核心29、最终新场19、Selector13、M01 15通过；旧M02A26及其后定向帧回归的版本范围详见M02C-verification，失败原件保留。原M01/M02A场、共享Prefab、InputSettings及指定模型/Spine共7文件SHA一致。最终独立M02C场Edit ready/clean、角色2且各6引用齐、MissingScript0、Probe0。9份变化源码行号SHA/中文说明/待审账本与验证公开；用户架构和手感仍待审。旧解禁删除保留不提交，无第三方/用户媒体/Gitee；停止M02C不进入M02B/M03。全部Unity/代码/文档写租约释放。

2026-10-20 M02C-R1 active/unity-writer。用户明确执行v1.0通用跟随能力重构；基准653f0f1本机远端一致，旧禁编asmdef/meta本机删除不提交。独占Runtime/CharacterFollow新增、Contracts/Root/Selector必要接线、Director/DebugView缩窄、共享自有PlayerCharacter.prefab、M02C新R1验证场及必要迁移、Candidates/M02C/Tests本轮测试、docs/ossastra/M02C-R1-*、README/规则与本任务同步。原M01/M02A场、用户媒体/旧代码/渲染配置只读。当前自有M02C Play clean无PrefabStage，允许先测旧行为再退出。无并行Unity写租约，保持唯一Current/Frame/运动和动作执行权；无AI战斗/寻路/M02B/M03。

2026-10-20 M02C-R1 TECH_VERIFIED/docs-writer：运行时重构/Editor Prefab迁移已完成，Unity与代码写租约释放；仅保留本轮文档/README/规则/选择性发布租约。R1世界19、组件27、分配8、Selector13、M01 15、M02A26、原M02C19、纯策略13/核心29分别通过，源码/待审入口已写。最终R1 Edit clean/角色2各7引用齐/MissingScript0/Probe0，Prefab GUID保持；保护9项SHA不变，仅授权Prefab内容变化。准备R11发布，无Gitee/自动下一阶段。

2026-10-20 M02C-R1 CANDIDATE_PUBLISHED/complete/read-only。实现e5819ab、发布记录6263eda4b85c50a5b2232d31fd811e666fbef3c5 已推送GitHub OSSASTRA/main并原生核对远端一致。跟随策略/历史/参数归各角色CharacterFollow，Director只目标分配/绑定，Root单入口/原动作运动链保持；7变化源/Prefab增量/独立R1场/真实合同/集中待审账本已发布。功能和架构人工审查仍待用户，未M02B/M03/战斗AI。最终R1 Edit ready/clean，保护9项SHA一致；仅授权Prefab内容变化。旧解禁删除保留未提交，不Gitee/媒体/第三方发布。外部审查ZIP46payload逐项SHA核验，路径work/OSSASTRA/M02C-R1-20261020/OSSASTRA-M02C-R1-review-6263eda.zip（不是可独立运行完整工程）。全部Unity/源码/文档/发布写租约释放。

2026-10-20 M02B1 active/unity-writer。用户明确执行v1.0，基准6263eda本机/远端一致；当前R1 Edit clean，无PrefabStage/并行Unity writer。独占本轮必要Core/Runtime生命、近战、输入/控制接线、新Enemies/Runtime与Development战斗验证、新M02B独立场、共享PlayerCharacter.prefab、Candidates/M02B1、docs/ossastra/M02B1-*、README/规则与本任务同步。保护四旧场/媒体/旧代码/原稿/开发细则；旧解禁asmdef/meta删除保留不上传。只B1真实HP/双向攻击/死亡与交接，无B2/返场/M03/正式AI，R11验证后GitHub main发布，禁Gitee。

2026-10-20 M02B1 TECH_VERIFIED/docs-writer：B1真实世界最终22/22、边界15/15、核心战斗13/13、旧核心29/29、跟随13/13；R1世界19、M02A26通过；M01原14/15且过期来源名夹具定向适配1/1。反馈空block真实异常已修复，编辑器焦点/缓冲夹具失败原件保留；最终临时InputSettings完全恢复原资产、设备0。新B1 Edit clean/双角色各8引用齐/敌目标2/MissingScript0，四旧场/Prefab GUID/input/指定素材8项SHA不变。代码/Unity写租约释放，仅持有本轮文档/选择性发布租约；旧解禁删除保留，准备R11 GitHub，禁Gitee/B2/M03。

2026-10-20 M02B1 CANDIDATE_PUBLISHED/complete/read-only：实现f39ffef97e1f3b405429f7f93ceeddf7e24dadf7、发布记录a72a7f6414c7efe4c738c7b7ad1bd5526bcf78e8 已推送GitHub OSSASTRA/main并核对远端SHA一致。B1真实世界22/边界15/纯战斗13/旧核心29/跟随13、R1世界19/M02A26通过；M01原14/15+夹具定向1/1，真实失败记录保留。最终B1 Edit ready/clean，8项保护SHA不变，仅授权共享Prefab生命增量。12源行号SHA与待审账本公开，人工架构/手感仍待审。审查ZIP E:\WORLDCREATOR\XingHaiHuiLang\work\OSSASTRA\M02B1-20261020\OSSASTRA-M02B1-review-a72a7f6.zip 共66 payload逐项SHA通过，含本地截图不上传；非完整工程。既存旧解禁删除保留未提交，无Gitee/用户素材/第三方发布；停止B1无B2/M03/正式返场。全部Unity/源码/文档/发布写租约释放。

2026-10-20 entity-root-boundaries docs-writer：用户要求自机/其他NPC/敌方使用不同根与各自标准，参照灰烬之国。仅登记明确规则和只读核查，拥有OSSASTRA_RULES.md与本任务同步；不改生产源码/Unity，不自动启动B2/M03。基准a72a7f6；原有旧禁编删除保持。

2026-10-20 entity-root-boundaries complete/read-only：R21登记自机/NPC/敌方不同根与组装标准。当前敌人独立MonoBehaviour，Root[]只是玩家目标引用；共用Vitals/Melee/动作结算，仍耦合玩家目标类型。Cinderia本机CRAR01/CR04静态研究确认玩家输入与敌方AI分层、共享技能/伤害入口；NPC完整架构未证。B1§10明确B2/M03另行制定，本机未发现可直接执行的下阶段详细任务单。本轮只规则登记/答疑，无代码/Unity更改，未把建议当已批准实现；全部租约释放。

2026-10-20 ARCH01 active/unity-writer：用户批准v1.0及重开未保存B1现场。基准b596f97本机远端一致。拥有Combat新模块、Characters必要契约接线、EnemyRoot/Brain、NpcRoot交互、Development、新ARCH01场、受控迁移B1场、必要共享Prefab/测试/本轮文档。保留旧解禁删除。采用A路径；其他旧场、用户媒体、全局设置只读。R11 GitHub发布；不B2/M03。


2026-10-20 ARCH01 CANDIDATE_PUBLISHED/complete/read-only：按v1.0三根/中立战斗落地。实现a8a9fdf911e846961b8865c0efbe236340c749f1，发布记录718b2385204251b423fef7f0d38da8ba1a1de810，GitHub OSSASTRA/main远端完整SHA已核对。EnemyRoot独立唯一时钟+StationaryEnemyBrain，中立CombatVitals/Melee保GUID迁移；NpcRoot自身交互真实回执。B1场受控迁移A路径，非字节不变。技术：ARCH世界22/边界15；新增中立NPC初始20/21（source-disabled前摇未取消缺陷）修复后23/23及加严24/24；最终B1世界22；M01/M02A/M02C/R1共享定向3/5/5/5、R1真实世界19；NPC纯9。真实失败与测试/编辑器编排错误在verification分类，不声称一次全绿。最终新ARCH01场Edit clean，Missing0/Probe0/原InputSettings/仅原键鼠笔设备；三份移动meta、共享Prefab/meta、四早期场/PRE00/InputSettings共11项SHA不变，仅授权B1变化。12份源码行号SHA/待审账本与审查/验证/publication已发布，人工手感架构仍待审。本机旧解禁asmdef/meta删除保持未提交；不Gitee/用户媒体/第三方/binobj。释放全部Unity/源码/文档/发布写租约，停ARCH01不B2/M03。ChatGPT引用链接为登录墙，未读取不可见正文。截图仅本地work/OSSASTRA/ARCH01-20261020/arch-game.png。

2026-10-20 animation-direction complete/read-only：已核查新Root明确Read→方向→Request/Advance/命中→Movement→Present，当前三片段View无正式意图/路由/过渡。旧VELGITA真实Composer/Mapping/Override/Transition/Handle链路与CRAR01§5只读证据对照；记录R22正式能力要求、单一ActionId/时间线建议与原作未知范围。无代码/Unity更改，全部文档租约释放。

2026-10-21 M02B2 technical-complete/publication-pending：共享PostureState/CombatPosture接入两根唯一时钟和原Release/HP事实；独立B2场，旧场/Prefab78项SHA不变。纯23、旧核心29/战斗13、最终世界22/边界26、旧ARCH/B1各22、R1真实19、M01/M02A定向3/5均通过。根禁用新硬直漏洞真实RED后修复。NPC近远回执正确。最终B2 Edit clean、Missing0/Probe0、原输入设置与设备。10源码完整带行号/哈希待用户审查，verification明确未测项及B2-18仅继承敌方LOS范围。旧禁编删除保持未提交。只GitHub；不推进B3/M03。Unity写租约释放，发布文档租约暂持有。

2026-10-21 M02B2 CANDIDATE_PUBLISHED/complete/read-only：实现f4cc13287059b02b69b716ab5989b166fcd02724已推送GitHub main并远端核对；本轮50文件，不含跨项目历史同步总表。全部源码/Unity/发布租约释放。用户架构和手感待审；停B2。

## 2026-10-21 ROADMAP01 收口
四阶段实现已推送：cc07189 / 6adef5d / a70973d / e824e3d；集中入口docs/ossastra/ROADMAP01-review.md。保存的综合场Assets/OSSASTRA/ROADMAP01/SC_ROADMAP01_CombatLoop.unity包含场景返场、可选两段、语义动画、独立有限敌动及NPC。各阶段技术Gate通过；旧场/Prefab保护74项无变化，旧asmdef/meta删除保持用户现场。Unity Edit clean；全租约释放。用户集中审查未进行，正式媒体/完整旧系统未迁移，不追加下一阶段。


## ROADMAP02 收口
2026-10-21 ROADMAP02 CANDIDATE_PUBLISHED/complete/read-only：S5 35b9a5a / S6 1f53cc8 / S7技术03bee68 / S8 bf0ff977d3df65bd1131c65207fab8fcb3405422均GitHub main远端核对。新场SC_ROADMAP02_TeamCombat Edit ready/clean；Missing0/Probe0/测试设备0/原InputSettings/预警材质0，76保护SHA不变。S5纯10世界11边界8；S6纯8世界12；S7纯10世界11新场连段14；S8自然12保存回开再12、边界2；旧B2 22/R1 19/M01定向3/M02A定向5/核心29。15份源码行号SHA公开。S7正式敌美术ASSET_BLOCKED，本地Spine测试替身；人工手感和架构待审，未迁完整职业/技能/敌人。旧禁编两删除保留不提交；无Gitee/商业媒体。全部Unity/源码/文档/发布租约释放，停止ROADMAP02。

2026-10-21 companion-contracts：两个伙伴候选接口移至CharacterContracts，签名/行为保持；Unity刷新ready、无近期Error、反射接口检查通过，ROADMAP02场clean。小蓝已有4.1.23运行时导出3项SHA匹配，媒体本地只读。详见docs/ossastra/companion-contracts-20261021.md。

2026-10-21 ROADMAP03阶段暂停：S9共享防御网关7b2a104已GitHub发布；S10体力/Shift闪避保存本机（纯18、真实18、连段15、旧连段14），未发布。prepare_editor因OPERATION_JOURNAL_UNAVAILABLE/日志tmp共享占用受阻，退出Play/干净域重载未恢复。S11–S13未开始。119保护SHA不变，旧禁编删除保持；写租约释放，接手先读docs/ossastra/ROADMAP03-publication.md。

- 2026-10-21 ROADMAP03 收口：S9-S13完成技术Gate，角色资源/盾/独立炸弹与场景共享Y/一次性G篝火只装配新ROADMAP03场。最终Edit clean；用户旧禁编asmdef删除保持。详见任务与ROADMAP03-verification，人工体验待审。writer释放，不开启ROADMAP04。

2026-10-23 VC02A complete/released：产品 a5f2df8b6c5cc0c532c0b921447bbc432c321abc 已GitHub main核对，角色动作组/六技能/三根17分支10符文/独立装备Spine/正常HUD链路完成技术验收。127纯合同、56自然阶段、旧场15；保存重开/域重载9、定义无污染、writer释放。正式美术/人工审定未完成，ART_ENTRY_READY=false，停A不执行B。用户既有dirty保留；用户取消关机，不执行。详见docs/ossastra/VC02A-handoff.md。

2026-10-23 VC02B active：用户明确连续执行B0-B8。新独立SC_VC02B_IntegrationReady，库存36纯/初始化9/开包移动拖放真实输入20/药与五食物实际30通过；B4/B5新接线待验，B6-B8未完成。唯一unity-writer仍归本任务，四既有dirty保持；不Gitee/不关机。详见tasks/2026-10-23-VC02B.md。

2026-10-23 VC02B 技术完成/发布准备：B0-B8包内Editor闭环完成。库存唯一实例/后备消费者/地面全灭/独立Spine/Rig及作者工具，31目录与31有限初始实例；自然47/60/35、UI57、A纯127/A自然56/旧场15、域重载13通过。A213原生资产SHA不变。实际追加Player构建被旧Unity-Favorites-Tool Editor类型误入Player阻塞，未修改第三方。ART_ENTRY_READY=true仅Editor契约审查，正式素材未完、用户规范未冻；详见VC02B-handoff/readiness/verification。四既有dirty保持，发布完成后释放writer。


2026-10-23 VC02B complete/released：产品a2fa0cd9e9efa98eba3f3567c6704f417f6bfb16已GitHub main远端核对。347 payload指纹一致，351自有文件；Editor技术准入完成，Player旧第三方构建阻塞单列。最终SC_VC02B_IntegrationReady Edit clean/原输入设备，无MissingScript。四原dirty保持，全部writer与发布租约释放；不自动ART01，不关机。详见VC02B-publication.md。


2026-10-23 功能审查工作台：docs/ossastra/OSSASTRA_功能模块审查与落实清单.md 已建立24模块、72待审验收项和5跨模块复验链。候选功能实现与架构审查状态分离，正式审查均未代用户勾选；建议从M01-M04根/数据/输入/仲裁开始。仅文档，不是下一轮代码调整授权。


2026-10-23 input-layout：共享PlayerInputLayoutSO成为B角色与场景输入源；左右手字段RightHandAction/LeftHandAction具旧字段迁移兼容；Space/B闪避、QER/LB面键、Y/LB+A、Z/LB+RB、Tab/Start、Select非暂停菜单、LT+RT动作组切换、RB四格引用。默认扳机组合宽限0.08s可配，仍沿原模式切换资格；投掷瞄准快捷使用明确延期。26输入合同/8实际场景通过，真实硬件长测未做；旧输入override清理，全部writer释放。


2026-10-24 AM01 complete/AM02 active：ed74ae1af1698306cd44f5fdb7a871dbc1bbf3b7已GitHub main核对。正式StateArbitrator登记五状态，Combat实际拥有唯一Runtime/连段/事务受理转发/事实分派，运动和并行能力保持原时序；B与候选Prefab接线，旧参考不变。自然17/并行5/生命周期13/纯68/背包47通过，InventoryView销毁回归RED→GREEN；原动画链尚未迁入，继续AM02/03。用户已有dirty仍保留，唯一Unity writer由本任务持有。

> 2026-10-24 AM01–AM03技术完成：AM01 ed74ae1、AM02 67efa5c已GitHub；AM03会话级移动/瞄准独立、配置Action按设备采样、UI按下归属/真释放保护和实际能力目标接通。来源24、综合36、原生E目标7、双手9、举盾开关包13、生命周期13、背包20+手柄拖拽20通过；作者工具3项RED→GREEN。最终B Edit clean/用户VELGITA Stage clean/Missing0，身体双手与Enemy/NPC真实播放有效；旧代码参考及用户键位dirty保留，实体XInput设备保留但实机操控未测。交接docs/ossastra/AM01-handoff.md、AM02-handoff.md、AM03-handoff.md；AM03 faf2938已GitHub精确核对，整轮complete/全部租约释放，不Gitee。用户明确要求整轮验证发布后正常关机。

> 2026-10-24 AM复核定向修正technical-complete：F01-F06及Movement已RED→GREEN，完整快照/正式Router/装备条件/持续设备凭据/背包不补RT/同帧pulse落正式链；六技能25、混合36、举盾13、播放22等通过。仍为注册式主干，不宣称所有旧子状态对象已恢复。最终B Edit clean/Missing0，实体手柄体验与旧Player构建保留；GitHub定向发布中。见ossastra/AM01-AM03-directed-fixes.md，Unity租约释放，仅本任务文档/发布租约。

> 2026-10-24 AM定向修正complete：c7feaf14c5bd1e7542e79924529c24c712f1fe08已GitHub main精确核对（API逐Blob/树/Commit一致，expected-sha无强推）；F01-F06与Movement及相关回归完成，B Edit ready/clean，所有租约释放。不Gitee/不重复关机；用户旧dirty保持。交接docs/ossastra/AM01-AM03-directed-fixes.md。

> 2026-10-24 CONFIG-01技术完成：根Inspector实际view导航与只读已执行信息；Workbench schema3重导164SO/3模型755条，326依赖指纹/307引用GUID对账；当前配置四表/一页样板同步，72用户审查勾选原样。A/B导航13、保存恢复5、小流程19及导出日志RED→GREEN。仅Editor/资料，35原dirty保留，B Edit clean，非实体手柄验收；GitHub定向发布中。交接docs/ossastra/ArtIntegrationCandidate/ConfigurationHandoff.md。

> 2026-10-24 CONFIG-01 complete：产品448ccc5e16255c333455dee8fc033b7e18641d6e已GitHub main核对；A/B根已可直达实际Map/Router/Rig，当前配置说明与可操作样板已交接，末次19步确认身体与左右装备真实轨道播放。B Edit clean/Missing0/无临时输入设备，35原dirty保留，全部租约释放；后续由用户选定具体配置逐模块审查。

> 2026-10-24 DIR01开工：用户附件明确模型目标改为多面，局部替代R24，保留旧仲裁器与原式动画SO。仅研究/隔离可视工具/接入草案，不修改正式运行时或生产资产。

> 2026-10-24 DIR01研究技术完成：776骨/494slot/138动画，官方CLI副本拆图及4.3原工程读取；方向根位移＋原作Late矩形slot显隐已静态闭环，非仅源裁切。独立29+6检查/300姿态、192拆图通过，正式Composer清另一owner方向轨已隔离复现。仅报告/工具/DIR02草案，未接生产；D03/D11/D14未穷举或未正式接入。B Edit ready且dirty保留，35磁盘dirty保护；Unity/源码租约释放，仅文档定向发布。

> 2026-10-24 DIR01 complete：产品14715e763e4ab92936aa7f26b49ca34fb457c744已GitHub main精确核对。自有研究台与三方报告/DIR02草案交付；D03/D11/D14边界详列。B Edit ready/内存dirty保留，35原磁盘dirty与素材SHA一致；全部租约释放，不自动DIR02，不安装MCP/不Gitee/不关机。

> 2026-10-25 DIR02 technical-complete：小蓝4.3五面Variant与SC_CharacterPresentationCompare已实际接入，A小蓝/B原rozeul；原式动画链的合法结构层、每实例显隐、30条动态Rig与同装备换锚点完成。42输入/1200姿态/10生命周期/16实战/36混合/5原生E验证通过。仅候选，未覆盖主场，复杂穿插/近似动作PARTIAL，实体手感未代签。原36dirty与源SHA保护，最终Edit clean；GitHub定向发布中，Unity/源码租约已释放。见ossastra/DIR02-report.md。

> 2026-10-25 DIR02 complete：产品93eda092c894722a65beff6974217228c2f615dd已GitHub main核对。小蓝五面/rozeul候选与指南交付，原主场未替换、用户美术未代签；最终对照场Edit ready/clean，无临时设备，原36dirty与源SHA一致，全部租约释放。停在用户试玩与正式素材接入判断，不继续扩张。

> 2026-10-25 DIR02-FIX01验证进展：显式新规则替代DIR02静止右杆选面。Orientation统一最终显示方向，键鼠普通移动/鼠标攻击分开，手柄用移动/最近移动；普通未来投递消费同一政策，盾冲/跃击/闪避保留具名机动快照。小蓝身体0.08混合导致场外slot入中心已实际定位，Blue限定零mix A/B及1440连续帧验证通过，未全局改rozeul。历史攻击末帧目前NO_REPRO，不宣称唯一根因已修复。生命周期最终10项通过；最终动作组及重开交接仍在收尾，不沿用旧候选PASS覆盖新规则。

> 2026-10-25 DIR02-FIX01 complete：产品4fe18701e1c9ce6b3cd731b3ebd434c0eeabbcd0已GitHub main精确核对。键鼠移动/攻击鼠标分权、手柄移动定向及未来投递一致；Blue限定身体mix0修正场外分支滑入。历史末帧NO_REPRO/PARTIAL，不冒称已修；49动作组/29停止/43输入/36混合/15方向/10生命周期及1440注入帧定向证据已交接。原36dirty/素材不变，最终对照场Edit clean/ready，全部租约释放；仅用户复验，不自动DIR03。

> 2026-10-25 AUDIO-M01 technical-complete/publishing：独立Ossastra.Audio根/纯仲裁/SO/固定六源DSP与监控已在自有SC_BGM_AudioMigration验证。Core17/Mix12/Transport16/Host5与真实12项输入/音乐故事、原创三层/快速淡变、跨场与设置、缺媒体回退通过；原373保护路径/36dirty不变。Player仍被旧Unity-Favorites-Tool阻塞，用户听感及正式Title/Dialog/Boss未签。全部Unity/源码租约释放，仅定向GitHub发布记录继续，不推进AUDIO-M02。

> 2026-10-25 AUDIO-M01 complete：产品df90b9fe9ac7c831f791878408e495a6cc46e0bb已GitHub main精确核对，128文件只含独立BGM与原创诊断/证据。Editor自有新场Edit clean/监控已打开，全部租约释放、原36dirty保护。不代签用户听感，Player仍第三方阻塞；停于AUDIO-M01。

> 2026-10-25 AUDIO-M01-FIX01 technical-complete/publishing：已复现死敌点位空挥Accepted误启combat，最小修正交战入口需有效敌对成员；BGM仍只读、原3秒calm保留。点位3/3、7条实体路径15/15，附近活敌命中600→588及退战通过。撤回旧远处空挥等同真实遇敌的验收，修正夹具；用户Editor全局静音保持true，当前两RMS未通过单列。372保护路径/原36dirty不变，Edit clean，全部Unity/源码租约释放，仅发布记录继续。

> 2026-10-25 AUDIO-M01-FIX01 complete：651a6e42da2d01b6827627abd7eca7e917f98eea已GitHub main核SHA。交战入口拒绝无有效敌对实体的Accepted；原calm/BGM被动边界保留。7路径15断言/点位复验通过；纠正旧远处空挥验收，RMS受当前Editor全局静音限制单列。Edit clean、原36dirty及372非目标SHA保持，所有租约释放；不扩张仇恨/距离系统。

> 2026-10-25 AUDIO-M02 technical-complete/publishing：在原唯一音频根组合短音仲裁/32源池/共享Master与分组，保留原BGM DSP；Feedback仅视觉，独立音频宿主且单场观察者准入。47/32/9/3/7合同、8混音后输出、旧BGM12/FIX01七路径15、切场6/旧无根bootstrap及自然四总线路由通过；投掷正例6/6，公开资源为原创数学样本。原645保护路径/36dirty保持，Player仍旧插件14错误、人耳听感未签；Unity/源码租约完成释放，只剩定向GitHub发布。详见ossastra/AUDIO-M02-handoff.md。

> 2026-10-25 AUDIO-M02 complete：产品e79be1f1d5b4d03b8843b2d9a184971897bcfe7a/164文件已GitHub main精确核对。原36dirty逐行保持、645保护路径无差异；统一根/分组混音/固定池及真实输入/原BGM/FIX01/旧场接管交付。Editor自有SC_AUDIO_M02_SoundIntegration Edit clean/ready，全部租约释放；Player旧插件14错误与人耳待验收单列，不Gitee、不M03、不关机。

## 2026-10-26 AUDIO-SFXPASS verified
用户要求现有内容音效表现整理已完成Editor验证：唯一音频根/BGM/Mixer不换；公共72条语义配置、本机42精选/63有效目录，四段挥击、真实接触/盾挡/受伤、枪声3变体与机械装填/真实爆炸分开。缺闪避/喝药/元素专用素材留白，无正常诊断音回退。AbilityFeedbackFact与CombatContactBatch只读快照，Cause区分直击/周期/派生，冰世界2调用仅补元数据；队列冻结来源租约防重连旧声。合同14/21/47/32/13/9/8/4/3及所有权7/生命周期3/场循环6、BGM17/12通过。自然故事20/21（短1秒敌击窗口未新增损血，保留失败）、游侠真实库存换装/绑定LT枪械7/7；70秒Listener输出两段无削波。原36dirty保留，628保护仅3已登记事实生产源码变更，625一致；Editor原SC_AUDIO_M02 Edit clean。详见docs/ossastra/AUDIO-SFXPASS-handoff.md；受限PCM/本机内容SO不公开，GitHub发布另记，不Gitee。

2026-10-26 AUDIO-SFXPASS complete：GitHub产品5fc2ee359b2fde008f4e1c82fac4daa7557331e5/64文件已远端核验，tree与本地逐字节相同；受限PCM/本机SO零公开。所有writer释放，原36dirty精确保持，Editor原场Edit clean；仅发布回执文档收口，无后续自动迁移。

2026-10-27 AUDIO-BB-FUSION启动：用户授权F00–F10一轮连续融合及完成后休眠，保留唯一音频根与原BGM/仲裁；efdd6138基准、36原dirty保护，参考内容本机隔离，源码/素材只读审计并行。

## 2026-10-27 AUDIO-BB-FUSION verified
F00–F10工程实施/定向验证完成：原唯一音频根/Transport/32短音池与Mixer保留，A0–A3原子切档；本机参考67媒体、117绑定/111事件，3音乐/3区域环境。实际四档同事实Mixer捕获无削波、峰5/5/9/9；主输入20/21、枪7/7、世界8/9保留真实未过。跨场6/附加5/晚Host4/死亡3/原创替换3通过。36原dirty/67源目录项/5FSB/67派生保持；新场SC_AUDIO_BB_Fusion Edit clean、418依赖无私有内容、零测试设备/对象/缺脚本。Player既有收藏插件14错，人工NEEDS_LISTENING，真冰等CONTENT_INCOMPLETE。详见docs/ossastra/AUDIO-BB-FUSION-handoff.md。Unity/源码writer已释放；仅文档/选择性GitHub发布收口，不Gitee。

2026-10-27 AUDIO-BB-FUSION complete：GitHub产品765fea102638cb329a45867a74dee471da0574a8/tree2c0fc859已精确核验main，158路径操作不含私有媒体/SO；4旧孤立meta仅撤销跟踪保留本机。36原dirty不变，新场Edit clean，全部writer释放。人工/内容缺口与Player旧插件阻断另列，回执见docs/ossastra/AUDIO-BB-FUSION-publication-receipt.md。本轮不启动新迁移；按用户要求最终安排现有S3睡眠。

## 2026-10-27 AUDIO-LISTENING-FIX complete
已修整体低音量/下缘浑浊/持续不适声：运行期唯一听点跟随实控(.8u)并精确恢复原相机Listener，四地图边界不作声学墙、disabled marker不遮挡、遮挡不自动Room；原Esc/Select菜单新增保存的Master/BGM/SFX/Ambience/mute/推荐，当前1/.65/1/0。环境默认关闭并释放实际loop，用户确认持续声消失。私有A2/A3一次声净×1.6，最终真实28.096s枪械7/7/峰.749935/0削波，初始超限结果保留；UI14、可用性4、遮挡3、生命周期4、初始禁用2、位置2、跨场6与SFX32/语义14/档案14通过。36原dirty/PCM/源库保持，最终Edit clean、原相机Listener恢复、临时设备/听点0；全部writer释放，仅定向GitHub发布收口，无电源动作。交接docs/ossastra/AUDIO-LISTENING-FIX-handoff.md；不代签全部音色/旧Player插件或其它内容缺口。


## 2026-10-29 DS-MAP-01 technical-complete / publishing
独立SC_DontStarveMapLab、原Blue完整控制链Variant及Root管理的optional ViewSpace完成；滚轮/十字键45度旋转，Q/E/方向移动保留，WorldRoot/碰撞固定。三地表/40物件/结构侧俯视，本机素材隔离。119/162/37/13/10/7/24/91/48/31/25/10分层检查及原Commerce移动普攻、三轮重开通过；错误长按连招夹具预期、全Editor隐藏管线材质增长、原作OBSERVATION_BLOCKED与真实OS焦点/实体手柄/用户视觉未代验均单列。42保护项/40源SHA保持，最终Edit clean/Missing0；全部Unity/源码租约释放，仅自有文档/选择性GitHub发布。详见ossastra/DS-MAP-01/handoff.md。无电源动作。

2026-10-29 DS-MAP-01 complete：产品ff08d51399476893ae7b08fd63a79b3716abec81/186路径已GitHub main精确核验，受限媒体/私有帧Mesh零公开；独立GPL转换工具附许可。全部租约释放，最终SC_DontStarveMapLab Edit clean，无后续自动迁移或电源动作。用户视觉/原作实机/实体手柄和外部Hidden管线材质增长保持分列。见ossastra/DS-MAP-01/publication-receipt.md。
