# DS-MAP-02 Implementation Plan

**Goal:** 排查小蓝闪烁，交付既有双角色与一个敌人的独立旋转样地。

**Architecture:** 沿用CharacterRoot/协调者/伙伴与EnemyRoot。MapResearch只拥有一份观察状态；外层桥接注册观察消费者并向镜头提供当前跟随Transform，切人不重新Bind。敌人只接只读表现，不伪造玩家输入。

**Spec:** 用户附件DS-MAP-02 v1.0的01执行计划、02验收清单（完整副本仅本机研究DS-MAP-02/00-plan）。

## Global Constraints

- 不改原基础Prefab、原Commerce/Freycia/单人场、源媒体、全局管线与受保护规范。
- 滚轮/十字键45°，Q/E保留技能；实际观察基只发布一次，玩家输入只映射一次。
- 共用yaw/targetYaw/zoom，切人只平滑中心；默认smoothTime .20s，不自动转向。
- F6观察不暂停世界；捕获两角色与组内手动切控，死亡系统交接继续。
- 闪烁必须动态复现/因果A/B；未复现保留UNREPRODUCED。
- 37项旧dirty保护；仅本任务自有可发布内容推GitHub，不含媒体。

## Task 1 — 原现象反馈环（M0–M1）

- [x] 读取计划/保护规则并登记唯一Unity writer，验证当前项目、API与保存基线。
- [x] 在原单人场用临时真实Play探针运行静止/顺逆旋转/边界/移动与动作故事；保存实际渲染帧和mesh alpha摘要。
- [x] 先记录异常，再按方向/回调/深度/多写入/bounds逐个变量A/B。只有符合原现象才标确诊。
- [x] 增加默认关闭、容量有界的异常标记/环形导出；正常游玩无逐帧磁盘写入。

## Task 2 — 多参与者与共享镜头（M2）

- [x] Candidates真实行为夹具先验证切人保持yaw/target/zoom、所有消费者解绑、捕获回滚，记录RED。
- [x] MapResearchRoot最小会话端口、-150时序、smooth center；保留单participant装配。
- [x] 外层MapParty桥接只读原Current，绑定全部角色和敌人观察端口；输入捕获只归玩家集合。
- [x] 夹具GREEN，实际Play在旋转中换人验证同帧观察与连续中心。

## Task 3 — 角色与单敌人实战（M3–M4）

- [x] 新builder先preflight RED，保存新PartyCombatLab，不覆盖已存在场；不重做媒体。
- [x] 从Commerce实际A/B、唯一协调者、跟随/交战与一个原敌人装配，本地引用重映射、不同actorId。
- [x] EnemyRoot显式组织可选观察adapter，EnemySpineView只改变显示水平分量；未装配维持原行为。
- [x] 显式开发遭遇复位调用原恢复入口，不自动复活；两人实际攻击/受伤/敌死均验。

## Task 4 — 集成、兼容和发布（M5–M6）

- [x] 执行V01–V22少量故事，保留失败与未验项；动态遮挡、结构捕获/死亡、移除参与者、三次重开。
- [x] 原单人场和Commerce定向冒烟；共享方向若修改，再验Freycia显隐。
- [x] 同条件帧时间/自有资源，Editor与模拟输入证据边界明确。
- [x] 保存回读/入口/报告、选择性GitHub发布精确核验及全部租约释放完成。
