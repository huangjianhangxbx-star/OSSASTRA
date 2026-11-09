# VEGETATION-01A v1.2 交付草案

这是独立草地候选，尚未接回主场或 Room 门窗／黑幕。当前交付一套既有二维草图、分段承载面与 GPU 风动；基础地形和碰撞继续由原地表负责。最终形体、配色与风浪观感仍请用户审阅。

## 从哪里看

打开 `Assets/OSSASTRA/WorldRendering/Vegetation/GrassLab/SC_GrassWindLab.unity`，Play 后展开左上“二维草风动独立小样”。当前完整两块地形共 **4099 簇、27 块**；推荐从正常 **45°** 观察，30° 是教程斜角对照。左右箭头／滚轮每次转 45°；WASD 只移动本场静态代理，不是原角色玩法测试。

- **A 原效果／B 视频候选**：在当前机位和保存放置集上切换两份独立作者配置。Play 使用运行副本，退出不把试调写回作者 Profile。
- **教程参考／项目环境**：共用草形与风动，分别观察教程配色链和接入现有环境受光后的结果。正午／暮色／深夜按钮只调整独立样场环境。
- **Alpha 配色／原 RGB 细节**：前者以原图透明轮廓配合程序配色，后者恢复原图 RGB 细节贡献。它们不会重画源图。
- **仅细摆／仅粗风／叠加／+颜色风**：区分叶片细摆、世界纹理粗风、二者叠加和颜色风；“原地表／静止草／轻风／阵风／环境风”是整体显示及风强度入口。草叶投影独立切换。
- **正常／高度／地色／阴影／近远／粗风**：仅为诊断色。看实际效果后请回“正常”，不能把调试色当最终配色。

作者参数在 `Assets/OSSASTRA/WorldRendering/Vegetation/GrassLab/GrassCandidateB_v12.asset`。本次保存值包括 density **36**、maxDensity **72**、高度 **0.32–0.50m**、宽度 **0.30–0.48m**、纵向 **3 段**、maximumBend **0.14m**；Environment／CombinedColor、rgbDetail **0**、depthColorStrength **0.04**、windColorStrength **0.65**。这些是候选值，不推广为整场画风默认。

## 地表改色后怎样更新草根取色

退出 Play，在对应 GrassLayer Inspector 指定单一 surface 和明确 `terrainSources`，点击“烘焙当前层基础色”。必须包含该 MeshCollider 同物体同 Mesh 基底；可追加同场、向上、位于该 surface Bounds 每侧 0.05m 范围内的地表 overlay。不同 surface／高度层分别烘焙，不扫描世界。未知 Shader、MPB、透明混合和草自身 Renderer 会明确拒绝。

它只烘焙未受光基础色与有效 alpha，不捕获角色、草、灯光或阴影，也不每帧重拍。重烘焙保持已有纹理 GUID，版本递增，只保存自有图／映射和该草场。实际最终配置回执为 **780.57ms** 一次、两张 RGBA32 基色图合计 **524288 字节**；A 地表映射版本2、B版本1。

## 已有证据与边界

- 地表三色 GPU 合同：方向、遮盖深度、alpha 有效区、世界 UV、源材质及 RT 状态归还通过。视频风 GPU 合同 **50/50**、**100** 个采样通过，覆盖草根固定、零风／静止、细粗风、周期接缝、颜色联动及变形范围；该合同只认证数学／信号，不代替真实草卡透明边缘、阴影或视觉验收。
- `reload-lifecycle.json`：禁用释放、重建、冷重开均通过，4099 簇／27 块，作者放置数据保持，草场 dirty=false。
- Windows64 Release 构建完成，**0 错误、46 警告**，约37.96秒；不是“零警告”。只包含草场，不构建主场。
- R2 实录36秒、1280×720；模式巡览32秒、1600×900，均完成且无音轨，已抽取巡览5帧。录像编码帧率不是游戏帧率，也不自动构成视觉通过。

四态成本来自同机位、1600×900 的真实绘制采样：S0 原地表；S1 静止草／教程配色；S2 相同草层开风；S3 项目环境与草投影。下表是 **frame elapsed p50**，包含等待，不是有效 CPU 或单独 GPU 成本。

| 独立样场 | S0 | S1 | S2 | S3 | S3−S0 |
|---|---:|---:|---:|---:|---:|
| Editor，4099簇 | 1.3296ms | 1.3795ms | 1.3719ms | 1.3880ms | +0.0584ms |
| Release Player，4099簇 | 0.3014ms | 0.3259ms | 0.3266ms | 0.3420ms | +0.0406ms |
| Release Player，密度54／6162簇 | 0.32795ms | 0.41910ms | 0.42200ms | 0.43390ms | +0.10595ms |

Player 推荐档 Draw Calls p50 为 **40→67→67→121**，证明确有提交。早先隐藏 Player 的零 Draw 数据已单独判无效并剔除。**Player GPU 时间没有有效样本，保留 null，不写成0；GPU预算尚未认证。** Editor 有有限 FrameTiming GPU 样本（S0 0.6175ms、S3 0.7352ms），不以此代签 Player／主场预算；本轮不宣布主场稳定60帧。

## 主场现场事故必须保留

Main 原本 dirty=true。在 additive 隔离期间，发生了来源未明的保存，将37个临时关闭根的状态写入磁盘。Root 已依 `main-borrow.json` 恢复这37个根，保留较新的场景内容后保存并卸载；最新回执 status=restored、repairedRootCount=37。**不能声称 Main 原始 SHA 未变，不能用 final-lab-config 的阶段性 mainTouched=false 覆盖这次事故。** 最终仍须由 Root 重开 Main 并恢复原 dirty=true 状态后读回；本草案未操作 Unity，不代签最后现场。

证据根：`E:/WORLDCREATOR/new/Project_ANKALUTE/artifacts/VEGETATION-01A/`。关键文件：`v12/*-summary.json`、`v12/reload-lifecycle.json`、`v12/terrain-bake-contract.json`、`v12/gpu-video-wind-20261109-173117-1580000.json`、`v12/r2-record-result.json`、`v12/mode-tour-record-result.json`、`v12/grass-player-build.json`、`main-borrow.json`。草案只汇总已落盘事实；视觉待用户、Player GPU预算未认证、Main／Room未集成仍是交付边界。
