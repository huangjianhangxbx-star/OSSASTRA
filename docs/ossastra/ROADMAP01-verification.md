# ROADMAP01 验证
基准108ea250928f8a8f1a338c8fbf0c976488598e61。链接登录墙，采用用户本地完整附件，不声称读到在线后续内容。

## S1 PASS
- dotnet：RespawnSchedule 10合同通过（先观察缺类型文件编译RED，再GREEN）；死亡事件登记模拟时刻，不复制HP。
- Unity实际Play+虚拟InputSystem：12/12，包括真实敌杀、退场、单死同身份满HP/架势恢复、全灭、落点阻挡、释放再按恢复移动。
- Unity生命周期：7/7，服务禁用、应用暂停、玩家暂停不计时；重新启用只恢复一次；禁用/销毁本体不复生。
- 旧B2真实世界回归通过，22条记录、0失败。74份旧场/Prefab及meta SHA与接手相同。
- 首轮测试1失败为fixture写死Generation=1，实际测试前已自然返场；改为初始Generation+1，保留first证据。测试未清零生产代际。
- 新场只生产恢复双人体；敌人/物品/世界不重置。单死10秒/全灭0.5秒为SAMPLE。没有真人键鼠手感验收。
- 证据：Candidates/ROADMAP01/Evidence，夹具Tests。缺商业媒体/包的克隆限制沿用DEPENDENCIES。

## S2 / S3 / S4
未实施；必须通过各自Gate后更新。

## S2 PASS
纯路由10/10；真实Spine+虚拟InputSystem14/14：Idle/Run/Attack、相同动作不重播、新动作同片段重播、移动不抢攻击、双实例、暂停、破势/死亡fallback、返场、失焦取消。首次夹具把第二次按键安排在AttackReady之前，合法拒绝导致1项失败；改到Ready之后通过，未放宽生产输入资格。兼容本机Spine4.3 GetTrack API。旧全局同名类型用明确别名隔离，无旧代码修改。
动画仍仅单主轨，暂无真实Hit/Break/Death片段；fallback明确可读。未迁移完整VELGITA动画系统。

## S3 PASS
纯连段14/14；原动作核心29/29；真实InputSystem双角色14/14；旧B2世界22/22。
实测Stage1造成12HP、Stage2造成18HP，每段只Release一次；原场敌最大HP为72（夹具首次误写100已修正）。一条预输入、按住不自动第三段、空挥重新Stage1、切人清缓冲、B也可独立两段、破势取消未释放、真实墙挡命中均通过。
修复真实初始化缺陷：早期Root.Awake检查Profile.isActiveAndEnabled发生在Profile.OnEnable之前，改读序列化enabled，运行时仍查真实激活。first/diagnostic失败与green均保留。
新场A/B加BasicChainProfile，旧场未加、保持一段。Stage2暂无专用动画，明确复用atk1且新ActionId重播。没有真人手感验收。
