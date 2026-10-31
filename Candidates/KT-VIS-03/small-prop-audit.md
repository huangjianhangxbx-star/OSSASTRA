# KT-VIS-03 小型物件透视策略只读审查

2026-11-01。只读生产/既有候选与文档；未操作 Unity、Git 或生产源码；未把静态发现写成 Native 复现结论。唯一写入为本文件。

## 已确认的调用链

- `Candidates/WORLD-PROP-01/Install.cs.txt:55`（ResumeInstall 同处56）在每次 `Add()` 无条件创建 `SceneOccluderGroup`，`allowWholeFade=true`，`minimumOpacity` 沿默认0。17个石头、植物、不可破坏对照等均进入原 `SceneVisibilityRoot.groups`。
- `Candidates/CRAFT-01/BuildSample.cs.txt:23` 对二维、三维、unsupported 三个台都创建同样的组，`allowWholeFade=true`、`minimumOpacity=.15`，59行统一追加到根。没有“保留正常遮挡”作者策略。
- `Assets/OSSASTRA/Scripts/SceneVisibility/Runtime/SceneVisibilityRoot.cs:257–261` 只检查组启用/目标可见，再以 `VisibilityProbe.Evaluate` 的任何遮身参考点命中生成局部开窗需求。`allowWholeFade` 只进 `VisibilityDemand.AllowWholeFade`，不禁止局部开窗。
- 同文件175–196行只以组件 `isActiveAndEnabled` 控制绘制资格；局部强度仍来自 `state.LocalStrength`。即使关闭 whole fade，局部窗仍能把小物件的一部分挖掉。
- `WorldPropRoot.cs:82–83` 的 `SetDepleted(false)` 会再次 `visibilityGroup.enabled=true`。仅把小物件 Group 组件在 Inspector 勾掉，不能可靠表达正常遮挡政策：活物初始化/恢复可能复开。
- `minimumOpacity=1` 在现有 CPU/GPU 路径能作为诊断消融保持不透明，但现 Root 的 score 仍来自 LocalStrength，因此它仍可能占有限捕获/排序槽。它是可用诊断变量，不宜作为整项政策的唯一实现。

## 推荐的最小政策边界（待 Root 决定/实施）

在 `SceneOccluderGroup` 增加显式可见性政策，例如“角色透视 / 保留正常遮挡”；由组作者指定，不能仅依高度、名字、是否可破坏或 Renderer 类型猜测。新枚举默认值需保持已有大墙/高台行为，当前小型资源物与工作台通过 Native 明确配置“保留正常遮挡”；以后样板生成器/Prefab 同步此值，避免重建复发。

根至少在检测和渲染/有限槽准入两处消费该政策。政策切成正常遮挡后，要立即归还原渲染；只发送 `Eligible=false` 不足，因为 `VisibilityState` 有 `exitGraceSeconds` 和恢复过渡，残留局部/整组状态可继续淡出。可对本组 reset，且渲染门直接 bypass。不要停用物理、WorldProp、碰撞/Health、角色深度或整个 SceneVisibilityRoot。

不要一律禁止全部 WorldProp 淡出；未来大型树木/建筑仍可能确实需要透视。不要把所有台、石块的 minimumOpacity 全局硬编码成一个数，也不要改全局 Settings 来掩盖每组政策。

## 能捕获本缺陷的反馈环

Root 需要在安全 CRAFT 新场 Play 中通过实际 SceneVisibilityRoot 正式相机路径建立 RED；本子代理没有 Unity 权限，未运行 RED。

1. 定位 `stone-a` 或 `stone-b`，把当前角色以原 Motor 明确位置夹具置于其后方，稳定若干正式相机帧，让其 QueryVolumes 命中身体下部；记录实际五点投影、同层地面、Camera pose 与组身份。切控伙伴以及相机旋转复用同一坐标合同。
2. 从根私有 `runtime`/`frames` 只读取得对应组的实际 Demand、State、Circle/WholeFade/MinimumOpacity 与捕获 StableId。旧代码若 `LocalStrength>0` 且该小物件进入捕获，便是此政策的真实 RED；独立截图确认用户看到的小物件缺口。不要把“缺少新字段导致反射失败”当行为 RED。
3. 修复后同轨迹断言：小物件原 Renderer/材质、碰撞、Health 不变；该组不产生有效局部/整组 reveal、不占捕获槽，正常前后遮挡保留；移动离开/切控/相机转向也一样。
4. 对明确登记的大墙/遮身障碍作阳性对照：同一原机制仍产生局部 reveal；主控站高台的支撑保护仍有效。只测小物件不透明会让“禁用全部透视”假通过。
5. 运行中从 reveal 切到正常遮挡应立即保留原物体，不等 grace/restore；再启用 reveal 重新测当前身体/相机，不借旧需求。坏配置/停用/销毁/跨场归还原渲染状态。

政策纯值测试可补充，但不能替代实际 Root 筛选/捕获的回归。独立计算 `VisibilityDemand(false,...)` 的 State 测试太浅，旧代码“从未正确路由正常遮挡政策”仍会被放过。

## 现有可复用测试与配置入口

