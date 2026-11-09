# VEGETATION-01A v1.2 视频颜色与纹理风候选

本目录仅是非自动导入候选。Root 持有 Unity、生产文件、资产和 Git 写入权。本子任务没有执行 Unity，也没有改动共享 Environment/Room 后端、主场、CHAR Profile 或旧 GPU Probe。

## 三份正式候选

- `GrassLayer.shader.txt`：沿用现有 Shader 名称和 4 Pass，分支选择与可见调试。
- `GrassWind.hlsl.txt`：保留旧数学与四参数入口，新宏显式启用视频风。
- `GrassArtColor.hlsl.txt`：逐簇高度、地色、视距配色、颜色风联动。

`_ArtMode=0`、`_WindMode=0` 均为旧路线。新的 `GrassDeformWithSignal` 附带输出粗风，正式顶点阶段传给片元颜色，不在片元重复采粗风纹理。四个 Pass 共用相同顶点变形、Alpha 裁剪和现有密度 LOD 裁剪。顶点输出显式清零并使用独立 `coarse` 局部变量，避免结构体成员同时作为 `out` 参数的编译器未初始化警告。

## 已冻结的绑定

| Profile / 来源 | Shader 属性 | 意义 |
| --- | --- | --- |
| appearance: Legacy/Tutorial/Environment | `_ArtMode` 0/1/2 | 原路线／视频艺术色／既有环境受光适配 |
| motion: Legacy/Fine/Coarse/Combined/CombinedColor | `_WindMode` 0/1/2/3/4 | 运动与形色联动，0 保留旧 Probe |
| rgbDetail | `_RGBDetail` | 0 只用 Alpha；1 再乘源 RGB |
| nearColor / farColor | `_NearColor` / `_FarColor` | 视距艺术色 |
| nearFarRange | `_NearFarRange.xy` | 配色距离，独立于剔除/密度距离 |
| depthColorStrength | `_DepthColorStrength` | Depth 分支加色量，候选 .25 |
| bottomColor / shadowColor | `_BottomColor` / `_ShadowColor` | 缺地色回退、视频手工阴影色 |
| heightBlend | `_HeightBlend` | UV1.w 逐簇根到尖的混合范围 |
| useTerrainColor | `_UseTerrainColor` | Shadow-or-Bottom 分支选择 |
| terrainColor.texture / worldXZ | `_TerrainColor` / `_TerrainRect` | 线性、未受光地表基础色；rect=(xMin,zMin,width,depth) |
| Root 核验单一 surfaceId | `_TerrainColorValid` | 0 或 1；纹理 Alpha 为该表面的有效区 |
| windTexture | `_WindTexture` | Repeat 平铺，顶点显式 LOD |
| windNoiseScale | `_WindNoiseScale.xy` | 每次重复覆盖的世界米数 |
| windNoiseSpeed | `_WindNoiseSpeed` | 每 16 秒固定滚动跨度；不是累积时间倍速 |
| windNoiseContrast | `_WindNoiseContrast.xy` | 粗风 Smoothstep 区间 |
| windTextureLod | `_WindTextureLod` | 顶点纹理采样 LOD |
| fineWindStrength / coarseWindStrength | `_FineWindStrength` / `_CoarseWindStrength` | 细摆／粗风形态权重 |
| windColorStrength | `_WindColorStrength` | 仅 CombinedColor 的高度颜色联动 |
| artDebug | `_ArtDebug` | 0 正常、1 高度、2 地色、3 主光阴影、4 视距、5 粗风 |
| 诊断专用，正常为 1 | `_WindDisplacement` | 设 0 仅停几何，保留颜色风，支持形色分离检查 |

地色无绑定、rect 无效、UV 越界或 Alpha 无效时回退 BottomColor；调试 2 用洋红标记无效，不能把白纹理悄悄当作成功烘焙。一个 Layer 对应一个 surfaceId 的约束由 Root 作者入口负责。

## 视频证据到 HLSL 的对应

读过批准包的实施计划、时间轴、验收单，并查看 05:45、10:45、15:45、17:05、17:15 原始截图。

