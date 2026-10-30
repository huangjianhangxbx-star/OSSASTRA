# KT-VIS-02：贴墙净空与柔边点阵消隐

已在原遮挡验证场完成本轮增量实现和模块验证。正式美术的身体包络及淡出观感仍由用户试玩调节；这不是所有场景/动画的发布级认证。

## 打开与试玩

场景保持 `Assets/OSSASTRA/GroundWorkshop/Samples/KT-VIS-01/SC_OcclusionVisibilityLab.unity`。菜单：`OSSASTRA > 场景可见性 > 打开独立遮挡验证场`。

Play 后右上角：

- **关闭效果**：分别用两角色贴墙、沿墙走、闪避并转动镜头；真正位于镜头前方的墙仍正常遮挡。
- **Original（只读）**：实际旧SO的只读对照副本。
- **Soft 柔边**：分离的核心、扩圈、整件时序及较宽羽化。
- **Soft + 轻点阵**：同样柔边，仅在空间羽化带增加稳定细点。

切换预设保留角色/相机位置、目标身份、当前圆窗历史及后端资源。修改候选SO后，在Inspector点“应用配置（保留机位与过渡）”；当前settings引用随预设切换，ActiveSettings是独立运行快照。

右下“轻挡/扩大/整件”是合法预置站位；随后沿原输入移动即可。三路线已按新身体预检，统一z=-11.2。两角色目标全部合法才一起调用原Motor.Teleport，复位失败不会只移动一人。按原Z切控后也可再次进入路线。

“展开诊断”可查看真实coverage/entry/local/radius/whole及延迟进度，或显示核心/外沿、opacity灰度。灰度为专用诊断深度，不能用于正常深度签收；折叠时自动关闭灰度。

## 配置位置与样板值

本场两个原角色仅增加场景覆盖组件 `CharacterEnvironmentClearance`；源Prefab、原受击CC未修改。

| 配置 | 当前SAMPLE |
|---|---:|
| 环境固定主体半径 / 高度 | 0.55m / 1.8m |
| 接触余量 / 单次初始恢复上限 | 0.015m / 0.55m |
| overlap / slide / 命中缓冲预算 | 4 / 3 / 64 |
| 明确原实墙 | 2个原侧壁Mesh、3个Box |
| 原CC半径 / 高度 / center | 0.22 / 1.5 / (0,0.75,0) |
| 原CC skin / step / slope | 0.02 / 0.2 / 45° |

身体参数是稳定世界米数，不随发丝、武器、隐藏分面、动画Bounds或镜头转动膨胀。单一环境约束不扩大受击、选取或交互范围。兼有顶面/侧面的Box仍须登记；专用顶盖与坡道由原CC负责，不从透视动态查询代理收集实墙。

三个配置在本场 `Presets/`：`KT_VIS02_Original_ReadOnly`、`KT_VIS02_Soft`、`KT_VIS02_SoftDitherEdge`。

| 参数 | Original实际值 | Soft / SoftDitherEdge |
|---|---:|---:|
| 核心进入 / 恢复 | 0.16 / 0.28s | 0.20 / 0.45s |
| 扩圈进入 / 恢复 | 0沿用旧核心速率 | 0.38 / 0.50s |
| 整件进入 / 恢复 / 延迟 | 0沿用 / 0沿用 / 0s | 0.60 / 0.50 / 0.25s |
| 最大半径倍率 | 2.5 | 1.5 |
| 身体留白 / 退出滞回 | 0.1 / 0.1s | 相同 |
| 羽化 | 身体屏幕高度×0.3 | max(身体高度×0.55, 当前内半径×0.45, 12px@1080) |
| 整件门槛start / end | 0.75 / 0.95 | 0.85 / 0.98 |
| 点阵强度 / 单元 | 0 | Soft=0；Dither=0.18 / 1px@1080 |
| 检测 / 目标 / 活跃组预算 | 20Hz / 2 / 4 | 相同 |

原 `VisibilitySettings.asset` 保留，SHA256：`DCFE00924C96F4798CE0B096A76792FB0D463A98CD1E7D6F747F0D1CC88FAFC5`。Baseline Inspector只读；候选SO可编辑。

## 架构与边界

- `CharacterRoot` 只负责可选环境约束的生命周期。`CharacterMovement` 合成普通/闪避/受迫位移后约束，仍只有一次 `CharacterController.Move`；环境侧阻与原CC.Sides共同结束原受迫运动。
- 数学Capsule放在自有私有PhysicsScene，已激活原生形状、不模拟、不进入主世界查询、无Renderer/Rigidbody。初始重叠仅在原水平移动请求时有界恢复，静止旋转不推出Root。
- 任意深传送、开放Mesh背面或无MTD的重叠不承诺通用纠正。检测到未解重叠/超预算保守拒绝水平推进并报告状态；完全越过开放面且查询无命中时不能凭空推断实体内部。返场/地图流式替换需显式提供合法站位/更新实墙引用。
- 原有限组颜色+原深度捕获、逐像素远近排序、双窗max并集、脚下支撑保护保留。点阵不增加RT，不改角色/武器alpha，不用时间随机种子或TAA。
- local/radius/whole各自推进，不重启Tween。点阵只在合并后的空间羽化带；核心/外围无点，整件partial不铺满模型。最低opacity从帧数据通过现有uniform.w传递，在共享颜色/深度逻辑末端统一约束。

[实施映射](implementation-plan.md) · [验证与已知边界](VALIDATION.md)
