# VOXEL-LIGHT-01：显式三维分面受光

**当前为 [v1.1：方案C独立明暗调参](v1.1.md)**。用户偏好C，覆盖v1.0的B推荐；原C0/A/B保留。当前主场新增两条百分比、单项档位、独立会话与显式保存入口。下文数值表保留v1.0基础说明。

本轮给选定三维表面增加可校准的受光响应，不接管灯、天气、相机、房间许可或角色受光。当前主场保留45°、环境底光1.99，以及已认可的角色主体、内轮廓、外缘柔光和高亮保护。

## 在主场使用

场景仍为 `Assets/OSSASTRA/GroundWorkshop/Samples/KT-VIS-04/SC_RoomVisibilityLab.unity`，没有新建验证场景。

1. 进入Play，展开左侧“体素受光”，切换 **A 原版 / B 校准 / C 轻分面**。当前保存主场保留A启动；本轮先看C的独立调参，B/C只应用于22个显式登记Renderer。
2. 环境模拟窗口“去光影样板”到原观察区；比较对象包括台阶、柱、墙、树、四个屋顶与粗糙/光滑/金属参考球。
3. 新增四个合并网格样块：灰色墙、浅石台阶、深木柱、绿色树冠。它们在三个档位使用相同几何与配色，没有逐体素GameObject或Update。
4. 作者入口：观察区子物体 `VOXEL-LIGHT-01_显式材质对照` 上的 `VoxelSurfaceComparison`，以及 `Assets/OSSASTRA/WorldRendering/VoxelSurface/Profiles/` 中两个Profile。

开发面板可修改C的独立会话；作者Profile只在明确点击保存时写入。C0、会话、已存候选和本绑定组默认分开，操作见v1.1。诊断0–7正常交付为0，不进入候选默认。不要将运行时私有材质另存成作者源。

| 参数 | 中性 | 当前B | 当前C |
|---|---:|---:|---:|
| 主灯分面响应 | 0 | 0 | .65 |
| 间接漫反射 | 1 | .65 | .65 |
| 环境反射 | 1 | .25 | .25 |
| 直接镜面 | 1 | 1 | 1 |
| 亮部软肩强度 | 0 | 0 | 0 |
| 软肩拐点/渐近上限 | 4/8 | 4/8 | 4/8 |
| 诊断/顶点色RGB | 0/关 | 0/关 | 0/关 |

M0发现原石木已是非金属、低光滑度；台阶/墙/Canopy已有硬边法线，且没有顶点色。本轮没有把不存在的金属度、顶点色或法线错误当成修复。B减少的是选定材质自身间接通道与环境镜面的泛白，不减全场真实灯光。v1.1在用户认可的C上增加独立明暗控制，不自动更改全项目默认。

## 着色边界

`VoxelLit`与`SightVoxelLit`只有名称和许可宏不同，共用Input、Forward和Lighting。所有Forward、Caster、DepthOnly、DepthNormals使用同一无条件UnityPerMaterial布局；实际SRP Batcher兼容码均为0。源材质、顶点法线、Alpha覆盖和遮光保持原合同；顶点色只显式乘入albedo RGB，不改变alpha。

间接漫反射、环境镜面、直接镜面分别计算。主灯的可选方向函数为 `lerp(x, x*x*(3-2*x), strength)`，连续、单调且0端点为0；镜面保留原NdotL，额外灯不应用方向塑形。距离、阴影、spot/cookie、云影仍连续，云影只乘一次。软肩按光贡献使用统一RGB倍率，不对最终颜色逐通道截断，不改Emission或全局曝光。

URP的bakedGI通道可能包含烘焙直射；这里的“间接漫反射”是URP该通道的响应，不声称从Mixed/Subtractive中重新拆出了物理反弹。既有Mixed、Clustered、light-layer、AO和coat计算顺序保留；没有新增ClearCoat材质作者能力或修改PackageCache。

许可路径继续执行Partial/历史色/黑幕，EnvironmentFull仍由后端授予，不由材质自行放行；Roof继续原揭顶许可，源Caster保留遮光。新家族精确注册、逐源选择adapter，复用既有调度位，没有第17位或全场Mesh替换。

普通Voxel通过原EnvironmentSkyBinding在相机Begin消费同一Frame/工作集；非owner绑定不清理有效owner，普通角色标记重置为环境role1。合法绘制阶段继续原MPB覆盖与归还。无相机登记或工作集失败只关闭该环境接收，不改变可视权威。

## 生命周期

Profile只存纯参数。比较器只管理明确Renderer和完整材质槽，按源材质/Profile缓存私有副本；切档、显式刷新和生命周期边界才写材质数组，不新增每帧MPB写者。

外部改写数组后逐binding让权；禁用/销毁只归还仍完全属于自己的数组。外部继续持有自有clone时将该clone移交并移出缓存，不制造Missing、不继续刷新它。正常切回A或禁用会销毁已脱离Renderer的私有缓存；之后B/C允许重建副本。源材质和Profile不被运行时修改。

## 验证与本机证据

可重放合同位于 `Candidates/VOXEL-LIGHT-01/`，Native执行由唯一Unity写者负责：

- Math：真实DX11 ARGBFloat/DrawProcedural，80项，先数值RED再GREEN；中性等价、独立分项、AO一次、方向曲线、保色软肩。
- Settings：23项，覆盖源材质只读、多槽、非法值、缓存、重绑、外部改写、缺目标槽、归还和移交。
- Routing：18项，覆盖真实新家族、混合3槽、Full/EnvironmentFull/Partial/Roof、缺引用局部拒绝/恢复、Shader变更拒绝、MPB与数组归还。
- OrdinaryContext：17项，覆盖相机owner/顺序、role残留、无效工作集、构造失败与恢复。整数全局必须使用Get/SetGlobalInteger；旧GetGlobalInt读取浮点槽的测试错误已纠正。
- 两Shader四个Pass同步SetPass无消息；主场保存重开后22组原引用、B/C作者值、45°与原环境参数准确。
- 固定太阳A/B/C与七种诊断图位于本机 `artifacts/VOXEL-LIGHT-01/Fixed/`、`Diagnostics/`；26秒真实太阳/相机/移动冷暖点灯录像为 `sun-camera-local-light.mp4`。首段因重载中断的录像不作为完成证据。

Windows Player实机九组与角色同屏见证均已通过：270个真实主相机帧、退出码0、运行时错误0，详见同目录`validation.md`。没有宣称穷举全部Mixed/Clustered平台组合，GPU时间未取得时明确未测。

媒体、参考图、场景、作者材质与Profile仅本机保留；源码、重放合同与文档按既有定向规则发布GitHub。原版A始终可回退，没有把整张地图批量改成候选效果。
