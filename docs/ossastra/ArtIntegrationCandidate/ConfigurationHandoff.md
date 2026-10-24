# CONFIG-01 配置入口整理交接

2026-10-24；起点本机/远端main均为0b922064b217e46e274ec29b5c2349ac355aa2e1。用户附件v1.0为执行规格；私有ChatGPT链接未取得正文，不声称读取隐藏对话。

本轮三项完成：

- 根Inspector从实际view导航身体、Controller/ConfigManager/Composer/Handle、Map/Router/Overrides、角色定义/Rig与装备表现。区分场景覆盖/Prefab默认、Edit预置/运行手槽。Play读已执行事实，不重路由冒充命中规则。保留原有序列化字段。
- 现有工作台导出schema3：164SO、3实际模型、755模型条目；116局部PASS/48INFO/0REJECT。326依赖文件指纹与307对象引用GUID已对账。事件时线用Freeze结果导出；旧兼容字段单列。当前无已挂载Override资产，空值如实保留。
- 当前A/rozeul/主手I组/Q身体技能/盾A与盾B组成可操作样板；无需新建世界。README四关系表、readiness及模块清单M01–M04/M07/M11/M24同步当前实现；72个用户审查勾选原样保留。

|验证|证据|结论|
|---|---|---|
|A/B导航及Editor类型13项|navigation-final.json|四按钮共用真实处理器调用，Selection等于对应目标且OpenAsset返回成功；非桌面鼠标实点截图|
|Mapping保存恢复/Prefab/缺绑定5项|configuration-edit.json|真实A mix .08→.12保存并重新导入回读；finally恢复，原asset/meta字节一致；B不串|
|正常操作小流程19步|configuration-story.json|虚拟InputSystem LT+RT/RT/Q/LT/Tab与uGUI拖放真实箱内盾B，身体/装备同执行ID；当次旧mix、下次新mix|
|导出合同5项|export-contract-final.json|日期/基线、GUID引用角色、场景默认、挂点和typedID/Router存在|
|导出现场/指纹/时线5项|configuration-consistency-final.json|单一clean场、选择/根未改、无Edit初始化；指纹/GUID真实；原生事件时线未导成旧标量|
|已加载Scene枚举错误RED→GREEN|export-scene-read-red.json / export-scene-read-final.json|旧导出LoadAllAssetsAtPath遍历.unity产生ReadObjectThreaded；Scene仍参与依赖/指纹，SO枚举排除Scene，现无错误|
|文档|document-audit.json|本地链接无缺失，72项用户待审勾选不变|

全部证据位于`Candidates/CONFIG-01/Evidence`；夹具为同目录Tests。小流程关闭会话威胁/伙伴策略、恢复体力并传送靠箱以稳定观察；不授予物品、不直接请求能力、不冒充真人实体手柄或无辅助实战。运行Map副本验证快照与真实Map保存恢复是两种独立证据。

本轮产品改动仅Editor工具与资料，未改任何Runtime、场景、Prefab、SO常值、源骨架、第三方或用户规则。原35项dirty（31删除+四修改）保留并排除提交。Unity最终保留B综合场Edit clean；原生日志旧错误证据保留，修正后MCP新日志无错误。Windows Player旧Favorites插件构建阻塞不在本轮处理。

用户现在可以直接从角色根和[一页样板](ConfigurationSample.md)逐模块确定正式内容。当前状态为技术整理完成，正式美术/实体手感/具体架构认可等待用户审查；不自动扩大迁移或永久冻结标准。按既有授权只定向发布GitHub main，不同步Gitee，不关机。

## 发布回执

产品提交448ccc5e16255c333455dee8fc033b7e18641d6e已发布到GitHub main，并用连接器读取main精确核SHA；43文件定向提交，原35项dirty未纳入。后续收尾补强同一19步夹具：实际读取右手/左手Spine track0，普攻与Q右手strike、副手盾B左手strike均通过，身体/装备各自轨道有真实播放。未扩大运行时范围。全部本任务租约释放，停止在用户配置审查入口。
