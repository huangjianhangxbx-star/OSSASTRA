# DS-MAP-02 小蓝局部闪烁调查

记录日期：2026-10-29。当前结论：**已修复确认的研究场共面深度渲染缺陷，修复后固定快照回归稳定。** 用户原先报告的全部动态或独立装备闪烁尚未完成对应复现与回归，不能据此宣布全部闭环。

用户描述的是角色局部或装备的闪烁。整具身体的网格仍存在，不能排除其中一部分画错、被遮挡或装备单独异常。本轮保留原方向、显隐与Shader行为，先建立反馈环，再在研究装配层修正已取得因果证据的片层深度与bounds。

## 已取得的证据

本机证据目录为 `E:/WORLDCREATOR/XingHaiHuiLang/研究/饥荒地图与视角/DS-MAP-02/01-baseline/`。公开仓库只保留调查说明与自有诊断代码，不包含原作素材或完整运行记录。

| 证据 | 已确认的结果 | 不能据此认定的内容 |
| --- | --- | --- |
| 原单人场 `orbit-baseline.json` | 约17秒顺逆旋转故事；9,631次真实Spine网格回调；探针空网格判定0次，状态 `UNREPRODUCED_CPU_MESH` | 不能排除局部、装备、深度或最终GPU画面异常 |
| 同场带录像 `orbit-with-video.json` | 6,973次真实网格回调；空网格判定0次；实际yaw覆盖约0至359.95度 | 不能把CPU摘要当成屏幕可见性证明 |
| 30fps游戏录像及少量抽帧检查 | 已查看的少量帧未发现整身消失 | 没有逐帧审阅整段录像；30fps录像也不能覆盖更高游戏帧率中的每个瞬间 |
| 新诊断合同 `diagnostic-gpu-results.json` 中 `DiagnosticContracts` | 隔离复测20/20通过；8次正式mesh事件产生24条记录；24条均关联原party会话的 `temporary-a` / revision 11；idle相位为空，采样错误0 | 验证的是诊断功能和记录关联，不是用户闪烁已经复现或修复 |
| 早期无效GPU夹具尝试 | 返回 `GPU_FIXTURE_ERROR`，错误为 `Snapshot mesh topology mismatch`，`userFlickerClosed=false` | 这次A/B无效；不能把快照夹具错误写成角色网格根因或材质结论 |
| 修复前新场独立重跑 `gpu-fresh-results.json`，唯一版本的 `PartialFlickerGpuProbe` | `MODEL_SNAPSHOT_DEPTH_CANDIDATE`；同一次正式姿态/网格通过等价检查。A的0间距在微小相机变化下最多5,124/22,558个内部像素差值达到32；B的-0.0001间距对应计数0 | 证实此研究材质与共面深度组合的局部像素不稳定；采样当时尚未应用生产修复，不是正式游戏动态或独立装备复现 |
| `spine-depth-green.json` | 真实Unity Mesh合同7/7通过：修正负Z bounds，手算范围吻合，包含全部样例顶点，vertices/UV/colors/triangles精确不变 | 不替代正式事件生命周期及真实动态画面验收 |
| `install-depth.json` | 新场3个Renderer实例与已有研究Variant的1个身体安装研究深度组件；间距-0.0001，原单人研究Scene文件未变 | 不代表改动正式基础Prefab或全局Spine/Shader |
| 修复后 `gpu-fixed-results.json`，唯一版本的 `PartialFlickerGpuProbe` | `MODEL_SNAPSHOT_STABLE`；实际源间距-0.0001，严格引用网格等价检查通过，实际bounds Z extents约+0.0017；六个偏移中两组最高阈值差异像素计数均0 | 固定姿态回归通过，不能扩展为用户全部动态/独立装备问题已关闭 |

旧旋转探针曾从角色根查找方向组件，因此其 `face=none` 列没有选面证据价值。新记录器沿正式 `CharacterAnimationController.Skeleton` 端口取得身体，并关联同控制器的方向组件，避免重复这一取样错误。

