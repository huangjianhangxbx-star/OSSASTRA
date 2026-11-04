# 角色光感颜色与增益合同

本目录的 `.cs.txt` 是交给唯一 Unity writer 运行的 Native 夹具，不是生产菜单或自动入口。

| 合同 | 真实受测对象 | 能发现的错误 |
| --- | --- | --- |
| EnvironmentAmbientTintContract | 正式 SightWorldTile 的 role2 环境受光路径 | 天空桥未接、finalRGB重复染色、局部灯改色、室内/信息/禁用未排除、原夜间保底漂移 |
| CharacterGainGpuContract | 正式 SightSpine 与 SightDepthPlane，各自 Straight/PMA 输入 | 保护未接、普通段变暗、强光不收敛、alpha参与非线性曲线、非角色被修改、关闭不回原 |
| CharacterGainBoundaryContract | 正式 SightSpine 与8盏已登记点灯 | 只逐灯限幅却不限制总和、接收者关闭无效、alpha0加色附件误压、黑色原图发光 |
| CharacterLightingSettingsContract | 反射访问实际生产配置类型 | 缺失实现、非有限参数漏校验、上限不大于拐点、运行态副本与共享配置互相污染 |

基线回执由 root 保存；禁止为覆盖旧失败结果而重写回执。修改测试源码时另存新运行结果并标版本。

截至本目录建立时，前三项初始合同（环境色、增益、配置）已由 root 在旧生产上观察到功能 RED：环境色不变化、强光无软肩、配置类型缺失；夹具真值/原alpha等有效。边界合同属于后续专项，是否通过以 root 的真实回执为准。本文件不宣称 GPU GREEN。

生产数学以**线性光照增益**为运算对象，最终统一乘原图 RGB。它不直接对预乘 RGB 做非线性曲线，因此透明边缘与不透明区域获得相同增益映射。PMA混合、输入关键字、slot alpha、深度及许可仍由既有适配器负责。alpha0但RGB非零的加色业务附件走旧路径；轮廓覆盖不包含这些附件。

软肩限制的是合计光照增益的亮度，不是对所有 HDR 贴图的最终RGB逐通道限幅；本轮手绘角色为当前已选 LDR 素材。它保留纹理的深浅与光色比例，不等同于物理BRDF、全局tone mapper或Bloom。拐点1.2、渐近上限2和轮廓预算.35均为待主场对照的 SAMPLE 初值。

`ProfileCompile` 只用于两份配置 C# 的独立依赖编译；已得到0警告0错误。它不能证明实际 Shader、GPU、真实角色或主场集成正确。
