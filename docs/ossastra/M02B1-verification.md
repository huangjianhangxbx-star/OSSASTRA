# M02-B1 定向验证

基准6263eda，本机工作树实施；团结2022.3.62t16/Spine4.3.26。旧代码解禁asmdef/meta删除作为既有本机状态保留，未纳入发布。C级共享HP/控制/Prefab风险，按B级定向规模执行，不全旧项目扫描或PlayerBuild。

## 可重复合同与证据
| 计划项 | 结果 | 依据 |
|---|---|---|
| B1-01 独立HP | PASS | world初始不同Health/ID；boundary跨开发重置同实例 |
| B1-02 A真普攻 | PASS | 真实InputAction→Accepted不扣→Released敌72→60，来源A |
| B1-03 B真普攻 | PASS | Tab真实切换、B释放敌60→48，来源B |
| B1-04/05 敌人真攻击 | PASS | 实际Enemy Update/动作/空间查询，分别A100→80、非主控B100→80 |
| B1-06 友伤/自伤 | PASS | 实际重叠盟友/自身无损；纯阵营资格13项含敌同阵营拒绝 |
| B1-07 多Collider | PASS | 敌人临时附加第二Collider，实际玩家释放仍只减12 |
| B1-08 空挥/取消 | PASS | 隔墙不请求，Accepted后出圈具名取消、无迟到伤害；边界来源身份不符关闭空批次 |
| B1-09 主控死亡 | PASS | 实际前摇致死、当帧清源/取消/关闭Renderer与Collider，Current转存活者，Held移动/攻击必须释放 |
| B1-10 非主控死亡 | PASS | B死亡不取消A动作；A仍释放敌扣12 |
| B1-11 换人后输入 | PASS | 正常Tab来源保留，死亡后新移动/新攻击分开验证；无第二Current |
| B1-12 全灭 | PASS | Current null、两个来源Neutral、Follow不可用；禁启会话/分配不恢复死人 |
| B1-13 单死亡 | PASS | 同Applied回调重复通知，DeathCount仅+1，HP0不负数；旧Release不重开 |
| B1-14 死亡与释放竞争 | PASS | 前摇取消未扣敌HP，已Released扣血不回滚；敌人自己的死亡也取消其runtime |
| B1-15 旧M01/M02A | PASS（分项） | M02A26/26；M01原集14/15，过期来源身份夹具单独适配后1/1；不冒称一次15全绿 |
| B1-16 旧跟随 | PASS | 保存的R1场原世界19项通过；原M02C文件字节保持，未额外机械重跑同一跟随场副本 |
| B1-17 持久化 | PASS | Editor API保存重开新场；原四场/Prefab GUID/InputSettings/指定素材SHA保护；最终Edit/引用/Probe回执 |
| B1-18 发布与用户审查 | 候选发布 | 实际Git范围/远端SHA在publication/交接；全部源码仍待用户 |

## 分项结果与范围

- B1核心资格/Health显式重置13/13；旧领域29/29；原跟随策略13/13。
- B1真实世界修复后22/22；另有真实组件/碰撞/生命周期边界15/15，不合并成一个“总套件”。
- 世界合同通过虚拟Input System设备驱动实际角色，敌方由实际Update请求/释放。开发注入只用于精确死亡/来源边界，仍经BasicActionRuntime→AttackRelease.Resolve→Health→目标通知，不冒称注入是敌人的自主攻击。
- 旧控制/跟随按影响范围执行，未全量重跑所有旧输入/Bus/渲染/浏览器；无正式手柄/物理键鼠手感、全项目构建或干净克隆认证。

## 原始失败如实保留

1. 当前真实旧路径RED：角色0个正式HP接收者，敌方Released实际Melee为空挥（character-hit-red）。阵营RED是按旧Release的自身/死者语义构造的基线适配，6/8；不能说旧项目已有CombatTargetPolicy。
2. 首轮世界21项、3失败（world-first）：实际HP已扣，ShowHit里flashBlock空引用令反馈异常、LastResult未更新；direct-hit-probe最小复现保留堆栈。修复为显式主线程Initialize创建MaterialPropertyBlock，boundary锁定实际命中不抛且来源记录正确。
3. 另一失败是夹具同时新攻击并在MoveReady前期待移动；改成先新移动、再新攻击两条断言。未放宽原动作限制。后来进一步要求致死前真实设备D/鼠标均已按住，并验证死者多帧仍隐藏，防止“仅排队事件/仅瞬时关闭”假通过。
4. 原M01粗帧夹具使用无关来源coarse-frame-actor，新增身份资格正确拒绝；原14项通过、这1项失败回执保留。仅让定向夹具以实际p.ActorId构造来源，其余一次Release/左靶88/右靶100/重复拒绝断言全部保留，定向1/1。没有放宽生产资格或重跑无关全部用例。
5. 最后加严回放时Editor失去输入焦点，虚拟键鼠未到GameView，10项失败原件另存world-input-failure，未当作产品全绿。独立探针仅改Background策略仍0→0；同时隔离Editor路由后0→1，证明GameView焦点路由限制。最终合同临时克隆InputSettings，设IgnoreFocus和AllDeviceInputAlwaysGoesToGameView，结束恢复原对象并销毁克隆；工程设置资产不修改。
6. 加严Held前置时，Editor回调直接读Keyboard/Mouse控件用了编辑器缓冲而非游戏缓冲，只有该断言失败、其余21项通过；原件world-editor-buffer-failure保留。改从Root本帧DataBus确认move及attackHeld均被游戏采样，再致死；最终同一世界22/22通过，不削弱持键/停止/释放断言。
7. 初次Enemy外部命名空间误取旧全局CharacterRoot造成编译错误，已用明确Root别名修复；该编译错误不当作行为RED。测试回执中的查询/夹具错误不计业务通过。

没有删除失败用例、改HP期待以迎合实现或声称一次全绿。受测时是实际未提交工作树，代码SHA清单冻结最终源码；后续只改测试/文档不冒称修改了生产行为。

## 尚未迁移

正式架势/硬直/盾挡/虚血、LT01的10秒/.5秒返场与资源延续、连段技能、完整敌人AI/追击/寻路、正式美术/HUD/掉落/存档/战斗音效均NOT RUN/未实施。开发恢复不是正式复活。

开发面板显示实际HP/Alive/来源/最近事实，无友方头顶血条；1张实际GameView图本地查看，不上传用户Spine媒体。动态热增Renderer/Collider、Play中热改角色身份/成员不在本轮生命周期支持范围。Root/输入/Coordinator/命中/生命所有权均等待用户集中架构审查，旧A/C/R1账本不代签。

最终保存重开审计：B1 Edit clean、角色2各8引用齐、敌方2目标引用齐/默认攻击开启、MissingScript0、Probe0，InputSettings引用原资产。Prefab内容为授权增量，其meta、四旧场、InputSettings与指定素材共8项SHA不变。
