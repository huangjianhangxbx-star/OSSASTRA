# VEGETATION-01A

- 状态：独立候选已交付，定向源码发布中；Unity与所有子writer已释放，仅Root收尾Git。
- 批准输入：v1.0 ZIP SHA25765AAFD10D322EF59BBB6A4F24F3D728FF8F66134110AAD282714D08752864；v1.1二维素材与Shader优先补充覆盖冲突草型条款。
- 独占：Candidates/VEGETATION-01A/、docs/ossastra/VEGETATION-01A/；拟新增Assets/OSSASTRA/Scripts/WorldRendering/Vegetation/和Assets/OSSASTRA/WorldRendering/Vegetation/GrassLab/，导入前核对无既有同职责目录。
- 只读：Main、CHAR Profile、原素材、Environment/Room共享后端、PERF夹具。LOOKDEV仍file-only，当前没有其他Unity租约。源分支23e50dd、既有42 tracked dirty与无主meta保留。

## 范围

一套现有二维素材，用少量贴图承载面与GPU风动，先真实小片，再成片、贴地/排除、作者保存复现与同条件成本对照。不做第二套草型、实体逐叶建模、树木/踩踏/天气权威或自动主场回接。

## 顺序

- [ ] 检查原图及同相机环境风、合法渲染入口，固化最小方案。
- [ ] 先测根固定/风连续及分布排除/复现，再实现候选；共享后端不足即记录，不绕许可。
- [ ] 唯一Editor窗口Native创建独立草场，Main以可恢复的加法隔离保留，不隐式保存Main。
- [ ] G1单簇/小片实机动态，扩为连续草地、第二形状、保存重开。
- [ ] S0/S1/S2/S3及密度阶梯成本、旋转/光影/黑幕定向检查。
- [ ] 恢复现场与R11定向发布；技术/视觉待用户/增量预算/主场许可分列。

## v1.2 接续（2026-11-10）

- 用户批准视频路线补充，SHA CFDEF21124C655DC405AEC6B56DE12FCBE0DFD2DDA9C72F1BADE84986B893F96；仍为同一任务和原独占路径，Root 唯一 Unity/Git writer。
- v1.1已有4099簇、逐簇根尖权重、显式表面/排除、合并分块与独立Lab。14散布合同和24 GPU风函数合同通过；未将其当作最终视觉或成本通过。
- 当前Lab Play、Main加法保留且37根暂关；main-borrow.json active，Main原dirty和磁盘不保存。结尾必须按原GlobalObjectId恢复。
- v1.2优先：R0保存A与差异表 → R1小块Alpha主导/基础地表色/高度及近远色/手工阴影 → R2世界纹理草浪与颜色联动 → R3现有环境适配 → R4已有Lab及成本 → R5候选交付。先实机视觉再扩大覆盖。
- 不改共享Environment/Room、主场、CHAR或源图；不接Terrain新世界后端。Room仍不支持本草Shader，主场不回接。
- 子执行者仅候选目录：VideoAudit、TerrainBake、VideoShader；无Unity或生产写权。

## v1.2 验证与交付

- 已完成R0–R5独立候选。4099簇/27合并块；单套原GrassTuft、Alpha/RGB对照、每surface线性基础色、逐簇高度与Depth颜色、教程手工阴影/项目EnvironmentShade一次、细噪声/纹理粗风/颜色风、45°与30°及旋转入口均已接入。
- 实际三色基础色GPU合同通过；新风50检查/100采样通过，旧Legacy24检查/58采样复验通过。初次失败保留，1ms纹理量化与饱和测试点修正边界见交付，不冒称原测试全部首轮通过。
- 禁用资源释放/重建/冷重开通过；原3512+587放置集、独立A/B与2份surface map保存读回；重烘焙GUID保持、version递增。2临时Shader经SHA/引用核验Native删除。
- 两次有真实Draw的Win64 Release四态及Editor四态完成；Player4099簇S3−S0 frame elapsed中位+.0406ms，6162簇+.10595ms；40→67→67→121 Draw Calls。EditorGPU差+.11776ms，PlayerGPU未取得null，PlayerGPU预算未认证，不代签主场60。隐藏零Draw初轮已作废，原始有效数据gzip验证SHA后保留并回收大JSON。
- 原生36秒固定机位与32秒切换/旋转录像完成，已看关键抽帧；不冒称逐帧看过全片或用户已认可。视频核心为等价项目适配，未知常量未造原值。
- Main保护事故：00:34未知保存把临时off写盘，Root按原37根记录恢复最新内容并保存修复，随后卸载隔离；结束重开Main Edit/dirty=true、37根原状态回读一致、两CHAR Profile/原PNG/meta SHA守恒。Main原SHA不保持，完整收据main-borrow.json/final-restoration.json，不用阶段mainTouched=false掩盖。
- 现只有主场Edit、无测试Player或采样；Main/Room无草回接，视觉待用户，GPU预算未认证。交付入口：docs/ossastra/VEGETATION-01A/v1.2/交付.md；本机研究/植被/VEGETATION-01A/delivery/v1.2/index.html。