| 可见节点/事实 | 本候选对应 |
| --- | --- |
| BaseColor 黑，Emission 接艺术颜色，原纹理 Alpha 保留 | Tutorial 直接输出艺术色；Alpha 沿用原裁剪 |
| View Vector Length → Smoothstep → OneMinus → Far/Near Lerp | `GrassNearFactor`、`GrassDepthColor`；不是减少实例数量 |
| Depth 分支可见 .25 | `_DepthColorStrength=.25`，可调，不宣称所有隐藏常量完全还原 |
| TerrainColor × ShadowColor 与 TerrainColor 由主光 ShadowAtten 混合 | `shadowTerrain`，仅 Tutorial 路线手工接阴影 |
| UseTerrainColor 选择 Shadow 分支或 BottomColor | `shadowOrBottom`；并不禁用根部 A 端的地色输入 |
| 最终 Lerp A=TerrainColor+Depth，B=Shadow-or-Bottom | `GrassVideoArt` |
| 粗风同时进入位移和 Add→Saturate→Multiply(Height) | `GrassCoarseNoise` 输出同一信号给 `GrassLinkedHeight` |

根到尖使用实际生成器写入 UV1.w，绝不使用整个合并网格的高度或 worldY。隐藏连线和未显示常量不能被截图证明；本候选的范围、默认颜色、世界尺度以及连续周期过渡是明确的项目适配。

Tutorial 路线不经过项目环境照明，因此它只是视频路线对照，不能代替最终深夜表现。Environment 路线先生成未受光艺术基础色，再且仅再调用一次现有 `EnvironmentShade`；不先乘手工阴影再把同一主光阴影乘第二遍。环境/局部灯/夜间能量继续归既有后端所有。本轮没有复制天气管理器或修改共享受光函数。

## 16 秒周期与风强度

粗风从稳定根部 worldXZ 取样，不随草尖变形坐标漂移。`p=frac(seconds/16)`，两次取样分别是 `N(uv-travel*p)` 与 `N(uv-travel*(p-1))`，权重为 `smoothstep(0,1,p)`。

当 p→0 时第一视图取 uv；当 p→1 时第二视图也取 uv。权重两端导数为 0，取样导数也在同一 uv 以同一 travel 相接，因而值与一阶导数连续；travel 不需要恰好是整数纹理周期。细风使用错相 Gradient Noise，也用同一双视图方式连续相接。

EnvironmentFrame 的时钟保持既有来源。`wind.xy` 的长度只控制实际幅度，风向由它归一化取得；不把会改变的天气强度乘进累积时间。新路线将形变向量长度限制为 1，水平最大位移不超过 `maximumBend × clamp(length(wind.xy),0,1) × weight²`，额外竖直下垂上限为水平位移的 .08 倍。生成器仍须为两轴摆幅和下垂扩展 Bounds；不能通过关闭剔除掩盖边缘消失。现有卡片法线未做解析弯曲，属于与旧路线一致的近似。

## 有限 GPU 合同入口

Root 在看到小片视觉成立后，可临时导入 `GrassVideoWindProbe.shader.txt` 至 `Assets/OSSASTRA/Scripts/WorldRendering/Vegetation/Rendering/GrassVideoWindProbe.shader`，然后通过 Native 执行 `ValidateVideoWind.cs.txt` 的 `ValidateVideoWind` 命令。

合同直接在顶点阶段 include 正式 `GrassWind.hlsl` 与 `GrassArtColor.hlsl`，使用私有 4×2 ARGBFloat 目标，一次读回几何与信号。固定 32×32 线性平铺噪声是可复现输入，不是重写变形算法。覆盖 Fine/Coarse/Combined/CombinedColor、16/32 秒边界两侧和有限差分速度、根行固定、零风、静态关键字、最大摆幅、转向与超量强度、有色无位移、显式纹理 LOD。每次 draw 的原始参数和输出写至固定 `artifacts/VEGETATION-01A/v12/` 下唯一 JSON。

本合同不改全局关键字、天气、相机、质量、场景或 Profile；finally 恢复 active RT、sRGBWrite，释放自有材质、RT、纹理和命令缓冲。`.14m` 是测试夹具，不冒称当前 Lab 实际 Profile 已认证。有限数学测试不等于草卡片 Alpha、真实阴影、Room 许可、性能或最终审美通过；旧 24 项 Probe 无需改写，可另行复测回退入口。

离线 `dotnet build VideoWindCompile.csproj` 已通过 C# 编译，0 warning / 0 error。子任务未运行 Unity 或 GPU；GPU 结果以 Root 实际执行写出的 JSON 为准。

## 冻结源码 SHA256

| 文件 | SHA256 |
| --- | --- |
| GrassLayer.shader.txt | `9F4D6D71A4F63BA8D082DE52CC70C71A2FE8020241D712ACFE005C3A9CA421F4` |
| GrassWind.hlsl.txt | `6118B21283272F1977CB0E103FCD681541CE7EAA350CFBD7C90883BCC421EF71` |
| GrassArtColor.hlsl.txt | `8ADE6BE76B7109F0AF166B35CBFAE96AF0C068443DD518A2821907611341D503` |
