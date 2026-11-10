# ToonSurface01 v1.3 定向只读复核

日期：2026-11-11。基线：`HEAD a2d397a16da05b716805e2a50612f6d632e7c0c9`。范围：当前 5 个共享 C# 文件的工作区 diff，以及本轮 6 个 Rendering Shader/HLSL 新文件；对照其旧 Voxel 实现。没有改动生产文件、操作 Unity、增加测试或干预构建。

**结论：在当前已登记的 7 对象、17 材质槽范围内，未发现需要阻断本次构建的新增实质问题。** 这是代码审查结论，不替代当前构建后的 LiveAdapterAudit、Player 和视觉验收。

## Standards：边界与生命周期

- `LookDevFiniteToon.cs:44` 的运行材质键同时包含作者模板与原材质身份。不同木色不因共用一个 timber 模板而合并；同一对则复用。克隆数量有 24 上限，生成发生在显式切档，不在 Update/OnGUI 每帧复制材质。
- `LookDevFiniteToon.cs:224` 先检查全部目标与重复槽，再准备全部新增克隆；准备失败仅销毁本次增加的资源，不先写半套 Renderer 引用。分项操作在 `:161` 也先检查所有当前槽，之后只写私有克隆。
- `LookDevFiniteToon.cs:335` 的归还先比较当前槽引用，只恢复仍为本组件已知候选的槽，再清理私有克隆。`OnDisable`/`OnDestroy` 复用幂等清理。既有 Room 材质桶在下一次准备中按所需源集合裁掉旧桶；没有引入无限累积的第二套缓存。
- `SurfaceLightingRecipe.cs:144` 的新比较入口复用既有租约、环境根、运行 Profile 和 A/B/C 入口；旧枚举值及旧状态默认规则未改变。新比较状态只修改新运行副本；Release 保留原有 compare-before-restore。
- 没有新增管理器、天气来源、相机或 Room 调度位，也没有在本轮修改角色、草、旧 Voxel Shader 或共享后端的所有权策略。

## Spec：原作者保色与真实 Room 分支

- `LookDevFiniteToon.cs:138` 将原 BaseMap、各纹理 scale/offset、BaseColor/alpha、必要法线/细节/遮蔽/自发光输入与适用关键词复制到运行材质。原 URP Lit 没有体素顶点色合同，显式置 `_VoxelVertexColors=0`；原 Voxel 则保留其原值。Toon 三色系数及特性默认仍来自四个作者模板。
- `RoomPositiveRenderingLayers.cs:24,192` 精确识别独立 Shader，拒绝透明/裁切/非 ZWrite1 的该家族；共享 PartialLit 调度位没有等同为 GenericLit 着色。
- `RoomPositivePass.cs:392` 在 GenericLit 前解析 `positiveToonSurface01Shader`，同时覆盖 PartialLit 与 Roof。字段缺失会被现有准备阶段拒绝，未添加悄悄退回旧 Lit 的分支。
- 现有 `RoomPositiveMaterialBuckets.Refresh` 仍检查源及自有副本内容 CRC。Ramp/Spec/Rim 写入运行源后，对应颜色 Adapter 可以更新；Caster 克隆仍用源 Shader。新源与 Sight 的属性布局、ForwardLit/ShadowCaster/DepthOnly/DepthNormals 顺序一致。
- `ToonSurface01Forward.hlsl:252,280` 保留旧 Room 合法覆盖、屋顶裁切、历史静态显示与黑幕混合。普通与 Sight 只差同一 Forward 实现的 Room 开关，没有独立重写颜色算法。
- `ToonSurface01Lighting.hlsl:285` 仅替换主光表达；GI、额外点/方向光、室内因子、云场、阴影与高亮保护沿用原路径。Math 中真实 shadowAttenuation 乘一次；ShadowColor 是半影色度，完全遮挡没有伪造太阳能量。Spec/Rim 均受实际主光颜色、距离、阴影和受光侧权重约束。
- 新 Toon 参数均无条件放入同一 UnityPerMaterial 布局；各 Pass 共用 Input。没有因特性启停产生不同的材质常量布局。

## 已有边界，未升级为当前缺陷

- 运行克隆是本次有限目标的私有资源，不是可借给其他 Renderer 的公共材质库。当前实现不承诺在运行中重配 Bindings 后仍归还已移出登记表的旧目标。
- 当前候选是已认证的不透明有限材质。运行源保留表没有宣称复制任意未来材质的全部渲染状态（例如未来双面源的 Cull）；今后扩大作者输入范围时应同时审查状态及 Player 关键词保留，不能凭本轮 17 槽验收宣布任意材质通用。
- 四个作者模板不能证明所有未来 normal/detail 关键词组合都已纳入 Player。当前现场输入和本轮保留集合之外的组合仍需单独登记。
- 高级面板在有 ToonSurface01 时实际高度为 540，而不是旧 430。新增控件预算与正常 1600×900 视口相容；本次没有发现必须先打 UI 修补才能构建的问题。长错误文本或更小视口不在此静态审查中作实际无遮挡保证。

## 证据与当前状态

读取了现存 setup-before/setup-complete 记录：17 槽、7 对象、四个作者模板、候选 Renderer 索引 18、旧默认 Renderer 0、Main 未保存。Root 已报告的 Native Integration 17 项及 Shader Math/GPU 19 项通过作为既有证据记录，没有在本次复跑。构建后的 LiveAdapterAudit 尚待 Root 实际执行；当前 Shader 编译中的构建不因本报告被认定成功。

先前被取消的保存提示回执不能作为本次实际构建耗时。现存 20261110 Player 图片/JSON 只能按其原时间和构建身份引用，不能冒充正在进行的 20261111 构建结果。Root 已更新本地 Build 脚本的输出日期和 Main 安全复制；本次未修改该脚本。
