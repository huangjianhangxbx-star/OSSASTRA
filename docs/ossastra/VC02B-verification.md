# VC02B 验证

受测环境 Unity 2022.3.62t16、安装Spine4.3，Git接手6c93183上的本轮工作区。不是隔离干净工作树：用户旧SC_CharacterVerticalSlice命中层、CharacterEquipmentView行尾、旧禁编asmdef及meta删除始终存在且不纳入提交。最终产品版本以本轮提交和source-manifest为准。

证据均位于 `Candidates/VC02B/Evidence`。自然故事使用虚拟键鼠经真实InputSystem/UI执行，不直接改HP/库存/坐标；绑定/设备/输入设置在结束恢复。边界夹具另标，不能拿它们冒充自然玩法。

|合同|最终证据与范围|结果|
|---|---|---|
|B-T01 唯一身份/状态|inventory-core37-green、final-contracts-reloaded|PASS|
|B-T02 位置事务/容量/修订|纯库存37、ui-boundary-final 57、death-world-first|PASS|
|B-T03 装备payload/空手|死亡边界21、自然死亡35、双手Spine24|PASS|
|B-T04 正常UI/拆合/抓取偏移|ground-input-safe-spawn 47、ui-boundary-final 57|PASS|
|B-T05 布局/素材/身份|source-inventory-*与final-inventory-*三真实分辨率；source-geometry-*内外格；内容差异见coverage|PASS适配核验，不宣称逐像素一致|
|B-T06 开包/输入捕获/不暂停|47真实输入、57边界含输入框焦点、自然补给60|PASS；最终截图单独暂停仅为实拍|
|B-T07 有限整理|纯37中的守恒/坏修订/合并；保留非最优说明|PASS|
|B-T08 Y独立|自然死亡35、死亡边界21、A自然56|PASS|
|B-T09 缓回药/五食品|item-use-final 30、regen-green 10、自然补给60|PASS|
|B-T10 两种来源/一次消费|伙伴投弹14、自然60、A能力纯34|PASS|
|B-T11 后备/引用/接管|companion-item-retry 14、自然补给60、UI57|PASS|
|B-T12 伙伴真实用药与投弹|自然补给60；目标/资格/间隔边界14|PASS|
|B-T13 死亡/返场|death-world-first 21；natural-death-final 35|PASS|
|B-T14 地面/访问/F|47真实拾取拖入、UI57满包、自然35篝火|PASS|
|B-T15 独立Spine/Rig|b6-spine-retry 24、rig-binding-green 4|PASS技术资产|
|B-T16 语义/独立播放|双手Spine24、生命周期20、A纯动画9|PASS|
|B-T17 镜像/显隐/顺序/空间|双手Spine24、b6-lifecycle-final20、point-binding-final6|PASS当前整Renderer排序政策；不声称slot内拆层|
|B-T18 坏内容拒绝|Rig4、Point6、最终13、A定义/能力纯合同|PASS覆盖当前映射和阶段；INFO类型见作者工具边界|
|B-T19 生命周期/无污染|生命周期20、死亡21、最终13、213项A原生资产SHA不变|PASS|
|B-T20 四作者配方|author-recipe-world6、shield-recipe-world10、Spine24、自然补给60|PASS；近战/盾威胁隔离夹具与自然投弹区分|
|B-T21 模型与功能目录|实导157 SO/3模型/755条；README全部当前功能挂点|PASS，装饰骨语义未臆测|
|B-T22 A与旧场回归|本轮127纯、a-natural-regression56、legacy-chain-final15|PASS直接影响范围；未重跑无关旧实验|
|B-T23 重开/域重载|RequestScriptReload后新Play final-contracts-reloaded13；无缺脚本/测试设备，新会话31有限实例|PASS；域重载首个HTTP请求取消，重新准备ready后才执行检查|
|B-T24 准入|readiness.md及本轮交接|以最终发布核对为准|

## 本轮真实发现和修复

- 拆分数量输入后拖动同一选中物品，PointerDown重新Select清空数量，导致整堆转移。`ui-split-red.json`保留；同ID选择保持输入，32绿再扩展57，最终57复验通过。
- 全灭掉落使用条目位无法满足面积格路径：纯模型真实RED后采用5列足够行数；37项通过。未把容量失败当作丢失物品的理由。
- F无条件先选容器抢占更近的篝火：自然35第四轮暴露；散落物优先，其余合法箱/篝火取最近，单次只一个处理。最终自然死亡35通过。
- 样本图标字形ID最初错误、外部网格背景行数和世界标签大命中区已定向校正。最终三尺寸截图为更改后的实拍。

夹具失败另列：非新会话已被敌人全灭、食品释放后0.5秒不足累积1HP、敌人回出生点导致投弹目标失效、Point夹具未SetupPose/scale、直线路线被墙或伙伴阻挡。对应失败文件保留；调整夹具或B初始摆位，不放宽生产伤害/资格。最终契约脚本首次Count方法组编译错误仅属内存探针，改为Count()后13通过。构建探针首次直接写报告被MCP文件保护拒绝，改用SessionState记录，由普通文件工具取回；未关闭安全检查。

## 限制

本轮不是性能基准、正式美术验收、全项目回归、完整存档或干净克隆发行认证。实际临时装备为PROBE；外观作者契约仍待用户冻结。三分辨率1280×720、1440×900、1920×1080均检查实际GameView，不用缩放截图冒充原生窗口。窗口详情按选中显示，箱内容通过滚动访问。

Windows单场景开发构建结果另见 player-build.json；必须以实际终态填写，不能把排队或Editor Play视作Player成功。

最终Player实测：构建Failed，14错误/19警告/15.12秒。错误定位旧Unity-Favorites-Tool的Editor专用类型参与Player编译，asmdef只有name未限定Editor；未修改插件。原生Editor最终Edit/clean且无MissingScript，独立Player未通过，保留阻塞。

