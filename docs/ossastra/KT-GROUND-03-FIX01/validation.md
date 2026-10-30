# KT-GROUND-03-FIX01 验证记录（定向验收收口，发布待录）

当前状态：**真实RED及因果对照已取得：共面后的原偏移裕量不足，方向正确且Material状态会影响结果。最终采用C：同plane/pixel统一重建、零层偏移/原队列LEqual。生产仅窄改同名同GUID Shader，Bounded四组累计2207934有效样本、wrong0/unknown0；独立opaque绝对遮挡4组已通过；正常Play原Orbit45°→315°实际画面地表/角色/墙正常，已返回Edit；三Map作者与20文件保护通过。最终内存dirty=true保留，发布待root。**

本机证据根为 `研究/开拓物语地表/KT-GROUND-03-FIX01`。旧KT03通过数不移作本轮通过；尤其55点几何接合、八角静帧和资源稳定不能代签多层动态显示。用户方案与选择规则见[fix-plan.md](fix-plan.md)。

## 已取得的证据

`01-evidence/current-state.json`：Edit、无PrefabStage、D3D11/Tuanjie2022.3.62t16。A/B的flattenDisplay为true，实际缓存全部层分别统一世界Y3/Y5；原生Renderer有效显示0。层0/1的queue2450/2451，材质与MPB DepthUnits0/−1、DepthFactor0。属性读回不等于已经证明固定渲染状态生效。

`01-evidence/layer-order-first-result.json`与`01-evidence/LayerOrderA01_016f2c1e50d643c2bef30142701195cd/layer-order.json`：真实A层0+1的受控Preview副本，保留Mesh/完整TRS/原Shader/材质及MPB。3机位×Single0/Single1/Pair，共9次渲染；不是9机位。结果RED、error=null、everyPoseValid=true：

| 机位 | 有效内部样本 | 正确上层 | 错误下层 | 未归类 |
|---|---:|---:|---:|---:|
| −0.001m | 12,666 | 11,930 | 736 | 0 |
| 原位置 | 12,674 | 10,534 | 2,140 | 0 |
| ＋0.001m | 12,659 | 9,919 | 2,740 | 0 |
| 合计 | 37,999 | 32,383 | 5,616 | 0 |

总时0.5960073秒；384×256、yaw315°、pitch35°、distance25、FOV35、near.05/far180，Preview无MSAA/HDR。两单层oracle在相同机位独立渲染；5×5内部邻域、contrast20/channelTolerance2/minSamples128，三个机位均有效。测试已区分“错误下层获胜”与无法归类，不能把5616说成未知差异或alpha边缘差异。它也只覆盖这两个层、这些机位及有效样本；不覆盖所有像素/所有平台。

测试条件中sourceDirtyBefore/Now均false、TimeScale前后1、Preview角色组件0；source未Save、未Rebuild、未切角色或写TimeScale。更早current-state为dirty=true，原因不能由本回执推出，不据此回滚用户现场。第一轮只记RT请求nominal24；后续实际读回D32_SFloat_S8_UInt、32位浮点，显式D32原参数仍5616错误；该首轮基线并非D24；后续真正D24生产覆盖另列。

## 因果对照（当前两层/三机位）

| 回执summary | 有效样本 | 错误下层 | 未归类 |
|---|---:|---:|---:|
| original-revised-default | 37,999 | 5,616 | 0 |
| units16-negative | 37,999 | 1,908 | 0 |
| units64-negative | 37,999 | 651 | 0 |
| units256-negative | 37,997 | 0 | 0 |
| units16-positive | 37,998 | 33,276 | 0 |
| units64-material-only | 37,999 | 651 | 0 |
| original-no-depth-mpb | 37,999 | 5,616 | 0 |
| original-depth32 | 37,999 | 5,616 | 0 |
| height-half-mm | 37,980 | 0 | 0 |

各项均由`01-evidence/<名称>-summary.json`链接原始render回执；sample差异如实保留，未改opaque/contrast判据或把unknown算正确。后续配置确认请求nominal24与显式D32均创建D32_SFloat_S8_UInt；原问题不是实际D24升到D32才消失。×256与0.0005m仅这两层/三机位Green，不预签多层、12°、距离、角色/侧壁遮挡或最终生产修复。最终采用C为同plane/pixel统一片元平面深度、额外层偏移0、原队列/LEqual；0.0005m是对照而非默认。正式机制依据与利弊见fix-plan。

## C零层偏移原型与多层受限集合

