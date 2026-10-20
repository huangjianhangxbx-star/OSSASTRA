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
