# 高台与悬崖样板

本轮采用方案B：侧壁是独立3D模型，顶面从模型的真实开放上沿生成。Tilemap保留为唯一刷地作者数据，顶面Mesh和站立Collider是可重建的派生结果。

## 本机入口

打开 `Assets/OSSASTRA/GroundWorkshop/Samples/KT-GROUND-03/SC_CliffEdgeLab.unity`。

使用菜单 `Tools > OSSASTRA > 高台与悬崖观察`。这里可以选平台、重取模型上沿、重建顶盖、打开原工坊刷地，以及保存整个样板或直接试玩。多平台场景必须使用这个保存入口；旧单地图另存/克隆试玩入口会在写入前拒绝。

- A为3米高凹L平台，含非整格缺口与两份可切换的已保存侧壁模型。
- B为5米高斜切平台，资源与surfaceId独立。
- 低地仍使用原生Tilemap，宽缓坡通往A。
- 原双角色、单敌、35°镜头、45°滚轮旋转和切控保留，敌攻击默认关闭。

## 刷地与换模型

1. 在高台窗口明确选择A或B，点击“刷当前平台”。
2. 在原工坊选择未锁的覆盖/道路层，Scene View拖刷。可使用原擦除、吸管、锁和Undo/Redo。
3. 配方的已有格替换、Future预览、取消和提交沿原工坊操作。显示同步消费真实临时Tilemap，不修改第二份格表。
4. A可切换侧壁版本并重新接合。缩小范围只裁显示，不删除域外手绘；恢复模型后仍可显示原数据。
5. 查看“装饰脚点检查”。失效物件保留原位置，明确列出高度、域外或退让不足原因；点击选中后自行调整。锁定和手摆物件不自动重排。
6. 保存整个样板，再直接Play。

Inspector直接更换MeshFilter引用会使连接环失效，旧正确缓存保留并提示重取，不自动猜测新环。正常样本版本切换同时更新其匹配的侧壁Collider；外部模型须自行提供正确的侧壁碰撞。

## 试玩与观察

沿角色前方缓坡可从低地走上A，实际Root脚点Y升至约3.02米。使用现有移动、切人和镜头输入；默认滚轮每档45°、PageUp/Down缩放、Z切控，最终以项目InputAction配置为准。Q/E技能不占用作旋转。

原F6/F7提供12°侧看，F6返回正常35°玩法镜头。高台窗口另提供成品、裸接缝、结构、拆开看和裁剪前对照。观察只改变显示，真实碰撞和作者根不移动，世界时间继续。窗口失焦、关闭、保存和Play转换会归还其拥有的观察状态。

## 结构与验证

- `GroundSurfaceModelReader`只读已保存模型，验证变换/上沿并计算来源指纹。
- `GroundSurfaceGeometry`负责临时焊接、闭环验证、凹域三角化和带UV/颜色的三角裁剪。
- `GroundSurfaceBinding`关联唯一作者Map、Boundary、侧壁与缓存。
- `GroundSurfaceMeshGenerator`读取真实Sprite几何/UV/颜色/矩阵，按表面、层、块和材质纹理分组。
- `GroundSurfaceUpdates`仅在变更时合并工作，以一次性Editor update回调处理，不依赖Inspector重绘，不在静止/转镜头时重建。
- `GroundSurfacePersistence`保存稳定的自有缓存资产；保存前退出本Lab的临时观察，不烘入遮罩调试。

完整证据与限制见 [validation.md](validation.md)，模型约定见 [model-contract.md](model-contract.md)。有限样本通过不代表任意模型、正式Player或GPU认证。严格像素对照保留11/12，一案有单像素world采样边界差异。

## 资源与公开边界

代码、候选验证源码、自有技术侧壁/缓坡几何和无参考纹理的墙材质可公开。原草路/花草、角色与其引用、含参考Sprite形状或UV的顶盖缓存、样板场景、截图和视频仅本机保留。公开仓库没有这些受限素材时不能据此声称一键复现本机美术场景；代码入口会明确拒绝缺失依赖。

本机教程位于 `E:/WORLDCREATOR/XingHaiHuiLang/研究/开拓物语地表/KT-GROUND-03/03-tutorial/index.html`，运行图片及40秒原生录像在同级 `02-visuals`。
