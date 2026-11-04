# 外缘后端定向验证

本目录脚本由Root通过Unity execute_code执行，不属于生产程序集，不在生产类型中加入测试分支。

- `CharacterOuterGlowGpuContract.cs.txt`：真实合成Shader，有限外扩、内部不追加、半径外零、1像素许可断层、前景深度、斜向supercover禁画。
- `CharacterOuterGlowSeedGpuContract.cs.txt`：真实Mesh+Seed Shader，冷暖/方向、熄灯（环境底光和最低可读度再高也不当光源）、强光预算、被墙挡住的源和墙截线不发光。

后端契约：每相机三张当前RT，实际target尺寸RGBA32F+R32F+RGBA16F共28字节/像素；2个独立人物顺序复用seed，先合并最近场景及透明人物深度，再作逐人物受光源和有限外扩。每帧重建，无历史、CPU读回或逐帧材质创建。新RT拒绝超过4096×2160和非2D目标。Render Scale换算采用当前真实target高度/1080，UV和完整覆盖Atlas沿用原缓存的ScreenRect，不把512槽像素当显示像素。

保护性限制：只认证Spine/Skeleton、UprightSpinePma、MapResearch/DepthPlane、UprightDepthPlane的MeshRenderer；外部Sprite和非Always stencil保守关闭该帧柔光并诊断。未知房间/窗口静态许可不等同动态人物许可，所有源、目标与supercover路径都必须通过RoomSightEyeContains及同owner限制。无真值许可和深度附件则不画。

环境处理：柔光在合法世界/许可生成之后生成独立能量层；有WorldLook时首次在Focus/Bloom之后、HUD之前加入，无Look在PositivePass内直接追加；源不再调用EnvironmentFogColor，之后WorldLook按目标合法环境深度处理空气一次。该方式是目标深度近似，不宣称三维空气散射。C诊断只清世界颜色，保留已生成深度与许可，不代替普通A/B实景。

Root Native专项GREEN，含薄近景容差RED→4ULP GREEN与WorldLook重扩散RED→延后首次加入GREEN；详任务与交付记录。

