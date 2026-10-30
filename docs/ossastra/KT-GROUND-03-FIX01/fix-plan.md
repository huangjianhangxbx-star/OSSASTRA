# KT-GROUND-03-FIX01：地表闪烁原因与修复方案

当前已经确认本轮问题来自**多层地表共面后，原深度处理不能稳定表示层序**：应当显示上层的位置出现下层颜色，相机微移后条带变化。真实单因素对照支持这一原因；偏移方向正确，改变材质偏移确实影响结果，MPB未应用不是当前首因。最终采用C：同一平台平面、同一像素采用统一的片元深度重建，额外层偏移为0，沿用原绘制队列和LEqual。生产Shader已窄修并取得A/B四组Bounded显示Green，四组独立opaque绝对遮挡也已通过。正常Play经原Orbit从45°转到315°，实际截图地表/角色/墙已目检正常并返回Edit；本轮定向修复与保护已收口，功能提交79f61936a9429bf375d0544b45147262663ee60b已GitHub main精确核验，全部租约释放。

## 1. 已确认的问题

用户在已交付的新高台场景看到地表条带与闪烁。本轮读取实际场景后，在独立Preview中复用高台A真实缓存层0、层1的Mesh、完整变换、原Shader及材质/MPB有效内容，只复制测试需要的对象。没有在源场切换角色、改TimeScale、重建或保存来制造现象。

每个机位分别渲染“只显示下层”“只显示上层”“两层同时显示”。从前两张图中选出两层都不透明、颜色足以区分的相交区域，再检查合成图应当呈现的上层。对照只统计稳定内部样本，不将alpha边缘混成叠层结论；边缘画面需要另验。

真实RED回执为 `LayerOrderA01_016f2c1e50d643c2bef30142701195cd/layer-order.json`：

| 机位 | 有效样本 | 错误显示下层 | 无法归类 |
|---|---:|---:|---:|
| x偏移−0.001m | 12,666 | 736 | 0 |
| 原测试位置 | 12,674 | 2,140 | 0 |
| x偏移＋0.001m | 12,659 | 2,740 | 0 |
| 合计 | 37,999 | 5,616 | 0 |

这是**3个机位，每机位3次渲染，共9次渲染**，不是9个机位。测试0.5960秒，每机位样本数均满足预设最低128。样本采用5×5内部邻域（内缩半径2）、通道对比度20及匹配容差2。上述计数只代表可判定相交样本，不是全画面的错误比例。

条件为384×256、yaw315°、pitch35°、distance25、FOV35°、near0.05/far180，Preview测试未启用MSAA/HDR。第一轮只登记RT请求nominal24。后续实际配置读回为 **D32_SFloat_S8_UInt、32位浮点深度**；显式请求D32仍复现相同5616错误。这组基线并非D24，不将nominal24写成真实D24；后续真正D24的生产覆盖另列。

## 2. 当前场景事实与现场保护

`current-state.json`记录：

- 高台A、B的`flattenDisplay=true`；各外观层的实际缓存顶点分别统一在世界Y=3、Y=5。
- 两平台原生TilemapRenderer仍保留，但均`forceRenderingOff=true`，有效原生显示数为0。
- 层0/1的渲染队列为2450/2451；材质及MPB读回DepthUnits为0/−1、DepthFactor为0。

这些是读回事实，**不能单凭Inspector数值证明固定渲染状态已按预期作用于GPU**，也不能单凭共面就确定首因。最小Preview只含两层地表、不含角色组件；此最小复现不需要角色动画或双原生路径参与，但仍不扩大为整场所有闪烁只有同一个原因。

首次现场快照为dirty=true，RED开始及结束时为dirty=false；期间可能发生了用户保存，现有证据不能确定其原因。本轮测试没有保存或回滚源场，不将这次变化据为自己的修复或恢复动作。

## 3. 为什么上一轮验收漏掉它

上一轮计划V22原本要求八方向及连续旋转时近看边沿/身体不出现新闪烁，接口计划也要求层深度与遮挡验证。因此这是需要补齐的验收覆盖，不是后来才增加的质量要求。