| 回执summary | 状态 | 有效样本 | 错误下层 | 未归类 | 范围 |
|---|---|---:|---:|---:|---|
| unified-plane-zero-step | harness ERROR | 0 | 0 | 0 | 临时Shader名字查找失败；没有渲染，不是Green |
| unified-zero-id | GREEN | 37,996 | 0 | 0 | 修TransientShaderId查找后，C原型step0、两层/三机位 |
| multilayer-original-A | RED | 72,009 | 19,093 | 0 | 原路径多层A、TwoViews受限集合 |
| multi-unified-A | GREEN | 72,005 | 0 | 0 | C原型step0，多层A/TwoViews受限集合 |
| multi-unified-B | GREEN | 97,281 | 0 | 0 | C原型step0，多层B/TwoViews受限集合 |

C从同一平台平面、同一像素路径重建片元深度，沿用原队列与LEqual；不把0.0005m米制/几何微偏移作为默认。后续实际生产Bounded矩阵及创建的D24/D32 RT另列；标准opaque绝对遮挡及明确几何的MSAA另有4组通过；正常Play原Orbit45°→315°及返回现已完成；仍不签任意长时/所有相机无闪烁。表中原型Green只签各自集合，不签所有相机无闪烁。

## 生产C与真实创建RT格式的Bounded矩阵

生产仅修改同名同GUID的`GroundThinTop.shader`：隐藏deprecated旧DepthUnits/Factor兼容字段、Offset0、同pixel水平plane的SV_Depth，生产无ViewDepthOffset或几何抬高。作者格/缓存Mesh/材质引用身份不变，无需重建或重序列化材质，故不改cache签名/生成版本。`prepare-edit-ready.json` operation f4a38be…为ready、CS compilation_errors空。

| summary / runId前缀 | 平台 | 机位 | raw实际创建深度格式 | 有效样本 | wrong / unknown |
|---|---|---:|---|---:|---:|
| production-A-D32 / 6f2d41cb | A | 28 | D32_SFloat_S8_UInt | 463,725 | 0 / 0 |
| production-B-D32 / 0a2c5025 | B | 28 | D32_SFloat_S8_UInt | 640,242 | 0 / 0 |
| production-A-D24 / b9e808e6 | A | 28 | D24_UNorm_S8_UInt | 463,725 | 0 / 0 |
| production-B-D24 / 7a8560c5 | B | 28 | D24_UNorm_S8_UInt | 640,242 | 0 / 0 |
| 合计（两格式重复采样） | A/B | 每组28 | 两种RT格式 | 2,207,934 | 0 / 0 |

四组均GREEN、everyPoseValid=true、shaderOverrideName空/shaderOverrideId0，是实际生产Shader无override的渲染。A活跃[0,1,2,6]、coveredHighLayers[1,2,6]；B活跃[0,1,2,4,6]、coveredHighLayers[1,2,4,6]。Single/Prefix和移除上层后的最高存活下层oracle使用原opaque/contrast判据，并有empty frame验证clear alpha。创建的RT格式是raw读回；不把它冒称已GPU抓取URP内部attachment。

早先bounded-unified的D24输入漏应用，raw仍D32，**不作D24覆盖证据**。修夹具强制检查后exact-D24-unified-A/B raw实际D24；生产D24两组也实际D24。这四组不代签下述独立opaque绝对遮挡/MSAA、正常Play或最终保护。

## 独立opaque绝对遮挡：4组184机位

下列4个`01-evidence/occlusion-prod-*-result.json`实际GREEN，格式与MSAA创建后读回符合请求。每组46机位＝23透视＋23正交；pitch12/35/70、distance5/25/32及八角方向，有界集合而非所有参数组合。标准opaque参考与生产地表的明确水平面/前后立卡对照，planeY3、gap0.3，白纹理/蓝色地表；每机位前后重叠各≥64，全部valid。

| 回执 | raw实际RT深度 | 实际MSAA | 内部样本 | front样本 | behind样本 | interior/front/behind错误 | strict全图差之和 |
|---|---|---:|---:|---:|---:|---|---:|
| occlusion-prod-D32-AA1 | D32_SFloat_S8_UInt | 1 | 1,055,285 | 24,940 | 19,937 | 0 / 0 / 0 | 0 |
| occlusion-prod-D32-AA4 | D32_SFloat_S8_UInt | 4 | 1,038,576 | 23,507 | 18,457 | 0 / 0 / 0 | 0 |
| occlusion-prod-D24-AA1 | D24_UNorm_S8_UInt | 1 | 1,055,285 | 24,940 | 19,937 | 0 / 0 / 0 | 0 |
| occlusion-prod-D24-AA4 | D24_UNorm_S8_UInt | 4 | 1,038,576 | 23,507 | 18,457 | 0 / 0 / 0 | 0 |