- `Candidates/KT-VIS-01/state/VisibilityContracts.cs.txt`：纯过渡/opacity。
- `Candidates/KT-VIS-01/sprite-contracts/SpriteGroupContracts.cs.txt`：私有 PreviewScene、实际 SpriteRenderer、源图独立参照的有限捕获颜色/深度验证；不是 clone 自比。
- `Candidates/KT-VIS-02/transition/FloorContracts.cs.txt`：生产 CPU opacity floor 真实回归。
- `Candidates/KT-VIS-02/transition/GpuStyleContracts.cs.txt`：真实 GPU 同判，可用于原有最低 opacity/点阵路径的定向保守回归。
- `Candidates/KT-VIS-01/ReadVisibilityRuntime.cs.txt`：只读根/目标/组资源模板，原脚本只允许旧 Lab，Root 应按本轮安全场与 GUID 改成新候选，不能直接放宽旧入口。
- 当前场：`Assets/OSSASTRA/GroundWorkshop/Samples/CRAFT-01/SC_CraftingIntegration.unity`。当前问题的实际序列化组/物件身份仍以 Root Native 读回为准，不能仅从本机 YAML 当权威。
- 原资源模板：`Assets/OSSASTRA/WorldProps/Prefabs/WorldProp_2D_Template.prefab`、`WorldProp_3D_Template.prefab`；装备台：`Assets/OSSASTRA/Crafting/Prefabs/Station2D.prefab`、`Station3D.prefab`。如改样板，只能 Native 编辑相应政策，不能 shell 补 YAML。

模型本体“消失一半”的首因仍需 Root 做对照：保持相机/模型/姿态不变，只改变某小组的 reveal 资格或 effectEnabled，查缺口是否随有限捕获消失。若关闭透视仍有截断，则应独立测试地面深度/直立深度与原网格范围；不能因为两问题出现在同图就写成同一根因。

## 后续生产租约与实现（Root 已取得行为 RED 后授权）

Root 已实际观测石头组 LocalStrength=1/CaptureCount=1，并以独立 source-only/ground/full on-off 像素对照确认2129个石头像素随 effect 改变、source 与 ground 无丢失。本子代理随后取得恰好两文件租约，并在 Root 确认 Edit ready 后修改：

- `SceneOccluderGroup.cs`：公开 `SceneOcclusionPolicy`，`CharacterReveal=0` 保留旧墙/平台、`KeepNaturalOcclusion=1` 为正常遮挡；字段 `occlusionPolicy` 和 getter `AllowsCharacterReveal`，中文枚举、作者提示。
- `SceneVisibilityRoot.cs`：Detect 与 render/slot 准入均检查 getter；`RefreshRevealPolicies()` 追踪每组有效许可（含组件启停）变化，立即清空原 State/Demand/Circle 并把 nextDetection 置0。正常遮挡不等待原恢复滞回；再许可必须重新检测。

没有改材质alpha、Collider、Health、角色/时间、保存或组预算上限；没有操控 Unity/Git。Native 编译/最终 GREEN 仍由 Root 独立执行，不由本次静态实现代签。

## 白色帽状物是否掉落图标：第二条独立静态线索

`Assets/OSSASTRA/Scripts/UI/InventoryWorldView.cs:64–65` 创建实际松散物显示 `GroundItemSprite_<itemId>`：原默认 SpriteRenderer 材质、sortingOrder40、固定 localScale(.5,.5,.5)，随后每次 LateUpdate 将图标放在会话 ContainerPosition+worldUp*.15，并使用 **完整 Camera.main.rotation**。它不登记 SceneOccluderGroup，也没有透视政策；不能用小物件 reveal RED 解释它的所有缺口。

此处确有“中心pivot＋任意高宽图标＋固定偏移”可穿地的一般漏洞：应读取实际 sprite.bounds/vertices、lossyScale、billboard up/forward 和真实脚点支撑高度。仅提高一个魔数会掩盖比例/父级变化，Sprite sortingOrder 也不能绕过地面深度。不能无条件给掉落加角色身体 shader，那会把物品显示身份与角色域耦合。

但当前静态原装素材本身尚不能证明白帽首因：磁盘 `DS_StoneDrop.png` 为69×44、alpha bbox(4,3)-(67,42)，PPU100/中心pivot；假设Native仍是此sprite和scale.5，在35°俯角时不透明下缘约在脚点+.07米，并未跨地。`DS_FibreDrop.png` 78×60、alpha bbox(3,3)-(76,58)，同假设也约+.035米。反之完整 `DS_Stone2D.png`161×128 作为图标、同scale，则下缘可约-.10米；父级放大也会改变结论。素材GUID文本在本机meta为团结编码格式，不用原始YAML引用推断真实绑定。

因此 Root 应先只读所有实际 `GroundItemSprite_*`，返回 sprite.name、texture尺寸、pivot/PPU/bounds、transform.localScale/lossyScale、真实Renderer.bounds.min.y、Ledger容器点/层高以及实际材质/Shader，确认白帽确为哪个实例。随后沿当前正式相机做实际 source-only（禁掉其他 Renderer）对 source+actual-ground 的像素差分；只在“源完整、有地面后同一图标缺失”的 Native RED 上实施偏移/承托修复。需覆盖 stone/fibre以及一个明显高/非中心pivot/父级scale的原创fixture，避免只凭几何bounds或升Y就冒称用户图像修复。