| 上一轮证据 | 有效结论 | 对本问题的缺口 |
|---|---|---|
| 55点墙沿接合 | 墙、盖、Collider几何位置吻合 | 未测不同地表层谁应当出现在画面上 |
| Native/缓存平面对照 | 指定输入与机位下两路径外观接近 | 两路径可能共同显示错误；没有独立的上层预期 |
| 八角静帧 | 八个落定机位可见 | 未判定档间连续相机变化的错误层翻转 |
| 缓存稳定44项 | 不重建、对象与资源数量稳定 | 不代表输出像素顺序正确 |
| 显隐、Mask状态合同 | 状态同步并恢复 | 不代表多层合成正确 |
| groundOnly静态图 | 场地结构与接合 | 不替代正常运行的角色/地表组合画面 |

已有40秒录像可以保留为运行记录，但没有针对问题区域的独立判据，不能仅凭“录过视频”签署动态稳定。上轮严格像素11/12和GPU/实体UI未验边界继续保留。

## 4. 原因对照与排除项

相同3个机位、相同单层oracle与opaque内部判据下，root仅修改一个待查变量，得到以下实际结果。原始回执均在本机FIX01的`01-evidence/*-summary.json`，各summary继续链接完整render回执。

| 对照 | 有效样本 | 错误下层 | 无法归类 | 结论范围 |
|---|---:|---:|---:|---|
| 原参数重跑 | 37,999 | 5,616 | 0 | 原问题重复出现 |
| 原负向Units幅度×16 | 37,999 | 1,908 | 0 | 错误减少但未修好 |
| 原负向Units幅度×64 | 37,999 | 651 | 0 | 错误继续减少但未修好 |
| 原负向Units幅度×256 | 37,997 | 0 | 0 | 此两层/三机位局部Green |
| 反向正Units幅度×16 | 37,998 | 33,276 | 0 | 反向明显恶化，原方向正确 |
| 仅Material改为原负向×64 | 37,999 | 651 | 0 | 与同时改Material/MPB一致，材质状态确实影响显示 |
| 清除MPB中的深度参数，保持原Material | 37,999 | 5,616 | 0 | MPB深度项未应用不是当前首因 |
| 显式D32，保持原偏移 | 37,999 | 5,616 | 0 | 深度格式不是从“实际D24”升D32就能解决；本机原请求已经是D32 |
| 显示层间高度步进0.0005m | 37,980 | 0 | 0 | 分离显示几何也能消除本最小例的错误，仍需裸缝/遮挡/多层验证 |

这组对照支持：**共面后的原偏移裕量不足以稳定表达上层优先**。H1得到支持；H2的“符号反了”被反向恶化对照排除；H3的“没有写MPB所以偏移完全无效”不能解释Material-only对照，排除为当前首因。

仍需区分证据层级：以上是实际像素与受控变量的因果证据，尚未通过GPU抓帧直接读取最终rasterizer state。也不能由此宣布每一种平台、每一个三角形或所有相机距离都已稳定。

### 官方机制与本机结论分开