共184组机位采样，内部样本4,187,722。MSAA显式resolve后再ReadPixels；opaque明确几何不仅内部0错，strictAllPixelsMismatch全帧之和也为0。这不是所有草叶alpha边缘的MSAA证明，亦非URP内部附件GPU抓帧。

## Shader-only定向回归与Play进度

生产Shader errors=false/messages=[]，GUID保持`54b11adfa486615458f1c071053ec790`。三个Map authorHash与初始相同，最终保护20/20、3张Map hash全等现已取得。本修复只改Shader，不改作者、生成、材质身份或保存路径，后续以实际无override渲染、遮挡、现有Play/原Orbit、authorHash/源指纹和最终引用检查作为定向回归。**不主动写作者笔画、不保存或关场重开用户场景**来凑全套旧测试。

`prepare-play-ready.json` operation860fc042…实际ready、进入Play。`runtime-capture-01.json`是真实Game View原1600×900输出1280×720，初始45°画面已由root目检地表/角色正常；不是实体鼠标或Editor桌面截图，也不是全程稳定认证。原Orbit两次RequestStep(-1)经正常Update使45°过渡至315°，`runtime-capture-315.json`的实际运行画面已由root目检地表/角色/墙正常；不是直接改相机Transform。退出Play prepare operation f22bad2e…实际ready。

`runtime-errors.json`的MCP getter返回未找到error，但它可能过滤工具自身错误。随后`final-raw-console.json`原始Console实际count1：Funplay MCP工具任务取消error；因此不能写全局0错误。所见这条栈来自工具链，不据此扩修角色音画或插件。

## 环境事件（与渲染首因分开）

本机C盘剩余0字节，一次poll编译发生IO112（空间不足）。root沿原run恢复后D24-AA4为GREEN，原回执不改成“从未失败”；这是工具/环境中断，不拿来解释此前已经复现的地表层序错误。

曾只对Unity进程尝试TMP/TEMP重定向E；`temp-route.json`读回Path.GetTempPath仍为C盘。`temp-route-restored.json`确认进程环境已恢复，不能声称成功迁移临时目录；没有删除C盘文件。随后小Native请求仍可成功不代表磁盘问题已解决，磁盘来源/清理不在本修复范围内。

## 最终Edit现场与保护

- `runtime-verification.json`：生产Shader isSupported=true/errors=false/messages=[]，GUID未变；3个authorHash与初始相同，实际缓存min/maxY仍A3/B5。
- `final-scene-info.json`：退出Play后Edit，仅1个原Lab，**isDirty=true**。Play生命周期自动生成cache，A rebuildCount91→92、B55→56；本轮未手动重建/保存/清dirty，不声明没有发生重建。
- `final-verification.json`：全量Native检查在输出Snippet.dll时IO112失败，是C盘空间事件，不记成已通过。随后最小`final-author-hashes.json`成功，3张Map hash均与初始一致。
- `final-protection.json`：20文件全等、protectedFailures=0；labFileEqual=true，磁盘SHA `7505DB59651C4C99077ACAF1EE91075364A916F300E742CDC559FE6045DA24D3`不变。没有保存、重开或rollback用户场景；磁盘保护不代表内存clean。
- `final-owned-cleanup.json`：确切ID −127298临时Shader实际destroyed=true，processTempRouteActive=false。alreadyGone是在Destroy后求Unity假空而为true，不据此说清理前已gone。
- 原始Console仍是1条Funplay MCP取消error；不宣称全局zero。C盘仍0字节，需用户后续释放空间；来源/清理与本渲染修复分别处理。

本轮修复与定向验收已收口，发布待root。没有写入任何占位SHA。下表保留限定覆盖或未测，不把最终收口解释成任意运行情形全部通过。

## 本轮验收矩阵

以下为新增判据，不把“待验”写成通过。最小夹具沿用原阈值；需要修改判据时必须另立理由并保留原结果。

