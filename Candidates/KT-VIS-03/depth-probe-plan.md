# KT-VIS-03 模型截断：Native像素反馈环候选

2026-11-01。子代理只读生产源码、作者脚本与旧验证；未运行Unity/Git，未修改生产。实际Root复现和因果判定仍须以Native回执为准。

## 已交付的最快反馈环

`PropPixelDifferential.cs.txt`，Root通过项目原execute_code执行`Vis03PropPixelDifferential`。硬门只允许当前CRAFT Main GUID、正常Play、`kt-vis03-01`以及明确的KT-VIS-03/profiles目录。不要为此开旧场或默认profile。

Root先设置`SessionState.SetString("KT-VIS-03.Output", <已存在本轮证据目录>)`。可选`KT-VIS-03.PropProbe.Ids`默认`stone-a,plant-a`，1—4个明确的Sprite型WorldProp。Root负责用原Motor把角色摆在物体后方并等待透视状态稳定；探针不移动角色、不造伤害、不Grant、不改存档。

一个同步调用只渲染同一帧、同一原GameplayCamera及投影：

1. 完整世界，效果off/on各一张。
2. 全场Renderer关闭的空底图。
3. 每个实际原生Sprite单独一张；原Sprite+实际GroundSurfaceMeshCache.chunks一张。
4. 用独立原生Sprite图的3×3内缩不透明像素作样本，原生图与空底图也须有颜色差，以免URP强制alpha=1污染全屏mask。至少16样本，linear Half颜色容差4/1024。

记录`groundMismatch`（只加实际地表是否切掉模型）、`offMismatch`（完整世界正常遮挡，可能合理）、`onVsOff`（透视/后端是否改变小物正常遮挡）和PNG、实际sprite bounds/pivot/世界顶点Y等。预置“物体应完整显露、角色在后”的场景合同应为`groundMismatch=0`、`onVsOff=0`；不能从“没有异常”或“有PNG”直接给全视觉PASS。先看原生Sprite单独图是否本来就是短/残株图。

finally恢复本次改过的Renderer.enabled、原相机target/aspect/projection/clear/background、effectEnabled和RenderTexture.active；销毁自有RT/读取纹理。不会保存源场或写任何Unity资产，媒体/JSON只落本机证据目录。探针本身未由子代理Native编译/执行；Root须确认返回error为空与恢复字段true。

## 复现后应分别证伪的预测

- 单独Sprite完整，加实际地表后截断：可归入地表/源几何深度关系，继续用相同参考图与地表最小集合追深度，不改透视作为补偿。
- effectoff完整，effecton变短/消失：进一步区分资格/WholeFade与有限捕获A/A；远屏透视圆强制真实capture但不覆盖物体，可证明后端复制是否保真。
- source-only本来就短/残株：查选图状态与作者crop，不能写Shader“修复”原有美术状态。
- Sprite分支都正常而白色立体块仍截断：单独检查`mesh-a`真实MeshRenderer及原材质；本候选明确只接受Sprite，不借图鉴图标解释立体几何。

## 可复用旧Native合同

- `Candidates/KT-VIS-01/sprite-contracts/SpriteGroupContracts.cs.txt`：生产FiniteGroupCapture，独立原生SpriteRenderer参照，远屏circle强制A/A真实capture；已有alpha/tint/flip/动态Sprite、两层非交换TRS及Native纹理绑定测量。Half 4/1024，最少64样本。
- `Candidates/KT-VIS-01/backend-contracts/BackendVisualContracts.cs.txt`：私有PreviewScene/生产后端、原opaque独立图与CPU三角深度排序，适合后端A/A单变量缩小；需要已存在原SessionState路径，不应把旧场硬门绕开。
- `Candidates/KT-VIS-01/upright-depth/UprightDepthContracts.cs.txt`及`ActualActorVisualContracts.cs.txt`：冻结正式Spine网格、只替换自有材质，实际墙几何逐像素射线oracle。用于角色身体确实被切时，不能以它替代WorldProp测试。

## 源码事实（不是本轮因果结论）

- WorldPropsRoot对faceCamera视觉只写Y旋转，WorldPropView短时写独立Body localRotation摇动，未移动逻辑根。
- WorldProps原作者脚本`ResumeInstall.cs.txt`实际选`grass-states-idle_dead.png`，即DS死亡/残株状态帧；截图里的黑短株可能原图即如此，必须看source-only图，不预判丢顶点。
- SceneVisibilityRoot消费显式组；每帧组局部Strength会乘`1-minimumOpacity`。allowWholeFade只控制整组淡出资格，并不单独禁用局部透视，所以只把allowWholeFade关掉不足以使小物完全不参与透视。
- 实际DecorationCutout原生shader用普通栅格深度，source RGB来自_MainTex/顶点_Color；FiniteGroupCapture Sprite分支使用明确原生Sprite纹理/几何重建，配套OwnedGroupDeviceDepth mode3。历史A/A通过不能替代当前参数与深度回归。

该子任务代码/笔记租约交还Root；没有任何生产修复或测试成功声明。
