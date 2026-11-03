# ENV-01 渲染参数契约

唯一绑定方是当前相机的 RoomPositivePass。计数必须使用 SetGlobalInteger；所有数组上传为固定上限长度并清零尾部。关闭环境时设置 `_EnvironmentLightingEnabled=0`，角色/地表无光照适配器严格返回原 RGB，alpha 和深度不受影响。五项逐表面元数据只声明HLSL uniform，不列入材质Properties：静态路径由MPB覆盖，独立路径由逐draw命令全局覆盖，防止材质默认值遮住全局。调用方须显式上传响应1、接影1、最低亮度0等默认值。

| 参数 | 类型 / 范围 | 含义 |
| --- | --- | --- |
| `_EnvironmentLightingEnabled` | float 0/1 | 环境RGB接管 |
| `_EnvironmentFullSurface` | 每Renderer float 0/1 | 后端已证明Full时仅绕过Partial裁剪/表现雾；不产生新权限 |
| `_EnvironmentSurfaceRole` | 每Renderer float | 0地面、1装饰、2角色/装备、-1信息或业务自发光 |
| `_EnvironmentResponseOverride` | 每Renderer float，默认1 | 无光照家族的响应倍率；0严格回原色 |
| `_EnvironmentReceiveShadow` | 每Renderer float，默认1 | 无光照家族主灯阴影开关 |
| `_EnvironmentMinimumOverride` | 每Renderer float，默认0 | 无光照家族最低受光亮度；角色仍保留全局可读性下限 |
| `_EnvironmentSkyAmbient` | float4 | 线性空间天顶底光RGB，已乘 AmbientIntensity 一次 |
| `_EnvironmentHorizonAmbient` | float4 | 线性空间赤道底光RGB，已乘 AmbientIntensity 一次 |
| `_EnvironmentGroundAmbient` | float4 | 线性空间地面底光RGB，已乘 AmbientIntensity 一次 |
| `_EnvironmentLightResponse` | float4 | 地面强度、装饰强度、角色强度、角色最低可读亮度；均0..1 |
| `_EnvironmentIndoorAmbientScale` | float 0..1 | 只衰减明确房间OBB内部片元的环境底光/GI |
| `_EnvironmentIndoorCount` | int 0..32 | 明确登记的房间体积数；禁止拿整栋建筑OBB代替 |
| `_EnvironmentIndoorMatrices[32]` | Matrix4x4 | 房间世界到本地变换 |
| `_EnvironmentIndoorHalfExtents[32]` | Vector4 | 房间本地半尺寸xyz；全量顺序与局部灯所属房间一致 |
| `_EnvironmentCloudParameters` | float4 | 覆盖、密度、直射遮光强度、世界尺度（米） |
| `_EnvironmentCloudMotion` | float4 | 世界速度x/z、连续Seconds相位、云启用0/1 |
| `_EnvironmentWetness` | float 0..1 | 只对非角色保守压暗最多8%，不改变alpha/碰撞 |
| `_EnvironmentLocalLightCount` | int 0..8 | 明确登记局部灯的有界底光补偿 |
| `_EnvironmentLocalLightPositions[8]` | Vector4 | xyz世界位置，w范围（米） |
| `_EnvironmentLocalLightColors[8]` | Vector4 | RGB线性色×灯强度；w=所属房间索引+1，0代表室外 |

`EnvironmentCloudDensity(float2 worldXZ)` 是天空与地表可共用的稳定噪声函数。不拥有时钟，不读取材质局部时间。云只削弱直射项，不削弱环境底光；无光照家族接 URP 的真实主灯方向、颜色与阴影图，禁止用地面贴片假装投影。

局部灯补偿按实际片元所属房间筛选，不穿到其他房间或室外。此项只补局部RGB，不声称实现额外灯阴影；原生 Lit/SimpleLit/Spine SpriteLit 保留其原生额外灯、阴影与BRDF，不重复套 EnvironmentShade。

原生 Lit/SimpleLit 的主灯云钩子位于原生 MixRealtimeAndBakedGI 之后，仅缩 mainLight.color；Spine 原生受光函数在 cookie 之后采用同一世界云场。原生家族不二次套响应倍率/最低亮度等无光照专用配置，Inspector 应按源材质家族说明适用范围。包升级时复核本地函数副本与对应 URP14 / Spine 源函数的差异。

历史退场片元不继续接收新的动态实时阴影；保持已有正向可见性静态历史策略。原生 Sprite CG 适配器为保留 Flip、PixelSnap、ExternalAlpha 和 PMA，使用本机 URP14 主灯阴影 ABI，不混入重复定义 Unity 内建全局变量的 Core 头。
