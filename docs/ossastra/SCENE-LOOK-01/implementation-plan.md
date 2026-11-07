# SCENE-LOOK-01 实施计划

**Goal:** 真实主场可试玩的木屋材质双候选与统一夕照，原效果可还原。
**Architecture:** 复用 Comparison/Look 缓存和现有 EnvironmentRoot/SkyBinding。显式房屋 source/语义登记接入材质预设；单一环境候选仅调整场景环境漫射贡献，普通/Sight共用函数。角色已认可系统不重做。
**Spec:** 用户 SCENE-LOOK-01 v1.0 ZIP，外部研究目录保存完整输入。
**Tech Stack:** Tuanjie 2022.3.62t16 / URP14.2.0-t1 / Native MCP / C# / HLSL。

## 约束
45度镜头；保留当前 dirty 与只读资产；唯一 Unity writer；测试 Commerce 启动前隔离；不改可见许可/碰撞/业务/角色参数；不公开参考媒体；不关机。

## 任务与验收
1. 快照当前真实场景和配置。读取实际 material slots、Pass、响应与权限家族，固定机位记录 A、暖砂0/50%受光和日照分项。保存业务文件 SHA，Edit 配置独立测试数据根，进入 Play 前读回。
2. 房屋：Native 编辑候选验证每个真实槽完整覆盖、未知 source 原样保留、源材质指纹不变、两个Look使用同一response。测试先用现有木屋运行，若映射缺失则红；实现显式 source/role 数据及候选，重复安装不重复，A恢复数组。可取消房屋 lightingOnly 但保留顶点色策略并修正注册幂等契约，地形/树/水不改配色。
3. 环境：先断言现有夕照基线与零强度恒等，复用实际帧自然/手动天空；新增纯解析器及候选资产。HLSL 仅混合环境漫射的色度、保留亮度，不碰main/local/specular/emission；室内世界坐标旁路、角色旁路。自然15度至0度至夜间连续，非有限值安全关闭。
4. 控件：现有 EnvironmentSimulationPanel 增加原表现/增强融合和当前Profile状态，独立保存目标；VoxelPanel说明材质范围/对象数，实际槽数另列覆盖表。Native创建实际候选并接入主场/独立场，保存重开读回。代码需编译/合同检验，GPU用同机位与分项图验证，最多两轮美术校准。
5. 回归：正午/低夕照/地平线/冷夜/室内暖冷灯，两角色/窗外/封窗/揭顶；有界反复切换不增资源；一段连续日落+转镜头录像。构建实际主场 Development Player，隔离数据源，读回实际GPU帧和错误，原业务SHA不变。
6. 收口：三组原始比较图、操作与新增资产指南、CPU/GPU实际可测项和限制；仅定向source/docs发布，不夹带原dirty或用户素材。推荐候选独立保存，不暗改旧默认。