诊断首次整合运行曾出现17通过、3失败。三个失败来自动态测试DTO经 `JsonUtility.FromJson` 取不到样本后的连带误判。改用 `JObject/JArray`，在存活、隔离且不并行切控的条件下复测后20/20通过；为这次误判没有追加生产修正。

早期GPU快照错误之后，一次后续运行（第三次）又因fresh builder小修导入触发Play域重载/停止，动态结果丢失。这次也不计入有效A/B。有效结果取自后续新场专门重跑，不能混用此前残留Snippet的静态结果。

## 已确认的快照渲染缺陷

`gpu-fresh-results.json`中被采样的 `temporary-a/MainBody` 为 `backLeft`、镜像状态。姿态、正式instruction与网格帧号均为2640；`sourceMeshMatchesGeneratedA=true`。正式网格有375个顶点容量，但864个三角索引只引用308个顶点，另67个顶点未参与绘制。

A沿实际正式instruction和同一生成分支重建0间距网格，并与真实网格的索引及被引用顶点属性核对。B沿相同instruction仅把独立MeshGenerator的 `zSpacing` 改为-0.0001；索引、XY、UV、顶点色等价检查通过。没有推进原骨架、混合不同姿态或修改真实场景相机。快照相机仅收窄FOV以放大观察，输出512×512。

对同一姿态快照施加相机测试偏移0、-0.01、-0.001、+0.001、+0.01、0，比较共同内部区域（向内收缩3像素，共22,558像素），差值阈值为32：

| 偏移 | A：0间距，达到阈值的像素 | B：-0.0001间距，达到阈值的像素 |
| --- | ---: | ---: |
| -0.01 | 5,124 | 0 |
| -0.001 | 1,987 | 0 |
| +0.001 | 1,843 | 0 |
| +0.01 | 4,379 | 0 |
| 再次回到0 | 0 | 0 |

再次回到0时，两组均逐像素相同。非零偏移下B仍有小于32的细微差异，不能表述成所有像素完全不变。该受控结果确认了 **DepthPlane研究材质与0间距共面片层组合** 的局部渲染不稳定，并给出深度间距候选。它尚不能证明用户所见动态或独立装备现象完全来自这一缺陷，也不证明全程无闪烁。修复前结果保留 `userFlickerClosed=false`、`productionFixApplied=false`。

## 研究范围的修复与回归

新增自有 `MapResearchSpineDepth`，只在明确装配的研究实例生效：启用时保存本GO显式SkeletonRenderer的原间距，设为-0.0001；随该实例官方网格完成事件对实际sharedMesh执行 `RecalculateBounds()`。没有增加一次骨架推进、创建额外Mesh/Material，或修改第三方Spine与Shader。禁用时退订；只有间距仍保持自身写入值时才恢复自己曾改变的原值。

本机Spine的负间距生成分支会给出负Z bounds extents；不能仅设负间距而留下这个裁剪风险。真实Mesh缩小合同已7/7通过，包含顶点、手算min/max（1e-6浮点容差）、精确绘制数据不变等检查。正式实例由每次真实网格事件重算范围，本机未引用尾部顶点重复最后顶点，不需要任意扩大的包围盒。

修复后快照取自实际 `temporary-a/MainBody`，仍为 `backLeft` / 镜像，姿态、instruction、网格帧号均为15520。实际源 `settings.zSpacing=-0.0001`，`sourceHadNonzeroSpacing=true`，严格网格等价检查通过；实际bounds中心Z为-0.0095、extents Z约+0.0017。独立重新生成的裸快照仍会带原生成器的负bounds，而正式源网格已被研究组件修正，这两者不能混用。

本次共同内部区域22,459像素，仍使用阈值32、相同六个相机偏移，`worstA=0`、`worstB=0`。两组此时都使用-0.0001间距，因此这是修复后真实源的固定快照回归，不是另一轮0对负间距的因果A/B。微小偏移仍有低于32的差异，重复0偏移最大差为0。

夹具不负责应用场景修复，其遗留字段 `productionFixApplied=false` 为固定报告位；不能用它否定安装回执或实际源配置。`fixtureModifiedSource=false`、实际源非零间距和正bounds共同说明，本次夹具未修改源对象来制造通过。`userFlickerClosed=false`继续保留，表示用户全部动态/独立装备现象没有据此自动关闭。

