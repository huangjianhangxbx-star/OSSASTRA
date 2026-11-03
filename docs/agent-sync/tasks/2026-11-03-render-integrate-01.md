# RENDER-INTEGRATE-01：既有体素渲染接入当前认证主场与过期场景清理

- 状态：complete/read-only；所有租约释放
- 开始日期：2026-11-03
- 唯一Unity/Git writer：root；并行只在明确新源码子目录互斥写入。
- 授权：用户要求把之前3D体素地图渲染接入正在认证的主场，删除已不需要的旧验证场；后续明确确认使用3D URP，可按需要调整渲染管线。主场本来已是XH_URP3D/UniversalRenderer，不把此次误述为从2D改3D。
- 主场：Assets/OSSASTRA/GroundWorkshop/Samples/KT-VIS-04/SC_RoomVisibilityLab.unity，同场增量保45°/2角色/1敌/业务/物理/原存档。
- 独占：新增Assets/OSSASTRA/Scripts/WorldRendering/，SceneVisibility/Rooms中环境显示角色/合法深度输出最小缝；自有主场渲染配置；Candidates/RENDER-INTEGRATE-01、docs/ossastra/RENDER-INTEGRATE-01、本任务同步/规则后缀。旧RenderLab源码/材质/Profile只读复用，不替换全局URP预览controller，不改变默认renderer；新增camera专用Feature。
- 清理范围：先Native GUID/依赖/BuildSettings核验历史Backups15场与两个私有Checkpoint；无活引用后备份到Assets外并用AssetDatabase删除。扩展候选需同门禁。原dirty场、原31已删除与对应迁移目录不夹带；依赖素材保留。
- 基线：研究/渲染接入/RENDER-INTEGRATE-01/git-before.bin、protected-dirty-before.json和同步原始字节。当前Native Play/主Lab，Edit后保留当前用户场景状态并做Native安全副本。
- 禁读Assets/Script和Assets/Scripts，不手改unity/prefab/asset/meta，不改用户维护/开发细则/AGENTS/Skill；源码R19中文职责头，R11必要验证后只自有代码测试文档GitHub，不Gitee。

## 计划
- [x] 找回旧用户选择：Outdoor Light、Dungeon Light、Sunset Expressive、Green A；发现旧预览替换全局Pipeline、RealToon在正向后端未支持、主场禁止原depth后处理。
- [x] 在正向世界重建后提供合法深度与环境角色遮罩，环境后处理不能用旧未裁世界depth或泄漏黑幕；正式2D角色/HUD保持清晰。
- [x] 以显式引用读取旧Light等Profile语义，接入室外/室内Look与可切原图/单项对照；不拖旧实验输入/HUD/扫描/全局管线重建根。
- [x] Native同场对照、窗口/动态撤权/角色与HUD/原材质引用/RT释放重入验证；保存重开。
- [x] 依赖核验、外部可恢复备份后删除无用场；核保留场引用/BuildSettings无失效，报告删除列表。
- [x] 定向GitHub收口与保护校验，释放全部租约。

## 实际收口

Native编译/角色/快照14/GPU7/MSAA8/桥6/控制器8、主场室内外实际feed与2RT2材质释放重入通过。17旧场景已Native备份校验后删除；原失效SampleScene2构建项替换为当前Lab。主场已Native保存重开并读回dirty=false、缺失脚本0、45度、两Feature有效，原06存档路由恢复。全部子代理停止；Root仅定向发布文档/源码。完整限制见本轮交付文档。

产品提交：961b47e963576506541edd339e398d280cc5cd6c，GitHub已核对。全部租约释放，不自动开启下一阶段。
