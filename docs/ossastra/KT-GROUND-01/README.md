# KT-GROUND-01 地表研究场

打开 `Assets/OSSASTRA/Scenes/Research/SC_FrontierGroundLab.unity`，或使用菜单 **Tools → OSSASTRA → 地表研究 → 打开独立研究场**。这是独立研究场，保留已保存双角色单敌场的角色、控制协调者、输入、战斗和镜头；旧场与基础Prefab未改。

## 实际组成

- 原作 Starter 自然地面：原生 Grid＋七层 Tilemap/TilemapRenderer，14条 RuleTile 邻接规则加默认，共15张边缘 Sprite；七层各用不同底纹。
- 本场唯一作者数据也是原生 Tilemap，没有每格一个 GameObject、持久第二份格子数组或自建分块Mesh。
- 旧场实测32×32＝1024平方米；新场64×48＝3072平方米，恰好三倍。逻辑格仍4米，角色尺度不变，底纹世界周期未随面积放大。
- 一块连续3D地面Collider和四面外墙；显示层不是玩法碰撞。地表观察默认显式关闭本场敌方攻击，右上研究面板可开启交战；世界不暂停。

## 使用

退出Play，选择地表根，菜单 **Tools → OSSASTRA → 地表研究 → 刷地与来源**。选择本场Surface、中文底纹与笔刷大小，然后激活绘制、擦除或矩形填充。实际写入同一份原生Tilemap，按正常场景保存。

详细操作见[刷地手册](brush-guide.md)。刷新显示保留手绘；只有明确的重置命令才重新生成示范布局。不要把重置作为普通载入步骤。

Play中使用原有移动、滚轮/方向键旋转、PageUp/Down缩放、Z切控。F6是结构观察，不是暂停。右上研究按钮提供图层/原生遮罩/来源对照。

## 来源与适配边界

原图、同源Atlas切片、底纹以及源保存格的规则回读有证据：7959/7959 Sprite及旋转矩阵匹配，15/15 Atlas切片像素匹配。详见[来源调查](source-findings.md)与[Unity适配说明](source-to-unity.md)。

原Shader为编译体，其精确采样和光照公式仍未知。本工程采用自有URP Unlit裁切Shader、可配的16米候选底纹周期、满覆盖底层和世界边界裁剪；原始Tight Mesh未逐顶点还原。示范路网是本工程布局，不宣称复制了宣传图或源地图。

原创资产替换规格及当前入口的真实限制见[替换规格](original-replacement-spec.md)。来源区ObjectField仅展示，不能误当成完整的正式美术替换管线。

## 验证

[验收矩阵](acceptance.md)逐项区分通过、失败和未测。已实跑的范围合同22/22、原生笔刷数据合同16/16、导入合同15/15、观察面板29/29与小样结构13/13，不代替Scene View实际操作验收。

三倍场真实InputAction路线走过旧±16边界到四边角；滚轮八档、缩放与两次切控共24项通过。另有实际Current的Applied命中回执证明、敌方真实释放/扣血/原入口复位，7项通过。

两轮同机位1600×900有限对照中，带角色1倍/3倍均46批次，三角2621→3209；隐藏三实体Renderer后均43批次，三角1296→1884。玩法模拟和HUD仍在运行，不能称为无角色CPU对照。7个地表材质身份与全Editor资源计数稳定，原生作者数据恢复通过。Editor帧间隔不是独立构建或GPU认证；GPU耗时未知。

## 本机依赖与发布

`Art/FrontierTaleReferenceLocal`、`GroundResearch/LocalGenerated`及本机来源研究目录不公开。公开代码、场景壳和工具不包含原图、原规则资产、原Shader或可逆派生表。缺本机依赖时生成器应明确停止，不能静默制作替代地面或覆盖已保存候选。

本机完整回执位于 `E:/WORLDCREATOR/XingHaiHuiLang/研究/开拓物语地表/KT-GROUND-01/04-evidence/`。代码验证入口在 `Candidates/KT-GROUND-01/`，经项目Funplay工作流由唯一Unity writer执行。


实际Scene View回归还覆盖擦除、连续矩形、Esc/焦点与外部Undo保护，以及跨原生块的7格笔画两次保存重开。当前只留下原示范布局，测试哨兵和临时chunk设置已清理。八角正常画面与实际道路mask/底纹对照已核看；连续视频级闪烁和用户手感仍未代验。