## 取样位置与证据边界

记录器只读已有角色链，订阅 `BeforeApply`、`UpdateComplete`、`OnMeshAndMaterialsUpdated` 和URP相机开始/结束事件。它不调用Spine的Update或Apply，不另推进动作、输入、镜头或动画时钟。

`UpdateComplete`阶段单独记录姿态slot alpha摘要；网格完成事件读取实际生成网格的顶点色alpha。原显隐逻辑会在网格完成后恢复slot alpha，事后读slot alpha不能代表刚生成的网格。两类摘要因此保留各自阶段与帧号。

每条记录包含角色、身体、骨架身份哈希、网格与Renderer实例，实际/目标yaw，选面与镜像，主轨/结构轨，动作/相位及bounds、材质摘要。相机阶段沿用最近一次网格摘要，保留 `meshFrame`，并另记实际触发回调的 `renderCameraId` 与期望研究相机 `expectedCameraId`。这些字段用来关联阶段，不能证明Renderer最终被GPU画到屏幕。

实际销毁身体故事曾证实，相机事件会继续把已销毁身体的旧摘要标成新事件帧；该独立诊断缺陷已加入最小存活guard，相机阶段跳过角色或身体已销毁的绑定。RED为7通过1失败（`diagnostic-destroyed-red.json`），修复后同一真实Play故事GREEN为8/8（`diagnostic-destroyed-green.json`）：旧身体的新帧样本从6降至0；重新绑定另一个真实存活身体取得网格，再停止并继续3次真实相机事件，mesh与sample计数均不增加。此诊断准确性修复不等于用户闪烁根因。

`currentActorId`、`controlRevision`直接读取 `map.sessionComponent` 所接原 `MapResearchPartyBridge.coordinator`；记录器不维护第二个Current。原单人场明确记作 `single`、revision -1。相位只有在动作活动、ActionId非空且控制器捕获的ActionId吻合时才有效，idle不带旧相位。

当前记录对象是正式身体，独立Spine装备不会被误认成身体。它也没有逐slot或逐三角形证明某个局部始终可见。全身顶点数与alpha汇总正常，仍不足以排除局部或独立装备问题。所有报告固定写入 `gpuVisibilityVerified=false`。

还需注意：现CPU alpha摘要统计整个顶点缓冲，包括Spine留下的未引用尾部容量。尾部非零alpha可能掩盖实际三角索引所引用顶点全为0的情况。因此9,631与6,973次的“空网格判定0”只描述当时探针判定，不能排除被绘制部分的alpha异常。后续诊断准确性修正应以最终网格实际索引引用集合为准，保留容量与引用数量的区别；当前代码仍冻结，尚未追加此修正。

本次基线使用D3D11与现有URP研究场。最初读到身体 `zSpacing=0`、研究深度材质开启ZWrite；单独读取配置不是因果证据，后续同姿态GPU对照才提供本项深度缺陷的证据。

## 后续反馈环

1. 在同一存活角色、装备、相机和动作条件下，先捕捉用户所指局部的连续画面，并标记对应帧附近的记录。
2. 保留本次同姿态、索引及被引用属性等价检查与修复后真实源快照结果；继续真实Play动态复验。
3. 验证顺逆旋转、移动、动作、两角色切控与结构观察，并检查部位完整性、装备、世界遮挡及恢复前后的对应表现。一次只改一个变量，不把快照结论扩成全局管线结论。
4. 用户原现象未对应重现或未完成修复回归时，继续明确未关闭该问题；若动态候选失败，保留失败并继续定位。

本报告已纳入修复前因果GPU对照与修复后真实源快照回归；更广动态/装备复验结果取得后再更新。原游戏现场启动受阻的 `OBSERVATION_BLOCKED` 仍是DS-MAP-01原作对照的限制，不能用Unity本轮画面替代原作运行证据。

操作入口见 [diagnostics-guide.md](diagnostics-guide.md)。