| 编号 | 范围与通过标准 | 当前状态 |
|---|---|---|
| F01 | 用实际缓存、原有效材质/MPB复现用户条带；机位、格式、样本有效性与保护完整 | **RED已复现**：37999有效样本、5616错误下层、unknown0；后续真实RT为D32浮点（非实际D24） |
| F02 | H1幅度、H2符号/reversed-Z、H3参数生效分别作单因素对照；说明可排除与未区分项 | 已支持原幅度裕量不足；反向恶化、Material-only/清MPB排除方向与MPB首因；最终GPU raster state未直接抓取 |
| F03 | 原3机位最小夹具保持相同opaque/contrast/内缩/颜色容差，错误下层0、unknown0、每机位样本≥128 | 最终生产C零偏移已选；实际无Shader override的Bounded四组wrong0/unknown0，最小原始RED继续保留 |
| F04 | A/B当前实际非空叠层与上层优先关系正确；不只层0+1；包含跨chunk区域 | 原A多层RED保留；生产A活跃[0,1,2,6]、B[0,1,2,4,6]均覆盖所有当前高层，Bounded28机位×D24/D32四组全Green；空层不冒称已覆盖 |
| F05 | 正常35°、低角12°，八档及档间连续转动、用户问题缩放和小幅平移；地表内部不错误翻层 | Bounded预设机位已通过；原Orbit45°→315°正常Update过渡及前后实际截图目检正常；不是逐帧全场错误分类，不用运动帧简单全帧相等作唯一判据 |
| F06 | 固定相机连续帧的地表自身稳定；alpha边缘/显露区域单列，不通过扩大排除区掩盖问题 | 本轮未另做长时固定相机连续帧像素分类；已有微移/Bounded与正常Play有限证据，不签长时稳定 |
| F07 | 标准opaque绝对遮挡及正常角色/墙/凹口；不永久顶层、不ZTest Always、不浮层 | 独立opaque四组184机位/4187722内部样本/前后与严格全帧错误均0；真实Play45°与315°截图正常、已返回Edit；不代签任意角色战斗/长时遮挡 |
| F08 | 原物理接合/Collider/角色Root不移动，凹口不填平；两平台渲染修复保持真实站立高度 | 无几何抬高；运行缓存min/maxY仍A3/B5，作者hash与Lab磁盘不变。未重新执行55点，不搬旧计数签本轮 |
| F09 | Shader-only不改作者/更新路径，检查原输入身份与只读authorHash、现有运行显示直接生效 | 三Map authorHash相同；本轮不主动写作者/刷改撤销，不把旧闭环计数搬过来 |
| F10 | Shader-only保持同名GUID/材质引用，重载及Play后现有资源可直接显示；不重序列化或保存用户场 | Shader GUID保持、prepare Play经重载ready，实际Game View已出图；不保存/关场重开用户场景，runtime Shader supported/errorsfalse、原GUID/材质名称与共面Y读回；返回Edit保留dirty，未强制重存 |
| F11 | 静止/转镜头不重建顶盖，Renderer/Mesh/Material数量稳定；原低地/旧场/角色配置及用户dirty保护 | 三Map输入/20文件指纹通过；Play生命周期A91→92/B55→56自动生成缓存，未手动Rebuild，不声称全程0重建；本轮未另跑长时资源稳定采样，GPU/Player未测 |
| F12 | 原始RED→因果对照→生产Green→动态回归→最终正常场/发布SHA | C生产Bounded与独立opaque/MSAA四组Green；原Orbit45°→315°、返回Edit、作者/文件保护及临时Shader清理完成；发布SHA待root，有限/未测项保留 |

## 必须保留的限制

- 已支持当前两层共面后的原深度偏移裕量不足；方向正确、Material-only影响像素结果，MPB未应用不是当前首因。仍未直接抓取GPU raster state，不声称所有API、距离、三角形都成立。
- 基线请求nominal24实际D32；后续exact-D24和生产D24 raw才支持真实创建的D24。早先仅传参未应用的D24结果作废为格式证据；URP内部depth attachment未GPU抓帧。原层序Preview无MSAA/HDR；独立opaque四组有真实MSAA1/4，但不代签所有alpha边缘或正常游戏画质。
- 孤立Preview可以建立因果，最终需要正常运行原场复验，不能只用groundOnly或关闭问题层收口。
- 原KT03严格Native/Cache像素11/12、GPU/Player/实体鼠标与人工审美未验边界继续保留；本轮也不声称全局Console零错误。
- 源媒体/包含原Sprite形状与UV的派生输出仅本机；root独占Unity并按协议更新任务同步。本轮不保存/改作者原场、不改角色配置或渲染管线来扩大任务。