Unity说明Offset改变GPU深度偏移，units的效果依设备，负值使显示更靠近相机；Unity会处理深度偏移与其深度方向的匹配，不应因为D3D reversed-Z再自行翻一次号。[Unity 2022.3 Offset](https://docs.unity3d.com/2022.3/Documentation/Manual/SL-Offset.html)、[图形API与深度方向](https://docs.unity3d.com/2022.3/Documentation/Manual/SL-PlatformDifferences.html)

Microsoft说明，D3D11的浮点深度偏移尺度依赖该primitive最大深度的指数；偏移对单个primitive为常量，在裁剪后、插值前应用。因此固定Units不是固定米数，也不能仅凭两个共面网格就假定所有三角形获得相同世界尺度的分离。这里的“不同三角形/距离可能需要不同裕量”是依据该机制对本方案风险的推断；文档不把官方规则冒称本项目GPU状态抓帧。[Microsoft D3D11 Depth Bias](https://learn.microsoft.com/en-us/windows/win32/direct3d11/d3d10-graphics-programming-guide-output-merger-stage-depth-bias)

## 5. 三种修复候选

256倍和0.5mm都只是原最小夹具的局部Green，均未被选作最终常数。最终采用C的零层偏移版本；0.0005m作为对照，不作为默认。Bounded机位矩阵在创建的D24/D32 RT下已取得生产Green；标准opaque绝对遮挡在D24/D32、MSAA1/4的明确几何下已通过；正常Play旋转/返回与最终保护也已完成；未测边界仍保留。

| 候选 | 如何处理 | 优点 | 必须解决的代价/风险 | 当前证据 |
|---|---|---|---|---|
| A：增大固定Units偏移 | 保留物理与显示几何共面，增加层间硬件深度偏移 | 改动小，沿用当前路径；不会直接把显示顶点抬高 | Units不是米制且受primitive/深度条件影响；大偏移可能破坏角色或侧壁遮挡，256不是官方推荐值或跨平台保证 | ×256在原两层/三机位0错；更多条件待验 |
| B：微量分离显示几何 | 仅显示层按有界高度步进，Collider仍留真实封口 | 参数有世界单位，最小例易理解与复现 | 多层累加可能产生边沿夹层、低角可见浮起，显示/物理分离还须检查角色脚部；不能把0.5mm局部通过当任意尺度通用值 | 0.0005m步进在原最小例0错；裸缝/多层/遮挡待验 |
| C：统一片元平面深度（最终采用） | 相同平台平面/相同像素从同一路径重建深度；额外层偏移0，沿用原队列及LEqual，不把几何抬离墙沿 | 避免各primitive独立深度计算/偏移尺度差异；保留共面几何，不靠累加层高保持顺序 | Shader与深度转换更复杂；须验证reversed-Z、GPU投影、全部路径、alpha/墙/角色/MSAA及深度优化代价；米制微偏移仅为对照或待证备选 | 生产原Shader、无override，A/B各28个Bounded机位×两种实际创建的RT格式，累计2207934有效样本0错；绝对遮挡4组已通过；正常Play45°→315°及返回已实测；不扩大为所有运行情形 |

### 候选C的当前原型证据

`unified-plane-zero-step-summary.json`首次没有取得渲染结果：ShaderOverrideName查找临时Shader失败，0样本，属于harness ERROR，不是“方案0错”。root修为正确查找TransientShaderId后，`unified-zero-id-summary.json`真实GREEN：37,996有效样本、wrong0、unknown0，原型prototypeStep=0，三个机位均有效。

原多层A的TwoViews基线 `multilayer-original-A-summary.json` 72,009样本、19,093错误、unknown0，说明问题超出单纯两层小样。随后C的TwoViews受限集合已取得A 72,005样本0错、B 97,281样本0错的summary；这些是该集合的原型结果。后续生产Bounded与格式验证另列，MSAA及标准opaque绝对遮挡由后述独立4组实测支撑，不能由TwoViews代签。

最终选择是**先让本应相同的平面深度以相同方式计算，再由原有层队列表达先后**，不先增大偏移或制造层高。只有扩验显示确有必要并且遮挡/性能通过时，才讨论有界米制微偏移；不将0.0005m写成默认。

候选C若使用`SV_Depth`，所有有效执行路径必须给出正确深度；其对MSAA及优化的影响不能忽略。Unity明确提示这种输出在不少GPU上会关闭部分深度优化，成本依架构；Microsoft也列出多重采样与未写完整路径的限制。因此本轮需实测本机代价并保留GPU/Player未测边界。[Unity Shader semantics](https://docs.unity3d.com/2022.3/Documentation/Manual/SL-ShaderSemantics.html)、[Microsoft SV_Depth限制](https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/dx-graphics-hlsl-semantics)

## 6. 最终生产改动与选择范围

最终只修改`Assets/OSSASTRA/Scripts/GroundSurfaces/Shaders/GroundThinTop.shader`，保留原Shader名称与GUID；原DepthUnits/Factor字段保留为隐藏deprecated兼容字段，实际Offset为0。片元深度从同pixel水平平台平面重建，生产没有ViewDepthOffset或几何抬高。

本次不改作者格、Mesh内容或材质绑定，已有资源仍引用同一Shader；无需重建缓存或重序列化材质，因此不改cache签名/生成版本。后续编辑闭环验证检查现有路径继续可用，不以重建或重存资产作为本修复的生效前提。

选择C的理由是：它让本应一致的平面深度先一致，再保留原有层序，避免把更大的Units或累加层高变成默认依赖。采用C遵守下列约束；绝对遮挡已有独立实测，Play旋转/返回与最终保护现已取得实际回执；未测范围继续如实保留：

- Tilemap仍是唯一作者数据；草、路、覆盖的配置和原图保持。
- 物理Collider与侧壁接合不被渲染修复移动；不增加角色Move或每帧强设Y。
- 明确图层的可见顺序；透明部分仍能正确露出下面地表。
- 不使用永久顶层、`ZTest Always`，不牺牲角色脚部/侧壁遮挡，不用可见浮层换取不闪。
- 旧低地、旧工坊和未启用本适配的场景保持原路径；不改全局渲染管线或Packages。
- 现有作者更新、保存重开与重载路径继续正确；本次不变更缓存输入语义/布局，不为Shader修复强制重建或更换资源身份。

`prepare-edit-ready.json`中operation `f4a38be829a547b08657a0c1128b2f30`为ready，CS compilation_errors为空。这证明准备/编译就绪，不代替后面的像素与运行遮挡验收。

### 生产原Shader的Bounded四组结果

下表均`shaderOverrideName=""`、`shaderOverrideId=0`，不是测试Shader替换。每组28个有界机位，使用Single/Prefix以及移除上层后的最高存活下层oracle；empty frame独立验证clear alpha，所有机位有效。

| 本机summary | 真实创建的RT深度格式 | 有效样本 | 错误下层 | 未归类 |
|---|---|---:|---:|---:|
| production-A-D32 | D32_SFloat_S8_UInt | 463,725 | 0 | 0 |
| production-B-D32 | D32_SFloat_S8_UInt | 640,242 | 0 | 0 |
| production-A-D24 | D24_UNorm_S8_UInt | 463,725 | 0 | 0 |
| production-B-D24 | D24_UNorm_S8_UInt | 640,242 | 0 | 0 |
| 四组累计 | 两格式重复采样，非独立世界点计数 | 2,207,934 | 0 | 0 |

A活跃层[0,1,2,6]、被检上层[1,2,6]；B活跃层[0,1,2,4,6]、被检上层[1,2,4,6]。当前没有内容的层不借此宣称已经像素覆盖。

早先`bounded-unified-*-D24`只传入格式选项却遗漏实际应用，raw创建的仍是D32，不能作D24证据。修夹具并强制检查后，`exact-D24-unified-A/B`及上述生产D24两组的raw均明确为D24_UNorm_S8_UInt。这里确认的是创建的RT格式；URP内部depth attachment尚未由GPU抓帧直接捕获。Bounded是有界预设集合，不是所有相机参数的笛卡尔全覆盖，也不替代后述独立opaque绝对遮挡/正常Play。

### 独立opaque绝对遮挡与MSAA

`occlusion-prod-{D32,D24}-AA{1,4}-result.json`四组均GREEN，每组46机位（23透视＋23正交），包括pitch12°/35°/70°、distance5/25/32及八角方向。使用独立标准opaque参考Shader，对已知前/后位置的立卡与水平地面作实际遮挡对照，地表仅替换为生产Shader、白纹理/蓝色，不依赖另一个同样错误的地表Shader作oracle。

| 创建RT格式 | 实际MSAA | 内部样本 | 前卡重叠样本 | 后卡重叠样本 | interior/front/behind错误 | 全图严格差 |
|---|---:|---:|---:|---:|---|---:|
| D32浮点 | 1 | 1,055,285 | 24,940 | 19,937 | 0 / 0 / 0 | 0 |
| D32浮点 | 4 | 1,038,576 | 23,507 | 18,457 | 0 / 0 / 0 | 0 |
| D24 UNorm | 1 | 1,055,285 | 24,940 | 19,937 | 0 / 0 / 0 | 0 |
| D24 UNorm | 4 | 1,038,576 | 23,507 | 18,457 | 0 / 0 / 0 | 0 |

合计184组机位采样、4,187,722个内部样本；所有机位有效，每个机位的前/后卡重叠均至少64样本，严格全图mismatch之和各组也为0。格式与MSAA均从创建后的RT读回。该MSAA通过只覆盖此明确opaque几何，不扩成所有草叶alpha边缘或所有URP内部附件/平台的证明。

### Shader-only定向回归与现场策略

本修复没有改变作者、Mesh生成、材质身份、保存或更新路径。后续采用与改动相称的定向回归：真实生产Shader无override渲染、独立遮挡、现有Play及镜头旋转、作者hash/源资产指纹和最终运行引用检查。**不主动刷改用户作者数据，不保存、关场重开或重序列化用户场景**来凑验收步骤；旧KT03数目不直接冒作本轮通过。

生产Shader检查errors=false、messages=[]，GUID保持`54b11adfa486615458f1c071053ec790`；root只读比较三个Map authorHash与初始相同。`prepare-play-ready.json` operation `860fc0429da74083b04c8adad87b820f`实际ready。`runtime-capture-01.json`是真实Play Game View（1600×900输出1280×720），初始45°画面已由root目检普通地面/角色正常，单帧不签整个动态运行过程；随后调用原Orbit的RequestStep(-1)两次，正常Update从45°过渡至315°；`runtime-capture-315.json`实际画面由root核对地表/角色/墙正常。该正常状态请求不直接改相机Transform。

### 最终回归、清理与剩余限制

`runtime-verification.json`读回生产Shader supported=true/errors=false/messages=[]、三Map authorHash与初始相同。退出Play的prepare operation `f22bad2e…`实际ready；`final-scene-info.json`为Edit、仅1个Lab、**isDirty=true**。Play生命周期自动生成缓存，A rebuildCount91→92、B55→56；因此不说“没有发生重建”，也不把它擅自记成相机旋转引起的重建。本轮没有手动Rebuild、保存或清dirty。

完整final-verification请求因C盘空间不足发生IO112；改用最小Native FinalAuthorHashes读回成功，3张Map hash全等。`final-protection.json`的20个保护文件全等、protectedFailures=0，Lab磁盘SHA为`7505DB59651C4C99077ACAF1EE91075364A916F300E742CDC559FE6045DA24D3`且不变。它证明磁盘文件/作者输入保护，不等于内存场景clean。

`final-owned-cleanup.json`记录确切ID −127298的临时Shader已destroyed=true，进程TMP/TEMP路由active=false。alreadyGone字段是在Destroy后的Unity假空求值，也为true，不能据此说它在清理前已经不存在。raw Console保留1条MCP任务取消错误，不声明全局0错误。

C盘仍剩余0字节，需要用户后续释放空间；本轮没有清理C盘或把TMP重定向当成功。此环境问题与已验证的地表修复分别记录。长时连续帧稳定、GPU/Player性能、实体鼠标与任意alpha边缘均不由上述有限测试包办。发布尚待root。

## 7. 实施顺序与出口

1. **复现与保护：已取得RED。** 保留源场与基准指纹、真实材质/MPB/相机条件及原始图。
2. **因果实验：已取得首因证据。** 原偏移不足、方向正确、Material数值确实影响结果；统一片元平面零层偏移原型已取得最小Green，继续比较范围与代价。
3. **生产窄修：已完成。** 仅同名同GUID Shader采用C step0、原queue/LEqual，无几何抬高；A/B实际生产无override的Bounded四组均Green。
4. **定向动态与遮挡：完成限定覆盖。** A/B活跃层Bounded四组、独立opaque绝对遮挡四组及原Orbit45°→315°正常Play/截图目检/返回Edit均完成；不签长时、所有alpha边缘或所有相机无闪烁。
5. **定向保护：完成；发布待root。** 三Map作者hash与初始相同、20保护文件全等、Lab磁盘文件未变；没有保存/重开/rollback用户场，退出Play后内存dirty=true保留。发布SHA另补，不使用占位。

每步以本轮回执更新[验证记录](validation.md)。原始媒体仅本机；文档总结与自有代码按任务发布边界处理。root独占Unity，本轮不执行电源动作。
